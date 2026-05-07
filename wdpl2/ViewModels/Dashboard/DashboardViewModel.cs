using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.ViewModels;

/// <summary>
/// ViewModel for the Dashboard page — shows at-a-glance league stats.
/// </summary>
public partial class DashboardViewModel : BaseViewModel
{
    [ObservableProperty]
    private string _seasonName = "No Season";

    [ObservableProperty]
    private int _totalTeams;

    [ObservableProperty]
    private int _totalPlayers;

    [ObservableProperty]
    private int _totalFixtures;

    [ObservableProperty]
    private int _completedFixtures;

    [ObservableProperty]
    private int _upcomingFixtures;

    [ObservableProperty]
    private int _totalDivisions;

    [ObservableProperty]
    private int _totalVenues;

    [ObservableProperty]
    private int _totalFramesPlayed;

    [ObservableProperty]
    private int _totalEightBalls;

    [ObservableProperty]
    private string _nextFixtureInfo = "None scheduled";

    [ObservableProperty]
    private double _seasonProgress;

    [ObservableProperty]
    private string _seasonProgressText = "";

    [ObservableProperty]
    private string _topPlayerName = "-";

    [ObservableProperty]
    private int _topPlayerRating;

    [ObservableProperty]
    private string _recentResults = "";

    [ObservableProperty]
    private string _playerOfTheMonth = "";

    [ObservableProperty]
    private string _playerOfTheMonthStats = "";

    public DashboardViewModel(ISeasonService seasonService, IDataStore dataStore)
        : base(seasonService)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _seasonService.SeasonChanged += OnSeasonChanged;
        SafeFireAndForget(LoadDashboardAsync);
    }

    private readonly IDataStore _dataStore;

    protected override void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        base.OnSeasonChanged(sender, e);
        SafeFireAndForget(LoadDashboardAsync);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            await Task.Run(() =>
            {
                var data = _dataStore.GetData();
                if (data == null) return;
                var seasonId = _seasonService.CurrentSeasonId;

                var season = seasonId.HasValue
                    ? data.Seasons.FirstOrDefault(s => s.Id == seasonId.Value)
                    : null;

                SeasonName = season?.Name ?? "No Season Selected";

                if (season == null)
                {
                    TotalTeams = data.Teams.Count;
                    TotalPlayers = data.Players.Count;
                    TotalFixtures = data.Fixtures.Count;
                    TotalDivisions = data.Divisions.Count;
                    TotalVenues = data.Venues.Count;
                    CompletedFixtures = data.Fixtures.Count(f => f.Frames.Count > 0);
                    UpcomingFixtures = TotalFixtures - CompletedFixtures;
                    TotalFramesPlayed = data.Fixtures.Sum(f => f.Frames.Count);
                    TotalEightBalls = data.Fixtures.Sum(f => f.Frames.Count(fr => fr.EightBall));
                    SeasonProgress = 0;
                    SeasonProgressText = "No season selected";
                    NextFixtureInfo = "None scheduled";
                    TopPlayerName = "-";
                    TopPlayerRating = 0;
                    return;
                }

                var teams = data.Teams.Where(t => t.SeasonId == seasonId).ToList();
                var players = data.Players.Where(p => p.SeasonId == seasonId).ToList();
                var fixtures = data.Fixtures.Where(f => f.SeasonId == seasonId).ToList();
                var divisions = data.Divisions.Where(d => d.SeasonId == seasonId).ToList();
                var venues = data.Venues.Where(v => v.SeasonId == seasonId).ToList();

                TotalTeams = teams.Count;
                TotalPlayers = players.Count;
                TotalFixtures = fixtures.Count;
                TotalDivisions = divisions.Count;
                TotalVenues = venues.Count;

                var completed = fixtures.Where(f => f.Frames.Count > 0).ToList();
                CompletedFixtures = completed.Count;
                UpcomingFixtures = TotalFixtures - CompletedFixtures;
                TotalFramesPlayed = completed.Sum(f => f.Frames.Count);
                TotalEightBalls = completed.Sum(f => f.Frames.Count(fr => fr.EightBall));

                // Season progress
                if (season.StartDate < season.EndDate)
                {
                    var totalDays = (season.EndDate - season.StartDate).TotalDays;
                    var elapsed = (DateTime.Today - season.StartDate).TotalDays;
                    SeasonProgress = Math.Clamp(elapsed / totalDays, 0, 1);
                    SeasonProgressText = $"{SeasonProgress:P0} ({season.StartDate:dd MMM} - {season.EndDate:dd MMM})";
                }
                else
                {
                    SeasonProgress = 0;
                    SeasonProgressText = "";
                }

                // Next fixture
                var nextFixture = fixtures
                    .Where(f => f.Date >= DateTime.Today && f.Frames.Count == 0)
                    .OrderBy(f => f.Date)
                    .FirstOrDefault();

                if (nextFixture != null)
                {
                    var homeTeam = teams.FirstOrDefault(t => t.Id == nextFixture.HomeTeamId)?.Name ?? "?";
                    var awayTeam = teams.FirstOrDefault(t => t.Id == nextFixture.AwayTeamId)?.Name ?? "?";
                    NextFixtureInfo = $"{homeTeam} vs {awayTeam} - {nextFixture.Date:ddd dd MMM HH:mm}";
                }
                else
                {
                    NextFixtureInfo = "None scheduled";
                }

                // Top player by rating
                var ratings = RatingCalculator.CalculateAllRatings(
                    completed, players, teams, data.GetSettingsForSeason(_currentSeasonId), season.StartDate);

                var topPlayer = ratings.Values
                    .Where(r => r.Played >= 5)
                    .OrderByDescending(r => r.Rating)
                    .FirstOrDefault();

                TopPlayerName = topPlayer?.PlayerName ?? "-";
                TopPlayerRating = topPlayer?.Rating ?? 0;

                // Recent results feed
                var resultFeed = ExportService.GetRecentResultsFeed(completed, teams, 5);
                RecentResults = resultFeed.Count > 0 ? string.Join("\n", resultFeed) : "No results yet";

                // Player of the month
                var monthlyWinners = LeagueStatsService.CalculatePlayersOfMonth(completed, players, teams);
                var currentMonth = monthlyWinners.LastOrDefault();
                if (currentMonth != null)
                {
                    PlayerOfTheMonth = currentMonth.PlayerName;
                    PlayerOfTheMonthStats = $"{currentMonth.MonthName}: {currentMonth.FramesWon}/{currentMonth.FramesPlayed} ({currentMonth.WinPercentage:F0}%)";
                }
                else
                {
                    PlayerOfTheMonth = "-";
                    PlayerOfTheMonthStats = "";
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Dashboard load error: {ex.Message}");
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
