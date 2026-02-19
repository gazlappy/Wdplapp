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
    private ObservableCollection<Competition> _activeCompetitions = new();
    
    [ObservableProperty]
    private ObservableCollection<Competition> _completedCompetitions = new();
    
    [ObservableProperty]
    private Competition? _selectedCompetition;
    
    [ObservableProperty]
    private bool _hasNoCompetitions = true;
    
    [ObservableProperty]
    private bool _hasSelectedCompetition;
    
    [ObservableProperty]
    private bool _showHistory;
    
    [ObservableProperty]
    private bool _hasCompletedCompetitions;

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
        CurrentSeasonId = SeasonService.CurrentSeasonId;
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
        IsLoading = true;
        
        try
        {
            var competitions = await _dataStore.GetCompetitionsAsync(CurrentSeasonId);
            
            Competitions.Clear();
            ActiveCompetitions.Clear();
            CompletedCompetitions.Clear();
            
            foreach (var comp in competitions)
            {
                Competitions.Add(comp);
                
                if (comp.Status == CompetitionStatus.Completed)
                    CompletedCompetitions.Add(comp);
                else
                    ActiveCompetitions.Add(comp);
            }
            
            HasNoCompetitions = !ActiveCompetitions.Any();
            HasCompletedCompetitions = CompletedCompetitions.Any();
            SetStatus($"{ActiveCompetitions.Count} active, {CompletedCompetitions.Count} completed");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading competitions: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
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
            
            SelectedCompetition = competition;
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
            
            SelectedCompetition = null;
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
        HasSelectedCompetition = value != null;
    }
}
