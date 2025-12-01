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
    private readonly ObservableCollection<Venue> _venues = new();
    private readonly ObservableCollection<VenueTable> _tables = new();

    private Venue? _selectedVenue;
    private bool _isMultiSelectMode = false;
    private Guid? _currentSeasonId;
    private bool _isFlyoutOpen = false;

    public VenuesPage()
    {
        InitializeComponent();

        VenuesList.ItemsSource = _venues;
        TablesList.ItemsSource = _tables;

        // Wire up burger menu and flyout
        BurgerMenuBtn.Clicked += OnBurgerMenuClicked;
        CloseFlyoutBtn.Clicked += OnCloseFlyoutClicked;
        OverlayTap.Tapped += (_, __) => CloseFlyout();

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

        SaveBtn.Clicked += async (_, __) =>
        {
            DataStore.Save();
            await DisplayAlert("Saved", "All changes saved.", "OK");
            SetStatus("Saved.");
        };

        ReloadBtn.Clicked += (_, __) =>
        {
            DataStore.Load();
            RefreshAll();
            SetStatus("Reloaded.");
        };

        ExportBtn.Clicked += async (_, __) => await ExportVenuesAsync();
        VenuesImport.ImportRequested += async (stream, fileName) => await ImportVenuesCsvAsync(stream, fileName);

        // SUBSCRIBE to global season changes
        SeasonService.SeasonChanged += OnGlobalSeasonChanged;

        RefreshAll();
    }

    ~VenuesPage()
    {
        SeasonService.SeasonChanged -= OnGlobalSeasonChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

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

    private void OnGlobalSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _currentSeasonId = e.NewSeasonId;
                RefreshVenues(SearchEntry?.Text);
                SetStatus($"Season changed to: {e.NewSeason?.Name ?? "None"}");
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
            // Use global season from SeasonService
            _currentSeasonId = SeasonService.CurrentSeasonId;

            // If no season is set, try to use the active season
            if (!_currentSeasonId.HasValue)
            {
                var activeSeason = DataStore.Data?.Seasons?.FirstOrDefault(s => s.IsActive);
                if (activeSeason != null)
                {
                    _currentSeasonId = activeSeason.Id;
                }
            }

            RefreshVenues(SearchEntry?.Text);
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
                SetStatus("No season selected - activate a season to see venues");
                System.Diagnostics.Debug.WriteLine("   ✅ No active season - returning early (list cleared)");
                System.Diagnostics.Debug.WriteLine("=== RefreshVenues END ===");
                return; // List is already cleared
            }

            if (DataStore.Data?.Venues == null)
            {
                SetStatus("No venues data available");
                System.Diagnostics.Debug.WriteLine("   ⚠️ No venues data available");
                System.Diagnostics.Debug.WriteLine("=== RefreshVenues END ===");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"   📥 Loading venues...");

            var venues = DataStore.Data.Venues
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

            var season = DataStore.Data.Seasons?.FirstOrDefault(s => s.Id == _currentSeasonId);
            var seasonInfo = season != null ? $" in {season.Name}" : "";
            var importedTag = season != null && !season.IsActive ? " (Imported)" : "";
            SetStatus($"{_venues.Count} venue(s){seasonInfo}{importedTag}");
            
            System.Diagnostics.Debug.WriteLine($"Added {_venues.Count} venues to list");
            System.Diagnostics.Debug.WriteLine("=== RefreshVenues END ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshVenues Error: {ex}");
            SetStatus($"Error loading venues: {ex.Message}");
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
            return;
        }

        _selectedVenue = item;
        LoadEditor(_selectedVenue);
        ShowVenueInfo(_selectedVenue);
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
        VenueNameEntry.Text = "";
        AddressEntry.Text = "";
        NotesEntry.Text = "";
        NewTableEntry.Text = "";
        _tables.Clear();
    }

    private void OnAddVenue(object? sender, EventArgs e)
    {
        if (!_currentSeasonId.HasValue)
        {
            SetStatus("Please select a season on the Seasons page first");
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

        DataStore.Data.Venues.Add(venue);
        RefreshVenues(SearchEntry.Text);
        SetStatus($"Added: {name}");
    }

    private void OnUpdateVenue(object? sender, EventArgs e)
    {
        if (_selectedVenue == null)
        {
            SetStatus("No venue selected");
            return;
        }

        _selectedVenue.Name = VenueNameEntry.Text?.Trim() ?? "";
        _selectedVenue.Address = AddressEntry.Text?.Trim();
        _selectedVenue.Notes = NotesEntry.Text?.Trim();

        string venueName = _selectedVenue.Name; // Store name before refresh
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

        var confirm = await DisplayAlert("Delete Venue", $"Delete '{_selectedVenue.Name}'?", "Yes", "No");
        if (!confirm) return;

        DataStore.Data.Venues.Remove(_selectedVenue);
        _selectedVenue = null;
        RefreshVenues(SearchEntry.Text);
        ClearEditor();
        SetStatus("Deleted");
    }

    private void OnAddTable(object? sender, EventArgs e)
    {
        if (_selectedVenue == null)
        {
            SetStatus("Please select a venue first");
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
        SetStatus($"Added table: {tableName}");
    }

    private void OnRemoveTable(object? sender, EventArgs e)
    {
        var selectedTable = TablesList.SelectedItem as VenueTable;
        if (selectedTable == null || _selectedVenue == null)
        {
            SetStatus("Please select a table to remove");
            return;
        }

        _selectedVenue.Tables.Remove(selectedTable);
        _tables.Remove(selectedTable);
        SetStatus($"Removed table: {selectedTable.Label}");
    }

    private void OnTableSelected(object? sender, SelectionChangedEventArgs e)
    {
        // Just for selection tracking
    }

    private void OnToggleMultiSelect(object? sender, EventArgs e)
    {
        _isMultiSelectMode = !_isMultiSelectMode;

        if (_isMultiSelectMode)
        {
            VenuesList.SelectionMode = SelectionMode.Multiple;
            MultiSelectBtn.Text = "✓ Multi-Select ON";
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
            MultiSelectBtn.Text = "☐ Multi-Select OFF";
            MultiSelectBtn.BackgroundColor = Color.FromArgb("#6B7280");
            BulkDeleteBtn.IsVisible = false;

            UpdateVenueBtn.IsEnabled = true;
            DeleteVenueBtn.IsEnabled = true;
            AddVenueBtn.IsEnabled = true;
            AddTableBtn.IsEnabled = true;
            RemoveTableBtn.IsEnabled = true;
        }

        SetStatus(_isMultiSelectMode ? "Multi-select enabled" : "Multi-select disabled");
    }

    private async void OnBulkDelete(object? sender, EventArgs e)
    {
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
            DataStore.Data.Venues.Remove(item);
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

        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
        var csv = new StringBuilder();
        csv.AppendLine("Name,Address,Notes,Tables");

        var venues = DataStore.Data.Venues.Where(v => v.SeasonId == _currentSeasonId).OrderBy(v => v.Name);

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
        if (!_currentSeasonId.HasValue)
        {
            await DisplayAlert("No Season", "Please select a season on the Seasons page before importing.", "OK");
            return;
        }

        var rows = Csv.Read(stream);
        int added = 0, updated = 0;

        foreach (var r in rows)
        {
            var name = r.Get("Name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            var existing = DataStore.Data.Venues.FirstOrDefault(v =>
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

                DataStore.Data.Venues.Add(venue);
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

                updated++;
            }
        }

        RefreshVenues(SearchEntry.Text);
        SetStatus($"Imported: {added} added, {updated} updated");
    }

    private void SetStatus(string msg) => StatusLbl.Text = $"{DateTime.Now:HH:mm:ss} {msg}";
    private void OnBurgerMenuClicked(object? sender, EventArgs e)
    {
        if (_isFlyoutOpen)
            CloseFlyout();
        else
            OpenFlyout();
    }

    private void OnCloseFlyoutClicked(object? sender, EventArgs e)
    {
        CloseFlyout();
    }

    private async void OpenFlyout()
    {
        _isFlyoutOpen = true;
        FlyoutOverlay.IsVisible = true;
        FlyoutPanel.IsVisible = true;

        // Animate flyout sliding in
        FlyoutPanel.TranslationX = -400;
        await FlyoutPanel.TranslateTo(0, 0, 250, Easing.CubicOut);
    }

    private async void CloseFlyout()
    {
        // Animate flyout sliding out
        await FlyoutPanel.TranslateTo(-400, 0, 250, Easing.CubicIn);
        
        FlyoutOverlay.IsVisible = false;
        FlyoutPanel.IsVisible = false;
        _isFlyoutOpen = false;
    }

    private void ShowVenueInfo(Venue venue)
    {
        EmptyStatePanel.IsVisible = false;
        VenueInfoPanel.IsVisible = true;

        SelectedVenueName.Text = venue.Name;
        SelectedVenueAddress.Text = venue.Address ?? "";
        SelectedVenueStats.Text = $"{venue.Tables.Count} table(s)";
    }

    private void HideVenueInfo()
    {
        EmptyStatePanel.IsVisible = true;
        VenueInfoPanel.IsVisible = false;
    }
}