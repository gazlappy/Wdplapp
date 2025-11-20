using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class TeamsPage : ContentPage
{
    public sealed class TeamListItem
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string VenueName { get; set; } = "";
        public string TableLabel { get; set; } = "";
    }

    public sealed class TeamHeadToHeadItem
    {
        public Guid OpponentId { get; set; }
        public string OpponentName { get; set; } = "";
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public int TotalMatches => Wins + Draws + Losses;
        public double WinPercentage => TotalMatches == 0 ? 0 : (double)Wins / TotalMatches * 100.0;
        public int FramesFor { get; set; }
        public int FramesAgainst { get; set; }
        public int PointsFor { get; set; }
        public int PointsAgainst { get; set; }
        public string RecordText => Draws > 0 ? $"{Wins}-{Draws}-{Losses}" : $"{Wins}-{Losses}";
        public string FrameRecord => $"{FramesFor}-{FramesAgainst}";
        public string PointsRecord => $"{PointsFor}-{PointsAgainst}";
        public Color RecordColor => Wins > Losses ? Color.FromArgb("#10B981") : 
                                    Wins < Losses ? Color.FromArgb("#EF4444") : 
                                    Color.FromArgb("#6B7280");
        public List<TeamSeasonRecord> SeasonBreakdown { get; set; } = new();
        public bool HasMultipleSeasons => SeasonBreakdown.Count > 1;
    }

    public sealed class TeamSeasonRecord
    {
        public string SeasonName { get; set; } = "";
        public string MatchRecord { get; set; } = "";
        public string FrameRecord { get; set; } = "";
    }

    private readonly ObservableCollection<TeamListItem> _teamItems = new();
    private readonly ObservableCollection<Division> _divisions = new();
    private readonly ObservableCollection<Venue> _venues = new();
    private readonly ObservableCollection<VenueTable> _tables = new();
    private readonly ObservableCollection<Player> _players = new();
    private readonly ObservableCollection<TeamHeadToHeadItem> _h2hItems = new();
    private readonly ObservableCollection<Season> _h2hSeasons = new();

    private Team? _selectedTeam;
    private bool _isMultiSelectMode = false;
    private Guid? _currentSeasonId;
    private bool _isFlyoutOpen = false;

    public TeamsPage()
    {
        InitializeComponent();

        TeamsList.ItemsSource = _teamItems;
        DivisionPicker.ItemsSource = _divisions;
        VenuePicker.ItemsSource = _venues;
        TablePicker.ItemsSource = _tables;
        CaptainPicker.ItemsSource = _players;
        H2HList.ItemsSource = _h2hItems;
        H2HSeasonPicker.ItemsSource = _h2hSeasons;

        // Burger menu
        BurgerMenuBtn.Clicked += OnBurgerMenuClicked;
        CloseFlyoutBtn.Clicked += OnCloseFlyoutClicked;
        OverlayTap.Tapped += (_, __) => CloseFlyout();

        SearchEntry.TextChanged += (_, __) => RefreshTeamList(SearchEntry.Text);
        TeamsList.SelectionChanged += OnTeamSelected;
        VenuePicker.SelectedIndexChanged += (_, __) => RefreshTablesForSelectedVenue();
        H2HSeasonPicker.SelectedIndexChanged += (_, __) => RefreshHeadToHead();

        AddBtn.Clicked += OnAdd;
        UpdateBtn.Clicked += OnUpdate;
        DeleteBtn.Clicked += OnDelete;
        MultiSelectBtn.Clicked += OnToggleMultiSelect;
        BulkDeleteBtn.Clicked += OnBulkDelete;

        SaveBtn.Clicked += async (_, __) =>
        {
            DataStore.Save();
            await DisplayAlert("Saved", "All changes have been saved.", "OK");
            SetStatus("Saved.");
        };

        ReloadBtn.Clicked += (_, __) =>
        {
            DataStore.Load();
            RefreshAll();
            SetStatus("Reloaded.");
        };

        ExportBtn.Clicked += async (_, __) => await ExportTeamsAsync();
        TeamsImport.ImportRequested += async (stream, fileName) => await ImportTeamsCsvAsync(stream, fileName);

        // SUBSCRIBE to global season changes
        SeasonService.SeasonChanged += OnGlobalSeasonChanged;

        RefreshAll();
    }

    ~TeamsPage()
    {
        SeasonService.SeasonChanged -= OnGlobalSeasonChanged;
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
            System.Diagnostics.Debug.WriteLine($"TeamsPage OnAppearing Error: {ex}");
            SetStatus($"Error loading data: {ex.Message}");
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

            if (_selectedTeam == null)
            {
                TeamInfoPanel.IsVisible = false;
                FilterPanel.IsVisible = false;
                EmptyStatePanel.IsVisible = true;
                H2HList.IsVisible = false;
                return;
            }

            // Show team info
            TeamInfoPanel.IsVisible = true;
            FilterPanel.IsVisible = true;
            EmptyStatePanel.IsVisible = false;
            H2HList.IsVisible = true;

            SelectedTeamName.Text = _selectedTeam.Name ?? "Unknown Team";
            
            var division = DataStore.Data.Divisions?.FirstOrDefault(d => d.Id == _selectedTeam.DivisionId);
            SelectedTeamDivision.Text = division?.Name ?? "No division assigned";

            // Determine which seasons to include
            List<Guid> seasonIds = new();
            var selectedSeason = H2HSeasonPicker.SelectedItem as Season;
            
            if (selectedSeason != null)
            {
                seasonIds.Add(selectedSeason.Id);
            }
            else
            {
                // All seasons
                seasonIds = DataStore.Data.Seasons
                    .Select(s => s.Id)
                    .ToList();
            }

            // Build head-to-head records
            var h2hData = new Dictionary<Guid, TeamHeadToHeadItem>();
            var seasonRecords = new Dictionary<Guid, Dictionary<Guid, (int w, int d, int l, int ff, int fa, int pf, int pa)>>();

            // Get all fixtures for the selected seasons involving this team
            var fixtures = DataStore.Data.Fixtures
                .Where(f => seasonIds.Contains(f.SeasonId ?? Guid.Empty))
                .Where(f => f.HomeTeamId == _selectedTeam.Id || f.AwayTeamId == _selectedTeam.Id)
                .Where(f => f.Frames.Any()) // Only completed matches
                .ToList();

            int totalMatches = 0;
            int totalWins = 0;
            int totalDraws = 0;
            int totalLosses = 0;
            int totalFramesFor = 0;
            int totalFramesAgainst = 0;

            foreach (var fixture in fixtures)
            {
                var isHome = fixture.HomeTeamId == _selectedTeam.Id;
                var opponentId = isHome ? fixture.AwayTeamId : fixture.HomeTeamId;

                var homeScore = fixture.HomeScore;
                var awayScore = fixture.AwayScore;

                var ourScore = isHome ? homeScore : awayScore;
                var theirScore = isHome ? awayScore : homeScore;

                totalFramesFor += ourScore;
                totalFramesAgainst += theirScore;
                totalMatches++;

                bool won = ourScore > theirScore;
                bool drew = ourScore == theirScore;
                bool lost = ourScore < theirScore;

                if (won) totalWins++;
                else if (drew) totalDraws++;
                else totalLosses++;

                // Calculate points (using settings)
                var settings = DataStore.Data.Settings;
                int ourPoints = ourScore; // Frames won
                int theirPoints = theirScore;

                if (won)
                    ourPoints += settings.MatchWinBonus;
                else if (lost)
                    theirPoints += settings.MatchWinBonus;
                else
                {
                    ourPoints += settings.MatchDrawBonus;
                    theirPoints += settings.MatchDrawBonus;
                }

                // Overall head-to-head
                if (!h2hData.ContainsKey(opponentId))
                {
                    var opponent = DataStore.Data.Teams?.FirstOrDefault(t => t.Id == opponentId);
                    h2hData[opponentId] = new TeamHeadToHeadItem
                    {
                        OpponentId = opponentId,
                        OpponentName = opponent?.Name ?? "Unknown Team"
                    };
                }

                if (won)
                    h2hData[opponentId].Wins++;
                else if (drew)
                    h2hData[opponentId].Draws++;
                else
                    h2hData[opponentId].Losses++;

                h2hData[opponentId].FramesFor += ourScore;
                h2hData[opponentId].FramesAgainst += theirScore;
                h2hData[opponentId].PointsFor += ourPoints;
                h2hData[opponentId].PointsAgainst += theirPoints;

                // Season breakdown (only if showing all seasons)
                if (selectedSeason == null && fixture.SeasonId.HasValue)
                {
                    var seasonId = fixture.SeasonId.Value;
                    if (!seasonRecords.ContainsKey(opponentId))
                        seasonRecords[opponentId] = new Dictionary<Guid, (int, int, int, int, int, int, int)>();

                    if (!seasonRecords[opponentId].ContainsKey(seasonId))
                        seasonRecords[opponentId][seasonId] = (0, 0, 0, 0, 0, 0, 0);

                    var current = seasonRecords[opponentId][seasonId];
                    seasonRecords[opponentId][seasonId] = won
                        ? (current.w + 1, current.d, current.l, current.ff + ourScore, current.fa + theirScore, current.pf + ourPoints, current.pa + theirPoints)
                        : drew
                        ? (current.w, current.d + 1, current.l, current.ff + ourScore, current.fa + theirScore, current.pf + ourPoints, current.pa + theirPoints)
                        : (current.w, current.d, current.l + 1, current.ff + ourScore, current.fa + theirScore, current.pf + ourPoints, current.pa + theirPoints);
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
                        var record = seasonKvp.Value;
                        kvp.Value.SeasonBreakdown.Add(new TeamSeasonRecord
                        {
                            SeasonName = season?.Name ?? "Unknown Season",
                            MatchRecord = record.d > 0 ? $"{record.w}-{record.d}-{record.l}" : $"{record.w}-{record.l}",
                            FrameRecord = $"{record.ff}-{record.fa}"
                        });
                    }
                }
            }

            // Sort by total matches played (most frequent opponents first)
            var sortedH2H = h2hData.Values
                .OrderByDescending(h => h.TotalMatches)
                .ThenByDescending(h => h.WinPercentage)
                .ToList();

            foreach (var item in sortedH2H)
                _h2hItems.Add(item);

            // Update team stats
            var winPct = totalMatches > 0 ? (double)totalWins / totalMatches * 100.0 : 0;
            var recordText = totalDraws > 0 
                ? $"{totalWins}W-{totalDraws}D-{totalLosses}L" 
                : $"{totalWins}W-{totalLosses}L";
            SelectedTeamStats.Text = $"{totalMatches} matches • {recordText} ({winPct:0.#}%) • {totalFramesFor}-{totalFramesAgainst} frames";

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
                RefreshTeamList(SearchEntry?.Text);
                RefreshH2HSeasons();
                SetStatus($"Season changed to: {e.NewSeason?.Name ?? "None"}");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TeamsPage Season change error: {ex}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetStatus($"Error changing season: {ex.Message}");
            });
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

            RefreshTeamList(SearchEntry?.Text);
            RefreshH2HSeasons();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TeamsPage RefreshAll Error: {ex}");
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

    private void RefreshTeamList(string? search)
    {
        try
        {
            _teamItems.Clear();

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
                .Where(t => t != null && t.SeasonId == _currentSeasonId.Value)
                .OrderBy(t => t.Name ?? "")
                .ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                teams = teams.Where(t => (t.Name ?? "").ToLower().Contains(lower))
                    .OrderBy(t => t.Name ?? "")
                    .ToList();
            }

            var venueLookup = DataStore.Data.Venues?
                .Where(v => v != null && v.SeasonId == _currentSeasonId)
                .ToDictionary(v => v.Id, v => v)
                ?? new Dictionary<Guid, Venue>();

            foreach (var t in teams)
            {
                var venue = t.VenueId.HasValue && venueLookup.TryGetValue(t.VenueId.Value, out var v) ? v : null;
                var table = venue != null && t.TableId.HasValue
                    ? venue.Tables?.FirstOrDefault(tb => tb.Id == t.TableId)
                    : null;

                _teamItems.Add(new TeamListItem
                {
                    Id = t.Id,
                    Name = t.Name,
                    VenueName = venue?.Name ?? "",
                    TableLabel = table?.Label ?? ""
                });
            }

            var season = DataStore.Data.Seasons?.FirstOrDefault(s => s.Id == _currentSeasonId);
            var seasonInfo = season != null ? $" in {season.Name}" : "";
            var importedTag = season != null && !season.IsActive ? " (Imported)" : "";
            SetStatus($"{_teamItems.Count} team(s){seasonInfo}{importedTag}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TeamsPage RefreshTeamList Error: {ex}");
            SetStatus($"Error loading teams: {ex.Message}");
        }
    }

    private void OnTeamSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_isMultiSelectMode) return;

        var item = e.CurrentSelection?.FirstOrDefault() as TeamListItem;
        if (item == null)
        {
            _selectedTeam = null;
            ClearEditor();
            RefreshHeadToHead(); // Clear H2H
            return;
        }

        _selectedTeam = DataStore.Data.Teams.FirstOrDefault(t => t.Id == item.Id);
        if (_selectedTeam == null) return;

        LoadEditor(_selectedTeam);
        RefreshHeadToHead(); // Load H2H for selected team
    }

    private void LoadEditor(Team team)
    {
        TeamNameEntry.Text = team.Name;
        FoodSwitch.IsToggled = team.ProvidesFood;

        RefreshDivisions();
        RefreshVenues();
        RefreshPlayers();

        DivisionPicker.SelectedItem = _divisions.FirstOrDefault(d => d.Id == team.DivisionId);
        VenuePicker.SelectedItem = _venues.FirstOrDefault(v => v.Id == team.VenueId);

        RefreshTablesForSelectedVenue();
        TablePicker.SelectedItem = _tables.FirstOrDefault(t => t.Id == team.TableId);

        CaptainPicker.SelectedItem = _players.FirstOrDefault(p => p.Id == team.CaptainPlayerId);
    }

    private void ClearEditor()
    {
        TeamNameEntry.Text = "";
        FoodSwitch.IsToggled = false;
        DivisionPicker.SelectedIndex = -1;
        VenuePicker.SelectedIndex = -1;
        TablePicker.SelectedIndex = -1;
        CaptainPicker.SelectedIndex = -1;
    }

    private void RefreshDivisions()
    {
        _divisions.Clear();
        if (!_currentSeasonId.HasValue) return;

        foreach (var d in DataStore.Data.Divisions.Where(d => d.SeasonId == _currentSeasonId).OrderBy(d => d.Name))
            _divisions.Add(d);
    }

    private void RefreshVenues()
    {
        _venues.Clear();
        if (!_currentSeasonId.HasValue) return;

        foreach (var v in DataStore.Data.Venues.Where(v => v.SeasonId == _currentSeasonId).OrderBy(v => v.Name))
            _venues.Add(v);
    }

    private void RefreshPlayers()
    {
        _players.Clear();
        if (!_currentSeasonId.HasValue) return;

        foreach (var p in DataStore.Data.Players.Where(p => p.SeasonId == _currentSeasonId).OrderBy(p => p.FullName))
            _players.Add(p);
    }

    private void RefreshTablesForSelectedVenue()
    {
        _tables.Clear();
        if (VenuePicker.SelectedItem is not Venue v) return;

        foreach (var t in v.Tables)
            _tables.Add(t);
    }

    private void OnAdd(object? sender, EventArgs e)
    {
        if (!_currentSeasonId.HasValue)
        {
            SetStatus("Please select a season first on the Seasons page");
            return;
        }

        var name = TeamNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetStatus("Team name required");
            return;
        }

        var team = new Team
        {
            SeasonId = _currentSeasonId.Value,
            Name = name,
            DivisionId = (DivisionPicker.SelectedItem as Division)?.Id,
            VenueId = (VenuePicker.SelectedItem as Venue)?.Id,
            TableId = (TablePicker.SelectedItem as VenueTable)?.Id,
            CaptainPlayerId = (CaptainPicker.SelectedItem as Player)?.Id,
            ProvidesFood = FoodSwitch.IsToggled
        };

        DataStore.Data.Teams.Add(team);
        RefreshTeamList(SearchEntry.Text);
        SetStatus($"Added: {name}");
    }

    private void OnUpdate(object? sender, EventArgs e)
    {
        if (_selectedTeam == null)
        {
            SetStatus("No team selected");
            return;
        }

        _selectedTeam.Name = TeamNameEntry.Text?.Trim();
        _selectedTeam.DivisionId = (DivisionPicker.SelectedItem as Division)?.Id;
        _selectedTeam.VenueId = (VenuePicker.SelectedItem as Venue)?.Id;
        _selectedTeam.TableId = (TablePicker.SelectedItem as VenueTable)?.Id;
        _selectedTeam.CaptainPlayerId = (CaptainPicker.SelectedItem as Player)?.Id;
        _selectedTeam.ProvidesFood = FoodSwitch.IsToggled;

        var updatedName = _selectedTeam.Name; // Store name before RefreshTeamList clears selection
        RefreshTeamList(SearchEntry.Text);
        RefreshHeadToHead(); // Refresh H2H with updated team info
        SetStatus($"Updated: {updatedName}");
    }

    private async void OnDelete(object? sender, EventArgs e)
    {
        if (_selectedTeam == null)
        {
            SetStatus("No team selected");
            return;
        }

        var confirm = await DisplayAlert("Delete Team", $"Delete '{_selectedTeam.Name}'?", "Yes", "No");
        if (!confirm) return;

        DataStore.Data.Teams.Remove(_selectedTeam);
        _selectedTeam = null;
        RefreshTeamList(SearchEntry.Text);
        ClearEditor();
        RefreshHeadToHead(); // Clear H2H
        SetStatus("Deleted");
    }

    private void OnToggleMultiSelect(object? sender, EventArgs e)
    {
        _isMultiSelectMode = !_isMultiSelectMode;

        if (_isMultiSelectMode)
        {
            TeamsList.SelectionMode = SelectionMode.Multiple;
            MultiSelectBtn.Text = "✓ Multi-Select ON";
            MultiSelectBtn.BackgroundColor = Color.FromArgb("#10B981");
            BulkDeleteBtn.IsVisible = true;

            UpdateBtn.IsEnabled = false;
            DeleteBtn.IsEnabled = false;
            AddBtn.IsEnabled = false;
        }
        else
        {
            TeamsList.SelectionMode = SelectionMode.Single;
            MultiSelectBtn.Text = "☐ Multi-Select OFF";
            MultiSelectBtn.BackgroundColor = Color.FromArgb("#6B7280");
            BulkDeleteBtn.IsVisible = false;

            UpdateBtn.IsEnabled = true;
            DeleteBtn.IsEnabled = true;
            AddBtn.IsEnabled = true;
        }

        SetStatus(_isMultiSelectMode ? "Multi-select enabled" : "Multi-select disabled");
    }

    private async void OnBulkDelete(object? sender, EventArgs e)
    {
        var selectedItems = TeamsList.SelectedItems?.Cast<TeamListItem>().ToList();

        if (selectedItems == null || selectedItems.Count == 0)
        {
            await DisplayAlert("No Selection", "Please select teams to delete.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Bulk Delete",
            $"Delete {selectedItems.Count} team(s)?",
            "Yes, Delete",
            "Cancel");

        if (!confirm) return;

        int deleted = 0;
        foreach (var item in selectedItems)
        {
            var team = DataStore.Data.Teams.FirstOrDefault(t => t.Id == item.Id);
            if (team != null)
            {
                DataStore.Data.Teams.Remove(team);
                deleted++;
            }
        }

        RefreshTeamList(SearchEntry.Text);
        SetStatus($"Deleted {deleted} team(s)");
    }

    private async Task ExportTeamsAsync()
    {
        if (!_currentSeasonId.HasValue)
        {
            await DisplayAlert("No Season", "Please select a season on the Seasons page first.", "OK");
            return;
        }

        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Name,Division,Venue,Table,Captain,ProvidesFood");

        var teams = DataStore.Data.Teams.Where(t => t.SeasonId == _currentSeasonId).OrderBy(t => t.Name);

        foreach (var t in teams)
        {
            var div = t.DivisionId.HasValue ? DataStore.Data.Divisions.FirstOrDefault(d => d.Id == t.DivisionId)?.Name : "";
            var venue = t.VenueId.HasValue ? DataStore.Data.Venues.FirstOrDefault(v => v.Id == t.VenueId)?.Name : "";
            var venueObj = t.VenueId.HasValue ? DataStore.Data.Venues.FirstOrDefault(v => v.Id == t.VenueId) : null;
            var table = venueObj != null && t.TableId.HasValue
                ? venueObj.Tables.FirstOrDefault(tb => tb.Id == t.TableId)?.Label
                : "";
            var captain = t.CaptainPlayerId.HasValue
                ? DataStore.Data.Players.FirstOrDefault(p => p.Id == t.CaptainPlayerId)?.FullName
                : "";

            csv.AppendLine($"\"{t.Name}\",\"{div}\",\"{venue}\",\"{table}\",\"{captain}\",{t.ProvidesFood}");
        }

        var fileName = $"Teams_{season?.Name?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
        await System.IO.File.WriteAllTextAsync(path, csv.ToString());

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Export Teams",
            File = new ShareFile(path)
        });

        SetStatus($"Exported {teams.Count()} teams");
    }

    private async Task ImportTeamsCsvAsync(System.IO.Stream stream, string fileName)
    {
        if (!_currentSeasonId.HasValue)
        {
            await DisplayAlert("No Season", "Please select a season on the Seasons page before importing.", "OK");
            return;
        }

        var rows = Csv.Read(stream);
        int added = 0, updated = 0;

        var divisions = DataStore.Data.Divisions.Where(d => d.SeasonId == _currentSeasonId)
            .ToDictionary(d => (d.Name ?? "").Trim(), d => d, StringComparer.OrdinalIgnoreCase);
        var venues = DataStore.Data.Venues.Where(v => v.SeasonId == _currentSeasonId)
            .ToDictionary(v => (v.Name ?? "").Trim(), v => v, StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            var name = r.Get("Name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            var existing = DataStore.Data.Teams.FirstOrDefault(t =>
                t.SeasonId == _currentSeasonId &&
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

            var divName = r.Get("Division");
            var venueName = r.Get("Venue");
            var providesFood = r.Get("ProvidesFood")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            if (existing == null)
            {
                var team = new Team
                {
                    SeasonId = _currentSeasonId.Value,
                    Name = name.Trim(),
                    DivisionId = divisions.TryGetValue(divName ?? "", out var div) ? div.Id : null,
                    VenueId = venues.TryGetValue(venueName ?? "", out var ven) ? ven.Id : null,
                    ProvidesFood = providesFood
                };
                DataStore.Data.Teams.Add(team);
                added++;
            }
            else
            {
                existing.DivisionId = divisions.TryGetValue(divName ?? "", out var div) ? div.Id : null;
                existing.VenueId = venues.TryGetValue(venueName ?? "", out var ven) ? ven.Id : null;
                existing.ProvidesFood = providesFood;
                updated++;
            }
        }

        RefreshTeamList(SearchEntry.Text);
        SetStatus($"Imported: {added} added, {updated} updated");
    }

    private void SetStatus(string msg) => StatusLbl.Text = $"{DateTime.Now:HH:mm:ss} {msg}";
}
