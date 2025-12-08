using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Debug service to trace rating calculations frame-by-frame for comparison with VBA Access database.
/// </summary>
/// <remarks>
/// VBA DATA STRUCTURES:
/// - tblRatings: Weekly snapshots (ID, WeekNo, PlayerID, Rating)
/// - tblPlayerResult: Frame results (PlayerResultID, MatchNo, FrameNo, PlayerID, Played, Result, EightBall, OppRating, PlayerRating, WeekNo)
/// 
/// KEY VBA ALGORITHM:
/// 1. All players start Week 1 at RatingStartValue (1000)
/// 2. OppRating = Opponent's rating from tblRatings for CURRENT week (not week+1!)
/// 3. PlayerRating = OppRating × Factor (Win=1.25, Loss=0.75, 8-Ball=1.35)
/// 4. Final Rating = ?(PlayerRating × BiasX) / ?(BiasX)
/// 5. BiasX starts at (RatingWeighting - 4 × (TotalFrames-1)) for oldest frame
/// 
/// CRITICAL: VBA looks up opponent rating using the SAME week number being calculated.
/// This means as ratings are recalculated week by week, opponent ratings evolve.
/// </remarks>
public static class RatingDebugService
{
    public class FrameCalculationDebug
    {
        public int FrameNumber { get; set; }
        public DateTime MatchDate { get; set; }
        public int WeekNo { get; set; }
        public string OpponentName { get; set; } = "";
        public int OpponentRating { get; set; }
        public bool Won { get; set; }
        public bool EightBall { get; set; }
        public int BiasX { get; set; }
        public int RatingAttn { get; set; }
        public long ValueTot { get; set; }
        public long WeightingTot { get; set; }
        public int CalculatedRating { get; set; }
    }

    public class WeeklyRatingSnapshot
    {
        public int WeekNo { get; set; }
        public int Rating { get; set; }
        public int FramesPlayed { get; set; }
    }

    /// <summary>
    /// Calculate rating for a single player with detailed debug output matching VBA algorithm.
    /// </summary>
    public static (List<FrameCalculationDebug> Frames, List<WeeklyRatingSnapshot> WeeklyRatings) CalculateWithDebug(
        Guid playerId,
        List<Fixture> allFixtures,
        AppSettings settings,
        DateTime seasonStartDate)
    {
        var frameDebug = new List<FrameCalculationDebug>();
        var weeklySnapshots = new List<WeeklyRatingSnapshot>();

        var playerFrames = new List<(DateTime date, int weekNo, Guid oppId, bool won, bool eightBall, int matchNo, int frameNo)>();

        var fixturesByWeek = allFixtures
            .Where(f => f.Frames.Any())
            .OrderBy(f => f.Date)
            .ThenBy(f => f.Id)
            .GroupBy(f => GetSeasonWeekNumber(f.Date, seasonStartDate))
            .OrderBy(g => g.Key)
            .ToList();

        int matchNo = 0;
        foreach (var weekGroup in fixturesByWeek)
        {
            foreach (var fixture in weekGroup.OrderBy(f => f.Date).ThenBy(f => f.Id))
            {
                matchNo++;
                foreach (var frame in fixture.Frames.OrderBy(fr => fr.Number))
                {
                    if (frame.HomePlayerId == playerId && frame.AwayPlayerId.HasValue)
                    {
                        playerFrames.Add((
                            fixture.Date,
                            weekGroup.Key,
                            frame.AwayPlayerId.Value,
                            frame.Winner == FrameWinner.Home,
                            frame.EightBall && frame.Winner == FrameWinner.Home,
                            matchNo,
                            frame.Number
                        ));
                    }
                    else if (frame.AwayPlayerId == playerId && frame.HomePlayerId.HasValue)
                    {
                        playerFrames.Add((
                            fixture.Date,
                            weekGroup.Key,
                            frame.HomePlayerId.Value,
                            frame.Winner == FrameWinner.Away,
                            frame.EightBall && frame.Winner == FrameWinner.Away,
                            matchNo,
                            frame.Number
                        ));
                    }
                }
            }
        }

        if (playerFrames.Count == 0)
            return (frameDebug, weeklySnapshots);

        // Build weekly ratings for ALL players using VBA algorithm
        var allPlayerRatings = BuildWeeklyRatings(allFixtures, settings, seasonStartDate);
        var playerNames = DataStore.Data.Players.ToDictionary(p => p.Id, p => p.FullName ?? "Unknown");

        int maxWeek = playerFrames.Max(f => f.weekNo);

        // Calculate frame-by-frame for display (using final week's opponent ratings for display)
        int totalFrames = playerFrames.Count;
        int startingBiasX = settings.RatingWeighting - (settings.RatingsBias * (totalFrames - 1));
        if (startingBiasX < 1) startingBiasX = 1;
        
        int biasX = startingBiasX;
        long valueTot = 0;
        long weightingTot = 0;
        int frameNum = 0;

        foreach (var (date, fwkNo, oppId, won, eightBall, mNo, fNo) in playerFrames)
        {
            frameNum++;

            // VBA uses CURRENT week for opponent rating lookup
            // For display, we use the week of the frame
            int oppRating = allPlayerRatings.TryGetValue((oppId, fwkNo), out var r)
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
            int currentRating = weightingTot > 0 ? (int)(valueTot / weightingTot) : settings.RatingStartValue;

            frameDebug.Add(new FrameCalculationDebug
            {
                FrameNumber = frameNum,
                MatchDate = date,
                WeekNo = fwkNo,
                OpponentName = playerNames.TryGetValue(oppId, out var n) ? n : oppId.ToString(),
                OpponentRating = oppRating,
                Won = won,
                EightBall = eightBall,
                BiasX = biasX,
                RatingAttn = ratingAttn,
                ValueTot = valueTot,
                WeightingTot = weightingTot,
                CalculatedRating = currentRating
            });

            biasX += settings.RatingsBias;
        }

        // Build weekly snapshots
        for (int wkNo = 1; wkNo <= maxWeek; wkNo++)
        {
            if (allPlayerRatings.TryGetValue((playerId, wkNo), out var rating))
            {
                var framesCount = playerFrames.Count(f => f.weekNo <= wkNo);
                weeklySnapshots.Add(new WeeklyRatingSnapshot
                {
                    WeekNo = wkNo,
                    Rating = rating,
                    FramesPlayed = framesCount
                });
            }
        }

        return (frameDebug, weeklySnapshots);
    }

    /// <summary>
    /// Build weekly ratings for ALL players (simulates VBA tblRatings table).
    /// VBA Algorithm: For each week, recalculate all ratings using CURRENT week's opponent ratings.
    /// </summary>
    private static Dictionary<(Guid, int), int> BuildWeeklyRatings(
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

        // Process week by week - this is the VBA algorithm
        for (int wkNo = 1; wkNo <= maxWeek; wkNo++)
        {
            // Add frames from this week
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

            // Recalculate ratings for ALL players who have played
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
                    // VBA: GetOppRating uses CURRENT week (wkNo)
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
                weeklyRatings[(pid, wkNo + 1)] = rating; // Carry forward to next week
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
    /// Export debug calculation to CSV format matching VBA tblPlayerResult structure.
    /// </summary>
    public static string ExportToCsv(List<FrameCalculationDebug> debug, List<WeeklyRatingSnapshot> weeklyRatings, string playerName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== RATING CALCULATION DEBUG: {playerName} ===");
        sb.AppendLine();
        sb.AppendLine("=== FRAME-BY-FRAME CALCULATION ===");
        sb.AppendLine("Frame#,Date,Week,Opponent,OppRating,Won,8Ball,BiasX,RatingAttn,ValueTot,WeightingTot,CalcRating");

        foreach (var frame in debug)
        {
            sb.AppendLine($"{frame.FrameNumber},{frame.MatchDate:yyyy-MM-dd},{frame.WeekNo}," +
                $"{frame.OpponentName},{frame.OpponentRating},{(frame.Won ? "Y" : "N")}," +
                $"{(frame.EightBall ? "Y" : "N")},{frame.BiasX},{frame.RatingAttn}," +
                $"{frame.ValueTot},{frame.WeightingTot},{frame.CalculatedRating}");
        }

        sb.AppendLine();
        sb.AppendLine("=== WEEKLY RATINGS (matches tblRatings) ===");
        sb.AppendLine("WeekNo,Rating,FramesPlayed");
        foreach (var week in weeklyRatings)
        {
            sb.AppendLine($"{week.WeekNo},{week.Rating},{week.FramesPlayed}");
        }

        return sb.ToString();
    }
}
