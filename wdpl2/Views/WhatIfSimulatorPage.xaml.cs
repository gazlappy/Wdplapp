using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.Services;
using Wdpl2.Models;

namespace Wdpl2.Views;

public partial class WhatIfSimulatorPage : ContentPage
{
    private readonly ObservableCollection<Division> _divisions = new();
    private readonly ObservableCollection<TableRow> _currentStandings = new();
    private readonly ObservableCollection<SimulatedFixture> _remainingFixtures = new();
    private readonly ObservableCollection<TableRow> _predictedStandings = new();
    private Guid? _currentSeasonId;
    private Division? _selectedDivision;

    public WhatIfSimulatorPage()
    {
        InitializeComponent();
        
        DivisionPicker.ItemsSource = _divisions;
        CurrentStandingsList.ItemsSource = _currentStandings;
        RemainingFixturesList.ItemsSource = _remainingFixtures;
        PredictedStandingsList.ItemsSource = _predictedStandings;
        
        SeasonService.SeasonChanged += OnSeasonChanged;
        
        LoadDivisions();
    }

    ~WhatIfSimulatorPage()
    {
        SeasonService.SeasonChanged -= OnSeasonChanged;
    }

    private void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentSeasonId = e.NewSeasonId;
            LoadDivisions();
        });
    }

    private void LoadDivisions()
    {
        _currentSeasonId = SeasonService.CurrentSeasonId;
        
        if (!_currentSeasonId.HasValue)
        {
            StatusLabel.Text = "No season selected";
            return;
        }

        _divisions.Clear();
        var divisions = DataStore.Data.Divisions
            .Where(d => d.SeasonId == _currentSeasonId)
            .OrderBy(d => d.Name)
            .ToList();

        foreach (var division in divisions)
            _divisions.Add(division);

        if (_divisions.Any() && DivisionPicker.SelectedIndex == -1)
            DivisionPicker.SelectedIndex = 0;
    }

    private void OnDivisionSelected(object? sender, EventArgs e)
    {
        _selectedDivision = DivisionPicker.SelectedItem as Division;
        if (_selectedDivision != null)
        {
            CalculateCurrentStandings();
            LoadRemainingFixtures();
        }
    }

    private void CalculateCurrentStandings()
    {
        if (!_currentSeasonId.HasValue || _selectedDivision == null)
            return;

        _currentStandings.Clear();

        var teams = DataStore.Data.Teams.Where(t => t.DivisionId == _selectedDivision.Id).ToList();
        var fixtures = DataStore.Data.Fixtures
            .Where(f => f.SeasonId == _currentSeasonId && f.Frames.Any())
            .Where(f => teams.Select(t => t.Id).Contains(f.HomeTeamId) || teams.Select(t => t.Id).Contains(f.AwayTeamId))
            .ToList();

        var standings = CalculateStandings(teams, fixtures);

        int pos = 1;
        foreach (var row in standings.OrderByDescending(r => r.Points).ThenByDescending(r => r.FrameDiff))
        {
            row.Position = pos++;
            _currentStandings.Add(row);
        }

        StatusLabel.Text = $"{_currentStandings.Count} team(s) in division";
    }

    private void LoadRemainingFixtures()
    {
        if (!_currentSeasonId.HasValue || _selectedDivision == null)
            return;

        _remainingFixtures.Clear();

        var teams = DataStore.Data.Teams.Where(t => t.DivisionId == _selectedDivision.Id).ToList();
        var teamIds = teams.Select(t => t.Id).ToHashSet();
        
        var remaining = DataStore.Data.Fixtures
            .Where(f => f.SeasonId == _currentSeasonId)
            .Where(f => !f.Frames.Any()) // No results yet
            .Where(f => teamIds.Contains(f.HomeTeamId) && teamIds.Contains(f.AwayTeamId))
            .OrderBy(f => f.Date)
            .ToList();

        foreach (var fixture in remaining)
        {
            var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
            var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);

            _remainingFixtures.Add(new SimulatedFixture
            {
                FixtureId = fixture.Id,
                HomeTeamId = fixture.HomeTeamId,
                AwayTeamId = fixture.AwayTeamId,
                HomeTeamName = homeTeam?.Name ?? "Unknown",
                AwayTeamName = awayTeam?.Name ?? "Unknown",
                FixtureDescription = $"{homeTeam?.Name} vs {awayTeam?.Name}",
                Date = fixture.Date,
                PredictedResult = "Not set"
            });
        }

        RemainingFixturesLabel.Text = $"{_remainingFixtures.Count} remaining fixture(s)";
    }

    private void OnPredictHomeWin(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is SimulatedFixture fixture)
        {
            fixture.PredictedHomeScore = DataStore.Data.Settings.DefaultFramesPerMatch;
            fixture.PredictedAwayScore = DataStore.Data.Settings.DefaultFramesPerMatch - 2;
            fixture.PredictedResult = $"{fixture.PredictedHomeScore}-{fixture.PredictedAwayScore}";
            
            // Refresh the UI
            var index = _remainingFixtures.IndexOf(fixture);
            _remainingFixtures.RemoveAt(index);
            _remainingFixtures.Insert(index, fixture);
        }
    }

    private void OnPredictAwayWin(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is SimulatedFixture fixture)
        {
            fixture.PredictedHomeScore = DataStore.Data.Settings.DefaultFramesPerMatch - 2;
            fixture.PredictedAwayScore = DataStore.Data.Settings.DefaultFramesPerMatch;
            fixture.PredictedResult = $"{fixture.PredictedHomeScore}-{fixture.PredictedAwayScore}";
            
            // Refresh the UI
            var index = _remainingFixtures.IndexOf(fixture);
            _remainingFixtures.RemoveAt(index);
            _remainingFixtures.Insert(index, fixture);
        }
    }

    private void OnAllHomeWinsClicked(object? sender, EventArgs e)
    {
        foreach (var fixture in _remainingFixtures)
        {
            fixture.PredictedHomeScore = DataStore.Data.Settings.DefaultFramesPerMatch;
            fixture.PredictedAwayScore = DataStore.Data.Settings.DefaultFramesPerMatch - 2;
            fixture.PredictedResult = $"{fixture.PredictedHomeScore}-{fixture.PredictedAwayScore}";
        }
        
        // Force refresh
        var temp = _remainingFixtures.ToList();
        _remainingFixtures.Clear();
        foreach (var f in temp) _remainingFixtures.Add(f);
        
        StatusLabel.Text = "Set all remaining fixtures to home wins";
    }

    private void OnAllAwayWinsClicked(object? sender, EventArgs e)
    {
        foreach (var fixture in _remainingFixtures)
        {
            fixture.PredictedHomeScore = DataStore.Data.Settings.DefaultFramesPerMatch - 2;
            fixture.PredictedAwayScore = DataStore.Data.Settings.DefaultFramesPerMatch;
            fixture.PredictedResult = $"{fixture.PredictedHomeScore}-{fixture.PredictedAwayScore}";
        }
        
        // Force refresh
        var temp = _remainingFixtures.ToList();
        _remainingFixtures.Clear();
        foreach (var f in temp) _remainingFixtures.Add(f);
        
        StatusLabel.Text = "Set all remaining fixtures to away wins";
    }

    private void OnCalculatePredictionClicked(object? sender, EventArgs e)
    {
        CalculatePredictedStandings();
    }

    private void CalculatePredictedStandings()
    {
        if (!_currentSeasonId.HasValue || _selectedDivision == null)
            return;

        _predictedStandings.Clear();

        var teams = DataStore.Data.Teams.Where(t => t.DivisionId == _selectedDivision.Id).ToList();
        
        // Get actual completed fixtures
        var completedFixtures = DataStore.Data.Fixtures
            .Where(f => f.SeasonId == _currentSeasonId && f.Frames.Any())
            .Where(f => teams.Select(t => t.Id).Contains(f.HomeTeamId) || teams.Select(t => t.Id).Contains(f.AwayTeamId))
            .ToList();

        // Add simulated results
        var simulatedResults = _remainingFixtures.Where(f => f.PredictedResult != "Not set").ToList();
        var predictedStandings = CalculateStandings(teams, completedFixtures, simulatedResults);

        int pos = 1;
        foreach (var row in predictedStandings.OrderByDescending(r => r.Points).ThenByDescending(r => r.FrameDiff))
        {
            var currentRow = _currentStandings.FirstOrDefault(r => r.TeamId == row.TeamId);
            int oldPosition = currentRow?.Position ?? pos;
            int positionChange = oldPosition - pos;

            row.Position = pos++;
            row.PositionChange = positionChange == 0 ? "=" : positionChange > 0 ? $"?{positionChange}" : $"?{-positionChange}";
            row.ChangeColor = positionChange > 0 ? Color.FromArgb("#10B981") : 
                              positionChange < 0 ? Color.FromArgb("#EF4444") : 
                              Color.FromArgb("#6B7280");
            
            _predictedStandings.Add(row);
        }

        StatusLabel.Text = $"Predicted standings based on {simulatedResults.Count} simulated result(s)";
    }

    private System.Collections.Generic.List<TableRow> CalculateStandings(
        System.Collections.Generic.List<Team> teams,
        System.Collections.Generic.List<Fixture> completedFixtures,
        System.Collections.Generic.List<SimulatedFixture>? simulatedFixtures = null)
    {
        var standings = teams.ToDictionary(t => t.Id, t => new TableRow
        {
            TeamId = t.Id,
            TeamName = t.Name ?? "Unknown",
            Points = 0,
            Played = 0,
            Won = 0,
            Drawn = 0,
            Lost = 0,
            FramesFor = 0,
            FramesAgainst = 0
        });

        var settings = DataStore.Data.Settings;

        // Process completed fixtures
        foreach (var fixture in completedFixtures)
        {
            if (!standings.ContainsKey(fixture.HomeTeamId) || !standings.ContainsKey(fixture.AwayTeamId))
                continue;

            ProcessFixtureResult(standings[fixture.HomeTeamId], standings[fixture.AwayTeamId], 
                                fixture.HomeScore, fixture.AwayScore, settings);
        }

        // Process simulated fixtures
        if (simulatedFixtures != null)
        {
            foreach (var fixture in simulatedFixtures)
            {
                if (!standings.ContainsKey(fixture.HomeTeamId) || !standings.ContainsKey(fixture.AwayTeamId))
                    continue;

                ProcessFixtureResult(standings[fixture.HomeTeamId], standings[fixture.AwayTeamId],
                                    fixture.PredictedHomeScore, fixture.PredictedAwayScore, settings);
            }
        }

        return standings.Values.ToList();
    }

    private void ProcessFixtureResult(TableRow homeRow, TableRow awayRow, int homeScore, int awayScore, AppSettings settings)
    {
        homeRow.Played++;
        awayRow.Played++;
        homeRow.FramesFor += homeScore;
        homeRow.FramesAgainst += awayScore;
        awayRow.FramesFor += awayScore;
        awayRow.FramesAgainst += homeScore;

        if (homeScore > awayScore)
        {
            homeRow.Won++;
            awayRow.Lost++;
            homeRow.Points += homeScore + settings.MatchWinBonus;
            awayRow.Points += awayScore;
        }
        else if (homeScore < awayScore)
        {
            awayRow.Won++;
            homeRow.Lost++;
            awayRow.Points += awayScore + settings.MatchWinBonus;
            homeRow.Points += homeScore;
        }
        else
        {
            homeRow.Drawn++;
            awayRow.Drawn++;
            homeRow.Points += homeScore + settings.MatchDrawBonus;
            awayRow.Points += awayScore + settings.MatchDrawBonus;
        }
    }
}

public class TableRow
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = "";
    public int Position { get; set; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int FramesFor { get; set; }
    public int FramesAgainst { get; set; }
    public int FrameDiff => FramesFor - FramesAgainst;
    public int Points { get; set; }
    public string Record => Drawn > 0 ? $"{Won}-{Drawn}-{Lost}" : $"{Won}-{Lost}";
    public string PositionChange { get; set; } = "";
    public Color ChangeColor { get; set; } = Colors.Gray;
}

public class SimulatedFixture
{
    public Guid FixtureId { get; set; }
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public string HomeTeamName { get; set; } = "";
    public string AwayTeamName { get; set; } = "";
    public string FixtureDescription { get; set; } = "";
    public DateTime Date { get; set; }
    public int PredictedHomeScore { get; set; }
    public int PredictedAwayScore { get; set; }
    public string PredictedResult { get; set; } = "";
}
