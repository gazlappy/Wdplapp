using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Calculates various league statistics: Player of the Month, Venue Stats, Season Recap.
/// </summary>
public static class LeagueStatsService
{
    // ========== PLAYER OF THE MONTH ==========

    public sealed class PlayerOfMonth
    {
        public string PlayerName { get; set; } = "";
        public Guid PlayerId { get; set; }
        public string TeamName { get; set; } = "";
        public int Month { get; set; }
        public int Year { get; set; }
        public string MonthName { get; set; } = "";
        public int FramesPlayed { get; set; }
        public int FramesWon { get; set; }
        public double WinPercentage { get; set; }
        public int EightBalls { get; set; }
        public int RatingChange { get; set; }
    }

    public static List<PlayerOfMonth> CalculatePlayersOfMonth(
        List<Fixture> fixtures, List<Player> players, List<Team> teams)
    {
        var results = new List<PlayerOfMonth>();
        var playerLookup = players.ToDictionary(p => p.Id);
        var teamLookup = teams.ToDictionary(t => t.Id);

        var months = fixtures
            .Where(f => f.Frames.Count > 0)
            .GroupBy(f => new { f.Date.Year, f.Date.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

        foreach (var month in months)
        {
            var stats = new Dictionary<Guid, (int played, int won, int eightBalls)>();

            foreach (var fixture in month)
            {
                foreach (var frame in fixture.Frames)
                {
                    if (frame.HomePlayerId.HasValue && !FrameResult.IsVoidPlayer(frame.HomePlayerId))
                    {
                        var id = frame.HomePlayerId.Value;
                        var cur = stats.GetValueOrDefault(id);
                        stats[id] = (cur.played + 1, cur.won + (frame.Winner == FrameWinner.Home ? 1 : 0),
                            cur.eightBalls + (frame.EightBall && frame.Winner == FrameWinner.Home ? 1 : 0));
                    }
                    if (frame.AwayPlayerId.HasValue && !FrameResult.IsVoidPlayer(frame.AwayPlayerId))
                    {
                        var id = frame.AwayPlayerId.Value;
                        var cur = stats.GetValueOrDefault(id);
                        stats[id] = (cur.played + 1, cur.won + (frame.Winner == FrameWinner.Away ? 1 : 0),
                            cur.eightBalls + (frame.EightBall && frame.Winner == FrameWinner.Away ? 1 : 0));
                    }
                }
            }

            var best = stats
                .Where(s => s.Value.played >= 3)
                .OrderByDescending(s => (double)s.Value.won / s.Value.played)
                .ThenByDescending(s => s.Value.won)
                .ThenByDescending(s => s.Value.eightBalls)
                .FirstOrDefault();

            if (best.Key != Guid.Empty && playerLookup.TryGetValue(best.Key, out var player))
            {
                var team = player.TeamId.HasValue && teamLookup.TryGetValue(player.TeamId.Value, out var t) ? t : null;
                results.Add(new PlayerOfMonth
                {
                    PlayerId = best.Key,
                    PlayerName = player.FullName,
                    TeamName = team?.Name ?? "",
                    Month = month.Key.Month,
                    Year = month.Key.Year,
                    MonthName = new DateTime(month.Key.Year, month.Key.Month, 1).ToString("MMMM yyyy"),
                    FramesPlayed = best.Value.played,
                    FramesWon = best.Value.won,
                    WinPercentage = best.Value.played > 0 ? (double)best.Value.won / best.Value.played * 100 : 0,
                    EightBalls = best.Value.eightBalls
                });
            }
        }

        return results;
    }

    // ========== VENUE STATS ==========

    public sealed class VenueStats
    {
        public Guid VenueId { get; set; }
        public string VenueName { get; set; } = "";
        public int TotalMatches { get; set; }
        public int HomeWins { get; set; }
        public int AwayWins { get; set; }
        public int Draws { get; set; }
        public double HomeWinPercentage => TotalMatches > 0 ? (double)HomeWins / TotalMatches * 100 : 0;
        public int TotalFrames { get; set; }
        public int HomeFrames { get; set; }
        public int AwayFrames { get; set; }
    }

    public static List<VenueStats> CalculateVenueStats(
        List<Fixture> fixtures, List<Venue> venues)
    {
        var venueLookup = venues.ToDictionary(v => v.Id);
        var stats = new Dictionary<Guid, VenueStats>();

        foreach (var f in fixtures.Where(f => f.VenueId.HasValue && f.Frames.Count > 0))
        {
            var vid = f.VenueId!.Value;
            if (!stats.TryGetValue(vid, out var vs))
            {
                vs = new VenueStats
                {
                    VenueId = vid,
                    VenueName = venueLookup.TryGetValue(vid, out var v) ? v.Name : "Unknown"
                };
                stats[vid] = vs;
            }

            vs.TotalMatches++;
            vs.TotalFrames += f.Frames.Count;
            vs.HomeFrames += f.HomeScore;
            vs.AwayFrames += f.AwayScore;

            if (f.HomeScore > f.AwayScore) vs.HomeWins++;
            else if (f.AwayScore > f.HomeScore) vs.AwayWins++;
            else vs.Draws++;
        }

        return stats.Values.OrderByDescending(v => v.TotalMatches).ToList();
    }

    // ========== SEASON RECAP ==========

    public sealed class SeasonRecap
    {
        public string SeasonName { get; set; } = "";
        public int TotalFixtures { get; set; }
        public int TotalFrames { get; set; }
        public int TotalEightBalls { get; set; }
        public string TopScorer { get; set; } = "";
        public int TopScorerWins { get; set; }
        public string MostImproved { get; set; } = "";
        public int MostImprovedGain { get; set; }
        public string MostEightBalls { get; set; } = "";
        public int MostEightBallCount { get; set; }
        public string LongestWinStreak { get; set; } = "";
        public int LongestWinStreakCount { get; set; }
        public string BiggestUpset { get; set; } = "";
        public List<PlayerOfMonth> MonthlyWinners { get; set; } = new();
    }

    private sealed class PlayerSeasonAccumulator
    {
        public int Wins;
        public int Losses;
        public int EightBalls;
        public List<bool> Results { get; } = new();
    }

    public static SeasonRecap GenerateSeasonRecap(
        Season season, List<Fixture> fixtures, List<Player> players,
        List<Team> teams, AppSettings settings)
    {
        var recap = new SeasonRecap { SeasonName = season.Name };
        var completed = fixtures.Where(f => f.Frames.Count > 0).ToList();

        recap.TotalFixtures = completed.Count;
        recap.TotalFrames = completed.Sum(f => f.Frames.Count);
        recap.TotalEightBalls = completed.Sum(f => f.Frames.Count(fr => fr.EightBall));

        // Player stats
        var playerStats = new Dictionary<Guid, PlayerSeasonAccumulator>();
        foreach (var fixture in completed.OrderBy(f => f.Date))
        {
            foreach (var frame in fixture.Frames)
            {
                if (frame.HomePlayerId.HasValue && !FrameResult.IsVoidPlayer(frame.HomePlayerId))
                {
                    var id = frame.HomePlayerId.Value;
                    if (!playerStats.TryGetValue(id, out var s)) { s = new PlayerSeasonAccumulator(); playerStats[id] = s; }
                    bool won = frame.Winner == FrameWinner.Home;
                    if (won) s.Wins++; else s.Losses++;
                    if (frame.EightBall && won) s.EightBalls++;
                    s.Results.Add(won);
                }
                if (frame.AwayPlayerId.HasValue && !FrameResult.IsVoidPlayer(frame.AwayPlayerId))
                {
                    var id = frame.AwayPlayerId.Value;
                    if (!playerStats.TryGetValue(id, out var s)) { s = new PlayerSeasonAccumulator(); playerStats[id] = s; }
                    bool won = frame.Winner == FrameWinner.Away;
                    if (won) s.Wins++; else s.Losses++;
                    if (frame.EightBall && won) s.EightBalls++;
                    s.Results.Add(won);
                }
            }
        }

        var playerLookup = players.ToDictionary(p => p.Id);

        // Top scorer
        var topScorer = playerStats.OrderByDescending(s => s.Value.Wins).FirstOrDefault();
        if (topScorer.Key != Guid.Empty && playerLookup.TryGetValue(topScorer.Key, out var ts))
        {
            recap.TopScorer = ts.FullName;
            recap.TopScorerWins = topScorer.Value.Wins;
        }

        // Most 8-balls
        var most8b = playerStats.OrderByDescending(s => s.Value.EightBalls).FirstOrDefault();
        if (most8b.Key != Guid.Empty && playerLookup.TryGetValue(most8b.Key, out var m8))
        {
            recap.MostEightBalls = m8.FullName;
            recap.MostEightBallCount = most8b.Value.EightBalls;
        }

        // Longest win streak
        int bestStreak = 0;
        Guid bestStreakPlayer = Guid.Empty;
        foreach (var (pid, stat) in playerStats)
        {
            int streak = 0, maxStreak = 0;
            foreach (var won in stat.Results)
            {
                streak = won ? streak + 1 : 0;
                if (streak > maxStreak) maxStreak = streak;
            }
            if (maxStreak > bestStreak)
            {
                bestStreak = maxStreak;
                bestStreakPlayer = pid;
            }
        }
        if (bestStreakPlayer != Guid.Empty && playerLookup.TryGetValue(bestStreakPlayer, out var sp))
        {
            recap.LongestWinStreak = sp.FullName;
            recap.LongestWinStreakCount = bestStreak;
        }

        // Most improved (rating gain)
        var ratings = RatingCalculator.CalculateAllRatings(completed, players, teams, settings, season.StartDate);
        var improved = ratings.Values
            .Where(r => r.Played >= 5)
            .OrderByDescending(r => r.Rating - settings.RatingStartValue)
            .FirstOrDefault();
        if (improved != null)
        {
            recap.MostImproved = improved.PlayerName;
            recap.MostImprovedGain = improved.Rating - settings.RatingStartValue;
        }

        // Monthly winners
        recap.MonthlyWinners = CalculatePlayersOfMonth(completed, players, teams);

        return recap;
    }

    /// <summary>
    /// Generate a shareable text summary of the season recap.
    /// </summary>
    public static string FormatRecapAsText(SeasonRecap recap)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"🏆 {recap.SeasonName} — Season Recap");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine($"📊 {recap.TotalFixtures} matches | {recap.TotalFrames} frames | {recap.TotalEightBalls} 8-balls");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(recap.TopScorer))
            sb.AppendLine($"🎯 Top Scorer: {recap.TopScorer} ({recap.TopScorerWins} wins)");
        if (!string.IsNullOrEmpty(recap.MostImproved))
            sb.AppendLine($"📈 Most Improved: {recap.MostImproved} (+{recap.MostImprovedGain} rating)");
        if (!string.IsNullOrEmpty(recap.MostEightBalls))
            sb.AppendLine($"🎱 Most 8-Balls: {recap.MostEightBalls} ({recap.MostEightBallCount})");
        if (!string.IsNullOrEmpty(recap.LongestWinStreak))
            sb.AppendLine($"🔥 Longest Win Streak: {recap.LongestWinStreak} ({recap.LongestWinStreakCount} in a row)");

        if (recap.MonthlyWinners.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("⭐ Players of the Month:");
            foreach (var m in recap.MonthlyWinners)
                sb.AppendLine($"   {m.MonthName}: {m.PlayerName} ({m.WinPercentage:F0}% win rate)");
        }

        return sb.ToString();
    }

    // ========== SEASON COMPARISON ==========

    public sealed class SeasonComparison
    {
        public string Season1Name { get; set; } = "";
        public string Season2Name { get; set; } = "";
        public int Season1Fixtures { get; set; }
        public int Season2Fixtures { get; set; }
        public int Season1Frames { get; set; }
        public int Season2Frames { get; set; }
        public int Season1EightBalls { get; set; }
        public int Season2EightBalls { get; set; }
        public int Season1Teams { get; set; }
        public int Season2Teams { get; set; }
        public int Season1Players { get; set; }
        public int Season2Players { get; set; }
        public double Season1AvgFramesPerMatch { get; set; }
        public double Season2AvgFramesPerMatch { get; set; }
        public double Season1HomeWinPct { get; set; }
        public double Season2HomeWinPct { get; set; }
    }

    public static SeasonComparison CompareSeasons(
        Season season1, List<Fixture> fixtures1, List<Team> teams1, List<Player> players1,
        Season season2, List<Fixture> fixtures2, List<Team> teams2, List<Player> players2)
    {
        var c1 = fixtures1.Where(f => f.Frames.Count > 0).ToList();
        var c2 = fixtures2.Where(f => f.Frames.Count > 0).ToList();

        double HomeWinPct(List<Fixture> fxs)
        {
            int total = 0, homeWins = 0;
            foreach (var f in fxs)
            {
                total++;
                if (f.HomeScore > f.AwayScore) homeWins++;
            }
            return total > 0 ? (double)homeWins / total * 100 : 0;
        }

        return new SeasonComparison
        {
            Season1Name = season1.Name,
            Season2Name = season2.Name,
            Season1Fixtures = c1.Count,
            Season2Fixtures = c2.Count,
            Season1Frames = c1.Sum(f => f.Frames.Count),
            Season2Frames = c2.Sum(f => f.Frames.Count),
            Season1EightBalls = c1.Sum(f => f.Frames.Count(fr => fr.EightBall)),
            Season2EightBalls = c2.Sum(f => f.Frames.Count(fr => fr.EightBall)),
            Season1Teams = teams1.Count,
            Season2Teams = teams2.Count,
            Season1Players = players1.Count,
            Season2Players = players2.Count,
            Season1AvgFramesPerMatch = c1.Count > 0 ? (double)c1.Sum(f => f.Frames.Count) / c1.Count : 0,
            Season2AvgFramesPerMatch = c2.Count > 0 ? (double)c2.Sum(f => f.Frames.Count) / c2.Count : 0,
            Season1HomeWinPct = HomeWinPct(c1),
            Season2HomeWinPct = HomeWinPct(c2)
        };
    }
}
