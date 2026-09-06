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
using Wdpl2.Services.Import;

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

    public sealed class TeamPlayerItem
    {
        public Guid PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public int Rank { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public int EightBalls { get; set; }
        public int Rating { get; set; } = 1000;
        public bool IsCaptain { get; set; }
        
        public double WinPercentage => Played > 0 ? (double)Won / Played * 100.0 : 0;
        public string WinPercentageDisplay => Played > 0 ? $"{WinPercentage:0}%" : "-";
        public string RankDisplay => Rank <= 3 ? Rank switch { 1 => "??", 2 => "??", 3 => "??", _ => Rank.ToString() } : Rank.ToString();
        public string CaptainLabel => IsCaptain ? "? Captain" : "";
        
        public Color RankColor => Rank switch
        {
            1 => Color.FromArgb("#FFD700"), // Gold
            2 => Color.FromArgb("#C0C0C0"), // Silver
            3 => Color.FromArgb("#CD7F32"), // Bronze
            _ => Color.FromArgb("#6B7280")  // Gray
        };
        
        public Color WinPercentageColor => WinPercentage switch
        {
            >= 60 => Color.FromArgb("#10B981"), // Green
            >= 40 => Color.FromArgb("#F59E0B"), // Amber
            _ => Color.FromArgb("#EF4444")      // Red
        };
    }

    public sealed class TeamFixtureItem
    {
        public Guid FixtureId { get; set; }
        public DateTime Date { get; set; }
        public string DateText => Date.ToString("ddd dd MMM");
        public string OpponentName { get; set; } = "";
        public string VenueName { get; set; } = "";
        public string DivisionName { get; set; } = "";
        public bool IsHome { get; set; }
        public bool HasResult { get; set; }
        public int OurScore { get; set; }
        public int TheirScore { get; set; }
        public string ResultText => HasResult ? $"{OurScore} - {TheirScore}" : "vs";
        public string HomeAwayText => IsHome ? "H" : "A";
        public Color HomeAwayColor => IsHome ? Color.FromArgb("#3B82F6") : Color.FromArgb("#F59E0B");
        public Color ResultColor => !HasResult ? Color.FromArgb("#6B7280")
            : OurScore > TheirScore ? Color.FromArgb("#10B981")
            : OurScore < TheirScore ? Color.FromArgb("#EF4444")
            : Color.FromArgb("#6B7280");
        public string ResultIcon => !HasResult ? "" : OurScore > TheirScore ? "W" : OurScore < TheirScore ? "L" : "D";
        public Color ResultIconColor => ResultColor;
    }

    private readonly ObservableCollection<TeamListItem> _teamItems = new();
    private readonly ObservableCollection<Division> _divisions = new();
    private readonly ObservableCollection<Venue> _venues = new();
    private readonly ObservableCollection<VenueTable> _tables = new();
    private readonly ObservableCollection<Player> _players = new();
    private readonly ObservableCollection<TeamHeadToHeadItem> _h2hItems = new();
    private readonly ObservableCollection<Season> _h2hSeasons = new();
    private readonly ObservableCollection<TeamPlayerItem> _teamPlayerItems = new();
    private readonly ObservableCollection<TeamFixtureItem> _teamFixtureItems = new();

    private Team? _selectedTeam;
    private bool _isMultiSelectMode = false;
    private Guid? _currentSeasonId;
    private bool _showAllSeasons = false;
    private readonly SeasonConfigurationSelection _configuration = new();
    private bool _refreshingConfiguration;
    private bool _saving;
    private bool _openingHistoricalPlayers;

    private readonly IDataStore _dataStore;

    public TeamsPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        InitializeComponent();

        TeamsList.ItemsSource = _teamItems;
        DivisionPicker.ItemsSource = _divisions;
        VenuePicker.ItemsSource = _venues;
        TablePicker.ItemsSource = _tables;
        CaptainPicker.ItemsSource = _players;
        H2HList.ItemsSource = _h2hItems;
        H2HSeasonPicker.ItemsSource = _h2hSeasons;
        TeamPlayersList.ItemsSource = _teamPlayerItems;
        TeamFixturesList.ItemsSource = _teamFixtureItems;

        SearchEntry.TextChanged += (_, __) => RefreshTeamList(SearchEntry.Text);
        TeamsList.SelectionChanged += OnTeamSelected;
        VenuePicker.SelectedIndexChanged += (_, __) => RefreshTablesForSelectedVenue();
        H2HSeasonPicker.SelectedIndexChanged += (_, __) => RefreshHeadToHead();

        AddBtn.Clicked += OnAdd;
        UpdateBtn.Clicked += OnUpdate;
        DeleteBtn.Clicked += OnDelete;
        MultiSelectBtn.Clicked += OnToggleMultiSelect;
        BulkAssignDivisionBtn.Clicked += OnBulkAssignDivision;
        BulkDeleteBtn.Clicked += OnBulkDelete;
        RandomDivisionBtn.Clicked += OnRandomDivisionAssign;

        ReloadBtn.Clicked += (_, __) =>
        {
            RefreshAll();
            SetStatus("Reloaded.");
        };

        ExportBtn.Clicked += async (_, __) => await ExportTeamsAsync();
        TeamsImport.ImportRequested += async (stream, fileName) => await ImportTeamsCsvAsync(stream, fileName);

        // NEW: Show all seasons checkbox
        ShowAllSeasonsCheck.CheckedChanged += (_, __) =>
        {
            _showAllSeasons = ShowAllSeasonsCheck.IsChecked;
            ResetSelection();
            RefreshTeamList(SearchEntry?.Text);
            UpdateActions();
        };

        // NEW: Debug check button
        DebugCheckBtn.Clicked += async (_, __) => await CheckDatabaseAsync();

        // RefreshAll() is called from OnAppearing(); no need to also do it in the ctor.
    }

    /// <summary>
    /// Select a specific team by ID (called when navigating from league table)
    /// </summary>
    public void SelectTeam(Guid teamId)
    {
        _pendingTeamSelection = teamId;
    }

    private Guid? _pendingTeamSelection;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SeasonService.Current.SeasonChanged += OnGlobalSeasonChanged;

        try
        {
            // Refresh data when page appears to ensure we have latest season
            RefreshAll();
            
            // Handle pending team selection (from navigation)
            if (_pendingTeamSelection.HasValue)
            {
                var teamId = _pendingTeamSelection.Value;
                _pendingTeamSelection = null;

                // RefreshAll() above has already populated _teamItems synchronously.
                // Marshal to the UI thread to set the selection without an arbitrary delay.
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var teamItem = _teamItems.FirstOrDefault(t => t.Id == teamId);
                    if (teamItem != null)
                    {
                        TeamsList.SelectedItem = teamItem;
                    }
                    else
                    {
                        // Team might be in a different season - enable "Show all seasons"
                        _showAllSeasons = true;
                        ShowAllSeasonsCheck.IsChecked = true;
                        RefreshTeamList(null);

                        teamItem = _teamItems.FirstOrDefault(t => t.Id == teamId);
                        if (teamItem != null)
                        {
                            TeamsList.SelectedItem = teamItem;
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TeamsPage OnAppearing Error: {ex}");
            SetStatus($"Error loading data: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        SeasonService.Current.SeasonChanged -= OnGlobalSeasonChanged;
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
                H2HSection.IsVisible = false;
                TeamPlayersSection.IsVisible = false;
                TeamFixturesSection.IsVisible = false;
                return;
            }

            // Show team info
            TeamInfoPanel.IsVisible = true;
            FilterPanel.IsVisible = true;
            EmptyStatePanel.IsVisible = false;
            H2HSection.IsVisible = true;
            TeamPlayersSection.IsVisible = true;
            TeamFixturesSection.IsVisible = true;

            SelectedTeamName.Text = _selectedTeam.Name ?? "Unknown Team";
            
            var division = _dataStore.GetData().Divisions?.FirstOrDefault(d => d.Id == _selectedTeam.DivisionId);
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
                seasonIds = _dataStore.GetData().Seasons
                    .Select(s => s.Id)
                    .ToList();
            }

            // Build head-to-head records
            var h2hData = new Dictionary<Guid, TeamHeadToHeadItem>();
            var seasonRecords = new Dictionary<Guid, Dictionary<Guid, (int w, int d, int l, int ff, int fa, int pf, int pa)>>();

            // Get all fixtures for the selected seasons involving this team
            var fixtures = _dataStore.GetData().Fixtures
                .Where(f => seasonIds.Contains(f.SeasonId ?? Guid.Empty))
                .Where(f => f.HomeTeamId == _selectedTeam.Id || f.AwayTeamId == _selectedTeam.Id)
                .Where(f => f.Frames.Count != 0) // Only completed matches
                .ToList();

            int totalMatches = 0;
            int totalWins = 0;
            int totalDraws = 0;
            int totalLosses = 0;
            int totalFramesFor = 0;
            int totalFramesAgainst = 0;

            // Hoisted: settings don't change per fixture (uses the selected team's own season)
            var settings = _dataStore.GetData().GetSettingsForSeason(_selectedTeam?.SeasonId);

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

                // Calculate points (using hoisted settings)
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
                    var opponent = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == opponentId);
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
                        var season = _dataStore.GetData().Seasons?.FirstOrDefault(s => s.Id == seasonKvp.Key);
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
            SelectedTeamStats.Text = $"{totalMatches} matches � {recordText} ({winPct:0.#}%) � {totalFramesFor}-{totalFramesAgainst} frames";

            // Refresh team players list
            RefreshTeamPlayers(seasonIds);

            // Refresh team fixtures list
            RefreshTeamFixtures(seasonIds);

            SetStatus($"Found {_h2hItems.Count} opponent(s)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshHeadToHead Error: {ex}");
            SetStatus($"Error loading head-to-head: {ex.Message}");
        }
    }

    private void RefreshTeamPlayers(List<Guid> seasonIds)
    {
        try
        {
            _teamPlayerItems.Clear();

            if (_selectedTeam == null)
            {
                TeamPlayersCountLabel.Text = "";
                return;
            }

            // Get players for this team
            var teamPlayers = _dataStore.GetData().Players
                .Where(p => p.TeamId == _selectedTeam.Id)
                .ToList();

            if (teamPlayers.Count == 0)
            {
                TeamPlayersCountLabel.Text = "0 players";
                return;
            }

            // Get fixtures for calculating player stats
            var fixtures = _dataStore.GetData().Fixtures
                .Where(f => seasonIds.Contains(f.SeasonId ?? Guid.Empty))
                .Where(f => f.HomeTeamId == _selectedTeam.Id || f.AwayTeamId == _selectedTeam.Id)
                .Where(f => f.Frames.Count != 0)
                .ToList();

            // Get season start date for rating calculation
            var settings = _dataStore.GetData().GetSettingsForSeason(_selectedTeam.SeasonId);
            var season = seasonIds.Count == 1
                ? _dataStore.GetData().Seasons.FirstOrDefault(s => s.Id == seasonIds[0])
                : _dataStore.GetData().Seasons.FirstOrDefault(s => s.Id == _selectedTeam.SeasonId);
            var seasonStartDate = season?.StartDate ?? DateTime.Now.AddMonths(-6);

            // Calculate ratings using the shared RatingCalculator
            var allRatings = RatingCalculator.CalculateAllRatings(
                fixtures,
                teamPlayers,
                _dataStore.GetData().Teams,
                settings,
                seasonStartDate);

            // Build player items with stats
            var playerItems = new List<TeamPlayerItem>();

            foreach (var player in teamPlayers)
            {
                var item = new TeamPlayerItem
                {
                    PlayerId = player.Id,
                    PlayerName = player.FullName ?? $"{player.FirstName} {player.LastName}".Trim(),
                    IsCaptain = _selectedTeam.CaptainPlayerId == player.Id
                };

                // Get rating stats if available
                if (allRatings.TryGetValue(player.Id, out var ratingStats))
                {
                    item.Played = ratingStats.Played;
                    item.Won = ratingStats.Wins;
                    item.Lost = ratingStats.Losses;
                    item.EightBalls = ratingStats.EightBalls;
                    item.Rating = ratingStats.Rating;
                }

                playerItems.Add(item);
            }

            // Sort by rating (highest first), then by played
            var sortedPlayers = playerItems
                .OrderByDescending(p => p.Rating)
                .ThenByDescending(p => p.WinPercentage)
                .ThenByDescending(p => p.Played)
                .ToList();

            // Assign ranks
            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                sortedPlayers[i].Rank = i + 1;
                _teamPlayerItems.Add(sortedPlayers[i]);
            }

            TeamPlayersCountLabel.Text = $"{_teamPlayerItems.Count} player(s)";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshTeamPlayers Error: {ex}");
            TeamPlayersCountLabel.Text = "Error loading players";
        }
    }

    private void RefreshTeamFixtures(List<Guid> seasonIds)
    {
        try
        {
            _teamFixtureItems.Clear();

            if (_selectedTeam == null)
            {
                TeamFixturesCountLabel.Text = "";
                return;
            }

            var fixtures = _dataStore.GetData().Fixtures
                .Where(f => seasonIds.Contains(f.SeasonId ?? Guid.Empty))
                .Where(f => f.HomeTeamId == _selectedTeam.Id || f.AwayTeamId == _selectedTeam.Id)
                .OrderBy(f => f.Date)
                .ToList();

            if (fixtures.Count == 0)
            {
                TeamFixturesCountLabel.Text = "0 fixtures";
                return;
            }

            foreach (var f in fixtures)
            {
                var isHome = f.HomeTeamId == _selectedTeam.Id;
                var opponentId = isHome ? f.AwayTeamId : f.HomeTeamId;
                var opponent = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == opponentId);
                var venue = _dataStore.GetData().Venues?.FirstOrDefault(v => v.Id == f.VenueId);
                var division = _dataStore.GetData().Divisions?.FirstOrDefault(d => d.Id == f.DivisionId);
                var hasResult = f.Frames.Count > 0;

                var ourScore = isHome ? f.HomeScore : f.AwayScore;
                var theirScore = isHome ? f.AwayScore : f.HomeScore;

                _teamFixtureItems.Add(new TeamFixtureItem
                {
                    FixtureId = f.Id,
                    Date = f.Date,
                    OpponentName = opponent?.Name ?? "Unknown",
                    VenueName = venue?.Name ?? "",
                    DivisionName = division?.Name ?? "",
                    IsHome = isHome,
                    HasResult = hasResult,
                    OurScore = ourScore,
                    TheirScore = theirScore
                });
            }

            var played = _teamFixtureItems.Count(f => f.HasResult);
            var remaining = _teamFixtureItems.Count - played;
            TeamFixturesCountLabel.Text = $"{_teamFixtureItems.Count} fixture(s) � {played} played � {remaining} remaining";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshTeamFixtures Error: {ex}");
            TeamFixturesCountLabel.Text = "Error loading fixtures";
        }
    }

    // ========== EXISTING METHODS ==========

    private void OnGlobalSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== TEAMS PAGE: Season Changed Event ===");
            System.Diagnostics.Debug.WriteLine($"Old Season: {e.OldSeasonId?.ToString() ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"New Season: {e.NewSeasonId?.ToString() ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"New Season Name: {e.NewSeason?.Name ?? "NULL"}");
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!_saving) RefreshAll();
                
                System.Diagnostics.Debug.WriteLine("=== TEAMS PAGE: Refresh Complete ===");
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
            var seasons = _dataStore.GetData().Seasons.OrderByDescending(s => s.StartDate).ThenBy(s => s.Name).ToList();
            _configuration.Refresh(seasons, SeasonService.Current.CurrentSeasonId);
            _currentSeasonId = _configuration.SeasonId;
            _refreshingConfiguration = true;
            try
            {
                ConfigurationSeasonPicker.ItemsSource = seasons;
                ConfigurationSeasonPicker.SelectedItem = seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
            }
            finally { _refreshingConfiguration = false; }
            ResetSelection();
            RefreshDivisions();
            RefreshVenues();
            RefreshPlayers();
            RefreshTeamList(SearchEntry?.Text);
            RefreshH2HSeasons();
            UpdateActions();
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

            var seasons = _dataStore.GetData().Seasons
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
            System.Diagnostics.Debug.WriteLine($"=== RefreshTeamList START ===");
            System.Diagnostics.Debug.WriteLine($"   _currentSeasonId: {_currentSeasonId?.ToString() ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"   _showAllSeasons: {_showAllSeasons}");
            
            _teamItems.Clear();

            if (!_showAllSeasons && !_currentSeasonId.HasValue)
            {
                SetStatus("Choose a season to configure, including inactive seasons.");
                System.Diagnostics.Debug.WriteLine("   ? No active season - returning early (list cleared)");
                System.Diagnostics.Debug.WriteLine("=== RefreshTeamList END ===");
                return; // This already clears the list since we called _teamItems.Clear() above
            }

            if (_dataStore.GetData()?.Teams == null)
            {
                SetStatus("No teams data available");
                System.Diagnostics.Debug.WriteLine("   ?? No teams data available");
                System.Diagnostics.Debug.WriteLine("=== RefreshTeamList END ===");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"   ?? Loading teams...");

            var teams = _showAllSeasons
                ? _dataStore.GetData().Teams.Where(t => t != null).OrderBy(t => t.Name ?? "").ToList()
                : _dataStore.GetData().Teams.Where(t => t != null && _currentSeasonId.HasValue && t.SeasonId == _currentSeasonId.Value).OrderBy(t => t.Name ?? "").ToList();

            System.Diagnostics.Debug.WriteLine($"   Found {teams.Count} teams");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                teams = teams.Where(t => (t.Name ?? "").ToLower().Contains(lower))
                    .OrderBy(t => t.Name ?? "")
                    .ToList();
            }

            var venueLookup = _dataStore.GetData().Venues?
                .Where(v => _showAllSeasons || (v != null && v.SeasonId == _currentSeasonId))
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

            if (_showAllSeasons && teams.Count != 0)
            {
                var seasonGroups = teams.GroupBy(t => t.SeasonId).Count();
                SetStatus($"{_teamItems.Count} team(s) across {seasonGroups} season(s)");
            }
            else if (teams.Count != 0)
            {
                var season = _dataStore.GetData().Seasons?.FirstOrDefault(s => s.Id == _currentSeasonId);
                var seasonInfo = season != null ? $" in {season.Name}" : "";
                var importedTag = season != null && !season.IsActive ? " (Inactive)" : "";
                SetStatus($"{_teamItems.Count} team(s){seasonInfo}{importedTag}");
            }
            else
            {
                SetStatus("No teams found for the current season");
            }

            System.Diagnostics.Debug.WriteLine($"Added {_teamItems.Count} items to ObservableCollection");
            System.Diagnostics.Debug.WriteLine("=== RefreshTeamList END ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshTeamList Error: {ex}");
            SetStatus($"Error loading teams: {ex.Message}");
        }
    }

    // NEW: Tap handler label
    private void OnShowAllSeasonsTapped(object? sender, EventArgs e)
    {
        ShowAllSeasonsCheck.IsChecked = !ShowAllSeasonsCheck.IsChecked;
    }

    private async System.Threading.Tasks.Task CheckDatabaseAsync()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("?? TEAMS DATABASE CHECK\n");
            sb.AppendLine($"Total Teams: {_dataStore.GetData().Teams?.Count ?? 0}");
            sb.AppendLine($"Total Players: {_dataStore.GetData().Players?.Count ?? 0}");
            sb.AppendLine($"Show All Seasons: {_showAllSeasons}");
            sb.AppendLine("\nTeams by season:");

            var teamsList = _dataStore.GetData().Teams ?? new System.Collections.Generic.List<Team>();
            var grouped = teamsList.GroupBy(t => t.SeasonId).Select(g => new { SeasonId = g.Key, Count = g.Count() });
            foreach (var g in grouped)
            {
                var season = g.SeasonId.HasValue ? _dataStore.GetData().Seasons.FirstOrDefault(s => s.Id == g.SeasonId.Value)?.Name : "No season";
                sb.AppendLine($"  {season}: {g.Count} (SeasonId: {g.SeasonId})");
            }

            await DisplayAlert("Database Check", sb.ToString(), "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
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

    private void RefreshDivisions()
    {
        _divisions.Clear();

        var divisions = _dataStore.GetData().Divisions.Where(d => d.SeasonId == (_showAllSeasons ? _selectedTeam?.SeasonId : _currentSeasonId)).OrderBy(d => d.Name);

        foreach (var d in divisions)
            _divisions.Add(d);
    }

    private void RefreshVenues()
    {
        _venues.Clear();

        var venues = _dataStore.GetData().Venues.Where(v => v.SeasonId == (_showAllSeasons ? _selectedTeam?.SeasonId : _currentSeasonId)).OrderBy(v => v.Name);

        foreach (var v in venues)
            _venues.Add(v);
    }

    private void RefreshPlayers()
    {
        _players.Clear();

        var players = _dataStore.GetData().Players.Where(p => p.SeasonId == (_showAllSeasons ? _selectedTeam?.SeasonId : _currentSeasonId) && p.TeamId == _selectedTeam?.Id).OrderBy(p => p.FullName);

        foreach (var p in players)
            _players.Add(p);
    }

    private async void OnAdd(object? sender, EventArgs e)
    {
        if (!CanEdit()) return;
        if (!_currentSeasonId.HasValue && !_showAllSeasons)
        {
            SetStatus("Please select a season first on the Seasons page");
            return;
        }

        if (_dataStore.GetData().IsSeasonLocked(_currentSeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot add teams � this season is locked.", "OK");
            return;
        }

        var name = TeamNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetStatus("Team name required");
            return;
        }

        var selectedDivision = DivisionPicker.SelectedItem as Division;
        var seasonIdForTeam = _currentSeasonId;

        var team = new Team
        {
            SeasonId = seasonIdForTeam,
            Name = name,
            DivisionId = selectedDivision?.Id,
            VenueId = (VenuePicker.SelectedItem as Venue)?.Id,
            TableId = (TablePicker.SelectedItem as VenueTable)?.Id,
            CaptainPlayerId = (CaptainPicker.SelectedItem as Player)?.Id,
            ProvidesFood = FoodSwitch.IsToggled
        };

        await SaveTeamChangesAsync(new[] { team }, Array.Empty<Guid>(), $"Added: {name}");
    }

    private async void OnUpdate(object? sender, EventArgs e)
    {
        if (!CanEdit() || _selectedTeam?.SeasonId != _currentSeasonId) return;
        if (_selectedTeam == null)
        {
            SetStatus("No team selected");
            return;
        }

        if (_dataStore.GetData().IsSeasonLocked(_selectedTeam.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot edit teams � this season is locked.", "OK");
            return;
        }

        var selectedDivision = DivisionPicker.SelectedItem as Division;

        var team = ImportWorkspace.Clone(_selectedTeam);
        team.Name = TeamNameEntry.Text?.Trim();
        team.DivisionId = selectedDivision?.Id;
        team.VenueId = (VenuePicker.SelectedItem as Venue)?.Id;
        team.TableId = (TablePicker.SelectedItem as VenueTable)?.Id;
        team.CaptainPlayerId = (CaptainPicker.SelectedItem as Player)?.Id;
        team.ProvidesFood = FoodSwitch.IsToggled;
        await SaveTeamChangesAsync(new[] { team }, Array.Empty<Guid>(), $"Updated: {team.Name}");
    }

    private void OnTeamSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_isMultiSelectMode) return;

        var item = e.CurrentSelection?.FirstOrDefault() as TeamListItem;
        if (item == null)
        {
            _selectedTeam = null;
            ClearEditor();
            RefreshHeadToHead();
            UpdateActions();
            return;
        }

        _selectedTeam = _dataStore.GetData().Teams.FirstOrDefault(t => t.Id == item.Id);
        if (_selectedTeam == null)
        {
            SetStatus("Team not found");
            return;
        }

        LoadEditor(_selectedTeam);
        RefreshHeadToHead();
        UpdateActions();
    }

    private void RefreshTablesForSelectedVenue()
    {
        _tables.Clear();
        if (VenuePicker.SelectedItem is not Venue v) return;
        foreach (var t in v.Tables)
            _tables.Add(t);
    }

    private async void OnDelete(object? sender, EventArgs e)
    {
        if (!CanEdit() || _selectedTeam?.SeasonId != _currentSeasonId) return;
        if (_selectedTeam == null)
        {
            SetStatus("No team selected");
            return;
        }

        if (_dataStore.GetData().IsSeasonLocked(_selectedTeam.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot delete teams � this season is locked.", "OK");
            return;
        }

        var id = _selectedTeam.Id;
        var target = _currentSeasonId;
        var confirm = await DisplayAlert("Delete Team", $"Delete '{_selectedTeam.Name}'?", "Yes", "No");
        if (!confirm || target != _currentSeasonId) return;
        await SaveTeamChangesAsync(Array.Empty<Team>(), new[] { id }, "Deleted team.");
    }

    private void OnToggleMultiSelect(object? sender, EventArgs e)
    {
        _isMultiSelectMode = !_isMultiSelectMode;

        if (_isMultiSelectMode)
        {
            TeamsList.SelectionMode = SelectionMode.Multiple;
            MultiSelectBtn.Text = "? Multi-Select ON";
            MultiSelectBtn.BackgroundColor = Color.FromArgb("#10B981");
            BulkAssignDivisionBtn.IsVisible = true;
            BulkDeleteBtn.IsVisible = true;

            UpdateBtn.IsEnabled = false;
            DeleteBtn.IsEnabled = false;
            AddBtn.IsEnabled = false;
        }
        else
        {
            TeamsList.SelectionMode = SelectionMode.Single;
            MultiSelectBtn.Text = "? Multi-Select OFF";
            MultiSelectBtn.BackgroundColor = Color.FromArgb("#6B7280");
            BulkAssignDivisionBtn.IsVisible = false;
            BulkDeleteBtn.IsVisible = false;

            UpdateBtn.IsEnabled = true;
            DeleteBtn.IsEnabled = true;
            AddBtn.IsEnabled = true;
        }

        ResetSelection();
        UpdateActions();
        SetStatus(_isMultiSelectMode ? "Multi-select enabled" : "Multi-select disabled");
    }

    private async void OnBulkDelete(object? sender, EventArgs e)
    {
        if (!CanEdit()) return;
        if (_dataStore.GetData().IsSeasonLocked(_currentSeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot delete teams � this season is locked.", "OK");
            return;
        }

        var selectedItems = TeamsList.SelectedItems?.Cast<TeamListItem>().ToList();

        if (selectedItems == null || selectedItems.Count == 0)
        {
            await DisplayAlert("No Selection", "Please select teams to delete.", "OK");
            return;
        }

        var confirm = await DisplayAlert("Bulk Delete", $"Delete {selectedItems.Count} team(s)?", "Yes, Delete", "Cancel");
        if (!confirm) return;

        await SaveTeamChangesAsync(Array.Empty<Team>(), selectedItems.Select(t => t.Id).ToArray(), $"Deleted {selectedItems.Count} team(s)");
    }

    private async void OnBulkAssignDivision(object? sender, EventArgs e)
    {
        if (!CanEdit()) return;
        try
        {
            if (_dataStore.GetData().IsSeasonLocked(_currentSeasonId))
            {
                await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                    "Cannot assign divisions \u2014 this season is locked.", "OK");
                return;
            }

            var selectedItems = TeamsList.SelectedItems?.Cast<TeamListItem>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                await DisplayAlert($"{Helpers.Emojis.Info} No Selection", "Please select teams to assign.", "OK");
                return;
            }

            var availableDivisions = _dataStore.GetData().Divisions
                .Where(d => d.SeasonId == _currentSeasonId)
                .OrderBy(d => d.Name)
                .ToList();

            if (availableDivisions.Count == 0)
            {
                await DisplayAlert($"{Helpers.Emojis.Info} No Divisions", "No divisions exist for this season. Create divisions first.", "OK");
                return;
            }

            var divisionNames = availableDivisions.Select(d => d.Name ?? "Unknown").ToArray();
            var chosen = await DisplayActionSheet(
                $"Assign {selectedItems.Count} team(s) to division:", "Cancel", "\u2014 No Division \u2014", divisionNames);
            if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return;

            Guid? newDivisionId = null;
            string divisionLabel;
            if (chosen != "\u2014 No Division \u2014")
            {
                var division = availableDivisions.FirstOrDefault(d => d.Name == chosen);
                if (division == null) return;
                newDivisionId = division.Id;
                divisionLabel = division.Name ?? "Unknown";
            }
            else
            {
                divisionLabel = "no division";
            }

            var changes = new List<Team>();
            foreach (var item in selectedItems)
            {
                var team = _dataStore.GetData().Teams.FirstOrDefault(t => t.Id == item.Id);
                if (team != null)
                {
                    var copy = ImportWorkspace.Clone(team);
                    copy.DivisionId = newDivisionId;
                    changes.Add(copy);
                }
            }
            await SaveTeamChangesAsync(changes, Array.Empty<Guid>(), $"Assigned {changes.Count} team(s) to {divisionLabel}");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Helpers.Emojis.Error} Error", $"Bulk assign failed: {ex.Message}", "OK");
        }
    }

    private async void OnRandomDivisionAssign(object? sender, EventArgs e)
    {
        if (!CanEdit()) return;
        if (!_currentSeasonId.HasValue)
        {
            await DisplayAlert("No Season", "Please select a season first.", "OK");
            return;
        }

        if (_dataStore.GetData().IsSeasonLocked(_currentSeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot modify teams \u2014 this season is locked.", "OK");
            return;
        }

        var divisions = _dataStore.GetData().Divisions
            .Where(d => d.SeasonId == _currentSeasonId)
            .OrderBy(d => d.Name)
            .ToList();

        if (divisions.Count < 2)
        {
            await DisplayAlert("Not Enough Divisions",
                "You need at least 2 divisions in the current season to use random assignment.", "OK");
            return;
        }

        var teams = _dataStore.GetData().Teams
            .Where(t => t.SeasonId == _currentSeasonId)
            .ToList();

        if (teams.Count == 0)
        {
            await DisplayAlert("No Teams", "There are no teams in the current season.", "OK");
            return;
        }

        var perDiv = teams.Count / divisions.Count;
        var remainder = teams.Count % divisions.Count;
        var summary = string.Join("\n", divisions.Select((d, i) =>
            $"  {d.Name}: {perDiv + (i < remainder ? 1 : 0)} teams"));

        var confirm = await DisplayAlert(
            "Random Division Assign",
            $"This will randomly assign {teams.Count} team(s) across {divisions.Count} division(s) as evenly as possible:\n\n{summary}\n\nExisting division assignments will be overwritten. Continue?",
            "Assign", "Cancel");

        if (!confirm) return;

        // Shuffle teams using Fisher-Yates
        var rng = new Random();
        var shuffled = teams.Select(t => ImportWorkspace.Clone(t)).ToList();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // Build division assignments as lists of team names (round-robin)
        var divisionNames = divisions.Select(d => d.Name).ToList();
        var divisionAssignments = new List<List<string>>();
        for (int d = 0; d < divisions.Count; d++)
            divisionAssignments.Add([]);

        for (int i = 0; i < shuffled.Count; i++)
            divisionAssignments[i % divisions.Count].Add(shuffled[i].Name);

        // Show animated draw
        var teamNames = shuffled.Select(t => t.Name).ToList();
        var animPage = new DivisionDrawAnimationPage(teamNames, divisionNames, divisionAssignments);
        await Navigation.PushModalAsync(animPage);
        await animPage.GetResultAsync();

        // Apply the actual DivisionId assignments
        for (int i = 0; i < shuffled.Count; i++)
            shuffled[i].DivisionId = divisions[i % divisions.Count].Id;

        await SaveTeamChangesAsync(shuffled, Array.Empty<Guid>(), $"Randomly assigned {teams.Count} team(s) across {divisions.Count} division(s)");
    }

    private async Task ExportTeamsAsync()
    {
        if (!_currentSeasonId.HasValue && !_showAllSeasons)
        {
            await DisplayAlert("No Season", "Please select a season on the Seasons page first.", "OK");
            return;
        }

        var season = _dataStore.GetData().Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Name,Division,Venue,Table,Captain,ProvidesFood");

        var teams = _showAllSeasons
            ? _dataStore.GetData().Teams.OrderBy(t => t.Name)
            : _dataStore.GetData().Teams.Where(t => t.SeasonId == _currentSeasonId).OrderBy(t => t.Name);

        foreach (var t in teams)
        {
            var div = t.DivisionId.HasValue ? _dataStore.GetData().Divisions.FirstOrDefault(d => d.Id == t.DivisionId)?.Name : "";
            var venue = t.VenueId.HasValue ? _dataStore.GetData().Venues.FirstOrDefault(v => v.Id == t.VenueId)?.Name : "";
            var venueObj = t.VenueId.HasValue ? _dataStore.GetData().Venues.FirstOrDefault(v => v.Id == t.VenueId) : null;
            var table = venueObj != null && t.TableId.HasValue ? venueObj.Tables.FirstOrDefault(tb => tb.Id == t.TableId)?.Label : "";
            var captain = t.CaptainPlayerId.HasValue ? _dataStore.GetData().Players.FirstOrDefault(p => p.Id == t.CaptainPlayerId)?.FullName : "";

            csv.AppendLine($"\"{t.Name}\",\"{div}\",\"{venue}\",\"{table}\",\"{captain}\",{t.ProvidesFood}");
        }

        var fileName = $"Teams_{season?.Name?.Replace(" ", "_") ?? "All"}_{DateTime.Now:yyyyMMdd}.csv";
        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
        await System.IO.File.WriteAllTextAsync(path, csv.ToString());

        await Share.RequestAsync(new ShareFileRequest { Title = "Export Teams", File = new ShareFile(path) });

        SetStatus($"Exported teams to {fileName}");
    }

    private async Task ImportTeamsCsvAsync(System.IO.Stream stream, string fileName)
    {
        if (!CanEdit()) return;
        if (!_currentSeasonId.HasValue && !_showAllSeasons)
        {
            await DisplayAlert("No Season", "Please select a season on the Seasons page before importing.", "OK");
            return;
        }

        var rows = Csv.Read(stream);
        int added = 0, updated = 0;
        var changes = new List<Team>();

        var divisions = _dataStore.GetData().Divisions.Where(d => d.SeasonId == _currentSeasonId).ToDictionary(d => (d.Name ?? "").Trim(), d => d, StringComparer.OrdinalIgnoreCase);
        var venues = _dataStore.GetData().Venues.Where(v => v.SeasonId == _currentSeasonId).ToDictionary(v => (v.Name ?? "").Trim(), v => v, StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            var name = r.Get("Name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            var original = changes.Concat(_dataStore.GetData().Teams).FirstOrDefault(t => t.SeasonId == _currentSeasonId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            var existing = original == null ? null : ImportWorkspace.Clone(original);

            var divName = r.Get("Division");
            var venueName = r.Get("Venue");
            var providesFood = r.Get("ProvidesFood")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            if (existing == null)
            {
                var team = new Team
                {
                    SeasonId = _currentSeasonId ?? (divisions.TryGetValue(divName ?? "", out var d) ? d.SeasonId : null),
                    Name = name.Trim(),
                    DivisionId = divisions.TryGetValue(divName ?? "", out var div) ? div.Id : null,
                    VenueId = venues.TryGetValue(venueName ?? "", out var ven) ? ven.Id : null,
                    ProvidesFood = providesFood
                };
                changes.Add(team);
                added++;
            }
            else
            {
                existing.DivisionId = divisions.TryGetValue(divName ?? "", out var div) ? div.Id : null;
                existing.VenueId = venues.TryGetValue(venueName ?? "", out var ven) ? ven.Id : null;
                if (existing.VenueId != original?.VenueId) existing.TableId = null;
                existing.ProvidesFood = providesFood;
                changes.RemoveAll(t => t.Id == existing.Id);
                changes.Add(existing);
                updated++;
            }
        }

        await SaveTeamChangesAsync(changes, Array.Empty<Guid>(), $"Imported: {added} added, {updated} updated");
    }

    private bool CanEdit() => !_saving && _configuration.CanEdit(_dataStore.GetData().Seasons, _showAllSeasons);

    private void OnConfigurationSeasonChanged(object? sender, EventArgs e)
    {
        if (_refreshingConfiguration || _saving) return;
        _configuration.Select((ConfigurationSeasonPicker.SelectedItem as Season)?.Id);
        ShowAllSeasonsCheck.IsChecked = false;
        OnCloseEditor(null, EventArgs.Empty);
        RefreshAll();
    }

    private void ResetSelection()
    {
        _selectedTeam = null;
        TeamsList.SelectedItem = null;
        TeamsList.SelectedItems?.Clear();
        ClearEditor();
        RefreshHeadToHead();
    }

    private void UpdateActions()
    {
        var season = _dataStore.GetData().Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
        var editable = CanEdit();
        var selected = editable && !_isMultiSelectMode && _selectedTeam != null && _selectedTeam.SeasonId == _currentSeasonId;
        SeasonContextLbl.Text = season == null ? "No season selected." : $"{season.Name} · {(season.IsLocked ? "Locked · read-only" : season.IsActive ? "Active" : "Inactive · available for setup")}";
        EditorSeasonLbl.Text = _showAllSeasons ? "Browsing all seasons — read-only." : $"Configuring: {season?.Name ?? "Choose a season first"}";
        NewTeamBtn.IsEnabled = AddBtn.IsEnabled = editable && !_isMultiSelectMode;
        UpdateBtn.IsEnabled = DeleteBtn.IsEnabled = selected;
        AddHistoricalPlayersBtn.IsEnabled = selected && !_openingHistoricalPlayers;
        BulkAssignDivisionBtn.IsEnabled = BulkDeleteBtn.IsEnabled = RandomDivisionBtn.IsEnabled = TeamsImport.IsEnabled = editable;
        TeamNameEntry.IsEnabled = DivisionPicker.IsEnabled = VenuePicker.IsEnabled = TablePicker.IsEnabled = FoodSwitch.IsEnabled = editable && !_isMultiSelectMode;
        CaptainPicker.IsEnabled = selected;
        ConfigurationSeasonPicker.IsEnabled = ShowAllSeasonsCheck.IsEnabled = TeamsList.IsEnabled = SearchEntry.IsEnabled = ReloadBtn.IsEnabled = MultiSelectBtn.IsEnabled = !_saving;
    }

    private async void OnAddHistoricalPlayers(object? sender, EventArgs e)
    {
        if (!CanEdit() || _isMultiSelectMode || _openingHistoricalPlayers ||
            _selectedTeam == null || _selectedTeam.SeasonId != _currentSeasonId) return;
        var teamId = _selectedTeam.Id;
        _openingHistoricalPlayers = true;
        UpdateActions();
        try
        {
            var page = new HistoricalTeamPlayersPage(_dataStore, teamId);
            _configuration.Select(_currentSeasonId);
            _pendingTeamSelection = teamId;
            OnCloseEditor(null, EventArgs.Empty);
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            _pendingTeamSelection = null;
            await DisplayAlert("Cannot add players", ex.Message, "OK");
        }
        finally
        {
            _openingHistoricalPlayers = false;
            UpdateActions();
        }
    }

    private void OnNewTeam(object? sender, EventArgs e)
    {
        if (!CanEdit()) return;
        ResetSelection();
        RefreshDivisions();
        RefreshVenues();
        RefreshPlayers();
        OnOpenEditor(sender, e);
        TeamNameEntry.Focus();
    }

    private void OnOpenEditor(object? sender, EventArgs e)
    {
        UpdateActions();
        EditorPanel.IsVisible = EditorOverlay.IsVisible = true;
    }

    private void OnCloseEditor(object? sender, EventArgs e) => EditorPanel.IsVisible = EditorOverlay.IsVisible = false;

    private async Task SaveTeamChangesAsync(IEnumerable<Team> changes, IReadOnlyCollection<Guid> deleted, string success)
    {
        if (!CanEdit()) return;
        _saving = true;
        UpdateActions();
        try
        {
            var workspace = new ImportWorkspace(_dataStore);
            var data = workspace.GetData();
            foreach (var team in changes)
            {
                if (team.SeasonId != _currentSeasonId || string.IsNullOrWhiteSpace(team.Name) || team.Name.Length > 100)
                    throw new InvalidOperationException("Choose teams from the configured season with names of 1 to 100 characters.");
                var previous = data.Teams.FirstOrDefault(t => t.Id == team.Id);
                if (previous != null && previous.SeasonId != _currentSeasonId)
                    throw new InvalidOperationException("Teams cannot be moved between seasons here.");
                data.Teams.RemoveAll(t => t.Id == team.Id);
                data.Teams.Add(ImportWorkspace.Clone(team));
            }
            if (deleted.Any(id => !data.Teams.Any(t => t.Id == id && t.SeasonId == _currentSeasonId)))
                throw new InvalidOperationException("Select teams from the configured season only.");
            if (data.Players.Any(p => p.TeamId.HasValue && deleted.Contains(p.TeamId.Value)) ||
                data.Fixtures.Any(f => deleted.Contains(f.HomeTeamId) || deleted.Contains(f.AwayTeamId)))
                throw new InvalidOperationException("Move the team's players and fixtures before deleting it.");
            data.Teams.RemoveAll(t => deleted.Contains(t.Id));
            await workspace.SaveAsync();
            RefreshAll();
            SetStatus(success);
        }
        catch (Exception ex)
        {
            SetStatus($"Changes not saved: {ex.Message}");
            await DisplayAlert("Team changes not saved", ex.Message, "OK");
        }
        finally { _saving = false; UpdateActions(); }
    }

    private void SetStatus(string msg) => StatusLbl.Text = $"{DateTime.Now:HH:mm:ss} {msg}";

    private void ClearEditor()
    {
        TeamNameEntry.Text = "";
        FoodSwitch.IsToggled = false;
        DivisionPicker.SelectedIndex = -1;
        VenuePicker.SelectedIndex = -1;
        TablePicker.SelectedIndex = -1;
        CaptainPicker.SelectedIndex = -1;
    }
}
