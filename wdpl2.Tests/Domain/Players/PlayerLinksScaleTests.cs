using System.Diagnostics;
using Wdpl2.Domain.Fixtures;
using Wdpl2.Domain.Players;
using Wdpl2.Models;

namespace wdpl2.Tests;

/// <summary>
/// The finder against a league the size of the real one.
/// </summary>
/// <remarks>
/// The first version of this page was correct and unusable: it asked how many
/// records named each row while it was drawing them, which on forty-nine
/// seasons meant tens of millions of passes over the fixtures on the thread
/// drawing the screen. The app stopped responding and was killed.
/// <para>
/// Correctness tests would never have caught that, because it was right. So
/// the shape of the work is what is measured here: one walk for everybody, not
/// one each.
/// </para>
/// </remarks>
public class PlayerLinksScaleTests
{
    /// <summary>A league roughly the size of the one this runs against.</summary>
    private static LeagueData Big(int seasons = 49, int playersPerSeason = 48, int fixturesPerSeason = 34)
    {
        var league = new LeagueData();
        var names = new[] { "Marsh", "Wilkes", "Foulkes", "Dowding", "Prentice", "Mannion", "Clarke", "Jones" };

        for (var s = 0; s < seasons; s++)
        {
            var seasonId = Guid.NewGuid();
            league.Seasons.Add(new Season
            {
                Id = seasonId,
                Name = $"Season {s}",
                StartDate = new DateTime(2000, 9, 1).AddYears(s),
            });

            var squad = new List<Player>();
            for (var p = 0; p < playersPerSeason; p++)
            {
                var player = new Player
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,

                    // The same people season after season, which is what makes
                    // this the real shape: hundreds of sets, not a handful.
                    FirstName = $"Player{p}",
                    LastName = names[p % names.Length],
                };

                league.Players.Add(player);
                squad.Add(player);
            }

            for (var f = 0; f < fixturesPerSeason; f++)
            {
                var fixture = new Fixture { Id = Guid.NewGuid(), SeasonId = seasonId };
                for (var n = 1; n <= 15; n++)
                {
                    fixture.Frames.Add(new FrameResult
                    {
                        Number = n,
                        HomePlayerId = squad[(f + n) % squad.Count].Id,
                        AwayPlayerId = squad[(f + n + 7) % squad.Count].Id,
                        Winner = n % 2 == 0 ? FrameWinner.Home : FrameWinner.Away,
                    });
                }

                league.Fixtures.Add(fixture);
            }
        }

        return league;
    }

    [Fact]
    public void Scan_OfAWholeLeagueIsQuickEnoughToWaitFor()
    {
        var league = Big();

        Assert.True(league.Players.Count > 2000, $"only built {league.Players.Count} players");

        var clock = Stopwatch.StartNew();
        var found = PlayerLinks.Find(league);
        var counts = PlayerLinks.CountReferences(league);
        clock.Stop();

        Assert.NotEmpty(found);
        Assert.NotEmpty(counts);

        // Generous on purpose: the point is that it is seconds at worst rather
        // than the minutes the per-row walk took. A regression here means the
        // work has gone back to being done once per row.
        Assert.True(clock.ElapsedMilliseconds < 5000,
            $"scanning {league.Players.Count} players over {league.Fixtures.Count} fixtures "
            + $"took {clock.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Counting references must not depend on how many players are asked about.
    /// </summary>
    /// <remarks>
    /// This is the actual bug, stated as a test: the cost of knowing every
    /// player's reference count is one walk of the league, so doubling the
    /// number of players must not square the time.
    /// </remarks>
    [Fact]
    public void CountReferences_IsOneWalkForEverybody()
    {
        var small = Big(seasons: 10);
        var large = Big(seasons: 20);

        // Warm the code paths so the first call is not paying for JIT.
        PlayerLinks.CountReferences(small);

        var one = Stopwatch.StartNew();
        PlayerLinks.CountReferences(small);
        one.Stop();

        var two = Stopwatch.StartNew();
        PlayerLinks.CountReferences(large);
        two.Stop();

        // Twice the league should be roughly twice the work. Eight times over
        // is the signature of a walk per player rather than a walk per league.
        var ratio = (two.Elapsed.TotalMilliseconds + 1) / (one.Elapsed.TotalMilliseconds + 1);

        Assert.True(ratio < 8,
            $"twice the data took {ratio:F1}x the time ({one.ElapsedMilliseconds}ms then {two.ElapsedMilliseconds}ms)");
    }
}
