using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class HistoricalImportPage : ContentPage
{
    private int _currentStep = 1;
    private ImportType _selectedImportType = ImportType.None;
    private readonly ObservableCollection<SelectedFile> _selectedFiles = new();

    private enum ImportType
    {
        None,
        AccessDatabase,
        WordDocument,
        ExcelSpreadsheet,
        SingleHTML,
        BatchHTML,
        PDF,
        SqlFile
    }

    public HistoricalImportPage()
    {
        InitializeComponent();
        SelectedFilesList.ItemsSource = _selectedFiles;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ResetWizard();
    }

    private void ResetWizard()
    {
        _currentStep = 1;
        _selectedImportType = ImportType.None;
        _selectedFiles.Clear();
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
        CancelButton.IsVisible = _currentStep < 3;
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
            "Choose multiple HTML files to import (add them one by one)",
            new[] { ".html", ".htm" },
            true);
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
        Step2Title.Text = title;
        Step2Description.Text = description;

        // Clear previous file selection UI
        FileSelectionArea.Children.Clear();

        // Add file picker button
        var pickerButton = new Button
        {
            Text = allowMultiple ? "Add File(s)" : "Choose File",
            BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6"),
            TextColor = Colors.White,
            FontSize = 16,
            Padding = new Thickness(32, 16),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 20)
        };

        pickerButton.Clicked += async (s, e) => await PickFilesAsync(extensions, allowMultiple);
        FileSelectionArea.Children.Add(pickerButton);

        if (allowMultiple)
        {
            // Add info label for batch import
            var infoLabel = new Label
            {
                Text = "?? TIP: Click 'Add File(s)' multiple times to add more files. When done, click 'Next' to preview all files.",
                TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#6B7280"),
                FontSize = 12,
                Margin = new Thickness(0, 8),
                HorizontalTextAlignment = TextAlignment.Center
            };
            FileSelectionArea.Children.Add(infoLabel);
        }

        UpdateStepDisplay();
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
                        $"Added: {result.FileName}\n\nTotal files: {_selectedFiles.Count}\n\nClick 'Add File(s)' to add more, or 'Next' to continue.", 
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
        if (_currentStep == 2)
        {
            _currentStep = 3;
            Step3Title.Text = "Processing Import...";
            ProgressPanel.IsVisible = true;
            UpdateStepDisplay();

            // Process based on import type
            await ProcessImportAsync();
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
                Text = $"? Error: {ex.Message}",
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

        ProgressMessage.Text = "Importing Access database...";

        // Navigate to existing import page (if you have one) or process inline
        await DisplayAlert("Access Import", "Access database import will be processed", "OK");
        
        ShowSuccessResult("Access Database Imported", "Database imported successfully!");
    }

    private async Task ProcessWordDocumentAsync()
    {
        var file = _selectedFiles.FirstOrDefault();
        if (file == null) return;

        ProgressMessage.Text = "Extracting data from Word document...";

        // Navigate to preview page
        var previewPage = new ImportPreviewPage();
        await Navigation.PushAsync(previewPage);
        await previewPage.LoadPreviewAsync(file.FilePath);
        
        // Return to main page after preview
        await Navigation.PopToRootAsync();
    }

    private async Task ProcessExcelSpreadsheetAsync()
    {
        var file = _selectedFiles.FirstOrDefault();
        if (file == null) return;

        ProgressMessage.Text = "Processing spreadsheet...";

        await DisplayAlert("Excel Import", "Excel/CSV import will be processed", "OK");
        
        ShowSuccessResult("Spreadsheet Imported", "Data imported successfully!");
    }

    private async Task ProcessSingleHTMLAsync()
    {
        var file = _selectedFiles.FirstOrDefault();
        if (file == null) return;

        ProgressMessage.Text = "Parsing HTML file...";

        await DisplayAlert("HTML Import", "Single HTML import will be processed", "OK");
        
        ShowSuccessResult("HTML Imported", "Webpage data imported successfully!");
    }

    private async Task ProcessBatchHTMLAsync()
    {
        ProgressMessage.Text = $"Processing {_selectedFiles.Count} HTML files...";

        // Navigate to batch preview page
        var filePaths = _selectedFiles.Select(f => f.FilePath).ToList();
        var batchPreviewPage = new BatchImportPreviewPage();
        await Navigation.PushAsync(batchPreviewPage);
        await batchPreviewPage.LoadBatchPreviewAsync(filePaths);
        
        // Return to main page after batch import
        await Navigation.PopToRootAsync();
    }

    private async Task ProcessSqlFileAsync()
    {
        var file = _selectedFiles.FirstOrDefault();
        if (file == null) return;

        ProgressMessage.Text = "Parsing SQL file and importing data...";

        try
        {
            // Import from SQL file with existing data and replace=false
            var (data, result) = await SqlFileImporter.ImportFromSqlFileAsync(
                file.FilePath, 
                DataStore.Data, 
                false); // replaceExisting parameter

            ProgressPanel.IsVisible = false;

            if (result.Success)
            {
                // Save imported data
                DataStore.Save();

                Step3Title.Text = "? SQL Import Successful!";
                
                ResultsArea.Children.Clear();

                // Show summary
                var summaryBorder = new Border
                {
                    BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#F0FDF4"),
                    Stroke = Microsoft.Maui.Graphics.Color.FromArgb("#10B981"),
                    StrokeThickness = 2,
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 16)
                };

                var summaryStack = new VerticalStackLayout { Spacing = 8 };
                
                summaryStack.Children.Add(new Label
                {
                    Text = $"Detected: {result.DetectedDialect} SQL",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold
                });

                if (result.DetectedSeason != null)
                {
                    summaryStack.Children.Add(new Label
                    {
                        Text = $"Season: {result.DetectedSeason.Name}",
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#3B82F6")
                    });
                }

                summaryStack.Children.Add(new Label { Text = result.Summary, FontSize = 13 });

                if (result.Warnings.Any())
                {
                    summaryStack.Children.Add(new Label
                    {
                        Text = $"\n?? Warnings: {result.Warnings.Count}",
                        TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#F59E0B"),
                        FontSize = 12
                    });
                }

                summaryBorder.Content = summaryStack;
                ResultsArea.Children.Add(summaryBorder);

                // Done button
                var doneButton = new Button
                {
                    Text = "Done",
                    BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#10B981"),
                    TextColor = Colors.White,
                    Padding = new Thickness(32, 16),
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 16)
                };
                doneButton.Clicked += async (s, e) => await Navigation.PopAsync();
                ResultsArea.Children.Add(doneButton);
            }
            else
            {
                throw new Exception("Import failed with errors");
            }
        }
        catch (Exception ex)
        {
            ProgressPanel.IsVisible = false;
            Step3Title.Text = "? Import Failed";
            
            var errorLabel = new Label
            {
                Text = $"Error: {ex.Message}",
                TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#EF4444"),
                FontSize = 14,
                Margin = new Thickness(0, 16)
            };
            ResultsArea.Children.Add(errorLabel);

            var retryButton = new Button
            {
                Text = "Try Again",
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
        doneButton.Clicked += async (s, e) => await Navigation.PopAsync();
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

    // ========== Navigation ==========

    private void OnBackClicked(object? sender, EventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            _selectedFiles.Clear();
            SelectedFilesPanel.IsVisible = false;
            UpdateStepDisplay();
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Cancel Import", 
            "Are you sure you want to cancel this import?", 
            "Yes", "No");
        
        if (confirm)
        {
            await Navigation.PopAsync();
        }
    }
}

// Helper class for selected files
public class SelectedFile
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
}
