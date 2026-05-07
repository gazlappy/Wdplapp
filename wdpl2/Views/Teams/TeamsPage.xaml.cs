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

        SaveBtn.Clicked += async (_, __) =>
        {
            if (DataStore.Data.IsSeasonLocked(_currentSeasonId))
            {
                await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                    "Cannot save changes — this season is locked.", "OK");
                return;
            }
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

        // NEW: Show all seasons checkbox
        ShowAllSeasonsCheck.CheckedChanged += (_, __) =>
        {
            _showAllSeasons = ShowAllSeasonsCheck.IsChecked;
            RefreshTeamList(SearchEntry?.Text);
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
                .Where(f => f.Frames.Count != 0) // Only completed matches
                .ToList();

            int totalMatches = 0;
            int totalWins = 0;
            int totalDraws = 0;
            int totalLosses = 0;
            int totalFramesFor = 0;
            int totalFramesAgainst = 0;

            // Hoisted: settings don't change per fixture (uses the selected team's own season)
            var settings = DataStore.Data.GetSettingsForSeason(_selectedTeam?.SeasonId);

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
            var teamPlayers = DataStore.Data.Players
                .Where(p => p.TeamId == _selectedTeam.Id)
                .ToList();

            if (teamPlayers.Count == 0)
            {
                TeamPlayersCountLabel.Text = "0 players";
                return;
            }

            // Get fixtures for calculating player stats
            var fixtures = DataStore.Data.Fixtures
                .Where(f => seasonIds.Contains(f.SeasonId ?? Guid.Empty))
                .Where(f => f.HomeTeamId == _selectedTeam.Id || f.AwayTeamId == _selectedTeam.Id)
                .Where(f => f.Frames.Count != 0)
                .ToList();

            // Get season start date for rating calculation
            var settings = DataStore.Data.GetSettingsForSeason(_selectedTeam.SeasonId);
            var season = seasonIds.Count == 1
                ? DataStore.Data.Seasons.FirstOrDefault(s => s.Id == seasonIds[0])
                : DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _selectedTeam.SeasonId);
            var seasonStartDate = season?.StartDate ?? DateTime.Now.AddMonths(-6);

            // Calculate ratings using the shared RatingCalculator
            var allRatings = RatingCalculator.CalculateAllRatings(
                fixtures,
                teamPlayers,
                DataStore.Data.Teams,
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

            var fixtures = DataStore.Data.Fixtures
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
                var opponent = DataStore.Data.Teams?.FirstOrDefault(t => t.Id == opponentId);
                var venue = DataStore.Data.Venues?.FirstOrDefault(v => v.Id == f.VenueId);
                var division = DataStore.Data.Divisions?.FirstOrDefault(d => d.Id == f.DivisionId);
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
            TeamFixturesCountLabel.Text = $"{_teamFixtureItems.Count} fixture(s) • {played} played • {remaining} remaining";
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
                _currentSeasonId = e.NewSeasonId;
                System.Diagnostics.Debug.WriteLine($"Teams Page _currentSeasonId updated to: {_currentSeasonId?.ToString() ?? "NULL"}");
                
                // Force clear the list first
                System.Diagnostics.Debug.WriteLine($"?? Clearing team list...");
                _teamItems.Clear();
                
                // Then refresh
                RefreshTeamList(SearchEntry?.Text);
                RefreshH2HSeasons();
                
                var statusMsg = e.NewSeason != null 
                    ? $"Season changed to: {e.NewSeason.Name}" 
                    : "No active season - data cleared";
                SetStatus(statusMsg);
                
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
            // Use global season from SeasonService
            _currentSeasonId = SeasonService.Current.CurrentSeasonId;

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
            System.Diagnostics.Debug.WriteLine($"=== RefreshTeamList START ===");
            System.Diagnostics.Debug.WriteLine($"   _currentSeasonId: {_currentSeasonId?.ToString() ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"   _showAllSeasons: {_showAllSeasons}");
            
            _teamItems.Clear();

            if (!_showAllSeasons && !_currentSeasonId.HasValue)
            {
                SetStatus("No season selected - check 'Show all seasons' or activate a season");
                System.Diagnostics.Debug.WriteLine("   ? No active season - returning early (list cleared)");
                System.Diagnostics.Debug.WriteLine("=== RefreshTeamList END ===");
                return; // This already clears the list since we called _teamItems.Clear() above
            }

            if (DataStore.Data?.Teams == null)
            {
                SetStatus("No teams data available");
                System.Diagnostics.Debug.WriteLine("   ?? No teams data available");
                System.Diagnostics.Debug.WriteLine("=== RefreshTeamList END ===");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"   ?? Loading teams...");

            var teams = _showAllSeasons
                ? DataStore.Data.Teams.Where(t => t != null).OrderBy(t => t.Name ?? "").ToList()
                : DataStore.Data.Teams.Where(t => t != null && _currentSeasonId.HasValue && t.SeasonId == _currentSeasonId.Value).OrderBy(t => t.Name ?? "").ToList();

            System.Diagnostics.Debug.WriteLine($"   Found {teams.Count} teams");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                teams = teams.Where(t => (t.Name ?? "").ToLower().Contains(lower))
                    .OrderBy(t => t.Name ?? "")
                    .ToList();
            }

            var venueLookup = DataStore.Data.Venues?
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
                var season = DataStore.Data.Seasons?.FirstOrDefault(s => s.Id == _currentSeasonId);
                var seasonInfo = season != null ? $" in {season.Name}" : "";
                var importedTag = season != null && !season.IsActive ? " (Imported)" : "";
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
            sb.AppendLine($"Total Teams: {DataStore.Data.Teams?.Count ?? 0}");
            sb.AppendLine($"Total Players: {DataStore.Data.Players?.Count ?? 0}");
            sb.AppendLine($"Show All Seasons: {_showAllSeasons}");
            sb.AppendLine("\nTeams by season:");

            var teamsList = DataStore.Data.Teams ?? new System.Collections.Generic.List<Team>();
            var grouped = teamsList.GroupBy(t => t.SeasonId).Select(g => new { SeasonId = g.Key, Count = g.Count() });
            foreach (var g in grouped)
            {
                var season = g.SeasonId.HasValue ? DataStore.Data.Seasons.FirstOrDefault(s => s.Id == g.SeasonId.Value)?.Name : "No season";
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

        // If showing all seasons, include every division; otherwise only current season
        var divisions = _showAllSeasons
            ? DataStore.Data.Divisions.OrderBy(d => d.Name)
            : DataStore.Data.Divisions.Where(d => d.SeasonId == _currentSeasonId).OrderBy(d => d.Name);

        foreach (var d in divisions)
            _divisions.Add(d);
    }

    private void RefreshVenues()
    {
        _venues.Clear();

        var venues = _showAllSeasons
            ? DataStore.Data.Venues.OrderBy(v => v.Name)
            : DataStore.Data.Venues.Where(v => v.SeasonId == _currentSeasonId).OrderBy(v => v.Name);

        foreach (var v in venues)
            _venues.Add(v);
    }

    private void RefreshPlayers()
    {
        _players.Clear();

        var players = _showAllSeasons
            ? DataStore.Data.Players.OrderBy(p => p.FullName)
            : DataStore.Data.Players.Where(p => p.SeasonId == _currentSeasonId).OrderBy(p => p.FullName);

        foreach (var p in players)
            _players.Add(p);
    }

    private async void OnAdd(object? sender, EventArgs e)
    {
        if (!_currentSeasonId.HasValue && !_showAllSeasons)
        {
            SetStatus("Please select a season first on the Seasons page");
            return;
        }

        if (DataStore.Data.IsSeasonLocked(_currentSeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot add teams — this season is locked.", "OK");
            return;
        }

        var name = TeamNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetStatus("Team name required");
            return;
        }

        var selectedDivision = DivisionPicker.SelectedItem as Division;
        var seasonIdForTeam = selectedDivision?.SeasonId ?? _currentSeasonId ?? selectedDivision?.SeasonId;

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

        DataStore.Data.Teams.Add(team);
        DataStore.Save();
        RefreshTeamList(SearchEntry.Text);
        SetStatus($"Added: {name}");
    }

    private async void OnUpdate(object? sender, EventArgs e)
    {
        if (_selectedTeam == null)
        {
            SetStatus("No team selected");
            return;
        }

        if (DataStore.Data.IsSeasonLocked(_selectedTeam.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot edit teams — this season is locked.", "OK");
            return;
        }

        var selectedDivision = DivisionPicker.SelectedItem as Division;

        // If user selected a division from a different season, move the team to that season
        if (selectedDivision != null && selectedDivision.SeasonId.HasValue && _selectedTeam.SeasonId != selectedDivision.SeasonId)
        {
            _selectedTeam.SeasonId = selectedDivision.SeasonId;
        }

        _selectedTeam.Name = TeamNameEntry.Text?.Trim();
        _selectedTeam.DivisionId = selectedDivision?.Id;
        _selectedTeam.VenueId = (VenuePicker.SelectedItem as Venue)?.Id;
        _selectedTeam.TableId = (TablePicker.SelectedItem as VenueTable)?.Id;
        _selectedTeam.CaptainPlayerId = (CaptainPicker.SelectedItem as Player)?.Id;
        _selectedTeam.ProvidesFood = FoodSwitch.IsToggled;

        DataStore.Save();
        var updatedName = _selectedTeam.Name; // Store name before RefreshTeamList clears selection
        RefreshTeamList(SearchEntry.Text);
        RefreshHeadToHead(); // Refresh H2H with updated team info
        SetStatus($"Updated: {updatedName}");
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
            return;
        }

        _selectedTeam = DataStore.Data.Teams.FirstOrDefault(t => t.Id == item.Id);
        if (_selectedTeam == null)
        {
            SetStatus("Team not found");
            return;
        }

        LoadEditor(_selectedTeam);
        RefreshHeadToHead();
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
        if (_selectedTeam == null)
        {
            SetStatus("No team selected");
            return;
        }

        if (DataStore.Data.IsSeasonLocked(_selectedTeam.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot delete teams — this season is locked.", "OK");
            return;
        }

        var confirm = await DisplayAlert("Delete Team", $"Delete '{_selectedTeam.Name}'?", "Yes", "No");
        if (!confirm) return;

        DataStore.Data.Teams.Remove(_selectedTeam);
        _selectedTeam = null;
        DataStore.Save();
        RefreshTeamList(SearchEntry.Text);
        ClearEditor();
        RefreshHeadToHead();
        SetStatus("Deleted");
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

        SetStatus(_isMultiSelectMode ? "Multi-select enabled" : "Multi-select disabled");
    }

    private async void OnBulkDelete(object? sender, EventArgs e)
    {
        if (DataStore.Data.IsSeasonLocked(_currentSeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot delete teams — this season is locked.", "OK");
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

        DataStore.Save();
        RefreshTeamList(SearchEntry.Text);
        SetStatus($"Deleted {deleted} team(s)");
    }

    private async void OnBulkAssignDivision(object? sender, EventArgs e)
    {
        try
        {
            if (DataStore.Data.IsSeasonLocked(_currentSeasonId))
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

            var availableDivisions = DataStore.Data.Divisions
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

            int count = 0;
            foreach (var item in selectedItems)
            {
                var team = DataStore.Data.Teams.FirstOrDefault(t => t.Id == item.Id);
                if (team != null)
                {
                    team.DivisionId = newDivisionId;
                    count++;
                }
            }

            DataStore.Save();
            RefreshTeamList(SearchEntry?.Text);

            if (_selectedTeam != null)
                LoadEditor(_selectedTeam);

            SetStatus($"{Helpers.Emojis.Success} Assigned {count} team(s) to {divisionLabel}");
        }
        catch (Exception ex)
        {
            await DisplayAlert($"{Helpers.Emojis.Error} Error", $"Bulk assign failed: {ex.Message}", "OK");
        }
    }

    private async void OnRandomDivisionAssign(object? sender, EventArgs e)
    {
        if (!_currentSeasonId.HasValue)
        {
            await DisplayAlert("No Season", "Please select a season first.", "OK");
            return;
        }

        if (DataStore.Data.IsSeasonLocked(_currentSeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot modify teams \u2014 this season is locked.", "OK");
            return;
        }

        var divisions = DataStore.Data.Divisions
            .Where(d => d.SeasonId == _currentSeasonId)
            .OrderBy(d => d.Name)
            .ToList();

        if (divisions.Count < 2)
        {
            await DisplayAlert("Not Enough Divisions",
                "You need at least 2 divisions in the current season to use random assignment.", "OK");
            return;
        }

        var teams = DataStore.Data.Teams
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
        var shuffled = teams.ToList();
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

        DataStore.Save();
        RefreshTeamList(SearchEntry.Text);

        if (_selectedTeam != null)
            LoadEditor(_selectedTeam);

        SetStatus($"Randomly assigned {teams.Count} team(s) across {divisions.Count} division(s)");
    }

    private async Task ExportTeamsAsync()
    {
        if (!_currentSeasonId.HasValue && !_showAllSeasons)
        {
            await DisplayAlert("No Season", "Please select a season on the Seasons page first.", "OK");
            return;
        }

        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Name,Division,Venue,Table,Captain,ProvidesFood");

        var teams = _showAllSeasons
            ? DataStore.Data.Teams.OrderBy(t => t.Name)
            : DataStore.Data.Teams.Where(t => t.SeasonId == _currentSeasonId).OrderBy(t => t.Name);

        foreach (var t in teams)
        {
            var div = t.DivisionId.HasValue ? DataStore.Data.Divisions.FirstOrDefault(d => d.Id == t.DivisionId)?.Name : "";
            var venue = t.VenueId.HasValue ? DataStore.Data.Venues.FirstOrDefault(v => v.Id == t.VenueId)?.Name : "";
            var venueObj = t.VenueId.HasValue ? DataStore.Data.Venues.FirstOrDefault(v => v.Id == t.VenueId) : null;
            var table = venueObj != null && t.TableId.HasValue ? venueObj.Tables.FirstOrDefault(tb => tb.Id == t.TableId)?.Label : "";
            var captain = t.CaptainPlayerId.HasValue ? DataStore.Data.Players.FirstOrDefault(p => p.Id == t.CaptainPlayerId)?.FullName : "";

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
        if (!_currentSeasonId.HasValue && !_showAllSeasons)
        {
            await DisplayAlert("No Season", "Please select a season on the Seasons page before importing.", "OK");
            return;
        }

        var rows = Csv.Read(stream);
        int added = 0, updated = 0;

        var divisions = DataStore.Data.Divisions.Where(d => d.SeasonId == _currentSeasonId).ToDictionary(d => (d.Name ?? "").Trim(), d => d, StringComparer.OrdinalIgnoreCase);
        var venues = DataStore.Data.Venues.Where(v => v.SeasonId == _currentSeasonId).ToDictionary(v => (v.Name ?? "").Trim(), v => v, StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            var name = r.Get("Name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            var existing = DataStore.Data.Teams.FirstOrDefault(t => t.SeasonId == _currentSeasonId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

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

        DataStore.Save();
        RefreshTeamList(SearchEntry.Text);
        SetStatus($"Imported: {added} added, {updated} updated");
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
