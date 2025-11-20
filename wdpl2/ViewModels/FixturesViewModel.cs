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
/// ViewModel for FixturesPage - manages fixture list and operations
/// </summary>
public partial class FixturesViewModel : BaseViewModel
{
    private readonly IDataStore _dataStore;
    
    [ObservableProperty]
    private ObservableCollection<Fixture> _fixtures = new();
    
    [ObservableProperty]
    private Fixture? _selectedFixture;
    
    [ObservableProperty]
    private string _searchText = "";
    
    [ObservableProperty]
    private DateTime _filterDate = DateTime.Today;
    
    [ObservableProperty]
    private bool _showAllDates = true;
    
    [ObservableProperty]
    private ObservableCollection<Team> _availableTeams = new();
    
    [ObservableProperty]
    private ObservableCollection<Venue> _availableVenues = new();

    public FixturesViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
        SeasonService.SeasonChanged += OnSeasonChanged;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _currentSeasonId = SeasonService.CurrentSeasonId;
        await LoadFixturesAsync();
        await LoadReferenceDataAsync();
    }

    protected override void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        base.OnSeasonChanged(sender, e);
        _ = LoadFixturesAsync();
        _ = LoadReferenceDataAsync();
    }

    [RelayCommand]
    private async Task LoadFixturesAsync()
    {
        _isLoading = true;
        
        try
        {
            if (!_currentSeasonId.HasValue)
            {
                SetStatus("No season selected");
                _fixtures.Clear();
                return;
            }

            var allFixtures = await _dataStore.GetFixturesAsync(_currentSeasonId);

            // Apply filters
            if (!_showAllDates)
            {
                allFixtures = allFixtures
                    .Where(f => f.Date.Date == _filterDate.Date)
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var lower = _searchText.ToLower();
                allFixtures = allFixtures
                    .Where(f => GetTeamName(f.HomeTeamId).ToLower().Contains(lower) ||
                               GetTeamName(f.AwayTeamId).ToLower().Contains(lower))
                    .ToList();
            }

            _fixtures.Clear();
            foreach (var fixture in allFixtures)
                _fixtures.Add(fixture);

            SetStatus($"{_fixtures.Count} fixture(s)");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading fixtures: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadReferenceDataAsync()
    {
        if (!_currentSeasonId.HasValue) return;

        var teams = await _dataStore.GetTeamsAsync(_currentSeasonId);
        _availableTeams.Clear();
        foreach (var team in teams)
            _availableTeams.Add(team);

        var venues = await _dataStore.GetVenuesAsync(_currentSeasonId);
        _availableVenues.Clear();
        foreach (var venue in venues)
            _availableVenues.Add(venue);
    }

    [RelayCommand]
    private async Task SearchFixturesAsync(string? searchText)
    {
        _searchText = searchText ?? "";
        await LoadFixturesAsync();
    }

    [RelayCommand]
    private async Task FilterByDateAsync(DateTime date)
    {
        _filterDate = date;
        _showAllDates = false;
        await LoadFixturesAsync();
    }

    [RelayCommand]
    private async Task ShowAllFixturesAsync()
    {
        _showAllDates = true;
        await LoadFixturesAsync();
    }

    [RelayCommand]
    private async Task AddFixtureAsync(Fixture fixture)
    {
        if (fixture == null) return;

        await _dataStore.AddFixtureAsync(fixture);
        await _dataStore.SaveAsync();
        await LoadFixturesAsync();

        SetStatus("Fixture added");
    }

    [RelayCommand]
    private async Task UpdateFixtureAsync(Fixture fixture)
    {
        if (fixture == null) return;

        await _dataStore.UpdateFixtureAsync(fixture);
        await _dataStore.SaveAsync();
        await LoadFixturesAsync();

        SetStatus("Fixture updated");
    }

    [RelayCommand]
    private async Task DeleteFixtureAsync(Fixture? fixture)
    {
        if (fixture == null) return;

        await _dataStore.DeleteFixtureAsync(fixture);
        await _dataStore.SaveAsync();
        await LoadFixturesAsync();

        SetStatus("Fixture deleted");
    }

    [RelayCommand]
    private async Task BulkDeleteAsync(System.Collections.Generic.List<Fixture>? fixtures)
    {
        if (fixtures == null || fixtures.Count == 0) return;

        foreach (var fixture in fixtures)
        {
            await _dataStore.DeleteFixtureAsync(fixture);
        }

        await _dataStore.SaveAsync();
        await LoadFixturesAsync();
        SetStatus($"Deleted {fixtures.Count} fixture(s)");
    }

    private string GetTeamName(Guid teamId)
    {
        var team = _availableTeams.FirstOrDefault(t => t.Id == teamId);
        return team?.Name ?? "Unknown Team";
    }
}
