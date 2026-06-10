using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for SqliteDataStore bulk operations — ReplaceFixturesForSeasonAsync
/// and AddSeasonEntitiesAsync (used by fixture generation and season copy).
/// </summary>
public class SqliteDataStoreBatchTests
{
    private static LeagueContext CreateContext() =>
        new(new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static Fixture MakeFixture(Guid seasonId, DateTime? date = null) => new()
    {
        Id = Guid.NewGuid(),
        SeasonId = seasonId,
        HomeTeamId = Guid.NewGuid(),
        AwayTeamId = Guid.NewGuid(),
        Date = date ?? new DateTime(2025, 9, 2, 19, 30, 0)
    };

    // ====== ReplaceFixturesForSeasonAsync ======

    [Fact]
    public async Task ReplaceFixturesForSeason_RemovesOldAndInsertsNew()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        using var context = CreateContext();
        var oldFixtures = new[] { MakeFixture(seasonId), MakeFixture(seasonId) };
        context.Fixtures.AddRange(oldFixtures);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);
        var newFixtures = new List<Fixture> { MakeFixture(seasonId), MakeFixture(seasonId), MakeFixture(seasonId) };

        // Act
        await store.ReplaceFixturesForSeasonAsync(seasonId, newFixtures);

        // Assert
        var stored = await context.Fixtures.AsNoTracking().Where(f => f.SeasonId == seasonId).ToListAsync();
        Assert.Equal(3, stored.Count);
        Assert.All(stored, f => Assert.Contains(f.Id, newFixtures.Select(n => n.Id)));
        Assert.DoesNotContain(stored, f => oldFixtures.Select(o => o.Id).Contains(f.Id));
    }

    [Fact]
    public async Task ReplaceFixturesForSeason_DoesNotTouchOtherSeasons()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var otherSeasonId = Guid.NewGuid();
        using var context = CreateContext();
        var otherFixture = MakeFixture(otherSeasonId);
        context.Fixtures.Add(otherFixture);
        context.Fixtures.Add(MakeFixture(seasonId));
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        await store.ReplaceFixturesForSeasonAsync(seasonId, new List<Fixture> { MakeFixture(seasonId) });

        // Assert
        var otherStored = await context.Fixtures.AsNoTracking().Where(f => f.SeasonId == otherSeasonId).ToListAsync();
        Assert.Single(otherStored);
        Assert.Equal(otherFixture.Id, otherStored[0].Id);
    }

    [Fact]
    public async Task ReplaceFixturesForSeason_EmptyList_ClearsSeasonFixtures()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        using var context = CreateContext();
        context.Fixtures.AddRange(MakeFixture(seasonId), MakeFixture(seasonId));
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        await store.ReplaceFixturesForSeasonAsync(seasonId, new List<Fixture>());

        // Assert
        Assert.Empty(await context.Fixtures.AsNoTracking().Where(f => f.SeasonId == seasonId).ToListAsync());
    }

    [Fact]
    public async Task ReplaceFixturesForSeason_RefreshesSnapshot()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        using var context = CreateContext();
        var store = new SqliteDataStore(context);
        _ = store.GetData(); // prime the snapshot cache

        var fixtures = new List<Fixture> { MakeFixture(seasonId) };

        // Act
        await store.ReplaceFixturesForSeasonAsync(seasonId, fixtures);

        // Assert — snapshot must reflect the new fixtures without manual reload
        var data = store.GetData();
        Assert.Contains(data.Fixtures, f => f.Id == fixtures[0].Id);
    }

    [Fact]
    public async Task ReplaceFixturesForSeason_CancellationRequested_Throws()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        using var context = CreateContext();
        var store = new SqliteDataStore(context);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.ReplaceFixturesForSeasonAsync(seasonId, new List<Fixture>(), cts.Token));
    }

    // ====== AddSeasonEntitiesAsync ======

    [Fact]
    public async Task AddSeasonEntities_InsertsAllCollections()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        using var context = CreateContext();
        context.Seasons.Add(new Season { Id = seasonId, Name = "S1" });
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);
        var divisionId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var divisions = new List<Division> { new() { Id = divisionId, SeasonId = seasonId, Name = "Div 1" } };
        var venues = new List<Venue> { new() { Id = venueId, SeasonId = seasonId, Name = "Venue 1" } };
        var teams = new List<Team> { new() { Id = teamId, SeasonId = seasonId, DivisionId = divisionId, VenueId = venueId, Name = "Team 1" } };
        var players = new List<Player> { new() { Id = Guid.NewGuid(), SeasonId = seasonId, TeamId = teamId, FirstName = "Pat", LastName = "Smith" } };

        // Act
        await store.AddSeasonEntitiesAsync(divisions, venues, teams, players);

        // Assert
        Assert.Single(await context.Divisions.AsNoTracking().ToListAsync());
        Assert.Single(await context.Venues.AsNoTracking().ToListAsync());
        Assert.Single(await context.Teams.AsNoTracking().ToListAsync());
        Assert.Single(await context.Players.AsNoTracking().ToListAsync());

        var storedTeam = await context.Teams.AsNoTracking().SingleAsync();
        Assert.Equal(divisionId, storedTeam.DivisionId);
        Assert.Equal(venueId, storedTeam.VenueId);
    }

    [Fact]
    public async Task AddSeasonEntities_NullCollections_NoOp()
    {
        // Arrange
        using var context = CreateContext();
        var store = new SqliteDataStore(context);

        // Act
        await store.AddSeasonEntitiesAsync();

        // Assert
        Assert.Empty(await context.Divisions.AsNoTracking().ToListAsync());
        Assert.Empty(await context.Venues.AsNoTracking().ToListAsync());
        Assert.Empty(await context.Teams.AsNoTracking().ToListAsync());
        Assert.Empty(await context.Players.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AddSeasonEntities_PartialCollections_InsertsOnlyProvided()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        using var context = CreateContext();
        var store = new SqliteDataStore(context);

        var divisions = new List<Division> { new() { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Div Only" } };

        // Act
        await store.AddSeasonEntitiesAsync(divisions: divisions);

        // Assert
        Assert.Single(await context.Divisions.AsNoTracking().ToListAsync());
        Assert.Empty(await context.Teams.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AddSeasonEntities_RefreshesSnapshot()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        using var context = CreateContext();
        var store = new SqliteDataStore(context);
        _ = store.GetData(); // prime the snapshot cache

        var divisions = new List<Division> { new() { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Div 1" } };

        // Act
        await store.AddSeasonEntitiesAsync(divisions: divisions);

        // Assert
        var data = store.GetData();
        Assert.Contains(data.Divisions, d => d.Id == divisions[0].Id);
    }

    [Fact]
    public async Task AddSeasonEntities_VenueTablesPersisted()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        using var context = CreateContext();
        var store = new SqliteDataStore(context);

        var venue = new Venue { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Venue T" };
        venue.Tables.Add(new VenueTable { Id = Guid.NewGuid(), Label = "T1", MaxTeams = 2 });
        venue.Tables.Add(new VenueTable { Id = Guid.NewGuid(), Label = "T2", MaxTeams = 4 });

        // Act
        await store.AddSeasonEntitiesAsync(venues: new List<Venue> { venue });

        // Assert
        var stored = await context.Venues.AsNoTracking().SingleAsync();
        Assert.Equal(2, stored.Tables.Count);
        Assert.Contains(stored.Tables, t => t.Label == "T1");
        Assert.Contains(stored.Tables, t => t.Label == "T2");
    }

    [Fact]
    public async Task AddSeasonEntities_CancellationRequested_Throws()
    {
        // Arrange
        using var context = CreateContext();
        var store = new SqliteDataStore(context);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.AddSeasonEntitiesAsync(ct: cts.Token));
    }
}
