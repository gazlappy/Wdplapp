using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Views;

public partial class HistoricalImportPage : ContentPage
{
    private int _currentStep = 1;
    private ImportType _selectedImportType = ImportType.None;
    private readonly ObservableCollection<SelectedFile> _selectedFiles = new();
    private string[] _currentExtensions = Array.Empty<string>();
    private bool _allowMultiple = false;

    private enum ImportType
    {
        None,
        AccessDatabase,
        ParadoxFolder,
        WordDocument,
        ExcelSpreadsheet,
        SingleHTML,
        BatchHTML,
        PDF,
        SqlFile
    }

    private readonly ImportWorkspace _dataStore;

    public HistoricalImportPage(IDataStore dataStore)
    {
        _dataStore = new ImportWorkspace(dataStore);
        InitializeComponent();
        SelectedFilesList.ItemsSource = _selectedFiles;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_initialized || _returningFromPreview)
        {
            _initialized = true;
            _returningFromPreview = false;
            ResetWizard();
        }
    }

    private void ResetWizard()
    {
        _dataStore.Reset();
        _intake?.Dispose();
        _intake = null;
        _targetSeasonId = null;
        _currentStep = 1;
        _selectedImportType = ImportType.None;
        _selectedFiles.Clear();
        _currentExtensions = Array.Empty<string>();
        _allowMultiple = false;
        SelectedFilesPanel.IsVisible = false;
        ProgressPanel.IsVisible = false;
        ResultsArea.Children.Clear();
        UpdateStepDisplay();
    }

    private void UpdateStepDisplay()
    {
        // Update step indicator
        UpdateStepIndicator(1, _currentStep >= 1);
        UpdateStepIndicator(2, _currentStep >= 2);
        UpdateStepIndicator(3, _currentStep >= 3);

        // Show/hide content
        Step1Content.IsVisible = _currentStep == 1;
        Step2Content.IsVisible = _currentStep == 2;
        Step3Content.IsVisible = _currentStep == 3;

        // Update navigation buttons
        BackButton.IsVisible = _currentStep > 1 && _currentStep < 3;
        NextButton.IsVisible = _currentStep == 2 && _selectedFiles.Any();
        CancelButton.IsVisible = _currentStep == 2;
    }

    private void UpdateStepIndicator(int stepNumber, bool isActive)
    {
        var icon = stepNumber switch
        {
            1 => Step1Icon,
            2 => Step2Icon,
            3 => Step3Icon,
            _ => null
        };

        var label = stepNumber switch
        {
            1 => Step1Label,
            2 => Step2Label,
            3 => Step3Label,
            _ => null
        };

        if (icon != null && label != null)
        {
            if (isActive)
            {
                icon.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#10B981");
                label.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#10B981");
                icon.Text = "?";
            }
            else if (_currentStep == stepNumber)
            {
                icon.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6");
                label.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6");
                icon.Text = stepNumber.ToString();
            }
            else
            {
                icon.TextColor = Colors.White;
                label.TextColor = Colors.White;
                icon.Text = stepNumber.ToString();
            }
        }
    }

    // ========== STEP 1: Import Type Selection ==========

    private void OnSelectAccessDatabase(object? sender, EventArgs e)
    {
        _selectedImportType = ImportType.AccessDatabase;
        SetupFileSelection("Select Access Database", 
            "Choose the .mdb or .accdb file you want to import",
            new[] { ".mdb", ".accdb" },
            false);
    }

    private async void OnSelectParadoxFolder(object? sender, EventArgs e)
    {
        _selectedImportType = ImportType.ParadoxFolder;
        
        try
        {
            var result = await FolderPicker.PickAsync(default);

            if (result != null && result.IsSuccessful)
            {
                var folderPath = result.Folder?.Path;
                if (string.IsNullOrEmpty(folderPath))
                {
                    await DisplayAlert("Error", "Could not get folder path", "OK");
                    return;
                }

                // Check if folder contains Paradox .DB files
                var dbFiles = Directory.GetFiles(folderPath, "*.DB", SearchOption.TopDirectoryOnly);
                if (!dbFiles.Any())
                {
                    await DisplayAlert("No Paradox Files", 
                        $"No .DB files found in:\n{folderPath}\n\nPlease select a folder containing Paradox database files (Team.DB, Player.DB, etc.)", 
                        "OK");
                    return;
                }

                // Process the folder
                await ProcessParadoxFolderAsync(folderPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Paradox FolderPicker failed: {ex.Message}");
            // FolderPicker might not be available on all platforms
            var manualPath = await DisplayPromptAsync(
                "Enter Folder Path",
                "Enter the full path to the Paradox database folder:",
                placeholder: @"C:\Users\...\LeagueData",
                keyboard: Keyboard.Text);

            if (!string.IsNullOrWhiteSpace(manualPath) && Directory.Exists(manualPath))
            {
                var dbFiles = Directory.GetFiles(manualPath, "*.DB", SearchOption.TopDirectoryOnly);
                if (!dbFiles.Any())
                {
                    await DisplayAlert("No Paradox Files", 
                        $"No .DB files found in:\n{manualPath}", 
                        "OK");
                    return;
                }

                await ProcessParadoxFolderAsync(manualPath);
            }
            else if (!string.IsNullOrWhiteSpace(manualPath))
            {
                await DisplayAlert("Error", $"Folder not found: {manualPath}", "OK");
            }
        }
    }

    /// <summary>
    /// Opens the Smart Import page for automatic file discovery and season merging
    /// </summary>
    private async void OnSmartImportClicked(object? sender, EventArgs e)
    {
        var smartImportPage = Application.Current?.Handler?.MauiContext?.Services.GetService<SmartImportPage>()
            ?? throw new InvalidOperationException("SmartImportPage not registered");
        await Navigation.PushAsync(smartImportPage);
    }

    /// <summary>
    /// Opens the Historical Competitions import page for manual entry
    /// </summary>
    private async void OnHistoricalCompetitionsClicked(object? sender, EventArgs e)
    {
        var importPage = new ImportHistoricalCompetitionsPage();
        await Navigation.PushModalAsync(new NavigationPage(importPage));
    }

    private void OnSelectWordDocument(object? sender, EventArgs e)
    {
        _selectedImportType = ImportType.WordDocument;
        SetupFileSelection("Select Word Document", 
            "Choose the .docx file containing season winners and competitions",
            new[] { ".docx", ".doc" },
            false);
    }

    private void OnSelectExcelSpreadsheet(object? sender, EventArgs e)
    {
        _selectedImportType = ImportType.ExcelSpreadsheet;
        SetupFileSelection("Select Spreadsheet", 
            "Choose the Excel or CSV file you want to import",
            new[] { ".xlsx", ".xls", ".csv" },
            false);
    }

    private void OnSelectSingleHTML(object? sender, EventArgs e)
    {
        _selectedImportType = ImportType.SingleHTML;
        SetupFileSelection("Select HTML File", 
            "Choose the saved webpage containing league data",
            new[] { ".html", ".htm" },
            false);
    }

    private void OnSelectBatchHTML(object? sender, EventArgs e)
    {
        _selectedImportType = ImportType.BatchHTML;
        SetupFileSelection("Select HTML Files", 
            "Choose multiple HTML files to import - use any method below",
            new[] { ".html", ".htm" },
            true);
    }

    private void OnSelectPDF(object? sender, EventArgs e)
    {
        _selectedImportType = ImportType.PDF;
        SetupFileSelection("Select PDF File", 
            "Choose the PDF file containing league tables, results, or player data",
            new[] { ".pdf" },
            false);
    }

    private void OnSelectSqlFile(object? sender, EventArgs e)
    {
        _selectedImportType = ImportType.SqlFile;
        SetupFileSelection("Select SQL Dump File", 
            "Choose the .sql file exported from MySQL, PostgreSQL, SQLite, or SQL Server",
            new[] { ".sql" },
            false);
    }

    // ========== STEP 2: File Selection ==========

    private void SetupFileSelection(string title, string description, string[] extensions, bool allowMultiple)
    {
        _currentStep = 2;
        _currentExtensions = extensions;
        _allowMultiple = allowMultiple;
        Step2Title.Text = title;
        Step2Description.Text = description;

        // Clear previous file selection UI
        FileSelectionArea.Children.Clear();

        if (allowMultiple)
        {
            // Enhanced batch selection UI
            SetupBatchFileSelection(extensions);
        }
        else
        {
            // Single file picker
            var pickerButton = new Button
            {
                Text = "Choose File",
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6"),
                TextColor = Colors.White,
                FontSize = 16,
                Padding = new Thickness(32, 16),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 20)
            };

            pickerButton.Clicked += async (s, e) => await PickFilesAsync(extensions, false);
            FileSelectionArea.Children.Add(pickerButton);
        }

        UpdateStepDisplay();
    }

    private void SetupBatchFileSelection(string[] extensions)
    {
        // Title for batch options
        var optionsTitle = new Label
        {
            Text = "ℹ️ Choose how to select files:",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 12)
        };
        FileSelectionArea.Children.Add(optionsTitle);

        // Option 1: Pick Individual Files
        var pickFilesCard = CreateOptionCard(
            "ℹ️ Pick Files",
            "Select one or more files individually",
            "Select Files",
            Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6"),
            async () => await PickMultipleFilesAsync(extensions));
        FileSelectionArea.Children.Add(pickFilesCard);

        // Option 2: Select Folder
        var folderCard = CreateOptionCard(
            "ℹ️ Select Folder",
            $"Choose a folder to find all {string.Join("/", extensions)} files",
            "Browse Folder",
            Microsoft.Maui.Graphics.Color.FromArgb("#10B981"),
            async () => await PickFolderAsync(extensions));
        FileSelectionArea.Children.Add(folderCard);

        // Option 3: Auto-Search Common Locations
        var autoSearchCard = CreateOptionCard(
            "ℹ️ Auto-Search",
            "Automatically scan common locations for importable files",
            "Scan Now",
            Microsoft.Maui.Graphics.Color.FromArgb("#F59E0B"),
            async () => await AutoSearchForFilesAsync(extensions));
        FileSelectionArea.Children.Add(autoSearchCard);

        // Option 4: Add Single File (legacy)
        var addOneCard = CreateOptionCard(
            "ℹ️ Add One File",
            "Add files one at a time (click multiple times)",
            "Add File",
            Microsoft.Maui.Graphics.Color.FromArgb("#6B7280"),
            async () => await PickFilesAsync(extensions, true));
        FileSelectionArea.Children.Add(addOneCard);

        // Info label
        var infoLabel = new Label
        {
            Text = "ℹ️ TIP: Use 'Select Folder' to quickly import all HTML files from a directory, or 'Auto-Search' to find files in Downloads and Documents.",
            TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#6B7280"),
            FontSize = 12,
            Margin = new Thickness(0, 16, 0, 0)
        };
        FileSelectionArea.Children.Add(infoLabel);
    }

    private Border CreateOptionCard(string title, string description, string buttonText, Color buttonColor, Func<Task> action)
    {
        var card = new Border
        {
            StrokeThickness = 1,
            Stroke = Microsoft.Maui.Graphics.Color.FromArgb("#E5E7EB"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(16),
            Margin = new Thickness(0, 4),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 16
        };

        var textStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        textStack.Children.Add(new Label
        {
            Text = title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        });
        textStack.Children.Add(new Label
        {
            Text = description,
            FontSize = 12,
            TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#6B7280")
        });

        var button = new Button
        {
            Text = buttonText,
            BackgroundColor = buttonColor,
            TextColor = Colors.White,
            Padding = new Thickness(16, 10),
            FontSize = 13,
            VerticalOptions = LayoutOptions.Center
        };
        button.Clicked += async (s, e) => await action();

        grid.Add(textStack, 0, 0);
        grid.Add(button, 1, 0);

        card.Content = grid;
        return card;
    }

    private async Task PickMultipleFilesAsync(string[] extensions)
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new System.Collections.Generic.Dictionary<DevicePlatform, System.Collections.Generic.IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, extensions },
                    { DevicePlatform.Android, extensions.Select(e => $"*{e}") },
                    { DevicePlatform.iOS, extensions }
                });

            var options = new PickOptions
            {
                PickerTitle = "Select files to import",
                FileTypes = customFileType
            };

            // Try to pick multiple files
            var results = await FilePicker.PickMultipleAsync(options);

            if (results != null && results.Any())
            {
                int addedCount = 0;
                foreach (var result in results)
                {
                    if (!_selectedFiles.Any(f => f.FilePath == result.FullPath))
                    {
                        _selectedFiles.Add(new SelectedFile
                        {
                            FileName = result.FileName,
                            FilePath = result.FullPath
                        });
                        addedCount++;
                    }
                }

                if (addedCount > 0)
                {
                    SelectedFilesPanel.IsVisible = true;
                    NextButton.IsVisible = true;
                    await DisplayAlert("Files Added", 
                        $"Added {addedCount} file(s).\nTotal selected: {_selectedFiles.Count}", 
                        "OK");
                }
                else
                {
                    await DisplayAlert("No New Files", "All selected files were already in the list.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select files: {ex.Message}", "OK");
        }
    }

    private async Task PickFolderAsync(string[] extensions)
    {
        try
        {
            var result = await FolderPicker.PickAsync(default);

            if (result != null && result.IsSuccessful)
            {
                var folderPath = result.Folder?.Path;
                if (string.IsNullOrEmpty(folderPath))
                {
                    await DisplayAlert("Error", "Could not get folder path", "OK");
                    return;
                }

                await ScanFolderForFilesAsync(folderPath, extensions, includeSubfolders: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FolderPicker failed: {ex.Message}");
            // FolderPicker might not be available on all platforms
            // Fall back to manual path entry
            var manualPath = await DisplayPromptAsync(
                "Enter Folder Path",
                "FolderPicker not available. Enter the full folder path:",
                placeholder: @"C:\Users\...\HTMLFiles",
                keyboard: Keyboard.Text);

            if (!string.IsNullOrWhiteSpace(manualPath) && Directory.Exists(manualPath))
            {
                await ScanFolderForFilesAsync(manualPath, extensions, includeSubfolders: true);
            }
            else if (!string.IsNullOrWhiteSpace(manualPath))
            {
                await DisplayAlert("Error", $"Folder not found: {manualPath}", "OK");
            }
        }
    }

    private async Task ScanFolderForFilesAsync(string folderPath, string[] extensions, bool includeSubfolders)
    {
        try
        {
            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var foundFiles = new System.Collections.Generic.List<string>();

            foreach (var ext in extensions)
            {
                var pattern = $"*{ext}";
                var files = Directory.GetFiles(folderPath, pattern, searchOption);
                foundFiles.AddRange(files);
            }

            if (foundFiles.Count != 0)
            {
                // Show confirmation with file count
                var proceed = await DisplayAlert(
                    "Files Found",
                    $"Found {foundFiles.Count} file(s) in:\n{folderPath}\n\n" +
                    (includeSubfolders ? "(Including subfolders)\n\n" : "") +
                    "Add all to import list?",
                    "Add All",
                    "Cancel");

                if (proceed)
                {
                    int addedCount = 0;
                    foreach (var filePath in foundFiles.OrderBy(f => f))
                    {
                        if (!_selectedFiles.Any(f => f.FilePath == filePath))
                        {
                            _selectedFiles.Add(new SelectedFile
                            {
                                FileName = Path.GetFileName(filePath),
                                FilePath = filePath
                            });
                            addedCount++;
                        }
                    }

                    SelectedFilesPanel.IsVisible = _selectedFiles.Any();
                    NextButton.IsVisible = _selectedFiles.Any();

                    await DisplayAlert("Files Added",
                        $"Added {addedCount} new file(s).\nTotal selected: {_selectedFiles.Count}",
                        "OK");
                }
            }
            else
            {
                await DisplayAlert("No Files Found",
                    $"No {string.Join(" or ", extensions)} files found in:\n{folderPath}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to scan folder: {ex.Message}", "OK");
        }
    }

    private async Task AutoSearchForFilesAsync(string[] extensions)
    {
        try
        {
            var foundFiles = new System.Collections.Generic.List<(string path, string source)>();

            // Common locations to search
            var searchLocations = new System.Collections.Generic.List<(string path, string name)>
            {
                (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents"),
                (Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Desktop"),
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), "Downloads"),
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WDPL"), "WDPL Folder"),
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "League"), "League Folder"),
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Pool"), "Pool Folder")
            };

            // Add recent file location if we have previously imported files
            if (_selectedFiles.Any())
            {
                var lastFolder = Path.GetDirectoryName(_selectedFiles.Last().FilePath);
                if (!string.IsNullOrEmpty(lastFolder) && Directory.Exists(lastFolder))
                {
                    searchLocations.Insert(0, (lastFolder, "Last Used Location"));
                }
            }

            // Show progress
            await DisplayAlert("Searching...", 
                "Scanning common locations for importable files. This may take a moment.", 
                "OK");

            foreach (var (path, name) in searchLocations)
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    continue;

                try
                {
                    foreach (var ext in extensions)
                    {
                        var files = Directory.GetFiles(path, $"*{ext}", SearchOption.AllDirectories);
                        foreach (var file in files.Take(100)) // Limit per location
                        {
                            foundFiles.Add((file, name));
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip folders we can't access - this is expected
                    System.Diagnostics.Debug.WriteLine($"Auto-search: access denied to {path}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-search error scanning {path}: {ex.Message}");
                }
            }

            if (foundFiles.Count != 0)
            {
                // Group by source location for display
                var summary = foundFiles
                    .GroupBy(f => f.source)
                    .Select(g => $"� {g.Key}: {g.Count()} file(s)")
                    .ToList();

                var proceed = await DisplayAlert(
                    $"Found {foundFiles.Count} Files",
                    $"Found files in:\n{string.Join("\n", summary)}\n\nAdd all to import list?",
                    "Add All",
                    "Cancel");

                if (proceed)
                {
                    int addedCount = 0;
                    foreach (var (filePath, _) in foundFiles)
                    {
                        if (!_selectedFiles.Any(f => f.FilePath == filePath))
                        {
                            _selectedFiles.Add(new SelectedFile
                            {
                                FileName = Path.GetFileName(filePath),
                                FilePath = filePath
                            });
                            addedCount++;
                        }
                    }

                    SelectedFilesPanel.IsVisible = _selectedFiles.Any();
                    NextButton.IsVisible = _selectedFiles.Any();

                    await DisplayAlert("Files Added",
                        $"Added {addedCount} new file(s).\nTotal selected: {_selectedFiles.Count}",
                        "OK");
                }
            }
            else
            {
                await DisplayAlert("No Files Found",
                    $"No {string.Join(" or ", extensions)} files found in common locations.\n\n" +
                    "Try using 'Select Folder' to browse to a specific location.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Auto-search failed: {ex.Message}", "OK");
        }
    }

    private async Task PickFilesAsync(string[] extensions, bool allowMultiple)
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new System.Collections.Generic.Dictionary<DevicePlatform, System.Collections.Generic.IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, extensions },
                    { DevicePlatform.Android, extensions.Select(e => $"*{e}") },
                    { DevicePlatform.iOS, extensions }
                });

            var options = new PickOptions
            {
                PickerTitle = Step2Title.Text,
                FileTypes = customFileType
            };

            var result = await FilePicker.PickAsync(options);

            if (result != null)
            {
                // Check if already added
                if (_selectedFiles.Any(f => f.FilePath == result.FullPath))
                {
                    await DisplayAlert("Already Added", "This file has already been selected", "OK");
                    return;
                }

                _selectedFiles.Add(new SelectedFile
                {
                    FileName = result.FileName,
                    FilePath = result.FullPath
                });

                SelectedFilesPanel.IsVisible = true;
                NextButton.IsVisible = true;

                if (allowMultiple)
                {
                    await DisplayAlert("File Added", 
                        $"Added: {result.FileName}\n\nTotal files: {_selectedFiles.Count}\n\nClick 'Add File' to add more, or 'Next' to continue.", 
                        "OK");
                }
                else
                {
                    // Auto-advance for single file selection
                    await Task.Delay(500);
                    OnNextClicked(null, EventArgs.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select file: {ex.Message}", "OK");
        }
    }

    private void OnRemoveFile(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is SelectedFile file)
        {
            _selectedFiles.Remove(file);
            
            if (!_selectedFiles.Any())
            {
                SelectedFilesPanel.IsVisible = false;
                NextButton.IsVisible = false;
            }
        }
    }

    // ========== STEP 3: Processing ==========

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        if (_isBusy || _currentStep != 2 || _selectedFiles.Count == 0) return;
        _isBusy = true;
        NextButton.IsEnabled = false;
        try
        {
            if (_selectedImportType == ImportType.BatchHTML && _selectedFiles.Count == 1)
                _selectedImportType = ImportType.SingleHTML;
            if (!await ConfirmTargetSeasonAsync()) return;
            _currentStep = 3;
            Step3Title.Text = "Processing Import...";
            ProgressPanel.IsVisible = true;
            ResultsArea.Children.Clear();
            UpdateStepDisplay();

            // Process based on import type
            await ProcessImportAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Cannot start import", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
            NextButton.IsEnabled = true;
        }
    }

    private async Task ProcessImportAsync()
    {
        try
        {
            switch (_selectedImportType)
            {
                case ImportType.AccessDatabase:
                    await ProcessAccessDatabaseAsync();
                    break;

                case ImportType.WordDocument:
                    await ProcessWordDocumentAsync();
                    break;

                case ImportType.ExcelSpreadsheet:
                    await ProcessExcelSpreadsheetAsync();
                    break;

                case ImportType.SingleHTML:
                    await ProcessSingleHTMLAsync();
                    break;

                case ImportType.BatchHTML:
                    await ProcessBatchHTMLAsync();
                    break;

                case ImportType.PDF:
                    await ProcessPDFAsync();
                    break;

                case ImportType.SqlFile:
                    await ProcessSqlFileAsync();
                    break;

                default:
                    throw new InvalidOperationException("Unknown import type");
            }
        }
        catch (Exception ex)
        {
            ProgressPanel.IsVisible = false;
            Step3Title.Text = "Import Failed";
            
            var errorLabel = new Label
            {
                Text = $"❌ Error: {ex.Message}",
                TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#EF4444"),
                FontSize = 14,
                Margin = new Thickness(0, 16)
            };
            ResultsArea.Children.Add(errorLabel);

            var retryButton = new Button
            {
                Text = "Start Over",
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6"),
                TextColor = Colors.White,
                Padding = new Thickness(32, 16),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 16)
            };
            retryButton.Clicked += (s, e) => ResetWizard();
            ResultsArea.Children.Add(retryButton);
        }
    }

    private async Task ProcessAccessDatabaseAsync()
    {
        var file = _selectedFiles.FirstOrDefault();
        if (file == null) return;

        // Validate file before import
        var (isValid, fileError) = DataStore.ValidateImportFile(file.FilePath);
        if (!isValid)
        {
            await DisplayAlert("Invalid File", fileError, "OK");
            ResetWizard();
            return;
        }

        ProgressMessage.Text = "Importing Access database...";

        try
        {
            // Create snapshot for rollback
            _dataStore.CreatePreImportSnapshot();

            var importer = new ActualDatabaseImporterV2(file.FilePath);
            var (data, summary) = await importer.ImportAllAsync();

            ProgressPanel.IsVisible = false;

            if (summary.Success)
            {
                if (!await DisplayAlert("Import Access database", summary.Summary + "\n\nSave these imported records?", "Import", "Cancel"))
                {
                    ResetWizard();
                    return;
                }
                // Merge imported data into the main DataStore
                _dataStore.GetData().Seasons.AddRange(data.Seasons);
                _dataStore.GetData().Divisions.AddRange(data.Divisions);
                _dataStore.GetData().Venues.AddRange(data.Venues);
                _dataStore.GetData().Teams.AddRange(data.Teams);
                _dataStore.GetData().Players.AddRange(data.Players);
                _dataStore.GetData().Fixtures.AddRange(data.Fixtures);
                await _dataStore.SaveAsync();
                _dataStore.ClearPreImportSnapshot();

                ShowSuccessResult("Access Database Imported", summary.Summary);
            }
            else
            {
                _dataStore.RestorePreImportSnapshot();
                await DisplayAlert("Import Failed", summary.Message, "OK");
                ResetWizard();
            }
        }
        catch (Exception ex)
        {
            _dataStore.RestorePreImportSnapshot();
            await DisplayAlert("Import Failed", $"Access import error: {ex.Message}\n\nYour data has been restored to its previous state.", "OK");
            ResetWizard();
        }
    }

    private async Task ProcessWordDocumentAsync()
    {
        var file = _selectedFiles.FirstOrDefault();
        if (file == null) return;

        var (isValid, fileError) = DataStore.ValidateImportFile(file.FilePath);
        if (!isValid)
        {
            await DisplayAlert("Invalid File", fileError, "OK");
            ResetWizard();
            return;
        }

        ProgressMessage.Text = "Extracting data from Word document...";

        // Navigate to preview page � it handles its own navigation
        // back when the user completes or cancels the import
        var previewPage = Application.Current?.Handler?.MauiContext?.Services.GetService<ImportPreviewPage>()
            ?? throw new InvalidOperationException("ImportPreviewPage not registered");
        _returningFromPreview = true;
        await Navigation.PushAsync(previewPage);
        await previewPage.LoadPreviewAsync(file.FilePath);
    }

    private async Task ProcessExcelSpreadsheetAsync()
    {
        var file = _selectedFiles.FirstOrDefault();
        if (file == null) return;

        var (isValid, fileError) = DataStore.ValidateImportFile(file.FilePath);
        if (!isValid)
        {
            await DisplayAlert("Invalid File", fileError, "OK");
            ResetWizard();
            return;
        }

        ProgressMessage.Text = "Processing spreadsheet...";
        _dataStore.CreatePreImportSnapshot();

        try
        {
            var result = await DocumentParser.ParseDocumentAsync(file.FilePath);
            
            if (result.Success && result.Tables.Count != 0)
            {
                // Show preview of found tables
                var tableInfo = string.Join("\n", result.Tables.Select(t => 
                    $"� {t.Name}: {t.RowCount} rows, {t.ColumnCount} columns"));
                
                var proceed = await DisplayAlert("Tables Found", 
                    $"Found {result.Tables.Count} table(s):\n\n{tableInfo}\n\nImport this data?", 
                    "Import", "Cancel");
                
                if (proceed)
                {
                    // Process each table based on detected type
                    var importStats = new ImportStats();
                    
                    foreach (var table in result.Tables)
                    {
                        // Auto-detect if league table, player list, venue list, etc.
                        if (IsLeagueTable(table))
                        {
                            await ImportLeagueTableFromTable(table, importStats);
                        }
                        else if (IsPlayerList(table))
                        {
                            await ImportPlayersFromTable(table, importStats);
                        }
                        else if (IsVenueList(table))
                        {
                            await ImportVenuesFromTable(table, importStats);
                        }
                    }

                    if (importStats.TeamsImported + importStats.PlayersImported + importStats.VenuesImported == 0)
                    {
                        _dataStore.RestorePreImportSnapshot();
                        await DisplayAlert("No new records", "No supported new league records were found. The rows may already exist, or the headers are not recognized.", "OK");
                        ResetWizard();
                        return;
                    }
                    await _dataStore.SaveAsync();
                    _dataStore.ClearPreImportSnapshot();
                    ShowSuccessResult("Spreadsheet Imported", 
                        $"Successfully processed {result.Tables.Count} tables:\n" +
                        $"� Teams: {importStats.TeamsImported}\n" +
                        $"� Players: {importStats.PlayersImported}\n" +
                        $"� Venues: {importStats.VenuesImported}");
                }
                else
                {
                    _dataStore.ClearPreImportSnapshot();
                    ResetWizard();
                }
            }
            else
            {
                _dataStore.ClearPreImportSnapshot();
                var errorMsg = result.Errors.Count != 0
                    ? string.Join("\n", result.Errors) 
                    : "No tables found in spreadsheet";
                await DisplayAlert("Import Failed", errorMsg, "OK");
                ResetWizard();
            }
        }
        catch (Exception ex)
        {
            _dataStore.RestorePreImportSnapshot();
            await DisplayAlert("Error", $"Failed to process spreadsheet: {ex.Message}\n\nYour data has been restored to its previous state.", "OK");
            ResetWizard();
        }
    }

    private async Task ProcessSingleHTMLAsync()
    {
        var file = _selectedFiles.FirstOrDefault();
        if (file == null) return;

        var (isValid, fileError) = DataStore.ValidateImportFile(file.FilePath);
        if (!isValid)
        {
            await DisplayAlert("Invalid File", fileError, "OK");
            ResetWizard();
            return;
        }

        ProgressMessage.Text = "Parsing HTML file...";
        _dataStore.CreatePreImportSnapshot();

        try
        {
            var result = await HtmlLeagueParser.ParseHtmlFileAsync(file.FilePath);
            
            if (result.Success && (result.Tables.Count != 0 || result.DetectedCompetitions.Count != 0))
            {
                var summary = new System.Text.StringBuilder();
                summary.AppendLine($"Page: {result.PageTitle}");
                summary.AppendLine($"Tables found: {result.Tables.Count}");
                
                if (result.HasLeagueTable) summary.AppendLine("� League standings detected");
                if (result.HasResults) summary.AppendLine("� Match results detected");
                if (result.HasPlayerStats) summary.AppendLine("� Player statistics detected");
                if (result.HasFixtures) summary.AppendLine("� Fixtures detected");
                if (result.HasCompetitions) 
                {
                    summary.AppendLine($"� {result.DetectedCompetitions.Count} competition(s) detected");
                    foreach (var comp in result.DetectedCompetitions.Take(5))
                    {
                        summary.AppendLine($"  - {comp.Name}" + (!string.IsNullOrEmpty(comp.WinnerName) ? $" (Winner: {comp.WinnerName})" : ""));
                    }
                    if (result.DetectedCompetitions.Count > 5)
                        summary.AppendLine($"  ... and {result.DetectedCompetitions.Count - 5} more");
                }
                
                var proceed = await DisplayAlert("HTML Parsed", 
                    $"{summary}\n\nImport this data?", 
                    "Import", "Cancel");
                
                if (proceed)
                {
                    var importStats = new ImportStats();
                    
                    // Process the HTML tables
                    foreach (var table in result.Tables)
                    {
                        if (table.DetectedType == HtmlLeagueParser.TableType.LeagueStandings)
                        {
                            var standings = HtmlLeagueParser.ParseLeagueStandings(table);
                            await ImportLeagueStandings(standings, importStats);
                        }
                        else if (table.DetectedType == HtmlLeagueParser.TableType.MatchResults)
                        {
                            var results2 = HtmlLeagueParser.ParseMatchResults(table);
                            await ImportMatchResults(results2, importStats);
                        }
                    }
                    
                    // Import detected competitions
                    if (result.DetectedCompetitions.Count != 0)
                    {
                        await ImportDetectedCompetitions(result.DetectedCompetitions, importStats);
                    }
                    
                    // Also try to extract venues from team names or venue column
                    await ExtractVenuesFromHtml(result, importStats);
                    
                    var resultSummary = new System.Text.StringBuilder();
                    resultSummary.AppendLine($"Successfully imported from {result.PageTitle}:");
                    if (importStats.TeamsImported > 0) resultSummary.AppendLine($"� Teams: {importStats.TeamsImported}");
                    if (importStats.PlayersImported > 0) resultSummary.AppendLine($"� Players: {importStats.PlayersImported}");
                    if (importStats.VenuesImported > 0) resultSummary.AppendLine($"� Venues: {importStats.VenuesImported}");
                    if (importStats.FixturesImported > 0) resultSummary.AppendLine($"� Fixtures: {importStats.FixturesImported}");
                    if (importStats.CompetitionsImported > 0) resultSummary.AppendLine($"� Competitions: {importStats.CompetitionsImported}");

                    if (importStats.TeamsImported + importStats.PlayersImported + importStats.VenuesImported +
                        importStats.FixturesImported + importStats.CompetitionsImported == 0)
                    {
                        _dataStore.RestorePreImportSnapshot();
                        await DisplayAlert("No new records", "No supported new records were found. Existing records were left unchanged.", "OK");
                        ResetWizard();
                        return;
                    }
                    await _dataStore.SaveAsync();
                    _dataStore.ClearPreImportSnapshot();
                    ShowSuccessResult("HTML Imported", resultSummary.ToString());
                }
                else
                {
                    _dataStore.ClearPreImportSnapshot();
                    ResetWizard();
                }
            }
            else
            {
                _dataStore.ClearPreImportSnapshot();
                await DisplayAlert("No Data Found", 
                    "Could not find any league data in this HTML file.\n\n" +
                    "Make sure the file contains tables with standings, results, or fixtures.", 
                    "OK");
                ResetWizard();
            }
        }
        catch (Exception ex)
        {
            _dataStore.RestorePreImportSnapshot();
            await DisplayAlert("Error", $"Failed to parse HTML: {ex.Message}\n\nYour data has been restored to its previous state.", "OK");
            ResetWizard();
        }
    }

    private async Task ProcessBatchHTMLAsync()
    {
        ProgressMessage.Text = $"Processing {_selectedFiles.Count} HTML files...";

        // Navigate to batch preview page
        var filePaths = _selectedFiles.Select(f => f.FilePath).ToList();
        var batchPreviewPage = Application.Current?.Handler?.MauiContext?.Services.GetService<BatchImportPreviewPage>()
            ?? throw new InvalidOperationException("BatchImportPreviewPage not registered");
        _returningFromPreview = true;
        await Navigation.PushAsync(batchPreviewPage);
        
        // Load the preview - the page will handle its own navigation when import is complete or cancelled
        await batchPreviewPage.LoadBatchPreviewAsync(filePaths);
        
        // Note: Don't pop navigation here - let the BatchImportPreviewPage handle its own navigation
        // when the user completes or cancels the import
    }

    private async Task ProcessPDFAsync()
    {
        var file = _selectedFiles.FirstOrDefault();
        if (file == null) return;

        var (isValid, fileError) = DataStore.ValidateImportFile(file.FilePath);
        if (!isValid)
        {
            await DisplayAlert("Invalid File", fileError, "OK");
            ResetWizard();
            return;
        }

        ProgressMessage.Text = "Extracting data from PDF...";
        _dataStore.CreatePreImportSnapshot();

        try
        {
            var result = await DocumentParser.ParseDocumentAsync(file.FilePath);
            
            if (result.Success && (result.TextContent.Count != 0 || result.Tables.Count != 0))
            {
                var summary = new System.Text.StringBuilder();
                summary.AppendLine($"File: {result.FileName}");
                
                if (result.TextContent.Count != 0)
                {
                    summary.AppendLine($"Text lines: {result.TextContent.Count}");
                    
                    // Try to detect what kind of data we have
                    var allText = string.Join(" ", result.TextContent).ToLower();
                    if (allText.Contains("league") || allText.Contains("division") || allText.Contains("standings"))
                        summary.AppendLine("� League data detected");
                    if (allText.Contains("player") || allText.Contains("rating"))
                        summary.AppendLine("� Player data detected");
                    if (allText.Contains("fixture") || allText.Contains("schedule"))
                        summary.AppendLine("� Fixture data detected");
                    if (allText.Contains("result") || allText.Contains("score"))
                        summary.AppendLine("� Results data detected");
                    if (allText.Contains("venue") || allText.Contains("pub") || allText.Contains("club") || allText.Contains("hall"))
                        summary.AppendLine("� Venue data detected");
                }
                
                if (result.Tables.Any())
                {
                    summary.AppendLine($"Tables found: {result.Tables.Count}");
                    foreach (var table in result.Tables)
                    {
                        summary.AppendLine($"  � {table.Name}: {table.RowCount} rows");
                    }
                }
                
                // Show preview of extracted text
                var previewText = result.TextContent.Take(20).ToList();
                if (previewText.Any())
                {
                    summary.AppendLine("\nPreview:");
                    summary.AppendLine(string.Join("\n", previewText.Select(t => 
                        t.Length > 60 ? t.Substring(0, 57) + "..." : t)));
                    
                    if (result.TextContent.Count > 20)
                        summary.AppendLine($"... and {result.TextContent.Count - 20} more lines");
                }
                
                var proceed = await DisplayAlert("PDF Parsed", 
                    $"{summary}\n\nWould you like to import this data?", 
                    "Import", "Cancel");
                
                if (proceed)
                {
                    // Try to parse the extracted text into structured data
                    var importResult = await ImportPdfDataAsync(result);

                    if (importResult.success)
                    {
                        await _dataStore.SaveAsync();
                        _dataStore.ClearPreImportSnapshot();
                        ShowSuccessResult("PDF Imported", importResult.message);
                    }
                    else
                    {
                        _dataStore.RestorePreImportSnapshot();
                        await DisplayAlert("Import Issue", importResult.message, "OK");
                        ResetWizard();
                    }
                }
                else
                {
                    _dataStore.ClearPreImportSnapshot();
                    ResetWizard();
                }
            }
            else
            {
                _dataStore.ClearPreImportSnapshot();
                // PDF parsing had errors or no content
                var errorMsg = result.Errors.Count != 0
                    ? string.Join("\n", result.Errors) 
                    : "Could not extract readable text from this PDF.";
                
                await DisplayAlert("PDF Import", 
                    $"{errorMsg}\n\n" +
                    "ℹ️ TIP: For best results with PDFs:\n" +
                    "� Make sure the PDF contains selectable text (not scanned images)\n" +
                    "� Try copying the data from the PDF and saving as a text file\n" +
                    "� Or export the data from the original source as CSV/Excel", 
                    "OK");
                ResetWizard();
            }
        }
        catch (Exception ex)
        {
            _dataStore.RestorePreImportSnapshot();
            await DisplayAlert("Error", $"Failed to process PDF: {ex.Message}\n\nYour data has been restored to its previous state.", "OK");
            ResetWizard();
        }
    }

    private async Task<(bool success, string message)> ImportPdfDataAsync(DocumentParser.ParsedDocument pdfResult)
    {
        try
        {
            var stats = new ImportStats();
            
            // If we have tables, try to import them
            if (pdfResult.Tables.Count != 0)
            {
                foreach (var table in pdfResult.Tables)
                {
                    if (IsLeagueTable(table))
                    {
                        await ImportLeagueTableFromTable(table, stats);
                    }
                    else if (IsPlayerList(table))
                    {
                        await ImportPlayersFromTable(table, stats);
                    }
                    else if (IsVenueList(table))
                    {
                        await ImportVenuesFromTable(table, stats);
                    }
                }
            }
            
            // Also try to parse text content for structured data
            if (pdfResult.TextContent.Count != 0)
            {
                var textTable = TryParseTextAsTable(pdfResult.TextContent);
                if (textTable != null && textTable.Rows.Count > 0)
                {
                    if (IsLeagueTable(textTable))
                        await ImportLeagueTableFromTable(textTable, stats);
                    else if (IsPlayerList(textTable))
                        await ImportPlayersFromTable(textTable, stats);
                    else if (IsVenueList(textTable))
                        await ImportVenuesFromTable(textTable, stats);
                }
                
                // Also try to extract venues from text content
                await ExtractVenuesFromText(pdfResult.TextContent, stats);
            }
            
            if (stats.TeamsImported > 0 || stats.PlayersImported > 0 || stats.FixturesImported > 0 || stats.VenuesImported > 0)
            {
                var summary = new System.Collections.Generic.List<string>();
                if (stats.TeamsImported > 0) summary.Add($"{stats.TeamsImported} teams");
                if (stats.PlayersImported > 0) summary.Add($"{stats.PlayersImported} players");
                if (stats.VenuesImported > 0) summary.Add($"{stats.VenuesImported} venues");
                if (stats.FixturesImported > 0) summary.Add($"{stats.FixturesImported} fixtures");
                
                return (true, $"Successfully imported:\n� {string.Join("\n� ", summary)}");
            }
            else
            {
                return (false, "Could not identify any structured league data in the PDF content.\n\n" +
                    "The PDF was parsed but the format wasn't recognized as league data.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Error processing PDF data: {ex.Message}");
        }
    }

    // ========== Table Detection Methods ==========

    private bool IsLeagueTable(DocumentParser.TableData table)
    {
        if (table.RowCount < 2) return false;
        
        var headerRow = table.Rows.FirstOrDefault();
        if (headerRow == null) return false;
        
        var header = string.Join(" ", headerRow).ToLower();
        
        // Check for common league table headers
        return (header.Contains("team") || header.Contains("pos") || header.Contains("position")) &&
               (header.Contains("pts") || header.Contains("points") || header.Contains("played") || header.Contains("won"));
    }

    private bool IsPlayerList(DocumentParser.TableData table)
    {
        if (table.RowCount < 2) return false;
        
        var headerRow = table.Rows.FirstOrDefault();
        if (headerRow == null) return false;
        
        var header = string.Join(" ", headerRow).ToLower();
        
        // Check for common player list headers
        return header.Contains("player") && 
               (header.Contains("team") || header.Contains("rating") || header.Contains("wins") || header.Contains("frames"));
    }

    private bool IsVenueList(DocumentParser.TableData table)
    {
        if (table.RowCount < 2) return false;
        
        var headerRow = table.Rows.FirstOrDefault();
        if (headerRow == null) return false;
        
        var header = string.Join(" ", headerRow).ToLower();
        
        // Check for common venue list headers
        return header.Contains("venue") || 
               (header.Contains("name") && (header.Contains("address") || header.Contains("location"))) ||
               header.Contains("pub") || header.Contains("club");
    }

    // ========== Import Helper Methods ==========

    private Task ImportLeagueTableFromTable(DocumentParser.TableData table, ImportStats stats)
    {
        var currentSeasonId = _targetSeasonId ?? SeasonService.Current.CurrentSeasonId;
        if (!currentSeasonId.HasValue) return Task.CompletedTask;

        // Skip header row
        var dataRows = table.Rows.Skip(1).ToList();
        var headerRow = table.Rows.FirstOrDefault();
        if (headerRow == null) return Task.CompletedTask;

        // Find column indices
        var teamCol = FindColumnIndex(headerRow, "team", "name", "club");
        var venueCol = FindColumnIndex(headerRow, "venue", "pub", "home");
        if (teamCol < 0) return Task.CompletedTask;

        // Get or create a default division for this season
        var division = _dataStore.GetData().Divisions.FirstOrDefault(d => d.SeasonId == currentSeasonId);
        if (division == null)
        {
            division = new Division
            {
                Id = Guid.NewGuid(),
                SeasonId = currentSeasonId,
                Name = "Imported Division"
            };
            _dataStore.GetData().Divisions.Add(division);
        }

        foreach (var row in dataRows)
        {
            if (row.Count <= teamCol) continue;

            var teamName = row[teamCol]?.Trim();
            if (string.IsNullOrWhiteSpace(teamName)) continue;

            // Check if team already exists in this season
            var existingTeam = _dataStore.GetData().Teams.FirstOrDefault(t =>
                t.SeasonId == currentSeasonId &&
                !string.IsNullOrWhiteSpace(t.Name) &&
                t.Name.Equals(teamName, StringComparison.OrdinalIgnoreCase));

            if (existingTeam != null) continue;

            Guid? venueId = null;

            // Extract venue if available
            if (venueCol >= 0 && row.Count > venueCol)
            {
                var venueName = row[venueCol]?.Trim();
                if (!string.IsNullOrWhiteSpace(venueName))
                {
                    var existingVenue = _dataStore.GetData().Venues.FirstOrDefault(v =>
                        v.SeasonId == currentSeasonId &&
                        !string.IsNullOrWhiteSpace(v.Name) &&
                        v.Name.Equals(venueName, StringComparison.OrdinalIgnoreCase));

                    if (existingVenue == null)
                    {
                        var venue = new Venue
                        {
                            Id = Guid.NewGuid(),
                            Name = venueName,
                            SeasonId = currentSeasonId
                        };
                        _dataStore.GetData().Venues.Add(venue);
                        venueId = venue.Id;
                        stats.VenuesImported++;
                    }
                    else
                    {
                        venueId = existingVenue.Id;
                    }
                }
            }

            var team = new Team
            {
                Id = Guid.NewGuid(),
                SeasonId = currentSeasonId,
                Name = teamName,
                DivisionId = division.Id,
                VenueId = venueId
            };
            _dataStore.GetData().Teams.Add(team);
            stats.TeamsImported++;
        }

        return Task.CompletedTask;
    }

    private Task ImportPlayersFromTable(DocumentParser.TableData table, ImportStats stats)
    {
        var currentSeasonId = _targetSeasonId ?? SeasonService.Current.CurrentSeasonId;
        if (!currentSeasonId.HasValue) return Task.CompletedTask;

        var dataRows = table.Rows.Skip(1).ToList();
        var headerRow = table.Rows.FirstOrDefault();
        if (headerRow == null) return Task.CompletedTask;

        var playerCol = FindColumnIndex(headerRow, "player", "name");
        var teamCol = FindColumnIndex(headerRow, "team", "club");

        foreach (var row in dataRows)
        {
            if (row.Count <= playerCol) continue;

            var playerName = row[playerCol]?.Trim();
            if (string.IsNullOrWhiteSpace(playerName)) continue;

            var nameParts = playerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstName = nameParts.FirstOrDefault() ?? "";
            var lastName = string.Join(" ", nameParts.Skip(1));

            // Check if player already exists in this season
            var existingPlayer = _dataStore.GetData().Players.FirstOrDefault(p =>
                p.SeasonId == currentSeasonId &&
                p.FirstName?.Equals(firstName, StringComparison.OrdinalIgnoreCase) == true &&
                p.LastName?.Equals(lastName, StringComparison.OrdinalIgnoreCase) == true);

            if (existingPlayer != null) continue;

            // Try to match team
            Guid? teamId = null;
            if (teamCol >= 0 && row.Count > teamCol)
            {
                var teamName = row[teamCol]?.Trim();
                if (!string.IsNullOrWhiteSpace(teamName))
                {
                    var team = _dataStore.GetData().Teams.FirstOrDefault(t =>
                        t.SeasonId == currentSeasonId &&
                        !string.IsNullOrWhiteSpace(t.Name) &&
                        t.Name.Equals(teamName, StringComparison.OrdinalIgnoreCase));
                    teamId = team?.Id;
                }
            }

            var player = new Player
            {
                Id = Guid.NewGuid(),
                SeasonId = currentSeasonId,
                FirstName = firstName,
                LastName = lastName,
                TeamId = teamId
            };
            _dataStore.GetData().Players.Add(player);
            stats.PlayersImported++;
        }

        return Task.CompletedTask;
    }

    private Task ImportVenuesFromTable(DocumentParser.TableData table, ImportStats stats)
    {
        var dataRows = table.Rows.Skip(1).ToList();
        var headerRow = table.Rows.FirstOrDefault();
        if (headerRow == null) return Task.CompletedTask;
        
        var venueCol = FindColumnIndex(headerRow, "venue", "name", "pub", "club");
        var addressCol = FindColumnIndex(headerRow, "address", "location", "postcode");
        
        foreach (var row in dataRows)
        {
            if (row.Count <= venueCol) continue;
            
            var venueName = row[venueCol]?.Trim();
            if (string.IsNullOrWhiteSpace(venueName)) continue;
            
            // Check if venue already exists
            var existingVenue = _dataStore.GetData().Venues.FirstOrDefault(v =>
                v.Name != null && v.Name.Equals(venueName, StringComparison.OrdinalIgnoreCase));
            
            if (existingVenue == null)
            {
                var venue = new Venue
                {
                    Id = Guid.NewGuid(),
                    Name = venueName,
                    SeasonId = SeasonService.Current.CurrentSeasonId
                };
                
                // Add address if available
                if (addressCol >= 0 && row.Count > addressCol)
                {
                    var address = row[addressCol]?.Trim();
                    if (!string.IsNullOrWhiteSpace(address))
                    {
                        venue.Address = address;
                    }
                }
                
                _dataStore.GetData().Venues.Add(venue);
                stats.VenuesImported++;
            }
        }

        return Task.CompletedTask;
    }

    private Task ImportLeagueStandings(System.Collections.Generic.List<LeagueStandingRow> standings, ImportStats stats)
    {
        var currentSeasonId = _targetSeasonId ?? SeasonService.Current.CurrentSeasonId;
        if (!currentSeasonId.HasValue) return Task.CompletedTask;

        // Get or create a default division
        var division = _dataStore.GetData().Divisions.FirstOrDefault(d => d.SeasonId == currentSeasonId);
        if (division == null)
        {
            division = new Division
            {
                Id = Guid.NewGuid(),
                SeasonId = currentSeasonId,
                Name = "Imported Division"
            };
            _dataStore.GetData().Divisions.Add(division);
        }

        foreach (var standing in standings)
        {
            if (string.IsNullOrWhiteSpace(standing.TeamName)) continue;

            // Check if team already exists in this season
            var existingTeam = _dataStore.GetData().Teams.FirstOrDefault(t =>
                t.SeasonId == currentSeasonId &&
                !string.IsNullOrWhiteSpace(t.Name) &&
                t.Name.Equals(standing.TeamName, StringComparison.OrdinalIgnoreCase));

            if (existingTeam != null) continue;

            var team = new Team
            {
                Id = Guid.NewGuid(),
                SeasonId = currentSeasonId,
                Name = standing.TeamName,
                DivisionId = division.Id
            };
            _dataStore.GetData().Teams.Add(team);
            stats.TeamsImported++;
        }

        return Task.CompletedTask;
    }

    private Task ImportMatchResults(System.Collections.Generic.List<MatchResultRow> results, ImportStats stats)
    {
        var currentSeasonId = _targetSeasonId ?? SeasonService.Current.CurrentSeasonId;
        if (!currentSeasonId.HasValue) return Task.CompletedTask;

        foreach (var matchResult in results)
        {
            if (string.IsNullOrWhiteSpace(matchResult.HomeTeam) || string.IsNullOrWhiteSpace(matchResult.AwayTeam))
                continue;

            // Find home and away teams
            var homeTeam = _dataStore.GetData().Teams.FirstOrDefault(t =>
                t.SeasonId == currentSeasonId &&
                !string.IsNullOrWhiteSpace(t.Name) &&
                t.Name.Equals(matchResult.HomeTeam, StringComparison.OrdinalIgnoreCase));

            var awayTeam = _dataStore.GetData().Teams.FirstOrDefault(t =>
                t.SeasonId == currentSeasonId &&
                !string.IsNullOrWhiteSpace(t.Name) &&
                t.Name.Equals(matchResult.AwayTeam, StringComparison.OrdinalIgnoreCase));

            if (homeTeam == null || awayTeam == null) continue;

            var fixtureDate = matchResult.Date ?? DateTime.Today;

            // Check for duplicate fixture
            var existingFixture = _dataStore.GetData().Fixtures.FirstOrDefault(f =>
                f.SeasonId == currentSeasonId &&
                f.Date.Date == fixtureDate.Date &&
                f.HomeTeamId == homeTeam.Id &&
                f.AwayTeamId == awayTeam.Id);

            if (existingFixture != null) continue;

            var fixture = new Fixture
            {
                Id = Guid.NewGuid(),
                SeasonId = currentSeasonId,
                Date = fixtureDate,
                HomeTeamId = homeTeam.Id,
                AwayTeamId = awayTeam.Id
            };
            _dataStore.GetData().Fixtures.Add(fixture);
            stats.FixturesImported++;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Import detected competitions from HTML or other sources
    /// </summary>
    private Task ImportDetectedCompetitions(System.Collections.Generic.List<HtmlLeagueParser.DetectedCompetition> detectedCompetitions, ImportStats stats)
    {
        var currentSeasonId = _targetSeasonId ?? SeasonService.Current.CurrentSeasonId;
        if (!currentSeasonId.HasValue)
        {
            return Task.CompletedTask;
        }

        _dataStore.GetData().Competitions ??= new List<Competition>();

        foreach (var detected in detectedCompetitions)
        {
            // Check if competition with same name already exists in this season
            var existing = _dataStore.GetData().Competitions.FirstOrDefault(c =>
                c.SeasonId == currentSeasonId &&
                !string.IsNullOrWhiteSpace(c.Name) &&
                c.Name.Equals(detected.Name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                // Skip duplicate
                continue;
            }

            // Determine format from type
            var format = detected.Type?.ToLower() switch
            {
                "doubles" => CompetitionFormat.DoublesKnockout,
                "team" => CompetitionFormat.TeamKnockout,
                _ => CompetitionFormat.SinglesKnockout
            };

            // Create the competition
            var competition = new Competition
            {
                Id = Guid.NewGuid(),
                Name = detected.Name,
                SeasonId = currentSeasonId,
                Format = format,
                Status = CompetitionStatus.Completed,
                StartDate = detected.Date,
                CreatedDate = DateTime.Now,
                Notes = BuildCompetitionNotesFromDetected(detected)
            };

            // Try to find winner player in database
            Guid? winnerId = null;
            Guid? runnerUpId = null;

            if (!string.IsNullOrEmpty(detected.WinnerName))
            {
                var winnerPlayer = FindPlayerByName(detected.WinnerName, currentSeasonId.Value);
                if (winnerPlayer != null)
                {
                    winnerId = winnerPlayer.Id;
                    competition.ParticipantIds.Add(winnerPlayer.Id);
                }
            }

            if (!string.IsNullOrEmpty(detected.RunnerUpName))
            {
                var runnerUpPlayer = FindPlayerByName(detected.RunnerUpName, currentSeasonId.Value);
                if (runnerUpPlayer != null)
                {
                    runnerUpId = runnerUpPlayer.Id;
                    competition.ParticipantIds.Add(runnerUpPlayer.Id);
                }
            }

            // Create final round with result
            if (winnerId.HasValue || !string.IsNullOrEmpty(detected.WinnerName))
            {
                var finalRound = new CompetitionRound
                {
                    Id = Guid.NewGuid(),
                    Name = "Final",
                    RoundNumber = 1
                };

                var finalMatch = new CompetitionMatch
                {
                    Id = Guid.NewGuid(),
                    Participant1Id = winnerId,
                    Participant2Id = runnerUpId,
                    WinnerId = winnerId,
                    IsComplete = true,
                    ScheduledDate = detected.Date
                };

                // Parse score if available
                if (!string.IsNullOrEmpty(detected.Score))
                {
                    var scoreParts = detected.Score.Split(new[] { '-', ':' }, StringSplitOptions.RemoveEmptyEntries);
                    if (scoreParts.Length == 2 && 
                        int.TryParse(scoreParts[0].Trim(), out var score1) &&
                        int.TryParse(scoreParts[1].Trim(), out var score2))
                    {
                        finalMatch.Participant1Score = score1;
                        finalMatch.Participant2Score = score2;
                    }
                }

                finalRound.Matches.Add(finalMatch);
                competition.Rounds.Add(finalRound);
            }

            _dataStore.GetData().Competitions.Add(competition);
            stats.CompetitionsImported++;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Find a player by name in the current season or any season
    /// </summary>
    private Player? FindPlayerByName(string name, Guid seasonId)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var nameParts = name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 0 ? nameParts[0] : "";
        var lastName = nameParts.Length > 1 ? nameParts[1] : "";

        // Try to find in current season first
        var player = _dataStore.GetData().Players.FirstOrDefault(p =>
            p.SeasonId == seasonId &&
            (p.FullName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
             (p.FirstName?.Equals(firstName, StringComparison.OrdinalIgnoreCase) == true &&
              p.LastName?.Equals(lastName, StringComparison.OrdinalIgnoreCase) == true)));

        // If not found, try any season
        if (player == null)
        {
            player = _dataStore.GetData().Players.FirstOrDefault(p =>
                p.FullName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                (p.FirstName?.Equals(firstName, StringComparison.OrdinalIgnoreCase) == true &&
                 p.LastName?.Equals(lastName, StringComparison.OrdinalIgnoreCase) == true));
        }

        return player;
    }

    /// <summary>
    /// Build notes string for detected competition
    /// </summary>
    private string BuildCompetitionNotesFromDetected(HtmlLeagueParser.DetectedCompetition detected)
    {
        var notes = new System.Text.StringBuilder();
        notes.AppendLine($"[Auto-detected from HTML import: {DateTime.Now:yyyy-MM-dd HH:mm}]");
        
        if (!string.IsNullOrEmpty(detected.WinnerName))
            notes.AppendLine($"Winner: {detected.WinnerName}");
        
        if (!string.IsNullOrEmpty(detected.WinnerTeam))
            notes.AppendLine($"Team: {detected.WinnerTeam}");
        
        if (!string.IsNullOrEmpty(detected.RunnerUpName))
            notes.AppendLine($"Runner-up: {detected.RunnerUpName}");
        
        if (!string.IsNullOrEmpty(detected.Score))
            notes.AppendLine($"Score: {detected.Score}");

        return notes.ToString().Trim();
    }

    private Task ExtractVenuesFromHtml(HtmlLeagueParser.HtmlParseResult result, ImportStats stats)
    {
        // Look for venue information in tables
        foreach (var table in result.Tables)
        {
            // Check if any column contains venue-like data
            var headerRow = table.Rows.FirstOrDefault();
            if (headerRow == null) continue;
            
            var venueCol = -1;
            for (int i = 0; i < headerRow.Count; i++)
            {
                var header = headerRow[i].ToLower();
                if (header.Contains("venue") || header.Contains("pub") || header.Contains("home"))
                {
                    venueCol = i;
                    break;
                }
            }
            
            if (venueCol >= 0)
            {
                foreach (var row in table.Rows.Skip(1))
                {
                    if (row.Count <= venueCol) continue;
                    
                    var venueName = row[venueCol]?.Trim();
                    if (string.IsNullOrWhiteSpace(venueName)) continue;
                    
                    var existingVenue = _dataStore.GetData().Venues.FirstOrDefault(v =>
                        v.Name != null && v.Name.Equals(venueName, StringComparison.OrdinalIgnoreCase));
                    
                    if (existingVenue == null)
                    {
                        var venue = new Venue
                        {
                            Id = Guid.NewGuid(),
                            Name = venueName,
                            SeasonId = SeasonService.Current.CurrentSeasonId
                        };
                        _dataStore.GetData().Venues.Add(venue);
                        stats.VenuesImported++;
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    private Task ExtractVenuesFromText(System.Collections.Generic.List<string> textContent, ImportStats stats)
    {
        // Match venue-like names using word boundaries to avoid false positives
        // (e.g. "published" shouldn't match "pub")
        var venuePatterns = new[] { @"\bpub\b", @"\bclub\b", @"\barms\b", @"\binn\b", @"\btavern\b", @"\bhall\b", @"\blounge\b", @"\bsports\s+bar\b" };

        foreach (var line in textContent)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length < 5 || trimmed.Length > 100) continue;

            var lower = trimmed.ToLower();

            // Check if line matches a venue pattern (word boundary match)
            if (!venuePatterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(lower, p)))
                continue;

            // Skip lines that look like sentences or paragraphs (too many words)
            var wordCount = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount > 8) continue;

            var venueName = trimmed;

            // Clean up common prefixes
            foreach (var prefix in new[] { "at ", "venue: ", "home: ", "@ " })
            {
                if (lower.StartsWith(prefix))
                {
                    venueName = trimmed.Substring(prefix.Length).Trim();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(venueName) || venueName.Length < 3) continue;

            // Check if venue already exists
            var existingVenue = _dataStore.GetData().Venues.FirstOrDefault(v =>
                v.Name != null && v.Name.Equals(venueName, StringComparison.OrdinalIgnoreCase));

            if (existingVenue == null)
            {
                var venue = new Venue
                {
                    Id = Guid.NewGuid(),
                    Name = venueName,
                    SeasonId = SeasonService.Current.CurrentSeasonId
                };
                _dataStore.GetData().Venues.Add(venue);
                stats.VenuesImported++;
            }
        }

        return Task.CompletedTask;
    }

    private int FindColumnIndex(System.Collections.Generic.List<string> headerRow, params string[] keywords)
    {
        for (int i = 0; i < headerRow.Count; i++)
        {
            var header = headerRow[i].ToLower();
            if (keywords.Any(k => header.Contains(k)))
            {
                return i;
            }
        }
        return -1;
    }

    private DocumentParser.TableData? TryParseTextAsTable(System.Collections.Generic.List<string> lines)
    {
        var table = new DocumentParser.TableData { Name = "Parsed Text Table" };
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            // Try tab-separated
            if (line.Contains('\t'))
            {
                table.Rows.Add(line.Split('\t').Select(s => s.Trim()).ToList());
            }
            // Try multiple spaces (common in PDFs)
            else if (MyRegex().IsMatch(line))
            {
                var parts = MyRegex().Split(line)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                
                if (parts.Count >= 2)
                    table.Rows.Add(parts);
            }
        }
        
        return table.Rows.Count != 0 ? table : null;
    }

    private async Task ProcessSqlFileAsync()
    {
        var file = _selectedFiles.FirstOrDefault();
        if (file == null) return;

        // Navigate to the dedicated SQL Import Wizard page with the selected file
        var sqlImportPage = Application.Current?.Handler?.MauiContext?.Services.GetService<SqlImportPage>()
            ?? throw new InvalidOperationException("SqlImportPage not registered");
        _returningFromPreview = true;
        await Navigation.PushAsync(sqlImportPage);
        await sqlImportPage.LoadFileAsync(file.FilePath);
    }

    private Task ProcessParadoxFolderAsync(string folderPath)
    {
        _currentStep = 3;
        Step3Title.Text = "Processing Paradox Database...";
        ProgressPanel.IsVisible = true;
        ProgressMessage.Text = "Scanning folder...";
        ResultsArea.Children.Clear();
        UpdateStepDisplay();

        try
        {
            // Scan the folder to see what's available
            var (hasData, scanSummary, files) = ParadoxDatabaseImporterV3.ScanFolder(folderPath);

            if (!hasData)
            {
                ProgressPanel.IsVisible = false;
                Step3Title.Text = "No Data Found";
                
                var errorLabel = new Label
                {
                    Text = $"✅ {scanSummary}",
                    TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#EF4444"),
                    FontSize = 14,
                    Margin = new Thickness(0, 16)
                };
                ResultsArea.Children.Add(errorLabel);
                
                var retryButton = new Button
                {
                    Text = "Start Over",
                    BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6"),
                    TextColor = Colors.White,
                    Padding = new Thickness(32, 16),
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 16)
                };
                retryButton.Clicked += (s, e) => ResetWizard();
                ResultsArea.Children.Add(retryButton);
                return Task.CompletedTask;
            }

            ProgressPanel.IsVisible = false;

            // Show preview of what files were found
            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"ℹ️ Folder: {Path.GetFileName(folderPath)}");
            summary.AppendLine();
            summary.AppendLine("Paradox files found:");
            summary.Append(scanSummary);

            Step3Title.Text = "Paradox Database Found";
            
            var summaryLabel = new Label
            {
                Text = summary.ToString(),
                FontSize = 13,
                Margin = new Thickness(0, 8)
            };
            ResultsArea.Children.Add(summaryLabel);

            // Import button - same pattern as Access import
            var importButton = new Button
            {
                Text = "ℹ️ Import All Data",
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#10B981"),
                TextColor = Colors.White,
                Padding = new Thickness(24, 14),
                HorizontalOptions = LayoutOptions.Fill,
                Margin = new Thickness(0, 12, 0, 6),
                FontAttributes = FontAttributes.Bold
            };
            importButton.Clicked += async (s, e) =>
            {
                if (_isBusy) return;
                _isBusy = true;
                importButton.IsEnabled = false;
                try { await RunParadoxImportV2Async(folderPath); }
                finally { _isBusy = false; importButton.IsEnabled = true; }
            };
            ResultsArea.Children.Add(importButton);

            // Description
            var noteLabel = new Label
            {
                Text = "ℹ️ Import will create:\n" +
                       "  � Divisions, Venues, Teams, Players\n" +
                       "  � Fixtures with frame results\n" +
                       "  � All marked as [IMPORTED]",
                TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#6B7280"),
                FontSize = 11,
                Margin = new Thickness(0, 12, 0, 16)
            };
            ResultsArea.Children.Add(noteLabel);

            // Cancel button
            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#6B7280"),
                TextColor = Colors.White,
                Padding = new Thickness(24, 12),
                HorizontalOptions = LayoutOptions.Fill
            };
            cancelButton.Clicked += (s, e) => ResetWizard();
            ResultsArea.Children.Add(cancelButton);
        }
        catch (Exception ex)
        {
            ProgressPanel.IsVisible = false;
            Step3Title.Text = "Error";
            
            var errorLabel = new Label
            {
                Text = $"❌ Error: {ex.Message}",
                TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#EF4444"),
                FontSize = 14,
                Margin = new Thickness(0, 16)
            };
            ResultsArea.Children.Add(errorLabel);

            var retryButton = new Button
            {
                Text = "Start Over",
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6"),
                TextColor = Colors.White,
                Padding = new Thickness(32, 16),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 16)
            };
            retryButton.Clicked += (s, e) => ResetWizard();
            ResultsArea.Children.Add(retryButton);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Run the Paradox import using the simplified V3 importer - same pattern as Access import
    /// </summary>
    private async Task RunParadoxImportV2Async(string folderPath)
    {
        ResultsArea.Children.Clear();
        ProgressPanel.IsVisible = true;
        ProgressMessage.Text = "Importing Paradox data...";
        Step3Title.Text = "Importing...";

        // Create snapshot for rollback
        _dataStore.CreatePreImportSnapshot();

        try
        {
            // Create the importer (V3)
            var importer = new ParadoxDatabaseImporterV3(folderPath);

            // Run the import
            var (data, summary) = await importer.ImportAllAsync();

            ProgressPanel.IsVisible = false;

            if (summary.Success)
            {
                Step3Title.Text = "✅ Import Complete!";

                // Merge the imported data into the main DataStore
                _dataStore.GetData().Seasons.AddRange(data.Seasons);
                _dataStore.GetData().Divisions.AddRange(data.Divisions);
                _dataStore.GetData().Venues.AddRange(data.Venues);
                _dataStore.GetData().Teams.AddRange(data.Teams);
                _dataStore.GetData().Players.AddRange(data.Players);
                _dataStore.GetData().Fixtures.AddRange(data.Fixtures);
                await _dataStore.SaveAsync();
                _dataStore.ClearPreImportSnapshot();

                // Show summary
                var resultText = new System.Text.StringBuilder();
                resultText.AppendLine(summary.Summary);
                resultText.AppendLine();
                resultText.AppendLine("✅ Data saved successfully!");

                var summaryLabel = new Label
                {
                    Text = resultText.ToString(),
                    FontSize = 14,
                    Margin = new Thickness(0, 16)
                };
                ResultsArea.Children.Add(summaryLabel);

                // Show diagnostic log if there are messages
                if (summary.Errors.Count != 0)
                {
                    var logExpander = new Border
                    {
                        StrokeThickness = 1,
                        Stroke = Microsoft.Maui.Graphics.Color.FromArgb("#E5E7EB"),
                        BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#F9FAFB"),
                        Padding = new Thickness(12),
                        Margin = new Thickness(0, 8),
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }
                    };

                    var logStack = new VerticalStackLayout { Spacing = 2 };
                    logStack.Children.Add(new Label
                    {
                        Text = "ℹ️ Import Log:",
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 12
                    });

                    foreach (var line in summary.Errors.Take(20))
                    {
                        logStack.Children.Add(new Label
                        {
                            Text = line,
                            FontSize = 10,
                            TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#6B7280")
                        });
                    }

                    if (summary.Errors.Count > 20)
                    {
                        logStack.Children.Add(new Label
                        {
                            Text = $"... and {summary.Errors.Count - 20} more",
                            FontSize = 10,
                            FontAttributes = FontAttributes.Italic
                        });
                    }

                    logExpander.Content = logStack;
                    ResultsArea.Children.Add(logExpander);
                }
            }
            else
            {
                // Rollback on failure
                _dataStore.RestorePreImportSnapshot();
                Step3Title.Text = "ℹ️ Import Failed";

                var errorLabel = new Label
                {
                    Text = summary.Message,
                    TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#EF4444"),
                    FontSize = 14,
                    Margin = new Thickness(0, 16)
                };
                ResultsArea.Children.Add(errorLabel);

                // Show error log
                if (summary.Errors.Count != 0)
                {
                    var logLabel = new Label
                    {
                        Text = string.Join("\n", summary.Errors),
                        FontSize = 11,
                        TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#6B7280"),
                        Margin = new Thickness(0, 8)
                    };
                    ResultsArea.Children.Add(logLabel);
                }
            }

            // Save Log button - always show after import
            var saveLogButton = new Button
            {
                Text = "ℹ️ Save Log",
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#6366F1"),
                TextColor = Colors.White,
                Padding = new Thickness(24, 12),
                HorizontalOptions = LayoutOptions.Fill,
                Margin = new Thickness(0, 16, 0, 8)
            };
            saveLogButton.Clicked += async (s, e) =>
            {
                var (success, message) = await summary.SaveLogToFileAsync(folderPath);
                await DisplayAlert(success ? "Log Saved" : "Save Failed", message, "OK");
            };
            ResultsArea.Children.Add(saveLogButton);

            // Done button
            var doneButton = new Button
            {
                Text = "Done",
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#10B981"),
                TextColor = Colors.White,
                Padding = new Thickness(32, 16),
                HorizontalOptions = LayoutOptions.Fill,
                Margin = new Thickness(0, 8, 0, 8)
            };
            doneButton.Clicked += async (s, e) => await Navigation.PopAsync();
            ResultsArea.Children.Add(doneButton);

            // Import more button
            var moreButton = new Button
            {
                Text = "Import More Data",
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6"),
                TextColor = Colors.White,
                Padding = new Thickness(32, 16),
                HorizontalOptions = LayoutOptions.Fill
            };
            moreButton.Clicked += (s, e) => ResetWizard();
            ResultsArea.Children.Add(moreButton);
        }
        catch (Exception ex)
        {
            // Rollback on unhandled exception
            _dataStore.RestorePreImportSnapshot();

            ProgressPanel.IsVisible = false;
            Step3Title.Text = "Import Failed";

            var errorLabel = new Label
            {
                Text = $"❌ Error: {ex.Message}\n\nYour data has been restored to its previous state.",
                TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#EF4444"),
                FontSize = 14,
                Margin = new Thickness(0, 16)
            };
            ResultsArea.Children.Add(errorLabel);

            var retryButton = new Button
            {
                Text = "Start Over",
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6"),
                TextColor = Colors.White,
                Padding = new Thickness(32, 16),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 16)
            };
            retryButton.Clicked += (s, e) => ResetWizard();
            ResultsArea.Children.Add(retryButton);
        }
    }

    // Remove the old RunParadoxImportAsync method - replaced by RunParadoxImportV2Async above

    // ========== Navigation ==========

    private void ShowSuccessResult(string title, string message)
    {
        ProgressPanel.IsVisible = false;
        Step3Title.Text = "? " + title;
        
        ResultsArea.Children.Clear();

        var successLabel = new Label
        {
            Text = message,
            FontSize = 16,
            Margin = new Thickness(0, 16),
            HorizontalTextAlignment = TextAlignment.Center
        };
        ResultsArea.Children.Add(successLabel);

        var doneButton = new Button
        {
            Text = "Done",
            BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#10B981"),
            TextColor = Colors.White,
            Padding = new Thickness(32, 16),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 16)
        };
        doneButton.Clicked += (s, e) => ResetWizard();
        ResultsArea.Children.Add(doneButton);

        var newImportButton = new Button
        {
            Text = "Import More Data",
            BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6"),
            TextColor = Colors.White,
            Padding = new Thickness(32, 16),
            HorizontalOptions = LayoutOptions.Center
        };
        newImportButton.Clicked += (s, e) => ResetWizard();
        ResultsArea.Children.Add(newImportButton);
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        if (!_isBusy) ResetWizard();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Cancel Import", 
            "Are you sure you want to cancel this import?", 
            "Yes", "No");
        
        if (confirm)
        {
            if (!_isBusy) ResetWizard();
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s{2,}")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}

// Helper class for selected files
public class SelectedFile
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
}

// Helper class for tracking import statistics
public class ImportStats
{
    public int TeamsImported { get; set; }
    public int PlayersImported { get; set; }
    public int VenuesImported { get; set; }
    public int FixturesImported { get; set; }
    public int ResultsImported { get; set; }
    public int CompetitionsImported { get; set; }
}

// Helper class for Paradox import statistics
public class ParadoxImportStats
{
    public int DivisionsImported { get; set; }
    public int TeamsImported { get; set; }
    public int PlayersImported { get; set; }
    public int VenuesImported { get; set; }
    public int FixturesImported { get; set; }
    public int FramesImported { get; set; }
}
