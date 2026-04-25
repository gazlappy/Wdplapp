using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Storage;
using Wdpl2.Helpers;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Views;

public partial class SmartImportPage : ContentPage
{
    private readonly LeagueFileDiscoveryService _discoveryService = new();
    private readonly Dictionary<string, bool> _scanLocations = new();
    private List<LeagueFileDiscoveryService.DiscoveredFile> _discoveredFiles = new();
    private List<LeagueFileDiscoveryService.SeasonGroup> _seasonGroups = new();
    private List<LeagueFileDiscoveryService.SeasonGroup> _allSeasonGroups = new(); // unfiltered copy
    private CancellationTokenSource? _scanCts;
    private int _currentStep = 1;
    private string? _activeYearFilter;

    public SmartImportPage()
    {
        InitializeComponent();
        PopulateScanLocations();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ResetWizard();
    }

    // ── Initialisation ─────────────────────────────────────────────

    private void PopulateScanLocations()
    {
        LocationsPanel.Children.Clear();
        _scanLocations.Clear();

        var defaults = LeagueFileDiscoveryService.GetDefaultScanLocations();

        foreach (var (path, label, defaultChecked) in defaults)
        {
            _scanLocations[path] = defaultChecked;
            AddLocationCheckbox(label, path, defaultChecked);
        }
    }

    private void AddLocationCheckbox(string label, string path, bool isChecked)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8,
            Padding = new Thickness(0, 2)
        };

        var checkbox = new CheckBox
        {
            IsChecked = isChecked,
            Color = Color.FromArgb("#3B82F6"),
            VerticalOptions = LayoutOptions.Center
        };
        checkbox.CheckedChanged += (s, e) => _scanLocations[path] = e.Value;

        var stack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 1 };
        stack.Children.Add(new Label { Text = label, FontSize = 13, FontAttributes = FontAttributes.Bold });
        stack.Children.Add(new Label { Text = path, FontSize = 11, TextColor = Color.FromArgb("#888") });

        grid.Add(checkbox, 0, 0);
        grid.Add(stack, 1, 0);
        LocationsPanel.Children.Add(grid);
    }

    // ── Step Management ────────────────────────────────────────────

    private void ResetWizard()
    {
        _currentStep = 1;
        _discoveredFiles.Clear();
        _seasonGroups.Clear();
        _allSeasonGroups.Clear();
        _activeYearFilter = null;
        _scanCts?.Cancel();
        _scanCts = null;
        UpdateStepDisplay();
    }

    private void UpdateStepDisplay()
    {
        UpdateStepIndicator(Step1Border, _currentStep >= 1);
        UpdateStepIndicator(Step2Border, _currentStep >= 2);
        UpdateStepIndicator(Step3Border, _currentStep >= 3);

        Step1Content.IsVisible = _currentStep == 1;
        Step2Content.IsVisible = _currentStep == 2;
        Step3Content.IsVisible = _currentStep == 3;

        BackButton.IsVisible = _currentStep == 2;
        NextButton.IsVisible = _currentStep == 2 && _seasonGroups.Any(g => g.IsSelected);
        NextButton.Text = _seasonGroups.Any(g => g.IsSelected && g.HasData)
            ? $"Import {_seasonGroups.Count(g => g.IsSelected && g.HasData)} Season{(_seasonGroups.Count(g => g.IsSelected && g.HasData) != 1 ? "s" : "")} →"
            : "Import Selected →";
        CancelButton.IsVisible = _currentStep < 3;
    }

    private static void UpdateStepIndicator(Border border, bool isActive)
    {
        border.BackgroundColor = isActive
            ? Colors.White
            : Color.FromArgb("#FFFFFF40");

        if (border.Content is HorizontalStackLayout hsl)
        {
            foreach (var child in hsl.Children.OfType<Label>())
            {
                child.TextColor = isActive
                    ? Color.FromArgb("#3B82F6")
                    : Colors.White;
            }
        }
    }

    // ── Step 1: Scan ───────────────────────────────────────────────

    private async void OnAddFolderClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(default);
            if (result.IsSuccessful && !string.IsNullOrEmpty(result.Folder?.Path))
            {
                var path = result.Folder.Path;
                if (!_scanLocations.ContainsKey(path))
                {
                    _scanLocations[path] = true;
                    AddLocationCheckbox($"📂 {Path.GetFileName(path)}", path, true);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Folder picker error: {ex.Message}");
        }
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        var selectedPaths = _scanLocations.Where(kv => kv.Value).Select(kv => kv.Key).ToList();

        if (selectedPaths.Count == 0)
        {
            await DisplayAlert("No Locations", "Please select at least one folder to scan.", "OK");
            return;
        }

        ScanButton.IsVisible = false;
        ScanProgressPanel.IsVisible = true;
        AddFolderButton.IsEnabled = false;
        _scanCts = new CancellationTokenSource();

        var progress = new Progress<LeagueFileDiscoveryService.ScanProgress>(p =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var shortPath = p.CurrentPath;
                if (shortPath.Length > 60)
                    shortPath = "..." + shortPath[^57..];
                ScanProgressLabel.Text = $"Scanning: {shortPath}";
                ScanCountLabel.Text = $"{p.FilesFound} league files found ({p.FilesScanned:N0} files scanned)";
            });
        });

        try
        {
            _discoveredFiles = await _discoveryService.ScanAsync(selectedPaths, progress, _scanCts.Token);

            if (_scanCts.Token.IsCancellationRequested)
            {
                ScanButton.IsVisible = true;
                ScanProgressPanel.IsVisible = false;
                AddFolderButton.IsEnabled = true;
                return;
            }

            if (_discoveredFiles.Count == 0)
            {
                ScanButton.IsVisible = true;
                ScanProgressPanel.IsVisible = false;
                AddFolderButton.IsEnabled = true;
                await DisplayAlert("No Files Found",
                    "No pool league files were found in the selected locations.\n\n" +
                    "Try adding a custom folder where your league data is stored.", "OK");
                return;
            }

            // Group by season (with normalization + merge)
            _seasonGroups = _discoveryService.GroupBySeason(_discoveredFiles);

            // Analyze HTML files to count actual data per group
            ScanProgressLabel.Text = "Analyzing file contents...";
            ScanCountLabel.Text = "Pre-parsing HTML files to detect teams, players, results...";

            var analyzeProgress = new Progress<LeagueFileDiscoveryService.ScanProgress>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ScanProgressLabel.Text = $"Analyzing: {p.CurrentPath}";
                    ScanCountLabel.Text = $"{p.FilesScanned}/{p.FilesFound} files analyzed";
                });
            });

            await LeagueFileDiscoveryService.AnalyzeGroupsAsync(
                _seasonGroups, analyzeProgress, _scanCts.Token);

            // Store unfiltered groups
            _allSeasonGroups = _seasonGroups;

            // Apply year filter if specified
            _activeYearFilter = YearFilterEntry.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(_activeYearFilter))
            {
                _seasonGroups = FilterGroupsByYear(_allSeasonGroups, _activeYearFilter);
            }

            _currentStep = 2;
            UpdateStepDisplay();
            BuildReviewUI();
        }
        catch (OperationCanceledException)
        {
            // Scan cancelled
        }
        catch (Exception ex)
        {
            await DisplayAlert("Scan Error", $"An error occurred during scanning:\n{ex.Message}", "OK");
        }
        finally
        {
            ScanButton.IsVisible = true;
            ScanProgressPanel.IsVisible = false;
            AddFolderButton.IsEnabled = true;
        }
    }

    private void OnCancelScanClicked(object? sender, EventArgs e)
    {
        _scanCts?.Cancel();
    }

    private void OnClearYearFilterClicked(object? sender, EventArgs e)
    {
        YearFilterEntry.Text = "";
    }

    private void OnClearReviewFilterClicked(object? sender, EventArgs e)
    {
        _activeYearFilter = null;
        _seasonGroups = _allSeasonGroups;
        ReviewFilterBorder.IsVisible = false;
        BuildReviewUI();
    }

    /// <summary>
    /// Filter season groups to only those matching a year string (e.g. "2014", "2023-24").
    /// </summary>
    private static List<LeagueFileDiscoveryService.SeasonGroup> FilterGroupsByYear(
        List<LeagueFileDiscoveryService.SeasonGroup> groups, string yearFilter)
    {
        var filter = yearFilter.Trim();
        return groups.Where(g =>
        {
            var key = g.SeasonKey;
            // Direct match
            if (key.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;
            // Match start year from range (e.g. filter "2014" matches "2014-15")
            if (filter.Length == 4 && key.StartsWith(filter))
                return true;
            // Match display name
            if (g.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }).ToList();
    }

    // ── Step 2: Review ─────────────────────────────────────────────

    private void BuildReviewUI()
    {
        // Show/hide year filter bar
        if (!string.IsNullOrWhiteSpace(_activeYearFilter))
        {
            ReviewFilterBorder.IsVisible = true;
            ReviewFilterLabel.Text = _activeYearFilter;
        }
        else
        {
            ReviewFilterBorder.IsVisible = false;
        }

        var totalFiles = _seasonGroups.Sum(g => g.Files.Count);
        var totalSeasons = _seasonGroups.Count;
        var existingSeasons = _seasonGroups.Count(g => g.IsExistingSeason);
        var newSeasons = _seasonGroups.Count(g => !g.IsExistingSeason && g.HasData);
        var emptyGroups = _seasonGroups.Count(g => g.IsAnalyzed && !g.HasData);
        var htmlCount = _seasonGroups.Sum(g => g.HtmlCount);
        var dbCount = _seasonGroups.Sum(g => g.DatabaseCount);
        var totalTeams = _seasonGroups.Where(g => g.IsSelected).Sum(g => g.AnalyzedTeams);
        var totalPlayers = _seasonGroups.Where(g => g.IsSelected).Sum(g => g.AnalyzedPlayers);
        var totalResults = _seasonGroups.Where(g => g.IsSelected).Sum(g => g.AnalyzedResults);
        var seasonsWithData = _seasonGroups.Count(g => g.HasData);

        ReviewTitle.Text = $"Found {totalFiles} Files → {seasonsWithData} Season{(seasonsWithData != 1 ? "s" : "")} with Data";

        SummaryLabel.Text = $"📊 {totalFiles} files across {totalSeasons} detected season{(totalSeasons != 1 ? "s" : "")}";

        var details = new List<string>();
        if (seasonsWithData > 0) details.Add($"✅ {seasonsWithData} season{(seasonsWithData != 1 ? "s" : "")} contain importable data");
        if (existingSeasons > 0) details.Add($"🔗 {existingSeasons} match existing season{(existingSeasons != 1 ? "s" : "")} (will merge)");
        if (newSeasons > 0) details.Add($"🆕 {newSeasons} new season{(newSeasons != 1 ? "s" : "")} to create");
        if (emptyGroups > 0) details.Add($"⚠️ {emptyGroups} season{(emptyGroups != 1 ? "s" : "")} with no detectable data (auto-skipped)");
        if (totalTeams > 0) details.Add($"👥 {totalTeams} teams, {totalPlayers} players, {totalResults} results detected");
        SummaryDetailLabel.Text = string.Join("\n", details);

        SeasonGroupsPanel.Children.Clear();

        // Show seasons WITH data first, then empty ones
        var orderedGroups = _seasonGroups
            .OrderByDescending(g => g.HasData)
            .ThenBy(g => g.Files.FirstOrDefault()?.SeasonSortKey ?? "9999")
            .ToList();

        foreach (var group in orderedGroups)
        {
            SeasonGroupsPanel.Children.Add(BuildSeasonGroupCard(group));
        }
    }

    private View BuildSeasonGroupCard(LeagueFileDiscoveryService.SeasonGroup group)
    {
        var hasData = group.HasData;
        var border = new Border
        {
            Padding = 0,
            StrokeThickness = hasData ? (group.IsExistingSeason ? 2 : 1) : 1,
            Stroke = !hasData
                ? Color.FromArgb("#D1D5DB")
                : group.IsExistingSeason
                    ? Color.FromArgb("#10B981")
                    : Color.FromArgb("#E5E7EB"),
            BackgroundColor = !hasData
                ? Color.FromArgb("#F3F4F6")
                : group.IsExistingSeason
                    ? Color.FromArgb("#F0FDF4")
                    : Color.FromArgb("#F9FAFB"),
            Margin = new Thickness(0, 0, 0, 4),
            Opacity = hasData ? 1.0 : 0.7
        };

        var mainStack = new VerticalStackLayout { Padding = new Thickness(16), Spacing = 8 };

        // Header row: checkbox + season name + badge
        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        var checkbox = new CheckBox
        {
            IsChecked = group.IsSelected,
            Color = Color.FromArgb("#3B82F6"),
            VerticalOptions = LayoutOptions.Center
        };
        checkbox.CheckedChanged += (s, e) =>
        {
            group.IsSelected = e.Value;
            NextButton.IsVisible = _seasonGroups.Any(g => g.IsSelected);
        };

        var nameStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2 };
        nameStack.Children.Add(new Label
        {
            Text = $"📅 {group.DisplayName}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        });

        if (group.IsExistingSeason)
        {
            nameStack.Children.Add(new Label
            {
                Text = $"✅ Matches existing: \"{group.ExistingSeasonName}\" — data will merge",
                FontSize = 11,
                TextColor = Color.FromArgb("#10B981")
            });
        }
        else if (hasData)
        {
            nameStack.Children.Add(new Label
            {
                Text = "🆕 New season will be created",
                FontSize = 11,
                TextColor = Color.FromArgb("#3B82F6")
            });
        }
        else
        {
            nameStack.Children.Add(new Label
            {
                Text = "⚠️ No importable data detected — skipped",
                FontSize = 11,
                TextColor = Color.FromArgb("#9CA3AF")
            });
        }

        // Data count badge
        var badgeColor = hasData ? Color.FromArgb("#3B82F6") : Color.FromArgb("#9CA3AF");
        var badgeText = hasData
            ? $"{group.TotalEntities} entities"
            : "No data";
        var countBadge = new Border
        {
            BackgroundColor = badgeColor,
            Padding = new Thickness(10, 4),
            StrokeThickness = 0,
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = badgeText,
                TextColor = Colors.White,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold
            }
        };

        headerGrid.Add(checkbox, 0, 0);
        headerGrid.Add(nameStack, 1, 0);
        headerGrid.Add(countBadge, 2, 0);
        mainStack.Children.Add(headerGrid);

        // Data summary line (teams, players, results)
        if (group.IsAnalyzed && hasData)
        {
            mainStack.Children.Add(new Label
            {
                Text = $"📊 {group.DataSummary}",
                FontSize = 12,
                TextColor = Color.FromArgb("#059669"),
                FontAttributes = FontAttributes.Bold
            });
        }

        // File type summary
        mainStack.Children.Add(new Label
        {
            Text = group.Summary,
            FontSize = 12,
            TextColor = Color.FromArgb("#6B7280")
        });

        // Duplicate warning
        if (group.HasDuplicateTypes)
        {
            mainStack.Children.Add(new Label
            {
                Text = group.DuplicateInfo,
                FontSize = 11,
                TextColor = Color.FromArgb("#F59E0B")
            });
        }

        // File type breakdown (by HTML page type)
        if (group.HtmlCount > 0)
        {
            var htmlFiles = group.Files.Where(f => f.FileType == "HTML").ToList();
            var breakdownParts = new List<string>();

            // Count HTML files by detected page name pattern
            var leagueTables = htmlFiles.Count(f => f.FileName.StartsWith("table", StringComparison.OrdinalIgnoreCase));
            var playerRatings = htmlFiles.Count(f => f.FileName.StartsWith("single", StringComparison.OrdinalIgnoreCase));
            var doublesRatings = htmlFiles.Count(f => f.FileName.StartsWith("double", StringComparison.OrdinalIgnoreCase));
            var results = htmlFiles.Count(f => f.FileName.Equals("results.htm", StringComparison.OrdinalIgnoreCase) || f.FileName.Equals("results.html", StringComparison.OrdinalIgnoreCase));
            var playerProfiles = htmlFiles.Count(f => f.FileName.StartsWith("player", StringComparison.OrdinalIgnoreCase) && !f.FileName.Equals("players.htm", StringComparison.OrdinalIgnoreCase) && !f.FileName.Equals("players.html", StringComparison.OrdinalIgnoreCase));
            var playerLists = htmlFiles.Count(f => f.FileName.Equals("players.htm", StringComparison.OrdinalIgnoreCase) || f.FileName.Equals("players.html", StringComparison.OrdinalIgnoreCase));
            var fixtures = htmlFiles.Count(f => f.FileName.StartsWith("fixture", StringComparison.OrdinalIgnoreCase));

            if (leagueTables > 0) breakdownParts.Add($"📋 {leagueTables} league table{(leagueTables != 1 ? "s" : "")}");
            if (playerRatings > 0) breakdownParts.Add($"⭐ {playerRatings} player rating{(playerRatings != 1 ? "s" : "")}");
            if (doublesRatings > 0) breakdownParts.Add($"👯 {doublesRatings} doubles rating{(doublesRatings != 1 ? "s" : "")}");
            if (results > 0) breakdownParts.Add($"🏆 {results} results");
            if (fixtures > 0) breakdownParts.Add($"📅 {fixtures} fixture{(fixtures != 1 ? "s" : "")}");
            if (playerProfiles > 0) breakdownParts.Add($"👤 {playerProfiles} player profile{(playerProfiles != 1 ? "s" : "")}");
            if (playerLists > 0) breakdownParts.Add($"📜 {playerLists} player list{(playerLists != 1 ? "s" : "")}");

            var other = htmlFiles.Count - leagueTables - playerRatings - doublesRatings - results - playerProfiles - playerLists - fixtures;
            if (other > 0) breakdownParts.Add($"📄 {other} other");

            if (breakdownParts.Count > 0)
            {
                mainStack.Children.Add(new Label
                {
                    Text = $"HTML breakdown: {string.Join("  ", breakdownParts)}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#6366F1")
                });
            }
        }

        // File list (collapsible)
        var filesStack = new VerticalStackLayout { Spacing = 2, IsVisible = false };
        foreach (var file in group.Files)
        {
            var fileRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 6,
                Padding = new Thickness(4, 2)
            };

            fileRow.Add(new Label
            {
                Text = file.FileTypeIcon,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            var fileNameLabel = new Label
            {
                Text = file.FileName,
                FontSize = 12,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            fileRow.Add(fileNameLabel, 1, 0);

            fileRow.Add(new Label
            {
                Text = file.FileSizeDisplay,
                FontSize = 11,
                TextColor = Color.FromArgb("#9CA3AF"),
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);

            fileRow.Add(new Label
            {
                Text = file.ConfidenceDisplay,
                FontSize = 11,
                TextColor = file.ConfidenceColor,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            }, 3, 0);

            // Preview button for HTML files
            if (file.FileType == "HTML")
            {
                var previewBtn = new Button
                {
                    Text = "👁",
                    BackgroundColor = Colors.Transparent,
                    TextColor = Color.FromArgb("#3B82F6"),
                    FontSize = 14,
                    Padding = new Thickness(6, 0),
                    VerticalOptions = LayoutOptions.Center
                };
                ToolTipProperties.SetText(previewBtn, "Preview file data");
                var capturedFile = file;
                previewBtn.Clicked += async (s, e) => await ShowFilePreviewAsync(capturedFile);
                fileRow.Add(previewBtn, 4, 0);
            }

            filesStack.Children.Add(fileRow);
        }

        // Toggle button
        var toggleButton = new Button
        {
            Text = $"▶ Show {group.Files.Count} file{(group.Files.Count != 1 ? "s" : "")}",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#3B82F6"),
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(0, 4)
        };
        toggleButton.Clicked += (s, e) =>
        {
            filesStack.IsVisible = !filesStack.IsVisible;
            toggleButton.Text = filesStack.IsVisible
                ? $"▼ Hide files"
                : $"▶ Show {group.Files.Count} file{(group.Files.Count != 1 ? "s" : "")}";
        };

        mainStack.Children.Add(toggleButton);
        mainStack.Children.Add(filesStack);

        border.Content = mainStack;
        return border;
    }

    // ── File Preview ───────────────────────────────────────────────

    /// <summary>
    /// Parse and display a preview of an HTML file's contents in a modal popup.
    /// Shows detected page type, division, teams, players, results etc.
    /// </summary>
    private async Task ShowFilePreviewAsync(LeagueFileDiscoveryService.DiscoveredFile file)
    {
        try
        {
            var result = await Task.Run(async () => await HtmlLeagueParser.ParseHtmlFileAsync(file.FilePath));

            var lines = new List<string>
            {
                $"📄 {file.FileName}",
                $"📁 {file.FilePath}",
                $"📏 {file.FileSizeDisplay}",
                ""
            };

            if (!result.Success)
            {
                lines.Add("❌ Failed to parse file");
                foreach (var err in result.Errors)
                    lines.Add($"  • {err}");
            }
            else
            {
                lines.Add($"Page Type: {result.DetectedPageType}");
                if (!string.IsNullOrWhiteSpace(result.PageHeading))
                    lines.Add($"Heading: {result.PageHeading}");
                if (!string.IsNullOrWhiteSpace(result.DetectedDivision))
                    lines.Add($"Division: {result.DetectedDivision}");
                lines.Add($"Tables found: {result.Tables.Count}");
                lines.Add("");

                // Teams
                if (result.Teams.Count > 0)
                {
                    lines.Add($"── Teams ({result.Teams.Count}) ──");
                    foreach (var team in result.Teams.Take(15))
                    {
                        var pos = team.Position > 0 ? $"#{team.Position} " : "";
                        lines.Add($"  {pos}{team.Name} ({team.Division}) P:{team.Played} W:{team.Won} Pts:{team.Points}");
                    }
                    if (result.Teams.Count > 15)
                        lines.Add($"  ... and {result.Teams.Count - 15} more");
                    lines.Add("");
                }

                // Players
                if (result.Players.Count > 0)
                {
                    lines.Add($"── Players ({result.Players.Count}) ──");
                    foreach (var player in result.Players.Take(15))
                    {
                        var rating = player.CurrentRating > 0 ? $" R:{player.CurrentRating}" : "";
                        lines.Add($"  {player.Name} ({player.TeamName}) P:{player.Played} W:{player.Won}{rating}");
                    }
                    if (result.Players.Count > 15)
                        lines.Add($"  ... and {result.Players.Count - 15} more");
                    lines.Add("");
                }

                // Results
                if (result.Results.Count > 0)
                {
                    lines.Add($"── Results ({result.Results.Count}) ──");
                    foreach (var r in result.Results.Take(10))
                        lines.Add($"  {r.Date:dd/MM/yyyy} {r.HomeTeam} {r.HomeScore}-{r.AwayScore} {r.AwayTeam}");
                    if (result.Results.Count > 10)
                        lines.Add($"  ... and {result.Results.Count - 10} more");
                    lines.Add("");
                }

                // Player profile
                if (result.PlayerProfile != null)
                {
                    var p = result.PlayerProfile;
                    lines.Add($"── Player Profile ──");
                    lines.Add($"  {p.PlayerName} ({p.TeamName})");
                    lines.Add($"  P:{p.Played} W:{p.Won} L:{p.Lost} Rating:{p.CurrentRating}");
                    lines.Add($"  Match history: {p.MatchHistory.Count} match{(p.MatchHistory.Count != 1 ? "es" : "")}");
                    foreach (var m in p.MatchHistory.Take(8))
                        lines.Add($"    {m.Date:dd/MM/yyyy} vs {m.OpponentName} ({m.OpponentTeam}) {m.Result} R:{m.RatingAttained} W:{m.Weighting}");
                    if (p.MatchHistory.Count > 8)
                        lines.Add($"    ... and {p.MatchHistory.Count - 8} more");
                    lines.Add("");
                }

                // Doubles
                if (result.DoublesEntries.Count > 0)
                {
                    lines.Add($"── Doubles Entries ({result.DoublesEntries.Count}) ──");
                    foreach (var d in result.DoublesEntries.Take(10))
                        lines.Add($"  {d.Player1Name} & {d.Player2Name} ({d.TeamName}) P:{d.Played} W:{d.Won}");
                    if (result.DoublesEntries.Count > 10)
                        lines.Add($"  ... and {result.DoublesEntries.Count - 10} more");
                    lines.Add("");
                }

                // Player list
                if (result.PlayerListEntries.Count > 0)
                {
                    lines.Add($"── Player List ({result.PlayerListEntries.Count}) ──");
                    foreach (var entry in result.PlayerListEntries.Take(20))
                        lines.Add($"  {entry.Name}{(entry.ProfileLink != null ? $" → {entry.ProfileLink}" : "")}");
                    if (result.PlayerListEntries.Count > 20)
                        lines.Add($"  ... and {result.PlayerListEntries.Count - 20} more");
                    lines.Add("");
                }

                // Competitions
                if (result.DetectedCompetitions.Count > 0)
                {
                    lines.Add($"── Competitions ({result.DetectedCompetitions.Count}) ──");
                    foreach (var c in result.DetectedCompetitions)
                        lines.Add($"  {c.Name}");
                    lines.Add("");
                }

                // Warnings
                if (result.Warnings.Count > 0)
                {
                    lines.Add($"── Warnings ──");
                    foreach (var w in result.Warnings)
                        lines.Add($"  ⚠️ {w}");
                }

                // Viability assessment
                lines.Add("");
                var entityCount = result.Teams.Count + result.Players.Count + result.Results.Count;
                if (result.PlayerProfile != null) entityCount += result.PlayerProfile.MatchHistory.Count;
                entityCount += result.DoublesEntries.Count + result.PlayerListEntries.Count;

                if (entityCount > 0)
                    lines.Add($"✅ Viable — {entityCount} importable entities detected");
                else
                    lines.Add("⚠️ No importable data detected in this file");
            }

            // Show in a scrollable page
            var previewPage = new ContentPage
            {
                Title = $"Preview: {file.FileName}"
            };
            var scrollView = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(16),
                    Spacing = 2,
                    Children =
                    {
                        new Label
                        {
                            Text = string.Join("\n", lines),
                            FontSize = 12,
                            FontFamily = "Consolas",
                            LineHeight = 1.3
                        }
                    }
                }
            };
            var closeBtn = new Button
            {
                Text = "Close Preview",
                BackgroundColor = Color.FromArgb("#3B82F6"),
                TextColor = Colors.White,
                Margin = new Thickness(16, 8),
                Padding = new Thickness(16, 10)
            };
            closeBtn.Clicked += async (s, e) => await Navigation.PopModalAsync();

            previewPage.Content = new Grid
            {
                RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
                Children = { scrollView, closeBtn }
            };
            Grid.SetRow(closeBtn, 1);

            await Navigation.PushModalAsync(new NavigationPage(previewPage));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Preview Error", $"Failed to preview file:\n{ex.Message}", "OK");
        }
    }

    // ── Step 3: Import ─────────────────────────────────────────────

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        var selectedGroups = _seasonGroups.Where(g => g.IsSelected).ToList();

        if (selectedGroups.Count == 0)
        {
            await DisplayAlert("Nothing Selected", "Please select at least one season group to import.", "OK");
            return;
        }

        var dataGroups = selectedGroups.Where(g => g.HasData || g.Files.Any(f => f.FileType != "HTML")).ToList();

        if (dataGroups.Count == 0)
        {
            await DisplayAlert("No Data", "None of the selected seasons contain importable data.", "OK");
            return;
        }

        var totalFiles = dataGroups.Sum(g => g.Files.Count);
        var confirmMsg = $"Import {totalFiles} file{(totalFiles != 1 ? "s" : "")} across {dataGroups.Count} season{(dataGroups.Count != 1 ? "s" : "")}?\n\n" +
            "• HTML files: teams, players, results imported directly\n" +
            "• SQL files: full database import automatically\n" +
            "• Paradox databases: full import with frames\n" +
            "• Duplicate entries will be skipped";

        var confirm = await DisplayAlert("Confirm Import", confirmMsg, "Import", "Cancel");

        if (!confirm) return;

        _currentStep = 3;
        UpdateStepDisplay();
        ImportProgressPanel.IsVisible = true;

        var totalCreated = new ImportTotals();
        var manualFiles = new List<(string seasonName, LeagueFileDiscoveryService.DiscoveredFile file)>();
        var errors = new List<string>();
        int processedGroups = 0;

        try
        {
            foreach (var group in dataGroups)
            {
                processedGroups++;
                ImportProgressLabel.Text = $"Season {processedGroups}/{dataGroups.Count}: {group.DisplayName}";

                // Pre-check: if analysis says no data and only HTML files, skip this group
                if (group.IsAnalyzed && !group.HasData && group.Files.All(f => f.FileType == "HTML"))
                {
                    ImportDetailLabel.Text = $"Skipping \"{group.DisplayName}\" — no data detected";
                    await Task.Delay(50);
                    continue;
                }

                Guid? seasonId = null;

                if (group.ExistingSeasonId.HasValue)
                {
                    seasonId = group.ExistingSeasonId.Value;
                    ImportDetailLabel.Text = $"Merging into existing season \"{group.ExistingSeasonName}\"...";
                }

                await Task.Delay(50);

                // ── Process HTML files directly via HtmlLeagueParser ──
                var htmlFiles = group.Files.Where(f => f.FileType == "HTML").ToList();
                if (htmlFiles.Count > 0)
                {
                    ImportDetailLabel.Text = $"Importing {htmlFiles.Count} HTML file{(htmlFiles.Count != 1 ? "s" : "")} directly...";
                    await Task.Delay(50);

                    try
                    {
                        var htmlEntities = await ImportHtmlFilesDirectAsync(
                            htmlFiles, group.DisplayName, seasonId, totalCreated, errors);
                        // Update seasonId if a new season was created inside
                        if (!seasonId.HasValue && htmlEntities.seasonId.HasValue)
                            seasonId = htmlEntities.seasonId;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"[{group.DisplayName}] HTML import error: {ex.Message}");
                    }
                }

                // ── Process SQL files automatically ──
                var sqlFiles = group.Files.Where(f => f.FileType == "SQL").ToList();
                foreach (var sqlFile in sqlFiles)
                {
                    ImportDetailLabel.Text = $"Importing SQL: {sqlFile.FileName}...";
                    await Task.Delay(50);

                    // Ensure season exists before SQL import so data goes to the right place
                    if (!seasonId.HasValue)
                    {
                        seasonId = CreateSeason(group.DisplayName);
                        totalCreated.Seasons++;
                    }

                    try
                    {
                        var (_, sqlResult) = await SqlFileImporter.ImportFromSqlFileAsync(
                            sqlFile.FilePath, DataStore.Data, false, targetSeasonId: seasonId);

                        if (sqlResult.Success)
                        {
                            totalCreated.FilesProcessed++;
                            totalCreated.Divisions += sqlResult.ImportedDivisionIds.Count;
                            totalCreated.Teams += sqlResult.TeamsImported;
                            totalCreated.Players += sqlResult.PlayersImported;
                            totalCreated.Fixtures += sqlResult.FixturesImported;
                            totalCreated.Venues += sqlResult.VenuesImported;
                            totalCreated.Competitions += sqlResult.CompetitionsImported;
                            if (sqlResult.ImportedSeasonIds.Count > 0)
                                totalCreated.Seasons += sqlResult.ImportedSeasonIds.Count;
                            // Track the season used by SQL import
                            if (sqlResult.DetectedSeason != null)
                                seasonId = sqlResult.DetectedSeason.Id;
                        }
                        else
                        {
                            errors.AddRange(sqlResult.Errors.Select(err => $"[{group.DisplayName}] SQL: {err}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"[{group.DisplayName}] SQL import error: {ex.Message}");
                    }
                }

                // ── Process Paradox databases automatically ──
                var paradoxFiles = group.Files.Where(f => f.FileType == "Paradox").ToList();
                foreach (var paradoxFile in paradoxFiles)
                {
                    ImportDetailLabel.Text = $"Importing Paradox DB: {paradoxFile.FileName}...";
                    await Task.Delay(50);

                    try
                    {
                        if (!seasonId.HasValue)
                        {
                            seasonId = CreateSeason(group.DisplayName);
                            totalCreated.Seasons++;
                        }

                        var orchestrator = new ParadoxImportOrchestrator(paradoxFile.FilePath);
                        var pdxResult = await orchestrator.ImportAsync(seasonId.Value);

                        if (pdxResult.Success)
                        {
                            totalCreated.FilesProcessed++;
                            totalCreated.Divisions += pdxResult.DivisionsImported;
                            totalCreated.Teams += pdxResult.TeamsImported;
                            totalCreated.Players += pdxResult.PlayersImported;
                            totalCreated.Fixtures += pdxResult.FixturesImported;
                            totalCreated.Venues += pdxResult.VenuesImported;
                        }
                        else
                        {
                            errors.AddRange(pdxResult.Errors.Select(err => $"[{group.DisplayName}] Paradox: {err}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"[{group.DisplayName}] Paradox import error: {ex.Message}");
                    }
                }

                // ── Queue only truly unprocessable formats as manual ──
                var manualTypeFiles = group.Files
                    .Where(f => f.FileType is "Access" or "Word" or "Excel" or "CSV" or "PDF")
                    .ToList();
                if (manualTypeFiles.Count > 0)
                {
                    if (!seasonId.HasValue)
                    {
                        seasonId = CreateSeason(group.DisplayName);
                        totalCreated.Seasons++;
                    }

                    foreach (var file in manualTypeFiles)
                        manualFiles.Add((group.DisplayName, file));
                }

                // ── Post-import: deduplicate divisions and clean misidentified entities ──
                if (seasonId.HasValue)
                {
                    DeduplicateSeasonData(seasonId.Value, errors);
                }
            }

            // Save all changes
            DataStore.Save();

            // Build results
            ImportProgressPanel.IsVisible = false;
            ImportTitle.Text = "Import Complete ✅";
            BuildResultsUI(totalCreated, manualFiles, errors, dataGroups.Count);
        }
        catch (Exception ex)
        {
            ImportProgressPanel.IsVisible = false;
            ImportTitle.Text = "Import Failed ❌";

            ResultsArea.Children.Add(new Label
            {
                Text = $"❌ Import failed:\n{ex.Message}",
                TextColor = Color.FromArgb("#EF4444"),
                FontSize = 14,
                Margin = new Thickness(0, 16)
            });

            AddStartOverButton();
        }
    }

    /// <summary>
    /// Directly import HTML files by parsing with HtmlLeagueParser and creating entities.
    /// Bypasses the BatchHtmlImportService preview pipeline which loses data from tables
    /// with Unknown DetectedType.
    /// </summary>
    private static async Task<(int entitiesCreated, Guid? seasonId)> ImportHtmlFilesDirectAsync(
        List<LeagueFileDiscoveryService.DiscoveredFile> htmlFiles,
        string seasonDisplayName,
        Guid? seasonId,
        ImportTotals totals,
        List<string> errors)
    {
        // Parse all HTML files and collect extracted data
        var allTeams = new List<HtmlLeagueParser.ExtractedTeam>();
        var allPlayers = new List<HtmlLeagueParser.ExtractedPlayer>();
        var allResults = new List<HtmlLeagueParser.ExtractedResult>();
        var allCompetitions = new List<HtmlLeagueParser.DetectedCompetition>();
        var allProfiles = new List<HtmlLeagueParser.ExtractedPlayerProfile>();
        var allDoublesEntries = new List<HtmlLeagueParser.ExtractedDoublesEntry>();

        // Collect division names from AUTHORITATIVE sources only
        // (league tables, player ratings, page headings — NOT from results which have abbreviations)
        var authoritativeDivisionNames = new List<string>();
        // Also track result division names for lookup only (not creation)
        var resultDivisionNames = new List<string>();

        foreach (var file in htmlFiles)
        {
            try
            {
                var result = await HtmlLeagueParser.ParseHtmlFileAsync(file.FilePath);
                if (!result.Success) continue;

                allTeams.AddRange(result.Teams);
                allPlayers.AddRange(result.Players);
                allResults.AddRange(result.Results);
                allCompetitions.AddRange(result.DetectedCompetitions);
                allDoublesEntries.AddRange(result.DoublesEntries);

                if (result.PlayerProfile != null)
                    allProfiles.Add(result.PlayerProfile);

                // Authoritative division sources (full names from headings)
                foreach (var t in result.Teams)
                    if (!string.IsNullOrWhiteSpace(t.Division))
                        authoritativeDivisionNames.Add(t.Division);
                foreach (var p in result.Players)
                    if (!string.IsNullOrWhiteSpace(p.Division))
                        authoritativeDivisionNames.Add(p.Division);
                foreach (var d in result.DoublesEntries)
                    if (!string.IsNullOrWhiteSpace(d.Division))
                        authoritativeDivisionNames.Add(d.Division);
                if (!string.IsNullOrWhiteSpace(result.DetectedDivision))
                    authoritativeDivisionNames.Add(result.DetectedDivision);

                // Result divisions (abbreviations like "Red" vs "Red Division") — lookup only
                foreach (var r in result.Results)
                    if (!string.IsNullOrWhiteSpace(r.Division))
                        resultDivisionNames.Add(r.Division);
            }
            catch (Exception ex)
            {
                errors.Add($"[{seasonDisplayName}] Parse error ({file.FileName}): {ex.Message}");
            }
        }

        int totalEntities = allTeams.Count + allPlayers.Count + allResults.Count + allCompetitions.Count + allProfiles.Count + allDoublesEntries.Count;
        if (totalEntities == 0)
            return (0, seasonId);

        // Create season if needed
        if (!seasonId.HasValue)
        {
            seasonId = CreateSeason(seasonDisplayName);
            totals.Seasons++;
        }

        var data = DataStore.Data;
        var sid = seasonId.Value;

        // ── Create Divisions (normalized and fuzzy-merged) ──
        // Key = normalized name, Value = Guid
        var divisionMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // Populate from existing divisions in this season (normalize their names too)
        foreach (var existing in data.Divisions.Where(d => d.SeasonId == sid))
        {
            if (string.IsNullOrWhiteSpace(existing.Name)) continue;
            var normalized = NormalizeDivisionName(existing.Name);
            divisionMap.TryAdd(normalized, existing.Id);
            // Also keep raw name mapping so exact lookups still work
            divisionMap.TryAdd(existing.Name, existing.Id);
        }

        // Normalize all authoritative division names and create unique ones
        var uniqueNormalizedDivisions = authoritativeDivisionNames
            .Select(NormalizeDivisionName)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n != " Division")
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var normalizedName in uniqueNormalizedDivisions)
        {
            if (divisionMap.ContainsKey(normalizedName)) continue;

            // Fuzzy: check if there's an existing division that shares the core word
            var fuzzyMatch = FindFuzzyDivisionMatch(normalizedName, divisionMap);
            if (fuzzyMatch.HasValue)
            {
                divisionMap[normalizedName] = fuzzyMatch.Value;
                continue;
            }

            var division = new Division
            {
                Id = Guid.NewGuid(),
                SeasonId = sid,
                Name = normalizedName
            };
            data.Divisions.Add(division);
            divisionMap[normalizedName] = division.Id;
            totals.Divisions++;
        }

        // Default division for entities without a division name
        // Use the single division if there's only one unique normalized division
        var uniqueDivisionIds = divisionMap.Values.Distinct().ToList();
        Guid? defaultDivisionId = uniqueDivisionIds.Count == 1 ? uniqueDivisionIds[0] : null;

        // ── Create Teams from AUTHORITATIVE sources only ──
        // League table teams and player rating TeamName fields are reliable.
        // Do NOT create teams from results (HomeTeam/AwayTeam may be player names
        // from individual competition results, or venue names from misdetected pages).
        var teamMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // Populate from existing teams in this season
        foreach (var existingTeam in data.Teams.Where(t => t.SeasonId == sid))
            if (!string.IsNullOrWhiteSpace(existingTeam.Name))
                teamMap.TryAdd(existingTeam.Name, existingTeam.Id);

        // Teams from league tables (most authoritative — columns are position-based)
        var teamNamesFromTables = allTeams
            .Select(t => t.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var teamName in teamNamesFromTables)
        {
            if (teamMap.ContainsKey(teamName)) continue;

            var extractedTeam = allTeams.First(t =>
                string.Equals(t.Name, teamName, StringComparison.OrdinalIgnoreCase));
            var divId = ResolveDivisionId(extractedTeam.Division, divisionMap, defaultDivisionId);

            var team = new Team
            {
                Id = Guid.NewGuid(),
                SeasonId = sid,
                Name = teamName,
                DivisionId = divId
            };
            data.Teams.Add(team);
            teamMap[teamName] = team.Id;
            totals.Teams++;
        }

        // Teams from player ratings (also authoritative — explicit TeamName column)
        var teamNamesFromPlayers = allPlayers
            .Select(p => p.TeamName)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var teamName in teamNamesFromPlayers)
        {
            if (teamMap.ContainsKey(teamName)) continue;

            var ep = allPlayers.First(p =>
                string.Equals(p.TeamName, teamName, StringComparison.OrdinalIgnoreCase));
            var divId = ResolveDivisionId(ep.Division, divisionMap, defaultDivisionId);

            var team = new Team
            {
                Id = Guid.NewGuid(),
                SeasonId = sid,
                Name = teamName,
                DivisionId = divId
            };
            data.Teams.Add(team);
            teamMap[teamName] = team.Id;
            totals.Teams++;
        }

        // Teams from doubles ratings (also authoritative)
        var teamNamesFromDoubles = allDoublesEntries
            .Select(d => d.TeamName)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var teamName in teamNamesFromDoubles)
        {
            if (teamMap.ContainsKey(teamName)) continue;

            var ed = allDoublesEntries.First(d =>
                string.Equals(d.TeamName, teamName, StringComparison.OrdinalIgnoreCase));
            var divId = ResolveDivisionId(ed.Division, divisionMap, defaultDivisionId);

            var team = new Team
            {
                Id = Guid.NewGuid(),
                SeasonId = sid,
                Name = teamName,
                DivisionId = divId
            };
            data.Teams.Add(team);
            teamMap[teamName] = team.Id;
            totals.Teams++;
        }

        // Teams from player profiles (authoritative — team name in heading)
        var teamNamesFromProfiles = allProfiles
            .Select(p => p.TeamName)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var teamName in teamNamesFromProfiles)
        {
            if (teamMap.ContainsKey(teamName)) continue;

            var team = new Team
            {
                Id = Guid.NewGuid(),
                SeasonId = sid,
                Name = teamName,
                DivisionId = defaultDivisionId
            };
            data.Teams.Add(team);
            teamMap[teamName] = team.Id;
            totals.Teams++;
        }

        // ── Create Players (deduplicated) ──
        var uniquePlayers = allPlayers
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        foreach (var ep in uniquePlayers)
        {
            // Check if player already exists in this season
            var existingPlayer = data.Players.FirstOrDefault(p =>
                p.SeasonId == sid &&
                string.Equals(p.Name, ep.Name, StringComparison.OrdinalIgnoreCase));
            if (existingPlayer != null) continue;

            // Find team ID
            Guid? teamId = null;
            if (!string.IsNullOrWhiteSpace(ep.TeamName) && teamMap.TryGetValue(ep.TeamName, out var tid))
                teamId = tid;

            // Split name into first/last
            var nameParts = ep.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var firstName = nameParts.Length > 0 ? nameParts[0] : "";
            var lastName = nameParts.Length > 1 ? nameParts[1] : "";

            var player = new Player
            {
                Id = Guid.NewGuid(),
                SeasonId = sid,
                FirstName = firstName,
                LastName = lastName,
                Name = ep.Name,
                TeamId = teamId
            };
            data.Players.Add(player);
            totals.Players++;
        }

        // ── Create Fixtures from Results ──
        // Only for teams that are in our authoritative team map.
        // Skip results where home/away don't match known teams (these are likely
        // individual player competition results, not team matches).
        foreach (var matchResult in allResults)
        {
            if (string.IsNullOrWhiteSpace(matchResult.HomeTeam) ||
                string.IsNullOrWhiteSpace(matchResult.AwayTeam))
                continue;

            if (!teamMap.TryGetValue(matchResult.HomeTeam, out var homeTeamId) ||
                !teamMap.TryGetValue(matchResult.AwayTeam, out var awayTeamId))
                continue;

            // Check for duplicate fixture (same date, same teams)
            var existingFixture = data.Fixtures.FirstOrDefault(f =>
                f.SeasonId == sid &&
                f.HomeTeamId == homeTeamId &&
                f.AwayTeamId == awayTeamId &&
                f.Date.Date == matchResult.Date.Date);
            if (existingFixture != null) continue;

            // Resolve division from result (fuzzy lookup against existing divisions)
            var divId = ResolveDivisionId(matchResult.Division, divisionMap, defaultDivisionId);

            var fixture = new Fixture
            {
                Id = Guid.NewGuid(),
                SeasonId = sid,
                DivisionId = divId,
                HomeTeamId = homeTeamId,
                AwayTeamId = awayTeamId,
                Date = matchResult.Date
            };

            // Add frame results to represent the score
            int frameNum = 1;
            for (int i = 0; i < matchResult.HomeScore; i++)
                fixture.Frames.Add(new FrameResult { Number = frameNum++, Winner = FrameWinner.Home });
            for (int i = 0; i < matchResult.AwayScore; i++)
                fixture.Frames.Add(new FrameResult { Number = frameNum++, Winner = FrameWinner.Away });

            data.Fixtures.Add(fixture);
            totals.Fixtures++;
        }

        // ── Create Competitions ──
        foreach (var detected in allCompetitions)
        {
            if (string.IsNullOrWhiteSpace(detected.Name)) continue;

            var existingComp = data.Competitions.FirstOrDefault(c =>
                c.SeasonId == sid &&
                string.Equals(c.Name, detected.Name, StringComparison.OrdinalIgnoreCase));
            if (existingComp != null) continue;

            var format = detected.Type?.ToLower() switch
            {
                "doubles" => CompetitionFormat.DoublesKnockout,
                "team" => CompetitionFormat.TeamKnockout,
                _ => CompetitionFormat.SinglesKnockout
            };

            var notes = new List<string>();
            if (!string.IsNullOrWhiteSpace(detected.WinnerName))
                notes.Add($"Winner: {detected.WinnerName}");
            if (!string.IsNullOrWhiteSpace(detected.RunnerUpName))
                notes.Add($"Runner-up: {detected.RunnerUpName}");
            if (!string.IsNullOrWhiteSpace(detected.Score))
                notes.Add($"Score: {detected.Score}");

            var competition = new Competition
            {
                Id = Guid.NewGuid(),
                SeasonId = sid,
                Name = detected.Name,
                Format = format,
                Status = CompetitionStatus.Completed,
                StartDate = detected.Date,
                Notes = notes.Count > 0 ? string.Join("\n", notes) : null
            };
            data.Competitions.Add(competition);
            totals.Competitions++;
        }

        // ── Import player profile frame results ──
        // Player profiles contain individual match records (player vs player)
        // These give us frame-level data that the results page doesn't have
        foreach (var profile in allProfiles)
        {
            if (profile.MatchHistory.Count == 0) continue;

            // Find or create the profile player
            var profilePlayer = data.Players.FirstOrDefault(p =>
                p.SeasonId == sid &&
                string.Equals(p.Name, profile.PlayerName, StringComparison.OrdinalIgnoreCase));

            if (profilePlayer == null)
            {
                var nameParts = profile.PlayerName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                Guid? profileTeamId = null;
                if (!string.IsNullOrWhiteSpace(profile.TeamName) && teamMap.TryGetValue(profile.TeamName, out var ptid))
                    profileTeamId = ptid;

                profilePlayer = new Player
                {
                    Id = Guid.NewGuid(),
                    SeasonId = sid,
                    FirstName = nameParts.Length > 0 ? nameParts[0] : "",
                    LastName = nameParts.Length > 1 ? nameParts[1] : "",
                    Name = profile.PlayerName,
                    TeamId = profileTeamId
                };
                data.Players.Add(profilePlayer);
                totals.Players++;
            }

            // Find the profile player's team ID
            Guid profilePlayerTeamId = profilePlayer.TeamId ?? Guid.Empty;

            foreach (var match in profile.MatchHistory)
            {
                // Find or create opponent player
                var opponent = data.Players.FirstOrDefault(p =>
                    p.SeasonId == sid &&
                    string.Equals(p.Name, match.OpponentName, StringComparison.OrdinalIgnoreCase));

                if (opponent == null)
                {
                    var oppParts = match.OpponentName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    Guid? oppTeamId = null;
                    if (!string.IsNullOrWhiteSpace(match.OpponentTeam) && teamMap.TryGetValue(match.OpponentTeam, out var otid))
                        oppTeamId = otid;

                    // Also try creating team for opponent if not found
                    if (oppTeamId == null && !string.IsNullOrWhiteSpace(match.OpponentTeam) && match.OpponentTeam.Length > 1)
                    {
                        if (!teamMap.ContainsKey(match.OpponentTeam))
                        {
                            var oppTeam = new Team
                            {
                                Id = Guid.NewGuid(),
                                SeasonId = sid,
                                Name = match.OpponentTeam,
                                DivisionId = defaultDivisionId
                            };
                            data.Teams.Add(oppTeam);
                            teamMap[match.OpponentTeam] = oppTeam.Id;
                            totals.Teams++;
                        }
                        teamMap.TryGetValue(match.OpponentTeam, out var newOppTid);
                        oppTeamId = newOppTid != Guid.Empty ? newOppTid : null;
                    }

                    opponent = new Player
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = sid,
                        FirstName = oppParts.Length > 0 ? oppParts[0] : "",
                        LastName = oppParts.Length > 1 ? oppParts[1] : "",
                        Name = match.OpponentName,
                        TeamId = oppTeamId
                    };
                    data.Players.Add(opponent);
                    totals.Players++;
                }

                Guid oppTeamIdResolved = opponent.TeamId ?? Guid.Empty;

                // Find or create fixture for this match date + teams
                var fixture = data.Fixtures.FirstOrDefault(f =>
                    f.SeasonId == sid &&
                    f.Date.Date == match.Date.Date &&
                    ((f.HomeTeamId == profilePlayerTeamId && f.AwayTeamId == oppTeamIdResolved) ||
                     (f.HomeTeamId == oppTeamIdResolved && f.AwayTeamId == profilePlayerTeamId)));

                if (fixture == null && profilePlayerTeamId != Guid.Empty && oppTeamIdResolved != Guid.Empty)
                {
                    fixture = new Fixture
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = sid,
                        Date = match.Date,
                        HomeTeamId = profilePlayerTeamId,
                        AwayTeamId = oppTeamIdResolved,
                        DivisionId = defaultDivisionId
                    };
                    data.Fixtures.Add(fixture);
                    totals.Fixtures++;
                }

                if (fixture == null) continue;

                // Determine home/away orientation
                var isProfileHome = fixture.HomeTeamId == profilePlayerTeamId;
                var homePlayerId = isProfileHome ? profilePlayer.Id : opponent.Id;
                var awayPlayerId = isProfileHome ? opponent.Id : profilePlayer.Id;

                // Skip if this frame already exists
                if (fixture.Frames.Any(fr => fr.HomePlayerId == homePlayerId && fr.AwayPlayerId == awayPlayerId))
                    continue;

                var winner = FrameWinner.None;
                var playerWon = match.Result.Equals("Won", StringComparison.OrdinalIgnoreCase);
                if (playerWon)
                    winner = isProfileHome ? FrameWinner.Home : FrameWinner.Away;
                else
                    winner = isProfileHome ? FrameWinner.Away : FrameWinner.Home;

                var frame = new FrameResult
                {
                    Number = fixture.Frames.Count + 1,
                    HomePlayerId = homePlayerId,
                    AwayPlayerId = awayPlayerId,
                    Winner = winner
                };

                // Store rating data from profile
                if (match.RatingAttained > 0)
                {
                    if (isProfileHome)
                        frame.HomePlayerRating = match.RatingAttained;
                    else
                        frame.AwayPlayerRating = match.RatingAttained;
                }

                // Weighting = opponent's rating at time of frame
                if (match.Weighting > 0)
                {
                    if (isProfileHome)
                        frame.HomeOppRating = match.Weighting;
                    else
                        frame.AwayOppRating = match.Weighting;
                }

                fixture.Frames.Add(frame);
                totals.Frames++;
            }
        }

        // ── Import doubles pairings (ratings from doubles ratings HTML pages) ──
        foreach (var entry in allDoublesEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Player1Name) && string.IsNullOrWhiteSpace(entry.Player2Name))
                continue;

            // Check for existing pairing (same players, same team, same season)
            var existingPairing = data.DoublesPairings.FirstOrDefault(dp =>
                dp.SeasonId == sid &&
                string.Equals(dp.Player1Name, entry.Player1Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(dp.Player2Name, entry.Player2Name, StringComparison.OrdinalIgnoreCase));

            if (existingPairing != null) continue;

            // Resolve division, team, and player IDs
            var divId = ResolveDivisionId(entry.Division, divisionMap, defaultDivisionId);
            Guid? teamId = null;
            if (!string.IsNullOrWhiteSpace(entry.TeamName) && teamMap.TryGetValue(entry.TeamName, out var tid))
                teamId = tid;

            // Try to find player IDs
            Guid? player1Id = data.Players.FirstOrDefault(p =>
                p.SeasonId == sid &&
                string.Equals(p.Name, entry.Player1Name, StringComparison.OrdinalIgnoreCase))?.Id;
            Guid? player2Id = data.Players.FirstOrDefault(p =>
                p.SeasonId == sid &&
                string.Equals(p.Name, entry.Player2Name, StringComparison.OrdinalIgnoreCase))?.Id;

            var pairing = new DoublesPairing
            {
                Id = Guid.NewGuid(),
                SeasonId = sid,
                DivisionId = divId,
                TeamId = teamId,
                Player1Id = player1Id,
                Player2Id = player2Id,
                Player1Name = entry.Player1Name,
                Player2Name = entry.Player2Name,
                TeamName = entry.TeamName,
                Played = entry.Played,
                Won = entry.Won,
                Lost = entry.Lost,
                BestRating = entry.BestRating,
                BestRatingDate = entry.BestRatingDate,
                CurrentRating = entry.CurrentRating
            };
            data.DoublesPairings.Add(pairing);
            totals.DoublesPairings++;
        }

        totals.FilesProcessed += htmlFiles.Count;
        return (totalEntities, seasonId);
    }

    /// <summary>
    /// Normalize a raw division name to a canonical form.
    /// Delegates to the shared DivisionHelper.
    /// </summary>
    private static string NormalizeDivisionName(string name) => DivisionHelper.NormalizeDivisionName(name);

    /// <summary>
    /// Find a fuzzy match for a normalized division name in the existing map.
    /// Delegates to the shared DivisionHelper.
    /// </summary>
    private static Guid? FindFuzzyDivisionMatch(string normalizedName, Dictionary<string, Guid> divisionMap)
        => DivisionHelper.FindFuzzyDivisionMatch(normalizedName, divisionMap);

    /// <summary>
    /// Resolve a raw division name to a GUID using normalization and fuzzy matching.
    /// Delegates to the shared DivisionHelper.
    /// </summary>
    private static Guid? ResolveDivisionId(
        string? rawName, Dictionary<string, Guid> divisionMap, Guid? defaultId)
        => DivisionHelper.ResolveDivisionId(rawName, divisionMap, defaultId);

    /// <summary>
    /// Post-import cleanup: merge near-duplicate divisions, remove teams that look like
    /// player names or venue names (cross-referencing entities within the same season).
    /// </summary>
    private static void DeduplicateSeasonData(Guid seasonId, List<string> errors)
    {
        var data = DataStore.Data;

        // ── 1. Merge near-duplicate divisions ──
        var seasonDivisions = data.Divisions.Where(d => d.SeasonId == seasonId).ToList();
        if (seasonDivisions.Count > 1)
        {
            // Group by normalized name
            var groups = seasonDivisions
                .GroupBy(d => NormalizeDivisionName(d.Name ?? ""), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var group in groups)
            {
                // Keep the first (canonical), merge others into it
                var canonical = group.First();
                var duplicates = group.Skip(1).ToList();

                foreach (var dup in duplicates)
                {
                    // Reassign teams pointing to the duplicate
                    foreach (var team in data.Teams.Where(t => t.DivisionId == dup.Id))
                        team.DivisionId = canonical.Id;

                    // Reassign fixtures pointing to the duplicate
                    foreach (var fixture in data.Fixtures.Where(f => f.DivisionId == dup.Id))
                        fixture.DivisionId = canonical.Id;

                    // Remove the duplicate division
                    data.Divisions.Remove(dup);
                }
            }

            // Also apply fuzzy/ordinal matching for remaining divisions
            // e.g. "1st Division" and "First Division", or "R Division" and "Red Division"
            var remaining = data.Divisions.Where(d => d.SeasonId == seasonId).ToList();
            if (remaining.Count > 1)
            {
                var merged = new HashSet<Guid>();
                for (int i = 0; i < remaining.Count; i++)
                {
                    if (merged.Contains(remaining[i].Id)) continue;

                    for (int j = i + 1; j < remaining.Count; j++)
                    {
                        if (merged.Contains(remaining[j].Id)) continue;

                        if (DivisionHelper.AreSameDivision(remaining[i].Name ?? "", remaining[j].Name ?? ""))
                        {
                            // Keep the one with the longer (more specific) name
                            var nameI = (remaining[i].Name ?? "").Length;
                            var nameJ = (remaining[j].Name ?? "").Length;
                            var (keep, remove) = nameI >= nameJ
                                ? (remaining[i], remaining[j])
                                : (remaining[j], remaining[i]);

                            foreach (var team in data.Teams.Where(t => t.DivisionId == remove.Id))
                                team.DivisionId = keep.Id;
                            foreach (var fixture in data.Fixtures.Where(f => f.DivisionId == remove.Id))
                                fixture.DivisionId = keep.Id;

                            data.Divisions.Remove(remove);
                            merged.Add(remove.Id);
                        }
                    }
                }
            }
        }

        // ── 2. Remove teams that are actually player names ──
        // Build a set of known player names for this season
        var playerNames = new HashSet<string>(
            data.Players.Where(p => p.SeasonId == seasonId && !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => p.Name!),
            StringComparer.OrdinalIgnoreCase);

        // Also build from FirstName + LastName
        foreach (var p in data.Players.Where(p => p.SeasonId == seasonId))
        {
            var fullName = $"{p.FirstName} {p.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
                playerNames.Add(fullName);
        }

        // Build a set of known venue names for this season
        var venueNames = new HashSet<string>(
            data.Venues.Where(v => v.SeasonId == seasonId && !string.IsNullOrWhiteSpace(v.Name))
                .Select(v => v.Name!),
            StringComparer.OrdinalIgnoreCase);

        // Find teams that match player or venue names but have no fixtures
        var suspectTeams = data.Teams
            .Where(t => t.SeasonId == seasonId && !string.IsNullOrWhiteSpace(t.Name))
            .Where(t => playerNames.Contains(t.Name!) || venueNames.Contains(t.Name!))
            .ToList();

        foreach (var suspect in suspectTeams)
        {
            // Only remove if this team has NO fixtures and NO players assigned to it
            var hasFixtures = data.Fixtures.Any(f =>
                f.SeasonId == seasonId &&
                (f.HomeTeamId == suspect.Id || f.AwayTeamId == suspect.Id));
            var hasPlayers = data.Players.Any(p => p.TeamId == suspect.Id);

            if (!hasFixtures && !hasPlayers)
            {
                data.Teams.Remove(suspect);
            }
        }
    }

    private static Guid CreateSeason(string displayName)
    {
        // Parse year from display name to set reasonable dates
        int startYear = DateTime.Now.Year;
        var yearMatch = System.Text.RegularExpressions.Regex.Match(displayName, @"\b(20\d{2})\b");
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var parsed))
            startYear = parsed;

        var season = new Season
        {
            Id = Guid.NewGuid(),
            Name = displayName,
            StartDate = new DateTime(startYear, 9, 1), // Default September start
            EndDate = new DateTime(startYear + 1, 5, 31), // Default May end
            MatchDayOfWeek = DayOfWeek.Tuesday,
            MatchStartTime = new TimeSpan(20, 0, 0)
        };

        DataStore.Data.Seasons.Add(season);
        return season.Id;
    }

    private void BuildResultsUI(ImportTotals totals, List<(string season, LeagueFileDiscoveryService.DiscoveredFile file)> manualFiles, List<string> errors, int seasonCount)
    {
        // Success summary
        var summaryBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#F0FDF4"),
            Stroke = Color.FromArgb("#10B981"),
            StrokeThickness = 1,
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var summaryStack = new VerticalStackLayout { Spacing = 6 };
        summaryStack.Children.Add(new Label
        {
            Text = "✅ Import Summary",
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            TextColor = Color.FromArgb("#10B981")
        });

        var lines = new List<string>();
        if (totals.Seasons > 0) lines.Add($"📅 {totals.Seasons} new season{(totals.Seasons != 1 ? "s" : "")} created");
        if (totals.Divisions > 0) lines.Add($"📊 {totals.Divisions} division{(totals.Divisions != 1 ? "s" : "")} imported");
        if (totals.Venues > 0) lines.Add($"📍 {totals.Venues} venue{(totals.Venues != 1 ? "s" : "")} imported");
        if (totals.Teams > 0) lines.Add($"👥 {totals.Teams} team{(totals.Teams != 1 ? "s" : "")} imported");
        if (totals.Players > 0) lines.Add($"🧑 {totals.Players} player{(totals.Players != 1 ? "s" : "")} imported");
        if (totals.Fixtures > 0) lines.Add($"🎱 {totals.Fixtures} fixture{(totals.Fixtures != 1 ? "s" : "")} / result{(totals.Fixtures != 1 ? "s" : "")} imported");
        if (totals.Frames > 0) lines.Add($"🎯 {totals.Frames} player frame result{(totals.Frames != 1 ? "s" : "")} imported");
        if (totals.Competitions > 0) lines.Add($"🏆 {totals.Competitions} competition{(totals.Competitions != 1 ? "s" : "")} imported");
        if (totals.DoublesPairings > 0) lines.Add($"👫 {totals.DoublesPairings} doubles pairing{(totals.DoublesPairings != 1 ? "s" : "")} imported");
        lines.Add($"📁 {totals.FilesProcessed} file{(totals.FilesProcessed != 1 ? "s" : "")} processed automatically");
        lines.Add($"Across {seasonCount} season{(seasonCount != 1 ? "s" : "")}");

        summaryStack.Children.Add(new Label
        {
            Text = string.Join("\n", lines),
            FontSize = 13,
            LineHeight = 1.4
        });

        summaryBorder.Content = summaryStack;
        ResultsArea.Children.Add(summaryBorder);

        // Manual import files
        if (manualFiles.Count > 0)
        {
            var manualBorder = new Border
            {
                BackgroundColor = Color.FromArgb("#FEF3C7"),
                Stroke = Color.FromArgb("#F59E0B"),
                StrokeThickness = 1,
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var manualStack = new VerticalStackLayout { Spacing = 6 };
            manualStack.Children.Add(new Label
            {
                Text = $"📋 {manualFiles.Count} File{(manualFiles.Count != 1 ? "s" : "")} Need Manual Import",
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
                TextColor = Color.FromArgb("#92400E")
            });
            manualStack.Children.Add(new Label
            {
                Text = "These file types require the Import Wizard for user-guided column mapping or schema selection:",
                FontSize = 12,
                TextColor = Color.FromArgb("#92400E")
            });

            foreach (var (seasonName, file) in manualFiles)
            {
                manualStack.Children.Add(new Label
                {
                    Text = $"  {file.FileTypeIcon} {file.FileName}  ({file.FileType} — Season: {seasonName})",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#78350F")
                });
            }

            manualStack.Children.Add(new Label
            {
                Text = "Use the Import Data wizard to process these files individually.",
                FontSize = 11,
                TextColor = Color.FromArgb("#92400E"),
                FontAttributes = FontAttributes.Italic,
                Margin = new Thickness(0, 4, 0, 0)
            });

            var openWizardButton = new Button
            {
                Text = "Open Import Wizard →",
                BackgroundColor = Color.FromArgb("#F59E0B"),
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Start,
                Padding = new Thickness(16, 10),
                Margin = new Thickness(0, 8, 0, 0)
            };
            openWizardButton.Clicked += async (s, e) =>
            {
                await Navigation.PopAsync();
            };
            manualStack.Children.Add(openWizardButton);

            manualBorder.Content = manualStack;
            ResultsArea.Children.Add(manualBorder);
        }

        // Errors
        if (errors.Count > 0)
        {
            var errorBorder = new Border
            {
                BackgroundColor = Color.FromArgb("#FEF2F2"),
                Stroke = Color.FromArgb("#EF4444"),
                StrokeThickness = 1,
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var errorStack = new VerticalStackLayout { Spacing = 4 };
            errorStack.Children.Add(new Label
            {
                Text = $"⚠️ {errors.Count} Warning{(errors.Count != 1 ? "s" : "")}",
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
                TextColor = Color.FromArgb("#EF4444")
            });

            foreach (var err in errors.Take(10))
            {
                errorStack.Children.Add(new Label
                {
                    Text = err,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#991B1B")
                });
            }

            if (errors.Count > 10)
            {
                errorStack.Children.Add(new Label
                {
                    Text = $"... and {errors.Count - 10} more",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#991B1B"),
                    FontAttributes = FontAttributes.Italic
                });
            }

            errorBorder.Content = errorStack;
            ResultsArea.Children.Add(errorBorder);
        }

        AddStartOverButton();
    }

    private void AddStartOverButton()
    {
        var doneButton = new Button
        {
            Text = "Done",
            BackgroundColor = Color.FromArgb("#10B981"),
            TextColor = Colors.White,
            Padding = new Thickness(32, 14),
            HorizontalOptions = LayoutOptions.Center,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 16)
        };
        doneButton.Clicked += async (s, e) =>
        {
            await Navigation.PopAsync();
        };
        ResultsArea.Children.Add(doneButton);
    }

    // ── Navigation ─────────────────────────────────────────────────

    private void OnBackClicked(object? sender, EventArgs e)
    {
        if (_currentStep == 2)
        {
            _currentStep = 1;
            _seasonGroups.Clear();
            SeasonGroupsPanel.Children.Clear();
            UpdateStepDisplay();
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _scanCts?.Cancel();
        await Navigation.PopAsync();
    }

    // ── Import totals ──────────────────────────────────────────────

    private class ImportTotals
    {
        public int Seasons;
        public int Divisions;
        public int Teams;
        public int Players;
        public int Fixtures;
        public int Frames;
        public int Venues;
        public int Competitions;
        public int DoublesPairings;
        public int FilesProcessed;
    }
}
