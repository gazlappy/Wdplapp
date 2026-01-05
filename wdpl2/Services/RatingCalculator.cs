using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Shared rating calculator that implements the VBA-style rating algorithm.
/// This ensures consistent ratings across the app and website generation.
/// </summary>
public static class RatingCalculator
{
    /// <summary>
    /// Player stats including the calculated rating
    /// </summary>
    public sealed class PlayerStats
    {
        public Guid PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public string TeamName { get; set; } = "";
        public Guid? TeamId { get; set; }
        public Guid? DivisionId { get; set; }
        public int Played { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int EightBalls { get; set; }
        public int Rating { get; set; } = 1000;
        public double WinPercentage => Played > 0 ? (Wins * 100.0 / Played) : 0;
    }

    /// <summary>
    /// Calculate player ratings for all players in the given fixtures using VBA-style algorithm.
    /// </summary>
    /// <param name="fixtures">All fixtures with frames to process</param>
    /// <param name="players">Player lookup</param>
    /// <param name="teams">Team lookup</param>
    /// <param name="settings">Rating settings</param>
    /// <param name="seasonStartDate">Season start date for week calculation</param>
    /// <param name="divisionFilter">Optional: only return players from these divisions</param>
    /// <returns>Dictionary of player ID to calculated stats</returns>
    public static Dictionary<Guid, PlayerStats> CalculateAllRatings(
        List<Fixture> fixtures,
        List<Player> players,
        List<Team> teams,
        AppSettings settings,
        DateTime seasonStartDate,
        HashSet<Guid>? divisionFilter = null)
    {
        var playerById = players.ToDictionary(p => p.Id, p => p);
        var teamById = teams.ToDictionary(t => t.Id, t => t);

        // Order fixtures by date for proper processing
        var orderedFixtures = fixtures
            .Where(f => f.Frames.Any())
            .OrderBy(f => f.Date)
            .ThenBy(f => f.Id)
            .ToList();

        // Track frames per player with week info
        var playerFrameData = new Dictionary<Guid, List<FrameData>>();

        // VBA tblRatings: stores rating GOING INTO each week
        var weeklyRatings = new Dictionary<(Guid, int), int>();

        // Get ALL player IDs and initialize for Week 1
        var allPlayerIds = new HashSet<Guid>();
        foreach (var fixture in orderedFixtures)
        {
            foreach (var frame in fixture.Frames)
            {
                if (frame.HomePlayerId.HasValue) allPlayerIds.Add(frame.HomePlayerId.Value);
                if (frame.AwayPlayerId.HasValue) allPlayerIds.Add(frame.AwayPlayerId.Value);
            }
        }

        // Week 1 = starting rating for all players
        foreach (var playerId in allPlayerIds)
        {
            weeklyRatings[(playerId, 1)] = settings.RatingStartValue;
        }

        // Group fixtures by week
        var fixturesByWeek = orderedFixtures
            .GroupBy(f => GetSeasonWeekNumber(f.Date, seasonStartDate))
            .OrderBy(g => g.Key)
            .ToList();

        int maxWeek = fixturesByWeek.Any() ? fixturesByWeek.Max(g => g.Key) : 0;

        // VBA Algorithm - Process week by week
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
                        var frameWeekNo = frame.WeekNo ?? wkNo;

                        if (frame.HomePlayerId.HasValue)
                        {
                            var playerId = frame.HomePlayerId.Value;
                            if (!playerFrameData.ContainsKey(playerId))
                                playerFrameData[playerId] = new List<FrameData>();

                            playerFrameData[playerId].Add(new FrameData
                            {
                                OpponentId = frame.AwayPlayerId ?? Guid.Empty,
                                Won = frame.Winner == FrameWinner.Home,
                                EightBall = frame.EightBall && frame.Winner == FrameWinner.Home,
                                WeekNo = frameWeekNo,
                                VbaOppRating = frame.HomeOppRating,
                                VbaPlayerRating = frame.HomePlayerRating
                            });
                        }

                        if (frame.AwayPlayerId.HasValue)
                        {
                            var playerId = frame.AwayPlayerId.Value;
                            if (!playerFrameData.ContainsKey(playerId))
                                playerFrameData[playerId] = new List<FrameData>();

                            playerFrameData[playerId].Add(new FrameData
                            {
                                OpponentId = frame.HomePlayerId ?? Guid.Empty,
                                Won = frame.Winner == FrameWinner.Away,
                                EightBall = frame.EightBall && frame.Winner == FrameWinner.Away,
                                WeekNo = frameWeekNo,
                                VbaOppRating = frame.AwayOppRating,
                                VbaPlayerRating = frame.AwayPlayerRating
                            });
                        }
                    }
                }
            }

            // Calculate ratings for NEXT week (wkNo + 1)
            foreach (var playerId in playerFrameData.Keys.ToList())
            {
                var framesUpToNow = playerFrameData[playerId].Where(f => f.WeekNo <= wkNo).ToList();
                if (framesUpToNow.Count == 0) continue;

                int totalFrames = framesUpToNow.Count;
                int biasX = settings.RatingWeighting - (settings.RatingsBias * (totalFrames - 1));
                if (biasX < 1) biasX = 1;

                long valueTot = 0;
                long weightingTot = 0;

                foreach (var frameData in framesUpToNow)
                {
                    int ratingAttn;

                    // Use VBA pre-calculated PlayerRating if available
                    if (frameData.VbaPlayerRating.HasValue && frameData.VbaPlayerRating.Value > 0)
                    {
                        ratingAttn = frameData.VbaPlayerRating.Value;
                    }
                    else
                    {
                        // Calculate using opponent's weekly rating
                        int oppRating = weeklyRatings.TryGetValue((frameData.OpponentId, frameData.WeekNo), out var r)
                            ? r
                            : settings.RatingStartValue;

                        double ratingAttnDouble;
                        if (frameData.Won)
                        {
                            if (frameData.EightBall && settings.UseEightBallFactor)
                                ratingAttnDouble = oppRating * settings.EightBallFactor;
                            else
                                ratingAttnDouble = oppRating * settings.WinFactor;
                        }
                        else
                        {
                            ratingAttnDouble = oppRating * settings.LossFactor;
                        }

                        // Use integer truncation as VBA does
                        ratingAttn = (int)ratingAttnDouble;
                    }

                    valueTot += (long)ratingAttn * biasX;
                    weightingTot += biasX;
                    biasX += settings.RatingsBias;
                }

                int rating = weightingTot > 0 ? (int)(valueTot / weightingTot) : settings.RatingStartValue;
                weeklyRatings[(playerId, wkNo + 1)] = rating;
            }
        }

        // Build result dictionary
        var result = new Dictionary<Guid, PlayerStats>();
        int finalWeek = maxWeek + 1;

        foreach (var playerId in allPlayerIds)
        {
            if (!playerById.TryGetValue(playerId, out var player))
                continue;

            // Apply division filter if specified
            if (divisionFilter != null && player.TeamId.HasValue)
            {
                var team = teamById.GetValueOrDefault(player.TeamId.Value);
                if (team?.DivisionId == null || !divisionFilter.Contains(team.DivisionId.Value))
                    continue;
            }

            var frames = playerFrameData.GetValueOrDefault(playerId) ?? new List<FrameData>();
            var team2 = player.TeamId.HasValue ? teamById.GetValueOrDefault(player.TeamId.Value) : null;

            int finalRating = weeklyRatings.TryGetValue((playerId, finalWeek), out var fr)
                ? fr
                : settings.RatingStartValue;

            result[playerId] = new PlayerStats
            {
                PlayerId = playerId,
                PlayerName = player.FullName ?? $"{player.FirstName} {player.LastName}".Trim(),
                TeamName = team2?.Name ?? "",
                TeamId = player.TeamId,
                DivisionId = team2?.DivisionId,
                Played = frames.Count,
                Wins = frames.Count(f => f.Won),
                Losses = frames.Count(f => !f.Won),
                EightBalls = frames.Count(f => f.EightBall),
                Rating = finalRating
            };
        }

        return result;
    }

    /// <summary>
    /// Get the rating for a specific player
    /// </summary>
    public static int GetPlayerRating(
        Guid playerId,
        List<Fixture> fixtures,
        AppSettings settings,
        DateTime seasonStartDate)
    {
        var allRatings = CalculateAllRatings(
            fixtures,
            DataStore.Data.Players.ToList(),
            DataStore.Data.Teams.ToList(),
            settings,
            seasonStartDate);

        return allRatings.TryGetValue(playerId, out var stats)
            ? stats.Rating
            : settings.RatingStartValue;
    }

    // Helper class for frame data
    private sealed class FrameData
    {
        public Guid OpponentId { get; set; }
        public bool Won { get; set; }
        public bool EightBall { get; set; }
        public int WeekNo { get; set; }
        public int? VbaOppRating { get; set; }
        public int? VbaPlayerRating { get; set; }
    }

    // Get season week number (1-based, weeks since season start)
    private static int GetSeasonWeekNumber(DateTime matchDate, DateTime seasonStartDate)
    {
        var daysSinceStart = (matchDate.Date - seasonStartDate.Date).Days;
        return (daysSinceStart / 7) + 1;
    }
}
