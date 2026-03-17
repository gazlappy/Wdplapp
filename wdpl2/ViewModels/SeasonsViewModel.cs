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
/// ViewModel for SeasonsPage - manages season list and CRUD operations
/// </summary>
public partial class SeasonsViewModel : ObservableObject
{
    private readonly IDataStore _dataStore;
    
    [ObservableProperty]
    private ObservableCollection<Season> _seasons = new();
    
    [ObservableProperty]
    private Season? _selectedSeason;
    
    [ObservableProperty]
    private string _statusMessage = "";
    
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private string _seasonName = "";
    
    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;
    
    [ObservableProperty]
    private DateTime _endDate = DateTime.Today.AddMonths(6);
    
    [ObservableProperty]
    private bool _isActive;

    public SeasonsViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
        _ = SafeInitializeAsync();
    }

    private async Task SafeInitializeAsync()
    {
        try
        {
            await LoadSeasonsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SeasonsViewModel init error: {ex.Message}");
            SetStatus($"Error: {ex.Message}");
        }
    }

    private async Task InitializeAsync()
    {
        await LoadSeasonsAsync();
    }

    [RelayCommand]
    private async Task LoadSeasonsAsync()
    {
        IsLoading = true;
        
        try
        {
            var allSeasons = await _dataStore.GetSeasonsAsync();

            _seasons.Clear();
            foreach (var season in allSeasons)
                _seasons.Add(season);

            SetStatus($"{_seasons.Count} season(s)");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading seasons: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddSeasonAsync()
    {
        if (string.IsNullOrWhiteSpace(_seasonName))
        {
            SetStatus("Season name required");
            return;
        }

        var season = new Season
        {
            Name = _seasonName.Trim(),
            StartDate = _startDate,
            EndDate = _endDate,
            IsActive = _isActive
        };

        // If this is being set as active, deactivate others
        if (_isActive)
        {
            var allSeasons = await _dataStore.GetSeasonsAsync();
            foreach (var s in allSeasons)
            {
                if (s.IsActive)
                {
                    s.IsActive = false;
                    await _dataStore.UpdateSeasonAsync(s);
                }
            }
        }

        await _dataStore.AddSeasonAsync(season);
        await _dataStore.SaveAsync();
        await LoadSeasonsAsync();

        // Update global season service
        if (_isActive)
        {
            SeasonService.Current.CurrentSeasonId = season.Id;
        }

        ClearEditor();
        SetStatus($"Added: {season.Name}");
    }

    [RelayCommand]
    private async Task UpdateSeasonAsync()
    {
        if (_selectedSeason == null)
        {
            SetStatus("No season selected");
            return;
        }

        _selectedSeason.Name = _seasonName?.Trim() ?? "";
        _selectedSeason.StartDate = _startDate;
        _selectedSeason.EndDate = _endDate;
        
        // If setting as active, deactivate others
        if (_isActive && !_selectedSeason.IsActive)
        {
            var allSeasons = await _dataStore.GetSeasonsAsync();
            foreach (var s in allSeasons)
            {
                if (s.IsActive && s.Id != _selectedSeason.Id)
                {
                    s.IsActive = false;
                    await _dataStore.UpdateSeasonAsync(s);
                }
            }
            _selectedSeason.IsActive = true;
            SeasonService.Current.CurrentSeasonId = _selectedSeason.Id;
        }
        else
        {
            _selectedSeason.IsActive = _isActive;
        }

        await _dataStore.UpdateSeasonAsync(_selectedSeason);
        await _dataStore.SaveAsync();
        await LoadSeasonsAsync();

        SetStatus($"Updated: {_selectedSeason.Name}");
    }

    [RelayCommand]
    private async Task DeleteSeasonAsync(Season? season)
    {
        if (season == null)
        {
            SetStatus("No season selected");
            return;
        }

        // Cascade delete from IDataStore (SQLite)
        await _dataStore.DeleteSeasonAsync(season);
        await _dataStore.SaveAsync();

        // Also cascade delete from JSON data store and clean up orphans
        DataStore.Data.DeleteSeasonCascade(season.Id);
        DataStore.Data.CleanupOrphans();
        DataStore.Save();

        await LoadSeasonsAsync();

        ClearEditor();
        SetStatus("Deleted season and all associated data");
    }

    [RelayCommand]
    private async Task SetActiveSeasonAsync(Season? season)
    {
        if (season == null) return;

        // Deactivate all seasons
        var allSeasons = await _dataStore.GetSeasonsAsync();
        foreach (var s in allSeasons)
        {
            if (s.IsActive)
            {
                s.IsActive = false;
                await _dataStore.UpdateSeasonAsync(s);
            }
        }

        // Activate selected season
        season.IsActive = true;
        await _dataStore.SaveAsync();
        
        // Update global season service
        SeasonService.Current.CurrentSeasonId = season.Id;
        SetStatus($"Active season: {season.Name}");
    }

    partial void OnSelectedSeasonChanged(Season? value)
    {
        if (value == null)
        {
            ClearEditor();
        }
        else
        {
            LoadEditor(value);
        }
    }

    private void LoadEditor(Season season)
    {
        SeasonName = season.Name ?? "";
        StartDate = season.StartDate;
        EndDate = season.EndDate;
        IsActive = season.IsActive;
    }

    private void ClearEditor()
    {
        SeasonName = "";
        StartDate = DateTime.Today;
        EndDate = DateTime.Today.AddMonths(6);
        IsActive = false;
    }

    private void SetStatus(string message)
    {
        StatusMessage = $"{DateTime.Now:HH:mm:ss}  {message}";
    }

    public void Cleanup()
    {
        // No season change subscription needed
    }
}
