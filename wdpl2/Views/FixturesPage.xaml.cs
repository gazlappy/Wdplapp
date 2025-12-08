using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2;
using Wdpl2.Helpers;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class FixturesPage : ContentPage
{
    // Left list projection
    public sealed class FixtureListItem
    {
        public Guid Id { get; init; }
        public DateTime Date { get; init; }
        public string Title { get; init; } = "";
        public string Subtitle { get; init; } = "";
        public bool HasReminder { get; init; }
    }

    // Player list item for the side panels
    public sealed class PlayerListItem
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public int FrameCount { get; set; } // How many frames they're assigned to
    }

    // Scorecard frame row data
    private sealed class FrameRowData
    {
        public int FrameNumber { get; set; }
        public Guid? HomePlayerId { get; set; }
        public string HomePlayerName { get; set; } = "";
        public Guid? AwayPlayerId { get; set; }
        public string AwayPlayerName { get; set; } = "";
        public FrameWinner Winner { get; set; } = FrameWinner.None;
        public bool EightBall { get; set; }
        
        // UI Elements
        public Border? RowBorder { get; set; }
        public Label? HomePlayerLabel { get; set; }
        public Label? AwayPlayerLabel { get; set; }
        public Button? HomeScoreBtn { get; set; }
        public Button? AwayScoreBtn { get; set; }
        public CheckBox? EightBallCheck { get; set; }
    }

    private readonly ObservableCollection<FixtureListItem> _items = new();
    private readonly ObservableCollection<PlayerListItem> _homePlayers = new();
    private readonly ObservableCollection<PlayerListItem> _awayPlayers = new();
    private readonly List<FrameRowData> _frameRows = new();

    private Fixture? _selectedFixture;
    private int _currentFrameIndex = 0; // Which frame is being edited (0-based)
    private bool _selectingHomePlayer = true; // Are we selecting home or away player?
    private bool _isFlyoutOpen = false;
    
    // Services for notification management
    private MatchReminderService? _reminderService;
    private INotificationService? _notificationService;
    private bool _servicesInitialized = false;

    public FixturesPage()
    {
        System.Diagnostics.Debug.WriteLine("=== FIXTURES PAGE: Constructor START ===");
        
        InitializeComponent();

        // Wire up burger menu and flyout
        BurgerMenuBtn.Clicked += OnBurgerMenuClicked;
        CloseFlyoutBtn.Clicked += OnCloseFlyoutClicked;
        OverlayTap.Tapped += (_, __) => CloseFlyout();

        // Initial control defaults
        if (FromDate != null)
        {
            FromDate.Date = DateTime.Today.AddDays(-7);
        }

        // Bind lists
        FixturesList.ItemsSource = _items;
        HomePlayersList.ItemsSource = _homePlayers;
        AwayPlayersList.ItemsSource = _awayPlayers;

        // Wire events
        FixturesList.SelectionChanged += OnSelectFixture;
        SearchEntry.TextChanged += (_, __) => RefreshList();
        
        if (FromDate != null)
            FromDate.DateSelected += (_, __) => RefreshList();
        
        ActiveSeasonOnly.Toggled += (_, __) => RefreshList();
        DivisionPicker.SelectedIndexChanged += (_, __) => RefreshList();

        SaveBtn.Clicked += async (_, __) => await SaveFromUIAsync();
        ClearBtn.Clicked += (_, __) => OnClearFrames();
        DiagnosticsBtn.Clicked += async (_, __) => await OnDiagnosticsAsync();
        GenerateFixturesBtn.Clicked += async (_, __) => await OnGenerateFixturesAsync();
        DeleteAllBtn.Clicked += async (_, __) => await OnDeleteAllFixturesAsync();
        DeleteSeasonBtn.Clicked += async (_, __) => await OnDeleteActiveSeasonFixturesAsync();
        
        if (ManageNotificationsBtn != null)
        {
            ManageNotificationsBtn.Clicked += async (_, __) => await OnManageNotificationsAsync();
        }

        // Subscribe to global season changes
        SeasonService.SeasonChanged += OnGlobalSeasonChanged;

        System.Diagnostics.Debug.WriteLine("=== FIXTURES PAGE: Constructor END, calling RefreshList ===");
        RefreshList();
    }
    
    ~FixturesPage()
    {
        SeasonService.SeasonChanged -= OnGlobalSeasonChanged;
    }
    
    private void OnGlobalSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!e.NewSeasonId.HasValue)
                {
                    ActiveSeasonOnly.IsToggled = false;
                }
                
                _items.Clear();
                _selectedFixture = null;
                ClearScorecard();
                
                RefreshList();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FixturesPage Season change error: {ex}");
        }
    }

    // ========== PLAYER SELECTION FROM SIDE LISTS ==========

    private void OnHomePlayerSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_selectedFixture == null || _currentFrameIndex < 0 || _currentFrameIndex >= _frameRows.Count)
            return;

        var selected = e.CurrentSelection.FirstOrDefault() as PlayerListItem;
        if (selected == null) return;

        var frameRow = _frameRows[_currentFrameIndex];
        frameRow.HomePlayerId = selected.Id;
        frameRow.HomePlayerName = selected.Name;
        
        if (frameRow.HomePlayerLabel != null)
        {
            frameRow.HomePlayerLabel.Text = selected.Name;
            frameRow.HomePlayerLabel.TextColor = Colors.Black;
        }

        // Update player frame counts
        UpdatePlayerFrameCounts();

        // Clear selection and move to away player selection
        HomePlayersList.SelectedItem = null;
        
        // Auto-advance: Now select away player for same frame
        _selectingHomePlayer = false;
        UpdateCurrentFrameIndicator();
    }

    private void OnAwayPlayerSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_selectedFixture == null || _currentFrameIndex < 0 || _currentFrameIndex >= _frameRows.Count)
            return;

        var selected = e.CurrentSelection.FirstOrDefault() as PlayerListItem;
        if (selected == null) return;

        var frameRow = _frameRows[_currentFrameIndex];
        frameRow.AwayPlayerId = selected.Id;
        frameRow.AwayPlayerName = selected.Name;
        
        if (frameRow.AwayPlayerLabel != null)
        {
            frameRow.AwayPlayerLabel.Text = selected.Name;
            frameRow.AwayPlayerLabel.TextColor = Colors.Black;
        }

        // Update player frame counts
        UpdatePlayerFrameCounts();

        // Clear selection and move to next frame
        AwayPlayersList.SelectedItem = null;
        
        // Auto-advance to next frame (home player)
        if (_currentFrameIndex < _frameRows.Count - 1)
        {
            _currentFrameIndex++;
            _selectingHomePlayer = true;
            UpdateCurrentFrameIndicator();
            HighlightCurrentFrame();
        }
        else
        {
            // All frames filled - hide indicator
            CurrentFrameIndicator.IsVisible = false;
        }
    }

    private void UpdatePlayerFrameCounts()
    {
        // Count how many frames each player is assigned to
        var homeCounts = new Dictionary<Guid, int>();
        var awayCounts = new Dictionary<Guid, int>();

        foreach (var frame in _frameRows)
        {
            if (frame.HomePlayerId.HasValue)
            {
                homeCounts.TryGetValue(frame.HomePlayerId.Value, out int count);
                homeCounts[frame.HomePlayerId.Value] = count + 1;
            }
            if (frame.AwayPlayerId.HasValue)
            {
                awayCounts.TryGetValue(frame.AwayPlayerId.Value, out int count);
                awayCounts[frame.AwayPlayerId.Value] = count + 1;
            }
        }

        foreach (var player in _homePlayers)
        {
            player.FrameCount = homeCounts.GetValueOrDefault(player.Id, 0);
        }
        foreach (var player in _awayPlayers)
        {
            player.FrameCount = awayCounts.GetValueOrDefault(player.Id, 0);
        }

        // Refresh the lists to show updated counts
        var homeTemp = _homePlayers.ToList();
        _homePlayers.Clear();
        foreach (var p in homeTemp) _homePlayers.Add(p);

        var awayTemp = _awayPlayers.ToList();
        _awayPlayers.Clear();
        foreach (var p in awayTemp) _awayPlayers.Add(p);
    }

    private void UpdateCurrentFrameIndicator()
    {
        if (_selectedFixture == null)
        {
            CurrentFrameIndicator.IsVisible = false;
            return;
        }

        CurrentFrameIndicator.IsVisible = true;
        var side = _selectingHomePlayer ? "HOME" : "AWAY";
        CurrentFrameLabel.Text = $"Frame {_currentFrameIndex + 1} - Select {side} player";
    }

    private void HighlightCurrentFrame()
    {
        for (int i = 0; i < _frameRows.Count; i++)
        {
            var row = _frameRows[i];
            if (row.RowBorder != null)
            {
                row.RowBorder.BackgroundColor = (i == _currentFrameIndex)
                    ? Color.FromArgb("#FFF9C4") // Highlighted yellow
                    : (i % 2 == 0 ? Colors.White : Color.FromArgb("#F5F5F5"));
            }
        }
    }

    // ========== BURGER MENU ==========

    private void OnBurgerMenuClicked(object? sender, EventArgs e)
    {
        if (_isFlyoutOpen) CloseFlyout();
        else OpenFlyout();
    }

    private void OnCloseFlyoutClicked(object? sender, EventArgs e) => CloseFlyout();

    private async void OpenFlyout()
    {
        _isFlyoutOpen = true;
        FlyoutOverlay.IsVisible = true;
        FlyoutPanel.IsVisible = true;
        FlyoutPanel.TranslationX = -400;
        await FlyoutPanel.TranslateTo(0, 0, 250, Easing.CubicOut);
        await UpdatePendingNotificationCountAsync();
    }

    private async void CloseFlyout()
    {
        await FlyoutPanel.TranslateTo(-400, 0, 250, Easing.CubicIn);
        FlyoutOverlay.IsVisible = false;
        FlyoutPanel.IsVisible = false;
        _isFlyoutOpen = false;
    }
    
    private async System.Threading.Tasks.Task UpdatePendingNotificationCountAsync()
    {
        if (PendingNotificationsLabel == null) return;
        if (_notificationService == null)
        {
            PendingNotificationsLabel.Text = "Notifications not available";
            return;
        }

        try
        {
            var count = await _notificationService.GetPendingNotificationCountAsync();
            PendingNotificationsLabel.Text = $"{Emojis.Bell} {count} pending reminder(s)";
        }
        catch
        {
            PendingNotificationsLabel.Text = "Could not check pending reminders";
        }
    }

    // ========== DIAGNOSTICS ==========
    
    private async System.Threading.Tasks.Task OnDiagnosticsAsync()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ACTIVE SEASON DIAGNOSTICS\n");
        
        var activeSeasonId = DataStore.Data.ActiveSeasonId;
        sb.AppendLine($"ActiveSeasonId Property: {(activeSeasonId.HasValue ? activeSeasonId.Value.ToString() : "NOT SET")}");
        sb.AppendLine();
        
        var seasons = DataStore.Data.Seasons ?? new List<Season>();
        sb.AppendLine($"Total Seasons: {seasons.Count}");
        sb.AppendLine();
        
        if (seasons.Count == 0)
        {
            sb.AppendLine("NO SEASONS FOUND!");
        }
        else
        {
            sb.AppendLine("Seasons:");
            foreach (var season in seasons.OrderByDescending(s => s.IsActive))
            {
                var activeMarker = season.IsActive ? "ACTIVE" : "   ";
                sb.AppendLine($"{activeMarker} {season.Name}");
                sb.AppendLine($"     ID: {season.Id}");
            }
        }
        
        if (_notificationService != null)
        {
            sb.AppendLine();
            sb.AppendLine("NOTIFICATION STATUS:");
            try
            {
                var enabled = await _notificationService.AreNotificationsEnabledAsync();
                var pending = await _notificationService.GetPendingNotificationCountAsync();
                sb.AppendLine($"  Enabled: {enabled}");
                sb.AppendLine($"  Pending: {pending}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Error: {ex.Message}");
            }
        }
        
        await DisplayAlert("Diagnostics", sb.ToString(), "OK");
    }

    // ========== LEFT LIST DATA ==========

    private void RefreshList()
    {
        _items.Clear();

        var data = DataStore.Data;
        if (data == null) return;
        
        // Load divisions into picker if empty
        if (DivisionPicker.ItemsSource == null)
        {
            var divisions = data.Divisions
                .Where(d => !ActiveSeasonOnly.IsToggled || d.SeasonId == data.ActiveSeasonId)
                .OrderBy(d => d.Name)
                .ToList();
            
            DivisionPicker.ItemsSource = divisions;
        }
        
        var teamById = data.Teams.ToDictionary(t => t.Id, t => t);
        var venueById = data.Venues.ToDictionary(v => v.Id, v => v);

        IEnumerable<Fixture> src = data.Fixtures;

        if (ActiveSeasonOnly.IsToggled && data.ActiveSeasonId != null)
        {
            src = src.Where(f => f.SeasonId == data.ActiveSeasonId);
        }
        else if (ActiveSeasonOnly.IsToggled && data.ActiveSeasonId == null)
        {
            return;
        }
        
        var selectedDivision = DivisionPicker.SelectedItem as Division;
        if (selectedDivision != null)
        {
            src = src.Where(f => f.DivisionId == selectedDivision.Id);
        }

        if (FromDate != null)
        {
            var from = FromDate.Date.Date;
            src = src.Where(f => f.Date.Date >= from);
        }

        var q = (SearchEntry.Text ?? "").Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(q))
        {
            src = src.Where(f =>
            {
                var home = teamById.TryGetValue(f.HomeTeamId, out var ht) ? (ht.Name ?? "") : "";
                var away = teamById.TryGetValue(f.AwayTeamId, out var at) ? (at.Name ?? "") : "";
                var venue = f.VenueId.HasValue && venueById.TryGetValue(f.VenueId.Value, out var v) ? (v.Name ?? "") : "";
                return home.ToLower().Contains(q) || away.ToLower().Contains(q) || venue.ToLower().Contains(q);
            });
        }

        var fixturesList = src.OrderBy(f => f.Date).ToList();

        foreach (var f in fixturesList)
        {
            var home = teamById.TryGetValue(f.HomeTeamId, out var ht) ? (ht.Name ?? "Home") : "Home";
            var away = teamById.TryGetValue(f.AwayTeamId, out var at) ? (at.Name ?? "Away") : "Away";

            string subtitle = "";
            if (f.VenueId.HasValue && venueById.TryGetValue(f.VenueId.Value, out var v))
                subtitle = v.Name;

            _items.Add(new FixtureListItem
            {
                Id = f.Id,
                Date = f.Date,
                Title = $"{home} vs {away}",
                Subtitle = subtitle,
                HasReminder = f.Date > DateTime.Now
            });
        }
    }

    private void OnSelectFixture(object? sender, SelectionChangedEventArgs e)
    {
        var li = e.CurrentSelection.FirstOrDefault() as FixtureListItem;
        if (li == null)
        {
            _selectedFixture = null;
            ClearScorecard();
            return;
        }

        _selectedFixture = DataStore.Data.Fixtures.First(x => x.Id == li.Id);
        BuildScorecard();
        UpdateHeader();
        UpdateReminderStatus();
    }

    // ========== SCORECARD BUILDING ==========

    private void ClearScorecard()
    {
        ScorecardHost.Children.Clear();
        _frameRows.Clear();
        _homePlayers.Clear();
        _awayPlayers.Clear();
        _currentFrameIndex = 0;
        _selectingHomePlayer = true;
        CurrentFrameIndicator.IsVisible = false;
        
        HomeTeamHeader.Text = "Home Team";
        AwayTeamHeader.Text = "Away";
        HomeTeamListHeader.Text = "HOME PLAYERS";
        AwayTeamListHeader.Text = "AWAY PLAYERS";
        DivisionLabel.Text = "Div.";
        DateLabel.Text = "Date";
        ScoreLbl.Text = "";
        HeaderLbl.Text = "Select a fixture";
    }

    private void BuildScorecard()
    {
        if (_selectedFixture == null) return;

        ScorecardHost.Children.Clear();
        _frameRows.Clear();
        _currentFrameIndex = 0;
        _selectingHomePlayer = true;

        // Get team info
        var data = DataStore.Data;
        var homeTeam = data.Teams.FirstOrDefault(t => t.Id == _selectedFixture.HomeTeamId);
        var awayTeam = data.Teams.FirstOrDefault(t => t.Id == _selectedFixture.AwayTeamId);
        var division = data.Divisions.FirstOrDefault(d => d.Id == _selectedFixture.DivisionId);

        // Update headers
        HomeTeamHeader.Text = homeTeam?.Name ?? "Home Team";
        AwayTeamHeader.Text = awayTeam?.Name ?? "Away";
        HomeTeamListHeader.Text = homeTeam?.Name?.ToUpper() ?? "HOME PLAYERS";
        AwayTeamListHeader.Text = awayTeam?.Name?.ToUpper() ?? "AWAY PLAYERS";
        DivisionLabel.Text = $"Div: {division?.Name ?? "?"}";
        DateLabel.Text = _selectedFixture.Date.ToString("ddd dd MMM yyyy");

        // Load player lists
        LoadPlayerLists();

        // Determine frame count
        int frameCount = 10;
        var season = data.Seasons.FirstOrDefault(s => s.Id == _selectedFixture.SeasonId);
        if (season != null && season.FramesPerMatch > 0) 
            frameCount = season.FramesPerMatch;

        // Ensure fixture has enough frames
        while (_selectedFixture.Frames.Count < frameCount)
            _selectedFixture.Frames.Add(new FrameResult { Number = _selectedFixture.Frames.Count + 1 });
        if (_selectedFixture.Frames.Count > frameCount)
            _selectedFixture.Frames = _selectedFixture.Frames.Take(frameCount).ToList();

        // Build frame rows
        for (int i = 0; i < frameCount; i++)
        {
            var fr = _selectedFixture.Frames[i];
            var frameRow = CreateFrameRow(i, fr);
            _frameRows.Add(frameRow);
            if (frameRow.RowBorder != null)
                ScorecardHost.Children.Add(frameRow.RowBorder);
        }

        // Add divider after frame 10 (if there are 15 frames like in the WDPL card)
        if (frameCount > 10)
        {
            // Insert black divider after frame 10
            var dividerIndex = ScorecardHost.Children.Count >= 10 ? 10 : ScorecardHost.Children.Count;
            var divider = new Border
            {
                BackgroundColor = Color.FromArgb("#1A1A1A"),
                HeightRequest = 24,
                Padding = new Thickness(8, 4)
            };
            divider.Content = new Label
            {
                Text = "Before start of game 11 all names, games 11 - 15 to be completed, home and away.",
                FontSize = 10,
                TextColor = Color.FromArgb("#FFD700"),
                HorizontalTextAlignment = TextAlignment.Center
            };
            // Insert at position 10 (after 10 frames)
            if (dividerIndex <= ScorecardHost.Children.Count)
            {
                ScorecardHost.Children.Insert(dividerIndex, divider);
            }
        }

        // Show frame indicator
        UpdateCurrentFrameIndicator();
        HighlightCurrentFrame();
    }

    private void LoadPlayerLists()
    {
        _homePlayers.Clear();
        _awayPlayers.Clear();

        if (_selectedFixture == null) return;

        var data = DataStore.Data;

        // Get home team players
        var homePlayers = data.Players
            .Where(p => p.TeamId == _selectedFixture.HomeTeamId)
            .OrderBy(p => p.LastName ?? "")
            .ThenBy(p => p.FirstName ?? "")
            .ToList();

        foreach (var p in homePlayers)
        {
            _homePlayers.Add(new PlayerListItem
            {
                Id = p.Id,
                Name = p.FullName ?? $"{p.FirstName} {p.LastName}".Trim(),
                FrameCount = 0
            });
        }

        // Get away team players
        var awayPlayers = data.Players
            .Where(p => p.TeamId == _selectedFixture.AwayTeamId)
            .OrderBy(p => p.LastName ?? "")
            .ThenBy(p => p.FirstName ?? "")
            .ToList();

        foreach (var p in awayPlayers)
        {
            _awayPlayers.Add(new PlayerListItem
            {
                Id = p.Id,
                Name = p.FullName ?? $"{p.FirstName} {p.LastName}".Trim(),
                FrameCount = 0
            });
        }

        // Update counts based on existing frame data
        UpdatePlayerFrameCounts();
    }

    private FrameRowData CreateFrameRow(int index, FrameResult fr)
    {
        var data = DataStore.Data;
        
        // Get existing player names
        string homePlayerName = "";
        string awayPlayerName = "";
        
        if (fr.HomePlayerId.HasValue)
        {
            var player = data.Players.FirstOrDefault(p => p.Id == fr.HomePlayerId.Value);
            homePlayerName = player?.FullName ?? "";
        }
        if (fr.AwayPlayerId.HasValue)
        {
            var player = data.Players.FirstOrDefault(p => p.Id == fr.AwayPlayerId.Value);
            awayPlayerName = player?.FullName ?? "";
        }

        var frameRow = new FrameRowData
        {
            FrameNumber = index + 1,
            HomePlayerId = fr.HomePlayerId,
            HomePlayerName = homePlayerName,
            AwayPlayerId = fr.AwayPlayerId,
            AwayPlayerName = awayPlayerName,
            Winner = fr.Winner,
            EightBall = fr.EightBall
        };

        // Create the row UI - columns match header: #(30), HomePlayer(*), HomeScore(36), AwayScore(36), AwayPlayer(*), 8Ball(36)
        var rowBorder = new Border
        {
            BackgroundColor = index % 2 == 0 ? Colors.White : Color.FromArgb("#F5F5F5"),
            Padding = new Thickness(0),
            StrokeThickness = 0
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(30) },  // Frame #
                new ColumnDefinition { Width = GridLength.Star },     // Home player
                new ColumnDefinition { Width = new GridLength(36) },  // Home score
                new ColumnDefinition { Width = new GridLength(36) },  // Away score
                new ColumnDefinition { Width = GridLength.Star },     // Away player
                new ColumnDefinition { Width = new GridLength(36) }   // 8-ball
            },
            Padding = new Thickness(2, 4),
            ColumnSpacing = 2
        };

        // Frame number
        var frameNumLabel = new Label
        {
            Text = (index + 1).ToString(),
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(frameNumLabel, 0);
        grid.Children.Add(frameNumLabel);

        // Home player name (tappable to edit)
        var homePlayerLabel = new Label
        {
            Text = string.IsNullOrEmpty(homePlayerName) ? "Tap to select..." : homePlayerName,
            TextColor = string.IsNullOrEmpty(homePlayerName) ? Colors.Gray : Colors.Black,
            FontSize = 11,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        var homePlayerBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 3 },
            Stroke = Color.FromArgb("#90CAF9"),
            StrokeThickness = 1,
            Padding = new Thickness(3, 2),
            Content = homePlayerLabel
        };
        var homeTap = new TapGestureRecognizer();
        homeTap.Tapped += (s, e) =>
        {
            _currentFrameIndex = index;
            _selectingHomePlayer = true;
            UpdateCurrentFrameIndicator();
            HighlightCurrentFrame();
        };
        homePlayerBorder.GestureRecognizers.Add(homeTap);
        Grid.SetColumn(homePlayerBorder, 1);
        grid.Children.Add(homePlayerBorder);

        // Home score button
        var homeScoreBtn = new Button
        {
            Text = fr.Winner == FrameWinner.Home ? "1" : "0",
            BackgroundColor = fr.Winner == FrameWinner.Home ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0"),
            TextColor = fr.Winner == FrameWinner.Home ? Colors.White : Color.FromArgb("#757575"),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 3,
            Padding = new Thickness(0),
            WidthRequest = 32,
            HeightRequest = 28
        };
        homeScoreBtn.Clicked += (s, e) =>
        {
            var btn = s as Button;
            var awayBtn = frameRow.AwayScoreBtn;
            if (btn == null || awayBtn == null) return;

            if (btn.Text == "0")
            {
                btn.Text = "1";
                btn.BackgroundColor = Color.FromArgb("#4CAF50");
                btn.TextColor = Colors.White;
                awayBtn.Text = "0";
                awayBtn.BackgroundColor = Color.FromArgb("#E0E0E0");
                awayBtn.TextColor = Color.FromArgb("#757575");
                frameRow.Winner = FrameWinner.Home;
            }
            else
            {
                btn.Text = "0";
                btn.BackgroundColor = Color.FromArgb("#E0E0E0");
                btn.TextColor = Color.FromArgb("#757575");
                frameRow.Winner = FrameWinner.None;
            }
            UpdateScoreDisplay();
        };
        Grid.SetColumn(homeScoreBtn, 2);
        grid.Children.Add(homeScoreBtn);

        // Away score button
        var awayScoreBtn = new Button
        {
            Text = fr.Winner == FrameWinner.Away ? "1" : "0",
            BackgroundColor = fr.Winner == FrameWinner.Away ? Color.FromArgb("#F44336") : Color.FromArgb("#E0E0E0"),
            TextColor = fr.Winner == FrameWinner.Away ? Colors.White : Color.FromArgb("#757575"),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 3,
            Padding = new Thickness(0),
            WidthRequest = 32,
            HeightRequest = 28
        };
        awayScoreBtn.Clicked += (s, e) =>
        {
            var btn = s as Button;
            var homeBtn = frameRow.HomeScoreBtn;
            if (btn == null || homeBtn == null) return;

            if (btn.Text == "0")
            {
                btn.Text = "1";
                btn.BackgroundColor = Color.FromArgb("#F44336");
                btn.TextColor = Colors.White;
                homeBtn.Text = "0";
                homeBtn.BackgroundColor = Color.FromArgb("#E0E0E0");
                homeBtn.TextColor = Color.FromArgb("#757575");
                frameRow.Winner = FrameWinner.Away;
            }
            else
            {
                btn.Text = "0";
                btn.BackgroundColor = Color.FromArgb("#E0E0E0");
                btn.TextColor = Color.FromArgb("#757575");
                frameRow.Winner = FrameWinner.None;
            }
            UpdateScoreDisplay();
        };
        Grid.SetColumn(awayScoreBtn, 3);
        grid.Children.Add(awayScoreBtn);

        // Away player name (tappable to edit)
        var awayPlayerLabel = new Label
        {
            Text = string.IsNullOrEmpty(awayPlayerName) ? "Tap to select..." : awayPlayerName,
            TextColor = string.IsNullOrEmpty(awayPlayerName) ? Colors.Gray : Colors.Black,
            FontSize = 11,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        var awayPlayerBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 3 },
            Stroke = Color.FromArgb("#EF9A9A"),
            StrokeThickness = 1,
            Padding = new Thickness(3, 2),
            Content = awayPlayerLabel
        };
        var awayTap = new TapGestureRecognizer();
        awayTap.Tapped += (s, e) =>
        {
            _currentFrameIndex = index;
            _selectingHomePlayer = false;
            UpdateCurrentFrameIndicator();
            HighlightCurrentFrame();
        };
        awayPlayerBorder.GestureRecognizers.Add(awayTap);
        Grid.SetColumn(awayPlayerBorder, 4);
        grid.Children.Add(awayPlayerBorder);

        // 8-ball checkbox
        var eightBallCheck = new CheckBox
        {
            IsChecked = fr.EightBall,
            Color = Color.FromArgb("#FF9800"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Scale = 0.9
        };
        eightBallCheck.CheckedChanged += (s, e) =>
        {
            frameRow.EightBall = e.Value;
        };
        Grid.SetColumn(eightBallCheck, 5);
        grid.Children.Add(eightBallCheck);

        rowBorder.Content = grid;

        // Store references
        frameRow.RowBorder = rowBorder;
        frameRow.HomePlayerLabel = homePlayerLabel;
        frameRow.AwayPlayerLabel = awayPlayerLabel;
        frameRow.HomeScoreBtn = homeScoreBtn;
        frameRow.AwayScoreBtn = awayScoreBtn;
        frameRow.EightBallCheck = eightBallCheck;

        return frameRow;
    }

    private void UpdateScoreDisplay()
    {
        int homeScore = _frameRows.Count(f => f.Winner == FrameWinner.Home);
        int awayScore = _frameRows.Count(f => f.Winner == FrameWinner.Away);
        ScoreLbl.Text = $"{homeScore} - {awayScore}";
    }

    // ========== SAVE & CLEAR ==========

    private async System.Threading.Tasks.Task SaveFromUIAsync()
    {
        if (_selectedFixture == null) return;

        // Update fixture frames from UI data
        for (int i = 0; i < _frameRows.Count && i < _selectedFixture.Frames.Count; i++)
        {
            var row = _frameRows[i];
            var fr = _selectedFixture.Frames[i];

            fr.HomePlayerId = row.HomePlayerId;
            fr.AwayPlayerId = row.AwayPlayerId;
            fr.Winner = row.Winner;
            fr.EightBall = row.EightBall;
        }

        DataStore.Save();
        UpdateHeader();
        
        await ScheduleFixtureReminderAsync(_selectedFixture);
        UpdateReminderStatus();
        RefreshList();
        
        await DisplayAlert($"{Emojis.Success} Saved", 
            "Fixture results saved successfully!", "OK");
    }

    private void OnClearFrames()
    {
        if (_selectedFixture == null) return;

        foreach (var row in _frameRows)
        {
            row.HomePlayerId = null;
            row.HomePlayerName = "";
            row.AwayPlayerId = null;
            row.AwayPlayerName = "";
            row.Winner = FrameWinner.None;
            row.EightBall = false;

            if (row.HomePlayerLabel != null)
            {
                row.HomePlayerLabel.Text = "Tap to select...";
                row.HomePlayerLabel.TextColor = Colors.Gray;
            }
            if (row.AwayPlayerLabel != null)
            {
                row.AwayPlayerLabel.Text = "Tap to select...";
                row.AwayPlayerLabel.TextColor = Colors.Gray;
            }
            if (row.HomeScoreBtn != null)
            {
                row.HomeScoreBtn.Text = "0";
                row.HomeScoreBtn.BackgroundColor = Color.FromArgb("#E0E0E0");
                row.HomeScoreBtn.TextColor = Color.FromArgb("#757575");
            }
            if (row.AwayScoreBtn != null)
            {
                row.AwayScoreBtn.Text = "0";
                row.AwayScoreBtn.BackgroundColor = Color.FromArgb("#E0E0E0");
                row.AwayScoreBtn.TextColor = Color.FromArgb("#757575");
            }
            if (row.EightBallCheck != null)
            {
                row.EightBallCheck.IsChecked = false;
            }
        }

        _currentFrameIndex = 0;
        _selectingHomePlayer = true;
        UpdateCurrentFrameIndicator();
        HighlightCurrentFrame();
        UpdatePlayerFrameCounts();
        UpdateScoreDisplay();
    }

    private void UpdateReminderStatus()
    {
        if (ReminderStatusLabel == null) return;
        if (_selectedFixture == null || _notificationService == null)
        {
            ReminderStatusLabel.Text = "";
            return;
        }

        if (_selectedFixture.Date <= DateTime.Now)
        {
            ReminderStatusLabel.Text = $"{Emojis.Info} Match has passed";
            return;
        }

        var settings = DataStore.Data.Settings;
        if (!settings.MatchRemindersEnabled)
        {
            ReminderStatusLabel.Text = $"{Emojis.Warning} Reminders disabled";
            return;
        }

        var hoursBeforeMatch = settings.ReminderHoursBefore;
        var reminderTime = _selectedFixture.Date.AddHours(-hoursBeforeMatch);
        ReminderStatusLabel.Text = $"{Emojis.Bell} Reminder: {reminderTime:ddd HH:mm}";
    }

    private void UpdateHeader()
    {
        if (_selectedFixture == null)
        {
            HeaderLbl.Text = "Select a fixture";
            ScoreLbl.Text = "";
            return;
        }

        var tById = DataStore.Data.Teams.ToDictionary(t => t.Id, t => t);
        var home = tById.TryGetValue(_selectedFixture.HomeTeamId, out var ht) ? (ht.Name ?? "Home") : "Home";
        var away = tById.TryGetValue(_selectedFixture.AwayTeamId, out var at) ? (at.Name ?? "Away") : "Away";

        HeaderLbl.Text = $"{home} vs {away}";
        UpdateScoreDisplay();
    }

    // ========== NOTIFICATION HELPERS ==========

    private async System.Threading.Tasks.Task ScheduleFixtureReminderAsync(Fixture fixture)
    {
        if (_reminderService == null || fixture.Date <= DateTime.Now) return;

        var settings = DataStore.Data.Settings;
        if (!settings.MatchRemindersEnabled) return;

        try
        {
            var teamById = DataStore.Data.Teams.ToDictionary(t => t.Id, t => t);
            var homeTeam = teamById.TryGetValue(fixture.HomeTeamId, out var ht) ? ht.Name : "Home";
            var awayTeam = teamById.TryGetValue(fixture.AwayTeamId, out var at) ? at.Name : "Away";

            await _reminderService.ScheduleMatchReminderAsync(
                fixture.Id,
                fixture.Date,
                homeTeam ?? "Home",
                awayTeam ?? "Away",
                hoursBeforeMatch: settings.ReminderHoursBefore
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to schedule reminder: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task OnManageNotificationsAsync()
    {
        if (_reminderService == null || _notificationService == null)
        {
            await DisplayAlert($"{Emojis.Info} Not Available", 
                "Notification services are not available.", "OK");
            return;
        }

        try
        {
            var reminders = await _reminderService.GetAllScheduledRemindersAsync();
            
            if (reminders.Count == 0)
            {
                await DisplayAlert($"{Emojis.Info} No Reminders", 
                    "You have no scheduled match reminders.", "OK");
                return;
            }

            var options = reminders.OrderBy(r => r.MatchDate)
                .Select(r => $"{r.HomeTeam} vs {r.AwayTeam} - {r.MatchDate:ddd dd MMM HH:mm}")
                .ToArray();

            var choice = await DisplayActionSheet(
                $"{Emojis.Bell} {reminders.Count} Reminder(s)",
                "Close", "Cancel All", options);

            if (choice == "Cancel All")
            {
                await _reminderService.CancelAllMatchRemindersAsync();
                await DisplayAlert($"{Emojis.Success} Cancelled", "All reminders cancelled.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", $"Failed to load reminders: {ex.Message}", "OK");
        }
    }

    // ========== DELETE OPERATIONS ==========

    private async System.Threading.Tasks.Task OnDeleteAllFixturesAsync()
    {
        var ok = await DisplayAlert($"{Emojis.Warning} Delete ALL",
            "Delete every fixture in the database?", "Delete All", "Cancel");
        if (!ok) return;

        int removed = DataStore.Data.Fixtures.Count;
        
        if (_reminderService != null)
        {
            try { await _reminderService.CancelAllMatchRemindersAsync(); }
            catch { }
        }
        
        DataStore.Data.Fixtures.Clear();
        DataStore.Save();

        _selectedFixture = null;
        ClearScorecard();
        RefreshList();

        await DisplayAlert($"{Emojis.Success} Done", $"Deleted {removed} fixture(s).", "OK");
    }

    private async System.Threading.Tasks.Task OnDeleteActiveSeasonFixturesAsync()
    {
        var seasonId = DataStore.Data.ActiveSeasonId;
        if (seasonId is null)
        {
            await DisplayAlert($"{Emojis.Info} No Active Season",
                "Set an active season first.", "OK");
            return;
        }

        var ok = await DisplayAlert($"{Emojis.Warning} Delete Season Fixtures",
            "Delete all fixtures in the active season?", "Delete", "Cancel");
        if (!ok) return;

        int before = DataStore.Data.Fixtures.Count;
        DataStore.Data.Fixtures.RemoveAll(f => f.SeasonId == seasonId);
        int removed = before - DataStore.Data.Fixtures.Count;

        DataStore.Save();

        if (_selectedFixture?.SeasonId == seasonId)
        {
            _selectedFixture = null;
            ClearScorecard();
        }

        RefreshList();
        await DisplayAlert($"{Emojis.Success} Done", $"Deleted {removed} fixture(s).", "OK");
    }

    private async System.Threading.Tasks.Task OnGenerateFixturesAsync()
    {
        var seasonId = DataStore.Data.ActiveSeasonId;
        
        if (seasonId is null)
        {
            var activeSeason = DataStore.Data.Seasons.FirstOrDefault(s => s.IsActive);
            if (activeSeason != null)
            {
                seasonId = activeSeason.Id;
                DataStore.Data.ActiveSeasonId = seasonId;
                try { DataStore.Save(); } catch { }
            }
        }
        
        if (seasonId is null)
        {
            await DisplayAlert("No Active Season",
                "Create or set an active season first.", "OK");
            return;
        }

        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == seasonId);
        if (season == null)
        {
            await DisplayAlert("Error", "Season not found.", "OK");
            return;
        }
        
        var teamCounts = DataStore.Data.Teams
            .Where(t => t.SeasonId == seasonId)
            .GroupBy(t => t.DivisionId)
            .Select(g => g.Count())
            .ToList();

        if (teamCounts.All(x => x < 2))
        {
            await DisplayAlert("Cannot Generate",
                "Need at least one division with 2+ teams.", "OK");
            return;
        }

        var existing = DataStore.Data.Fixtures.Count(f => f.SeasonId == seasonId);
        var confirm = await DisplayAlert("Generate Fixtures",
            $"Generate fixtures for '{season.Name}'?" + 
            (existing > 0 ? $"\n\n{existing} existing will be replaced." : ""),
            "Generate", "Cancel");

        if (!confirm) return;

        try
        {
            var settings = DataStore.Data.Settings;
            var fixtures = Services.FixtureGenerator.Generate(
                league: DataStore.Data,
                seasonId: seasonId.Value,
                startDate: season.StartDate,
                matchNight: settings.DefaultMatchDay,
                roundsPerOpponent: settings.DefaultRoundsPerOpponent,
                kickoff: new TimeSpan(19, 30, 0));

            DataStore.Data.Fixtures.RemoveAll(f => f.SeasonId == seasonId);
            DataStore.Data.Fixtures.AddRange(fixtures);
            DataStore.Save();

            _selectedFixture = null;
            ClearScorecard();
            RefreshList();

            await DisplayAlert($"{Emojis.Success} Success",
                $"Generated {fixtures.Count} fixture(s).", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", ex.Message, "OK");
        }
    }

    // ========== LIFECYCLE ==========

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        if (!_servicesInitialized && Handler?.MauiContext != null)
        {
            try
            {
                _reminderService = Handler.MauiContext.Services.GetService<MatchReminderService>();
                _notificationService = Handler.MauiContext.Services.GetService<INotificationService>();
                _servicesInitialized = true;
            }
            catch { }
        }
        
        RefreshList();
    }
}
