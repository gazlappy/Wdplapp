using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using Wdpl2.Helpers;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class PlayersPage : ContentPage
{
    public sealed class PlayerListItem
    {
        public Guid Id { get; set; }
        public string? First { get; set; }
        public string? Last { get; set; }
        public string FullName => string.Join(" ", new[] { First, Last }.Where(s => !string.IsNullOrWhiteSpace(s)));
        public string TeamLabel { get; set; } = "";
        public string Initials => $"{(string.IsNullOrWhiteSpace(First) ? "" : First![0].ToString())}{(string.IsNullOrWhiteSpace(Last) ? "" : Last![0].ToString())}";
    }

    public sealed class HeadToHeadItem
    {
        public Guid OpponentId { get; set; }
        public string OpponentName { get; set; } = "";
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int TotalFrames => Wins + Losses;
        public double WinPercentage => TotalFrames == 0 ? 0 : (double)Wins / TotalFrames * 100.0;
        public int EightBalls { get; set; }
        public string RecordText => $"{Wins}-{Losses}";
        public Color RecordColor => Wins > Losses ? Color.FromArgb("#10B981") : 
                                    Wins < Losses ? Color.FromArgb("#EF4444") : 
                                    Color.FromArgb("#6B7280");
        public List<SeasonRecord> SeasonBreakdown { get; set; } = new();
        public bool HasMultipleSeasons => SeasonBreakdown.Count > 1;
    }

    public sealed class SeasonRecord
    {
        public string SeasonName { get; set; } = "";
        public string Record { get; set; } = "";
    }

    private readonly ObservableCollection<PlayerListItem> _items = new();
    private readonly ObservableCollection<Team> _teams = new();
    private readonly ObservableCollection<HeadToHeadItem> _h2hItems = new();
    private readonly ObservableCollection<Season> _h2hSeasons = new();

    private Player? _selected;
    private bool _isMultiSelectMode = false;
    private Guid? _currentSeasonId;
    private bool _isFlyoutOpen = false;

    public PlayersPage()
    {
        try
        {
            InitializeComponent();

            PlayersList.ItemsSource = _items;
            TeamPicker.ItemsSource = _teams;
            H2HList.ItemsSource = _h2hItems;
            H2HSeasonPicker.ItemsSource = _h2hSeasons;

            // Burger menu
            BurgerMenuBtn.Clicked += OnBurgerMenuClicked;
            CloseFlyoutBtn.Clicked += OnCloseFlyoutClicked;
            OverlayTap.Tapped += (_, __) => CloseFlyout();

            PlayersList.SelectionChanged += OnSelectionChanged;
            SearchEntry.TextChanged += (_, __) => SafeRefreshPlayers(SearchEntry?.Text);
            H2HSeasonPicker.SelectedIndexChanged += (_, __) => RefreshHeadToHead();

            AddBtn.Clicked += OnAdd;
            UpdateBtn.Clicked += OnUpdate;
            DeleteBtn.Clicked += OnDelete;
            MultiSelectBtn.Clicked += OnToggleMultiSelect;
            BulkDeleteBtn.Clicked += OnBulkDelete;

            SaveBtn.Clicked += async (_, __) =>
            {
                try
                {
                    DataStore.Save();
                    await DisplayAlert($"{Emojis.Success} Saved", "All changes saved successfully!", "OK");
                    SetStatus("Saved.");
                }
                catch (Exception ex)
                {
                    await DisplayAlert($"{Emojis.Error} Error", $"Failed to save: {ex.Message}", "OK");
                    SetStatus($"Save failed: {ex.Message}");
                }
            };

            ReloadBtn.Clicked += (_, __) =>
            {
                try
                {
                    DataStore.Load();
                    RefreshAll();
                    SetStatus("Reloaded.");
                }
                catch (Exception ex)
                {
                    SetStatus($"Reload failed: {ex.Message}");
                }
            };

            ExportBtn.Clicked += async (_, __) => await ExportPlayersAsync();
            PlayersImport.ImportRequested += async (stream, fileName) => await ImportPlayersCsvAsync(stream, fileName);

            // SUBSCRIBE to global season changes
            SeasonService.SeasonChanged += OnGlobalSeasonChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PlayersPage Constructor Error: {ex}");
            SetStatus($"Initialization error: {ex.Message}");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // Refresh data when page appears to ensure we have latest season
            RefreshAll();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PlayersPage OnAppearing Error: {ex}");
            SetStatus($"Error loading data: {ex.Message}");
        }
    }

    ~PlayersPage()
    {
        try
        {
            SeasonService.SeasonChanged -= OnGlobalSeasonChanged;
        }
        catch
        {
            // Ignore cleanup errors
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
    }

    private async void CloseFlyout()
    {
        // Animate flyout sliding out
        await FlyoutPanel.TranslateTo(-400, 0, 250, Easing.CubicIn);
        
        FlyoutOverlay.IsVisible = false;
        FlyoutPanel.IsVisible = false;
        _isFlyoutOpen = false;
    }

    // ========== HEAD-TO-HEAD ==========

    private void RefreshHeadToHead()
    {
        try
        {
            _h2hItems.Clear();

            if (_selected == null)
            {
                PlayerInfoPanel.IsVisible = false;
                FilterPanel.IsVisible = false;
                EmptyStatePanel.IsVisible = true;
                H2HList.IsVisible = false;
                return;
            }

            // Show player info
            PlayerInfoPanel.IsVisible = true;
            FilterPanel.IsVisible = true;
            EmptyStatePanel.IsVisible = false;
            H2HList.IsVisible = true;

            SelectedPlayerName.Text = _selected.FullName;
            
            var team = DataStore.Data.Teams?.FirstOrDefault(t => t.Id == _selected.TeamId);
            SelectedPlayerTeam.Text = team?.Name ?? "No team assigned";

            // Determine which seasons to include
            List<Guid> seasonIds = new();
            var selectedSeason = H2HSeasonPicker.SelectedItem as Season;
            
            if (selectedSeason != null)
            {
                seasonIds.Add(selectedSeason.Id);
            }
            else
            {
                // All seasons (across player's history)
                seasonIds = DataStore.Data.Seasons
                    .Select(s => s.Id)
                    .ToList();
            }

            // Build head-to-head records
            var h2hData = new Dictionary<Guid, HeadToHeadItem>();
            var seasonRecords = new Dictionary<Guid, Dictionary<Guid, (int wins, int losses, int eightBalls)>>();

            // Get all fixtures for the selected seasons
            var fixtures = DataStore.Data.Fixtures
                .Where(f => seasonIds.Contains(f.SeasonId ?? Guid.Empty))
                .ToList();

            int totalFramesPlayed = 0;
            int totalWins = 0;
            int totalEightBalls = 0;

            foreach (var fixture in fixtures)
            {
                foreach (var frame in fixture.Frames)
                {
                    Guid? playerId = null;
                    Guid? opponentId = null;
                    bool playerWon = false;
                    bool eightBall = false;

                    // Check if our player is home
                    if (frame.HomePlayerId == _selected.Id)
                    {
                        playerId = frame.HomePlayerId;
                        opponentId = frame.AwayPlayerId;
                        playerWon = frame.Winner == FrameWinner.Home;
                        eightBall = frame.EightBall && frame.Winner == FrameWinner.Home;
                    }
                    // Check if our player is away
                    else if (frame.AwayPlayerId == _selected.Id)
                    {
                        playerId = frame.AwayPlayerId;
                        opponentId = frame.HomePlayerId;
                        playerWon = frame.Winner == FrameWinner.Away;
                        eightBall = frame.EightBall && frame.Winner == FrameWinner.Away;
                    }

                    if (playerId.HasValue && opponentId.HasValue)
                    {
                        totalFramesPlayed++;
                        if (playerWon) totalWins++;
                        if (eightBall) totalEightBalls++;

                        // Overall head-to-head
                        if (!h2hData.ContainsKey(opponentId.Value))
                        {
                            var opponent = DataStore.Data.Players?.FirstOrDefault(p => p.Id == opponentId.Value);
                            h2hData[opponentId.Value] = new HeadToHeadItem
                            {
                                OpponentId = opponentId.Value,
                                OpponentName = opponent?.FullName ?? "Unknown Player"
                            };
                        }

                        if (playerWon)
                            h2hData[opponentId.Value].Wins++;
                        else
                            h2hData[opponentId.Value].Losses++;

                        if (eightBall)
                            h2hData[opponentId.Value].EightBalls++;

                        // Season breakdown (only if showing all seasons)
                        if (selectedSeason == null && fixture.SeasonId.HasValue)
                        {
                            var seasonId = fixture.SeasonId.Value;
                            if (!seasonRecords.ContainsKey(opponentId.Value))
                                seasonRecords[opponentId.Value] = new Dictionary<Guid, (int, int, int)>();

                            if (!seasonRecords[opponentId.Value].ContainsKey(seasonId))
                                seasonRecords[opponentId.Value][seasonId] = (0, 0, 0);

                            var current = seasonRecords[opponentId.Value][seasonId];
                            seasonRecords[opponentId.Value][seasonId] = playerWon
                                ? (current.wins + 1, current.losses, current.eightBalls + (eightBall ? 1 : 0))
                                : (current.wins, current.losses + 1, current.eightBalls);
                        }
                    }
                }
            }

            // Add season breakdown to items
            foreach (var kvp in h2hData)
            {
                if (seasonRecords.TryGetValue(kvp.Key, out var seasons))
                {
                    foreach (var seasonKvp in seasons.OrderByDescending(s => s.Key))
                    {
                        var season = DataStore.Data.Seasons?.FirstOrDefault(s => s.Id == seasonKvp.Key);
                        kvp.Value.SeasonBreakdown.Add(new SeasonRecord
                        {
                            SeasonName = season?.Name ?? "Unknown Season",
                            Record = $"{seasonKvp.Value.wins}-{seasonKvp.Value.losses}"
                        });
                    }
                }
            }

            // Sort by total frames played (most frequent opponents first)
            var sortedH2H = h2hData.Values
                .OrderByDescending(h => h.TotalFrames)
                .ThenByDescending(h => h.WinPercentage)
                .ToList();

            foreach (var item in sortedH2H)
                _h2hItems.Add(item);

            // Update player stats
            var winPct = totalFramesPlayed > 0 ? (double)totalWins / totalFramesPlayed * 100.0 : 0;
            SelectedPlayerStats.Text = $"{totalFramesPlayed} frames • {totalWins}W-{totalFramesPlayed - totalWins}L ({winPct:0.#}%) • {totalEightBalls} 8-balls";

            SetStatus($"Found {_h2hItems.Count} opponent(s)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshHeadToHead Error: {ex}");
            SetStatus($"Error loading head-to-head: {ex.Message}");
        }
    }

    // ========== EXISTING METHODS ==========

    private void OnGlobalSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _currentSeasonId = e.NewSeasonId;
                SafeRefreshPlayers(SearchEntry?.Text);
                SafeRefreshTeams();
                RefreshH2HSeasons();

                var seasonName = e.NewSeason?.Name ?? "None";
                var isImported = e.NewSeason != null && !e.NewSeason.IsActive ? " (Imported)" : "";
                SetStatus($"Season: {seasonName}{isImported}");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Season change error: {ex}");
            SetStatus($"Error changing season: {ex.Message}");
        }
    }

    private void RefreshAll()
    {
        try
        {
            // Use global season from SeasonService
            _currentSeasonId = SeasonService.CurrentSeasonId;

            // If no season is set, try to use the active season
            if (!_currentSeasonId.HasValue)
            {
                var activeSeason = DataStore.Data?.Seasons?.FirstOrDefault(s => s.IsActive);
                if (activeSeason != null)
                {
                    _currentSeasonId = activeSeason.Id;
                }
            }

            SafeRefreshPlayers(SearchEntry?.Text);
            SafeRefreshTeams();
            RefreshH2HSeasons();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshAll Error: {ex}");
            SetStatus($"Refresh error: {ex.Message}");
        }
    }

    private void RefreshH2HSeasons()
    {
        try
        {
            _h2hSeasons.Clear();

            var seasons = DataStore.Data.Seasons
                .OrderByDescending(s => s.StartDate)
                .ToList();

            foreach (var season in seasons)
            {
                _h2hSeasons.Add(season);
            }

            // Select current season by default
            if (_currentSeasonId.HasValue)
            {
                var currentSeason = _h2hSeasons.FirstOrDefault(s => s.Id == _currentSeasonId);
                if (currentSeason != null)
                    H2HSeasonPicker.SelectedItem = currentSeason;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshH2HSeasons Error: {ex}");
        }
    }

    private void SafeRefreshTeams()
    {
        try
        {
            _teams.Clear();

            if (!_currentSeasonId.HasValue)
            {
                SetStatus("No season selected");
                return;
            }

            if (DataStore.Data?.Teams == null)
            {
                SetStatus("No teams data available");
                return;
            }

            var teams = DataStore.Data.Teams
                .Where(t => t != null && t.SeasonId == _currentSeasonId)
                .OrderBy(t => t.Name ?? "")
                .ToList();

            foreach (var t in teams)
            {
                _teams.Add(t);
            }

            System.Diagnostics.Debug.WriteLine($"Loaded {_teams.Count} teams for season {_currentSeasonId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SafeRefreshTeams Error: {ex}");
            SetStatus($"Error loading teams: {ex.Message}");
        }
    }

    private void SafeRefreshPlayers(string? search)
    {
        try
        {
            _items.Clear();

            if (!_currentSeasonId.HasValue)
            {
                SetStatus("No season selected");
                return;
            }

            if (DataStore.Data?.Players == null)
            {
                SetStatus("No players data available");
                return;
            }

            var players = DataStore.Data.Players
                .Where(p => p != null && p.SeasonId == _currentSeasonId.Value)
                .OrderBy(p => p.LastName ?? "")
                .ThenBy(p => p.FirstName ?? "")
                .ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                players = players.Where(p =>
                    (p.FirstName ?? "").ToLower().Contains(lower) ||
                    (p.LastName ?? "").ToLower().Contains(lower))
                    .OrderBy(p => p.LastName ?? "")
                    .ThenBy(p => p.FirstName ?? "")
                    .ToList();
            }

            // Build a team lookup dictionary for faster access
            var teamLookup = DataStore.Data.Teams?
                .Where(t => t != null && t.SeasonId == _currentSeasonId)
                .ToDictionary(t => t.Id, t => t.Name ?? "Unknown Team")
                ?? new Dictionary<Guid, string>();

            foreach (var p in players)
            {
                var teamLabel = "";
                if (p.TeamId.HasValue && teamLookup.TryGetValue(p.TeamId.Value, out var teamName))
                {
                    teamLabel = teamName;
                }

                _items.Add(new PlayerListItem
                {
                    Id = p.Id,
                    First = p.FirstName,
                    Last = p.LastName,
                    TeamLabel = teamLabel
                });
            }

            var season = DataStore.Data.Seasons?.FirstOrDefault(s => s.Id == _currentSeasonId);
            var seasonInfo = season != null ? $" in {season.Name}" : "";
            var importedTag = season != null && !season.IsActive ? " (Imported)" : "";
            SetStatus($"{_items.Count} player(s){seasonInfo}{importedTag}");

            System.Diagnostics.Debug.WriteLine($"Loaded {_items.Count} players for season {_currentSeasonId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SafeRefreshPlayers Error: {ex}");
            SetStatus($"Error loading players: {ex.Message}");
        }
    }

    private void RefreshPlayers(string? search) => SafeRefreshPlayers(search);
    private void RefreshTeams() => SafeRefreshTeams();

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_isMultiSelectMode) return;

            var item = e.CurrentSelection?.FirstOrDefault() as PlayerListItem;
            if (item == null)
            {
                _selected = null;
                ClearEditor();
                RefreshHeadToHead(); // Clear H2H when no selection
                return;
            }

            _selected = DataStore.Data?.Players?.FirstOrDefault(p => p.Id == item.Id);
            if (_selected == null)
            {
                SetStatus("Player not found");
                return;
            }

            LoadEditor(_selected);
            RefreshHeadToHead(); // Load H2H for selected player
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnSelectionChanged Error: {ex}");
            SetStatus($"Selection error: {ex.Message}");
        }
    }

    private void LoadEditor(Player player)
    {
        try
        {
            FirstNameEntry.Text = player.FirstName ?? "";
            LastNameEntry.Text = player.LastName ?? "";
            NotesEntry.Text = player.Notes ?? "";

            SafeRefreshTeams();

            if (player.TeamId.HasValue)
            {
                TeamPicker.SelectedItem = _teams.FirstOrDefault(t => t.Id == player.TeamId);
            }
            else
            {
                TeamPicker.SelectedIndex = -1;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadEditor Error: {ex}");
            SetStatus($"Error loading player: {ex.Message}");
        }
    }

    private void ClearEditor()
    {
        try
        {
            FirstNameEntry.Text = "";
            LastNameEntry.Text = "";
            NotesEntry.Text = "";
            TeamPicker.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ClearEditor Error: {ex}");
        }
    }

    private void OnAdd(object? sender, EventArgs e)
    {
        try
        {
            if (!_currentSeasonId.HasValue)
            {
                SetStatus("Please select a season on the Seasons page first");
                return;
            }

            var first = FirstNameEntry.Text?.Trim() ?? "";
            var last = LastNameEntry.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(last))
            {
                SetStatus("Name required");
                return;
            }

            var player = new Player
            {
                SeasonId = _currentSeasonId.Value,
                FirstName = first,
                LastName = last,
                TeamId = (TeamPicker.SelectedItem as Team)?.Id,
                Notes = NotesEntry.Text?.Trim()
            };

            DataStore.Data.Players.Add(player);
            SafeRefreshPlayers(SearchEntry?.Text);
            SetStatus($"Added: {player.FullName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnAdd Error: {ex}");
            SetStatus($"Add failed: {ex.Message}");
        }
    }

    private void OnUpdate(object? sender, EventArgs e)
    {
        try
        {
            if (_selected == null)
            {
                SetStatus("No player selected");
                return;
            }

            _selected.FirstName = FirstNameEntry.Text?.Trim() ?? "";
            _selected.LastName = LastNameEntry.Text?.Trim() ?? "";
            _selected.TeamId = (TeamPicker.SelectedItem as Team)?.Id;
            _selected.Notes = NotesEntry.Text?.Trim();

            SafeRefreshPlayers(SearchEntry?.Text);
            RefreshHeadToHead(); // Refresh H2H with updated player info
            SetStatus(msg: $"Updated: {_selected.FullName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnUpdate Error: {ex}");
            SetStatus($"Update failed: {ex.Message}");
        }
    }

    private async void OnDelete(object? sender, EventArgs e)
    {
        try
        {
            if (_selected == null)
            {
                SetStatus("No player selected");
                return;
            }

            var confirm = await DisplayAlert($"{Emojis.Warning} Delete Player", $"Delete '{_selected.FullName}'?", "Yes", "No");
            if (!confirm) return;

            DataStore.Data.Players.Remove(_selected);
            _selected = null;
            SafeRefreshPlayers(SearchEntry?.Text);
            ClearEditor();
            RefreshHeadToHead(); // Clear H2H
            SetStatus($"{Emojis.Success} Deleted");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnDelete Error: {ex}");
            await DisplayAlert($"{Emojis.Error} Error", $"Delete failed: {ex.Message}", "OK");
            SetStatus($"Delete failed: {ex.Message}");
        }
    }

    private void OnToggleMultiSelect(object? sender, EventArgs e)
    {
        try
        {
            _isMultiSelectMode = !_isMultiSelectMode;

            if (_isMultiSelectMode)
            {
                PlayersList.SelectionMode = SelectionMode.Multiple;
                MultiSelectBtn.Text = "✓ Multi-Select ON";
                MultiSelectBtn.BackgroundColor = Color.FromArgb("#10B981");
                BulkDeleteBtn.IsVisible = true;

                UpdateBtn.IsEnabled = false;
                DeleteBtn.IsEnabled = false;
                AddBtn.IsEnabled = false;
            }
            else
            {
                PlayersList.SelectionMode = SelectionMode.Single;
                MultiSelectBtn.Text = "☐ Multi-Select OFF";
                MultiSelectBtn.BackgroundColor = Color.FromArgb("#6B7280");
                BulkDeleteBtn.IsVisible = false;

                UpdateBtn.IsEnabled = true;
                DeleteBtn.IsEnabled = true;
                AddBtn.IsEnabled = true;
            }

            SetStatus(_isMultiSelectMode ? "Multi-select enabled" : "Multi-select disabled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnToggleMultiSelect Error: {ex}");
            SetStatus($"Toggle error: {ex.Message}");
        }
    }

    private async void OnBulkDelete(object? sender, EventArgs e)
    {
        try
        {
            var selectedItems = PlayersList.SelectedItems?.Cast<PlayerListItem>().ToList();

            if (selectedItems == null || selectedItems.Count == 0)
            {
                await DisplayAlert($"{Emojis.Info} No Selection", "Please select players to delete.", "OK");
                return;
            }

            var confirm = await DisplayAlert(
                $"{Emojis.Warning} Bulk Delete",
                $"Delete {selectedItems.Count} player(s)?",
                "Yes, Delete",
                "Cancel");

            if (!confirm) return;

            int deleted = 0;
            foreach (var item in selectedItems)
            {
                var player = DataStore.Data?.Players?.FirstOrDefault(p => p.Id == item.Id);
                if (player != null)
                {
                    DataStore.Data?.Players?.Remove(player);
                    deleted++;
                }
            }

            SafeRefreshPlayers(SearchEntry?.Text);
            SetStatus($"{Emojis.Success} Deleted {deleted} player(s)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnBulkDelete Error: {ex}");
            await DisplayAlert($"{Emojis.Error} Error", $"Bulk delete failed: {ex.Message}", "OK");
            SetStatus($"Bulk delete failed: {ex.Message}");
        }
    }

    private async Task ExportPlayersAsync()
    {
        try
        {
            if (!_currentSeasonId.HasValue)
            {
                await DisplayAlert($"{Emojis.Info} No Season", "Please select a season on the Seasons page first.", "OK");
                return;
            }

            var season = DataStore.Data?.Seasons?.FirstOrDefault(s => s.Id == _currentSeasonId);
            var csv = new StringBuilder();
            csv.AppendLine("FirstName,LastName,Team,Notes");

            var players = DataStore.Data?.Players?
                .Where(p => p != null && p.SeasonId == _currentSeasonId)
                .OrderBy(p => p.LastName ?? "")
                .ToList() ?? new List<Player>();

            var teamLookup = DataStore.Data?.Teams?
                .Where(t => t != null && t.SeasonId == _currentSeasonId)
                .ToDictionary(t => t.Id, t => t.Name ?? "")
                ?? new Dictionary<Guid, string>();

            foreach (var p in players)
            {
                var teamName = "";
                if (p.TeamId.HasValue && teamLookup.TryGetValue(p.TeamId.Value, out var name))
                {
                    teamName = name;
                }
                csv.AppendLine($"\"{p.FirstName}\",\"{p.LastName}\",\"{teamName}\",\"{p.Notes}\"");
            }

            var fileName = $"Players_{season?.Name?.Replace(" ", "_") ?? "Unknown"}_{DateTime.Now:yyyyMMdd}.csv";
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(path, csv.ToString());

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Export Players",
                File = new ShareFile(path)
            });

            SetStatus($"{Emojis.Success} Exported {players.Count} players");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ExportPlayersAsync Error: {ex}");
            await DisplayAlert($"{Emojis.Error} Error", $"Export failed: {ex.Message}", "OK");
            SetStatus($"Export failed: {ex.Message}");
        }
    }

    private async Task ImportPlayersCsvAsync(Stream stream, string fileName)
    {
        try
        {
            if (!_currentSeasonId.HasValue)
            {
                await DisplayAlert($"{Emojis.Info} No Season", "Please select a season on the Seasons page before importing.", "OK");
                return;
            }

            var rows = Csv.Read(stream);
            int added = 0, updated = 0;

            var teams = DataStore.Data?.Teams?
                .Where(t => t != null && t.SeasonId == _currentSeasonId)
                .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                .ToDictionary(t => t.Name!.Trim(), t => t, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, Team>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in rows)
            {
                var first = r.Get("FirstName") ?? "";
                var last = r.Get("LastName") ?? "";

                if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last)) continue;

                var existing = DataStore.Data?.Players?.FirstOrDefault(p =>
                    p != null &&
                    p.SeasonId == _currentSeasonId &&
                    string.Equals(p.FirstName, first, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.LastName, last, StringComparison.OrdinalIgnoreCase));

                var teamName = r.Get("Team");
                var notes = r.Get("Notes");

                if (existing == null)
                {
                    var player = new Player
                    {
                        SeasonId = _currentSeasonId.Value,
                        FirstName = first.Trim(),
                        LastName = last.Trim(),
                        TeamId = !string.IsNullOrWhiteSpace(teamName) && teams.TryGetValue(teamName, out var team) ? team.Id : null,
                        Notes = notes
                    };
                    DataStore.Data?.Players?.Add(player);
                    added++;
                }
                else
                {
                    existing.TeamId = !string.IsNullOrWhiteSpace(teamName) && teams.TryGetValue(teamName, out var team) ? team.Id : null;
                    existing.Notes = notes;
                    updated++;
                }
            }

            SafeRefreshPlayers(SearchEntry?.Text);
            SetStatus($"{Emojis.Success} Imported: {added} added, {updated} updated");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ImportPlayersCsvAsync Error: {ex}");
            await DisplayAlert($"{Emojis.Error} Error", $"Import failed: {ex.Message}", "OK");
            SetStatus($"Import failed: {ex.Message}");
        }
    }

    private void SetStatus(string msg)
    {
        try
        {
            if (StatusLbl != null)
            {
                StatusLbl.Text = $"{DateTime.Now:HH:mm:ss} {msg}";
            }
        }
        catch
        {
            // Ignore status update errors
        }
    }
}