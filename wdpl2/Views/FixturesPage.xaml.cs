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
        public bool HasReminder { get; init; } // NEW: Track if reminder is scheduled
    }

    // Right panel per-frame row
    private sealed class FrameRow
    {
        public int Number;
        public Picker HomePlayerPicker = null!;
        public Picker AwayPlayerPicker = null!;
        public Button HomeScoreBtn = null!;
        public Button AwayScoreBtn = null!;
        public CheckBox EightBallCheck = null!;
    }

    private readonly ObservableCollection<FixtureListItem> _items = new();
    private readonly List<FrameRow> _frameRows = new();

    private Fixture? _selectedFixture;
    private bool _isFlyoutOpen = false;
    
    // NEW: Services for notification management
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

        // Initial control defaults - ADD NULL CHECK
        if (FromDate != null)
        {
            FromDate.Date = DateTime.Today.AddDays(-7);
        }

        // Bind list
        FixturesList.ItemsSource = _items;

        // Wire events
        FixturesList.SelectionChanged += OnSelectFixture;
        SearchEntry.TextChanged += (_, __) => RefreshList();
        
        // ADD NULL CHECK HERE TOO
        if (FromDate != null)
            FromDate.DateSelected += (_, __) => RefreshList();
        
        ActiveSeasonOnly.Toggled += (_, __) => RefreshList();

        SaveBtn.Clicked += async (_, __) => await SaveFromUIAsync(); // NOW ASYNC
        ClearBtn.Clicked += (_, __) => OnClearFrames();
        DiagnosticsBtn.Clicked += async (_, __) => await OnDiagnosticsAsync();
        GenerateFixturesBtn.Clicked += async (_, __) => await OnGenerateFixturesAsync();
        DeleteAllBtn.Clicked += async (_, __) => await OnDeleteAllFixturesAsync();
        DeleteSeasonBtn.Clicked += async (_, __) => await OnDeleteActiveSeasonFixturesAsync();
        
        // NEW: Notification management button (check if button exists)
        if (ManageNotificationsBtn != null)
        {
            ManageNotificationsBtn.Clicked += async (_, __) => await OnManageNotificationsAsync();
        }

        // SUBSCRIBE to global season changes
        System.Diagnostics.Debug.WriteLine("   Subscribing to SeasonService.SeasonChanged...");
        SeasonService.SeasonChanged += OnGlobalSeasonChanged;
        System.Diagnostics.Debug.WriteLine($"   SeasonService.CurrentSeasonId: {SeasonService.CurrentSeasonId?.ToString() ?? "NULL"}");

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
            System.Diagnostics.Debug.WriteLine($"=== FIXTURES PAGE: Season Changed Event ===");
            System.Diagnostics.Debug.WriteLine($"Old Season: {e.OldSeasonId?.ToString() ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"New Season: {e.NewSeasonId?.ToString() ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"New Season Name: {e.NewSeason?.Name ?? "NULL"}");
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Update the active season toggle state if no season is active
                if (!e.NewSeasonId.HasValue)
                {
                    ActiveSeasonOnly.IsToggled = false; // Show all when no season
                }
                
                // Force clear the list first
                System.Diagnostics.Debug.WriteLine($"?? Clearing fixtures list...");
                _items.Clear();
                
                // Clear selected fixture
                _selectedFixture = null;
                FramesHost.Children.Clear();
                _frameRows.Clear();
                HeaderLbl.Text = e.NewSeasonId.HasValue ? "Select a fixture" : "No season selected";
                ScoreLbl.Text = "";
                if (ReminderStatusLabel != null)
                    ReminderStatusLabel.Text = "";
                
                // Then refresh
                RefreshList();
                
                var statusMsg = e.NewSeason != null 
                    ? $"Season changed to: {e.NewSeason.Name}" 
                    : "No active season - data cleared";
                
                System.Diagnostics.Debug.WriteLine(statusMsg);
                System.Diagnostics.Debug.WriteLine("=== FIXTURES PAGE: Refresh Complete ===");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FixturesPage Season change error: {ex}");
        }
    }

    // ========== BURGER MENU ==========

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

        // Animate flyout sliding in
        FlyoutPanel.TranslationX = -400;
        await FlyoutPanel.TranslateTo(0, 0, 250, Easing.CubicOut);
        
        // NEW: Update pending notification count
        await UpdatePendingNotificationCountAsync();
    }

    private async void CloseFlyout()
    {
        // Animate flyout sliding out
        await FlyoutPanel.TranslateTo(-400, 0, 250, Easing.CubicIn);
        
        FlyoutOverlay.IsVisible = false;
        FlyoutPanel.IsVisible = false;
        _isFlyoutOpen = false;
    }
    
    // NEW: Update pending notification count
    private async System.Threading.Tasks.Task UpdatePendingNotificationCountAsync()
    {
        // Check if label exists (might not exist in XAML yet)
        if (PendingNotificationsLabel == null)
            return;
            
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

    // ========== DIAGNOSTICS ========= []
    
    private async System.Threading.Tasks.Task OnDiagnosticsAsync()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ACTIVE SEASON DIAGNOSTICS\n");
        
        // Check ActiveSeasonId
        var activeSeasonId = DataStore.Data.ActiveSeasonId;
        sb.AppendLine($"ActiveSeasonId Property: {(activeSeasonId.HasValue ? activeSeasonId.Value.ToString() : "NOT SET")}");
        sb.AppendLine();
        
        // Check all seasons
        var seasons = DataStore.Data.Seasons ?? new List<Season>();
        sb.AppendLine($"Total Seasons: {seasons.Count}");
        sb.AppendLine();
        
        if (seasons.Count == 0)
        {
            sb.AppendLine("NO SEASONS FOUND!");
            sb.AppendLine("\nPlease create a season on the Seasons page first.");
        }
        else
        {
            sb.AppendLine("Seasons:");
            foreach (var season in seasons.OrderByDescending(s => s.IsActive))
            {
                var activeMarker = season.IsActive ? "ACTIVE" : "   ";
                var matchesId = activeSeasonId.HasValue && season.Id == activeSeasonId.Value ? " (matches ActiveSeasonId)" : "";
                sb.AppendLine($"{activeMarker} {season.Name}{matchesId}");
                sb.AppendLine($"     ID: {season.Id}");
                sb.AppendLine($"     IsActive: {season.IsActive}");
                sb.AppendLine();
            }
        }
        
        // Check teams and divisions
        var divisions = DataStore.Data.Divisions?.Count(d => activeSeasonId.HasValue && d.SeasonId == activeSeasonId.Value) ?? 0;
        var teams = DataStore.Data.Teams?.Count(t => activeSeasonId.HasValue && t.SeasonId == activeSeasonId.Value) ?? 0;
        
        sb.AppendLine($"Data for Active Season:");
        sb.AppendLine($"  Divisions: {divisions}");
        sb.AppendLine($"  Teams: {teams}");
        
        // NEW: Notification diagnostics
        if (_notificationService != null && _reminderService != null)
        {
            sb.AppendLine();
            sb.AppendLine("NOTIFICATION STATUS:");
            try
            {
                var enabled = await _notificationService.AreNotificationsEnabledAsync();
                var pending = await _notificationService.GetPendingNotificationCountAsync();
                sb.AppendLine($"  Notifications Enabled: {enabled}");
                sb.AppendLine($"  Pending Reminders: {pending}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Error checking notifications: {ex.Message}");
            }
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("NOTIFICATION STATUS:");
            sb.AppendLine("  Service not available");
        }
        
        await DisplayAlert("Diagnostics", sb.ToString(), "OK");
    }

    // ---------------- LEFT LIST DATA ----------------

    private void RefreshList()
    {
        System.Diagnostics.Debug.WriteLine($"=== FIXTURES RefreshList START ===");
        System.Diagnostics.Debug.WriteLine($"   ActiveSeasonOnly.IsToggled: {ActiveSeasonOnly?.IsToggled ?? false}");
        System.Diagnostics.Debug.WriteLine($"   DataStore.ActiveSeasonId: {DataStore.Data?.ActiveSeasonId?.ToString() ?? "NULL"}");
        
        _items.Clear();

        var data = DataStore.Data;
        
        if (data == null)
        {
            System.Diagnostics.Debug.WriteLine("   DataStore.Data is NULL!");
            return;
        }
        
        var teamById = data.Teams.ToDictionary(t => t.Id, t => t);
        var venueById = data.Venues.ToDictionary(v => v.Id, v => v);
        var tableById = data.Venues
            .SelectMany(v => v.Tables.Select(t => new { v.Id, Table = t }))
            .ToDictionary(x => x.Table.Id, x => (venueId: x.Id, table: x.Table));

        IEnumerable<Fixture> src = data.Fixtures;
        System.Diagnostics.Debug.WriteLine($"   Total fixtures in database: {data.Fixtures.Count}");

        if (ActiveSeasonOnly.IsToggled && data.ActiveSeasonId != null)
        {
            System.Diagnostics.Debug.WriteLine($"   Filtering by active season: {data.ActiveSeasonId}");
            src = src.Where(f => f.SeasonId == data.ActiveSeasonId);
        }
        else if (ActiveSeasonOnly.IsToggled && data.ActiveSeasonId == null)
        {
            System.Diagnostics.Debug.WriteLine("   ? ActiveSeasonOnly is ON but NO active season - returning 0 fixtures");
            // No active season, so show no fixtures when toggle is on
            System.Diagnostics.Debug.WriteLine($"=== FIXTURES RefreshList END: 0 fixtures ===");
            return;
        }

        // ADD NULL CHECK
        if (FromDate != null)
        {
            var from = FromDate.Date.Date;
            System.Diagnostics.Debug.WriteLine($"   Filtering by date from: {from:yyyy-MM-dd}");
            src = src.Where(f => f.Date.Date >= from);
        }

        var q = (SearchEntry.Text ?? "").Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(q))
        {
            System.Diagnostics.Debug.WriteLine($"   Filtering by search query: '{q}'");
            src = src.Where(f =>
            {
                var home = teamById.TryGetValue(f.HomeTeamId, out var ht) ? (ht.Name ?? "") : "";
                var away = teamById.TryGetValue(f.AwayTeamId, out var at) ? (at.Name ?? "") : "";
                var venue = f.VenueId.HasValue && venueById.TryGetValue(f.VenueId.Value, out var v) ? (v.Name ?? "") : "";
                return home.ToLower().Contains(q) || away.ToLower().Contains(q) || venue.ToLower().Contains(q);
            });
        }

        var fixturesList = src.OrderBy(f => f.Date).ToList();
        System.Diagnostics.Debug.WriteLine($"   After filtering: {fixturesList.Count} fixtures");

        foreach (var f in fixturesList)
        {
            var home = teamById.TryGetValue(f.HomeTeamId, out var ht) ? (ht.Name ?? "Home") : "Home";
            var away = teamById.TryGetValue(f.AwayTeamId, out var at) ? (at.Name ?? "Away") : "Away";

            string subtitle = "";
            if (f.VenueId.HasValue && venueById.TryGetValue(f.VenueId.Value, out var v))
                subtitle = v.Name;
            if (f.TableId.HasValue && tableById.TryGetValue(f.TableId.Value, out var tt))
                subtitle = string.IsNullOrEmpty(subtitle) ? tt.table.Label : $"{subtitle} • {tt.table.Label}";

            // NEW: Check if this fixture has a reminder scheduled
            bool hasReminder = f.Date > DateTime.Now; // Only future fixtures can have reminders

            _items.Add(new FixtureListItem
            {
                Id = f.Id,
                Date = f.Date,
                Title = $"{home} vs {away}",
                Subtitle = subtitle,
                HasReminder = hasReminder
            });
        }
        
        System.Diagnostics.Debug.WriteLine($"   Added {_items.Count} fixtures to list");
        System.Diagnostics.Debug.WriteLine($"=== FIXTURES RefreshList END ===");
    }

    private void OnSelectFixture(object? sender, SelectionChangedEventArgs e)
    {
        var li = e.CurrentSelection.FirstOrDefault() as FixtureListItem;
        if (li == null)
        {
            _selectedFixture = null;
            FramesHost.Children.Clear();
            _frameRows.Clear();
            HeaderLbl.Text = "Select a fixture";
            ScoreLbl.Text = "";
            if (ReminderStatusLabel != null) // NEW: Null check
                ReminderStatusLabel.Text = "";
            return;
        }

        _selectedFixture = DataStore.Data.Fixtures.First(x => x.Id == li.Id);
        BuildFrameEditors();
        UpdateHeader();
        UpdateReminderStatus(); // NEW
    }
    
    // NEW: Update reminder status display
    private void UpdateReminderStatus()
    {
        // Check if label exists
        if (ReminderStatusLabel == null)
            return;
            
        if (_selectedFixture == null || _notificationService == null)
        {
            ReminderStatusLabel.Text = "";
            return;
        }

        if (_selectedFixture.Date <= DateTime.Now)
        {
            ReminderStatusLabel.Text = $"{Emojis.Info} Match has passed - no reminder";
            return;
        }

        // Check if reminders are enabled in settings
        var settings = DataStore.Data.Settings;
        if (!settings.MatchRemindersEnabled)
        {
            ReminderStatusLabel.Text = $"{Emojis.Warning} Match reminders disabled in Settings";
            return;
        }

        var hoursUntil = (_selectedFixture.Date - DateTime.Now).TotalHours;
        
        // Use configurable hours from settings (Phase 3)
        var hoursBeforeMatch = settings.ReminderHoursBefore;
        var reminderTime = _selectedFixture.Date.AddHours(-hoursBeforeMatch);

        if (DateTime.Now < reminderTime)
        {
            ReminderStatusLabel.Text = $"{Emojis.Bell} Reminder scheduled for {reminderTime:ddd HH:mm} ({hoursBeforeMatch}h before)";
        }
        else
        {
            ReminderStatusLabel.Text = $"{Emojis.Warning} Match within reminder window";
        }
    }

    // ---------------- RIGHT PANEL (FRAMES) ----------------

    private void BuildFrameEditors()
    {
        if (_selectedFixture == null) return;

        // Determine frame count
        int frames = 10;
        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _selectedFixture.SeasonId);
        if (season != null && season.FramesPerMatch > 0) frames = season.FramesPerMatch;

        while (_selectedFixture.Frames.Count < frames)
            _selectedFixture.Frames.Add(new FrameResult { Number = _selectedFixture.Frames.Count + 1 });
        if (_selectedFixture.Frames.Count > frames)
            _selectedFixture.Frames = _selectedFixture.Frames.Take(frames).ToList();

        var homePlayers = DataStore.Data.Players
            .Where(p => p.TeamId == _selectedFixture.HomeTeamId)
            .OrderBy(p => p.LastName ?? "")
            .ThenBy(p => p.FirstName ?? "")
            .ToList();

        var awayPlayers = DataStore.Data.Players
            .Where(p => p.TeamId == _selectedFixture.AwayTeamId)
            .OrderBy(p => p.LastName ?? "")
            .ThenBy(p => p.FirstName ?? "")
            .ToList();

        FramesHost.Children.Clear();
        _frameRows.Clear();

        for (int i = 0; i < frames; i++)
        {
            var fr = _selectedFixture.Frames[i];

            var row = new Grid
            {
                ColumnSpacing = 8,
                Padding = new Thickness(4, 4),
                RowSpacing = 4
            };
            
            // Layout: Frame# | Home Player (takes full width) | 1/0 btn | v | 0/1 btn | Away Player (takes full width) | 8-ball
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });      // Frame number
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });  // Home player (expanded)
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });      // Home score button
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });      // "v"
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });      // Away score button
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });  // Away player (expanded)
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });      // 8-ball checkbox

            var numLbl = new Label
            {
                Text = (i + 1).ToString(),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold
            };

            var homePicker = new Picker 
            { 
                Title = "Home Player",
                FontSize = 13
            };
            // Don't set ItemDisplayBinding - it defaults to ToString() which uses FullName
            homePicker.ItemsSource = homePlayers;
            if (fr.HomePlayerId.HasValue)
                homePicker.SelectedItem = homePlayers.FirstOrDefault(p => p.Id == fr.HomePlayerId.Value);

            // Home score button (shows 1 or 0)
            var homeScoreBtn = new Button
            {
                Text = fr.Winner == FrameWinner.Home ? "1" : "0",
                BackgroundColor = fr.Winner == FrameWinner.Home ? Color.FromArgb("#10B981") : Color.FromArgb("#E5E7EB"),
                TextColor = fr.Winner == FrameWinner.Home ? Colors.White : Color.FromArgb("#6B7280"),
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 6,
                Padding = new Thickness(0),
                WidthRequest = 40,
                HeightRequest = 40
            };

            var vsLbl = new Label
            {
                Text = "v",
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 12,
                TextColor = Colors.Gray,
                FontAttributes = FontAttributes.Bold
            };

            // Away score button (shows 1 or 0)
            var awayScoreBtn = new Button
            {
                Text = fr.Winner == FrameWinner.Away ? "1" : "0",
                BackgroundColor = fr.Winner == FrameWinner.Away ? Color.FromArgb("#EF4444") : Color.FromArgb("#E5E7EB"),
                TextColor = fr.Winner == FrameWinner.Away ? Colors.White : Color.FromArgb("#6B7280"),
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 6,
                Padding = new Thickness(0),
                WidthRequest = 40,
                HeightRequest = 40
            };

            var awayPicker = new Picker 
            { 
                Title = "Away Player",
                FontSize = 13
            };
            // Don't set ItemDisplayBinding - it defaults to ToString() which uses FullName
            awayPicker.ItemsSource = awayPlayers;
            if (fr.AwayPlayerId.HasValue)
                awayPicker.SelectedItem = awayPlayers.FirstOrDefault(p => p.Id == fr.AwayPlayerId.Value);

            // Button click handlers - toggle between 1 and 0
            homeScoreBtn.Clicked += (s, e) =>
            {
                if (homeScoreBtn.Text == "0")
                {
                    // Set home to 1, away to 0
                    homeScoreBtn.Text = "1";
                    homeScoreBtn.BackgroundColor = Color.FromArgb("#10B981");
                    homeScoreBtn.TextColor = Colors.White;
                    
                    awayScoreBtn.Text = "0";
                    awayScoreBtn.BackgroundColor = Color.FromArgb("#E5E7EB");
                    awayScoreBtn.TextColor = Color.FromArgb("#6B7280");
                }
                else
                {
                    // Set home back to 0
                    homeScoreBtn.Text = "0";
                    homeScoreBtn.BackgroundColor = Color.FromArgb("#E5E7EB");
                    homeScoreBtn.TextColor = Color.FromArgb("#6B7280");
                }
            };

            awayScoreBtn.Clicked += (s, e) =>
            {
                if (awayScoreBtn.Text == "0")
                {
                    // Set away to 1, home to 0
                    awayScoreBtn.Text = "1";
                    awayScoreBtn.BackgroundColor = Color.FromArgb("#EF4444");
                    awayScoreBtn.TextColor = Colors.White;
                    
                    homeScoreBtn.Text = "0";
                    homeScoreBtn.BackgroundColor = Color.FromArgb("#E5E7EB");
                    homeScoreBtn.TextColor = Color.FromArgb("#6B7280");
                }
                else
                {
                    // Set away back to 0
                    awayScoreBtn.Text = "0";
                    awayScoreBtn.BackgroundColor = Color.FromArgb("#E5E7EB");
                    awayScoreBtn.TextColor = Color.FromArgb("#6B7280");
                }
            };

            var eightBallLayout = new HorizontalStackLayout
            {
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var eight = new CheckBox 
            { 
                IsChecked = fr.EightBall,
                Color = Color.FromArgb("#F59E0B")
            };

            var eightLbl = new Label
            {
                Text = "8?",
                FontSize = 12,
                VerticalTextAlignment = TextAlignment.Center
            };

            eightBallLayout.Children.Add(eight);
            eightBallLayout.Children.Add(eightLbl);

            Grid.SetColumn(numLbl, 0); row.Children.Add(numLbl);
            Grid.SetColumn(homePicker, 1); row.Children.Add(homePicker);
            Grid.SetColumn(homeScoreBtn, 2); row.Children.Add(homeScoreBtn);
            Grid.SetColumn(vsLbl, 3); row.Children.Add(vsLbl);
            Grid.SetColumn(awayScoreBtn, 4); row.Children.Add(awayScoreBtn);
            Grid.SetColumn(awayPicker, 5); row.Children.Add(awayPicker);
            Grid.SetColumn(eightBallLayout, 6); row.Children.Add(eightBallLayout);

            FramesHost.Children.Add(row);

            _frameRows.Add(new FrameRow
            {
                Number = i + 1,
                HomePlayerPicker = homePicker,
                AwayPlayerPicker = awayPicker,
                HomeScoreBtn = homeScoreBtn,
                AwayScoreBtn = awayScoreBtn,
                EightBallCheck = eight
            });
        }
    }

    // NEW: Async version of SaveFromUI with notification scheduling
    private async System.Threading.Tasks.Task SaveFromUIAsync()
    {
        if (_selectedFixture == null) return;

        foreach (var row in _frameRows)
        {
            var fr = _selectedFixture.Frames[row.Number - 1];

            var home = row.HomePlayerPicker.SelectedItem as Player;
            var away = row.AwayPlayerPicker.SelectedItem as Player;

            fr.HomePlayerId = home?.Id;
            fr.AwayPlayerId = away?.Id;

            // Determine winner from button text
            if (row.HomeScoreBtn.Text == "1")
                fr.Winner = FrameWinner.Home;
            else if (row.AwayScoreBtn.Text == "1")
                fr.Winner = FrameWinner.Away;
            else
                fr.Winner = FrameWinner.None;

            fr.EightBall = row.EightBallCheck.IsChecked;
        }

        DataStore.Save();
        UpdateHeader();
        
        // NEW: Schedule notification for this fixture
        await ScheduleFixtureReminderAsync(_selectedFixture);
        
        UpdateReminderStatus();
        RefreshList(); // Refresh to update bell icon
        
        await DisplayAlert($"{Emojis.Success} Saved", 
            "Fixture results saved successfully!\n\n" +
            (_reminderService != null && _selectedFixture.Date > DateTime.Now 
                ? $"{Emojis.Bell} Match reminder scheduled" 
                : ""),
            "OK");
    }
    
    // NEW: Schedule notification for a fixture
    private async System.Threading.Tasks.Task ScheduleFixtureReminderAsync(Fixture fixture)
    {
        if (_reminderService == null || fixture.Date <= DateTime.Now)
            return;

        // Check if match reminders are enabled in settings
        var settings = DataStore.Data.Settings;
        if (!settings.MatchRemindersEnabled)
        {
            System.Diagnostics.Debug.WriteLine($"Match reminders disabled - skipping notification for fixture {fixture.Id}");
            return;
        }

        try
        {
            var teamById = DataStore.Data.Teams.ToDictionary(t => t.Id, t => t);
            var homeTeam = teamById.TryGetValue(fixture.HomeTeamId, out var ht) ? ht.Name : "Home";
            var awayTeam = teamById.TryGetValue(fixture.AwayTeamId, out var at) ? at.Name : "Away";

            // Use configurable reminder hours from settings (Phase 3)
            var hoursBeforeMatch = settings.ReminderHoursBefore;

            await _reminderService.ScheduleMatchReminderAsync(
                fixture.Id,
                fixture.Date,
                homeTeam ?? "Home",
                awayTeam ?? "Away",
                hoursBeforeMatch: hoursBeforeMatch
            );
            
            System.Diagnostics.Debug.WriteLine($"Scheduled reminder {hoursBeforeMatch}h before match: {homeTeam} vs {awayTeam}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to schedule reminder: {ex.Message}");
        }
    }
    
    // NEW: Cancel notification for a fixture
    private async System.Threading.Tasks.Task CancelFixtureReminderAsync(Guid fixtureId)
    {
        if (_reminderService == null)
            return;

        try
        {
            await _reminderService.CancelMatchReminderAsync(fixtureId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cancel reminder: {ex.Message}");
        }
    }
    
    // NEW: Notification management dialog
    private async System.Threading.Tasks.Task OnManageNotificationsAsync()
    {
        if (_reminderService == null || _notificationService == null)
        {
            await DisplayAlert($"{Emojis.Info} Not Available", 
                "Notification services are not available on this platform.", 
                "OK");
            return;
        }

        try
        {
            var reminders = await _reminderService.GetAllScheduledRemindersAsync();
            
            if (reminders.Count == 0)
            {
                await DisplayAlert($"{Emojis.Info} No Reminders", 
                    "You have no scheduled match reminders.", 
                    "OK");
                return;
            }

            // Build display list
            var options = new List<string>();
            foreach (var reminder in reminders.OrderBy(r => r.MatchDate))
            {
                var hoursUntil = (reminder.MatchDate - DateTime.Now).TotalHours;
                var timeDesc = hoursUntil > 24 
                    ? $"{(int)(hoursUntil / 24)} days" 
                    : $"{(int)hoursUntil} hours";
                    
                options.Add($"{reminder.HomeTeam} vs {reminder.AwayTeam}\n" +
                           $"   {reminder.MatchDate:ddd dd MMM HH:mm} (in {timeDesc})");
            }

            var choice = await DisplayActionSheet(
                $"{Emojis.Bell} {reminders.Count} Scheduled Reminder(s)",
                "Close",
                "Cancel All Reminders",
                options.ToArray());

            if (choice == "Cancel All Reminders")
            {
                var confirm = await DisplayAlert(
                    $"{Emojis.Warning} Confirm",
                    "Cancel all match reminders?",
                    "Yes, Cancel All",
                    "No");

                if (confirm)
                {
                    await _reminderService.CancelAllMatchRemindersAsync();
                    await DisplayAlert($"{Emojis.Success} Cancelled", 
                        "All match reminders have been cancelled.", 
                        "OK");
                    await UpdatePendingNotificationCountAsync();
                }
            }
            else if (choice != null && choice != "Close")
            {
                // User selected a specific reminder - show details
                var index = options.IndexOf(choice);
                if (index >= 0 && index < reminders.Count)
                {
                    var reminder = reminders[index];
                    var details = $"{Emojis.EightBall} Match Details:\n\n" +
                                 $"Home: {reminder.HomeTeam}\n" +
                                 $"Away: {reminder.AwayTeam}\n" +
                                 $"Date: {reminder.MatchDate:dddd, dd MMMM yyyy}\n" +
                                 $"Time: {reminder.MatchDate:HH:mm}\n" +
                                 $"Reminder: {reminder.ReminderTime:HH:mm}";

                    var action = await DisplayActionSheet(
                        details,
                        "Close",
                        "Cancel This Reminder");

                    if (action == "Cancel This Reminder")
                    {
                        await _reminderService.CancelMatchReminderAsync(reminder.FixtureId);
                        await DisplayAlert($"{Emojis.Success} Cancelled", 
                            "Reminder cancelled.", 
                            "OK");
                        await UpdatePendingNotificationCountAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", 
                $"Failed to load reminders: {ex.Message}", 
                "OK");
        }
    }

    private void OnClearFrames()
    {
        if (_selectedFixture == null) return;

        _selectedFixture.Frames.Clear();
        BuildFrameEditors();
        UpdateHeader();
        DataStore.Save();
    }

    private async System.Threading.Tasks.Task OnDeleteAllFixturesAsync()
    {
        var ok = await DisplayAlert($"{Emojis.Warning} Delete ALL Fixtures",
            "This will permanently delete every fixture in the database. Are you sure?",
            "Delete All", "Cancel");
        if (!ok) return;

        int removed = DataStore.Data.Fixtures.Count;
        
        // NEW: Cancel all reminders
        if (_reminderService != null)
        {
            try
            {
                await _reminderService.CancelAllMatchRemindersAsync();
            }
            catch { /* Continue even if cancel fails */ }
        }
        
        DataStore.Data.Fixtures.Clear();
        DataStore.Save();

        _selectedFixture = null;
        FramesHost.Children.Clear();
        _frameRows.Clear();
        HeaderLbl.Text = "Select a fixture";
        ScoreLbl.Text = "";
        if (ReminderStatusLabel != null) // NEW: Null check
            ReminderStatusLabel.Text = "";

        RefreshList();
        await UpdatePendingNotificationCountAsync(); // NEW

        await DisplayAlert($"{Emojis.Success} Done", $"Deleted {removed} fixture(s).", "OK");
    }

    private async System.Threading.Tasks.Task OnDeleteActiveSeasonFixturesAsync()
    {
        var seasonId = DataStore.Data.ActiveSeasonId;
        if (seasonId is null)
        {
            await DisplayAlert($"{Emojis.Info} No Active Season",
                "Set an active season first (or use Delete ALL Fixtures).",
                "OK");
            return;
        }

        var ok = await DisplayAlert($"{Emojis.Warning} Delete Fixtures for Active Season",
            "This will permanently delete all fixtures in the active season. Continue?",
            "Delete", "Cancel");
        if (!ok) return;

        int before = DataStore.Data.Fixtures.Count;
        var fixturesToDelete = DataStore.Data.Fixtures.Where(f => f.SeasonId == seasonId).ToList();
        
        // NEW: Cancel reminders for deleted fixtures
        if (_reminderService != null)
        {
            foreach (var fixture in fixturesToDelete)
            {
                try
                {
                    await _reminderService.CancelMatchReminderAsync(fixture.Id);
                }
                catch { /* Continue even if cancel fails */ }
            }
        }
        
        DataStore.Data.Fixtures.RemoveAll(f => f.SeasonId == seasonId);
        int removed = before - DataStore.Data.Fixtures.Count;

        DataStore.Save();

        if (_selectedFixture != null && _selectedFixture.SeasonId == seasonId)
        {
            _selectedFixture = null;
            FramesHost.Children.Clear();
            _frameRows.Clear();
            HeaderLbl.Text = "Select a fixture";
            ScoreLbl.Text = "";
            if (ReminderStatusLabel != null) // NEW: Null check
                ReminderStatusLabel.Text = "";
        }

        RefreshList();
        await UpdatePendingNotificationCountAsync(); // NEW

        await DisplayAlert($"{Emojis.Success} Done", $"Deleted {removed} fixture(s) in the active season.", "OK");
    }

    private async System.Threading.Tasks.Task OnGenerateFixturesAsync()
    {
        // Debug: Check current state
        var activeSeasonId = DataStore.Data.ActiveSeasonId;
        var activeSeason = DataStore.Data.Seasons?.FirstOrDefault(s => s.IsActive);
        
        System.Diagnostics.Debug.WriteLine("=== GENERATE FIXTURES DEBUG ===");
        System.Diagnostics.Debug.WriteLine($"ActiveSeasonId from DataStore: {activeSeasonId}");
        System.Diagnostics.Debug.WriteLine($"Active season from IsActive flag: {(activeSeason != null ? $"{activeSeason.Name} ({activeSeason.Id})" : "NONE")}");
        System.Diagnostics.Debug.WriteLine($"Total seasons in database: {DataStore.Data.Seasons?.Count ?? 0}");
        
        var seasonId = activeSeasonId;
        
        // Fallback: If ActiveSeasonId is not set, try to find an active season
        if (seasonId is null)
        {
            if (activeSeason != null)
            {
                System.Diagnostics.Debug.WriteLine($"Using fallback - found active season: {activeSeason.Name}");
                seasonId = activeSeason.Id;
                // Also update the ActiveSeasonId for future use
                DataStore.Data.ActiveSeasonId = seasonId;
                try { DataStore.Save(); } catch { }
                System.Diagnostics.Debug.WriteLine($"Set ActiveSeasonId to: {seasonId}");
            }
        }
        
        // If still not set, prompt user to choose one
        if (seasonId is null)
        {
            var picked = await PromptSelectSeasonAsync();
            if (picked.HasValue)
            {
                seasonId = picked.Value;
            }
        }

        if (seasonId is null)
        {
            System.Diagnostics.Debug.WriteLine("No active season found after prompt");
            await DisplayAlert(
                "No Active Season",
                "No season selected. Create or set an active season on the Seasons tab and try again.",
                "OK");
            return;
        }

        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == seasonId);
        if (season == null)
        {
            System.Diagnostics.Debug.WriteLine($"Season with ID {seasonId} not found in database!");
            await DisplayAlert("Error", "Active season not found in database.", "OK");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"Found season: {season.Name}");

        // Check if there are divisions and teams
        var divCount = DataStore.Data.Divisions?.Count(d => d.SeasonId == seasonId) ?? 0;
        var teamCounts = DataStore.Data.Teams
            .Where(t => t.SeasonId == seasonId)
            .GroupBy(t => t.DivisionId)
            .Select(g => new { DivisionId = g.Key, Count = g.Count() })
            .ToList();

        if (divCount == 0 || teamCounts.All(x => x.Count < 2))
        {
            await DisplayAlert(
                "Cannot Generate Fixtures",
                "You need at least one division with 2+ teams (with DivisionId set) before fixtures can be generated.\n\n" +
                "Go to:\n" +
                "• Divisions page to create divisions\n" +
                "• Teams page to assign teams to divisions",
                "OK");
            return;
        }

        // Confirm generation
        var existing = DataStore.Data.Fixtures.Count(f => f.SeasonId == seasonId);
        string warning = existing > 0 
            ? $"\n\nWarning: {existing} existing fixture(s) for this season will be REPLACED."
            : "";

        var confirm = await DisplayAlert(
            "Generate Fixtures",
            $"Generate fixtures for '{season.Name}'?{warning}",
            "Yes, Generate",
            "Cancel");

        if (!confirm) return;

        try
        {
            // Get settings
            var settings = DataStore.Data.Settings;
            var matchNight = settings.DefaultMatchDay;
            var roundsPerOpponent = settings.DefaultRoundsPerOpponent;

            // Generate fixtures using FixtureGenerator
            var fixtures = Services.FixtureGenerator.Generate(
                league: DataStore.Data,
                seasonId: seasonId.Value,
                startDate: season.StartDate,
                matchNight: matchNight,
                roundsPerOpponent: roundsPerOpponent,
                kickoff: new TimeSpan(19, 30, 0));

            // NEW: Cancel reminders for old fixtures being replaced
            if (_reminderService != null && existing > 0)
            {
                var oldFixtures = DataStore.Data.Fixtures.Where(f => f.SeasonId == seasonId).ToList();
                foreach (var oldFixture in oldFixtures)
                {
                    try
                    {
                        await _reminderService.CancelMatchReminderAsync(oldFixture.Id);
                    }
                    catch { /* Continue */ }
                }
            }

            // Remove existing fixtures for this season
            DataStore.Data.Fixtures.RemoveAll(f => f.SeasonId == seasonId);
            
            // Add new fixtures
            DataStore.Data.Fixtures.AddRange(fixtures);

            DataStore.Save();

            // NEW: Schedule reminders for all new fixtures
            if (_reminderService != null)
            {
                int scheduled = 0;
                foreach (var fixture in fixtures.Where(f => f.Date > DateTime.Now))
                {
                    try
                    {
                        await ScheduleFixtureReminderAsync(fixture);
                        scheduled++;
                    }
                    catch { /* Continue */ }
                }
                
                System.Diagnostics.Debug.WriteLine($"Scheduled {scheduled} match reminders");
            }

            // Clear selection if it was for this season
            if (_selectedFixture != null && _selectedFixture.SeasonId == seasonId)
            {
                _selectedFixture = null;
                FramesHost.Children.Clear();
                _frameRows.Clear();
                HeaderLbl.Text = "Select a fixture";
                ScoreLbl.Text = "";
                if (ReminderStatusLabel != null) // NEW: Null check
                    ReminderStatusLabel.Text = "";
            }

            RefreshList();
            await UpdatePendingNotificationCountAsync(); // NEW

            var reminderMsg = _reminderService != null 
                ? $"\n\n{Emojis.Bell} Match reminders scheduled automatically" 
                : "";

            await DisplayAlert(
                $"{Emojis.Success} Success",
                $"Generated {fixtures.Count} fixture(s) for '{season.Name}'{reminderMsg}",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", $"Failed to generate fixtures:\n\n{ex.Message}", "OK");
        }
    }

    private async System.Threading.Tasks.Task<Guid?> PromptSelectSeasonAsync()
    {
        var seasons = DataStore.Data.Seasons ?? new List<Season>();
        if (seasons.Count == 0) return null;

        // Build display list with short id to avoid duplicates
        var options = seasons.Select(s => new { Label = $"{s.Name} ({s.StartDate:yyyy})", Id = s.Id }).ToList();
        var labels = options.Select(o => o.Label).ToArray();

        var choice = await DisplayActionSheet("Select season to set active", "Cancel", null, labels);
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return null;

        var selected = options.FirstOrDefault(o => o.Label == choice);
        if (selected == null) return null;

        // Set chosen season as active in data store
        foreach (var s in DataStore.Data.Seasons) s.IsActive = (s.Id == selected.Id);
        DataStore.Data.ActiveSeasonId = selected.Id;
        try { DataStore.Save(); } catch { /* ignore save errors here */ }

        // Notify season service
        try { SeasonService.CurrentSeasonId = selected.Id; } catch { }

        return selected.Id;
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

        HeaderLbl.Text = $"{_selectedFixture.Date:ddd dd MMM yyyy HH:mm} • {home} vs {away}";
        ScoreLbl.Text = $"Score: {_selectedFixture.HomeScore} – {_selectedFixture.AwayScore}";
    }
    
    // NEW: Override OnAppearing to initialize services when handler is available
    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        System.Diagnostics.Debug.WriteLine("=== FIXTURES PAGE: OnAppearing ===");
        System.Diagnostics.Debug.WriteLine($"   SeasonService.CurrentSeasonId: {SeasonService.CurrentSeasonId?.ToString() ?? "NULL"}");
        System.Diagnostics.Debug.WriteLine($"   DataStore.ActiveSeasonId: {DataStore.Data?.ActiveSeasonId?.ToString() ?? "NULL"}");
        
        // Initialize services once when handler is available
        if (!_servicesInitialized && Handler?.MauiContext != null)
        {
            try
            {
                _reminderService = Handler.MauiContext.Services.GetService<MatchReminderService>();
                _notificationService = Handler.MauiContext.Services.GetService<INotificationService>();
                _servicesInitialized = true;
                
                System.Diagnostics.Debug.WriteLine($"Services initialized: Reminder={_reminderService != null}, Notification={_notificationService != null}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize services: {ex.Message}");
            }
        }
        
        // IMPORTANT: Refresh list to ensure we show current season's data
        // This handles the case where the page was created after a season change event
        System.Diagnostics.Debug.WriteLine("   Calling RefreshList from OnAppearing...");
        RefreshList();
    }
}
