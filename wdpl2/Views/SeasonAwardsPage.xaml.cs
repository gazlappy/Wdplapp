using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class SeasonAwardsPage : ContentPage
{
    private readonly ObservableCollection<DivisionMVP> _divisionMVPs = new();
    private Guid? _currentSeasonId;

    public SeasonAwardsPage()
    {
        InitializeComponent();
        
        DivisionMVPsList.ItemsSource = _divisionMVPs;
        
        SeasonService.SeasonChanged += OnSeasonChanged;
        
        LoadAwards();
    }

    ~SeasonAwardsPage()
    {
        SeasonService.SeasonChanged -= OnSeasonChanged;
    }

    private void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentSeasonId = e.NewSeasonId;
            LoadAwards();
        });
    }

    private void LoadAwards()
    {
        try
        {
            _currentSeasonId = SeasonService.CurrentSeasonId;
            
            if (!_currentSeasonId.HasValue)
            {
                SeasonLabel.Text = "No season selected";
                ClearAwards();
                return;
            }

            var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
            SeasonLabel.Text = season?.Name ?? "Unknown Season";

            // Get all players and their stats for this season
            var players = DataStore.Data.Players.Where(p => p.SeasonId == _currentSeasonId).ToList();
            var fixtures = DataStore.Data.Fixtures.Where(f => f.SeasonId == _currentSeasonId && f.Frames.Any()).ToList();

            if (!players.Any() || !fixtures.Any())
            {
                StatusLabel.Text = "No data available for awards";
                ClearAwards();
                return;
            }

            // Calculate stats for each player
            var playerStats = players.Select(p => CalculatePlayerStats(p, fixtures)).Where(ps => ps != null).ToList();

            // Most Improved (highest rating gain) - need to track rating changes over time
            var mostImproved = playerStats.OrderByDescending(ps => ps.FinalRating - ps.StartingRating).FirstOrDefault();
            if (mostImproved != null)
            {
                MostImprovedLabel.Text = mostImproved.PlayerName;
                MostImprovedStatsLabel.Text = $"Rating: {mostImproved.StartingRating} ? {mostImproved.FinalRating} (+{mostImproved.FinalRating - mostImproved.StartingRating})";
            }

            // Most 8-Balls
            var most8Balls = playerStats.OrderByDescending(ps => ps.EightBalls).FirstOrDefault();
            if (most8Balls != null)
            {
                Most8BallsLabel.Text = most8Balls.PlayerName;
                Most8BallsStatsLabel.Text = $"{most8Balls.EightBalls} clearances in {most8Balls.FramesPlayed} frames";
            }

            // Highest Win %
            var highestWinPct = playerStats.Where(ps => ps.FramesPlayed >= 10).OrderByDescending(ps => ps.WinPercentage).FirstOrDefault();
            if (highestWinPct != null)
            {
                HighestWinPctLabel.Text = highestWinPct.PlayerName;
                HighestWinPctStatsLabel.Text = $"{highestWinPct.WinPercentage:F1}% ({highestWinPct.FramesWon}W-{highestWinPct.FramesLost}L in {highestWinPct.FramesPlayed} frames)";
            }

            // Most Consistent (lowest variance in performance)
            var mostConsistent = playerStats.Where(ps => ps.FramesPlayed >= 10).OrderBy(ps => ps.PerformanceVariance).FirstOrDefault();
            if (mostConsistent != null)
            {
                MostConsistentLabel.Text = mostConsistent.PlayerName;
                MostConsistentStatsLabel.Text = $"{mostConsistent.WinPercentage:F1}% win rate, Low variance: {mostConsistent.PerformanceVariance:F1}";
            }

            // Top Rated
            var topRated = playerStats.OrderByDescending(ps => ps.FinalRating).FirstOrDefault();
            if (topRated != null)
            {
                TopRatedLabel.Text = topRated.PlayerName;
                TopRatedStatsLabel.Text = $"Rating: {topRated.FinalRating} ({topRated.FramesPlayed} frames played)";
            }

            // Most Active
            var mostActive = playerStats.OrderByDescending(ps => ps.FramesPlayed).FirstOrDefault();
            if (mostActive != null)
            {
                MostActiveLabel.Text = mostActive.PlayerName;
                MostActiveStatsLabel.Text = $"{mostActive.FramesPlayed} frames across {mostActive.MatchesPlayed} matches";
            }

            // Division MVPs
            LoadDivisionMVPs(playerStats);

            StatusLabel.Text = $"Awards calculated from {playerStats.Count} players";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error loading awards: {ex.Message}";
        }
    }

    private PlayerSeasonStats? CalculatePlayerStats(Models.Player player, System.Collections.Generic.List<Models.Fixture> fixtures)
    {
        var stats = new PlayerSeasonStats
        {
            PlayerId = player.Id,
            PlayerName = player.FullName,
            StartingRating = DataStore.Data.Settings.RatingStartValue
        };

        var playerTeam = DataStore.Data.Teams.FirstOrDefault(t => t.Id == player.TeamId);
        stats.DivisionId = playerTeam?.DivisionId;

        var matchCount = 0;
        var wins = new System.Collections.Generic.List<bool>();

        foreach (var fixture in fixtures)
        {
            bool inMatch = false;
            foreach (var frame in fixture.Frames)
            {
                if (frame.HomePlayerId == player.Id)
                {
                    inMatch = true;
                    stats.FramesPlayed++;
                    var won = frame.Winner == Models.FrameWinner.Home;
                    if (won) stats.FramesWon++;
                    else stats.FramesLost++;
                    if (frame.EightBall && won) stats.EightBalls++;
                    wins.Add(won);
                }
                else if (frame.AwayPlayerId == player.Id)
                {
                    inMatch = true;
                    stats.FramesPlayed++;
                    var won = frame.Winner == Models.FrameWinner.Away;
                    if (won) stats.FramesWon++;
                    else stats.FramesLost++;
                    if (frame.EightBall && won) stats.EightBalls++;
                    wins.Add(won);
                }
            }
            if (inMatch) matchCount++;
        }

        stats.MatchesPlayed = matchCount;
        stats.WinPercentage = stats.FramesPlayed > 0 ? (double)stats.FramesWon / stats.FramesPlayed * 100.0 : 0;

        // Calculate performance variance (standard deviation of win/loss sequence)
        if (wins.Count > 0)
        {
            var mean = wins.Average(w => w ? 1.0 : 0.0);
            var variance = wins.Average(w => Math.Pow((w ? 1.0 : 0.0) - mean, 2));
            stats.PerformanceVariance = variance;
        }

        // Simplified rating calculation (would need full history for accurate)
        stats.FinalRating = stats.StartingRating + (int)(stats.WinPercentage * 5); // Rough approximation

        return stats.FramesPlayed > 0 ? stats : null;
    }

    private void LoadDivisionMVPs(System.Collections.Generic.List<PlayerSeasonStats> playerStats)
    {
        _divisionMVPs.Clear();

        var divisions = DataStore.Data.Divisions.Where(d => d.SeasonId == _currentSeasonId).ToList();

        foreach (var division in divisions)
        {
            var divisionPlayers = playerStats.Where(ps => ps.DivisionId == division.Id).ToList();
            var mvp = divisionPlayers.OrderByDescending(ps => ps.FinalRating).FirstOrDefault();

            if (mvp != null)
            {
                _divisionMVPs.Add(new DivisionMVP
                {
                    DivisionName = division.Name,
                    PlayerName = mvp.PlayerName,
                    Rating = mvp.FinalRating,
                    Stats = $"{mvp.FramesPlayed} frames, {mvp.WinPercentage:F1}% win rate"
                });
            }
        }
    }

    private void ClearAwards()
    {
        MostImprovedLabel.Text = "-";
        MostImprovedStatsLabel.Text = "";
        Most8BallsLabel.Text = "-";
        Most8BallsStatsLabel.Text = "";
        HighestWinPctLabel.Text = "-";
        HighestWinPctStatsLabel.Text = "";
        MostConsistentLabel.Text = "-";
        MostConsistentStatsLabel.Text = "";
        TopRatedLabel.Text = "-";
        TopRatedStatsLabel.Text = "";
        MostActiveLabel.Text = "-";
        MostActiveStatsLabel.Text = "";
        _divisionMVPs.Clear();
    }

    private async void OnExportAwardsClicked(object? sender, EventArgs e)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== SEASON AWARDS: {SeasonLabel.Text} ===");
        sb.AppendLine();
        sb.AppendLine($"Most Improved Player,{MostImprovedLabel.Text},{MostImprovedStatsLabel.Text}");
        sb.AppendLine($"Most 8-Balls,{Most8BallsLabel.Text},{Most8BallsStatsLabel.Text}");
        sb.AppendLine($"Highest Win %,{HighestWinPctLabel.Text},{HighestWinPctStatsLabel.Text}");
        sb.AppendLine($"Most Consistent,{MostConsistentLabel.Text},{MostConsistentStatsLabel.Text}");
        sb.AppendLine($"Highest Rating,{TopRatedLabel.Text},{TopRatedStatsLabel.Text}");
        sb.AppendLine($"Most Active,{MostActiveLabel.Text},{MostActiveStatsLabel.Text}");
        sb.AppendLine();
        sb.AppendLine("Division MVPs:");
        foreach (var mvp in _divisionMVPs)
        {
            sb.AppendLine($"{mvp.DivisionName},{mvp.PlayerName},{mvp.Rating},{mvp.Stats}");
        }

        var fileName = $"SeasonAwards_{SeasonLabel.Text?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
        await System.IO.File.WriteAllTextAsync(path, sb.ToString());

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Export Season Awards",
            File = new ShareFile(path, "text/csv")
        });

        StatusLabel.Text = "Awards exported successfully";
    }
}

public class PlayerSeasonStats
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public Guid? DivisionId { get; set; }
    public int FramesPlayed { get; set; }
    public int FramesWon { get; set; }
    public int FramesLost { get; set; }
    public int EightBalls { get; set; }
    public int MatchesPlayed { get; set; }
    public double WinPercentage { get; set; }
    public double PerformanceVariance { get; set; }
    public int StartingRating { get; set; }
    public int FinalRating { get; set; }
}

public class DivisionMVP
{
    public string DivisionName { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public int Rating { get; set; }
    public string Stats { get; set; } = "";
}
