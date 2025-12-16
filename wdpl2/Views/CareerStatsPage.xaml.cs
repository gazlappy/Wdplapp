using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.ViewModels;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class CareerStatsPage : ContentPage
{
    private readonly ObservableCollection<PlayerCareerStats> _players = new();
    private readonly ObservableCollection<SeasonStats> _seasonBreakdown = new();
    private bool _isFlyoutOpen = false;

    public CareerStatsPage()
    {
        InitializeComponent();

        PlayersList.ItemsSource = _players;
        SeasonBreakdownList.ItemsSource = _seasonBreakdown;

        // Wire up events
        BurgerMenuBtn.Clicked += OnBurgerMenuClicked;
        CloseFlyoutBtn.Clicked += OnCloseFlyoutClicked;
        OverlayTap.Tapped += (_, __) => CloseFlyout();
        SearchEntry.TextChanged += (_, __) => RefreshList();
        PlayersList.SelectionChanged += OnPlayerSelected;

        // NEW: Export button
        var exportBtn = new Button
        {
            Text = "Export to CSV",
            BackgroundColor = Color.FromArgb("#10B981"),
            TextColor = Colors.White,
            Margin = new Thickness(0, 8)
        };
        exportBtn.Clicked += async (_, __) => await ExportCareerStatsToCsvAsync();
        
        // Add to flyout panel (find the VerticalStackLayout in flyout)
        var flyoutContent = (FlyoutPanel.Content as ScrollView)?.Content as VerticalStackLayout;
        if (flyoutContent != null)
        {
            flyoutContent.Children.Insert(flyoutContent.Children.Count, exportBtn);
        }

        RefreshList();
    }

    private void RefreshList()
    {
        _players.Clear();

        var allPlayers = DataStore.Data.Players;
        var allFixtures = DataStore.Data.Fixtures;
        var allSeasons = DataStore.Data.Seasons;

        System.Diagnostics.Debug.WriteLine("=== CAREER STATS DEBUG ===");
        System.Diagnostics.Debug.WriteLine($"Total players: {allPlayers.Count}");
        System.Diagnostics.Debug.WriteLine($"Total fixtures: {allFixtures.Count}");
        System.Diagnostics.Debug.WriteLine($"Total seasons: {allSeasons.Count}");
        System.Diagnostics.Debug.WriteLine($"Players with GlobalPlayerId: {allPlayers.Count(p => p.GlobalPlayerId.HasValue)}");

        // Build a comprehensive player list:
        // 1. Group players WITH GlobalPlayerId by their GlobalPlayerId
        // 2. Include players WITHOUT GlobalPlayerId individually
        
        var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First, process players with GlobalPlayerId (multi-season players)
        var playerGroups = allPlayers
            .Where(p => p.GlobalPlayerId.HasValue)
            .GroupBy(p => p.GlobalPlayerId!.Value)
            .ToList();

        System.Diagnostics.Debug.WriteLine($"Player groups with GlobalPlayerId: {playerGroups.Count}");

        foreach (var group in playerGroups)
        {
            var firstPlayer = group.First();
            var playerName = firstPlayer.FullName;
            processedNames.Add(playerName);

            ProcessPlayerGroup(group.Key, playerName, group.Select(p => p.Id).ToList(), allFixtures, allSeasons);
        }

        // Second, process players WITHOUT GlobalPlayerId (single-season players not yet linked)
        var singleSeasonPlayers = allPlayers
            .Where(p => !p.GlobalPlayerId.HasValue && !processedNames.Contains(p.FullName))
            .ToList();

        System.Diagnostics.Debug.WriteLine($"Single-season players without GlobalPlayerId: {singleSeasonPlayers.Count}");

        foreach (var player in singleSeasonPlayers)
        {
            if (processedNames.Contains(player.FullName))
                continue;
                
            processedNames.Add(player.FullName);
            ProcessPlayerGroup(player.Id, player.FullName, new List<Guid> { player.Id }, allFixtures, allSeasons);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchEntry.Text))
        {
            var searchText = SearchEntry.Text;
            var filtered = _players.Where(p => p.PlayerName.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
            _players.Clear();
            foreach (var player in filtered)
                _players.Add(player);
        }

        // Sort by total frames played (most active first)
        var sorted = _players.OrderByDescending(p => p.TotalFramesPlayed).ToList();
        _players.Clear();
        foreach (var player in sorted)
            _players.Add(player);

        StatusLabel.Text = $"{_players.Count} player(s) with career stats";
        System.Diagnostics.Debug.WriteLine($"Displaying {_players.Count} players");
    }

    private void ProcessPlayerGroup(Guid globalId, string playerName, List<Guid> playerIds, 
        System.Collections.Generic.List<Models.Fixture> allFixtures, 
        System.Collections.Generic.List<Models.Season> allSeasons)
    {
        var playerIdSet = new HashSet<Guid>(playerIds);
        
        // Get all seasons these player IDs belong to
        var seasonIds = DataStore.Data.Players
            .Where(p => playerIdSet.Contains(p.Id) && p.SeasonId.HasValue)
            .Select(p => p.SeasonId!.Value)
            .Distinct()
            .ToList();
            
        var seasons = allSeasons.Where(s => seasonIds.Contains(s.Id)).OrderByDescending(s => s.StartDate).ToList();

        // Calculate career totals
        int totalFramesPlayed = 0;
        int totalFramesWon = 0;
        int totalEightBalls = 0;

        var seasonBreakdown = new System.Collections.Generic.List<SeasonStats>();

        foreach (var season in seasons)
        {
            int framesPlayed = 0;
            int framesWon = 0;
            int eightBalls = 0;

            // Find all frames for this player in this season
            var seasonFixtures = allFixtures.Where(f => f.SeasonId == season.Id);

            foreach (var fixture in seasonFixtures)
            {
                foreach (var frame in fixture.Frames)
                {
                    // Home player
                    if (frame.HomePlayerId.HasValue && playerIdSet.Contains(frame.HomePlayerId.Value))
                    {
                        framesPlayed++;
                        if (frame.Winner == Models.FrameWinner.Home)
                        {
                            framesWon++;
                            if (frame.EightBall)
                                eightBalls++;
                        }
                    }
                    // Away player
                    else if (frame.AwayPlayerId.HasValue && playerIdSet.Contains(frame.AwayPlayerId.Value))
                    {
                        framesPlayed++;
                        if (frame.Winner == Models.FrameWinner.Away)
                        {
                            framesWon++;
                            if (frame.EightBall)
                                eightBalls++;
                        }
                    }
                }
            }

            if (framesPlayed > 0)
            {
                seasonBreakdown.Add(new SeasonStats
                {
                    SeasonName = season.Name,
                    SeasonYear = season.StartDate.Year,
                    FramesPlayed = framesPlayed,
                    FramesWon = framesWon,
                    FramesLost = framesPlayed - framesWon,
                    WinPercentage = (double)framesWon / framesPlayed * 100,
                    EightBalls = eightBalls
                });

                totalFramesPlayed += framesPlayed;
                totalFramesWon += framesWon;
                totalEightBalls += eightBalls;
            }
        }

        if (totalFramesPlayed > 0)
        {
            _players.Add(new PlayerCareerStats
            {
                GlobalPlayerId = globalId,
                PlayerName = playerName,
                SeasonsPlayed = seasonBreakdown.Count,
                TotalFramesPlayed = totalFramesPlayed,
                TotalFramesWon = totalFramesWon,
                TotalFramesLost = totalFramesPlayed - totalFramesWon,
                CareerWinPercentage = (double)totalFramesWon / totalFramesPlayed * 100,
                TotalEightBalls = totalEightBalls,
                SeasonBreakdown = seasonBreakdown,
                FirstSeasonYear = seasons.Any() ? seasons.Min(s => s.StartDate.Year) : DateTime.Now.Year,
                LastSeasonYear = seasons.Any() ? seasons.Max(s => s.StartDate.Year) : DateTime.Now.Year
            });
        }
    }

    private void OnPlayerSelected(object? sender, SelectionChangedEventArgs e)
    {
        var player = e.CurrentSelection?.FirstOrDefault() as PlayerCareerStats;
        
        if (player == null)
        {
            EmptyStatePanel.IsVisible = true;
            DetailsPanel.IsVisible = false;
            return;
        }

        // Navigate to player profile page
        var profilePage = new PlayerProfilePage();
        profilePage.LoadPlayer(player.GlobalPlayerId, player.PlayerName);
        Navigation.PushAsync(profilePage);

        // Clear selection after navigation
        PlayersList.SelectedItem = null;
    }

    private void OnBurgerMenuClicked(object? sender, EventArgs e)
    {
        if (_isFlyoutOpen)
            CloseFlyout();
        else
            OpenFlyout();
    }

    private void OnCloseFlyoutClicked(object? sender, EventArgs e)
    {
        CloseFlyout();
    }

    private async void OpenFlyout()
    {
        _isFlyoutOpen = true;
        FlyoutOverlay.IsVisible = true;
        FlyoutPanel.IsVisible = true;

        FlyoutPanel.TranslationX = -400;
        await FlyoutPanel.TranslateTo(0, 0, 250, Easing.CubicOut);
    }

    private async void CloseFlyout()
    {
        await FlyoutPanel.TranslateTo(-400, 0, 250, Easing.CubicIn);

        FlyoutOverlay.IsVisible = false;
        FlyoutPanel.IsVisible = false;
        _isFlyoutOpen = false;
    }

    private async System.Threading.Tasks.Task ExportCareerStatsToCsvAsync()
    {
        if (_players.Count == 0)
        {
            await DisplayAlert("No Data", "No career statistics to export.", "OK");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== CAREER STATISTICS - ALL SEASONS ===");
        sb.AppendLine("Player,Career Span,Seasons,Total Frames,Frames Won,Frames Lost,Win %,8-Balls");

        foreach (var player in _players.OrderByDescending(p => p.TotalFramesPlayed))
        {
            sb.AppendLine($"\"{player.PlayerName}\",{player.CareerSpan},{player.SeasonsPlayed},{player.TotalFramesPlayed},{player.TotalFramesWon},{player.TotalFramesLost},{player.CareerWinPercentage:F1},{player.TotalEightBalls}");
            
            // Add season breakdown
            if (player.SeasonBreakdown.Any())
            {
                sb.AppendLine("  Season Breakdown:");
                foreach (var season in player.SeasonBreakdown)
                {
                    sb.AppendLine($"    {season.SeasonName},{season.FramesPlayed} frames,{season.WinLossRecord},{season.WinPercentage:F1}%,{season.EightBalls} 8-balls");
                }
                sb.AppendLine();
            }
        }

        var fileName = $"CareerStats_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var path = System.IO.Path.Combine(Microsoft.Maui.Storage.FileSystem.CacheDirectory, fileName);
        await System.IO.File.WriteAllTextAsync(path, sb.ToString());

        await Microsoft.Maui.ApplicationModel.DataTransfer.Share.RequestAsync(new Microsoft.Maui.ApplicationModel.DataTransfer.ShareFileRequest
        {
            Title = "Export Career Statistics",
            File = new Microsoft.Maui.ApplicationModel.DataTransfer.ShareFile(path, "text/csv")
        });

        StatusLabel.Text = $"Exported {_players.Count} player career stats";
    }
}
