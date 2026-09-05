using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class VenuesPage : ContentPage
{
    private readonly IDataStore _dataStore;
    private readonly ObservableCollection<Venue> _venues = new();
    private readonly ObservableCollection<VenueTable> _tables = new();

    private Venue? _selectedVenue;
    private bool _isMultiSelectMode = false;
    private Guid? _currentSeasonId;
    private bool _hasConfigurationSelection;
    private bool _refreshingSeasons;
    private bool _openingCopy;

    public VenuesPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        InitializeComponent();

        VenuesList.ItemsSource = _venues;
        TablesList.ItemsSource = _tables;

        SearchEntry.TextChanged += (_, __) => RefreshVenues(SearchEntry.Text);
        VenuesList.SelectionChanged += OnVenueSelected;
        TablesList.SelectionChanged += OnTableSelected;

        AddVenueBtn.Clicked += OnAddVenue;
        UpdateVenueBtn.Clicked += OnUpdateVenue;
        DeleteVenueBtn.Clicked += OnDeleteVenue;
        AddTableBtn.Clicked += OnAddTable;
        RemoveTableBtn.Clicked += OnRemoveTable;
        MultiSelectBtn.Clicked += OnToggleMultiSelect;
        BulkDeleteBtn.Clicked += OnBulkDelete;

        ReloadBtn.Clicked += (_, __) =>
        {
            RefreshAll();
            SetStatus("Reloaded.");
        };

        ExportBtn.Clicked += async (_, __) => await ExportVenuesAsync();
        VenuesImport.ImportRequested += async (stream, fileName) => await ImportVenuesCsvAsync(stream, fileName);

        // RefreshAll() is called from OnAppearing(); no need to also do it in the ctor.
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SeasonService.Current.SeasonChanged += OnGlobalSeasonChanged;

        try
        {
            // Refresh data when page appears to ensure we have latest season
            RefreshAll();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VenuesPage OnAppearing Error: {ex}");
            SetStatus($"Error loading data: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        SeasonService.Current.SeasonChanged -= OnGlobalSeasonChanged;
    }

    private void OnGlobalSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                RefreshAll();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VenuesPage Season change error: {ex}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetStatus($"Error changing season: {ex.Message}");
            });
        }
    }

    private void RefreshAll()
    {
        try
        {
            var targetId = _hasConfigurationSelection ? _currentSeasonId : SeasonService.Current.CurrentSeasonId;
            var seasons = _dataStore.GetData().Seasons.OrderByDescending(s => s.StartDate).ThenBy(s => s.Name).ToList();
            _refreshingSeasons = true;
            try
            {
                ConfigurationSeasonPicker.ItemsSource = seasons;
                ConfigurationSeasonPicker.SelectedItem = seasons.FirstOrDefault(s => s.Id == targetId);
                _currentSeasonId = (ConfigurationSeasonPicker.SelectedItem as Season)?.Id;
            }
            finally { _refreshingSeasons = false; }
            ResetSelection();
            RefreshVenues(SearchEntry?.Text);
            UpdateActions();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VenuesPage RefreshAll Error: {ex}");
            SetStatus($"Refresh error: {ex.Message}");
        }
    }

    private void RefreshVenues(string? search)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== RefreshVenues START ===");
            System.Diagnostics.Debug.WriteLine($"   _currentSeasonId: {_currentSeasonId?.ToString() ?? "NULL"}");
            
            _venues.Clear();

            if (!_currentSeasonId.HasValue)
            {
                SetStatus("Choose a season to configure, including a new inactive season.");
                System.Diagnostics.Debug.WriteLine("   ? No active season - returning early (list cleared)");
                System.Diagnostics.Debug.WriteLine("=== RefreshVenues END ===");
                return; // List is already cleared
            }

            if (_dataStore.GetData()?.Venues == null)
            {
                SetStatus("No venues data available");
                System.Diagnostics.Debug.WriteLine("   ?? No venues data available");
                System.Diagnostics.Debug.WriteLine("=== RefreshVenues END ===");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"   ?? Loading venues...");

            var venues = _dataStore.GetData().Venues
                .Where(v => v != null && v.SeasonId == _currentSeasonId.Value)
                .OrderBy(v => v.Name ?? "")
                .ToList();

            System.Diagnostics.Debug.WriteLine($"   Found {venues.Count} venues");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                venues = venues.Where(v => (v.Name ?? "").ToLower().Contains(lower))
                    .OrderBy(v => v.Name ?? "")
                    .ToList();
            }

            foreach (var v in venues)
                _venues.Add(v);

            var season = _dataStore.GetData().Seasons?.FirstOrDefault(s => s.Id == _currentSeasonId);
            var seasonInfo = season != null ? $" in {season.Name}" : "";
            SetStatus($"{_venues.Count} venue(s){seasonInfo}. Add, Update and table changes save immediately.");
            
            System.Diagnostics.Debug.WriteLine($"Added {_venues.Count} venues to list");
            System.Diagnostics.Debug.WriteLine("=== RefreshVenues END ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshVenues Error: {ex}");
            SetStatus($"Error loading venues: {ex.Message}");
        }
    }

    private void OnConfigurationSeasonChanged(object? sender, EventArgs e)
    {
        if (_refreshingSeasons) return;
        _hasConfigurationSelection = true;
        _currentSeasonId = (ConfigurationSeasonPicker.SelectedItem as Season)?.Id;
        ResetSelection();
        OnCloseEditor(null, EventArgs.Empty);
        RefreshVenues(SearchEntry.Text);
        UpdateActions();
    }

    private bool CanEdit() => _dataStore.GetData().Seasons.Any(s => s.Id == _currentSeasonId && !s.IsLocked);

    private void UpdateActions()
    {
        var season = _dataStore.GetData().Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
        var editable = CanEdit();
        var selected = editable && !_isMultiSelectMode && _selectedVenue != null && _selectedVenue.SeasonId == _currentSeasonId;
        SeasonContextLbl.Text = season == null ? "No season selected." :
            $"{season.Name} · {(season.IsLocked ? "Locked · read-only" : season.IsActive ? "Active" : "Inactive · available for setup")}";
        EditorSeasonLbl.Text = $"Configuring: {season?.Name ?? "Choose a season first"}";
        CopyVenuesBtn.IsEnabled = editable && !_openingCopy;
        NewVenueBtn.IsEnabled = AddVenueBtn.IsEnabled = editable && !_isMultiSelectMode;
        UpdateVenueBtn.IsEnabled = DeleteVenueBtn.IsEnabled = AddTableBtn.IsEnabled = selected;
        RemoveTableBtn.IsEnabled = selected && TablesList.SelectedItem is VenueTable;
        BulkDeleteBtn.IsEnabled = editable;
        VenuesImport.IsEnabled = editable;
        VenueNameEntry.IsEnabled = AddressEntry.IsEnabled = NotesEntry.IsEnabled = editable && !_isMultiSelectMode;
        NewTableEntry.IsEnabled = selected;
    }

    private void ResetSelection()
    {
        _selectedVenue = null;
        VenuesList.SelectedItem = null;
        VenuesList.SelectedItems?.Clear();
        TablesList.SelectedItem = null;
        ClearEditor();
        HideVenueInfo();
    }

    private void OnNewVenue(object? sender, EventArgs e)
    {
        if (!CanEdit()) return;
        ResetSelection();
        UpdateActions();
        OnOpenEditor(sender, e);
        VenueNameEntry.Focus();
    }

    private void OnOpenEditor(object? sender, EventArgs e)
    {
        UpdateActions();
        EditorPanel.IsVisible = EditorOverlay.IsVisible = true;
    }

    private void OnCloseEditor(object? sender, EventArgs e) => EditorPanel.IsVisible = EditorOverlay.IsVisible = false;

    private async void OnCopyHistoricalVenues(object? sender, EventArgs e)
    {
        if (!CanEdit() || _openingCopy || _currentSeasonId is not Guid destinationId) return;
        _openingCopy = true;
        _hasConfigurationSelection = true;
        UpdateActions();
        try
        {
            OnCloseEditor(null, EventArgs.Empty);
            await Navigation.PushAsync(new HistoricalVenueCopyPage(_dataStore, destinationId));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Cannot copy venues", ex.Message, "OK");
        }
        finally
        {
            _openingCopy = false;
            UpdateActions();
        }
    }

    private void OnVenueSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_isMultiSelectMode) return;

        var item = e.CurrentSelection?.FirstOrDefault() as Venue;
        if (item == null)
        {
            _selectedVenue = null;  // This is setting _selectedVenue to null!
            ClearEditor();
            HideVenueInfo();
            UpdateActions();
            return;
        }

        _selectedVenue = item;
        LoadEditor(_selectedVenue);
        ShowVenueInfo(_selectedVenue);
        UpdateActions();
    }

    private void LoadEditor(Venue venue)
    {
        VenueNameEntry.Text = venue.Name;
        AddressEntry.Text = venue.Address;
        NotesEntry.Text = venue.Notes;

        _tables.Clear();
        foreach (var t in venue.Tables)
            _tables.Add(t);
    }

    private void ClearEditor()
    {
        TablesList.SelectedItem = null;
        VenueNameEntry.Text = "";
        AddressEntry.Text = "";
        NotesEntry.Text = "";
        NewTableEntry.Text = "";
        _tables.Clear();
    }

    private async void OnAddVenue(object? sender, EventArgs e)
    {
        if (!_currentSeasonId.HasValue)
        {
            SetStatus("Please select a season on the Seasons page first");
            return;
        }

        if (_dataStore.GetData().IsSeasonLocked(_currentSeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot add venues — this season is locked.", "OK");
            return;
        }

        var name = VenueNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetStatus("Venue name required");
            return;
        }

        var venue = new Venue
        {
            SeasonId = _currentSeasonId.Value,
            Name = name,
            Address = AddressEntry.Text?.Trim(),
            Notes = NotesEntry.Text?.Trim(),
            Tables = new System.Collections.Generic.List<VenueTable>()
        };

        await _dataStore.AddVenueAsync(venue);
        RefreshVenues(SearchEntry.Text);
        SetStatus($"Added: {name}");
    }

    private async void OnUpdateVenue(object? sender, EventArgs e)
    {
        if (_selectedVenue == null)
        {
            SetStatus("No venue selected");
            return;
        }

        if (_dataStore.GetData().IsSeasonLocked(_selectedVenue.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot edit venues — this season is locked.", "OK");
            return;
        }

        _selectedVenue.Name = VenueNameEntry.Text?.Trim() ?? "";
        _selectedVenue.Address = AddressEntry.Text?.Trim();
        _selectedVenue.Notes = NotesEntry.Text?.Trim();

        string venueName = _selectedVenue.Name; // Store name before refresh
        await _dataStore.UpdateVenueAsync(_selectedVenue);
        RefreshVenues(SearchEntry.Text);
        SetStatus($"Updated: {venueName}");
    }

    private async void OnDeleteVenue(object? sender, EventArgs e)
    {
        if (_selectedVenue == null)
        {
            SetStatus("No venue selected");
            return;
        }

        if (_dataStore.GetData().IsSeasonLocked(_selectedVenue.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot delete venues — this season is locked.", "OK");
            return;
        }

        var confirm = await DisplayAlert("Delete Venue", $"Delete '{_selectedVenue.Name}'?", "Yes", "No");
        if (!confirm) return;

        var toDelete = _selectedVenue;
        _selectedVenue = null;
        await _dataStore.DeleteVenueAsync(toDelete);
        RefreshVenues(SearchEntry.Text);
        ClearEditor();
        SetStatus("Deleted");
    }

    private async void OnAddTable(object? sender, EventArgs e)
    {
        if (_selectedVenue == null)
        {
            SetStatus("Please select a venue first");
            return;
        }

        if (_dataStore.GetData().IsSeasonLocked(_selectedVenue.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot add tables — this season is locked.", "OK");
            return;
        }

        var tableName = NewTableEntry.Text?.Trim();
        if (string.IsNullOrEmpty(tableName))
        {
            SetStatus("Table name required");
            return;
        }

        var table = new VenueTable
        {
            Label = tableName,
            MaxTeams = 2
        };

        _selectedVenue.Tables.Add(table);
        _tables.Add(table);
        NewTableEntry.Text = "";
        await _dataStore.UpdateVenueAsync(_selectedVenue);
        SetStatus($"Added table: {tableName}");
    }

    private async void OnRemoveTable(object? sender, EventArgs e)
    {
        var selectedTable = TablesList.SelectedItem as VenueTable;
        if (selectedTable == null || _selectedVenue == null)
        {
            SetStatus("Please select a table to remove");
            return;
        }

        if (_dataStore.GetData().IsSeasonLocked(_selectedVenue.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot remove tables — this season is locked.", "OK");
            return;
        }

        _selectedVenue.Tables.Remove(selectedTable);
        _tables.Remove(selectedTable);
        await _dataStore.UpdateVenueAsync(_selectedVenue);
        SetStatus($"Removed table: {selectedTable.Label}");
    }

    private void OnTableSelected(object? sender, SelectionChangedEventArgs e)
    {
        UpdateActions();
    }

    private void OnToggleMultiSelect(object? sender, EventArgs e)
    {
        _isMultiSelectMode = !_isMultiSelectMode;

        if (_isMultiSelectMode)
        {
            VenuesList.SelectionMode = SelectionMode.Multiple;
            MultiSelectBtn.Text = "? Multi-Select ON";
            MultiSelectBtn.BackgroundColor = Color.FromArgb("#10B981");
            BulkDeleteBtn.IsVisible = true;

            UpdateVenueBtn.IsEnabled = false;
            DeleteVenueBtn.IsEnabled = false;
            AddVenueBtn.IsEnabled = false;
            AddTableBtn.IsEnabled = false;
            RemoveTableBtn.IsEnabled = false;
        }
        else
        {
            VenuesList.SelectionMode = SelectionMode.Single;
            MultiSelectBtn.Text = "? Multi-Select OFF";
            MultiSelectBtn.BackgroundColor = Color.FromArgb("#6B7280");
            BulkDeleteBtn.IsVisible = false;

            UpdateVenueBtn.IsEnabled = true;
            DeleteVenueBtn.IsEnabled = true;
            AddVenueBtn.IsEnabled = true;
            AddTableBtn.IsEnabled = true;
            RemoveTableBtn.IsEnabled = true;
        }

        ResetSelection();
        UpdateActions();
        SetStatus(_isMultiSelectMode ? "Multi-select enabled" : "Multi-select disabled");
    }

    private async void OnBulkDelete(object? sender, EventArgs e)
    {
        if (_dataStore.GetData().IsSeasonLocked(_currentSeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot delete venues — this season is locked.", "OK");
            return;
        }

        var selectedItems = VenuesList.SelectedItems?.Cast<Venue>().ToList();

        if (selectedItems == null || selectedItems.Count == 0)
        {
            await DisplayAlert("No Selection", "Please select venues to delete.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Bulk Delete",
            $"Delete {selectedItems.Count} venue(s)?",
            "Yes, Delete",
            "Cancel");

        if (!confirm) return;

        int deleted = 0;
        foreach (var item in selectedItems)
        {
            await _dataStore.DeleteVenueAsync(item);
            deleted++;
        }

        RefreshVenues(SearchEntry.Text);
        SetStatus($"Deleted {deleted} venue(s)");
    }

    private async Task ExportVenuesAsync()
    {
        if (!_currentSeasonId.HasValue)
        {
            await DisplayAlert("No Season", "Please select a season on the Seasons page first.", "OK");
            return;
        }

        var season = _dataStore.GetData().Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
        var csv = new StringBuilder();
        csv.AppendLine("Name,Address,Notes,Tables");

        var venues = _dataStore.GetData().Venues.Where(v => v.SeasonId == _currentSeasonId).OrderBy(v => v.Name);

        foreach (var v in venues)
        {
            var tables = string.Join(";", v.Tables.Select(t => t.Label));
            csv.AppendLine($"\"{v.Name}\",\"{v.Address}\",\"{v.Notes}\",\"{tables}\"");
        }

        var fileName = $"Venues_{season?.Name?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(path, csv.ToString());

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Export Venues",
            File = new ShareFile(path)
        });

        SetStatus($"Exported {venues.Count()} venues");
    }

    private async Task ImportVenuesCsvAsync(Stream stream, string fileName)
    {
        if (!CanEdit())
        {
            await DisplayAlert("Cannot import", "Choose an unlocked season to configure before importing.", "OK");
            return;
        }

        var rows = Csv.Read(stream);
        int added = 0, updated = 0;

        foreach (var r in rows)
        {
            var name = r.Get("Name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            var existing = _dataStore.GetData().Venues.FirstOrDefault(v =>
                v.SeasonId == _currentSeasonId &&
                string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

            var address = r.Get("Address");
            var notes = r.Get("Notes");
            var tables = r.Get("Tables");

            if (existing == null)
            {
                var venue = new Venue
                {
                    SeasonId = _currentSeasonId.Value,
                    Name = name.Trim(),
                    Address = address,
                    Notes = notes,
                    Tables = new System.Collections.Generic.List<VenueTable>()
                };

                if (!string.IsNullOrWhiteSpace(tables))
                {
                    foreach (var tableName in tables.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        venue.Tables.Add(new VenueTable { Label = tableName, MaxTeams = 2 });
                    }
                }

                await _dataStore.AddVenueAsync(venue);
                added++;
            }
            else
            {
                existing.Address = address;
                existing.Notes = notes;

                if (!string.IsNullOrWhiteSpace(tables))
                {
                    existing.Tables.Clear();
                    foreach (var tableName in tables.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        existing.Tables.Add(new VenueTable { Label = tableName, MaxTeams = 2 });
                    }
                }

                await _dataStore.UpdateVenueAsync(existing);
                updated++;
            }
        }

        RefreshVenues(SearchEntry.Text);
        SetStatus($"Imported: {added} added, {updated} updated");
    }

    private void SetStatus(string msg) => StatusLbl.Text = $"{DateTime.Now:HH:mm:ss} {msg}";

    private void ShowVenueInfo(Venue venue)
    {
        EmptyStatePanel.IsVisible = false;
        VenueInfoPanel.IsVisible = true;

        SelectedVenueName.Text = venue.Name;
        SelectedVenueAddress.Text = venue.Address ?? "No address";
        SelectedVenueTableCount.Text = $"{venue.Tables.Count} table(s)";
        
        // Stats
        VenueTableCountStat.Text = venue.Tables.Count.ToString();
        VenueCapacity.Text = venue.Tables.Sum(t => t.MaxTeams).ToString();
        
        // Get teams at this venue
        var teamsAtVenue = _dataStore.GetData()?.Teams?
            .Where(t => t.VenueId == venue.Id && t.SeasonId == _currentSeasonId)
            .OrderBy(t => t.Name)
            .ToList() ?? new List<Team>();
        
        VenueTeamCount.Text = teamsAtVenue.Count.ToString();
        VenueTeamsDisplay.ItemsSource = teamsAtVenue;
        
        // Get fixtures at this venue
        var fixturesAtVenue = _dataStore.GetData()?.Fixtures?
            .Where(f => f.VenueId == venue.Id && f.SeasonId == _currentSeasonId)
            .Count() ?? 0;
        
        VenueFixtureCount.Text = fixturesAtVenue.ToString();
        
        // Show tables
        VenueTablesDisplay.ItemsSource = venue.Tables;
        
        // Notes section
        if (!string.IsNullOrWhiteSpace(venue.Notes))
        {
            NotesSection.IsVisible = true;
            SelectedVenueNotes.Text = venue.Notes;
        }
        else
        {
            NotesSection.IsVisible = false;
        }
    }

    private void HideVenueInfo()
    {
        EmptyStatePanel.IsVisible = true;
        VenueInfoPanel.IsVisible = false;
    }
}