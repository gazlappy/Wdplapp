using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class SmartImportPage : ContentPage
{
    private readonly LeagueFileDiscoveryService _discoveryService = new();
    private readonly Dictionary<string, bool> _scanLocations = new();
    private List<LeagueFileDiscoveryService.DiscoveredFile> _discoveredFiles = new();
    private List<LeagueFileDiscoveryService.SeasonGroup> _seasonGroups = new();
    private CancellationTokenSource? _scanCts;
    private int _currentStep = 1;

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

            // Group by season and move to step 2
            _seasonGroups = _discoveryService.GroupBySeason(_discoveredFiles);
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

    // ── Step 2: Review ─────────────────────────────────────────────

    private void BuildReviewUI()
    {
        var totalFiles = _seasonGroups.Sum(g => g.Files.Count);
        var totalSeasons = _seasonGroups.Count;
        var existingSeasons = _seasonGroups.Count(g => g.IsExistingSeason);
        var newSeasons = totalSeasons - existingSeasons;
        var htmlCount = _seasonGroups.Sum(g => g.HtmlCount);
        var dbCount = _seasonGroups.Sum(g => g.DatabaseCount);

        ReviewTitle.Text = $"Found {totalFiles} Files in {totalSeasons} Season{(totalSeasons != 1 ? "s" : "")}";

        SummaryLabel.Text = $"📊 {totalFiles} files across {totalSeasons} detected season{(totalSeasons != 1 ? "s" : "")}";

        var details = new List<string>();
        if (existingSeasons > 0) details.Add($"✅ {existingSeasons} match existing season{(existingSeasons != 1 ? "s" : "")}");
        if (newSeasons > 0) details.Add($"🆕 {newSeasons} new season{(newSeasons != 1 ? "s" : "")} to create");
        if (htmlCount > 0) details.Add($"🌐 {htmlCount} HTML files (auto-import)");
        if (dbCount > 0) details.Add($"🗄️ {dbCount} database files (auto-import)");
        var dupeGroups = _seasonGroups.Count(g => g.HasDuplicateTypes);
        if (dupeGroups > 0) details.Add($"⚠️ {dupeGroups} season{(dupeGroups != 1 ? "s" : "")} with duplicate file types — data will be merged");
        SummaryDetailLabel.Text = string.Join("\n", details);

        SeasonGroupsPanel.Children.Clear();

        foreach (var group in _seasonGroups)
        {
            SeasonGroupsPanel.Children.Add(BuildSeasonGroupCard(group));
        }
    }

    private View BuildSeasonGroupCard(LeagueFileDiscoveryService.SeasonGroup group)
    {
        var border = new Border
        {
            Padding = 0,
            StrokeThickness = group.IsExistingSeason ? 2 : 1,
            Stroke = group.IsExistingSeason
                ? Color.FromArgb("#10B981")
                : Color.FromArgb("#E5E7EB"),
            BackgroundColor = group.IsExistingSeason
                ? Color.FromArgb("#F0FDF4")
                : Color.FromArgb("#F9FAFB"),
            Margin = new Thickness(0, 0, 0, 4)
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
        else
        {
            nameStack.Children.Add(new Label
            {
                Text = "🆕 New season will be created",
                FontSize = 11,
                TextColor = Color.FromArgb("#3B82F6")
            });
        }

        var countBadge = new Border
        {
            BackgroundColor = Color.FromArgb("#3B82F6"),
            Padding = new Thickness(10, 4),
            StrokeThickness = 0,
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = $"{group.Files.Count} file{(group.Files.Count != 1 ? "s" : "")}",
                TextColor = Colors.White,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold
            }
        };

        headerGrid.Add(checkbox, 0, 0);
        headerGrid.Add(nameStack, 1, 0);
        headerGrid.Add(countBadge, 2, 0);
        mainStack.Children.Add(headerGrid);

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

        // File list (collapsible)
        var filesStack = new VerticalStackLayout { Spacing = 4, IsVisible = false };
        foreach (var file in group.Files)
        {
            var fileRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8,
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

    // ── Step 3: Import ─────────────────────────────────────────────

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        var selectedGroups = _seasonGroups.Where(g => g.IsSelected).ToList();

        if (selectedGroups.Count == 0)
        {
            await DisplayAlert("Nothing Selected", "Please select at least one season group to import.", "OK");
            return;
        }

        var totalFiles = selectedGroups.Sum(g => g.Files.Count);
        var confirm = await DisplayAlert("Confirm Import",
            $"Import {totalFiles} file{(totalFiles != 1 ? "s" : "")} across {selectedGroups.Count} season{(selectedGroups.Count != 1 ? "s" : "")}?\n\n" +
            "• Existing seasons will have data merged\n" +
            "• New seasons will be created automatically\n" +
            "• Duplicate entries will be skipped",
            "Import", "Cancel");

        if (!confirm) return;

        _currentStep = 3;
        UpdateStepDisplay();
        ImportProgressPanel.IsVisible = true;

        DataStore.CreatePreImportSnapshot();

        var totalCreated = new ImportTotals();
        var manualFiles = new List<(string seasonName, LeagueFileDiscoveryService.DiscoveredFile file)>();
        var errors = new List<string>();
        int processedGroups = 0;

        try
        {
            foreach (var group in selectedGroups)
            {
                processedGroups++;
                ImportProgressLabel.Text = $"Season {processedGroups}/{selectedGroups.Count}: {group.DisplayName}";

                // Get or create the season
                Guid seasonId;
                if (group.ExistingSeasonId.HasValue)
                {
                    seasonId = group.ExistingSeasonId.Value;
                    ImportDetailLabel.Text = $"Merging into existing season \"{group.ExistingSeasonName}\"...";
                }
                else
                {
                    seasonId = CreateSeason(group.DisplayName);
                    totalCreated.Seasons++;
                    ImportDetailLabel.Text = $"Created new season \"{group.DisplayName}\"...";
                }

                await Task.Delay(100); // Let UI update

                // Process HTML files via batch pipeline
                var htmlFiles = group.Files.Where(f => f.FileType == "HTML").ToList();
                if (htmlFiles.Count > 0)
                {
                    ImportDetailLabel.Text = $"Processing {htmlFiles.Count} HTML file{(htmlFiles.Count != 1 ? "s" : "")}...";
                    await Task.Delay(50);

                    try
                    {
                        var htmlPaths = htmlFiles.Select(f => f.FilePath).ToList();
                        var batchPreview = await BatchHtmlImportService.CreateBatchPreviewAsync(
                            htmlPaths, DataStore.Data);

                        // Include all files
                        foreach (var file in batchPreview.Files)
                            file.Include = true;

                        var batchResult = await BatchHtmlImportService.ApplyBatchImportAsync(
                            batchPreview, seasonId, DataStore.Data);

                        totalCreated.Divisions += batchResult.TotalDivisionsCreated;
                        totalCreated.Teams += batchResult.TotalTeamsCreated;
                        totalCreated.Players += batchResult.TotalPlayersCreated;
                        totalCreated.Competitions += batchResult.TotalCompetitionsCreated;
                        totalCreated.FilesProcessed += batchResult.FilesSucceeded;

                        if (batchResult.Errors.Count > 0)
                            errors.AddRange(batchResult.Errors.Select(err => $"[{group.DisplayName}] {err}"));
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"[{group.DisplayName}] HTML import error: {ex.Message}");
                    }
                }

                // Queue non-HTML files for manual import guidance
                foreach (var file in group.Files.Where(f => f.FileType != "HTML"))
                {
                    manualFiles.Add((group.DisplayName, file));
                }
            }

            // Save all changes atomically
            DataStore.Save();
            DataStore.ClearPreImportSnapshot();

            // Build results
            ImportProgressPanel.IsVisible = false;
            ImportTitle.Text = "Import Complete ✅";
            BuildResultsUI(totalCreated, manualFiles, errors, selectedGroups.Count);
        }
        catch (Exception ex)
        {
            DataStore.RestorePreImportSnapshot();
            ImportProgressPanel.IsVisible = false;
            ImportTitle.Text = "Import Failed ❌";

            ResultsArea.Children.Add(new Label
            {
                Text = $"❌ Import failed and has been rolled back:\n{ex.Message}",
                TextColor = Color.FromArgb("#EF4444"),
                FontSize = 14,
                Margin = new Thickness(0, 16)
            });

            AddStartOverButton();
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
        if (totals.Teams > 0) lines.Add($"👥 {totals.Teams} team{(totals.Teams != 1 ? "s" : "")} imported");
        if (totals.Players > 0) lines.Add($"🧑 {totals.Players} player{(totals.Players != 1 ? "s" : "")} imported");
        if (totals.Competitions > 0) lines.Add($"🏆 {totals.Competitions} competition{(totals.Competitions != 1 ? "s" : "")} imported");
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
        public int Competitions;
        public int FilesProcessed;
    }
}
