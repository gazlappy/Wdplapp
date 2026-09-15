using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Domain.Fixtures;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Collecting a finished card has to land in the store the app reads.
/// </summary>
/// <remarks>
/// Persistence here is hybrid, and a collected card once went only into the
/// JSON snapshot while the Fixtures page read the SQLite copy: the website said
/// collected, the fixture still said unplayed, and nothing reported an error.
/// These cover the store side of that round trip.
/// </remarks>
public class CollectedCardPersistenceTests
{
    private static async Task<(SqliteConnection, DbContextOptions<LeagueContext>)> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options;
        using var seed = new LeagueContext(options);
        await seed.Database.EnsureCreatedAsync();
        return (connection, options);
    }

    /// <summary>Seeds the season and teams a fixture's foreign keys require.</summary>
    private static async Task<Fixture> SeedFixtureAsync(DbContextOptions<LeagueContext> options)
    {
        var season = new Season { Name = "2026/27" };
        var home = new Team { Name = "Home", SeasonId = season.Id };
        var away = new Team { Name = "Away", SeasonId = season.Id };

        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            Date = new DateTime(2026, 9, 17),
        };

        using var context = new LeagueContext(options);
        context.Seasons.Add(season);
        context.Teams.AddRange(home, away);
        context.Fixtures.Add(fixture);
        await context.SaveChangesAsync();

        return fixture;
    }

    [Fact]
    public async Task GetFixtureAsync_FindsById_WithoutKnowingTheSeason()
    {
        var (connection, options) = await OpenAsync();
        using var _ = connection;

        var fixture = await SeedFixtureAsync(options);

        using var read = new LeagueContext(options);
        var store = new SqliteDataStore(read);

        Assert.NotNull(await store.GetFixtureAsync(fixture.Id));
        Assert.Null(await store.GetFixtureAsync(Guid.NewGuid()));

        // The trap this method exists to avoid: the season-scoped read answers
        // "nothing at all" rather than "everything" when the season is unknown.
        Assert.Empty(await store.GetFixturesAsync(null));
    }

    [Fact]
    public async Task UpdateFixtureAsync_PersistsCollectedFrames()
    {
        var (connection, options) = await OpenAsync();
        using var _ = connection;

        var fixture = await SeedFixtureAsync(options);

        var homeId = Guid.NewGuid();
        var awayId = Guid.NewGuid();

        using (var write = new LeagueContext(options))
        {
            var store = new SqliteDataStore(write);
            var stored = await store.GetFixtureAsync(fixture.Id);
            Assert.NotNull(stored);
            Assert.Empty(stored!.Frames);

            for (var n = 1; n <= 15; n++)
            {
                stored.Frames.Add(new FrameResult
                {
                    Number = n,
                    HomePlayerId = homeId,
                    AwayPlayerId = awayId,
                    Winner = n <= 9 ? FrameWinner.Home : FrameWinner.Away,
                    EightBall = n == 3,
                });
            }

            await store.UpdateFixtureAsync(stored);
        }

        // A fresh context, because the bug was invisible to the one that wrote.
        using var reopened = new LeagueContext(options);
        var reloaded = await new SqliteDataStore(reopened).GetFixtureAsync(fixture.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(15, reloaded!.Frames.Count);
        Assert.Equal(9, reloaded.HomeScore);
        Assert.Equal(6, reloaded.AwayScore);
        Assert.Single(reloaded.Frames, f => f.EightBall);
        Assert.Equal(homeId, reloaded.Frames[0].HomePlayerId);
    }

    [Fact]
    public async Task CollectingTwice_DoesNotDuplicateFrames()
    {
        var (connection, options) = await OpenAsync();
        using var _ = connection;

        var fixture = await SeedFixtureAsync(options);

        using (var context = new LeagueContext(options))
        {
            var seeded = await context.Fixtures.FirstAsync(f => f.Id == fixture.Id);
            seeded.Frames.Add(new FrameResult { Number = 1, Winner = FrameWinner.Home });
            await context.SaveChangesAsync();
        }

        for (var pass = 0; pass < 2; pass++)
        {
            using var write = new LeagueContext(options);
            var store = new SqliteDataStore(write);
            var stored = await store.GetFixtureAsync(fixture.Id);

            var frame = stored!.Frames.FirstOrDefault(f => f.Number == 1)
                        ?? throw new InvalidOperationException("frame 1 missing");
            frame.Winner = FrameWinner.Away;

            await store.UpdateFixtureAsync(stored);
        }

        using var reopened = new LeagueContext(options);
        var reloaded = await new SqliteDataStore(reopened).GetFixtureAsync(fixture.Id);

        Assert.Single(reloaded!.Frames);
        Assert.Equal(FrameWinner.Away, reloaded.Frames[0].Winner);
    }
}
