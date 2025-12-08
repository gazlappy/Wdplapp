using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Service to compare app calculations against VBA tblRatings data to find discrepancies.
/// </summary>
public static class VbaComparisonService
{
    /// <summary>
    /// Parse VBA tblRatings data from text file format.
    /// Expected format: ID\tWeekNo\tPlayerID\tRating (tab-separated)
    /// </summary>
    public static Dictionary<(int VbaPlayerId, int WeekNo), int> ParseVbaRatings(string vbaData)
    {
        var ratings = new Dictionary<(int, int), int>();
        
        foreach (var line in vbaData.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("-"))
                continue;
                
            var parts = trimmed.Split('\t');
            if (parts.Length >= 4 && 
                int.TryParse(parts[1], out var weekNo) && 
                int.TryParse(parts[2], out var playerId) && 
                int.TryParse(parts[3], out var rating))
            {
                ratings[(playerId, weekNo)] = rating;
            }
        }
        
        return ratings;
    }

    /// <summary>
    /// Parse VBA tblPlayers data to create a mapping from VBA PlayerID to player name.
    /// Expected format: PlayerID\tPlayerName\t... (tab-separated)
    /// </summary>
    public static Dictionary<int, string> ParseVbaPlayers(string vbaData)
    {
        var players = new Dictionary<int, string>();
        
        foreach (var line in vbaData.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("-") || trimmed.StartsWith("PlayerID"))
                continue;
                
            var parts = trimmed.Split('\t');
            if (parts.Length >= 2 && int.TryParse(parts[0], out var playerId))
            {
                players[playerId] = parts[1];
            }
        }
        
        return players;
    }

    /// <summary>
    /// Build a mapping from VBA PlayerID to MAUI Player Guid by matching names.
    /// </summary>
    public static Dictionary<int, Guid> BuildPlayerIdMapping(Dictionary<int, string> vbaPlayers, List<Player> mauiPlayers)
    {
        var mapping = new Dictionary<int, Guid>();
        
        foreach (var vba in vbaPlayers)
        {
            var vbaName = vba.Value.Trim().ToUpperInvariant();
            
            // Try to find matching MAUI player by name
            var maui = mauiPlayers.FirstOrDefault(p => 
                (p.FullName ?? $"{p.FirstName} {p.LastName}").Trim().ToUpperInvariant() == vbaName);
            
            if (maui != null)
            {
                mapping[vba.Key] = maui.Id;
            }
        }
        
        return mapping;
    }

    /// <summary>
    /// Calculate weekly ratings using the app algorithm and compare against VBA data.
    /// Returns a detailed comparison report.
    /// </summary>
    public static string CompareRatings(
        Dictionary<(int VbaPlayerId, int WeekNo), int> vbaRatings,
        Dictionary<int, string> vbaPlayers,
        Dictionary<int, Guid> playerMapping,
        List<Fixture> allFixtures,
        AppSettings settings,
        DateTime seasonStartDate)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== VBA vs MAUI RATING COMPARISON ===");
        sb.AppendLine($"Settings: StartValue={settings.RatingStartValue}, Weighting={settings.RatingWeighting}, Bias={settings.RatingsBias}");
        sb.AppendLine($"Factors: Win={settings.WinFactor}, Loss={settings.LossFactor}, 8Ball={settings.EightBallFactor} (Use8Ball={settings.UseEightBallFactor})");
        sb.AppendLine($"Season Start: {seasonStartDate:dd/MM/yyyy}");
        sb.AppendLine();

        // Calculate ratings using our algorithm
        var mauiRatings = CalculateWeeklyRatings(allFixtures, settings, seasonStartDate);
        
        // Reverse mapping: Guid -> VbaPlayerId
        var guidToVba = playerMapping.ToDictionary(x => x.Value, x => x.Key);
        
        // Get all weeks from VBA data
        var weeks = vbaRatings.Keys.Select(k => k.WeekNo).Distinct().OrderBy(w => w).ToList();
        
        int totalComparisons = 0;
        int matches = 0;
        int mismatches = 0;
        var mismatchDetails = new List<(int week, string player, int vba, int maui, int diff)>();

        foreach (var week in weeks)
        {
            var weekVbaRatings = vbaRatings.Where(x => x.Key.WeekNo == week).ToList();
            
            foreach (var vbaEntry in weekVbaRatings)
            {
                var vbaPlayerId = vbaEntry.Key.VbaPlayerId;
                var vbaRating = vbaEntry.Value;
                
                if (!playerMapping.TryGetValue(vbaPlayerId, out var mauiPlayerId))
                    continue; // Player not mapped
                    
                totalComparisons++;
                
                // Get MAUI rating for this player at this week
                int mauiRating = settings.RatingStartValue;
                if (mauiRatings.TryGetValue((mauiPlayerId, week), out var r))
                    mauiRating = r;
                else if (week == 1)
                    mauiRating = settings.RatingStartValue;
                
                if (vbaRating == mauiRating)
                {
                    matches++;
                }
                else
                {
                    mismatches++;
                    var playerName = vbaPlayers.TryGetValue(vbaPlayerId, out var n) ? n : $"ID:{vbaPlayerId}";
                    mismatchDetails.Add((week, playerName, vbaRating, mauiRating, mauiRating - vbaRating));
                }
            }
        }

        sb.AppendLine($"SUMMARY: {matches}/{totalComparisons} match ({(double)matches/totalComparisons*100:F1}%), {mismatches} mismatches");
        sb.AppendLine();

        if (mismatches > 0)
        {
            sb.AppendLine("=== MISMATCHES (first 50) ===");
            sb.AppendLine("Week | Player | VBA | MAUI | Diff");
            sb.AppendLine("-----|--------|-----|------|-----");
            
            foreach (var m in mismatchDetails.OrderBy(x => x.week).ThenBy(x => x.player).Take(50))
            {
                sb.AppendLine($"{m.week,4} | {m.player,-20} | {m.vba,4} | {m.maui,4} | {m.diff:+#;-#;0}");
            }
            
            // Show breakdown by week
            sb.AppendLine();
            sb.AppendLine("=== MISMATCHES BY WEEK ===");
            var byWeek = mismatchDetails.GroupBy(x => x.week).OrderBy(g => g.Key);
            foreach (var g in byWeek)
            {
                sb.AppendLine($"Week {g.Key}: {g.Count()} mismatches, avg diff = {g.Average(x => Math.Abs(x.diff)):F1}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Calculate weekly ratings using the MAUI algorithm (should match VBA).
    /// This is a copy of the LeagueTablesPage algorithm for comparison.
    /// </summary>
    private static Dictionary<(Guid, int), int> CalculateWeeklyRatings(
        List<Fixture> allFixtures,
        AppSettings settings,
        DateTime seasonStartDate)
    {
        var weeklyRatings = new Dictionary<(Guid, int), int>();
        var playerFrameData = new Dictionary<Guid, List<(int weekNo, Guid oppId, bool won, bool eightBall)>>();
        var allPlayerIds = new HashSet<Guid>();

        foreach (var fixture in allFixtures.Where(f => f.Frames.Any()))
        {
            foreach (var frame in fixture.Frames)
            {
                if (frame.HomePlayerId.HasValue) allPlayerIds.Add(frame.HomePlayerId.Value);
                if (frame.AwayPlayerId.HasValue) allPlayerIds.Add(frame.AwayPlayerId.Value);
            }
        }

        // Initialize all players for Week 1
        foreach (var pid in allPlayerIds)
            weeklyRatings[(pid, 1)] = settings.RatingStartValue;

        var fixturesByWeek = allFixtures
            .Where(f => f.Frames.Any())
            .OrderBy(f => f.Date)
            .ThenBy(f => f.Id)
            .GroupBy(f => GetSeasonWeekNumber(f.Date, seasonStartDate))
            .OrderBy(g => g.Key)
            .ToList();

        int maxWeek = fixturesByWeek.Any() ? fixturesByWeek.Max(g => g.Key) : 0;

        // Process week by week
        for (int wkNo = 1; wkNo <= maxWeek; wkNo++)
        {
            var thisWeekFixtures = fixturesByWeek.FirstOrDefault(g => g.Key == wkNo);
            if (thisWeekFixtures != null)
            {
                foreach (var fixture in thisWeekFixtures.OrderBy(f => f.Date).ThenBy(f => f.Id))
                {
                    foreach (var frame in fixture.Frames.OrderBy(fr => fr.Number))
                    {
                        if (frame.HomePlayerId.HasValue)
                        {
                            var pid = frame.HomePlayerId.Value;
                            if (!playerFrameData.ContainsKey(pid))
                                playerFrameData[pid] = new List<(int, Guid, bool, bool)>();
                            playerFrameData[pid].Add((
                                wkNo,
                                frame.AwayPlayerId ?? Guid.Empty,
                                frame.Winner == FrameWinner.Home,
                                frame.EightBall && frame.Winner == FrameWinner.Home
                            ));
                        }
                        if (frame.AwayPlayerId.HasValue)
                        {
                            var pid = frame.AwayPlayerId.Value;
                            if (!playerFrameData.ContainsKey(pid))
                                playerFrameData[pid] = new List<(int, Guid, bool, bool)>();
                            playerFrameData[pid].Add((
                                wkNo,
                                frame.HomePlayerId ?? Guid.Empty,
                                frame.Winner == FrameWinner.Away,
                                frame.EightBall && frame.Winner == FrameWinner.Away
                            ));
                        }
                    }
                }
            }

            // Calculate ratings for this week
            foreach (var pid in playerFrameData.Keys)
            {
                var frames = playerFrameData[pid].Where(f => f.weekNo <= wkNo).ToList();
                if (frames.Count == 0) continue;

                int totalFrames = frames.Count;
                int biasX = settings.RatingWeighting - (settings.RatingsBias * (totalFrames - 1));
                if (biasX < 1) biasX = 1;

                long valueTot = 0;
                long weightingTot = 0;

                foreach (var (fwkNo, oppId, won, eightBall) in frames)
                {
                    // Use CURRENT week for opponent rating lookup
                    int oppRating = weeklyRatings.TryGetValue((oppId, wkNo), out var r)
                        ? r
                        : settings.RatingStartValue;

                    int ratingAttn;
                    if (won)
                    {
                        if (eightBall && settings.UseEightBallFactor)
                            ratingAttn = (int)(oppRating * settings.EightBallFactor);
                        else
                            ratingAttn = (int)(oppRating * settings.WinFactor);
                    }
                    else
                    {
                        ratingAttn = (int)(oppRating * settings.LossFactor);
                    }

                    valueTot += (long)ratingAttn * biasX;
                    weightingTot += biasX;
                    biasX += settings.RatingsBias;
                }

                int rating = weightingTot > 0 ? (int)(valueTot / weightingTot) : settings.RatingStartValue;
                weeklyRatings[(pid, wkNo)] = rating;
                weeklyRatings[(pid, wkNo + 1)] = rating;
            }
        }

        return weeklyRatings;
    }

    private static int GetSeasonWeekNumber(DateTime matchDate, DateTime seasonStartDate)
    {
        var daysSinceStart = (matchDate.Date - seasonStartDate.Date).Days;
        return (daysSinceStart / 7) + 1;
    }

    /// <summary>
    /// Trace a single player's calculation step-by-step for detailed debugging.
    /// </summary>
    public static string TracePlayerCalculation(
        int vbaPlayerId,
        Dictionary<(int VbaPlayerId, int WeekNo), int> vbaRatings,
        Dictionary<int, string> vbaPlayers,
        Dictionary<int, Guid> playerMapping,
        List<Fixture> allFixtures,
        AppSettings settings,
        DateTime seasonStartDate)
    {
        var sb = new StringBuilder();
        
        if (!playerMapping.TryGetValue(vbaPlayerId, out var mauiPlayerId))
        {
            sb.AppendLine($"ERROR: VBA Player {vbaPlayerId} not found in mapping");
            return sb.ToString();
        }
        
        var playerName = vbaPlayers.TryGetValue(vbaPlayerId, out var n) ? n : $"VBA:{vbaPlayerId}";
        sb.AppendLine($"=== TRACE: {playerName} (VBA ID: {vbaPlayerId}) ===");
        sb.AppendLine();
        
        // Get all VBA ratings for this player
        var playerVbaRatings = vbaRatings
            .Where(x => x.Key.VbaPlayerId == vbaPlayerId)
            .OrderBy(x => x.Key.WeekNo)
            .ToList();
        
        sb.AppendLine("VBA Weekly Ratings:");
        foreach (var r in playerVbaRatings)
        {
            sb.AppendLine($"  Week {r.Key.WeekNo}: {r.Value}");
        }
        sb.AppendLine();
        
        // Calculate using our algorithm with full trace
        var mauiRatings = CalculateWeeklyRatings(allFixtures, settings, seasonStartDate);
        
        sb.AppendLine("MAUI Weekly Ratings:");
        foreach (var r in playerVbaRatings)
        {
            var week = r.Key.WeekNo;
            var mauiRating = mauiRatings.TryGetValue((mauiPlayerId, week), out var mr) ? mr : settings.RatingStartValue;
            var match = r.Value == mauiRating ? "?" : $"? (diff: {mauiRating - r.Value:+#;-#;0})";
            sb.AppendLine($"  Week {week}: VBA={r.Value}, MAUI={mauiRating} {match}");
        }
        
        return sb.ToString();
    }
}
