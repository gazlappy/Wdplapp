using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services;

public static class VbaComparisonService
{
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

    public static Dictionary<int, Guid> BuildPlayerIdMapping(Dictionary<int, string> vbaPlayers, List<Player> mauiPlayers)
    {
        var mapping = new Dictionary<int, Guid>();
        var usedMauiIds = new HashSet<Guid>();
        foreach (var vba in vbaPlayers)
        {
            var vbaName = vba.Value.Trim().ToUpperInvariant();
            var maui = mauiPlayers.FirstOrDefault(p =>
                !usedMauiIds.Contains(p.Id) &&
                (p.FullName ?? $"{p.FirstName} {p.LastName}").Trim().ToUpperInvariant() == vbaName);
            if (maui != null)
            {
                mapping[vba.Key] = maui.Id;
                usedMauiIds.Add(maui.Id);
            }
        }
        return mapping;
    }

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
        sb.AppendLine($"Player Mappings: {playerMapping.Count} VBA players mapped to MAUI");
        sb.AppendLine();

        var mauiRatings = CalculateWeeklyRatings(allFixtures, settings, seasonStartDate);
        var weeks = vbaRatings.Keys.Select(k => k.WeekNo).Distinct().OrderBy(w => w).ToList();

        int totalComparisons = 0, matches = 0, mismatches = 0, unmapped = 0;
        var mismatchDetails = new List<(int week, string player, int vba, int maui, int diff)>();

        foreach (var week in weeks)
        {
            foreach (var vbaEntry in vbaRatings.Where(x => x.Key.WeekNo == week))
            {
                var vbaPlayerId = vbaEntry.Key.VbaPlayerId;
                var vbaRating = vbaEntry.Value;

                if (!playerMapping.TryGetValue(vbaPlayerId, out var mauiPlayerId))
                {
                    unmapped++;
                    continue;
                }
                totalComparisons++;

                int mauiRating = settings.RatingStartValue;
                if (mauiRatings.TryGetValue((mauiPlayerId, week), out var r))
                    mauiRating = r;

                if (vbaRating == mauiRating)
                    matches++;
                else
                {
                    mismatches++;
                    var playerName = vbaPlayers.TryGetValue(vbaPlayerId, out var n) ? n : $"ID:{vbaPlayerId}";
                    mismatchDetails.Add((week, playerName, vbaRating, mauiRating, mauiRating - vbaRating));
                }
            }
        }

        var matchPct = totalComparisons > 0 ? (double)matches / totalComparisons * 100 : 0;
        sb.AppendLine($"SUMMARY: {matches}/{totalComparisons} match ({matchPct:F1}%), {mismatches} mismatches, {unmapped} unmapped");
        sb.AppendLine();

        if (mismatches > 0)
        {
            sb.AppendLine("=== MISMATCHES (first 50) ===");
            sb.AppendLine("Week | Player | VBA | MAUI | Diff");
            sb.AppendLine("-----|--------|-----|------|-----");
            foreach (var m in mismatchDetails.OrderBy(x => x.week).ThenBy(x => x.player).Take(50))
                sb.AppendLine($"{m.week,4} | {m.player,-20} | {m.vba,4} | {m.maui,4} | {m.diff:+#;-#;0}");

            sb.AppendLine();
            sb.AppendLine("=== MISMATCHES BY WEEK ===");
            foreach (var g in mismatchDetails.GroupBy(x => x.week).OrderBy(g => g.Key))
                sb.AppendLine($"Week {g.Key}: {g.Count()} mismatches, avg diff = {g.Average(x => Math.Abs(x.diff)):F1}");
        }

        return sb.ToString();
    }

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

        foreach (var pid in allPlayerIds)
            weeklyRatings[(pid, 1)] = settings.RatingStartValue;

        var fixturesByWeek = allFixtures
            .Where(f => f.Frames.Any())
            .OrderBy(f => f.Date).ThenBy(f => f.Id)
            .GroupBy(f => GetSeasonWeekNumber(f.Date, seasonStartDate))
            .OrderBy(g => g.Key).ToList();

        int maxWeek = fixturesByWeek.Any() ? fixturesByWeek.Max(g => g.Key) : 0;

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
                            playerFrameData[pid].Add((wkNo, frame.AwayPlayerId ?? Guid.Empty,
                                frame.Winner == FrameWinner.Home,
                                frame.EightBall && frame.Winner == FrameWinner.Home));
                        }
                        if (frame.AwayPlayerId.HasValue)
                        {
                            var pid = frame.AwayPlayerId.Value;
                            if (!playerFrameData.ContainsKey(pid))
                                playerFrameData[pid] = new List<(int, Guid, bool, bool)>();
                            playerFrameData[pid].Add((wkNo, frame.HomePlayerId ?? Guid.Empty,
                                frame.Winner == FrameWinner.Away,
                                frame.EightBall && frame.Winner == FrameWinner.Away));
                        }
                    }
                }
            }

            foreach (var pid in playerFrameData.Keys)
            {
                var frames = playerFrameData[pid].Where(f => f.weekNo <= wkNo).ToList();
                if (frames.Count == 0) continue;

                int biasX = settings.RatingWeighting;
                long valueTot = 0;
                long weightingTot = 0;

                foreach (var (fwkNo, oppId, won, eightBall) in frames)
                {
                    int oppRating = weeklyRatings.TryGetValue((oppId, fwkNo), out var r) ? r : settings.RatingStartValue;

                    int playerRating;
                    if (won)
                        playerRating = eightBall && settings.UseEightBallFactor
                            ? (int)(oppRating * settings.EightBallFactor)
                            : (int)(oppRating * settings.WinFactor);
                    else
                        playerRating = (int)(oppRating * settings.LossFactor);

                    valueTot += (long)playerRating * biasX;
                    weightingTot += biasX;
                    biasX += settings.RatingsBias;
                }

                weeklyRatings[(pid, wkNo + 1)] = weightingTot > 0 ? (int)(valueTot / weightingTot) : settings.RatingStartValue;
            }
        }

        return weeklyRatings;
    }

    private static int GetSeasonWeekNumber(DateTime matchDate, DateTime seasonStartDate)
    {
        var daysSinceStart = (matchDate.Date - seasonStartDate.Date).Days;
        return (daysSinceStart / 7) + 1;
    }

    public static string TracePlayerDetailedCalculation(
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
        sb.AppendLine($"=== DETAILED TRACE: {playerName} (VBA ID: {vbaPlayerId}) ===");
        sb.AppendLine($"Settings: Win={settings.WinFactor}, Loss={settings.LossFactor}, 8Ball={settings.EightBallFactor}");
        sb.AppendLine($"Weighting={settings.RatingWeighting}, Bias={settings.RatingsBias}");
        sb.AppendLine();

        var playerFrames = new List<(int weekNo, DateTime date, string oppName, bool won, bool eightBall)>();
        var playersById = DataStore.Data.Players.ToDictionary(p => p.Id, p => p.FullName ?? "Unknown");

        var fixturesByWeek = allFixtures
            .Where(f => f.Frames.Any())
            .OrderBy(f => f.Date).ThenBy(f => f.Id)
            .GroupBy(f => GetSeasonWeekNumber(f.Date, seasonStartDate))
            .OrderBy(g => g.Key).ToList();

        foreach (var weekGroup in fixturesByWeek)
        {
            foreach (var fixture in weekGroup.OrderBy(f => f.Date).ThenBy(f => f.Id))
            {
                foreach (var frame in fixture.Frames.OrderBy(fr => fr.Number))
                {
                    if (frame.HomePlayerId == mauiPlayerId && frame.AwayPlayerId.HasValue)
                    {
                        var oppName = playersById.TryGetValue(frame.AwayPlayerId.Value, out var on) ? on : "?";
                        playerFrames.Add((weekGroup.Key, fixture.Date, oppName,
                            frame.Winner == FrameWinner.Home,
                            frame.EightBall && frame.Winner == FrameWinner.Home));
                    }
                    else if (frame.AwayPlayerId == mauiPlayerId && frame.HomePlayerId.HasValue)
                    {
                        var oppName = playersById.TryGetValue(frame.HomePlayerId.Value, out var on) ? on : "?";
                        playerFrames.Add((weekGroup.Key, fixture.Date, oppName,
                            frame.Winner == FrameWinner.Away,
                            frame.EightBall && frame.Winner == FrameWinner.Away));
                    }
                }
            }
        }

        sb.AppendLine($"Total frames found: {playerFrames.Count}");
        sb.AppendLine();

        var week1Frames = playerFrames.Where(f => f.weekNo == 1).ToList();
        sb.AppendLine($"=== WEEK 1 FRAMES ({week1Frames.Count} frames) ===");

        if (week1Frames.Count > 0)
        {
            int biasX = settings.RatingWeighting;
            long valueTot = 0;
            long weightingTot = 0;
            int frameNum = 0;

            foreach (var (wkNo, date, oppName, won, eightBall) in week1Frames)
            {
                frameNum++;
                int oppRating = 1000;
                int playerRating;
                string factorUsed;

                if (won)
                {
                    if (eightBall && settings.UseEightBallFactor)
                    {
                        playerRating = (int)(oppRating * settings.EightBallFactor);
                        factorUsed = $"8Ball ({settings.EightBallFactor})";
                    }
                    else
                    {
                        playerRating = (int)(oppRating * settings.WinFactor);
                        factorUsed = $"Win ({settings.WinFactor})";
                    }
                }
                else
                {
                    playerRating = (int)(oppRating * settings.LossFactor);
                    factorUsed = $"Loss ({settings.LossFactor})";
                }

                valueTot += (long)playerRating * biasX;
                weightingTot += biasX;

                sb.AppendLine($"  Frame {frameNum}: vs {oppName,-20} {(won ? "WON" : "LOST")}{(eightBall ? " 8-BALL" : "")}");
                sb.AppendLine($"    OppRating={oppRating}, Factor={factorUsed}, PlayerRating={playerRating}");
                sb.AppendLine($"    BiasX={biasX}, FrameValue={playerRating * biasX}, RunningTotal={valueTot}/{weightingTot}");

                biasX += settings.RatingsBias;
            }

            int calculatedRating = (int)(valueTot / weightingTot);
            int vbaWeek2Rating = vbaRatings.TryGetValue((vbaPlayerId, 2), out var vr) ? vr : -1;

            sb.AppendLine();
            sb.AppendLine($"Week 2 Calculation: {valueTot} / {weightingTot} = {calculatedRating}");
            sb.AppendLine($"VBA Week 2 Rating: {vbaWeek2Rating}");
            sb.AppendLine($"Difference: {calculatedRating - vbaWeek2Rating:+#;-#;0}");
        }

        return sb.ToString();
    }
}
