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
        System.Diagnostics.Debug.WriteLine($"Players with GlobalPlayerId: {allPlayers.Count(p => p.GlobalPlayerId.HasValue)}");

        // Group players by GlobalPlayerId
        var playerGroups = allPlayers
            .Where(p => p.GlobalPlayerId.HasValue)
            .GroupBy(p => p.GlobalPlayerId!.Value)
            .ToList();

        System.Diagnostics.Debug.WriteLine($"Unique global players: {playerGroups.Count}");

        foreach (var group in playerGroups)
        {
            var firstPlayer = group.First();
            var playerName = firstPlayer.FullName;

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchEntry.Text))
            {
                if (!playerName.Contains(SearchEntry.Text, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Get all seasons this player participated in
            var seasonIds = group.Select(p => p.SeasonId).Distinct().ToList();
            var seasons = allSeasons.Where(s => seasonIds.Contains(s.Id)).OrderByDescending(s => s.StartDate).ToList();

            // Calculate career totals
            int totalFramesPlayed = 0;
            int totalFramesWon = 0;
            int totalEightBalls = 0;

            var seasonBreakdown = new System.Collections.Generic.List<SeasonStats>();

            foreach (var season in seasons)
            {
                var playerInSeason = group.FirstOrDefault(p => p.SeasonId == season.Id);
                if (playerInSeason == null) continue;

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
                        if (frame.HomePlayerId == playerInSeason.Id)
                        {
                            framesPlayed++;
                            if (frame.Winner == Models.FrameWinner.Home)
                                framesWon++;
                            if (frame.EightBall)
                                eightBalls++;
                        }
                        // Away player
                        else if (frame.AwayPlayerId == playerInSeason.Id)
                        {
                            framesPlayed++;
                            if (frame.Winner == Models.FrameWinner.Away)
                                framesWon++;
                            if (frame.EightBall)
                                eightBalls++;
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
                    GlobalPlayerId = group.Key,
                    PlayerName = playerName,
                    SeasonsPlayed = seasonBreakdown.Count,
                    TotalFramesPlayed = totalFramesPlayed,
                    TotalFramesWon = totalFramesWon,
                    TotalFramesLost = totalFramesPlayed - totalFramesWon,
                    CareerWinPercentage = (double)totalFramesWon / totalFramesPlayed * 100,
                    TotalEightBalls = totalEightBalls,
                    SeasonBreakdown = seasonBreakdown,
                    FirstSeasonYear = seasons.Min(s => s.StartDate.Year),
                    LastSeasonYear = seasons.Max(s => s.StartDate.Year)
                });
            }
        }

        // Sort by total frames played (most active first)
        var sorted = _players.OrderByDescending(p => p.TotalFramesPlayed).ToList();
        _players.Clear();
        foreach (var player in sorted)
            _players.Add(player);

        StatusLabel.Text = $"{_players.Count} player(s) with career stats";
        System.Diagnostics.Debug.WriteLine($"Displaying {_players.Count} players");
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

        // Show player details
        EmptyStatePanel.IsVisible = false;
        DetailsPanel.IsVisible = true;

        PlayerNameLabel.Text = player.PlayerName;
        TotalFramesLabel.Text = player.TotalFramesPlayed.ToString();
        WinPercentageLabel.Text = $"{player.CareerWinPercentage:F1}%";
        EightBallsLabel.Text = player.TotalEightBalls.ToString();
        SeasonsLabel.Text = player.SeasonsPlayed.ToString();
        FramesWonLabel.Text = player.TotalFramesWon.ToString();
        FramesLostLabel.Text = player.TotalFramesLost.ToString();

        // Load season breakdown
        _seasonBreakdown.Clear();
        foreach (var season in player.SeasonBreakdown.OrderByDescending(s => s.SeasonYear))
            _seasonBreakdown.Add(season);

        StatusLabel.Text = $"{player.PlayerName} - {player.TotalFramesPlayed} frames across {player.SeasonsPlayed} seasons ({player.CareerSpan})";
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
}
