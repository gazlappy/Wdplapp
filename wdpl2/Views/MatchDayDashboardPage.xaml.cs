using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.Services;
using Wdpl2.Models;

namespace Wdpl2.Views;

public partial class MatchDayDashboardPage : ContentPage
{
    private readonly ObservableCollection<MatchDayFixture> _fixtures = new();
    private DateTime _currentDate = DateTime.Today;
    private Guid? _currentSeasonId;

    public MatchDayDashboardPage()
    {
        InitializeComponent();
        
        FixturesList.ItemsSource = _fixtures;
        
        SeasonService.SeasonChanged += OnSeasonChanged;
        
        LoadMatches();
    }

    ~MatchDayDashboardPage()
    {
        SeasonService.SeasonChanged -= OnSeasonChanged;
    }

    private void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentSeasonId = e.NewSeasonId;
            LoadMatches();
        });
    }

    private void LoadMatches()
    {
        try
        {
            _currentSeasonId = SeasonService.CurrentSeasonId;
            
            if (!_currentSeasonId.HasValue)
            {
                StatusLabel.Text = "No season selected";
                EmptyStatePanel.IsVisible = true;
                return;
            }

            DateLabel.Text = _currentDate.Date == DateTime.Today 
                ? "Today" 
                : _currentDate.ToString("dddd, MMMM dd, yyyy");

            // Get fixtures for the selected date
            var fixtures = DataStore.Data.Fixtures
                .Where(f => f.SeasonId == _currentSeasonId && f.Date.Date == _currentDate.Date)
                .OrderBy(f => f.Date)
                .ToList();

            _fixtures.Clear();

            if (!fixtures.Any())
            {
                EmptyStatePanel.IsVisible = true;
                StatusLabel.Text = "No matches scheduled for this date";
                UpdateSummary(0, 0, 0);
                return;
            }

            EmptyStatePanel.IsVisible = false;

            int completed = 0;
            int upcoming = 0;

            foreach (var fixture in fixtures)
            {
                var homeTeam = DataStore.Data.Teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                var awayTeam = DataStore.Data.Teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
                var division = DataStore.Data.Divisions.FirstOrDefault(d => d.Id == fixture.DivisionId);
                var venue = fixture.VenueId.HasValue ? DataStore.Data.Venues.FirstOrDefault(v => v.Id == fixture.VenueId) : null;

                bool hasResult = fixture.Frames.Any();
                if (hasResult) completed++;
                else upcoming++;

                _fixtures.Add(new MatchDayFixture
                {
                    FixtureId = fixture.Id,
                    HomeTeamName = homeTeam?.Name ?? "Unknown",
                    AwayTeamName = awayTeam?.Name ?? "Unknown",
                    DivisionName = division?.Name ?? "Division",
                    VenueName = venue != null ? $"@ {venue.Name}" : "",
                    Time = fixture.Date.ToString("HH:mm"),
                    HasResult = hasResult,
                    HomeScore = fixture.HomeScore,
                    AwayScore = fixture.AwayScore
                });
            }

            UpdateSummary(fixtures.Count, completed, upcoming);
            StatusLabel.Text = $"{fixtures.Count} match(es) scheduled";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error loading matches: {ex.Message}";
        }
    }

    private void UpdateSummary(int total, int completed, int upcoming)
    {
        TotalMatchesLabel.Text = total.ToString();
        CompletedMatchesLabel.Text = completed.ToString();
        UpcomingMatchesLabel.Text = upcoming.ToString();
    }

    private void OnPreviousDayClicked(object? sender, EventArgs e)
    {
        _currentDate = _currentDate.AddDays(-1);
        LoadMatches();
    }

    private void OnTodayClicked(object? sender, EventArgs e)
    {
        _currentDate = DateTime.Today;
        LoadMatches();
    }

    private void OnNextDayClicked(object? sender, EventArgs e)
    {
        _currentDate = _currentDate.AddDays(1);
        LoadMatches();
    }

    private async void OnViewFixtureClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is Guid fixtureId)
        {
            // Navigate to fixtures page with this fixture selected
            await Shell.Current.GoToAsync("//Fixtures");
            // Note: Would need to pass fixture ID to Fixtures page to auto-select
        }
    }
}

public class MatchDayFixture
{
    public Guid FixtureId { get; set; }
    public string HomeTeamName { get; set; } = "";
    public string AwayTeamName { get; set; } = "";
    public string DivisionName { get; set; } = "";
    public string VenueName { get; set; } = "";
    public string Time { get; set; } = "";
    public bool HasResult { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
}
