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

    /// <summary>
    /// Expose the data store so the page can use the same instance for editors.
    /// </summary>
    public IDataStore DataStore => _dataStore;

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

    public CompetitionsViewModel(IDataStore dataStore, ISeasonService seasonService) : base(seasonService)
    {
        _dataStore = dataStore;

        // Subscribe to season changes
        _seasonService.SeasonChanged += OnSeasonChanged;

        // Load initial data
        SafeFireAndForget(InitializeAsync);
    }

    private async Task InitializeAsync()
    {
        try
        {
            CurrentSeasonId = _seasonService.CurrentSeasonId;
            await LoadCompetitionsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CompetitionsViewModel init error: {ex.Message}");
        }
    }

    protected override void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        base.OnSeasonChanged(sender, e);
        SafeFireAndForget(LoadCompetitionsAsync);
    }

    [RelayCommand]
    private async Task LoadCompetitionsAsync()
    {
        IsLoading = true;
        
        try
        {
            var competitions = await _dataStore.GetCompetitionsAsync(CurrentSeasonId, LoadToken);
            
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
            
            // Add to UI collections FIRST so the list updates immediately
            Competitions.Add(competition);
            if (competition.Status == CompetitionStatus.Completed)
                CompletedCompetitions.Add(competition);
            else
                ActiveCompetitions.Add(competition);
            
            HasNoCompetitions = !ActiveCompetitions.Any();
            HasCompletedCompetitions = CompletedCompetitions.Any();
            SelectedCompetition = competition;
            
            // Save after updating UI - if this fails, the competition still shows
            try
            {
                await _dataStore.SaveAsync();
            }
            catch (Exception saveEx)
            {
                System.Diagnostics.Debug.WriteLine($"Save warning: {saveEx.Message}");
            }
            
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

        if (competition.IsLocked)
        {
            SetStatus($"Cannot delete '{competition.Name}' — competition is locked. Unlock it first.");
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
