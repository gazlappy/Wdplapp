using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wdpl2.Helpers;
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

    public LeagueTablesViewModel(IDataStore dataStore, ISeasonService seasonService) : base(seasonService)
    {
        _dataStore = dataStore;
        _seasonService.SeasonChanged += OnSeasonChanged;
        SafeFireAndForget(InitializeAsync);
    }

    private async Task InitializeAsync()
    {
        _currentSeasonId = _seasonService.CurrentSeasonId;
        await LoadDivisionsAsync();
    }

    protected override void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        base.OnSeasonChanged(sender, e);
        SafeFireAndForget(LoadDivisionsAsync);
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

            var allDivisions = await _dataStore.GetDivisionsAsync(_currentSeasonId, LoadToken);

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
            var allTeams = await _dataStore.GetTeamsAsync(_currentSeasonId, LoadToken);
            var divisionTeams = allTeams.Where(t => t.DivisionId == _selectedDivision.Id).ToList();

            var allFixtures = await _dataStore.GetFixturesAsync(_currentSeasonId, LoadToken);
            var divisionFixtures = allFixtures
                .Where(f => f.DivisionId == _selectedDivision.Id && f.Frames.Count > 0)
                .ToList();

            var standings = StandingsCalculator.Calculate(divisionTeams, divisionFixtures, DataStore.Data.Settings);

            var sortedStandings = StandingsSorter.Sort(
                standings,
                DataStore.Data.Settings,
                s => s.Points,
                s => s.FramesFor,
                s => s.FramesAgainst,
                s => s.Won,
                s => s.TeamId,
                divisionFixtures);

            for (int i = 0; i < sortedStandings.Count; i++)
                sortedStandings[i].Position = i + 1;

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
            SafeFireAndForget(CalculateStandingsAsync);
        }
    }
}
