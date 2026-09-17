using Wdpl2.Domain.Players;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.ViewModels;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class CareerStatsPage : ContentPage
{
    private readonly IDataStore _dataStore;
    private readonly ObservableCollection<PlayerCareerStats> _players = new();
    private readonly ObservableCollection<SeasonStats> _seasonBreakdown = new();
    private bool _isFlyoutOpen = false;

    public CareerStatsPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        InitializeComponent();

        PlayersList.ItemsSource = _players;
        SeasonBreakdownList.ItemsSource = _seasonBreakdown;

        // Wire up events
        BurgerMenuBtn.Clicked += OnBurgerMenuClicked;
        CloseFlyoutBtn.Clicked += OnCloseFlyoutClicked;
        OverlayTap.Tapped += (_, __) => CloseFlyout();
        SearchBar.TextChanged += (_, __) => RefreshList();
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
        var data = _dataStore.GetData();
        var allPlayers = data.Players;
        var allFixtures = data.Fixtures;
        var allSeasons = data.Seasons;

        // One row per person, as the league has tied them together. This used
        // to skip anyone whose name had already been seen, so a second player
        // of the same name - or an unlinked row of the same person - had their
        // whole record dropped rather than counted.
        var built = new List<PlayerCareerStats>();

        foreach (var career in PlayerCareers.All(data))
        {
            ProcessPlayerGroup(career.Id, career.Name, career.PlayerIds.ToList(), allFixtures, allSeasons, built);
        }

        // Apply search filter and sort in a single pass on the local list,
        // then atomically swap into the bound collection.
        IEnumerable<PlayerCareerStats> result = built;
        if (!string.IsNullOrWhiteSpace(SearchBar.Text))
        {
            var searchText = SearchBar.Text;
            result = result.Where(p => p.PlayerName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }
        result = result.OrderByDescending(p => p.TotalFramesPlayed);

        _players.Clear();
        foreach (var player in result)
            _players.Add(player);

        StatusLabel.Text = $"{_players.Count} player(s) with career stats";
    }

    private void ProcessPlayerGroup(Guid globalId, string playerName, List<Guid> playerIds, 
        System.Collections.Generic.List<Models.Fixture> allFixtures, 
        System.Collections.Generic.List<Models.Season> allSeasons,
        List<PlayerCareerStats> target)
    {
        var playerIdSet = new HashSet<Guid>(playerIds);
        
        // Get all seasons these player IDs belong to
        var seasonIds = _dataStore.GetData().Players
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
            target.Add(new PlayerCareerStats
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
                FirstSeasonYear = seasons.Count != 0 ? seasons.Min(s => s.StartDate.Year) : DateTime.Now.Year,
                LastSeasonYear = seasons.Count != 0 ? seasons.Max(s => s.StartDate.Year) : DateTime.Now.Year
            });
        }
    }

    private PlayerCareerStats? _selected;

    /// <summary>
    /// Shows the chosen player's career in the panel beside the list.
    /// </summary>
    /// <remarks>
    /// The panel is what this page is for and it was fully built, but selecting
    /// somebody navigated away to the profile page instead and cleared the
    /// selection on the way - so the panel never filled, the highlight never
    /// stayed, and the page looked like it had ignored the click.
    /// <para>
    /// The profile page is still a click away, from the button under the panel,
    /// for the head-to-head and the rest of it.
    /// </para>
    /// </remarks>
    private void OnPlayerSelected(object? sender, SelectionChangedEventArgs e)
    {
        _selected = e.CurrentSelection?.FirstOrDefault() as PlayerCareerStats;

        EmptyStatePanel.IsVisible = _selected is null;
        DetailsPanel.IsVisible = _selected is not null;

        if (_selected is null) return;

        PlayerNameLabel.Text = _selected.PlayerName;
        TotalFramesLabel.Text = _selected.TotalFramesPlayed.ToString();
        WinPercentageLabel.Text = $"{_selected.CareerWinPercentage:F1}%";
        EightBallsLabel.Text = _selected.TotalEightBalls.ToString();
        SeasonsLabel.Text = _selected.SeasonsPlayed.ToString();
        FramesWonLabel.Text = _selected.TotalFramesWon.ToString();
        FramesLostLabel.Text = _selected.TotalFramesLost.ToString();

        // Most recent season first: what somebody is doing now is the thing
        // being looked for, and a career here can be thirteen seasons long.
        SeasonBreakdownList.ItemsSource = _selected.SeasonBreakdown
            .OrderByDescending(s => s.SeasonYear)
            .ToList();
    }

    private void OnOpenProfile(object? sender, EventArgs e)
    {
        if (_selected is null) return;

        var profilePage = Application.Current?.Handler?.MauiContext?.Services.GetService<PlayerProfilePage>();
        if (profilePage is null)
        {
            StatusLabel.Text = "Could not open the full profile.";
            return;
        }

        profilePage.LoadPlayer(_selected.GlobalPlayerId, _selected.PlayerName);
        _ = Navigation.PushAsync(profilePage);
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
            if (player.SeasonBreakdown.Count != 0)
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
