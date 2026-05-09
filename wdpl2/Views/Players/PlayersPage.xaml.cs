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
        public string DisplayTeamLabel => string.IsNullOrEmpty(TeamLabel) ? "+ team" : TeamLabel;
        public Color TeamLabelColor => string.IsNullOrEmpty(TeamLabel) ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#3B82F6");
        public string Initials => $"{(string.IsNullOrWhiteSpace(First) ? "" : First![0].ToString())}{(string.IsNullOrWhiteSpace(Last) ? "" : Last![0].ToString())}";
        
        // Status properties
        public bool IsActive { get; set; } = true;
        public bool HasTransfers { get; set; } = false;
        public string TransferBadge => HasTransfers ? "\U0001F504" : "";
        public string StatusLabel => IsActive ? "" : "\u26AA Inactive";
        public bool ShowStatusLabel => !IsActive;
        public double Opacity => IsActive ? 1.0 : 0.6;
        public Color AvatarColor => IsActive ? Color.FromArgb("#3B82F6") : Color.FromArgb("#9CA3AF");
        public TextDecorations NameDecoration => IsActive ? TextDecorations.None : TextDecorations.Strikethrough;
    }

    public sealed class TransferHistoryItem
    {
        public string TransferSummary { get; set; } = "";
        public string TransferDetails { get; set; } = "";
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

    private readonly IDataStore _dataStore;
    private readonly ObservableCollection<PlayerListItem> _items = new();
    private readonly ObservableCollection<Team> _teams = new();
    private readonly ObservableCollection<HeadToHeadItem> _h2hItems = new();
    private readonly ObservableCollection<Season> _h2hSeasons = new();
    private readonly ObservableCollection<TransferHistoryItem> _transferHistory = new();

    private Player? _selected;
    private bool _isMultiSelectMode = false;
    private Guid? _currentSeasonId;
    private bool _showAllSeasons = false;
    private bool _showUnassigned = false;
    private bool _hideInactive = false;
    private string _sortOption = "A ? Z";

    private static readonly string[] SortOptions = ["A ? Z", "Z ? A", "By Team", "Newest First"];

    public PlayersPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        try
        {
            InitializeComponent();

            PlayersList.ItemsSource = _items;
            TeamPicker.ItemsSource = _teams;
            H2HList.ItemsSource = _h2hItems;
            H2HSeasonPicker.ItemsSource = _h2hSeasons;
            TransferHistoryList.ItemsSource = _transferHistory;

            PlayersList.SelectionChanged += OnSelectionChanged;
            SearchEntry.TextChanged += (_, __) => SafeRefreshPlayers(SearchEntry?.Text);
            H2HSeasonPicker.SelectedIndexChanged += (_, __) => RefreshHeadToHead();

            ShowAllSeasonsCheck.CheckedChanged += (_, __) =>
            {
                _showAllSeasons = ShowAllSeasonsCheck.IsChecked;
                SafeRefreshPlayers(SearchEntry?.Text);
            };

            ShowUnassignedCheck.CheckedChanged += (_, __) =>
            {
                _showUnassigned = ShowUnassignedCheck.IsChecked;
                SafeRefreshPlayers(SearchEntry?.Text);
            };

            HideInactiveCheck.CheckedChanged += (_, __) =>
            {
                _hideInactive = HideInactiveCheck.IsChecked;
                SafeRefreshPlayers(SearchEntry?.Text);
            };

            SortPicker.ItemsSource = SortOptions;
            SortPicker.SelectedIndex = 0;
            SortPicker.SelectedIndexChanged += (_, __) =>
            {
                _sortOption = SortPicker.SelectedItem as string ?? "A ? Z";
                SafeRefreshPlayers(SearchEntry?.Text);
            };

            IsActiveSwitch.Toggled += OnActiveStatusToggled;
            TransferBtn.Clicked += async (_, __) => await OnTransferPlayerAsync();

            AddBtn.Clicked += OnAdd;
            UpdateBtn.Clicked += OnUpdate;
            DeleteBtn.Clicked += OnDelete;
            MultiSelectBtn.Clicked += OnToggleMultiSelect;
            BulkAssignTeamBtn.Clicked += OnBulkAssignTeam;
            BulkDeleteBtn.Clicked += OnBulkDelete;

            SaveBtn.Clicked += async (_, __) =>
            {
                try
                {
                    if (_dataStore.GetData().IsSeasonLocked(_currentSeasonId))
                    {
                        await DisplayAlert($"{Emojis.Lock} Season Locked",
                            "Cannot save changes � this season is locked.", "OK");
                        return;
                    }
                    await _dataStore.SaveAsync();
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
                SeasonService.Current.SeasonChanged += OnGlobalSeasonChanged;
                try
                {
                    RefreshAll();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"PlayersPage OnAppearing Error: {ex}");
                    SetStatus($"Error loading data: {ex.Message}");
                }
            }

            protected override void OnDisappearing()
            {
                base.OnDisappearing();
                SeasonService.Current.SeasonChanged -= OnGlobalSeasonChanged;
            }

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

            PlayerInfoPanel.IsVisible = true;
            FilterPanel.IsVisible = true;
            EmptyStatePanel.IsVisible = false;
            H2HList.IsVisible = true;

            SelectedPlayerName.Text = _selected.FullName;
            var team = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == _selected.TeamId);
            SelectedPlayerTeam.Text = team?.Name ?? "No team assigned";

            List<Guid> seasonIds = new();
            var selectedSeason = H2HSeasonPicker.SelectedItem as Season;
            if (selectedSeason != null)
                seasonIds.Add(selectedSeason.Id);
            else
                seasonIds = _dataStore.GetData().Seasons.Select(s => s.Id).ToList();

            var h2hData = new Dictionary<Guid, HeadToHeadItem>();
            var seasonRecords = new Dictionary<Guid, Dictionary<Guid, (int wins, int losses, int eightBalls)>>();

            var fixtures = _dataStore.GetData().Fixtures
                .Where(f => seasonIds.Contains(f.SeasonId ?? Guid.Empty))
                .ToList();

            int totalFramesPlayed = 0, totalWins = 0, totalEightBalls = 0;

            foreach (var fixture in fixtures)
            {
                foreach (var frame in fixture.Frames)
                {
                    Guid? playerId = null, opponentId = null;
                    bool playerWon = false, eightBall = false;

                    if (frame.HomePlayerId == _selected.Id)
                    {
                        playerId = frame.HomePlayerId;
                        opponentId = frame.AwayPlayerId;
                        playerWon = frame.Winner == FrameWinner.Home;
                        eightBall = frame.EightBall && frame.Winner == FrameWinner.Home;
                    }
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

                        if (!h2hData.ContainsKey(opponentId.Value))
                        {
                            var opponent = _dataStore.GetData().Players?.FirstOrDefault(p => p.Id == opponentId.Value);
                            h2hData[opponentId.Value] = new HeadToHeadItem
                            {
                                OpponentId = opponentId.Value,
                                OpponentName = opponent?.FullName ?? "Unknown Player"
                            };
                        }

                        if (playerWon) h2hData[opponentId.Value].Wins++;
                        else h2hData[opponentId.Value].Losses++;
                        if (eightBall) h2hData[opponentId.Value].EightBalls++;

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

            foreach (var kvp in h2hData)
            {
                if (seasonRecords.TryGetValue(kvp.Key, out var seasons))
                {
                    foreach (var seasonKvp in seasons.OrderByDescending(s => s.Key))
                    {
                        var season = _dataStore.GetData().Seasons?.FirstOrDefault(s => s.Id == seasonKvp.Key);
                        kvp.Value.SeasonBreakdown.Add(new SeasonRecord
                        {
                            SeasonName = season?.Name ?? "Unknown Season",
                            Record = $"{seasonKvp.Value.wins}-{seasonKvp.Value.losses}"
                        });
                    }
                }
            }

            var sortedH2H = h2hData.Values
                .OrderByDescending(h => h.TotalFrames)
                .ThenByDescending(h => h.WinPercentage)
                .ToList();

            foreach (var item in sortedH2H)
                _h2hItems.Add(item);

            var winPct = totalFramesPlayed > 0 ? (double)totalWins / totalFramesPlayed * 100.0 : 0;
            SelectedPlayerStats.Text = $"{totalFramesPlayed} frames � {totalWins}W-{totalFramesPlayed - totalWins}L ({winPct:0.#}%) � {totalEightBalls} 8-balls";
            SetStatus($"Found {_h2hItems.Count} opponent(s)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshHeadToHead Error: {ex}");
            SetStatus($"Error loading head-to-head: {ex.Message}");
        }
    }

    private void OnGlobalSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _currentSeasonId = e.NewSeasonId;
                _items.Clear();
                SafeRefreshPlayers(SearchEntry?.Text);
                SafeRefreshTeams();
                RefreshH2HSeasons();

                var statusMsg = e.NewSeason != null
                    ? $"Season: {e.NewSeason.Name}{(e.NewSeason.IsActive ? "" : " (Imported)")}"
                    : "No active season - data cleared";
                SetStatus(statusMsg);
            });
        }
        catch (Exception ex)
        {
            SetStatus($"Error changing season: {ex.Message}");
        }
    }

    private void RefreshAll()
    {
        try
        {
            _currentSeasonId = SeasonService.Current.CurrentSeasonId;
            if (!_currentSeasonId.HasValue)
            {
                var activeSeason = _dataStore.GetData()?.Seasons?.FirstOrDefault(s => s.IsActive);
                if (activeSeason != null) _currentSeasonId = activeSeason.Id;
            }
            SafeRefreshPlayers(SearchEntry?.Text);
            SafeRefreshTeams();
            RefreshH2HSeasons();
        }
        catch (Exception ex)
        {
            SetStatus($"Refresh error: {ex.Message}");
        }
    }

    private void RefreshH2HSeasons()
    {
        try
        {
            _h2hSeasons.Clear();
            foreach (var season in _dataStore.GetData().Seasons.OrderByDescending(s => s.StartDate))
                _h2hSeasons.Add(season);

            if (_currentSeasonId.HasValue)
            {
                var currentSeason = _h2hSeasons.FirstOrDefault(s => s.Id == _currentSeasonId);
                if (currentSeason != null) H2HSeasonPicker.SelectedItem = currentSeason;
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
            if (!_showAllSeasons && !_currentSeasonId.HasValue) return;
            if (_dataStore.GetData()?.Teams == null) return;

            var teams = _showAllSeasons
                ? _dataStore.GetData().Teams.Where(t => t != null).OrderBy(t => t.Name ?? "").ToList()
                : _dataStore.GetData().Teams.Where(t => t != null && t.SeasonId == _currentSeasonId).OrderBy(t => t.Name ?? "").ToList();

            foreach (var t in teams) _teams.Add(t);
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading teams: {ex.Message}");
        }
    }

    private void SafeRefreshPlayers(string? search)
    {
        try
        {
            _items.Clear();
            if (!_showAllSeasons && !_currentSeasonId.HasValue)
            {
                SetStatus("No season selected - check 'Show all seasons' or activate a season");
                return;
            }
            if (_dataStore.GetData()?.Players == null)
            {
                SetStatus("No players data available");
                return;
            }

            var teamLookup = _showAllSeasons
                ? _dataStore.GetData().Teams?.Where(t => t != null).ToDictionary(t => t.Id, t => t.Name ?? "Unknown") ?? new Dictionary<Guid, string>()
                : _dataStore.GetData().Teams?.Where(t => t != null && t.SeasonId == _currentSeasonId).ToDictionary(t => t.Id, t => t.Name ?? "Unknown") ?? new Dictionary<Guid, string>();

            var playersQuery = _showAllSeasons
                ? _dataStore.GetData().Players.Where(p => p != null)
                : _dataStore.GetData().Players.Where(p => p != null && p.SeasonId == _currentSeasonId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                playersQuery = playersQuery.Where(p => (p.FirstName ?? "").ToLower().Contains(lower) || (p.LastName ?? "").ToLower().Contains(lower));
            }

            if (_showUnassigned)
                playersQuery = playersQuery.Where(p => !p.TeamId.HasValue);

            if (_hideInactive)
                playersQuery = playersQuery.Where(p => p.IsActive);

            string TeamName(Player p) => p.TeamId.HasValue && teamLookup.TryGetValue(p.TeamId.Value, out var n) ? n : "\uffff";

            var players = _sortOption switch
            {
                "Z \u2192 A" => playersQuery.OrderByDescending(p => p.LastName ?? "").ThenByDescending(p => p.FirstName ?? "").ToList(),
                "By Team" => playersQuery.OrderBy(TeamName).ThenBy(p => p.LastName ?? "").ThenBy(p => p.FirstName ?? "").ToList(),
                "Newest First" => playersQuery.OrderByDescending(p => p.Id).ToList(),
                _ => playersQuery.OrderBy(p => p.LastName ?? "").ThenBy(p => p.FirstName ?? "").ToList()
            };

            foreach (var p in players)
            {
                var teamLabel = p.TeamId.HasValue && teamLookup.TryGetValue(p.TeamId.Value, out var teamName) ? teamName : "";
                _items.Add(new PlayerListItem
                {
                    Id = p.Id,
                    First = p.FirstName,
                    Last = p.LastName,
                    TeamLabel = teamLabel,
                    IsActive = p.IsActive,
                    HasTransfers = p.TransferHistory != null && p.TransferHistory.Count != 0
                });
            }

            if (_showAllSeasons && players.Count != 0)
                SetStatus($"{_items.Count} player(s) across {players.GroupBy(p => p.SeasonId).Count()} season(s)");
            else if (players.Count != 0)
            {
                var season = _dataStore.GetData().Seasons?.FirstOrDefault(s => s.Id == _currentSeasonId);
                SetStatus($"{_items.Count} player(s){(season != null ? $" in {season.Name}" : "")}{(season != null && !season.IsActive ? " (Imported)" : "")}");
            }
            else
                SetStatus("No players found for the current season");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading players: {ex.Message}");
        }
    }

    private void OnShowAllSeasonsTapped(object? sender, EventArgs e) => ShowAllSeasonsCheck.IsChecked = !ShowAllSeasonsCheck.IsChecked;

    private void OnShowUnassignedTapped(object? sender, EventArgs e) => ShowUnassignedCheck.IsChecked = !ShowUnassignedCheck.IsChecked;

    private void OnHideInactiveTapped(object? sender, EventArgs e) => HideInactiveCheck.IsChecked = !HideInactiveCheck.IsChecked;

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
                RefreshHeadToHead();
                return;
            }
            _selected = _dataStore.GetData()?.Players?.FirstOrDefault(p => p.Id == item.Id);
            if (_selected == null) 
            { 
                SetStatus("Player not found"); 
                ClearEditor();
                return; 
            }
            LoadEditor(_selected);
            RefreshHeadToHead();
        }
        catch (Exception ex)
        {
            SetStatus($"Selection error: {ex.Message}");
        }
    }

    private void LoadEditor(Player player)
    {
        if (player == null)
        {
            ClearEditor();
            return;
        }
        
        try
        {
            FirstNameEntry.Text = player.FirstName ?? "";
            LastNameEntry.Text = player.LastName ?? "";
            NotesEntry.Text = player.Notes ?? "";
            IsActiveSwitch.IsToggled = player.IsActive;

            if (!player.IsActive)
            {
                var info = player.DeactivatedDate.HasValue ? $"Deactivated: {player.DeactivatedDate.Value:dd MMM yyyy}" : "Inactive";
                if (!string.IsNullOrEmpty(player.DeactivationReason)) info += $" - {player.DeactivationReason}";
                DeactivationInfoLabel.Text = $"?? {info}";
                DeactivationInfoLabel.IsVisible = true;
            }
            else
                DeactivationInfoLabel.IsVisible = false;

            SafeRefreshTeams();
            TeamPicker.SelectedItem = player.TeamId.HasValue ? _teams.FirstOrDefault(t => t.Id == player.TeamId) : null;
            TransferBtn.IsEnabled = player.TeamId.HasValue;
            RefreshTransferHistory(player);
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading player: {ex.Message}");
        }
    }

    private void RefreshTransferHistory(Player player)
    {
        _transferHistory.Clear();
        TransferHistorySection.IsVisible = false;
        if (player.TransferHistory == null || player.TransferHistory.Count == 0) return;

        TransferHistorySection.IsVisible = true;
        foreach (var transfer in player.TransferHistory.OrderByDescending(t => t.TransferDate))
        {
            _transferHistory.Add(new TransferHistoryItem
            {
                TransferSummary = $"{transfer.FromTeamName} ? {transfer.ToTeamName}",
                TransferDetails = $"{transfer.TransferDate:dd MMM yyyy} � Rating: {transfer.RatingAtTransfer} � {transfer.WinsAtTransfer}W-{transfer.LossesAtTransfer}L ({transfer.FramesPlayedAtTransfer} frames)"
            });
        }
    }

    private void ClearEditor()
    {
        FirstNameEntry.Text = "";
        LastNameEntry.Text = "";
        NotesEntry.Text = "";
        TeamPicker.SelectedIndex = -1;
        IsActiveSwitch.IsToggled = true;
        DeactivationInfoLabel.IsVisible = false;
        TransferBtn.IsEnabled = false;
        _transferHistory.Clear();
        TransferHistorySection.IsVisible = false;
    }

    private async void OnAdd(object? sender, EventArgs e)
    {
        try
        {
            if (!_currentSeasonId.HasValue) { SetStatus("Please select a season first"); return; }
            if (_dataStore.GetData().IsSeasonLocked(_currentSeasonId))
            {
                await DisplayAlert($"{Emojis.Lock} Season Locked",
                    "Cannot add players � this season is locked.", "OK");
                return;
            }
            var first = FirstNameEntry.Text?.Trim() ?? "";
            var last = LastNameEntry.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(last)) { SetStatus("Name required"); return; }

            var player = new Player
            {
                SeasonId = _currentSeasonId.Value,
                FirstName = first,
                LastName = last,
                TeamId = (TeamPicker.SelectedItem as Team)?.Id,
                Notes = NotesEntry.Text?.Trim()
            };
            _dataStore.GetData().Players.Add(player);
            await _dataStore.SaveAsync();
            SafeRefreshPlayers(SearchEntry?.Text);
            SetStatus($"Added: {player.FullName}");
        }
        catch (Exception ex) { SetStatus($"Add failed: {ex.Message}"); }
    }

    private async void OnUpdate(object? sender, EventArgs e)
    {
        try
        {
            if (_selected == null) { SetStatus("No player selected"); return; }
            if (_dataStore.GetData().IsSeasonLocked(_selected.SeasonId))
            {
                await DisplayAlert($"{Emojis.Lock} Season Locked",
                    "Cannot edit players � this season is locked.", "OK");
                return;
            }

            _selected.FirstName = FirstNameEntry.Text?.Trim() ?? "";
            _selected.LastName = LastNameEntry.Text?.Trim() ?? "";
            _selected.Notes = NotesEntry.Text?.Trim();

            var wasActive = _selected.IsActive;
            _selected.IsActive = IsActiveSwitch.IsToggled;
            if (wasActive && !_selected.IsActive)
                _selected.DeactivatedDate = DateTime.Now;
            else if (!wasActive && _selected.IsActive)
            {
                _selected.DeactivatedDate = null;
                _selected.DeactivationReason = null;
            }

            var selectedTeam = TeamPicker.SelectedItem as Team;
            if (selectedTeam != null && selectedTeam.SeasonId != _selected.SeasonId)
            {
                await DisplayAlert($"{Emojis.Warning} Wrong Season",
                    $"The team '{selectedTeam.Name}' belongs to a different season. Please select a team from this player's season.", "OK");
                return;
            }
            _selected.TeamId = selectedTeam?.Id;
            var updatedName = _selected.FullName;
            await _dataStore.SaveAsync();
            SafeRefreshPlayers(SearchEntry?.Text);
            RefreshHeadToHead();
            SetStatus($"Updated: {updatedName}");
        }
        catch (Exception ex) { SetStatus($"Update failed: {ex.Message}"); }
    }

    private async void OnDelete(object? sender, EventArgs e)
    {
        try
        {
            if (_selected == null) { SetStatus("No player selected"); return; }
            if (_dataStore.GetData().IsSeasonLocked(_selected.SeasonId))
            {
                await DisplayAlert($"{Emojis.Lock} Season Locked",
                    "Cannot delete players � this season is locked.", "OK");
                return;
            }
            if (!await DisplayAlert($"{Emojis.Warning} Delete Player", $"Delete '{_selected.FullName}'?", "Yes", "No")) return;

            _dataStore.GetData().Players.Remove(_selected);
            _selected = null;
            await _dataStore.SaveAsync();
            SafeRefreshPlayers(SearchEntry?.Text);
            ClearEditor();
            RefreshHeadToHead();
            SetStatus($"{Emojis.Success} Deleted");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", $"Delete failed: {ex.Message}", "OK");
        }
    }

    private void OnToggleMultiSelect(object? sender, EventArgs e)
    {
        _isMultiSelectMode = !_isMultiSelectMode;
        if (_isMultiSelectMode)
        {
            PlayersList.SelectionMode = SelectionMode.Multiple;
            MultiSelectBtn.Text = "? Multi-Select ON";
            MultiSelectBtn.BackgroundColor = Color.FromArgb("#10B981");
            BulkAssignTeamBtn.IsVisible = true;
            BulkDeleteBtn.IsVisible = true;
            UpdateBtn.IsEnabled = DeleteBtn.IsEnabled = AddBtn.IsEnabled = false;
        }
        else
        {
            PlayersList.SelectionMode = SelectionMode.Single;
            MultiSelectBtn.Text = "? Multi-Select OFF";
            MultiSelectBtn.BackgroundColor = Color.FromArgb("#6B7280");
            BulkAssignTeamBtn.IsVisible = false;
            BulkDeleteBtn.IsVisible = false;
            UpdateBtn.IsEnabled = DeleteBtn.IsEnabled = AddBtn.IsEnabled = true;
        }
        SetStatus(_isMultiSelectMode ? "Multi-select enabled" : "Multi-select disabled");
    }

    private async void OnBulkDelete(object? sender, EventArgs e)
    {
        try
        {
            if (_dataStore.GetData().IsSeasonLocked(_currentSeasonId))
            {
                await DisplayAlert($"{Emojis.Lock} Season Locked",
                    "Cannot delete players � this season is locked.", "OK");
                return;
            }
            var selectedItems = PlayersList.SelectedItems?.Cast<PlayerListItem>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                await DisplayAlert($"{Emojis.Info} No Selection", "Please select players to delete.", "OK");
                return;
            }
            if (!await DisplayAlert($"{Emojis.Warning} Bulk Delete", $"Delete {selectedItems.Count} player(s)?", "Yes, Delete", "Cancel")) return;

            int deleted = 0;
            foreach (var item in selectedItems)
            {
                var player = _dataStore.GetData()?.Players?.FirstOrDefault(p => p.Id == item.Id);
                if (player != null) { _dataStore.GetData()?.Players?.Remove(player); deleted++; }
            }
            await _dataStore.SaveAsync();
            SafeRefreshPlayers(SearchEntry?.Text);
            SetStatus($"{Emojis.Success} Deleted {deleted} player(s)");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", $"Bulk delete failed: {ex.Message}", "OK");
        }
    }

    private async Task ExportPlayersAsync()
    {
        try
        {
            if (!_currentSeasonId.HasValue)
            {
                await DisplayAlert($"{Emojis.Info} No Season", "Please select a season first.", "OK");
                return;
            }

            var season = _dataStore.GetData()?.Seasons?.FirstOrDefault(s => s.Id == _currentSeasonId);
            var csv = new StringBuilder();
            csv.AppendLine("FirstName,LastName,Team,Active,Notes");

            var players = _dataStore.GetData()?.Players?.Where(p => p != null && p.SeasonId == _currentSeasonId).OrderBy(p => p.LastName ?? "").ToList() ?? new List<Player>();
            var teamLookup = _dataStore.GetData()?.Teams?.Where(t => t != null && t.SeasonId == _currentSeasonId).ToDictionary(t => t.Id, t => t.Name ?? "") ?? new Dictionary<Guid, string>();

            foreach (var p in players)
            {
                var teamName = p.TeamId.HasValue && teamLookup.TryGetValue(p.TeamId.Value, out var name) ? name : "";
                csv.AppendLine($"\"{p.FirstName}\",\"{p.LastName}\",\"{teamName}\",{p.IsActive},\"{p.Notes}\"");
            }

            var fileName = $"Players_{season?.Name?.Replace(" ", "_") ?? "Unknown"}_{DateTime.Now:yyyyMMdd}.csv";
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(path, csv.ToString());
            await Share.RequestAsync(new ShareFileRequest { Title = "Export Players", File = new ShareFile(path) });
            SetStatus($"{Emojis.Success} Exported {players.Count} players");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", $"Export failed: {ex.Message}", "OK");
        }
    }

    private async Task ImportPlayersCsvAsync(Stream stream, string fileName)
    {
        try
        {
            if (!_currentSeasonId.HasValue)
            {
                await DisplayAlert($"{Emojis.Info} No Season", "Please select a season before importing.", "OK");
                return;
            }

            var rows = Csv.Read(stream);
            int added = 0, updated = 0;
            var teams = _dataStore.GetData()?.Teams?.Where(t => t != null && t.SeasonId == _currentSeasonId && !string.IsNullOrWhiteSpace(t.Name))
                .ToDictionary(t => t.Name!.Trim(), t => t, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, Team>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in rows)
            {
                var first = r.Get("FirstName") ?? "";
                var last = r.Get("LastName") ?? "";
                if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last)) continue;

                var existing = _dataStore.GetData()?.Players?.FirstOrDefault(p => p != null && p.SeasonId == _currentSeasonId &&
                    string.Equals(p.FirstName, first, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.LastName, last, StringComparison.OrdinalIgnoreCase));

                var teamName = r.Get("Team");
                var notes = r.Get("Notes");
                var isActiveStr = r.Get("Active");
                var isActive = string.IsNullOrEmpty(isActiveStr) || isActiveStr.Equals("true", StringComparison.OrdinalIgnoreCase);

                if (existing == null)
                {
                    var player = new Player
                    {
                        SeasonId = _currentSeasonId.Value,
                        FirstName = first.Trim(),
                        LastName = last.Trim(),
                        TeamId = !string.IsNullOrWhiteSpace(teamName) && teams.TryGetValue(teamName, out var team) ? team.Id : null,
                        Notes = notes,
                        IsActive = isActive
                    };
                    _dataStore.GetData()?.Players?.Add(player);
                    added++;
                }
                else
                {
                    existing.TeamId = !string.IsNullOrWhiteSpace(teamName) && teams.TryGetValue(teamName, out var team) ? team.Id : null;
                    existing.Notes = notes;
                    updated++;
                }
            }
            await _dataStore.SaveAsync();
            SafeRefreshPlayers(SearchEntry?.Text);
            SetStatus($"{Emojis.Success} Imported: {added} added, {updated} updated");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", $"Import failed: {ex.Message}", "OK");
        }
    }

    private async Task OnTransferPlayerAsync()
    {
        try
        {
            if (_selected == null)
            {
                await DisplayAlert($"{Emojis.Info} No Player Selected", "Please select a player to transfer.", "OK");
                return;
            }
            if (_dataStore.GetData().IsSeasonLocked(_selected.SeasonId))
            {
                await DisplayAlert($"{Emojis.Lock} Season Locked",
                    "Cannot transfer players � this season is locked.", "OK");
                return;
            }
            if (!_selected.TeamId.HasValue)
            {
                await DisplayAlert($"{Emojis.Info} No Current Team", "This player is not assigned to a team.", "OK");
                return;
            }

            var currentTeam = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == _selected.TeamId);
            if (currentTeam == null)
            {
                await DisplayAlert($"{Emojis.Error} Error", "Could not find the player's current team.", "OK");
                return;
            }

            var availableTeams = _dataStore.GetData().Teams?
                .Where(t => t != null && t.SeasonId == _selected.SeasonId && t.Id != currentTeam.Id)
                .OrderBy(t => t.Name).ToList() ?? new List<Team>();

            if (availableTeams.Count == 0)
            {
                await DisplayAlert($"{Emojis.Info} No Teams Available", "There are no other teams in this season to transfer to.", "OK");
                return;
            }

            var teamNames = availableTeams.Select(t => t.Name ?? "Unknown").ToArray();
            var selectedTeamName = await DisplayActionSheet($"Transfer {_selected.FullName} to:", "Cancel", null, teamNames);
            if (string.IsNullOrEmpty(selectedTeamName) || selectedTeamName == "Cancel") return;

            var newTeam = availableTeams.FirstOrDefault(t => t.Name == selectedTeamName);
            if (newTeam == null) return;

            // Calculate current stats - with null safety
            int currentRating = _dataStore.GetData().GetSettingsForSeason(_selected.SeasonId)?.RatingStartValue ?? 1000;
            int framesPlayed = 0, wins = 0, losses = 0;

            try
            {
                var seasonIds = new List<Guid>();
                if (_selected.SeasonId.HasValue) seasonIds.Add(_selected.SeasonId.Value);

                var fixtures = _dataStore.GetData().Fixtures?.Where(f => seasonIds.Contains(f.SeasonId ?? Guid.Empty)).ToList() ?? new List<Fixture>();
                var settings = _dataStore.GetData().GetSettingsForSeason(_selected.SeasonId);
                var season = _dataStore.GetData().Seasons?.FirstOrDefault(s => s.Id == _selected.SeasonId);
                var seasonStartDate = season?.StartDate ?? DateTime.Now.AddMonths(-6);

                if (fixtures.Count != 0)
                {
                    var allPlayers = _dataStore.GetData().Players?.ToList() ?? new List<Player>();
                    var allTeams = _dataStore.GetData().Teams?.ToList() ?? new List<Team>();
                    
                    var ratings = RatingCalculator.CalculateAllRatings(
                        fixtures, 
                        allPlayers, 
                        allTeams, 
                        settings, 
                        seasonStartDate);

                    if (ratings.TryGetValue(_selected.Id, out var stats))
                    {
                        currentRating = stats.Rating;
                        framesPlayed = stats.Played;
                        wins = stats.Wins;
                        losses = stats.Losses;
                    }
                }
            }
            catch (Exception ratingEx)
            {
                System.Diagnostics.Debug.WriteLine($"Rating calculation error (non-fatal): {ratingEx.Message}");
            }

            var confirmMessage = $"Transfer {_selected.FullName} from {currentTeam.Name} to {newTeam.Name}?\n\n" +
                                 $"Current Stats:\n� Rating: {currentRating}\n� Frames: {framesPlayed} ({wins}W-{losses}L)\n\n" +
                                 $"These stats will be preserved in the transfer history.";

            if (!await DisplayAlert("?? Confirm Transfer", confirmMessage, "Transfer", "Cancel")) return;

            var transfer = new PlayerTransfer
            {
                FromTeamId = currentTeam.Id,
                FromTeamName = currentTeam.Name ?? "Unknown",
                ToTeamId = newTeam.Id,
                ToTeamName = newTeam.Name ?? "Unknown",
                TransferDate = DateTime.Now,
                RatingAtTransfer = currentRating,
                FramesPlayedAtTransfer = framesPlayed,
                WinsAtTransfer = wins,
                LossesAtTransfer = losses
            };

            _selected.TransferHistory ??= new List<PlayerTransfer>();
            _selected.TransferHistory.Add(transfer);
            _selected.TeamId = newTeam.Id;

            // Cache values before refresh (which clears selection and sets _selected to null)
            var playerName = _selected.FullName;
            var newTeamName = newTeam.Name ?? "Unknown";

            await _dataStore.SaveAsync();
            SafeRefreshPlayers(SearchEntry?.Text);
            LoadEditor(_selected);
            RefreshHeadToHead();

            SetStatus($"? Transferred {playerName} to {newTeamName}");
            await DisplayAlert($"{Emojis.Success} Transfer Complete",
                $"{playerName} has been transferred to {newTeamName}.\n\nTheir rating ({currentRating}) and previous results have been preserved.", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnTransferPlayerAsync Error: {ex}");
            await DisplayAlert($"{Emojis.Error} Error", $"Transfer failed: {ex.Message}", "OK");
            SetStatus($"Transfer failed: {ex.Message}");
        }
    }

    private async void OnBulkAssignTeam(object? sender, EventArgs e)
    {
        try
        {
            if (_dataStore.GetData().IsSeasonLocked(_currentSeasonId))
            {
                await DisplayAlert($"{Emojis.Lock} Season Locked",
                    "Cannot assign players \u2014 this season is locked.", "OK");
                return;
            }
            var selectedItems = PlayersList.SelectedItems?.Cast<PlayerListItem>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                await DisplayAlert($"{Emojis.Info} No Selection", "Please select players to assign.", "OK");
                return;
            }

            var availableTeams = _dataStore.GetData().Teams?
                .Where(t => t != null && t.SeasonId == _currentSeasonId)
                .OrderBy(t => t.Name ?? "").ToList() ?? [];

            if (availableTeams.Count == 0)
            {
                await DisplayAlert($"{Emojis.Info} No Teams", "No teams exist for this season. Create teams first.", "OK");
                return;
            }

            var teamNames = availableTeams.Select(t => t.Name ?? "Unknown").ToArray();
            var chosen = await DisplayActionSheet(
                $"Assign {selectedItems.Count} player(s) to team:", "Cancel", "� No Team �", teamNames);
            if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return;

            Guid? newTeamId = null;
            string teamLabel;
            if (chosen != "� No Team �")
            {
                var team = availableTeams.FirstOrDefault(t => t.Name == chosen);
                if (team == null) return;
                newTeamId = team.Id;
                teamLabel = team.Name ?? "Unknown";
            }
            else
            {
                teamLabel = "no team";
            }

            int count = 0;
            foreach (var item in selectedItems)
            {
                var player = _dataStore.GetData().Players?.FirstOrDefault(p => p.Id == item.Id);
                if (player != null)
                {
                    player.TeamId = newTeamId;
                    count++;
                }
            }

            await _dataStore.SaveAsync();
            SafeRefreshPlayers(SearchEntry?.Text);
            SetStatus($"{Emojis.Success} Assigned {count} player(s) to {teamLabel}");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Emojis.Error} Error", $"Bulk assign failed: {ex.Message}", "OK");
        }
    }

    private async void OnPlayerTeamLabelTapped(object? sender, EventArgs e)
    {
        try
        {
            if (_isMultiSelectMode) return;

            var label = sender as Label;
            var item = label?.BindingContext as PlayerListItem;
            if (item == null) return;

            if (_dataStore.GetData().IsSeasonLocked(_currentSeasonId))
            {
                await DisplayAlert($"{Emojis.Lock} Season Locked",
                    "Cannot change team \u2014 this season is locked.", "OK");
                return;
            }

            var player = _dataStore.GetData().Players?.FirstOrDefault(p => p.Id == item.Id);
            if (player == null) return;

            var availableTeams = _dataStore.GetData().Teams?
                .Where(t => t != null && t.SeasonId == player.SeasonId)
                .OrderBy(t => t.Name ?? "").ToList() ?? [];

            if (availableTeams.Count == 0)
            {
                await DisplayAlert($"{Emojis.Info} No Teams", "No teams exist for this season.", "OK");
                return;
            }

            var teamNames = availableTeams.Select(t => t.Name ?? "Unknown").ToArray();
            var chosen = await DisplayActionSheet(
                $"Assign {player.FullName} to:", "Cancel", "� No Team �", teamNames);
            if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return;

            if (chosen == "� No Team �")
            {
                player.TeamId = null;
            }
            else
            {
                var team = availableTeams.FirstOrDefault(t => t.Name == chosen);
                if (team == null) return;
                player.TeamId = team.Id;
            }

            await _dataStore.SaveAsync();
            SafeRefreshPlayers(SearchEntry?.Text);

            // Re-select in editor if this was the active player
            if (_selected?.Id == player.Id)
                LoadEditor(player);

            SetStatus($"Assigned {player.FullName} to {chosen}");
        }
        catch (Exception ex)
        {
            SetStatus($"Quick assign failed: {ex.Message}");
        }
    }

    private void SetStatus(string msg)
    {
        try { if (StatusLbl != null) StatusLbl.Text = $"{DateTime.Now:HH:mm:ss} {msg}"; } catch { }
    }

    private void OnActiveStatusToggled(object? sender, ToggledEventArgs e)
    {
        if (!e.Value)
        {
            DeactivationInfoLabel.Text = "?? Player will be marked as inactive but their results will be preserved.";
            DeactivationInfoLabel.IsVisible = true;
        }
        else
            DeactivationInfoLabel.IsVisible = false;
    }
}
