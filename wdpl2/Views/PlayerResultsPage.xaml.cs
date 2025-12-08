using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class PlayerResultsPage : ContentPage
{
    private readonly ObservableCollection<PlayerResultRow> _results = new();
    private Guid _playerId;
    private string _playerName = "";
    private int _currentRating = 1000;

    public PlayerResultsPage()
    {
        InitializeComponent();
        ResultsList.ItemsSource = _results;
    }

    public void LoadPlayer(Guid playerId, string playerName, int currentRating = 1000)
    {
        _playerId = playerId;
        _playerName = playerName;
        _currentRating = currentRating;

        PlayerNameLabel.Text = $"{playerName.ToUpper()}'s Results";
        LoadPlayerResults();
    }

    private void LoadPlayerResults()
    {
        _results.Clear();

        var data = DataStore.Data;
        var settings = data.Settings;

        // Get player info
        var player = data.Players.FirstOrDefault(p => p.Id == _playerId);
        if (player == null)
        {
            TeamLabel.Text = "Team: Unknown";
            return;
        }

        // Get team info
        var team = player.TeamId.HasValue
            ? data.Teams.FirstOrDefault(t => t.Id == player.TeamId)
            : null;
        TeamLabel.Text = $"Team: {team?.Name ?? "Free Agent"}";

        // Get current season
        var currentSeasonId = SeasonService.CurrentSeasonId;
        if (!currentSeasonId.HasValue) return;

        // Get the season's START DATE (not min fixture date!)
        var season = data.Seasons.FirstOrDefault(s => s.Id == currentSeasonId);
        if (season == null) return;
        
        var seasonStartDate = season.StartDate;

        // Get all fixtures for this season
        var fixtures = data.Fixtures
            .Where(f => f.SeasonId == currentSeasonId && f.Frames.Any())
            .OrderBy(f => f.Date)
            .ThenBy(f => f.Id)
            .ToList();

        if (!fixtures.Any())
        {
            LastUpdatedLabel.Text = "No results available";
            return;
        }

        var lastMatchDate = fixtures.Max(f => f.Date);
        LastUpdatedLabel.Text = $"Last updated: {lastMatchDate:dd/MM/yyyy}";

        // Get teams dictionary for opponent team lookup
        var teamsById = data.Teams.ToDictionary(t => t.Id, t => t);
        var playersById = data.Players.ToDictionary(p => p.Id, p => p);

        // Collect all frames for this player in CHRONOLOGICAL order
        var playerFrames = new System.Collections.Generic.List<(DateTime date, int weekNo, Fixture fixture, FrameResult frame, bool isHome)>();

        foreach (var fixture in fixtures)
        {
            int weekNo = GetSeasonWeekNumber(fixture.Date, seasonStartDate);
            foreach (var frame in fixture.Frames.OrderBy(fr => fr.Number))
            {
                if (frame.HomePlayerId == _playerId)
                {
                    playerFrames.Add((fixture.Date, weekNo, fixture, frame, true));
                }
                else if (frame.AwayPlayerId == _playerId)
                {
                    playerFrames.Add((fixture.Date, weekNo, fixture, frame, false));
                }
            }
        }

        if (!playerFrames.Any())
        {
            LastUpdatedLabel.Text = "No frames found for this player";
            return;
        }

        // Build weekly ratings for all players (to get opponent ratings)
        var weeklyRatings = BuildWeeklyRatings(fixtures, settings, seasonStartDate);

        int totalFrames = playerFrames.Count;
        var resultRows = new System.Collections.Generic.List<PlayerResultRow>();

        // VBA Algorithm: Weight calculation
        // The NEWEST frame gets the base weighting (220)
        // Each OLDER frame decrements by RatingsBias (4)
        // So for display (newest first), weights go: 220, 216, 212, 208...
        
        // Process frames in CHRONOLOGICAL order (oldest first) for calculation
        // Frame 1 (oldest) gets: 220 - (4 × (totalFrames - 1))
        // Frame N (newest) gets: 220
        
        int startingBiasX = settings.RatingWeighting - (settings.RatingsBias * (totalFrames - 1));
        if (startingBiasX < 1) startingBiasX = 1;
        
        int biasX = startingBiasX;
        long runningValueTot = 0;
        long runningWeightingTot = 0;

        foreach (var (date, weekNo, fixture, frame, isHome) in playerFrames)
        {
            var oppId = isHome ? frame.AwayPlayerId : frame.HomePlayerId;
            var won = isHome ? frame.Winner == FrameWinner.Home : frame.Winner == FrameWinner.Away;
            var eightBall = frame.EightBall && won;

            // Get opponent info
            Player? oppPlayer = null;
            if (oppId.HasValue && playersById.TryGetValue(oppId.Value, out var op))
                oppPlayer = op;
            
            var oppTeamId = oppPlayer?.TeamId;
            Team? oppTeam = null;
            if (oppTeamId.HasValue && teamsById.TryGetValue(oppTeamId.Value, out var ot))
                oppTeam = ot;

            // VBA Algorithm: Get opponent rating for the CURRENT week (not final week)
            // VBA: GetOppRating = DLookup("Rating", "tblRatings", "[PlayerID] = " & rs2!Played & " and [WeekNo] = " & WkNo)
            // The week number used is the week when the match was played
            int lookupWeek = weekNo; // Use the week of the match, not final week
            int oppRating = settings.RatingStartValue;
            if (oppId.HasValue && weeklyRatings.TryGetValue((oppId.Value, lookupWeek), out var r))
                oppRating = r;

            // Calculate rating attained (VBA-style integer truncation)
            int ratingAttained;
            if (won)
            {
                if (eightBall && settings.UseEightBallFactor)
                    ratingAttained = (int)(oppRating * settings.EightBallFactor);
                else
                    ratingAttained = (int)(oppRating * settings.WinFactor);
            }
            else
            {
                ratingAttained = (int)(oppRating * settings.LossFactor);
            }

            // Calculate rating value for this frame
            long ratingValue = (long)ratingAttained * biasX;
            runningValueTot += ratingValue;
            runningWeightingTot += biasX;

            resultRows.Add(new PlayerResultRow
            {
                MatchDate = date,
                WeekNo = weekNo,
                Won = won,
                OpponentName = oppPlayer?.FullName ?? "Unknown",
                OpponentTeam = oppTeam?.Name ?? "Unknown",
                EightBall = eightBall,
                RatingAttained = ratingAttained,
                Weighting = biasX,
                RatingValue = ratingValue
            });

            biasX += settings.RatingsBias;
        }

        // Now add to display collection in reverse order (newest first)
        // This matches VBA display where newest is at top with highest weight
        foreach (var row in resultRows.AsEnumerable().Reverse())
        {
            _results.Add(row);
        }

        // Update totals
        int totalWins = resultRows.Count(r => r.Won);
        int totalLosses = resultRows.Count(r => !r.Won);
        int total8Balls = resultRows.Count(r => r.EightBall);
        long totalWeighting = resultRows.Sum(r => r.Weighting);
        long totalRatingValue = resultRows.Sum(r => r.RatingValue);

        TotalEightBallsLabel.Text = total8Balls.ToString();
        TotalWeightingLabel.Text = totalWeighting.ToString();
        TotalRatingValueLabel.Text = totalRatingValue.ToString();

        // Update summary
        SummaryPlayerName.Text = _playerName.ToUpper();
        
        // Calculate max available frames
        // In VBA this is based on available match slots for the player's team
        int maxPossible = fixtures.Count * settings.DefaultFramesPerMatch;
        double maxPct = maxPossible > 0 ? (double)totalFrames / maxPossible * 100 : 0;
        SummaryMaxLabel.Text = $"{maxPct:F0}%";
        
        SummaryPlayedLabel.Text = totalFrames.ToString();
        SummaryWonLabel.Text = totalWins.ToString();
        SummaryLostLabel.Text = totalLosses.ToString();
        Summary8BallsLabel.Text = total8Balls.ToString();

        double winPct = totalFrames > 0 ? (double)totalWins / totalFrames * 100 : 0;
        SummaryWinPctLabel.Text = $"{winPct:F0}%";

        // Current rating (calculated from totals)
        int calculatedRating = runningWeightingTot > 0 
            ? (int)(runningValueTot / runningWeightingTot) 
            : settings.RatingStartValue;
        SummaryRatingLabel.Text = calculatedRating.ToString();
    }

    private System.Collections.Generic.Dictionary<(Guid, int), int> BuildWeeklyRatings(
        System.Collections.Generic.List<Fixture> allFixtures,
        AppSettings settings,
        DateTime seasonStartDate)
    {
        var weeklyRatings = new System.Collections.Generic.Dictionary<(Guid, int), int>();
        var playerFrameData = new System.Collections.Generic.Dictionary<Guid, System.Collections.Generic.List<(int weekNo, Guid oppId, bool won, bool eightBall)>>();
        var allPlayerIds = new System.Collections.Generic.HashSet<Guid>();

        foreach (var fixture in allFixtures.Where(f => f.Frames.Any()))
        {
            foreach (var frame in fixture.Frames)
            {
                if (frame.HomePlayerId.HasValue) allPlayerIds.Add(frame.HomePlayerId.Value);
                if (frame.AwayPlayerId.HasValue) allPlayerIds.Add(frame.AwayPlayerId.Value);
            }
        }

        // Initialize all players with RatingStartValue for Week 1
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

        // VBA Algorithm - Process week by week:
        // For each week, calculate ratings using the CURRENT week's opponent ratings
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
                                playerFrameData[pid] = new System.Collections.Generic.List<(int, Guid, bool, bool)>();
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
                                playerFrameData[pid] = new System.Collections.Generic.List<(int, Guid, bool, bool)>();
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

            // Calculate rating for each player using ALL their frames up to this week
            // VBA uses the CURRENT week number (wkNo) for opponent rating lookup
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
                    // VBA: GetOppRating = DLookup("Rating", "tblRatings", "[PlayerID] = " & rs2!Played & " and [WeekNo] = " & WkNo)
                    // Use CURRENT week (wkNo) for opponent rating lookup
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
                // Store rating for current week AND next week as starting point
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

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}

/// <summary>
/// Row data for displaying a single frame result.
/// </summary>
public class PlayerResultRow
{
    public DateTime MatchDate { get; set; }
    public int WeekNo { get; set; }
    public bool Won { get; set; }
    public string WonLostText => Won ? "WON" : "LOST";
    public Color WonLostColor => Won ? Colors.Green : Colors.Red;
    public string OpponentName { get; set; } = "";
    public string OpponentTeam { get; set; } = "";
    public bool EightBall { get; set; }
    public string EightBallText => EightBall ? "1" : "0";
    public int RatingAttained { get; set; }
    public int Weighting { get; set; }
    public long RatingValue { get; set; }
}
