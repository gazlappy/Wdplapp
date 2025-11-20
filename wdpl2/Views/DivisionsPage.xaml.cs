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

public partial class DivisionsPage : ContentPage
{
    private readonly ObservableCollection<Division> _divisions = new();
    private Division? _selected;
    private bool _isMultiSelectMode = false;
    private Guid? _currentSeasonId;
    private bool _isFlyoutOpen = false;

    public DivisionsPage()
    {
        InitializeComponent();

        DivisionsList.ItemsSource = _divisions;

        // Wire up burger menu and flyout
        BurgerMenuBtn.Clicked += OnBurgerMenuClicked;
        CloseFlyoutBtn.Clicked += OnCloseFlyoutClicked;
        OverlayTap.Tapped += (_, __) => CloseFlyout();

        SearchEntry.TextChanged += (_, __) => RefreshDivisions(SearchEntry.Text);
        DivisionsList.SelectionChanged += OnSelectionChanged;

        AddBtn.Clicked += OnAdd;
        UpdateBtn.Clicked += OnUpdate;
        DeleteBtn.Clicked += OnDelete;
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

        ExportBtn.Clicked += async (_, __) => await ExportDivisionsAsync();
        DivisionsImport.ImportRequested += async (stream, fileName) => await ImportDivisionsCsvAsync(stream, fileName);

        // SUBSCRIBE to global season changes
        SeasonService.SeasonChanged += OnGlobalSeasonChanged;

        RefreshAll();
    }

    ~DivisionsPage()
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
            System.Diagnostics.Debug.WriteLine($"DivisionsPage OnAppearing Error: {ex}");
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
                RefreshDivisions(SearchEntry?.Text);
                SetStatus($"Season changed to: {e.NewSeason?.Name ?? "None"}");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DivisionsPage Season change error: {ex}");
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

            RefreshDivisions(SearchEntry?.Text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DivisionsPage RefreshAll Error: {ex}");
            SetStatus($"Refresh error: {ex.Message}");
        }
    }

    private void RefreshDivisions(string? search)
    {
        try
        {
            _divisions.Clear();

            if (!_currentSeasonId.HasValue)
            {
                SetStatus("No season selected");
                return;
            }

            if (DataStore.Data?.Divisions == null)
            {
                SetStatus("No divisions data available");
                return;
            }

            var divisions = DataStore.Data.Divisions
                .Where(d => d != null && d.SeasonId == _currentSeasonId.Value)
                .OrderBy(d => d.Name ?? "")
                .ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                divisions = divisions.Where(d => (d.Name ?? "").ToLower().Contains(lower))
                    .OrderBy(d => d.Name ?? "")
                    .ToList();
            }

            foreach (var d in divisions)
                _divisions.Add(d);

            var season = DataStore.Data.Seasons?.FirstOrDefault(s => s.Id == _currentSeasonId);
            var seasonInfo = season != null ? $" in {season.Name}" : "";
            var importedTag = season != null && !season.IsActive ? " (Imported)" : "";
            SetStatus($"{_divisions.Count} division(s){seasonInfo}{importedTag}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DivisionsPage RefreshDivisions Error: {ex}");
            SetStatus($"Error loading divisions: {ex.Message}");
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isMultiSelectMode) return;

        var item = e.CurrentSelection?.FirstOrDefault() as Division;
        if (item == null)
        {
            _selected = null;
            ClearEditor();
            HideDivisionInfo();
            return;
        }

        _selected = item;
        LoadEditor(_selected);
        ShowDivisionInfo(_selected);
    }

    private void LoadEditor(Division division)
    {
        NameEntry.Text = division.Name;
        NotesEntry.Text = division.Notes;
    }

    private void ClearEditor()
    {
        NameEntry.Text = "";
        NotesEntry.Text = "";
    }

    private void OnAdd(object? sender, EventArgs e)
    {
        if (!_currentSeasonId.HasValue)
        {
            SetStatus("Please select a season on the Seasons page first");
            return;
        }

        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetStatus("Division name required");
            return;
        }

        var division = new Division
        {
            SeasonId = _currentSeasonId.Value,
            Name = name,
            Notes = NotesEntry.Text?.Trim()
        };

        DataStore.Data.Divisions.Add(division);
        RefreshDivisions(SearchEntry.Text);
        SetStatus($"Added: {name}");
    }

    private void OnUpdate(object? sender, EventArgs e)
    {
        if (_selected == null)
        {
            SetStatus("No division selected");
            return;
        }

        _selected.Name = NameEntry.Text?.Trim() ?? "";
        _selected.Notes = NotesEntry.Text?.Trim();

        RefreshDivisions(SearchEntry.Text);
        SetStatus($"Updated: {_selected.Name}");
    }

    private async void OnDelete(object? sender, EventArgs e)
    {
        if (_selected == null)
        {
            SetStatus("No division selected");
            return;
        }

        var confirm = await DisplayAlert("Delete Division", $"Delete '{_selected.Name}'?", "Yes", "No");
        if (!confirm) return;

        DataStore.Data.Divisions.Remove(_selected);
        _selected = null;
        RefreshDivisions(SearchEntry.Text);
        ClearEditor();
        SetStatus("Deleted");
    }

    private void OnToggleMultiSelect(object? sender, EventArgs e)
    {
        _isMultiSelectMode = !_isMultiSelectMode;

        if (_isMultiSelectMode)
        {
            DivisionsList.SelectionMode = SelectionMode.Multiple;
            MultiSelectBtn.Text = "✓ Multi-Select ON";
            MultiSelectBtn.BackgroundColor = Color.FromArgb("#10B981");
            BulkDeleteBtn.IsVisible = true;

            UpdateBtn.IsEnabled = false;
            DeleteBtn.IsEnabled = false;
            AddBtn.IsEnabled = false;
        }
        else
        {
            DivisionsList.SelectionMode = SelectionMode.Single;
            MultiSelectBtn.Text = "☐ Multi-Select OFF";
            MultiSelectBtn.BackgroundColor = Color.FromArgb("#6B7280");
            BulkDeleteBtn.IsVisible = false;

            UpdateBtn.IsEnabled = true;
            DeleteBtn.IsEnabled = true;
            AddBtn.IsEnabled = true;
        }

        SetStatus(_isMultiSelectMode ? "Multi-select enabled" : "Multi-select disabled");
    }

    private async void OnBulkDelete(object? sender, EventArgs e)
    {
        var selectedItems = DivisionsList.SelectedItems?.Cast<Division>().ToList();

        if (selectedItems == null || selectedItems.Count == 0)
        {
            await DisplayAlert("No Selection", "Please select divisions to delete.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Bulk Delete",
            $"Delete {selectedItems.Count} division(s)?",
            "Yes, Delete",
            "Cancel");

        if (!confirm) return;

        int deleted = 0;
        foreach (var item in selectedItems)
        {
            DataStore.Data.Divisions.Remove(item);
            deleted++;
        }

        RefreshDivisions(SearchEntry.Text);
        SetStatus($"Deleted {deleted} division(s)");
    }

    private async Task ExportDivisionsAsync()
    {
        if (!_currentSeasonId.HasValue)
        {
            await DisplayAlert("No Season", "Please select a season on the Seasons page first.", "OK");
            return;
        }

        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
        var csv = new StringBuilder();
        csv.AppendLine("Name,Notes");

        var divisions = DataStore.Data.Divisions.Where(d => d.SeasonId == _currentSeasonId).OrderBy(d => d.Name);

        foreach (var d in divisions)
        {
            csv.AppendLine($"\"{d.Name}\",\"{d.Notes}\"");
        }

        var fileName = $"Divisions_{season?.Name?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(path, csv.ToString());

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Export Divisions",
            File = new ShareFile(path)
        });

        SetStatus($"Exported {divisions.Count()} divisions");
    }

    private async Task ImportDivisionsCsvAsync(Stream stream, string fileName)
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

            var existing = DataStore.Data.Divisions.FirstOrDefault(d =>
                d.SeasonId == _currentSeasonId &&
                string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

            var notes = r.Get("Notes");

            if (existing == null)
            {
                var division = new Division
                {
                    SeasonId = _currentSeasonId.Value,
                    Name = name.Trim(),
                    Notes = notes
                };
                DataStore.Data.Divisions.Add(division);
                added++;
            }
            else
            {
                existing.Notes = notes;
                updated++;
            }
        }

        RefreshDivisions(SearchEntry.Text);
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

    private void ShowDivisionInfo(Division division)
    {
        EmptyStatePanel.IsVisible = false;
        DivisionInfoPanel.IsVisible = true;

        SelectedDivisionName.Text = division.Name;
        SelectedDivisionNotes.Text = division.Notes ?? "No notes";
    }

    private void HideDivisionInfo()
    {
        EmptyStatePanel.IsVisible = true;
        DivisionInfoPanel.IsVisible = false;
    }
}