using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Tests;

public class ImportPersistenceTests
{
    [Fact]
    public async Task Commit_PersistsNewEntities_WithoutActivatingSeason()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var store = new SqliteDataStore(context);
        var baseline = new LeagueData();
        var imported = new LeagueData();
        var season = new Season { Name = "Imported" };
        imported.Seasons.Add(season);
        imported.Teams.Add(new Team { SeasonId = season.Id, Name = "Team" });

        await store.CommitImportAsync(baseline, imported);

        context.ChangeTracker.Clear();
        Assert.False((await context.Seasons.SingleAsync()).IsActive);
        Assert.Equal("Team", (await context.Teams.SingleAsync()).Name);
    }

    [Fact]
    public async Task Commit_UpdatesOwnedJson_WithoutNullingTeamVenue()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var season = new Season { Name = "Season" };
        var venue = new Venue { Name = "Pub", SeasonId = season.Id };
        var team = new Team { Name = "Team", SeasonId = season.Id, VenueId = venue.Id };
        context.AddRange(season, venue, team);
        await context.SaveChangesAsync();
        var baseline = new LeagueData { Seasons = [ImportWorkspace.Clone(season)], Venues = [ImportWorkspace.Clone(venue)], Teams = [ImportWorkspace.Clone(team)] };
        var imported = ImportWorkspace.Clone(baseline);
        imported.Venues[0].Tables.Add(new VenueTable { Label = "New table", MaxTeams = 2 });

        await new SqliteDataStore(context).CommitImportAsync(baseline, imported);

        context.ChangeTracker.Clear();
        Assert.Equal(venue.Id, (await context.Teams.SingleAsync()).VenueId);
        Assert.Equal("New table", Assert.Single((await context.Venues.SingleAsync()).Tables).Label);
    }

    [Fact]
    public async Task Commit_InvalidForeignKey_RollsBackWholeImport()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var season = new Season { Name = "New" };
        var imported = new LeagueData { Seasons = [season], Teams = [new Team { Name = "Bad", SeasonId = Guid.NewGuid() }] };

        await Assert.ThrowsAsync<DbUpdateException>(() => new SqliteDataStore(context).CommitImportAsync(new LeagueData(), imported));

        Assert.Empty(await context.Seasons.AsNoTracking().ToListAsync());
        Assert.Empty(await context.Teams.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Commit_LockedSeason_RejectsChanges()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var season = new Season { Name = "Locked", IsLocked = true };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        var baseline = new LeagueData { Seasons = [ImportWorkspace.Clone(season)] };
        var imported = ImportWorkspace.Clone(baseline);
        imported.Teams.Add(new Team { Name = "Not allowed", SeasonId = season.Id });

        await Assert.ThrowsAsync<InvalidOperationException>(() => new SqliteDataStore(context).CommitImportAsync(baseline, imported));
        Assert.Empty(await context.Teams.ToListAsync());
    }

    [Fact]
    public async Task Commit_ConcurrentChange_RejectsStalePreview()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var season = new Season { Name = "Original" };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        var baseline = new LeagueData { Seasons = [ImportWorkspace.Clone(season)] };
        var imported = ImportWorkspace.Clone(baseline);
        imported.Seasons[0].Name = "Imported edit";
        season.Name = "Someone else's edit";
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new SqliteDataStore(context).CommitImportAsync(baseline, imported));
        Assert.Equal("Someone else's edit", (await context.Seasons.AsNoTracking().SingleAsync()).Name);
    }

    private static LeagueContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options);
}
