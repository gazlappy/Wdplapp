using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using Wdpl2.Services;
using Wdpl2.Models;

namespace Wdpl2.Views;

public partial class FrameStatsPage : ContentPage
{
    private readonly ObservableCollection<Player> _players = new();
    private readonly ObservableCollection<DayPerformance> _dayPerformance = new();
    private Guid? _currentSeasonId;
    private Player? _selectedPlayer;

    public FrameStatsPage()
    {
        InitializeComponent();
        
        PlayerPicker.ItemsSource = _players;
        DayPerformanceList.ItemsSource = _dayPerformance;
        
        SeasonService.SeasonChanged += OnSeasonChanged;
        
        LoadPlayers();
    }

    ~FrameStatsPage()
    {
        SeasonService.SeasonChanged -= OnSeasonChanged;
    }

    private void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentSeasonId = e.NewSeasonId;
            LoadPlayers();
        });
    }

    private void LoadPlayers()
    {
        _currentSeasonId = SeasonService.CurrentSeasonId;
        
        if (!_currentSeasonId.HasValue)
        {
            StatusLabel.Text = "No season selected";
            return;
        }

        _players.Clear();
        var players = DataStore.Data.Players
            .Where(p => p.SeasonId == _currentSeasonId)
            .OrderBy(p => p.FullName)
            .ToList();

        foreach (var player in players)
            _players.Add(player);

        StatusLabel.Text = $"{_players.Count} player(s) available";
    }

    private void OnPlayerSelected(object? sender, EventArgs e)
    {
        _selectedPlayer = PlayerPicker.SelectedItem as Player;
        if (_selectedPlayer != null)
        {
            CalculateFrameStats();
        }
    }

    private void CalculateFrameStats()
    {
        if (_selectedPlayer == null || !_currentSeasonId.HasValue)
            return;

        try
        {
            var fixtures = DataStore.Data.Fixtures
                .Where(f => f.SeasonId == _currentSeasonId && f.Frames.Any())
                .OrderBy(f => f.Date)
                .ToList();

            // Track frames chronologically
            var playerFrames = new System.Collections.Generic.List<(DateTime date, int frameNum, bool won, Guid? oppId, Guid? venueId)>();

            foreach (var fixture in fixtures)
            {
                foreach (var frame in fixture.Frames.OrderBy(f => f.Number))
                {
                    if (frame.HomePlayerId == _selectedPlayer.Id)
                    {
                        playerFrames.Add((fixture.Date, frame.Number, frame.Winner == FrameWinner.Home, frame.AwayPlayerId, fixture.VenueId));
                    }
                    else if (frame.AwayPlayerId == _selectedPlayer.Id)
                    {
                        playerFrames.Add((fixture.Date, frame.Number, frame.Winner == FrameWinner.Away, frame.HomePlayerId, fixture.VenueId));
                    }
                }
            }

            if (!playerFrames.Any())
            {
                StatusLabel.Text = "No frame data available";
                return;
            }

            // First frame performance
            var firstFrames = playerFrames.Where(f => f.frameNum == 1).ToList();
            var firstWins = firstFrames.Count(f => f.won);
            FirstFrameLabel.Text = firstFrames.Any() ? $"{(double)firstWins / firstFrames.Count * 100:F1}%" : "0.0%";
            FirstFrameStatsLabel.Text = $"{firstWins}/{firstFrames.Count}";

            // Last frame performance (approximation - frames with high numbers)
            var lastFrames = playerFrames.Where(f => f.frameNum >= 8).ToList();
            var lastWins = lastFrames.Count(f => f.won);
            LastFrameLabel.Text = lastFrames.Any() ? $"{(double)lastWins / lastFrames.Count * 100:F1}%" : "0.0%";
            LastFrameStatsLabel.Text = $"{lastWins}/{lastFrames.Count}";

            // Calculate comebacks (simplified: won after losing first frame)
            var matchGroups = playerFrames.GroupBy(f => f.date);
            int comebacks = 0;
            foreach (var match in matchGroups)
            {
                var matchFrames = match.OrderBy(f => f.frameNum).ToList();
                if (matchFrames.Count > 1 && !matchFrames.First().won)
                {
                    // Count wins in later frames
                    if (matchFrames.Skip(1).Count(f => f.won) > matchFrames.Skip(1).Count(f => !f.won))
                        comebacks++;
                }
            }
            ComebackLabel.Text = comebacks.ToString();
            ComebackStatsLabel.Text = $"{comebacks} comeback win(s)";

            // Best opponent (most wins against)
            var opponentStats = playerFrames
                .Where(f => f.oppId.HasValue)
                .GroupBy(f => f.oppId)
                .Select(g => new
                {
                    OpponentId = g.Key!.Value,
                    Wins = g.Count(f => f.won),
                    Total = g.Count(),
                    WinRate = (double)g.Count(f => f.won) / g.Count()
                })
                .Where(s => s.Total >= 5)
                .OrderByDescending(s => s.WinRate)
                .FirstOrDefault();

            if (opponentStats != null)
            {
                var opponent = DataStore.Data.Players.FirstOrDefault(p => p.Id == opponentStats.OpponentId);
                BestOpponentLabel.Text = opponent?.FullName ?? "Unknown";
                BestOpponentStatsLabel.Text = $"{opponentStats.WinRate * 100:F1}% ({opponentStats.Wins}/{opponentStats.Total})";
            }

            // Best venue
            var venueStats = playerFrames
                .Where(f => f.venueId.HasValue)
                .GroupBy(f => f.venueId)
                .Select(g => new
                {
                    VenueId = g.Key!.Value,
                    Wins = g.Count(f => f.won),
                    Total = g.Count(),
                    WinRate = (double)g.Count(f => f.won) / g.Count()
                })
                .Where(s => s.Total >= 3)
                .OrderByDescending(s => s.WinRate)
                .FirstOrDefault();

            if (venueStats != null)
            {
                var venue = DataStore.Data.Venues.FirstOrDefault(v => v.Id == venueStats.VenueId);
                BestVenueLabel.Text = venue?.Name ?? "Unknown";
                BestVenueStatsLabel.Text = $"{venueStats.WinRate * 100:F1}% ({venueStats.Wins}/{venueStats.Total})";
            }

            // Performance by day of week
            _dayPerformance.Clear();
            var dayStats = playerFrames
                .GroupBy(f => f.date.DayOfWeek)
                .Select(g => new DayPerformance
                {
                    DayName = g.Key.ToString(),
                    Wins = g.Count(f => f.won),
                    Total = g.Count(),
                    WinRate = $"{(double)g.Count(f => f.won) / g.Count() * 100:F1}%",
                    Record = $"{g.Count(f => f.won)}W-{g.Count(f => !f.won)}L"
                })
                .OrderBy(d => (int)Enum.Parse<DayOfWeek>(d.DayName))
                .ToList();

            foreach (var day in dayStats)
                _dayPerformance.Add(day);

            // Streaks
            int currentStreak = 0;
            int longestWinStreak = 0;
            int longestLoseStreak = 0;
            int currentWinStreak = 0;
            int currentLoseStreak = 0;

            foreach (var frame in playerFrames)
            {
                if (frame.won)
                {
                    currentWinStreak++;
                    currentLoseStreak = 0;
                    currentStreak = currentWinStreak;
                    if (currentWinStreak > longestWinStreak)
                        longestWinStreak = currentWinStreak;
                }
                else
                {
                    currentLoseStreak++;
                    currentWinStreak = 0;
                    currentStreak = -currentLoseStreak;
                    if (currentLoseStreak > longestLoseStreak)
                        longestLoseStreak = currentLoseStreak;
                }
            }

            CurrentStreakLabel.Text = currentStreak > 0 ? $"{currentStreak}W" : currentStreak < 0 ? $"{-currentStreak}L" : "0";
            LongestWinStreakLabel.Text = longestWinStreak.ToString();
            LongestLoseStreakLabel.Text = longestLoseStreak.ToString();

            StatusLabel.Text = $"Analyzed {playerFrames.Count} frames";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (_selectedPlayer == null)
        {
            await DisplayAlert("No Player", "Please select a player first", "OK");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== FRAME STATISTICS: {_selectedPlayer.FullName} ===");
        sb.AppendLine();
        sb.AppendLine($"First Frame Win %: {FirstFrameLabel.Text}");
        sb.AppendLine($"Last Frame Win %: {LastFrameLabel.Text}");
        sb.AppendLine($"Comebacks: {ComebackLabel.Text}");
        sb.AppendLine($"Best Opponent: {BestOpponentLabel.Text} ({BestOpponentStatsLabel.Text})");
        sb.AppendLine($"Best Venue: {BestVenueLabel.Text} ({BestVenueStatsLabel.Text})");
        sb.AppendLine();
        sb.AppendLine("Performance by Day:");
        foreach (var day in _dayPerformance)
        {
            sb.AppendLine($"{day.DayName},{day.Record},{day.WinRate}");
        }

        var fileName = $"FrameStats_{_selectedPlayer.FullName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
        await System.IO.File.WriteAllTextAsync(path, sb.ToString());

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Export Frame Statistics",
            File = new ShareFile(path, "text/csv")
        });

        StatusLabel.Text = "Exported successfully";
    }
}

public class DayPerformance
{
    public string DayName { get; set; } = "";
    public int Wins { get; set; }
    public int Total { get; set; }
    public string WinRate { get; set; } = "";
    public string Record { get; set; } = "";
}
