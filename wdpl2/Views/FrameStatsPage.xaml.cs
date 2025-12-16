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
    private readonly ObservableCollection<PlayerOption> _players = new();
    private readonly ObservableCollection<DayPerformance> _dayPerformance = new();
    private PlayerOption? _selectedPlayer;
    private bool _allSeasonsMode = true;

    public FrameStatsPage()
    {
        InitializeComponent();
        
        PlayerPicker.ItemsSource = _players;
        DayPerformanceList.ItemsSource = _dayPerformance;
        
        LoadPlayers();
    }

    private void LoadPlayers()
    {
        _players.Clear();
        
        if (_allSeasonsMode)
        {
            // Group players - include those WITH GlobalPlayerId grouped together,
            // AND those WITHOUT GlobalPlayerId individually
            var playersWithGlobal = DataStore.Data.Players
                .Where(p => p.GlobalPlayerId.HasValue)
                .GroupBy(p => p.GlobalPlayerId!.Value)
                .Select(g => new PlayerOption
                {
                    GlobalPlayerId = g.Key,
                    DisplayName = g.First().FullName,
                    SubText = $"{g.Count()} season(s)",
                    PlayerIds = g.Select(p => p.Id).ToList()
                })
                .ToList();

            // Also include players without GlobalPlayerId (single season players)
            var playersWithoutGlobal = DataStore.Data.Players
                .Where(p => !p.GlobalPlayerId.HasValue)
                .Select(p => new PlayerOption
                {
                    GlobalPlayerId = p.Id, // Use their own ID as identifier
                    DisplayName = p.FullName,
                    SubText = "1 season",
                    PlayerIds = new List<Guid> { p.Id }
                })
                .ToList();

            // Combine and sort, deduping by name
            var allPlayers = playersWithGlobal
                .Concat(playersWithoutGlobal)
                .GroupBy(p => p.DisplayName.ToLower())
                .Select(g => g.OrderByDescending(p => p.PlayerIds.Count).First())
                .OrderBy(p => p.DisplayName)
                .ToList();

            foreach (var player in allPlayers)
                _players.Add(player);
            
            StatusLabel.Text = $"{_players.Count} player(s) available (all seasons)";
        }
        else
        {
            // Current season only
            var currentSeasonId = SeasonService.CurrentSeasonId;
            if (!currentSeasonId.HasValue)
            {
                StatusLabel.Text = "No season selected";
                return;
            }

            var players = DataStore.Data.Players
                .Where(p => p.SeasonId == currentSeasonId)
                .OrderBy(p => p.FullName)
                .ToList();

            foreach (var player in players)
            {
                _players.Add(new PlayerOption
                {
                    GlobalPlayerId = player.GlobalPlayerId ?? player.Id,
                    DisplayName = player.FullName,
                    SubText = "Current season",
                    PlayerIds = new List<Guid> { player.Id }
                });
            }

            StatusLabel.Text = $"{_players.Count} player(s) available";
        }
    }

    private void OnPlayerSelected(object? sender, EventArgs e)
    {
        _selectedPlayer = PlayerPicker.SelectedItem as PlayerOption;
        if (_selectedPlayer != null)
        {
            CalculateFrameStats();
        }
    }

    private void OnAllSeasonsToggled(object? sender, ToggledEventArgs e)
    {
        _allSeasonsMode = e.Value;
        _selectedPlayer = null;
        PlayerPicker.SelectedItem = null;
        _dayPerformance.Clear();
        ClearStats();
        LoadPlayers();
    }

    private void ClearStats()
    {
        FirstFrameLabel.Text = "-";
        FirstFrameStatsLabel.Text = "";
        LastFrameLabel.Text = "-";
        LastFrameStatsLabel.Text = "";
        ComebackLabel.Text = "-";
        ComebackStatsLabel.Text = "";
        BestOpponentLabel.Text = "-";
        BestOpponentStatsLabel.Text = "";
        BestVenueLabel.Text = "-";
        BestVenueStatsLabel.Text = "";
        CurrentStreakLabel.Text = "-";
        LongestWinStreakLabel.Text = "-";
        LongestLoseStreakLabel.Text = "-";
    }

    private void CalculateFrameStats()
    {
        if (_selectedPlayer == null)
            return;

        try
        {
            var playerIdSet = new HashSet<Guid>(_selectedPlayer.PlayerIds);
            
            List<Fixture> fixtures;
            if (_allSeasonsMode)
            {
                // Get ALL fixtures from ALL seasons
                fixtures = DataStore.Data.Fixtures
                    .Where(f => f.Frames.Any())
                    .OrderBy(f => f.Date)
                    .ToList();
            }
            else
            {
                var currentSeasonId = SeasonService.CurrentSeasonId;
                fixtures = DataStore.Data.Fixtures
                    .Where(f => f.SeasonId == currentSeasonId && f.Frames.Any())
                    .OrderBy(f => f.Date)
                    .ToList();
            }

            // Track frames chronologically
            var playerFrames = new List<(DateTime date, int frameNum, bool won, Guid? oppId, Guid? venueId)>();

            foreach (var fixture in fixtures)
            {
                foreach (var frame in fixture.Frames.OrderBy(f => f.Number))
                {
                    if (frame.HomePlayerId.HasValue && playerIdSet.Contains(frame.HomePlayerId.Value))
                    {
                        playerFrames.Add((fixture.Date, frame.Number, frame.Winner == FrameWinner.Home, frame.AwayPlayerId, fixture.VenueId));
                    }
                    else if (frame.AwayPlayerId.HasValue && playerIdSet.Contains(frame.AwayPlayerId.Value))
                    {
                        playerFrames.Add((fixture.Date, frame.Number, frame.Winner == FrameWinner.Away, frame.HomePlayerId, fixture.VenueId));
                    }
                }
            }

            if (!playerFrames.Any())
            {
                StatusLabel.Text = "No frame data available";
                ClearStats();
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
            else
            {
                BestOpponentLabel.Text = "-";
                BestOpponentStatsLabel.Text = "Min 5 frames required";
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
            else
            {
                BestVenueLabel.Text = "-";
                BestVenueStatsLabel.Text = "Min 3 frames required";
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

            var modeText = _allSeasonsMode ? "(All Seasons)" : "(Current Season)";
            StatusLabel.Text = $"Analyzed {playerFrames.Count} frames {modeText}";
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
        var modeText = _allSeasonsMode ? "ALL SEASONS" : "CURRENT SEASON";
        sb.AppendLine($"=== FRAME STATISTICS ({modeText}): {_selectedPlayer.DisplayName} ===");
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

        var fileName = $"FrameStats_{_selectedPlayer.DisplayName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
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
