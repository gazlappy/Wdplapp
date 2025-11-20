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
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadSeasonsAsync();
    }

    [RelayCommand]
    private async Task LoadSeasonsAsync()
    {
        _isLoading = true;
        
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
            _isLoading = false;
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
            foreach (var s in _dataStore.GetData().Seasons)
            {
                s.IsActive = false;
            }
        }

        await _dataStore.AddSeasonAsync(season);
        await _dataStore.SaveAsync();
        await LoadSeasonsAsync();

        // Update global season service
        if (_isActive)
        {
            SeasonService.CurrentSeasonId = season.Id;
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
            foreach (var s in _dataStore.GetData().Seasons)
            {
                s.IsActive = false;
            }
            _selectedSeason.IsActive = true;
            SeasonService.CurrentSeasonId = _selectedSeason.Id;
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

        await _dataStore.DeleteSeasonAsync(season);
        await _dataStore.SaveAsync();
        await LoadSeasonsAsync();

        ClearEditor();
        SetStatus("Deleted season");
    }

    [RelayCommand]
    private async Task SetActiveSeasonAsync(Season? season)
    {
        if (season == null) return;

        // Deactivate all seasons
        foreach (var s in _dataStore.GetData().Seasons)
        {
            s.IsActive = false;
        }

        // Activate selected season
        season.IsActive = true;
        await _dataStore.SaveAsync();
        
        // Update global season service
        SeasonService.CurrentSeasonId = season.Id;
        
        await LoadSeasonsAsync();
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
        _seasonName = season.Name ?? "";
        _startDate = season.StartDate;
        _endDate = season.EndDate;
        _isActive = season.IsActive;
    }

    private void ClearEditor()
    {
        _seasonName = "";
        _startDate = DateTime.Today;
        _endDate = DateTime.Today.AddMonths(6);
        _isActive = false;
    }

    private void SetStatus(string message)
    {
        _statusMessage = $"{DateTime.Now:HH:mm:ss}  {message}";
    }

    public void Cleanup()
    {
        // No season change subscription needed
    }
}
