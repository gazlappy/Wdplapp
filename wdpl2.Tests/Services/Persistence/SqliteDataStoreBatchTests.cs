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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteFixtures_PersistsAndRefreshesSnapshot(bool all)
    {
        var options = new DbContextOptionsBuilder<LeagueContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var context = new LeagueContext(options);
        var season = new Season { Name = "Current" };
        var other = new Season { Name = "Other" };
        context.Seasons.AddRange(season, other);
        context.Fixtures.AddRange(MakeFixture(season.Id), MakeFixture(season.Id), MakeFixture(other.Id));
        await context.SaveChangesAsync();
        var store = new SqliteDataStore(context);
        _ = store.GetData();
        int removed = await store.DeleteFixturesAsync(all ? null : season.Id);
        Assert.Equal(all ? 3 : 2, removed);
        using var reloaded = new LeagueContext(options);
        Assert.Equal(all ? 0 : 1, await reloaded.Fixtures.CountAsync());
        Assert.DoesNotContain(store.GetData().Fixtures, f => f.SeasonId == season.Id);
        Assert.Equal(0, await store.DeleteFixturesAsync(all ? null : season.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteFixtures_LockedScope_LeavesEverythingUntouched(bool all)
    {
        using var context = CreateContext();
        var locked = new Season { Name = "Locked", IsLocked = true };
        var open = new Season { Name = "Open" };
        context.Seasons.AddRange(locked, open);
        context.Fixtures.AddRange(MakeFixture(locked.Id), MakeFixture(open.Id));
        await context.SaveChangesAsync();
        var store = new SqliteDataStore(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.DeleteFixturesAsync(all ? null : locked.Id));
        await store.SaveAsync();
        Assert.Equal(2, await context.Fixtures.CountAsync());
        Assert.Equal(1, await store.DeleteFixturesAsync(open.Id));
        Assert.Equal(locked.Id, (await context.Fixtures.SingleAsync()).SeasonId);
    }

    [Fact]
    public async Task DeleteFixtures_Canceled_LeavesFixturesUntouched()
    {
        using var context = CreateContext();
        context.Fixtures.Add(MakeFixture(Guid.NewGuid()));
        await context.SaveChangesAsync();
        var store = new SqliteDataStore(context);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteFixturesAsync(null, new CancellationToken(true)));
        Assert.Equal(1, await context.Fixtures.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteFixtures_Sqlite_ReloadConfirmsDeletion(bool all)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options;
        using var context = new LeagueContext(options);
        await context.Database.EnsureCreatedAsync();
        var season = new Season { Name = "Current" };
        var other = new Season { Name = "Other" };
        context.Seasons.AddRange(season, other);
        foreach (var s in new[] { season, other })
        {
            var home = new Team { Name = "Home", SeasonId = s.Id };
            var away = new Team { Name = "Away", SeasonId = s.Id };
            context.Teams.AddRange(home, away);
            context.Fixtures.Add(new Fixture { SeasonId = s.Id, HomeTeamId = home.Id, AwayTeamId = away.Id });
        }
        await context.SaveChangesAsync();
        var store = new SqliteDataStore(context);
        Assert.Equal(all ? 2 : 1, await store.DeleteFixturesAsync(all ? null : season.Id));
        using var reloaded = new LeagueContext(options);
        Assert.Equal(all ? 0 : 1, await reloaded.Fixtures.CountAsync());
        Assert.Equal(4, await reloaded.Teams.CountAsync());
        Assert.Equal(2, await reloaded.Seasons.CountAsync());
    }

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

    [Theory]
    [InlineData("valid")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("placementChanged")]
    [InlineData("locked")]
    public async Task ReplaceGeneratedFixtures_ValidatesBeforeDeleting(string scenario)
    {
        using var context = CreateContext();
        var league = new LeagueData();
        var season = new Season { Name = "Schedule", StartDate = new DateTime(2025, 9, 2), EndDate = new DateTime(2026, 5, 1) };
        var division = new Division { SeasonId = season.Id, Name = "Division" };
        var venue = new Venue { SeasonId = season.Id, Name = "Home", Tables = new() { new VenueTable { Label = "1" } } };
        league.Seasons.Add(season);
        league.Divisions.Add(division);
        league.Venues.Add(venue);
        league.Teams.AddRange(Enumerable.Range(0, 2).Select(i => new Team
        {
            SeasonId = season.Id, DivisionId = division.Id, Name = $"Team {i}",
            VenueId = venue.Id, TableId = venue.Tables[0].Id
        }));
        context.Seasons.Add(season);
        context.Divisions.Add(division);
        context.Venues.Add(venue);
        context.Teams.AddRange(league.Teams);
        var old = MakeFixture(season.Id);
        context.Fixtures.Add(old);
        await context.SaveChangesAsync();
        var generated = FixtureGenerator.Generate(league, season.Id, season.StartDate, DayOfWeek.Tuesday);
        switch (scenario)
        {
            case "duplicate": generated.Add(generated[0]); break;
            case "missing": generated.Clear(); break;
            case "placementChanged": league.Teams[0].TableId = null; break;
            case "locked": season.IsLocked = true; break;
        }
        await context.SaveChangesAsync();
        var store = new SqliteDataStore(context);
        if (scenario == "valid")
        {
            await store.ReplaceGeneratedFixturesForSeasonAsync(season.Id, generated);
            Assert.Equal(2, await context.Fixtures.CountAsync());
            Assert.False(await context.Fixtures.AnyAsync(f => f.Id == old.Id));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReplaceGeneratedFixturesForSeasonAsync(season.Id, generated));
            Assert.Equal(old.Id, (await context.Fixtures.AsNoTracking().SingleAsync()).Id);
        }
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
