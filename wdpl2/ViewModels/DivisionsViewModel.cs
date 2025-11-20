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
/// ViewModel for DivisionsPage - manages division list and CRUD operations
/// </summary>
public partial class DivisionsViewModel : BaseViewModel
{
    private readonly IDataStore _dataStore;
    
    [ObservableProperty]
    private ObservableCollection<Division> _divisions = new();
    
    [ObservableProperty]
    private Division? _selectedDivision;
    
    [ObservableProperty]
    private string _searchText = "";
    
    [ObservableProperty]
    private string _divisionName = "";

    public DivisionsViewModel(IDataStore dataStore)
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

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var lower = _searchText.ToLower();
                allDivisions = allDivisions
                    .Where(d => (d.Name ?? "").ToLower().Contains(lower))
                    .ToList();
            }

            _divisions.Clear();
            foreach (var division in allDivisions)
                _divisions.Add(division);

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
    private async Task SearchDivisionsAsync(string? searchText)
    {
        _searchText = searchText ?? "";
        await LoadDivisionsAsync();
    }

    [RelayCommand]
    private async Task AddDivisionAsync()
    {
        if (string.IsNullOrWhiteSpace(_divisionName))
        {
            SetStatus("Division name required");
            return;
        }

        if (!_currentSeasonId.HasValue)
        {
            SetStatus("Please select a season first");
            return;
        }

        var division = new Division
        {
            SeasonId = _currentSeasonId.Value,
            Name = _divisionName.Trim()
        };

        await _dataStore.AddDivisionAsync(division);
        await _dataStore.SaveAsync();
        await LoadDivisionsAsync();

        ClearEditor();
        SetStatus($"Added: {division.Name}");
    }

    [RelayCommand]
    private async Task UpdateDivisionAsync()
    {
        if (_selectedDivision == null)
        {
            SetStatus("No division selected");
            return;
        }

        _selectedDivision.Name = _divisionName?.Trim() ?? "";

        await _dataStore.UpdateDivisionAsync(_selectedDivision);
        await _dataStore.SaveAsync();
        await LoadDivisionsAsync();

        SetStatus($"Updated: {_selectedDivision.Name}");
    }

    [RelayCommand]
    private async Task DeleteDivisionAsync(Division? division)
    {
        if (division == null)
        {
            SetStatus("No division selected");
            return;
        }

        await _dataStore.DeleteDivisionAsync(division);
        await _dataStore.SaveAsync();
        await LoadDivisionsAsync();

        ClearEditor();
        SetStatus("Deleted division");
    }

    partial void OnSelectedDivisionChanged(Division? value)
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

    private void LoadEditor(Division division)
    {
        _divisionName = division.Name ?? "";
    }

    private void ClearEditor()
    {
        _divisionName = "";
    }
}
