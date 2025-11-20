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
/// ViewModel for the CompetitionsPage - manages competition list and CRUD operations
/// </summary>
public partial class CompetitionsViewModel : BaseViewModel
{
    private readonly IDataStore _dataStore;
    
    [ObservableProperty]
    private ObservableCollection<Competition> _competitions = new();
    
    [ObservableProperty]
    private Competition? _selectedCompetition;
    
    [ObservableProperty]
    private bool _hasNoCompetitions = true;
    
    [ObservableProperty]
    private bool _hasSelectedCompetition;

    public CompetitionsViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
        
        // Subscribe to season changes
        SeasonService.SeasonChanged += OnSeasonChanged;
        
        // Load initial data
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _currentSeasonId = SeasonService.CurrentSeasonId;
        await LoadCompetitionsAsync();
    }

    protected override void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        base.OnSeasonChanged(sender, e);
        _ = LoadCompetitionsAsync();
    }

    [RelayCommand]
    private async Task LoadCompetitionsAsync()
    {
        _isLoading = true;
        
        try
        {
            var competitions = await _dataStore.GetCompetitionsAsync(_currentSeasonId);
            
            _competitions.Clear();
            foreach (var comp in competitions)
            {
                _competitions.Add(comp);
            }
            
            _hasNoCompetitions = !_competitions.Any();
            SetStatus($"{_competitions.Count} competition(s)");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading competitions: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateCompetitionAsync(Competition? competition)
    {
        if (competition == null) return;
        
        try
        {
            await _dataStore.AddCompetitionAsync(competition);
            await _dataStore.SaveAsync();
            await LoadCompetitionsAsync();
            
            _selectedCompetition = competition;
            SetStatus($"Created competition: {competition.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error creating competition: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteCompetitionAsync(Competition? competition)
    {
        if (competition == null)
        {
            SetStatus("No competition selected");
            return;
        }
        
        try
        {
            await _dataStore.DeleteCompetitionAsync(competition);
            await _dataStore.SaveAsync();
            
            _selectedCompetition = null;
            await LoadCompetitionsAsync();
            
            SetStatus("Competition deleted");
        }
        catch (Exception ex)
        {
            SetStatus($"Error deleting competition: {ex.Message}");
        }
    }

    partial void OnSelectedCompetitionChanged(Competition? value)
    {
        _hasSelectedCompetition = value != null;
    }
}
