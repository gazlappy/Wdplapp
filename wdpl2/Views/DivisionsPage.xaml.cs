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
    private readonly ObservableCollection<DivisionTeamItem> _divisionTeams = new();
    private readonly ObservableCollection<DivisionFixtureItem> _divisionFixtures = new();
    private Division? _selected;
    private bool _isMultiSelectMode = false;
    private Guid? _currentSeasonId;
    private bool _isFlyoutOpen = false;
    private bool _showAllSeasons = false;

    public DivisionsPage()
    {
        InitializeComponent();

        DivisionsList.ItemsSource = _divisions;
        DivisionTeamsDisplay.ItemsSource = _divisionTeams;
        DivisionFixturesDisplay.ItemsSource = _divisionFixtures;

        // Wire up burger menu and flyout
        BurgerMenuBtn.Clicked += OnBurgerMenuClicked;
        CloseFlyoutBtn.Clicked += OnCloseFlyoutClicked;
        OverlayTap.Tapped += (_, __) => CloseFlyout();

        SearchEntry.TextChanged += (_, __) => RefreshDivisions(SearchEntry.Text);
        DivisionsList.SelectionChanged += OnSelectionChanged;
        
        // Wire up show all seasons toggle
        ShowAllSeasonsCheck.CheckedChanged += (_, __) =>
        {
            _showAllSeasons = ShowAllSeasonsCheck.IsChecked;
            RefreshDivisions(SearchEntry?.Text);
        };

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

        DebugCheckBtn.Clicked += async (_, __) => await CheckDatabaseAsync();

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
            _currentSeasonId = SeasonService.CurrentSeasonId;

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

            System.Diagnostics.Debug.WriteLine($"=== DIVISIONS DEBUG ===");
            System.Diagnostics.Debug.WriteLine($"Current Season ID: {_currentSeasonId}");
            System.Diagnostics.Debug.WriteLine($"Show All Seasons: {_showAllSeasons}");
            System.Diagnostics.Debug.WriteLine($"Total Divisions in DB: {DataStore.Data?.Divisions?.Count ?? 0}");
            
            if (DataStore.Data?.Divisions != null)
            {
                foreach (var d in DataStore.Data.Divisions)
                {
                    System.Diagnostics.Debug.WriteLine($"  Division: '{d.Name}' (SeasonId: {d.SeasonId})");
                }
            }

            if (!_showAllSeasons && !_currentSeasonId.HasValue)
            {
                SetStatus("No season selected - check 'Show all seasons' to see all data");
                System.Diagnostics.Debug.WriteLine("No season selected and show all seasons is OFF");
                return;
            }

            if (DataStore.Data?.Divisions == null)
            {
                SetStatus("No divisions data available");
                System.Diagnostics.Debug.WriteLine("DataStore.Data.Divisions is NULL");
                return;
            }

            var divisions = _showAllSeasons
                ? DataStore.Data.Divisions
                    .Where(d => d != null)
                    .OrderBy(d => d.Name ?? "")
                    .ToList()
                : DataStore.Data.Divisions
                    .Where(d => d != null && _currentSeasonId.HasValue && d.SeasonId == _currentSeasonId.Value)
                    .OrderBy(d => d.Name ?? "")
                    .ToList();

            System.Diagnostics.Debug.WriteLine($"Filtered Divisions: {divisions.Count}");
            
            foreach (var d in divisions)
            {
                System.Diagnostics.Debug.WriteLine($"  Will display: '{d.Name}' (SeasonId: {d.SeasonId})");
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                divisions = divisions.Where(d => (d.Name ?? "").ToLower().Contains(lower))
                    .OrderBy(d => d.Name ?? "")
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"After search filter: {divisions.Count}");
            }

            foreach (var d in divisions)
                _divisions.Add(d);

            System.Diagnostics.Debug.WriteLine($"Added {_divisions.Count} items to ObservableCollection");

            if (_showAllSeasons)
            {
                var seasonGroups = divisions.GroupBy(d => d.SeasonId).Count();
                SetStatus($"{_divisions.Count} division(s) across {seasonGroups} season(s)");
            }
            else
            {
                var season = DataStore.Data.Seasons?.FirstOrDefault(s => s.Id == _currentSeasonId);
                var seasonInfo = season != null ? $" in {season.Name}" : "";
                var importedTag = season != null && !season.IsActive ? " (Imported)" : "";
                
                if (_divisions.Count == 0 && DataStore.Data.Divisions.Count > 0)
                {
                    var otherSeasons = DataStore.Data.Divisions
                        .Where(d => d.SeasonId != _currentSeasonId)
                        .GroupBy(d => d.SeasonId)
                        .Select(g => new { SeasonId = g.Key, Count = g.Count() })
                        .ToList();
                    
                    if (otherSeasons.Any())
                    {
                        var otherSeasonInfo = string.Join(", ", otherSeasons.Select(s => $"{s.Count} in season {s.SeasonId}"));
                        SetStatus($"No divisions in current season. Found: {otherSeasonInfo}. Check 'Show all seasons' or go to Seasons page to switch.");
                        System.Diagnostics.Debug.WriteLine($"Found divisions in other seasons: {otherSeasonInfo}");
                        return;
                    }
                }
                
                SetStatus($"{_divisions.Count} division(s){seasonInfo}{importedTag}");
            }
            
            System.Diagnostics.Debug.WriteLine("=== END DIVISIONS DEBUG ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DivisionsPage RefreshDivisions Error: {ex}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            SetStatus($"Error loading divisions: {ex.Message}");
        }
    }
    
    private void OnShowAllSeasonsTapped(object? sender, EventArgs e)
    {
        ShowAllSeasonsCheck.IsChecked = !ShowAllSeasonsCheck.IsChecked;
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
        ShowDivisionInfo(_selected); // Refresh the info panel
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
        HideDivisionInfo();
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
        HideDivisionInfo();
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

        FlyoutPanel.TranslationX = -400;
        await FlyoutPanel.TranslateTo(0, 0, 250, Easing.CubicOut);
    }

    private async void CloseFlyout()
    {
        await FlyoutPanel.TranslateTo(-400, 0, 250, Easing.CubicIn);
        
        FlyoutOverlay.IsVisible = false;
        FlyoutPanel.IsVisible = false;
        _isFlyoutOpen = false;
    }

    private void ShowDivisionInfo(Division division)
    {
        EmptyStatePanel.IsVisible = false;
        DivisionInfoPanel.IsVisible = true;

        // Header info
        SelectedDivisionName.Text = division.Name;
        
        // Get season name
        var season = DataStore.Data?.Seasons?.FirstOrDefault(s => s.Id == division.SeasonId);
        SelectedDivisionSeason.Text = season != null ? $"Season: {season.Name}" : "Season: Unknown";
        
        // Get teams in this division
        var teamsInDivision = DataStore.Data?.Teams?
            .Where(t => t.DivisionId == division.Id)
            .OrderBy(t => t.Name)
            .ToList() ?? new();
        
        SelectedDivisionTeamCount.Text = $"{teamsInDivision.Count} teams";
        DivisionTeamCountStat.Text = teamsInDivision.Count.ToString();
        
        // Get players from teams in this division
        var teamIds = teamsInDivision.Select(t => t.Id).ToHashSet();
        var playersInDivision = DataStore.Data?.Players?
            .Where(p => p.TeamId.HasValue && teamIds.Contains(p.TeamId.Value))
            .ToList() ?? new();
        DivisionPlayerCountStat.Text = playersInDivision.Count.ToString();
        
        // Get fixtures for this division
        var fixtures = DataStore.Data?.Fixtures?
            .Where(f => f.DivisionId == division.Id)
            .OrderByDescending(f => f.Date)
            .ToList() ?? new();
        
        DivisionFixtureCountStat.Text = fixtures.Count.ToString();
        
        // Count completed fixtures (those with frame results)
        var completedFixtures = fixtures.Count(f => f.Frames != null && f.Frames.Any());
        DivisionCompletedStat.Text = completedFixtures.ToString();
        
        // Populate teams list
        _divisionTeams.Clear();
        foreach (var team in teamsInDivision)
        {
            var venue = DataStore.Data?.Venues?.FirstOrDefault(v => v.Id == team.VenueId);
            var playerCount = DataStore.Data?.Players?.Count(p => p.TeamId == team.Id) ?? 0;
            
            _divisionTeams.Add(new DivisionTeamItem
            {
                Id = team.Id,
                Name = team.Name ?? "Unknown",
                VenueName = venue?.Name ?? "No venue",
                PlayerCount = playerCount
            });
        }
        
        // Populate recent fixtures (last 10)
        _divisionFixtures.Clear();
        foreach (var fixture in fixtures.Take(10))
        {
            var homeTeam = DataStore.Data?.Teams?.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
            var awayTeam = DataStore.Data?.Teams?.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
            
            var homeScore = fixture.Frames?.Count(f => f.Winner == FrameWinner.Home) ?? 0;
            var awayScore = fixture.Frames?.Count(f => f.Winner == FrameWinner.Away) ?? 0;
            var hasResults = fixture.Frames != null && fixture.Frames.Any();
            
            _divisionFixtures.Add(new DivisionFixtureItem
            {
                Id = fixture.Id,
                Date = fixture.Date,
                MatchTitle = $"{homeTeam?.Name ?? "?"} vs {awayTeam?.Name ?? "?"}",
                ScoreText = hasResults ? $"{homeScore}-{awayScore}" : "TBD"
            });
        }
        
        // Notes section
        if (!string.IsNullOrWhiteSpace(division.Notes))
        {
            NotesSection.IsVisible = true;
            SelectedDivisionNotes.Text = division.Notes;
        }
        else
        {
            NotesSection.IsVisible = false;
        }
    }

    private void HideDivisionInfo()
    {
        EmptyStatePanel.IsVisible = true;
        DivisionInfoPanel.IsVisible = false;
        _divisionTeams.Clear();
        _divisionFixtures.Clear();
    }
    
    // Helper classes for display
    public class DivisionTeamItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string VenueName { get; set; } = "";
        public int PlayerCount { get; set; }
    }
    
    public class DivisionFixtureItem
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public string MatchTitle { get; set; } = "";
        public string ScoreText { get; set; } = "";
    }
    
    // Comprehensive database check
    private async System.Threading.Tasks.Task CheckDatabaseAsync()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🔍 DATABASE DIAGNOSTIC CHECK\n");
            sb.AppendLine("================================\n");
            
            sb.AppendLine($"DataStore.Data is null: {DataStore.Data == null}");
            if (DataStore.Data == null)
            {
                await DisplayAlert("Database Check", sb.ToString(), "OK");
                return;
            }
            
            sb.AppendLine($"\n📅 SEASONS:");
            sb.AppendLine($"Total Seasons: {DataStore.Data.Seasons?.Count ?? 0}");
            sb.AppendLine($"Active Season ID: {DataStore.Data.ActiveSeasonId?.ToString() ?? "NOT SET"}");
            sb.AppendLine($"Current Season ID (page): {_currentSeasonId?.ToString() ?? "NOT SET"}");
            
            if (DataStore.Data.Seasons != null && DataStore.Data.Seasons.Any())
            {
                sb.AppendLine($"\nSeason List:");
                foreach (var season in DataStore.Data.Seasons.OrderByDescending(s => s.IsActive))
                {
                    var marker = season.IsActive ? "✓ ACTIVE" : "  ";
                    sb.AppendLine($"{marker} {season.Name}");
                    sb.AppendLine($"    ID: {season.Id}");
                    sb.AppendLine($"    IsActive: {season.IsActive}");
                }
            }
            
            sb.AppendLine($"\n🏆 DIVISIONS:");
            sb.AppendLine($"Total Divisions: {DataStore.Data.Divisions?.Count ?? 0}");
            
            if (DataStore.Data.Divisions != null && DataStore.Data.Divisions.Any())
            {
                sb.AppendLine($"\nDivision List:");
                foreach (var div in DataStore.Data.Divisions.OrderBy(d => d.Name))
                {
                    var seasonName = "UNKNOWN";
                    if (div.SeasonId.HasValue)
                    {
                        var season = DataStore.Data.Seasons?.FirstOrDefault(s => s.Id == div.SeasonId.Value);
                        seasonName = season?.Name ?? "Season Not Found";
                    }
                    else
                    {
                        seasonName = "NO SEASON ID";
                    }
                    
                    sb.AppendLine($"  • {div.Name}");
                    sb.AppendLine($"    Season: {seasonName}");
                    sb.AppendLine($"    SeasonId: {div.SeasonId?.ToString() ?? "NULL"}");
                }
                
                var grouped = DataStore.Data.Divisions
                    .GroupBy(d => d.SeasonId)
                    .Select(g => new { 
                        SeasonId = g.Key, 
                        Count = g.Count(),
                        SeasonName = g.Key.HasValue 
                            ? DataStore.Data.Seasons?.FirstOrDefault(s => s.Id == g.Key.Value)?.Name ?? "Unknown"
                            : "No Season"
                    })
                    .ToList();
                    
                sb.AppendLine($"\n📊 Divisions by Season:");
                foreach (var group in grouped)
                {
                    var currentMarker = group.SeasonId == _currentSeasonId ? " ← CURRENT" : "";
                    sb.AppendLine($"  {group.SeasonName}: {group.Count} division(s){currentMarker}");
                    sb.AppendLine($"    SeasonId: {group.SeasonId}");
                }
            }
            else
            {
                sb.AppendLine("  ❌ NO DIVISIONS FOUND IN DATABASE!");
            }
            
            sb.AppendLine($"\n👥 TEAMS:");
            sb.AppendLine($"Total Teams: {DataStore.Data.Teams?.Count ?? 0}");
            
            sb.AppendLine($"\n🎱 PLAYERS:");
            sb.AppendLine($"Total Players: {DataStore.Data.Players?.Count ?? 0}");
            
            sb.AppendLine($"\n🏠 VENUES:");
            sb.AppendLine($"Total Venues: {DataStore.Data.Venues?.Count ?? 0}");
            
            sb.AppendLine($"\n📋 FIXTURES:");
            sb.AppendLine($"Total Fixtures: {DataStore.Data.Fixtures?.Count ?? 0}");
            
            sb.AppendLine($"\n🖥️ UI STATE:");
            sb.AppendLine($"Show All Seasons: {_showAllSeasons}");
            sb.AppendLine($"Items in ObservableCollection: {_divisions.Count}");
            sb.AppendLine($"DivisionsList.ItemsSource is null: {DivisionsList.ItemsSource == null}");
            
            await DisplayAlert("Database Check", sb.ToString(), "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Database check failed:\n\n{ex.Message}\n\n{ex.StackTrace}", "OK");
        }
    }
}