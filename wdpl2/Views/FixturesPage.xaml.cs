using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Media;
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
        /// <summary>✅ = fully completed, ⚠️ = partially filled, empty = not started.</summary>
        public string StatusIcon { get; init; } = "";
        public Color StatusColor { get; init; } = Colors.Transparent;
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
        public bool IsDoubles { get; set; }
        public Guid? HomePlayerId { get; set; }
        public string HomePlayerName { get; set; } = "";
        public Guid? AwayPlayerId { get; set; }
        public string AwayPlayerName { get; set; } = "";
        public Guid? HomePlayer2Id { get; set; }
        public string HomePlayer2Name { get; set; } = "";
        public Guid? AwayPlayer2Id { get; set; }
        public string AwayPlayer2Name { get; set; } = "";
        public FrameWinner Winner { get; set; } = FrameWinner.None;
        public bool EightBall { get; set; }

        // UI Elements
        public Border? HomeRowBorder { get; set; }
        public Border? AwayRowBorder { get; set; }
        public Label? HomePlayerLabel { get; set; }
        public Label? AwayPlayerLabel { get; set; }
        public Button? HomeScoreBtn { get; set; }
        public Button? AwayScoreBtn { get; set; }
        public CheckBox? EightBallCheck { get; set; }

        /// <summary>True when all required home players are set.</summary>
        public bool IsHomeComplete => HomePlayerId.HasValue && (!IsDoubles || HomePlayer2Id.HasValue);
        /// <summary>True when all required away players are set.</summary>
        public bool IsAwayComplete => AwayPlayerId.HasValue && (!IsDoubles || AwayPlayer2Id.HasValue);

        public string FormatHomeLabel()
        {
            if (!HomePlayerId.HasValue) return IsDoubles ? "Tap to select pair..." : "Tap to select...";
            if (IsDoubles && !HomePlayer2Id.HasValue) return $"{HomePlayerName} & ?";
            if (IsDoubles) return $"{HomePlayerName} & {HomePlayer2Name}";
            return HomePlayerName;
        }

        public string FormatAwayLabel()
        {
            if (!AwayPlayerId.HasValue) return IsDoubles ? "Tap to select pair..." : "Tap to select...";
            if (IsDoubles && !AwayPlayer2Id.HasValue) return $"{AwayPlayerName} & ?";
            if (IsDoubles) return $"{AwayPlayerName} & {AwayPlayer2Name}";
            return AwayPlayerName;
        }
    }

    private readonly ObservableCollection<FixtureListItem> _items = new();
    private readonly ObservableCollection<PlayerListItem> _homePlayers = new();
    private readonly ObservableCollection<PlayerListItem> _awayPlayers = new();
    private readonly List<FrameRowData> _frameRows = new();
    private readonly Dictionary<string, (int index, bool isHome)> _keyMappings = new();

    private Fixture? _selectedFixture;
    private int _currentFrameIndex = 0; // Which frame is being edited (0-based)
    private bool _selectingHomePlayer = true; // Are we selecting home or away player?
    private bool _isFlyoutOpen = false;

    // Two-phase entry: Home lineup → Away lineup → Results
    private enum EntryPhase { HomeLineup, AwayLineup, Results }
    private EntryPhase _entryPhase = EntryPhase.HomeLineup;
    
    // Services for notification management
    private MatchReminderService? _reminderService;
    private INotificationService? _notificationService;
    private bool _servicesInitialized = false;

    /// <summary>Returns true (and shows alert) if the selected fixture's season is locked.</summary>
    private async Task<bool> CheckSeasonLockedAsync(string action = "modify")
    {
        var seasonId = _selectedFixture?.SeasonId ?? DataStore.Data.ActiveSeasonId;
        if (DataStore.Data.IsSeasonLocked(seasonId))
        {
            await DisplayAlert($"{Emojis.Lock} Season Locked",
                $"Cannot {action} — this season is locked. Unlock it from the Seasons page first.", "OK");
            return true;
        }
        return false;
    }

    public FixturesPage()
    {
        System.Diagnostics.Debug.WriteLine("=== FIXTURES PAGE: Constructor START ===");
        
        InitializeComponent();

        // Wire up burger menu and flyout
        BurgerMenuBtn.Clicked += OnBurgerMenuClicked;
        CloseFlyoutBtn.Clicked += OnCloseFlyoutClicked;
        OverlayTap.Tapped += (_, __) => CloseFlyout();

        // Initial control defaults - set to start of year or active season start date
        if (FromDate != null)
        {
            // Start from the beginning of the current year to ensure all fixtures are visible
            // This will be updated in OnAppearing() when we have season data
            FromDate.Date = new DateTime(DateTime.Today.Year, 1, 1);
        }

        // Bind fixture list
        FixturesList.ItemsSource = _items;

        // Wire events
        FixturesList.SelectionChanged += OnSelectFixture;
        SearchEntry.TextChanged += (_, __) => RefreshList();
        
        if (FromDate != null)
            FromDate.DateSelected += (_, __) => RefreshList();
        
        ActiveSeasonOnly.Toggled += (_, __) => RefreshList();
        DivisionPicker.SelectedIndexChanged += (_, __) => RefreshList();

        SaveBtn.Clicked += async (_, __) => await SaveFromUIAsync();
        ClearBtn.Clicked += (_, __) => OnClearFrames();
        LateCardBtn.Clicked += (_, __) => ToggleLateCardPanel();
        HomeLatePlusBtn.Clicked += (_, __) => AdjustLatePenalty(true, 1);
        HomeLateMinusBtn.Clicked += (_, __) => AdjustLatePenalty(true, -1);
        AwayLatePlusBtn.Clicked += (_, __) => AdjustLatePenalty(false, 1);
        AwayLateMinusBtn.Clicked += (_, __) => AdjustLatePenalty(false, -1);
        CancelMatchBtn.Clicked += (_, __) => ToggleCancelPanel();
        CancelHomeBtn.Clicked += (_, __) => SetCancelledBy(FrameWinner.Home);
        CancelAwayBtn.Clicked += (_, __) => SetCancelledBy(FrameWinner.Away);
        CancelPenaltyPlusBtn.Clicked += (_, __) => AdjustCancelPenalty(1);
        CancelPenaltyMinusBtn.Clicked += (_, __) => AdjustCancelPenalty(-1);
        DiagnosticsBtn.Clicked += async (_, __) => await OnDiagnosticsAsync();
        GenerateFixturesBtn.Clicked += async (_, __) => await OnGenerateFixturesAsync();
        DeleteAllBtn.Clicked += async (_, __) => await OnDeleteAllFixturesAsync();
        DeleteSeasonBtn.Clicked += async (_, __) => await OnDeleteActiveSeasonFixturesAsync();

        // Add Reschedule + Undo + Print Scorecard + Bulk Score buttons (defined in XAML flyout)
        RescheduleBtn.Clicked += async (_, __) => await OnRescheduleFixtureAsync();
        UndoSaveBtn.Clicked += async (_, __) =>
        {
            var confirm = await DisplayAlert("Undo", "Revert to the state before last save?", "Undo", "Cancel");
            if (!confirm) return;
            if (DataStore.UndoLastSave())
            {
                _selectedFixture = null;
                ClearScorecard();
                RefreshList();
                await DisplayAlert($"{Emojis.Success} Undone", "Reverted to previous save.", "OK");
            }
            else
                await DisplayAlert($"{Emojis.Error} Undo Failed", "No backup available.", "OK");
        };
        PrintScorecardBtn.Clicked += async (_, __) => await OnPrintScorecardAsync();
        BulkScoreBtn.Clicked += async (_, __) => await OnBulkScoreEntryAsync();
        
        if (ManageNotificationsBtn != null)
        {
            ManageNotificationsBtn.Clicked += async (_, __) => await OnManageNotificationsAsync();
        }

        if (ScanScoreCardBtn != null)
        {
            ScanScoreCardBtn.Clicked += async (_, __) => await OnScanScoreCardAsync();
        }

        // Wire up keyboard capture for quick player selection
        if (KeyboardCaptureEntry != null)
        {
            KeyboardCaptureEntry.TextChanged += OnKeyboardInput;
        }

        System.Diagnostics.Debug.WriteLine("=== FIXTURES PAGE: Constructor END, calling RefreshList ===");
        RefreshList();
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
        // Legacy handler - kept for compatibility but now using quick panels
    }

    private void OnAwayPlayerSelected(object? sender, SelectionChangedEventArgs e)
    {
        // Legacy handler - kept for compatibility but now using quick panels
    }

    private void OnQuickPlayerTapped(Guid playerId, string playerName, bool isHomeTeam)
    {
        System.Diagnostics.Debug.WriteLine($"=== QUICK PLAYER TAP: {playerName} (Home={isHomeTeam}, Phase={_entryPhase}) ===");

        if (_selectedFixture == null || _currentFrameIndex < 0 || _currentFrameIndex >= _frameRows.Count)
        {
            System.Diagnostics.Debug.WriteLine("  => EARLY RETURN - fixture or frame invalid");
            return;
        }

        var frameRow = _frameRows[_currentFrameIndex];
        bool isVoid = FrameResult.IsVoidPlayer(playerId);

        // Skip max-frame and duplicate-pairing checks for void players
        if (!isVoid)
        {
            // Check if player has already played 3 frames (WDPL rule)
            int playerFrameCount = _frameRows.Count(f => 
                (isHomeTeam && f.HomePlayerId == playerId) || 
                (!isHomeTeam && f.AwayPlayerId == playerId));

            if (playerFrameCount >= 3)
            {
                System.Diagnostics.Debug.WriteLine($"  => BLOCKED - {playerName} already has {playerFrameCount} frames (max 3)");
                _ = DisplayAlert("Maximum Frames Reached", 
                    $"{playerName} has already played 3 frames.\n\nEach player can only play a maximum of 3 frames per match.", 
                    "OK");
                return;
            }
        }

        if (isHomeTeam)
        {
            // Doubles: if player 1 is set but player 2 is not, assign player 2
            if (frameRow.IsDoubles && frameRow.HomePlayerId.HasValue && !frameRow.HomePlayer2Id.HasValue && !isVoid)
            {
                // Don't allow same player as player 1
                if (frameRow.HomePlayerId == playerId)
                {
                    _ = DisplayAlert("Same Player", "Player 2 must be different from Player 1.", "OK");
                    return;
                }
                frameRow.HomePlayer2Id = playerId;
                frameRow.HomePlayer2Name = playerName;
                if (frameRow.HomePlayerLabel != null)
                {
                    frameRow.HomePlayerLabel.Text = frameRow.FormatHomeLabel();
                    frameRow.HomePlayerLabel.TextColor = Color.FromArgb("#1E293B");
                    frameRow.HomePlayerLabel.FontAttributes = FontAttributes.None;
                }
                System.Diagnostics.Debug.WriteLine($"  => Assigned HOME player 2 to doubles frame {_currentFrameIndex + 1}");

                // Now advance since the pair is complete
                AdvanceToNextSlot(isHomeTeam);
                UpdateCurrentFrameIndicator();
                UpdatePlayerFrameCounts();
                HighlightCurrentFrame();
                return;
            }

            // Check for duplicate pairing if away player is already set (skip for void)
            if (!isVoid && frameRow.AwayPlayerId.HasValue && !FrameResult.IsVoidPlayer(frameRow.AwayPlayerId))
            {
                var duplicatePairing = _frameRows.Any(f => 
                    f != frameRow && 
                    f.HomePlayerId == playerId && 
                    f.AwayPlayerId == frameRow.AwayPlayerId);

                if (duplicatePairing)
                {
                    var awayName = frameRow.AwayPlayerName;
                    System.Diagnostics.Debug.WriteLine($"  => BLOCKED - {playerName} vs {awayName} already played");
                    _ = DisplayAlert("Duplicate Pairing", 
                        $"{playerName} has already played against {awayName} in this match.\n\nNo repeat pairings allowed.", 
                        "OK");
                    return;
                }
            }

            // For doubles: if replacing player 1, clear player 2
            if (frameRow.IsDoubles)
            {
                frameRow.HomePlayer2Id = null;
                frameRow.HomePlayer2Name = "";
            }

            frameRow.HomePlayerId = playerId;
            frameRow.HomePlayerName = playerName;
            if (frameRow.HomePlayerLabel != null)
            {
                frameRow.HomePlayerLabel.Text = frameRow.FormatHomeLabel();
                frameRow.HomePlayerLabel.TextColor = isVoid ? Color.FromArgb("#EA580C") : Color.FromArgb("#1E293B");
                frameRow.HomePlayerLabel.FontAttributes = FontAttributes.None;
            }
            System.Diagnostics.Debug.WriteLine($"  => Assigned HOME player to frame {_currentFrameIndex + 1}");

            // Auto-award win to opponent when voiding
            if (isVoid)
            {
                frameRow.Winner = FrameWinner.Away;
                if (frameRow.HomeScoreBtn != null)
                {
                    frameRow.HomeScoreBtn.Text = "0";
                    frameRow.HomeScoreBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
                    frameRow.HomeScoreBtn.TextColor = Color.FromArgb("#94A3B8");
                }
                if (frameRow.AwayScoreBtn != null)
                {
                    frameRow.AwayScoreBtn.Text = "1";
                    frameRow.AwayScoreBtn.BackgroundColor = Color.FromArgb("#DC2626");
                    frameRow.AwayScoreBtn.TextColor = Colors.White;
                }
                UpdateScoreDisplay();
            }

            // For doubles: don't advance yet if player 2 still needed (unless void)
            if (frameRow.IsDoubles && !isVoid && !frameRow.HomePlayer2Id.HasValue)
            {
                // Stay on this frame to collect player 2
                UpdateCurrentFrameIndicator();
                UpdatePlayerFrameCounts();
                HighlightCurrentFrame();
                return;
            }
        }
        else
        {
            // Doubles: if player 1 is set but player 2 is not, assign player 2
            if (frameRow.IsDoubles && frameRow.AwayPlayerId.HasValue && !frameRow.AwayPlayer2Id.HasValue && !isVoid)
            {
                if (frameRow.AwayPlayerId == playerId)
                {
                    _ = DisplayAlert("Same Player", "Player 2 must be different from Player 1.", "OK");
                    return;
                }
                frameRow.AwayPlayer2Id = playerId;
                frameRow.AwayPlayer2Name = playerName;
                if (frameRow.AwayPlayerLabel != null)
                {
                    frameRow.AwayPlayerLabel.Text = frameRow.FormatAwayLabel();
                    frameRow.AwayPlayerLabel.TextColor = Color.FromArgb("#1E293B");
                    frameRow.AwayPlayerLabel.FontAttributes = FontAttributes.None;
                }
                System.Diagnostics.Debug.WriteLine($"  => Assigned AWAY player 2 to doubles frame {_currentFrameIndex + 1}");

                AdvanceToNextSlot(isHomeTeam);
                UpdateCurrentFrameIndicator();
                UpdatePlayerFrameCounts();
                HighlightCurrentFrame();
                return;
            }

            // Check for duplicate pairing if home player is already set (skip for void)
            if (!isVoid && frameRow.HomePlayerId.HasValue && !FrameResult.IsVoidPlayer(frameRow.HomePlayerId))
            {
                var duplicatePairing = _frameRows.Any(f => 
                    f != frameRow && 
                    f.HomePlayerId == frameRow.HomePlayerId && 
                    f.AwayPlayerId == playerId);

                if (duplicatePairing)
                {
                    var homeName = frameRow.HomePlayerName;
                    System.Diagnostics.Debug.WriteLine($"  => BLOCKED - {homeName} vs {playerName} already played");
                    _ = DisplayAlert("Duplicate Pairing", 
                        $"{homeName} has already played against {playerName} in this match.\n\nNo repeat pairings allowed.", 
                        "OK");
                    return;
                }
            }

            // For doubles: if replacing player 1, clear player 2
            if (frameRow.IsDoubles)
            {
                frameRow.AwayPlayer2Id = null;
                frameRow.AwayPlayer2Name = "";
            }

            frameRow.AwayPlayerId = playerId;
            frameRow.AwayPlayerName = playerName;
            if (frameRow.AwayPlayerLabel != null)
            {
                frameRow.AwayPlayerLabel.Text = frameRow.FormatAwayLabel();
                frameRow.AwayPlayerLabel.TextColor = isVoid ? Color.FromArgb("#EA580C") : Color.FromArgb("#1E293B");
                frameRow.AwayPlayerLabel.FontAttributes = FontAttributes.None;
            }
            System.Diagnostics.Debug.WriteLine($"  => Assigned AWAY player to frame {_currentFrameIndex + 1}");

            // Auto-award win to opponent when voiding
            if (isVoid)
            {
                frameRow.Winner = FrameWinner.Home;
                if (frameRow.AwayScoreBtn != null)
                {
                    frameRow.AwayScoreBtn.Text = "0";
                    frameRow.AwayScoreBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
                    frameRow.AwayScoreBtn.TextColor = Color.FromArgb("#94A3B8");
                }
                if (frameRow.HomeScoreBtn != null)
                {
                    frameRow.HomeScoreBtn.Text = "1";
                    frameRow.HomeScoreBtn.BackgroundColor = Color.FromArgb("#16A34A");
                    frameRow.HomeScoreBtn.TextColor = Colors.White;
                }
                UpdateScoreDisplay();
            }

            // For doubles: don't advance yet if player 2 still needed (unless void)
            if (frameRow.IsDoubles && !isVoid && !frameRow.AwayPlayer2Id.HasValue)
            {
                UpdateCurrentFrameIndicator();
                UpdatePlayerFrameCounts();
                HighlightCurrentFrame();
                return;
            }
        }

        // Phase-aware auto-advance
        AdvanceToNextSlot(isHomeTeam);
        UpdateCurrentFrameIndicator();
        UpdatePlayerFrameCounts();
        HighlightCurrentFrame();
    }

    /// <summary>
    /// Advances to the next empty slot based on the current entry phase.
    /// HomeLineup: steps through all home slots, then transitions to AwayLineup.
    /// AwayLineup: steps through all away slots, then transitions to Results.
    /// Results: no auto-advance (free editing).
    /// </summary>
    private void AdvanceToNextSlot(bool justFilledHome)
    {
        if (_entryPhase == EntryPhase.HomeLineup)
        {
            // Find the next incomplete home slot (from current position forward, then wrap)
            for (int offset = 1; offset <= _frameRows.Count; offset++)
            {
                int idx = (_currentFrameIndex + offset) % _frameRows.Count;
                if (!_frameRows[idx].IsHomeComplete)
                {
                    _currentFrameIndex = idx;
                    _selectingHomePlayer = true;
                    return;
                }
            }
            // All home slots filled — transition to away lineup
            _entryPhase = EntryPhase.AwayLineup;
            _selectingHomePlayer = false;
            // Find first incomplete away slot
            for (int i = 0; i < _frameRows.Count; i++)
            {
                if (!_frameRows[i].IsAwayComplete)
                {
                    _currentFrameIndex = i;
                    return;
                }
            }
            // All away slots also filled — go to results
            _entryPhase = EntryPhase.Results;
            _currentFrameIndex = 0;
        }
        else if (_entryPhase == EntryPhase.AwayLineup)
        {
            // Find the next incomplete away slot
            for (int offset = 1; offset <= _frameRows.Count; offset++)
            {
                int idx = (_currentFrameIndex + offset) % _frameRows.Count;
                if (!_frameRows[idx].IsAwayComplete)
                {
                    _currentFrameIndex = idx;
                    _selectingHomePlayer = false;
                    return;
                }
            }
            // All away slots filled — transition to results
            _entryPhase = EntryPhase.Results;
            _currentFrameIndex = 0;
        }
        else // Results phase — advance to next frame after both players + winner set
        {
            var current = _frameRows[_currentFrameIndex];
            if (justFilledHome && !current.AwayPlayerId.HasValue)
            {
                _selectingHomePlayer = false;
            }
            else if (!justFilledHome && !current.HomePlayerId.HasValue)
            {
                _selectingHomePlayer = true;
            }
            else if (_currentFrameIndex < _frameRows.Count - 1)
            {
                _currentFrameIndex++;
                _selectingHomePlayer = true;
            }
        }
    }

    private void UpdatePlayerFrameCounts()
    {
        // Count how many frames each player is assigned to
        var homeCounts = new Dictionary<Guid, int>();
        var awayCounts = new Dictionary<Guid, int>();

        foreach (var frame in _frameRows)
        {
            if (frame.HomePlayerId.HasValue && !FrameResult.IsVoidPlayer(frame.HomePlayerId))
            {
                homeCounts.TryGetValue(frame.HomePlayerId.Value, out int count);
                homeCounts[frame.HomePlayerId.Value] = count + 1;
            }
            if (frame.AwayPlayerId.HasValue && !FrameResult.IsVoidPlayer(frame.AwayPlayerId))
            {
                awayCounts.TryGetValue(frame.AwayPlayerId.Value, out int count);
                awayCounts[frame.AwayPlayerId.Value] = count + 1;
            }
        }

        // Update home players
        foreach (var player in _homePlayers)
        {
            player.FrameCount = homeCounts.GetValueOrDefault(player.Id, 0);
        }
        
        // Update away players
        foreach (var player in _awayPlayers)
        {
            player.FrameCount = awayCounts.GetValueOrDefault(player.Id, 0);
        }

        // Update the count labels and visual state in quick panels
        foreach (var child in HomePlayersQuickPanel.Children)
        {
            if (child is Border border && border.BindingContext is PlayerListItem item)
            {
                var grid = border.Content as Grid;
                if (grid != null && grid.Children.Count >= 3)
                {
                    var countLabel = grid.Children[2] as Label;
                    if (countLabel != null)
                    {
                        countLabel.Text = $"({item.FrameCount})";
                        countLabel.TextColor = item.FrameCount >= 3 ? Color.FromArgb("#EF4444") : Color.FromArgb("#94A3B8");
                    }

                    var nameLabel = grid.Children[1] as Label;
                    if (nameLabel != null)
                    {
                        nameLabel.TextColor = item.FrameCount >= 3 ? Color.FromArgb("#94A3B8") : Color.FromArgb("#1E293B");
                    }
                }

                border.Opacity = item.FrameCount >= 3 ? 0.45 : 1.0;
            }
        }

        foreach (var child in AwayPlayersQuickPanel.Children)
        {
            if (child is Border border && border.BindingContext is PlayerListItem item)
            {
                var grid = border.Content as Grid;
                if (grid != null && grid.Children.Count >= 3)
                {
                    var countLabel = grid.Children[2] as Label;
                    if (countLabel != null)
                    {
                        countLabel.Text = $"({item.FrameCount})";
                        countLabel.TextColor = item.FrameCount >= 3 ? Color.FromArgb("#EF4444") : Color.FromArgb("#94A3B8");
                    }

                    var nameLabel = grid.Children[1] as Label;
                    if (nameLabel != null)
                    {
                        nameLabel.TextColor = item.FrameCount >= 3 ? Color.FromArgb("#94A3B8") : Color.FromArgb("#1E293B");
                    }
                }

                border.Opacity = item.FrameCount >= 3 ? 0.45 : 1.0;
            }
        }
    }

    // ========== KEYBOARD SHORTCUTS ==========

    private void RefocusKeyboardCapture()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await System.Threading.Tasks.Task.Delay(50);
            KeyboardCaptureEntry?.Focus();
        });
    }

    private void OnKeyboardInput(object? sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.NewTextValue))
            return;

        // Get the last character typed
        var key = e.NewTextValue.Length > 0 ? e.NewTextValue[^1].ToString() : "";
        
        if (!string.IsNullOrEmpty(key))
        {
            HandleKeyPress(key);
        }

        // Clear the entry for next input
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (KeyboardCaptureEntry != null)
                KeyboardCaptureEntry.Text = "";
        });
    }

    private void SetupKeyboardShortcuts()
    {
        _keyMappings.Clear();
        
        // Home players: 1, 2, 3, 4, 5, ...
        for (int i = 0; i < _homePlayers.Count && i < 9; i++)
        {
            _keyMappings[(i + 1).ToString()] = (i, true);
        }
        
        // Away players: 6, 7, 8, 9, 0, A, B, C, ...
        for (int i = 0; i < _awayPlayers.Count; i++)
        {
            string key;
            if (i < 4) key = (i + 6).ToString();      // 6, 7, 8, 9
            else if (i == 4) key = "0";                // 0
            else key = ((char)('A' + i - 5)).ToString(); // A, B, C...
            _keyMappings[key] = (i, false);
        }
        
        System.Diagnostics.Debug.WriteLine($"Keyboard shortcuts: {_keyMappings.Count} mappings");
    }

    /// <summary>
    /// Handle keyboard input for quick player selection
    /// Keys 1-5: Home players, Keys 6-9,0: Away players
    /// H: Home win, J: Away win, Arrow keys: navigate frames
    /// </summary>
    public void HandleKeyPress(string key)
    {
        if (_selectedFixture == null || _frameRows.Count == 0)
            return;

        var upperKey = key.ToUpperInvariant();
        System.Diagnostics.Debug.WriteLine($"KEY: '{upperKey}'");

        // Navigation: Up/Down arrow or W/S
        if (upperKey == "UP" || upperKey == "W")
        {
            if (_currentFrameIndex > 0)
            {
                _currentFrameIndex--;
                _selectingHomePlayer = true;
                UpdateCurrentFrameIndicator();
                HighlightCurrentFrame();
            }
            return;
        }
        
        if (upperKey == "DOWN" || upperKey == "S")
        {
            if (_currentFrameIndex < _frameRows.Count - 1)
            {
                _currentFrameIndex++;
                _selectingHomePlayer = true;
                UpdateCurrentFrameIndicator();
                HighlightCurrentFrame();
            }
            return;
        }

        // Toggle home/away
        if (upperKey == "TAB" || upperKey == " ")
        {
            _selectingHomePlayer = !_selectingHomePlayer;
            UpdateCurrentFrameIndicator();
            return;
        }

        // Set winner: H = Home, J = Away
        if (upperKey == "H")
        {
            var frame = _frameRows[_currentFrameIndex];
            frame.Winner = FrameWinner.Home;
            UpdateFrameScoreButtons(frame);
            UpdateScoreDisplay();
            AdvanceAfterScore(_currentFrameIndex);
            return;
        }

        if (upperKey == "J")
        {
            var frame = _frameRows[_currentFrameIndex];
            frame.Winner = FrameWinner.Away;
            UpdateFrameScoreButtons(frame);
            UpdateScoreDisplay();
            AdvanceAfterScore(_currentFrameIndex);
            return;
        }

        // Player selection by number/letter
        if (_keyMappings.TryGetValue(upperKey, out var mapping))
        {
            var (index, isHome) = mapping;
            var players = isHome ? _homePlayers : _awayPlayers;
            
            if (index < players.Count)
            {
                var player = players[index];
                System.Diagnostics.Debug.WriteLine($"KEY SELECT: {player.Name} (isHome={isHome})");
                OnQuickPlayerTapped(player.Id, player.Name, isHome);
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

    // ========== SCORE CARD RECOGNITION ==========

    private async System.Threading.Tasks.Task OnScanScoreCardAsync()
    {
        CloseFlyout();

        if (_selectedFixture == null)
        {
            await DisplayAlert($"{Emojis.Warning} No Fixture Selected",
                "Please select a fixture first before scanning a score card.", "OK");
            return;
        }

        // Ask user which OCR engine to use
        var ocrChoice = await DisplayActionSheet(
            "Select OCR Engine",
            "Cancel",
            null,
            $"{Emojis.Lightning} Local OCR - Fast, works offline",
            $"{Emojis.Rocket} Azure Vision - Best for handwriting");

        if (ocrChoice == "Cancel" || string.IsNullOrEmpty(ocrChoice))
            return;

        var useAzure = ocrChoice.Contains("Azure");

        // If Azure selected, check if it's configured
        if (useAzure)
        {
            var azureService = new AzureVisionOcrService();
            if (!azureService.IsConfigured)
            {
                var configure = await DisplayAlert(
                    $"{Emojis.Warning} Azure Vision Not Configured",
                    "Azure Vision requires an endpoint and API key.\n\n" +
                    "Would you like to configure it now, or use local OCR instead?",
                    "Configure", "Use Local");

                if (configure)
                {
                    await ShowAzureConfigDialogAsync();
                    // Re-check after configuration
                    azureService = new AzureVisionOcrService();
                    if (!azureService.IsConfigured)
                    {
                        await DisplayAlert("Not Configured", "Azure Vision was not configured. Using local OCR instead.", "OK");
                        useAzure = false;
                    }
                }
                else
                {
                    useAzure = false;
                }
            }
        }

        // Ask user whether to take photo or pick from gallery
        var choice = await DisplayActionSheet(
            $"{Emojis.Camera} Scan Score Card",
            "Cancel",
            null,
            $"{Emojis.Camera} Take Photo",
            $"{Emojis.Image} Pick from Gallery");

        if (choice == "Cancel" || string.IsNullOrEmpty(choice))
            return;

        try
        {
            FileResult? photo = null;

            if (choice.Contains("Take Photo"))
            {
                // Check camera availability
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    await DisplayAlert($"{Emojis.Error} Not Supported",
                        "Camera capture is not supported on this device.", "OK");
                    return;
                }

                photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = "Take a photo of the score card"
                });
            }
            else if (choice.Contains("Pick from Gallery"))
            {
                photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Select a score card photo"
                });
            }

            if (photo == null)
                return;

            // Log that processing is starting
            var ocrEngine = useAzure ? "Azure Vision" : "Local";
            System.Diagnostics.Debug.WriteLine($"Processing score card image ({photo.FileName}) with {ocrEngine} OCR...");

            // Read the image
            using var stream = await photo.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var imageData = memoryStream.ToArray();

            ScoreCardRecognitionService.RecognitionResult? result = null;

            if (useAzure)
            {
                // Use Azure Vision OCR
                var azureService = new AzureVisionOcrService();
                var azureResult = await azureService.RecognizeTextAsync(imageData);

                if (azureResult.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"Azure OCR returned {azureResult.Lines.Count} lines, confidence: {azureResult.AverageConfidence:P0}");
                    System.Diagnostics.Debug.WriteLine($"Raw text:\n{azureResult.AllText}");

                    // Parse the Azure OCR result using the ScoreCardRecognitionService
                    // Pass the fixture's team IDs so it can properly categorize home vs away players
                    var recognitionService = new ScoreCardRecognitionService();
                    result = recognitionService.RecognizeFromOcrText(
                        azureResult.AllText, 
                        imageData,
                        _selectedFixture.HomeTeamId,
                        _selectedFixture.AwayTeamId,
                        _frameRows.Count);
                }
                else
                {
                    // Azure failed - offer to try local OCR
                    var tryLocal = await DisplayAlert(
                        "Azure OCR Failed",
                        $"{azureResult.Error}\n\nWould you like to try local OCR instead?",
                        "Try Local", "Cancel");

                    if (tryLocal)
                    {
                        useAzure = false;
                        var recognitionService = new ScoreCardRecognitionService();
                        result = await recognitionService.RecognizeFromImageAsync(imageData);
                    }
                    else
                    {
                        return;
                    }
                }
            }
            else
            {
                // Use local OCR
                var recognitionService = new ScoreCardRecognitionService();
                result = await recognitionService.RecognizeFromImageAsync(imageData);
            }

            if (result != null && result.Success && result.Frames.Count != 0)
            {
                // Show preview of recognized data and ask for confirmation
                await ShowRecognitionResultsAsync(result, useAzure);
            }            
            else if (result != null)
            {
                // Recognition failed or no frames found - offer manual entry mode
                var message = result.Message;
                if (result.Errors.Count != 0)
                    message += "\n\n" + string.Join("\n", result.Errors);
                if (result.Warnings.Any())
                    message += "\n\n" + string.Join("\n", result.Warnings);

                var manualEntry = await DisplayAlert(
                    $"{Emojis.Warning} Recognition Limited",
                    message + "\n\nWould you like to enter data manually with the image as reference?",
                    "Manual Entry", "Cancel");

                if (manualEntry)
                {
                    await ShowManualEntryModeAsync(imageData);
                }
            }
        }
        catch (PermissionException)
        {
            await DisplayAlert($"{Emojis.Error} Permission Required",
                "Please grant camera/photo access permission in your device settings.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error",
                $"Failed to process score card: {ex.Message}", "OK");
        }
    }

    private async System.Threading.Tasks.Task ShowAzureConfigDialogAsync()
    {
        var endpoint = await DisplayPromptAsync(
            "Azure Vision Endpoint",
            "Enter your Azure Computer Vision endpoint URL:\n(e.g., https://your-resource.cognitiveservices.azure.com)",
            "Next", "Cancel",
            placeholder: "https://your-resource.cognitiveservices.azure.com");

        if (string.IsNullOrWhiteSpace(endpoint))
            return;

        var apiKey = await DisplayPromptAsync(
            "Azure Vision API Key",
            "Enter your Azure Computer Vision API key:",
            "Save", "Cancel",
            placeholder: "Your API key");

        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        var azureService = new AzureVisionOcrService();
        azureService.Configure(endpoint, apiKey);

        // Test the connection
        var (success, message) = await azureService.TestConnectionAsync();
        
        if (success)
        {
            await DisplayAlert("✅ Success", "Azure Vision configured successfully!\n\n" + message, "OK");
        }
        else
        {
            await DisplayAlert("❌ Configuration Error", message + "\n\nPlease check your endpoint and API key.", "OK");
            azureService.ClearConfiguration();
        }
    }

    private async System.Threading.Tasks.Task ShowRecognitionResultsAsync(ScoreCardRecognitionService.RecognitionResult result, bool usedAzure = false)
    {
        // Build preview string
        var sb = new System.Text.StringBuilder();
        var ocrLabel = usedAzure ? "Azure Vision" : "Local";
        sb.AppendLine($"OCR Engine: {ocrLabel}");
        sb.AppendLine($"Strategy: {result.ParsingStrategy}");
        sb.AppendLine($"Confidence: {result.Confidence:P0}");
        sb.AppendLine($"Score: {result.HomeScore} - {result.AwayScore}");
        sb.AppendLine();
        sb.AppendLine("Recognized Frames:");
        
        foreach (var frame in result.Frames.Take(5))
        {
            var homePlayer = frame.HomePlayerName ?? "?";
            var awayPlayer = frame.AwayPlayerName ?? "?";
            var homeMatched = frame.MatchedHomePlayerId.HasValue ? "?" : "?";
            var awayMatched = frame.MatchedAwayPlayerId.HasValue ? "?" : "?";
            var winner = frame.Winner == FrameWinner.Home ? "H" : frame.Winner == FrameWinner.Away ? "A" : "-";
            sb.AppendLine($"  {frame.FrameNumber}. {homePlayer}({homeMatched}) vs {awayPlayer}({awayMatched}) [{winner}]");
        }

        if (result.Frames.Count > 5)
        {
            sb.AppendLine($"  ... and {result.Frames.Count - 5} more frames");
        }

        if (result.Warnings.Count != 0)
        {
            sb.AppendLine();
            sb.AppendLine("Warnings:");
            foreach (var warning in result.Warnings.Take(3))
            {
                sb.AppendLine($"  - {warning}");
            }
        }

        // Show action sheet with options
        var action = await DisplayActionSheet(
            $"Score Card Recognized ({result.ParsingStrategy})",
            "Cancel",
            null,
            $"{Emojis.Success} Apply Results ({result.HomeScore}-{result.AwayScore})",
            $"{Emojis.Document} Show Raw OCR Text",
            $"{Emojis.Copy} Copy OCR to Clipboard");

        if (action == null || action == "Cancel")
            return;

        if (action.Contains("Apply"))
        {
            ApplyRecognitionResults(result);
        }
        else if (action.Contains("Raw OCR"))
        {
            await ShowRawOcrTextAsync(result.RawOcrText ?? "No OCR text available");
        }
        else if (action.Contains("Clipboard"))
        {
            if (!string.IsNullOrEmpty(result.RawOcrText))
            {
                await Clipboard.Default.SetTextAsync(result.RawOcrText);
                await DisplayAlert("Copied", "OCR text copied to clipboard", "OK");
            }
        }
    }

    private async System.Threading.Tasks.Task ShowRawOcrTextAsync(string ocrText)
    {
        // Show the raw OCR output in a scrollable dialog
        var lines = ocrText.Split('\n');
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Total Lines: {lines.Length}");
        sb.AppendLine("=========================");
        
        for (int i = 0; i < lines.Length; i++)
        {
            sb.AppendLine($"[{i + 1:D2}] {lines[i]}");
        }

        await DisplayAlert("Raw OCR Output", sb.ToString(), "OK");
    }

    private void ApplyRecognitionResults(ScoreCardRecognitionService.RecognitionResult result)
    {
        if (_selectedFixture == null)
            return;

        // Apply each recognized frame to the UI
        foreach (var recognized in result.Frames)
        {
            if (recognized.FrameNumber < 1 || recognized.FrameNumber > _frameRows.Count)
                continue;

            var frameRow = _frameRows[recognized.FrameNumber - 1];

            // Apply home player if matched
            if (recognized.MatchedHomePlayerId.HasValue)
            {
                frameRow.HomePlayerId = recognized.MatchedHomePlayerId;
                frameRow.HomePlayerName = recognized.HomePlayerName ?? "";
                if (frameRow.HomePlayerLabel != null)
                {
                    frameRow.HomePlayerLabel.Text = recognized.HomePlayerName ?? "Unknown";
                    frameRow.HomePlayerLabel.TextColor = Color.FromArgb("#1E293B");
                    frameRow.HomePlayerLabel.FontAttributes = FontAttributes.None;
                }
            }
            else if (!string.IsNullOrWhiteSpace(recognized.HomePlayerName))
            {
                // Name recognized but not matched - show it anyway
                frameRow.HomePlayerName = recognized.HomePlayerName;
                if (frameRow.HomePlayerLabel != null)
                {
                    frameRow.HomePlayerLabel.Text = recognized.HomePlayerName + " (?)";
                    frameRow.HomePlayerLabel.TextColor = Color.FromArgb("#FF9800"); // Orange for unmatched
                }
            }

            // Apply away player if matched
            if (recognized.MatchedAwayPlayerId.HasValue)
            {
                frameRow.AwayPlayerId = recognized.MatchedAwayPlayerId;
                frameRow.AwayPlayerName = recognized.AwayPlayerName ?? "";
                if (frameRow.AwayPlayerLabel != null)
                {
                    frameRow.AwayPlayerLabel.Text = recognized.AwayPlayerName ?? "Unknown";
                    frameRow.AwayPlayerLabel.TextColor = Color.FromArgb("#1E293B");
                    frameRow.AwayPlayerLabel.FontAttributes = FontAttributes.None;
                }
            }
            else if (!string.IsNullOrWhiteSpace(recognized.AwayPlayerName))
            {
                frameRow.AwayPlayerName = recognized.AwayPlayerName;
                if (frameRow.AwayPlayerLabel != null)
                {
                    frameRow.AwayPlayerLabel.Text = recognized.AwayPlayerName + " (?)";
                    frameRow.AwayPlayerLabel.TextColor = Color.FromArgb("#FF9800");
                }
            }

            // Apply winner
            frameRow.Winner = recognized.Winner;
            UpdateFrameScoreButtons(frameRow);

            // Apply 8-ball
            frameRow.EightBall = recognized.EightBall;
            if (frameRow.EightBallCheck != null)
            {
                frameRow.EightBallCheck.IsChecked = recognized.EightBall;
            }
        }

        UpdatePlayerFrameCounts();
        UpdateScoreDisplay();
        
        // Show confirmation
        _ = DisplayAlert($"{Emojis.Success} Applied",
            $"Applied {result.Frames.Count} frames from score card.\n\nPlease review and correct any errors before saving.",
            "OK");
    }

    private void UpdateFrameScoreButtons(FrameRowData frameRow)
    {
        if (frameRow.HomeScoreBtn == null || frameRow.AwayScoreBtn == null)
            return;

        if (frameRow.Winner == FrameWinner.Home)
        {
            frameRow.HomeScoreBtn.Text = "1";
            frameRow.HomeScoreBtn.BackgroundColor = Color.FromArgb("#16A34A");
            frameRow.HomeScoreBtn.TextColor = Colors.White;
            frameRow.AwayScoreBtn.Text = "0";
            frameRow.AwayScoreBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
            frameRow.AwayScoreBtn.TextColor = Color.FromArgb("#94A3B8");
        }
        else if (frameRow.Winner == FrameWinner.Away)
        {
            frameRow.HomeScoreBtn.Text = "0";
            frameRow.HomeScoreBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
            frameRow.HomeScoreBtn.TextColor = Color.FromArgb("#94A3B8");
            frameRow.AwayScoreBtn.Text = "1";
            frameRow.AwayScoreBtn.BackgroundColor = Color.FromArgb("#DC2626");
            frameRow.AwayScoreBtn.TextColor = Colors.White;
        }
        else
        {
            frameRow.HomeScoreBtn.Text = "0";
            frameRow.HomeScoreBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
            frameRow.HomeScoreBtn.TextColor = Color.FromArgb("#94A3B8");
            frameRow.AwayScoreBtn.Text = "0";
            frameRow.AwayScoreBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
            frameRow.AwayScoreBtn.TextColor = Color.FromArgb("#94A3B8");
        }
    }

    private async System.Threading.Tasks.Task ShowManualEntryModeAsync(byte[] imageData)
    {
        // For manual entry mode, we could show the image in a popup
        // For now, just inform the user the image was captured
        
        await DisplayAlert($"{Emojis.Info} Manual Entry Mode",
            "The score card image has been captured.\n\n" +
            "Please manually enter the player names and scores using the controls on this page.\n\n" +
            "Tip: Tap on a player name slot, then tap a player from the side list to assign them.",
            "OK");
        
        // Future enhancement: Show the image in a floating window or split view
        // so the user can reference it while entering data
    }

    // ========== LEFT LIST DATA ==========

    private void RefreshList()
    {
        _items.Clear();

        var data = DataStore.Data;
        if (data == null) return;
        
        // Load divisions into picker if empty or season changed
        var divisions = data.Divisions
            .Where(d => !ActiveSeasonOnly.IsToggled || d.SeasonId == data.ActiveSeasonId)
            .OrderBy(d => d.Name)
            .ToList();
        
        DivisionPicker.ItemsSource = divisions;
        
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

        // Only apply FromDate filter if it's a valid date (not default/minimum)
        if (FromDate != null && FromDate.Date > DateTime.MinValue)
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

        // Sort: upcoming fixtures first (nearest future date at top), then past fixtures (most recent at top)
        var now = DateTime.Now;
        var upcoming = src.Where(f => f.Date >= now).OrderBy(f => f.Date).ToList();
        var past = src.Where(f => f.Date < now).OrderByDescending(f => f.Date).ToList();
        var fixturesList = upcoming.Concat(past).ToList();

        foreach (var f in fixturesList)
        {
            var home = teamById.TryGetValue(f.HomeTeamId, out var ht) ? (ht.Name ?? "Home") : "Home";
            var away = teamById.TryGetValue(f.AwayTeamId, out var at) ? (at.Name ?? "Away") : "Away";

            string subtitle = "";
            if (f.VenueId.HasValue && venueById.TryGetValue(f.VenueId.Value, out var v))
                subtitle = v.Name;

            // Determine completion status
            string statusIcon = "";
            Color statusColor = Colors.Transparent;
            if (f.Frames.Count > 0)
            {
                bool allPlayersSet = f.Frames.All(fr => fr.HomePlayerId.HasValue && fr.AwayPlayerId.HasValue);
                bool allScored = f.Frames.All(fr => fr.Winner != FrameWinner.None);
                bool anyData = f.Frames.Any(fr => fr.HomePlayerId.HasValue || fr.AwayPlayerId.HasValue || fr.Winner != FrameWinner.None);

                if (allPlayersSet && allScored)
                {
                    statusIcon = "✔";
                    statusColor = Color.FromArgb("#16A34A");
                }
                else if (anyData)
                {
                    statusIcon = "/";
                    statusColor = Color.FromArgb("#D97706");
                }
            }

            _items.Add(new FixtureListItem
            {
                Id = f.Id,
                Date = f.Date,
                Title = $"{home} vs {away}",
                Subtitle = subtitle,
                HasReminder = f.Date > DateTime.Now,
                StatusIcon = statusIcon,
                StatusColor = statusColor
            });
        }
    }

    /// <summary>
    /// Re-select the fixture with the given ID after a list refresh and scroll it into view.
    /// </summary>
    private void RestoreSelection(Guid fixtureId)
    {
        var item = _items.FirstOrDefault(i => i.Id == fixtureId);
        if (item != null)
        {
            FixturesList.SelectedItem = item;
            FixturesList.ScrollTo(item, ScrollToPosition.Center, animate: false);
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
        
        // Focus keyboard capture for quick entry (with small delay to ensure UI is ready)
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await System.Threading.Tasks.Task.Delay(100);
            KeyboardCaptureEntry?.Focus();
        });
    }

    // ========== SCORECARD BUILDING ==========

    private void ClearScorecard()
    {
        HomeScorecardHost.Children.Clear();
        AwayScorecardHost.Children.Clear();
        _frameRows.Clear();
        _homePlayers.Clear();
        _awayPlayers.Clear();
        HomePlayersQuickPanel.Children.Clear();
        AwayPlayersQuickPanel.Children.Clear();
        _currentFrameIndex = 0;
        _selectingHomePlayer = true;
        _entryPhase = EntryPhase.HomeLineup;
        CurrentFrameIndicator.IsVisible = false;
        PhaseIndicator.IsVisible = false;
        
        HomeTeamHeader.Text = "Home Team";
        AwayTeamHeader.Text = "Away";
        HomeTeamListHeader.Text = "HOME PLAYERS";
        AwayTeamListHeader.Text = "AWAY PLAYERS";
        DivisionLabel.Text = "Div.";
        DateLabel.Text = "Date";
        ScoreLbl.Text = "";
        HeaderLbl.Text = "Select a fixture";

        // Reset late card UI
        LateCardPanel.IsVisible = false;
        HomeLatePenaltyLbl.Text = "0";
        AwayLatePenaltyLbl.Text = "0";
        LatePenaltySummary.Text = "";
        LateCardBtn.BackgroundColor = Color.FromArgb("#D97706");

        // Reset cancellation UI
        CancelPanel.IsVisible = false;
        CancelPenaltyLbl.Text = "0";
        CancelSummary.Text = "";
        CancelHomeBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
        CancelHomeBtn.TextColor = Color.FromArgb("#1E293B");
        CancelAwayBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
        CancelAwayBtn.TextColor = Color.FromArgb("#1E293B");
        CancelMatchBtn.BackgroundColor = Color.FromArgb("#991B1B");
    }

    private void BuildScorecard()
    {
        if (_selectedFixture == null) return;

        HomeScorecardHost.Children.Clear();
        AwayScorecardHost.Children.Clear();
        _frameRows.Clear();
        _currentFrameIndex = 0;
        _selectingHomePlayer = true;
        _entryPhase = EntryPhase.HomeLineup;

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
        HomeLateLabel.Text = homeTeam?.Name ?? "Home";
        AwayLateLabel.Text = awayTeam?.Name ?? "Away";
        DivisionLabel.Text = $"Div: {division?.Name ?? "?"}";
        DateLabel.Text = _selectedFixture.Date.ToString("ddd dd MMM yyyy");

        // Load player lists
        LoadPlayerLists();

        // Determine frame count - priority order:
        // 1. Season's doubles config (SinglesFrameCount + DoublesFrameCount)
        // 2. Season's FramesPerMatch (if explicitly set > 0)
        // 3. App Settings DefaultFramesPerMatch (if > 0)
        // 4. Default to 15 (WDPL standard)
        int frameCount = 15; // Ultimate default
        int singlesCount = 0;
        int doublesCount = 0;
        var season = data.Seasons.FirstOrDefault(s => s.Id == _selectedFixture.SeasonId);

        // First try settings
        if (data.Settings.DefaultFramesPerMatch > 0)
            frameCount = data.Settings.DefaultFramesPerMatch;

        // Then override with season-specific value if set
        if (season != null && season.FramesPerMatch > 0) 
            frameCount = season.FramesPerMatch;

        // If doubles enabled, use separate counts
        if (season != null && season.IncludeDoubles && (season.SinglesFrameCount > 0 || season.DoublesFrameCount > 0))
        {
            singlesCount = season.SinglesFrameCount;
            doublesCount = season.DoublesFrameCount;
            frameCount = singlesCount + doublesCount;
        }

        System.Diagnostics.Debug.WriteLine($"BuildScorecard: Using {frameCount} frames (singles={singlesCount}, doubles={doublesCount})");
        System.Diagnostics.Debug.WriteLine($"  Season '{season?.Name}' FramesPerMatch: {season?.FramesPerMatch ?? 0}");
        System.Diagnostics.Debug.WriteLine($"  Settings DefaultFramesPerMatch: {data.Settings.DefaultFramesPerMatch}");

        // Ensure fixture has enough frames
        while (_selectedFixture.Frames.Count < frameCount)
            _selectedFixture.Frames.Add(new FrameResult { Number = _selectedFixture.Frames.Count + 1 });
        if (_selectedFixture.Frames.Count > frameCount)
            _selectedFixture.Frames = _selectedFixture.Frames.Take(frameCount).ToList();

        // Mark doubles frames on the fixture's FrameResult list
        if (doublesCount > 0)
        {
            for (int i = 0; i < frameCount; i++)
            {
                _selectedFixture.Frames[i].IsDoubles = (i >= singlesCount);
            }
        }

        // Build frame rows
        for (int i = 0; i < frameCount; i++)
        {
            var fr = _selectedFixture.Frames[i];
            var frameRow = CreateFrameRow(i, fr);
            _frameRows.Add(frameRow);
            if (frameRow.HomeRowBorder != null)
                HomeScorecardHost.Children.Add(frameRow.HomeRowBorder);
            if (frameRow.AwayRowBorder != null)
                AwayScorecardHost.Children.Add(frameRow.AwayRowBorder);
        }

        // Add divider between singles and doubles sections
        if (doublesCount > 0 && singlesCount > 0)
        {
            var homeCount = HomeScorecardHost.Children.Count;
            var awayCount = AwayScorecardHost.Children.Count;
            var dividerIndex = homeCount >= singlesCount ? singlesCount : homeCount;

            var homeDivider = CreateSetDivider($"── DOUBLES ({doublesCount}) ──");
            var awayDivider = CreateSetDivider($"── DOUBLES ({doublesCount}) ──");

            if (dividerIndex <= homeCount)
                HomeScorecardHost.Children.Insert(dividerIndex, homeDivider);
            if (dividerIndex <= awayCount)
                AwayScorecardHost.Children.Insert(dividerIndex, awayDivider);
        }
        // Add divider after frame 10 (if there are 15 frames like in the WDPL card and no explicit doubles split)
        else if (frameCount > 10 && doublesCount == 0)
        {
            var homeCount = HomeScorecardHost.Children.Count;
            var awayCount = AwayScorecardHost.Children.Count;
            var dividerIndex = homeCount >= 10 ? 10 : homeCount;

            var homeDivider = CreateSetDivider("── 11–15 ──");
            var awayDivider = CreateSetDivider("── 11–15 ──");

            if (dividerIndex <= homeCount)
                HomeScorecardHost.Children.Insert(dividerIndex, homeDivider);
            if (dividerIndex <= awayCount)
                AwayScorecardHost.Children.Insert(dividerIndex, awayDivider);
        }

        // Detect the correct starting phase based on existing frame data
        DetectEntryPhase();

        // Show frame indicator
        UpdateCurrentFrameIndicator();
        HighlightCurrentFrame();

        // Load late card data
        LoadLatePenaltyUI();

        // Load cancellation data
        LoadCancelUI();
    }

    /// <summary>
    /// Detects the correct entry phase based on which slots are already filled.
    /// Positions the cursor at the first empty slot in that phase.
    /// </summary>
    private void DetectEntryPhase()
    {
        // Check if all home/away slots are complete (including doubles player 2)
        bool allHomeFilled = _frameRows.All(f => f.IsHomeComplete);
        bool allAwayFilled = _frameRows.All(f => f.IsAwayComplete);

        if (!allHomeFilled)
        {
            _entryPhase = EntryPhase.HomeLineup;
            _selectingHomePlayer = true;
            _currentFrameIndex = _frameRows.FindIndex(f => !f.IsHomeComplete);
            if (_currentFrameIndex < 0) _currentFrameIndex = 0;
        }
        else if (!allAwayFilled)
        {
            _entryPhase = EntryPhase.AwayLineup;
            _selectingHomePlayer = false;
            _currentFrameIndex = _frameRows.FindIndex(f => !f.IsAwayComplete);
            if (_currentFrameIndex < 0) _currentFrameIndex = 0;
        }
        else
        {
            _entryPhase = EntryPhase.Results;
            // Position at first frame without a winner
            _currentFrameIndex = _frameRows.FindIndex(f => f.Winner == FrameWinner.None);
            if (_currentFrameIndex < 0) _currentFrameIndex = 0;
        }
    }

    private void LoadPlayerLists()
    {
        _homePlayers.Clear();
        _awayPlayers.Clear();
        HomePlayersQuickPanel.Children.Clear();
        AwayPlayersQuickPanel.Children.Clear();

        if (_selectedFixture == null) return;

        var data = DataStore.Data;

        // Auto-repair: assign TeamId for unassigned players in this season
        // This fixes players created by season copy with TeamId = null
        var seasonId = _selectedFixture.SeasonId;
        var unassignedPlayers = data.Players
            .Where(p => p.SeasonId == seasonId && !p.TeamId.HasValue)
            .ToList();

        if (unassignedPlayers.Count > 0)
        {
            var targetTeamsByName = data.Teams
                .Where(t => t.SeasonId == seasonId)
                .GroupBy(t => t.Name?.Trim()?.ToLower() ?? "")
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .ToDictionary(g => g.Key, g => g.First().Id);

            foreach (var player in unassignedPlayers)
            {
                // Find the same player (by name) in other seasons to get their team
                var historicalPlayer = data.Players
                    .Where(p => p.SeasonId != seasonId && p.TeamId.HasValue
                        && string.Equals(p.FirstName?.Trim(), player.FirstName?.Trim(), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.LastName?.Trim(), player.LastName?.Trim(), StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p =>
                    {
                        var s = data.Seasons.FirstOrDefault(s => s.Id == p.SeasonId);
                        return s?.StartDate ?? DateTime.MinValue;
                    })
                    .FirstOrDefault();

                if (historicalPlayer?.TeamId != null)
                {
                    var oldTeam = data.Teams.FirstOrDefault(t => t.Id == historicalPlayer.TeamId.Value);
                    if (oldTeam != null
                        && !string.IsNullOrWhiteSpace(oldTeam.Name)
                        && targetTeamsByName.TryGetValue(oldTeam.Name.Trim().ToLower(), out var newTeamId))
                    {
                        player.TeamId = newTeamId;
                        System.Diagnostics.Debug.WriteLine($"  Auto-assigned '{player.FirstName} {player.LastName}' to team '{oldTeam.Name}'");
                    }
                }
            }
        }

        // Get home team players
        var homePlayers = data.Players
            .Where(p => p.TeamId == _selectedFixture.HomeTeamId)
            .OrderBy(p => p.LastName ?? "")
            .ThenBy(p => p.FirstName ?? "")
            .ToList();

        // Create quick key buttons for home players [1], [2], [3], [4], [5], ...
        for (int i = 0; i < homePlayers.Count; i++)
        {
            var player = homePlayers[i];
            var keyLabel = (i + 1).ToString(); // 1, 2, 3, 4, 5...

            var listItem = new PlayerListItem
            {
                Id = player.Id,
                Name = player.FullName ?? $"{player.FirstName} {player.LastName}".Trim(),
                FrameCount = 0
            };
            _homePlayers.Add(listItem);

            var btn = CreateQuickPlayerButton(keyLabel, player, listItem, true);
            HomePlayersQuickPanel.Children.Add(btn);
        }

        // Add VOID button for home team
        HomePlayersQuickPanel.Children.Add(CreateVoidButton(true));

        // Get away team players
        var awayPlayers = data.Players
            .Where(p => p.TeamId == _selectedFixture.AwayTeamId)
            .OrderBy(p => p.LastName ?? "")
            .ThenBy(p => p.FirstName ?? "")
            .ToList();

        // Create quick key buttons for away players [6], [7], [8], [9], [0], ...
        for (int i = 0; i < awayPlayers.Count; i++)
        {
            var player = awayPlayers[i];
            string keyLabel;
            if (i < 4) keyLabel = (i + 6).ToString();      // 6, 7, 8, 9
            else if (i == 4) keyLabel = "0";                // 0
            else keyLabel = ((char)('A' + i - 5)).ToString(); // A, B, C...
            
            var listItem = new PlayerListItem
            {
                Id = player.Id,
                Name = player.FullName ?? $"{player.FirstName} {player.LastName}".Trim(),
                FrameCount = 0
            };
            _awayPlayers.Add(listItem);

            var btn = CreateQuickPlayerButton(keyLabel, player, listItem, false);
            AwayPlayersQuickPanel.Children.Add(btn);
        }

        // Add VOID button for away team
        AwayPlayersQuickPanel.Children.Add(CreateVoidButton(false));

        // Update counts based on existing frame data
        UpdatePlayerFrameCounts();
        
        // Setup keyboard shortcuts for the loaded players
        SetupKeyboardShortcuts();
    }

    private Border CreateQuickPlayerButton(string keyLabel, Player player, PlayerListItem listItem, bool isHome)
    {
        var playerId = player.Id;
        var playerName = player.FullName ?? $"{player.FirstName} {player.LastName}".Trim();

        // Check availability for this fixture date
        var availabilityIcon = "";
        if (_selectedFixture != null)
        {
            var matchDate = _selectedFixture.Date.Date;
            var avail = player.Availability.FirstOrDefault(a => a.Date.Date == matchDate);
            if (avail != null)
            {
                availabilityIcon = avail.Status switch
                {
                    AvailabilityStatus.Available => " ✅",
                    AvailabilityStatus.Unavailable => " ❌",
                    AvailabilityStatus.Maybe => " ❓",
                    _ => ""
                };
            }
        }

        var border = new Border
        {
            BackgroundColor = Colors.White,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Stroke = isHome 
                ? Color.FromArgb("#3B82F6") 
                : Color.FromArgb("#EF4444"),
            StrokeThickness = 1,
            Padding = new Thickness(8, 5),
            Margin = new Thickness(0, 0, 0, 2),
            BindingContext = listItem
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(20) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 5
        };

        // Key badge
        var keyBadge = new Border
        {
            BackgroundColor = isHome 
                ? Color.FromArgb("#2563EB") 
                : Color.FromArgb("#DC2626"),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 9 },
            WidthRequest = 18,
            HeightRequest = 18,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        keyBadge.Content = new Label
        {
            Text = keyLabel,
            FontSize = 9,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(keyBadge, 0);
        grid.Children.Add(keyBadge);

        // Player name + availability
        var nameLabel = new Label
        {
            Text = playerName + availabilityIcon,
            FontSize = 12,
            TextColor = Color.FromArgb("#1E293B"),
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        Grid.SetColumn(nameLabel, 1);
        grid.Children.Add(nameLabel);

        // Frame count
        var countLabel = new Label
        {
            Text = "(0)",
            FontSize = 10,
            TextColor = Color.FromArgb("#94A3B8"),
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(countLabel, 2);
        grid.Children.Add(countLabel);

        border.Content = grid;

        var tapGesture = new TapGestureRecognizer { NumberOfTapsRequired = 1 };
        tapGesture.Tapped += (s, e) => 
        {
            System.Diagnostics.Debug.WriteLine($"TAP: {playerName} (isHome={isHome})");
            OnQuickPlayerTapped(playerId, playerName, isHome);
            RefocusKeyboardCapture();
        };
        border.GestureRecognizers.Add(tapGesture);

        return border;
    }

    private Border CreateVoidButton(bool isHome)
    {
        var voidId = FrameResult.VoidPlayerId;

        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#FFF7ED"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Stroke = Color.FromArgb("#F97316"),
            StrokeThickness = 1,
            Padding = new Thickness(8, 5),
            Margin = new Thickness(0, 2, 0, 0)
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 4
        };

        var iconLabel = new Label
        {
            Text = "⊘",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#EA580C"),
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(iconLabel, 0);
        grid.Children.Add(iconLabel);

        var nameLabel = new Label
        {
            Text = "VOID",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#EA580C"),
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(nameLabel, 1);
        grid.Children.Add(nameLabel);

        border.Content = grid;

        var tapGesture = new TapGestureRecognizer { NumberOfTapsRequired = 1 };
        tapGesture.Tapped += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"TAP: VOID (isHome={isHome})");
            OnQuickPlayerTapped(voidId, "VOID", isHome);
            RefocusKeyboardCapture();
        };
        border.GestureRecognizers.Add(tapGesture);

        return border;
    }

    private static Border CreateSetDivider(string text)
    {
        var divider = new Border
        {
            BackgroundColor = Color.FromArgb("#1E293B"),
            HeightRequest = 24,
            Padding = new Thickness(4, 3)
        };
        divider.Content = new Label
        {
            Text = text,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FBBF24"),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        return divider;
    }

    private FrameRowData CreateFrameRow(int index, FrameResult fr)
    {
        var data = DataStore.Data;

        // Get existing player names
        string homePlayerName = "";
        string awayPlayerName = "";
        string homePlayer2Name = "";
        string awayPlayer2Name = "";

        if (FrameResult.IsVoidPlayer(fr.HomePlayerId))
        {
            homePlayerName = "VOID";
        }
        else if (fr.HomePlayerId.HasValue)
        {
            var player = data.Players.FirstOrDefault(p => p.Id == fr.HomePlayerId.Value);
            homePlayerName = player?.FullName ?? "";
        }
        if (FrameResult.IsVoidPlayer(fr.AwayPlayerId))
        {
            awayPlayerName = "VOID";
        }
        else if (fr.AwayPlayerId.HasValue)
        {
            var player = data.Players.FirstOrDefault(p => p.Id == fr.AwayPlayerId.Value);
            awayPlayerName = player?.FullName ?? "";
        }

        // Doubles player 2 names
        if (fr.HomePlayer2Id.HasValue)
        {
            var player = data.Players.FirstOrDefault(p => p.Id == fr.HomePlayer2Id.Value);
            homePlayer2Name = player?.FullName ?? "";
        }
        if (fr.AwayPlayer2Id.HasValue)
        {
            var player = data.Players.FirstOrDefault(p => p.Id == fr.AwayPlayer2Id.Value);
            awayPlayer2Name = player?.FullName ?? "";
        }

        var frameRow = new FrameRowData
        {
            FrameNumber = index + 1,
            IsDoubles = fr.IsDoubles,
            HomePlayerId = fr.HomePlayerId,
            HomePlayerName = homePlayerName,
            AwayPlayerId = fr.AwayPlayerId,
            AwayPlayerName = awayPlayerName,
            HomePlayer2Id = fr.HomePlayer2Id,
            HomePlayer2Name = homePlayer2Name,
            AwayPlayer2Id = fr.AwayPlayer2Id,
            AwayPlayer2Name = awayPlayer2Name,
            Winner = fr.Winner,
            EightBall = fr.EightBall
        };

        var bgColor = GetFrameRowBackground(index);

        // ── HOME SIDE: # | [H] | Home Player ──
        var homeRowBorder = new Border
        {
            BackgroundColor = bgColor,
            Padding = new Thickness(0),
            StrokeThickness = 0,
            HeightRequest = 34
        };

        var homeGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(30) },  // Frame #
                new ColumnDefinition { Width = new GridLength(40) },  // Home score
                new ColumnDefinition { Width = GridLength.Star }      // Home player
            },
            Padding = new Thickness(4, 2),
            ColumnSpacing = 2
        };

        // Frame number
        var frameNumLabel = new Label
        {
            Text = (index + 1).ToString(),
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            TextColor = Color.FromArgb("#64748B"),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(frameNumLabel, 0);
        homeGrid.Children.Add(frameNumLabel);

        // Home score button
        var homeScoreBtn = new Button
        {
            Text = fr.Winner == FrameWinner.Home ? "1" : "0",
            BackgroundColor = fr.Winner == FrameWinner.Home ? Color.FromArgb("#16A34A") : Color.FromArgb("#E2E8F0"),
            TextColor = fr.Winner == FrameWinner.Home ? Colors.White : Color.FromArgb("#94A3B8"),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 4,
            Padding = new Thickness(0),
            WidthRequest = 36,
            HeightRequest = 28
        };
        homeScoreBtn.Clicked += (s, e) =>
        {
            var btn = s as Button;
            var awayBtn = frameRow.AwayScoreBtn;
            if (btn == null || awayBtn == null) return;

            if (frameRow.Winner == FrameWinner.Home)
            {
                // Already home win → swap to away win
                btn.Text = "0";
                btn.BackgroundColor = Color.FromArgb("#E2E8F0");
                btn.TextColor = Color.FromArgb("#94A3B8");
                awayBtn.Text = "1";
                awayBtn.BackgroundColor = Color.FromArgb("#DC2626");
                awayBtn.TextColor = Colors.White;
                frameRow.Winner = FrameWinner.Away;
            }
            else
            {
                // No winner or away win → set home win
                btn.Text = "1";
                btn.BackgroundColor = Color.FromArgb("#16A34A");
                btn.TextColor = Colors.White;
                awayBtn.Text = "0";
                awayBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
                awayBtn.TextColor = Color.FromArgb("#94A3B8");
                frameRow.Winner = FrameWinner.Home;
            }
            UpdateScoreDisplay();
            AdvanceAfterScore(index);
            RefocusKeyboardCapture();
        };
        Grid.SetColumn(homeScoreBtn, 1);
        homeGrid.Children.Add(homeScoreBtn);

        // Home player name (tappable)
        bool homeIsVoid = FrameResult.IsVoidPlayer(fr.HomePlayerId);
        var homeDisplayText = frameRow.FormatHomeLabel();
        bool homeIsEmpty = !fr.HomePlayerId.HasValue;
        var homePlayerLabel = new Label
        {
            Text = homeDisplayText,
            TextColor = homeIsVoid ? Color.FromArgb("#EA580C") 
                       : homeIsEmpty ? Color.FromArgb("#94A3B8") 
                       : Color.FromArgb("#1E293B"),
            FontSize = fr.IsDoubles ? 11 : 13,
            FontAttributes = homeIsEmpty ? FontAttributes.Italic : FontAttributes.None,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        var homePlayerBorder = new Border
        {
            BackgroundColor = fr.IsDoubles ? Color.FromArgb("#F0FDF4") : Color.FromArgb("#EFF6FF"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
            Stroke = fr.IsDoubles ? Color.FromArgb("#BBF7D0") : Color.FromArgb("#BFDBFE"),
            StrokeThickness = 1,
            Padding = new Thickness(6, 3),
            Content = homePlayerLabel
        };
        var homeTap = new TapGestureRecognizer();
        homeTap.Tapped += (s, e) =>
        {
            _currentFrameIndex = index;
            _selectingHomePlayer = true;
            if (_entryPhase != EntryPhase.HomeLineup)
                _entryPhase = EntryPhase.Results;
            UpdateCurrentFrameIndicator();
            HighlightCurrentFrame();
            RefocusKeyboardCapture();
        };
        homePlayerBorder.GestureRecognizers.Add(homeTap);
        Grid.SetColumn(homePlayerBorder, 2);
        homeGrid.Children.Add(homePlayerBorder);

        homeRowBorder.Content = homeGrid;

        // ── AWAY SIDE: Away Player | [A] | 8 ──
        var awayRowBorder = new Border
        {
            BackgroundColor = bgColor,
            Padding = new Thickness(0),
            StrokeThickness = 0,
            HeightRequest = 34
        };

        var awayGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },     // Away player
                new ColumnDefinition { Width = new GridLength(40) },  // Away score
                new ColumnDefinition { Width = new GridLength(30) }   // 8-ball
            },
            Padding = new Thickness(4, 2),
            ColumnSpacing = 2
        };

        // Away player name (tappable)
        bool awayIsVoid = FrameResult.IsVoidPlayer(fr.AwayPlayerId);
        var awayDisplayText = frameRow.FormatAwayLabel();
        bool awayIsEmpty = !fr.AwayPlayerId.HasValue;
        var awayPlayerLabel = new Label
        {
            Text = awayDisplayText,
            TextColor = awayIsVoid ? Color.FromArgb("#EA580C") 
                       : awayIsEmpty ? Color.FromArgb("#94A3B8") 
                       : Color.FromArgb("#1E293B"),
            FontSize = fr.IsDoubles ? 11 : 13,
            FontAttributes = awayIsEmpty ? FontAttributes.Italic : FontAttributes.None,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        var awayPlayerBorder = new Border
        {
            BackgroundColor = fr.IsDoubles ? Color.FromArgb("#FFF7ED") : Color.FromArgb("#FEF2F2"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
            Stroke = fr.IsDoubles ? Color.FromArgb("#FED7AA") : Color.FromArgb("#FECACA"),
            StrokeThickness = 1,
            Padding = new Thickness(6, 3),
            Content = awayPlayerLabel
        };
        var awayTap = new TapGestureRecognizer();
        awayTap.Tapped += (s, e) =>
        {
            _currentFrameIndex = index;
            _selectingHomePlayer = false;
            if (_entryPhase != EntryPhase.AwayLineup)
                _entryPhase = EntryPhase.Results;
            UpdateCurrentFrameIndicator();
            HighlightCurrentFrame();
            RefocusKeyboardCapture();
        };
        awayPlayerBorder.GestureRecognizers.Add(awayTap);
        Grid.SetColumn(awayPlayerBorder, 0);
        awayGrid.Children.Add(awayPlayerBorder);

        // Away score button
        var awayScoreBtn = new Button
        {
            Text = fr.Winner == FrameWinner.Away ? "1" : "0",
            BackgroundColor = fr.Winner == FrameWinner.Away ? Color.FromArgb("#DC2626") : Color.FromArgb("#E2E8F0"),
            TextColor = fr.Winner == FrameWinner.Away ? Colors.White : Color.FromArgb("#94A3B8"),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 4,
            Padding = new Thickness(0),
            WidthRequest = 36,
            HeightRequest = 28
        };
        awayScoreBtn.Clicked += (s, e) =>
        {
            var btn = s as Button;
            var homeBtn = frameRow.HomeScoreBtn;
            if (btn == null || homeBtn == null) return;

            if (frameRow.Winner == FrameWinner.Away)
            {
                // Already away win → swap to home win
                btn.Text = "0";
                btn.BackgroundColor = Color.FromArgb("#E2E8F0");
                btn.TextColor = Color.FromArgb("#94A3B8");
                homeBtn.Text = "1";
                homeBtn.BackgroundColor = Color.FromArgb("#16A34A");
                homeBtn.TextColor = Colors.White;
                frameRow.Winner = FrameWinner.Home;
            }
            else
            {
                // No winner or home win → set away win
                btn.Text = "1";
                btn.BackgroundColor = Color.FromArgb("#DC2626");
                btn.TextColor = Colors.White;
                homeBtn.Text = "0";
                homeBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
                homeBtn.TextColor = Color.FromArgb("#94A3B8");
                frameRow.Winner = FrameWinner.Away;
            }
            UpdateScoreDisplay();
            AdvanceAfterScore(index);
            RefocusKeyboardCapture();
        };
        Grid.SetColumn(awayScoreBtn, 1);
        awayGrid.Children.Add(awayScoreBtn);

        // 8-ball checkbox
        var eightBallCheck = new CheckBox
        {
            IsChecked = fr.EightBall,
            Color = Color.FromArgb("#F97316"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Scale = 0.85
        };
        eightBallCheck.CheckedChanged += (s, e) =>
        {
            frameRow.EightBall = e.Value;
        };
        Grid.SetColumn(eightBallCheck, 2);
        awayGrid.Children.Add(eightBallCheck);

        awayRowBorder.Content = awayGrid;

        // Store references
        frameRow.HomeRowBorder = homeRowBorder;
        frameRow.AwayRowBorder = awayRowBorder;
        frameRow.HomePlayerLabel = homePlayerLabel;
        frameRow.AwayPlayerLabel = awayPlayerLabel;
        frameRow.HomeScoreBtn = homeScoreBtn;
        frameRow.AwayScoreBtn = awayScoreBtn;
        frameRow.EightBallCheck = eightBallCheck;

        return frameRow;
    }

    private void UpdateCurrentFrameIndicator()
    {
        if (_selectedFixture == null)
        {
            CurrentFrameIndicator.IsVisible = false;
            PhaseIndicator.IsVisible = false;
            return;
        }

        CurrentFrameIndicator.IsVisible = true;
        PhaseIndicator.IsVisible = true;

        switch (_entryPhase)
        {
            case EntryPhase.HomeLineup:
            {
                int filled = _frameRows.Count(f => f.HomePlayerId.HasValue);
                CurrentFrameLabel.Text = $"Frame {_currentFrameIndex + 1} - Select HOME player ({filled}/{_frameRows.Count})";
                PhaseLabel.Text = $"📋 Phase 1 of 3: Home Lineup";
                PhaseLabel.TextColor = Color.FromArgb("#1976D2");
                break;
            }
            case EntryPhase.AwayLineup:
            {
                int filled = _frameRows.Count(f => f.AwayPlayerId.HasValue);
                CurrentFrameLabel.Text = $"Frame {_currentFrameIndex + 1} - Select AWAY player ({filled}/{_frameRows.Count})";
                PhaseLabel.Text = $"📋 Phase 2 of 3: Away Lineup";
                PhaseLabel.TextColor = Color.FromArgb("#D32F2F");
                break;
            }
            case EntryPhase.Results:
            {
                int scored = _frameRows.Count(f => f.Winner != FrameWinner.None);
                CurrentFrameLabel.Text = $"Frame {_currentFrameIndex + 1} - Enter results ({scored}/{_frameRows.Count})";
                PhaseLabel.Text = $"📋 Phase 3 of 3: Results";
                PhaseLabel.TextColor = Color.FromArgb("#2E7D32");
                break;
            }
        }
    }

    private void HighlightCurrentFrame()
    {
        for (int i = 0; i < _frameRows.Count; i++)
        {
            var row = _frameRows[i];
            var bg = (i == _currentFrameIndex)
                ? Color.FromArgb("#FEF9C3")
                : GetFrameRowBackground(i);

            if (row.HomeRowBorder != null)
                row.HomeRowBorder.BackgroundColor = bg;
            if (row.AwayRowBorder != null)
                row.AwayRowBorder.BackgroundColor = bg;

            // Highlight the active column
            if (row.HomePlayerLabel?.Parent is Border homeBorder)
            {
                bool activeHome = i == _currentFrameIndex && _selectingHomePlayer;
                homeBorder.StrokeThickness = activeHome ? 2.5 : 1;
                homeBorder.Stroke = activeHome ? Color.FromArgb("#2563EB") : Color.FromArgb("#BFDBFE");
            }
            if (row.AwayPlayerLabel?.Parent is Border awayBorder)
            {
                bool activeAway = i == _currentFrameIndex && !_selectingHomePlayer;
                awayBorder.StrokeThickness = activeAway ? 2.5 : 1;
                awayBorder.Stroke = activeAway ? Color.FromArgb("#DC2626") : Color.FromArgb("#FECACA");
            }
        }
    }

    /// <summary>
    /// Returns a background colour for the frame row based on which set of 5 it belongs to.
    /// Set 1 (frames 1-5): white/light grey alternating
    /// Set 2 (frames 6-10): light blue tint alternating
    /// Set 3 (frames 11-15): light green tint alternating
    /// </summary>
    private static Color GetFrameRowBackground(int index)
    {
        int set = index / 5; // 0, 1, or 2
        bool even = index % 2 == 0;

        return set switch
        {
            0 => even ? Colors.White : Color.FromArgb("#F8FAFC"),             // Set 1: white / slate-50
            1 => even ? Color.FromArgb("#EFF6FF") : Color.FromArgb("#DBEAFE"), // Set 2: blue-50 / blue-100
            _ => even ? Color.FromArgb("#F0FDF4") : Color.FromArgb("#DCFCE7"), // Set 3: green-50 / green-100
        };
    }

    private void UpdateScoreDisplay()
    {
        int homeScore = _frameRows.Count(f => f.Winner == FrameWinner.Home);
        int awayScore = _frameRows.Count(f => f.Winner == FrameWinner.Away);
        ScoreLbl.Text = $"{homeScore} - {awayScore}";
    }

    /// <summary>
    /// After setting a winner on a frame, advance to the next unscored frame.
    /// </summary>
    private void AdvanceAfterScore(int scoredIndex)
    {
        if (_entryPhase != EntryPhase.Results) return;

        // Find the next frame without a winner (searching forward, then wrapping)
        for (int offset = 1; offset <= _frameRows.Count; offset++)
        {
            int idx = (scoredIndex + offset) % _frameRows.Count;
            if (_frameRows[idx].Winner == FrameWinner.None)
            {
                _currentFrameIndex = idx;
                UpdateCurrentFrameIndicator();
                HighlightCurrentFrame();
                return;
            }
        }

        // All frames scored
        UpdateCurrentFrameIndicator();
        HighlightCurrentFrame();
    }

    // ========== LATE CARD ==========

    private void ToggleLateCardPanel()
    {
        LateCardPanel.IsVisible = !LateCardPanel.IsVisible;
    }

    private void AdjustLatePenalty(bool isHome, int delta)
    {
        if (_selectedFixture == null) return;

        if (isHome)
        {
            _selectedFixture.HomeLatePenalty = Math.Max(0, _selectedFixture.HomeLatePenalty + delta);
            HomeLatePenaltyLbl.Text = _selectedFixture.HomeLatePenalty.ToString();
        }
        else
        {
            _selectedFixture.AwayLatePenalty = Math.Max(0, _selectedFixture.AwayLatePenalty + delta);
            AwayLatePenaltyLbl.Text = _selectedFixture.AwayLatePenalty.ToString();
        }

        UpdateLatePenaltySummary();
    }

    private void UpdateLatePenaltySummary()
    {
        if (_selectedFixture == null) return;

        var parts = new List<string>();
        if (_selectedFixture.HomeLatePenalty > 0)
            parts.Add($"Home −{_selectedFixture.HomeLatePenalty}pts");
        if (_selectedFixture.AwayLatePenalty > 0)
            parts.Add($"Away −{_selectedFixture.AwayLatePenalty}pts");

        LatePenaltySummary.Text = parts.Count > 0 ? string.Join(", ", parts) : "";

        // Highlight the late card button if any penalty is set
        bool hasAny = _selectedFixture.HomeLatePenalty > 0 || _selectedFixture.AwayLatePenalty > 0;
        LateCardBtn.BackgroundColor = hasAny ? Color.FromArgb("#DC2626") : Color.FromArgb("#D97706");
    }

    private void LoadLatePenaltyUI()
    {
        if (_selectedFixture == null) return;

        HomeLatePenaltyLbl.Text = _selectedFixture.HomeLatePenalty.ToString();
        AwayLatePenaltyLbl.Text = _selectedFixture.AwayLatePenalty.ToString();

        // Show panel automatically if there are existing penalties
        bool hasAny = _selectedFixture.HomeLatePenalty > 0 || _selectedFixture.AwayLatePenalty > 0;
        LateCardPanel.IsVisible = hasAny;

        UpdateLatePenaltySummary();
    }

    // ========== MATCH CANCELLATION ==========

    private void ToggleCancelPanel()
    {
        CancelPanel.IsVisible = !CancelPanel.IsVisible;
    }

    private void SetCancelledBy(FrameWinner team)
    {
        if (_selectedFixture == null) return;

        // Toggle off if already selected
        if (_selectedFixture.CancelledByTeam == team)
        {
            _selectedFixture.CancelledByTeam = FrameWinner.None;
            _selectedFixture.CancellationPenalty = 0;
        }
        else
        {
            _selectedFixture.CancelledByTeam = team;
        }

        UpdateCancelUI();
    }

    private void AdjustCancelPenalty(int delta)
    {
        if (_selectedFixture == null || _selectedFixture.CancelledByTeam == FrameWinner.None) return;

        _selectedFixture.CancellationPenalty = Math.Max(0, _selectedFixture.CancellationPenalty + delta);
        UpdateCancelUI();
    }

    private void UpdateCancelUI()
    {
        if (_selectedFixture == null) return;

        var cancelled = _selectedFixture.CancelledByTeam;
        CancelPenaltyLbl.Text = _selectedFixture.CancellationPenalty.ToString();

        // Highlight selected team button
        CancelHomeBtn.BackgroundColor = cancelled == FrameWinner.Home
            ? Color.FromArgb("#DC2626") : Color.FromArgb("#E2E8F0");
        CancelHomeBtn.TextColor = cancelled == FrameWinner.Home
            ? Colors.White : Color.FromArgb("#1E293B");
        CancelAwayBtn.BackgroundColor = cancelled == FrameWinner.Away
            ? Color.FromArgb("#DC2626") : Color.FromArgb("#E2E8F0");
        CancelAwayBtn.TextColor = cancelled == FrameWinner.Away
            ? Colors.White : Color.FromArgb("#1E293B");

        // Summary text
        if (cancelled != FrameWinner.None)
        {
            var data = DataStore.Data;
            var teamId = cancelled == FrameWinner.Home ? _selectedFixture.HomeTeamId : _selectedFixture.AwayTeamId;
            var teamName = data.Teams.FirstOrDefault(t => t.Id == teamId)?.Name ?? (cancelled == FrameWinner.Home ? "Home" : "Away");
            CancelSummary.Text = _selectedFixture.CancellationPenalty > 0
                ? $"{teamName} −{_selectedFixture.CancellationPenalty}pts"
                : $"{teamName}";
        }
        else
        {
            CancelSummary.Text = "";
        }

        // Highlight button
        bool hasCancel = cancelled != FrameWinner.None;
        CancelMatchBtn.BackgroundColor = hasCancel ? Color.FromArgb("#DC2626") : Color.FromArgb("#991B1B");
    }

    private void LoadCancelUI()
    {
        if (_selectedFixture == null) return;

        bool hasCancel = _selectedFixture.CancelledByTeam != FrameWinner.None;
        CancelPanel.IsVisible = hasCancel;

        // Set team names on buttons
        var data = DataStore.Data;
        var homeTeam = data.Teams.FirstOrDefault(t => t.Id == _selectedFixture.HomeTeamId);
        var awayTeam = data.Teams.FirstOrDefault(t => t.Id == _selectedFixture.AwayTeamId);
        CancelHomeBtn.Text = homeTeam?.Name ?? "Home";
        CancelAwayBtn.Text = awayTeam?.Name ?? "Away";

        UpdateCancelUI();
    }

    // ========== SAVE & CLEAR ==========

    private async System.Threading.Tasks.Task SaveFromUIAsync()
    {
        if (_selectedFixture == null) return;
        if (await CheckSeasonLockedAsync("save results")) return;

        // Capture local reference — RefreshList() clears the ObservableCollection which
        // triggers SelectionChanged → OnSelectFixture(null) → _selectedFixture = null.
        var fixture = _selectedFixture;

        // Update fixture frames from UI data
        for (int i = 0; i < _frameRows.Count && i < fixture.Frames.Count; i++)
        {
            var row = _frameRows[i];
            var fr = fixture.Frames[i];

            fr.HomePlayerId = row.HomePlayerId;
            fr.AwayPlayerId = row.AwayPlayerId;
            fr.HomePlayer2Id = row.HomePlayer2Id;
            fr.AwayPlayer2Id = row.AwayPlayer2Id;
            fr.IsDoubles = row.IsDoubles;
            fr.Winner = row.Winner;
            fr.EightBall = row.EightBall;
        }

        // Check for scheduling conflicts
        var conflicts = FixtureValidator.DetectScheduleConflicts(
            fixture,
            DataStore.Data.Fixtures,
            DataStore.Data.Teams,
            DataStore.Data.Venues);

        if (conflicts.Warnings.Count > 0)
        {
            var msg = string.Join("\n", conflicts.Warnings);
            var proceed = await DisplayAlert($"{Emojis.Warning} Schedule Conflicts",
                msg + "\n\nSave anyway?", "Save", "Cancel");
            if (!proceed) return;
        }

        DataStore.Save();
        UpdateHeader();

        await ScheduleFixtureReminderAsync(fixture);
        UpdateReminderStatus();

        // Remember selected fixture before refresh clears it
        var savedId = fixture.Id;
        RefreshList();
        RestoreSelection(savedId);

        // Send result notification if enabled
        if (_reminderService != null && fixture.Frames.Count > 0)
        {
            try { await _reminderService.NotifyMatchResultIfEnabledAsync(fixture, DataStore.Data.GetSettingsForSeason(fixture.SeasonId)); }
            catch { /* non-critical */ }
        }

        await DisplayAlert($"{Emojis.Success} Saved", 
            "Fixture results saved successfully!", "OK");
    }

    private async void OnClearFrames()
    {
        if (_selectedFixture == null) return;
        if (await CheckSeasonLockedAsync("clear frames")) return;

        foreach (var row in _frameRows)
        {
            row.HomePlayerId = null;
            row.HomePlayerName = "";
            row.AwayPlayerId = null;
            row.AwayPlayerName = "";
            row.HomePlayer2Id = null;
            row.HomePlayer2Name = "";
            row.AwayPlayer2Id = null;
            row.AwayPlayer2Name = "";
            row.Winner = FrameWinner.None;
            row.EightBall = false;

            if (row.HomePlayerLabel != null)
            {
                row.HomePlayerLabel.Text = row.IsDoubles ? "Tap to select pair..." : "Tap to select...";
                row.HomePlayerLabel.TextColor = Color.FromArgb("#94A3B8");
                row.HomePlayerLabel.FontAttributes = FontAttributes.Italic;
            }
            if (row.AwayPlayerLabel != null)
            {
                row.AwayPlayerLabel.Text = row.IsDoubles ? "Tap to select pair..." : "Tap to select...";
                row.AwayPlayerLabel.TextColor = Color.FromArgb("#94A3B8");
                row.AwayPlayerLabel.FontAttributes = FontAttributes.Italic;
            }
            if (row.HomeScoreBtn != null)
            {
                row.HomeScoreBtn.Text = "0";
                row.HomeScoreBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
                row.HomeScoreBtn.TextColor = Color.FromArgb("#94A3B8");
            }
            if (row.AwayScoreBtn != null)
            {
                row.AwayScoreBtn.Text = "0";
                row.AwayScoreBtn.BackgroundColor = Color.FromArgb("#E2E8F0");
                row.AwayScoreBtn.TextColor = Color.FromArgb("#94A3B8");
            }
            if (row.EightBallCheck != null)
            {
                row.EightBallCheck.IsChecked = false;
            }
        }

        _currentFrameIndex = 0;
        _selectingHomePlayer = true;
        _entryPhase = EntryPhase.HomeLineup;
        UpdateCurrentFrameIndicator();
        HighlightCurrentFrame();
        UpdatePlayerFrameCounts();
        UpdateScoreDisplay();
    }

    private async System.Threading.Tasks.Task OnRescheduleFixtureAsync()
    {
        if (_selectedFixture == null)
        {
            await DisplayAlert("No Fixture", "Select a fixture to reschedule.", "OK");
            return;
        }
        if (await CheckSeasonLockedAsync("reschedule")) return;

        var newDateStr = await DisplayPromptAsync("Reschedule Fixture",
            $"Current date: {_selectedFixture.Date:ddd dd MMM yyyy}\nEnter new date (dd/MM/yyyy):",
            placeholder: _selectedFixture.Date.ToString("dd/MM/yyyy"));

        if (string.IsNullOrWhiteSpace(newDateStr)) return;

        if (!DateTime.TryParseExact(newDateStr, new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var newDate))
        {
            await DisplayAlert("Invalid Date", "Please enter a valid date in dd/MM/yyyy format.", "OK");
            return;
        }

        // Preserve time of day
        newDate = newDate.Date + _selectedFixture.Date.TimeOfDay;

        // Check for conflicts
        var tempFixture = new Fixture
        {
            Id = _selectedFixture.Id,
            HomeTeamId = _selectedFixture.HomeTeamId,
            AwayTeamId = _selectedFixture.AwayTeamId,
            VenueId = _selectedFixture.VenueId,
            TableId = _selectedFixture.TableId,
            Date = newDate,
            SeasonId = _selectedFixture.SeasonId
        };

        var conflicts = FixtureValidator.DetectScheduleConflicts(
            tempFixture, DataStore.Data.Fixtures, DataStore.Data.Teams, DataStore.Data.Venues);

        if (conflicts.Warnings.Count > 0)
        {
            var msg = string.Join("\n", conflicts.Warnings);
            var proceed = await DisplayAlert($"{Emojis.Warning} Schedule Conflicts",
                msg + "\n\nReschedule anyway?", "Yes", "Cancel");
            if (!proceed) return;
        }

        _selectedFixture.Date = newDate;
        _selectedFixture.ModifiedDate = DateTime.UtcNow;
        DataStore.Save();

        UpdateHeader();
        RefreshList();

        await DisplayAlert($"{Emojis.Success} Rescheduled",
            $"Fixture moved to {newDate:ddd dd MMM yyyy}", "OK");
    }

    private async System.Threading.Tasks.Task OnPrintScorecardAsync()
    {
        if (_selectedFixture == null)
        {
            await DisplayAlert("No Fixture", "Select a fixture first.", "OK");
            return;
        }

        try
        {
            var seasonId = _selectedFixture.SeasonId;
            var players = DataStore.Data.Players.Where(p => p.SeasonId == seasonId).ToList();
            var teams = DataStore.Data.Teams.Where(t => t.SeasonId == seasonId).ToList();
            var venues = DataStore.Data.Venues.Where(v => v.SeasonId == seasonId).ToList();
            var frames = DataStore.Data.GetSettingsForSeason(_selectedFixture.SeasonId).DefaultFramesPerMatch;

            var html = ExportService.GenerateBlankScorecardHtml(_selectedFixture, teams, players, venues, frames);
            await ExportService.ShareFileAsync(html, $"scorecard_{_selectedFixture.Date:yyyyMMdd}.html", "Blank Scorecard");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", ex.Message, "OK");
        }
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

        var settings = DataStore.Data.GetSettingsForSeason(_selectedFixture.SeasonId);
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

        var settings = DataStore.Data.GetSettingsForSeason(fixture.SeasonId);
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
                "Close", "Cancel", options);

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

        if (DataStore.Data.IsSeasonLocked(seasonId))
        {
            await DisplayAlert($"{Emojis.Lock} Season Locked",
                "Cannot delete fixtures — this season is locked.", "OK");
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
        if (await CheckSeasonLockedAsync("generate fixtures")) return;
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

        // Also count total season teams — handles case where DivisionId isn't set yet
        var totalSeasonTeams = DataStore.Data.Teams.Count(t => t.SeasonId == seasonId);
        var seasonDivisions = DataStore.Data.Divisions.Count(d => d.SeasonId == seasonId);

        if (teamCounts.All(x => x < 2) && (totalSeasonTeams < 2 || seasonDivisions == 0))
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
            var settings = DataStore.Data.GetSettingsForSeason(seasonId);
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

            // Detect any scheduling conflicts across the generated fixtures
            var allConflictWarnings = new List<string>();
            var teams = DataStore.Data.Teams;
            var venues = DataStore.Data.Venues;
            foreach (var fix in fixtures)
            {
                var check = FixtureValidator.DetectScheduleConflicts(fix, fixtures, teams, venues);
                allConflictWarnings.AddRange(check.Warnings);
            }
            // Deduplicate warnings
            allConflictWarnings = allConflictWarnings.Distinct().ToList();

            _selectedFixture = null;
            ClearScorecard();
            RefreshList();

            var successMsg = $"Generated {fixtures.Count} fixture(s).";
            if (allConflictWarnings.Count > 0)
            {
                var top = allConflictWarnings.Take(10);
                var extra = allConflictWarnings.Count > 10
                    ? $"\n...and {allConflictWarnings.Count - 10} more"
                    : "";
                successMsg += $"\n\n{Emojis.Warning} Schedule conflicts detected:\n"
                    + string.Join("\n", top) + extra;
            }

            await DisplayAlert($"{Emojis.Success} Success", successMsg, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", ex.Message, "OK");
        }
    }

    // ========== BULK SCORE ENTRY ==========

    private async System.Threading.Tasks.Task OnBulkScoreEntryAsync()
    {
        try
        {
            var data = DataStore.Data;
            var seasonId = data.ActiveSeasonId;
            if (!seasonId.HasValue)
            {
                await DisplayAlert($"{Emojis.Info} No Season", "Set an active season first.", "OK");
                return;
            }

            // Get unplayed fixtures for the active season
            var unplayed = data.Fixtures
                .Where(f => f.SeasonId == seasonId && f.Frames.All(fr => fr.Winner == FrameWinner.None))
                .OrderBy(f => f.Date)
                .Take(20)
                .ToList();

            if (unplayed.Count == 0)
            {
                await DisplayAlert($"{Emojis.Info} No Fixtures", "No unplayed fixtures found for bulk entry.", "OK");
                return;
            }

            var teams = data.Teams.Where(t => t.SeasonId == seasonId).ToDictionary(t => t.Id, t => t.Name ?? "?");

            // Build a quick-entry page
            var page = new ContentPage { Title = "⚡ Bulk Score Entry" };
            var scrollView = new ScrollView();
            var stack = new VerticalStackLayout { Spacing = 8, Padding = 16 };

            var entries = new List<(Fixture fixture, Entry homeEntry, Entry awayEntry)>();

            foreach (var fixture in unplayed)
            {
                var homeName = teams.TryGetValue(fixture.HomeTeamId, out var hn) ? hn : "Home";
                var awayName = teams.TryGetValue(fixture.AwayTeamId, out var an) ? an : "Away";

                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(50) },
                        new ColumnDefinition { Width = new GridLength(20) },
                        new ColumnDefinition { Width = new GridLength(50) },
                        new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }
                    },
                    ColumnSpacing = 4,
                    Padding = new Thickness(0, 4)
                };

                row.Add(new Label
                {
                    Text = $"{fixture.Date:dd/MM} {homeName}",
                    FontSize = 12,
                    VerticalTextAlignment = TextAlignment.Center,
                    HorizontalTextAlignment = TextAlignment.End
                }, 0, 0);

                var homeEntry = new Entry { Keyboard = Keyboard.Numeric, FontSize = 14, HorizontalTextAlignment = TextAlignment.Center };
                row.Add(homeEntry, 1, 0);

                row.Add(new Label { Text = "-", FontSize = 14, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 2, 0);

                var awayEntry = new Entry { Keyboard = Keyboard.Numeric, FontSize = 14, HorizontalTextAlignment = TextAlignment.Center };
                row.Add(awayEntry, 3, 0);

                row.Add(new Label
                {
                    Text = awayName,
                    FontSize = 12,
                    VerticalTextAlignment = TextAlignment.Center
                }, 4, 0);

                stack.Children.Add(row);
                entries.Add((fixture, homeEntry, awayEntry));
            }

            var saveAllBtn = new Button
            {
                Text = $"💾 Save All Scores",
                BackgroundColor = Color.FromArgb("#10B981"),
                TextColor = Colors.White,
                FontSize = 14,
                CornerRadius = 8,
                Margin = new Thickness(0, 16)
            };

            int saved = 0;
            saveAllBtn.Clicked += async (_, __) =>
            {
                foreach (var (fixture, homeEntry, awayEntry) in entries)
                {
                    if (int.TryParse(homeEntry.Text, out var hs) && int.TryParse(awayEntry.Text, out var aws))
                    {
                        // Distribute wins across frames
                        int totalFrames = fixture.Frames.Count;
                        if (totalFrames == 0) continue;

                        int homeWins = Math.Min(hs, totalFrames);
                        int awayWins = Math.Min(aws, totalFrames - homeWins);

                        for (int i = 0; i < totalFrames; i++)
                        {
                            if (i < homeWins)
                                fixture.Frames[i].Winner = FrameWinner.Home;
                            else if (i < homeWins + awayWins)
                                fixture.Frames[i].Winner = FrameWinner.Away;
                        }
                        saved++;
                    }
                }

                if (saved > 0)
                {
                    DataStore.Save();
                    RefreshList();
                    await page.DisplayAlert($"{Emojis.Success} Done", $"Saved scores for {saved} fixture(s).", "OK");
                    await Navigation.PopAsync();
                }
                else
                {
                    await page.DisplayAlert($"{Emojis.Info} Nothing Saved", "Enter at least one score pair.", "OK");
                }
            };

            stack.Children.Add(saveAllBtn);
            scrollView.Content = stack;
            page.Content = scrollView;

            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", ex.Message, "OK");
        }
    }

    // ========== LIFECYCLE ==========

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        SeasonService.Current.SeasonChanged -= OnGlobalSeasonChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SeasonService.Current.SeasonChanged += OnGlobalSeasonChanged;

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
        
        // Update FromDate to the active season's start date if available
        if (FromDate != null)
        {
            var activeSeasonId = DataStore.Data.ActiveSeasonId;
            if (activeSeasonId.HasValue)
            {
                var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == activeSeasonId);
                if (season != null)
                {
                    // Set to the season start date to show all fixtures
                    FromDate.Date = season.StartDate;
                }
            }
            else
            {
                // No active season - show fixtures from start of year
                FromDate.Date = new DateTime(DateTime.Today.Year, 1, 1);
            }
        }
        
        RefreshList();
    }
}
