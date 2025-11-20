using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.ViewModels;

/// <summary>
/// ViewModel for LeagueTablesPage - displays league standings
/// </summary>
public partial class LeagueTablesViewModel : BaseViewModel
{
    private readonly IDataStore _dataStore;
    
    [ObservableProperty]
    private ObservableCollection<Division> _divisions = new();
    
    [ObservableProperty]
    private Division? _selectedDivision;
    
    [ObservableProperty]
    private ObservableCollection<TeamStanding> _standings = new();
    
    [ObservableProperty]
    private bool _showAll = true;

    public LeagueTablesViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
        SeasonService.SeasonChanged += OnSeasonChanged;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _currentSeasonId = SeasonService.CurrentSeasonId;
        await LoadDivisionsAsync();
    }

    protected override void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        base.OnSeasonChanged(sender, e);
        _ = LoadDivisionsAsync();
    }

    [RelayCommand]
    private async Task LoadDivisionsAsync()
    {
        _isLoading = true;
        
        try
        {
            if (!_currentSeasonId.HasValue)
            {
                SetStatus("No season selected");
                _divisions.Clear();
                return;
            }

            var allDivisions = await _dataStore.GetDivisionsAsync(_currentSeasonId);

            _divisions.Clear();
            foreach (var division in allDivisions)
                _divisions.Add(division);

            // Auto-select first division
            if (_divisions.Any())
            {
                _selectedDivision = _divisions.First();
                await CalculateStandingsAsync();
            }

            SetStatus($"{_divisions.Count} division(s)");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading divisions: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task CalculateStandingsAsync()
    {
        if (_selectedDivision == null || !_currentSeasonId.HasValue)
        {
            _standings.Clear();
            return;
        }

        try
        {
            // Get all teams in this division
            var allTeams = await _dataStore.GetTeamsAsync(_currentSeasonId);
            var divisionTeams = allTeams.Where(t => t.DivisionId == _selectedDivision.Id).ToList();

            // Get all fixtures
            var allFixtures = await _dataStore.GetFixturesAsync(_currentSeasonId);
            var divisionFixtures = allFixtures
                .Where(f => f.DivisionId == _selectedDivision.Id && f.Frames.Count > 0)
                .ToList();

            // Calculate standings
            var standingsDict = new System.Collections.Generic.Dictionary<Guid, TeamStanding>();

            foreach (var team in divisionTeams)
            {
                standingsDict[team.Id] = new TeamStanding
                {
                    TeamId = team.Id,
                    TeamName = team.Name ?? "Unknown",
                    Played = 0,
                    Won = 0,
                    Drawn = 0,
                    Lost = 0,
                    FramesFor = 0,
                    FramesAgainst = 0,
                    Points = 0
                };
            }

            foreach (var fixture in divisionFixtures)
            {
                if (standingsDict.ContainsKey(fixture.HomeTeamId))
                {
                    var homeStanding = standingsDict[fixture.HomeTeamId];
                    homeStanding.Played++;
                    homeStanding.FramesFor += fixture.HomeScore;
                    homeStanding.FramesAgainst += fixture.AwayScore;

                    if (fixture.HomeScore > fixture.AwayScore)
                    {
                        homeStanding.Won++;
                        homeStanding.Points += 2;
                    }
                    else if (fixture.HomeScore == fixture.AwayScore)
                    {
                        homeStanding.Drawn++;
                        homeStanding.Points += 1;
                    }
                    else
                    {
                        homeStanding.Lost++;
                    }
                }

                if (standingsDict.ContainsKey(fixture.AwayTeamId))
                {
                    var awayStanding = standingsDict[fixture.AwayTeamId];
                    awayStanding.Played++;
                    awayStanding.FramesFor += fixture.AwayScore;
                    awayStanding.FramesAgainst += fixture.HomeScore;

                    if (fixture.AwayScore > fixture.HomeScore)
                    {
                        awayStanding.Won++;
                        awayStanding.Points += 2;
                    }
                    else if (fixture.AwayScore == fixture.HomeScore)
                    {
                        awayStanding.Drawn++;
                        awayStanding.Points += 1;
                    }
                    else
                    {
                        awayStanding.Lost++;
                    }
                }
            }

            // Sort by points, then frame difference
            var sortedStandings = standingsDict.Values
                .OrderByDescending(s => s.Points)
                .ThenByDescending(s => s.FrameDifference)
                .ThenByDescending(s => s.FramesFor)
                .ToList();

            // Assign positions
            for (int i = 0; i < sortedStandings.Count; i++)
            {
                sortedStandings[i].Position = i + 1;
            }

            _standings.Clear();
            foreach (var standing in sortedStandings)
                _standings.Add(standing);

            SetStatus($"Standings calculated for {_selectedDivision.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error calculating standings: {ex.Message}");
        }
    }

    partial void OnSelectedDivisionChanged(Division? value)
    {
        if (value != null)
        {
            _ = CalculateStandingsAsync();
        }
    }
}

/// <summary>
/// Team standing in league table
/// </summary>
public class TeamStanding
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
    public int FrameDifference => FramesFor - FramesAgainst;
    public int Points { get; set; }
}
