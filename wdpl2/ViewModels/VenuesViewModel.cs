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
/// ViewModel for VenuesPage - manages venue list and CRUD operations
/// </summary>
public partial class VenuesViewModel : BaseViewModel
{
    private readonly IDataStore _dataStore;
    
    [ObservableProperty]
    private ObservableCollection<Venue> _venues = new();
    
    [ObservableProperty]
    private Venue? _selectedVenue;
    
    [ObservableProperty]
    private string _searchText = "";
    
    [ObservableProperty]
    private string _venueName = "";
    
    [ObservableProperty]
    private string _address = "";
    
    [ObservableProperty]
    private string _notes = "";
    
    [ObservableProperty]
    private ObservableCollection<VenueTable> _tables = new();
    
    [ObservableProperty]
    private string _newTableName = "";
    
    [ObservableProperty]
    private bool _isMultiSelectMode;

    public VenuesViewModel(IDataStore dataStore, ISeasonService seasonService) : base(seasonService)
    {
        _dataStore = dataStore;
        _seasonService.SeasonChanged += OnSeasonChanged;
        SafeFireAndForget(InitializeAsync);
    }

    private async Task InitializeAsync()
    {
        _currentSeasonId = _seasonService.CurrentSeasonId;
        await LoadVenuesAsync();
    }

    protected override void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        base.OnSeasonChanged(sender, e);
        _ = LoadVenuesAsync();
    }

    [RelayCommand]
    private async Task LoadVenuesAsync()
    {
        IsLoading = true;
        
        try
        {
            if (!_currentSeasonId.HasValue)
            {
                SetStatus("No season selected");
                _venues.Clear();
                return;
            }

            var allVenues = await _dataStore.GetVenuesAsync(_currentSeasonId);

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var lower = _searchText.ToLower();
                allVenues = allVenues
                    .Where(v => (v.Name ?? "").ToLower().Contains(lower))
                    .ToList();
            }

            _venues.Clear();
            foreach (var venue in allVenues)
                _venues.Add(venue);

            SetStatus($"{_venues.Count} venue(s)");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading venues: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchVenuesAsync(string? searchText)
    {
        SearchText = searchText ?? "";
        await LoadVenuesAsync();
    }

    [RelayCommand]
    private async Task AddVenueAsync()
    {
        if (string.IsNullOrWhiteSpace(_venueName))
        {
            SetStatus("Venue name required");
            return;
        }

        if (!_currentSeasonId.HasValue)
        {
            SetStatus("Please select a season first");
            return;
        }

        var venue = new Venue
        {
            SeasonId = _currentSeasonId.Value,
            Name = _venueName.Trim(),
            Address = _address?.Trim(),
            Notes = _notes?.Trim(),
            Tables = new System.Collections.Generic.List<VenueTable>()
        };

        await _dataStore.AddVenueAsync(venue);
        await _dataStore.SaveAsync();
        await LoadVenuesAsync();

        ClearEditor();
        SetStatus($"Added: {venue.Name}");
    }

    [RelayCommand]
    private async Task UpdateVenueAsync()
    {
        if (_selectedVenue == null)
        {
            SetStatus("No venue selected");
            return;
        }

        _selectedVenue.Name = _venueName?.Trim() ?? "";
        _selectedVenue.Address = _address?.Trim();
        _selectedVenue.Notes = _notes?.Trim();

        await _dataStore.UpdateVenueAsync(_selectedVenue);
        await _dataStore.SaveAsync();
        await LoadVenuesAsync();

        SetStatus($"Updated: {_selectedVenue.Name}");
    }

    [RelayCommand]
    private async Task DeleteVenueAsync(Venue? venue)
    {
        if (venue == null)
        {
            SetStatus("No venue selected");
            return;
        }

        await _dataStore.DeleteVenueAsync(venue);
        await _dataStore.SaveAsync();
        await LoadVenuesAsync();

        ClearEditor();
        SetStatus("Deleted venue");
    }

    [RelayCommand]
    private void AddTable()
    {
        if (_selectedVenue == null || string.IsNullOrWhiteSpace(_newTableName))
        {
            SetStatus("Please select a venue and enter table name");
            return;
        }

        var table = new VenueTable
        {
            Label = _newTableName.Trim(),
            MaxTeams = 2
        };

        _selectedVenue.Tables.Add(table);
        _tables.Add(table);
        _newTableName = "";
        SetStatus($"Added table: {table.Label}");
    }

    [RelayCommand]
    private void RemoveTable(VenueTable? table)
    {
        if (table == null || _selectedVenue == null)
        {
            SetStatus("Please select a table to remove");
            return;
        }

        _selectedVenue.Tables.Remove(table);
        _tables.Remove(table);
        SetStatus($"Removed table: {table.Label}");
    }

    [RelayCommand]
    private void ToggleMultiSelect()
    {
        IsMultiSelectMode = !IsMultiSelectMode;
        SetStatus(IsMultiSelectMode ? "Multi-select enabled" : "Multi-select disabled");
    }

    [RelayCommand]
    private async Task BulkDeleteAsync(System.Collections.Generic.List<Venue>? venues)
    {
        if (venues == null || venues.Count == 0)
        {
            SetStatus("No venues selected");
            return;
        }

        foreach (var venue in venues)
        {
            await _dataStore.DeleteVenueAsync(venue);
        }

        await _dataStore.SaveAsync();
        await LoadVenuesAsync();
        SetStatus($"Deleted {venues.Count} venue(s)");
    }

    partial void OnSelectedVenueChanged(Venue? value)
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

    private void LoadEditor(Venue venue)
    {
        VenueName = venue.Name ?? "";
        Address = venue.Address ?? "";
        Notes = venue.Notes ?? "";

        _tables.Clear();
        foreach (var table in venue.Tables)
            _tables.Add(table);
    }

    private void ClearEditor()
    {
        VenueName = "";
        Address = "";
        Notes = "";
        NewTableName = "";
        _tables.Clear();
    }
}
