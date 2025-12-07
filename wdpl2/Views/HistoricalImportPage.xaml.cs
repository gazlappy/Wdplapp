using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Wdpl2.Services;
using Wdpl2.Models;

namespace Wdpl2.Views;

public partial class HistoricalImportPage : ContentPage
{
    private readonly ObservableCollection<Season> _seasons = new();
    private readonly ObservableCollection<ImportHistoryItem> _importHistory = new();

    public HistoricalImportPage()
    {
        InitializeComponent();
        
        SeasonPicker.ItemsSource = _seasons;
        ImportHistoryList.ItemsSource = _importHistory;
        
        LoadSeasons();
        LoadImportHistory();
    }

    private void LoadSeasons()
    {
        _seasons.Clear();
        foreach (var season in DataStore.Data.Seasons.OrderByDescending(s => s.StartDate))
        {
            _seasons.Add(season);
        }

        if (_seasons.Any())
            SeasonPicker.SelectedIndex = 0;
    }

    private void LoadImportHistory()
    {
        // In production, load from persistent storage
        StatusLabel.Text = "Ready to import historical data";
    }

    private async void OnCreateSeasonClicked(object? sender, EventArgs e)
    {
        var seasonName = await DisplayPromptAsync("New Season", "Enter season name (e.g., '2023-2024'):");
        if (string.IsNullOrWhiteSpace(seasonName)) return;

        var newSeason = new Season
        {
            Id = Guid.NewGuid(),
            Name = seasonName,
            StartDate = DateTime.Now,
            EndDate = DateTime.Now.AddMonths(8)
        };

        DataStore.Data.Seasons.Add(newSeason);
        DataStore.Save();

        LoadSeasons();
        SeasonPicker.SelectedItem = newSeason;
        StatusLabel.Text = $"? Created season: {seasonName}";
    }

    private async void OnImportLeagueTableClicked(object? sender, EventArgs e)
    {
        await ImportFileAsync("League Table", HistoricalDataImporter.ImportFormat.CSV);
    }

    private async void OnImportResultsClicked(object? sender, EventArgs e)
    {
        await ImportFileAsync("Results", HistoricalDataImporter.ImportFormat.CSV);
    }

    private async void OnImportPlayersClicked(object? sender, EventArgs e)
    {
        await ImportFileAsync("Players/Teams", HistoricalDataImporter.ImportFormat.CSV);
    }

    private async void OnImportImageClicked(object? sender, EventArgs e)
    {
        await ImportFileAsync("Image (OCR)", HistoricalDataImporter.ImportFormat.Image);
    }

    private async void OnImportWordClicked(object? sender, EventArgs e)
    {
        await ImportDocumentAsync("Word Document", HistoricalDataImporter.ImportFormat.Word);
    }

    private async void OnImportWordWithPreviewClicked(object? sender, EventArgs e)
    {
        try
        {
            StatusLabel.Text = "Selecting Word document for preview...";

            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".docx", ".doc" } },
                { DevicePlatform.Android, new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/msword" } },
                { DevicePlatform.iOS, new[] { "org.openxmlformats.wordprocessingml.document", "com.microsoft.word.doc" } }
            });

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Word document",
                FileTypes = fileTypes
            });

            if (result == null)
            {
                StatusLabel.Text = "Preview cancelled";
                return;
            }

            StatusLabel.Text = $"Opening preview for {result.FileName}...";

            // Navigate to preview page
            var previewPage = new ImportPreviewPage();
            await Navigation.PushAsync(previewPage);
            
            // Load preview (async after navigation)
            await previewPage.LoadPreviewAsync(result.FullPath);
            
            StatusLabel.Text = "Preview opened";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Preview failed: {ex.Message}", "OK");
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void OnImportPowerPointClicked(object? sender, EventArgs e)
    {
        await ImportDocumentAsync("PowerPoint", HistoricalDataImporter.ImportFormat.PowerPoint);
    }

    private async void OnImportExcelClicked(object? sender, EventArgs e)
    {
        await ImportDocumentAsync("Excel Workbook", HistoricalDataImporter.ImportFormat.Excel);
    }

    private async void OnImportPDFClicked(object? sender, EventArgs e)
    {
        await ImportDocumentAsync("PDF Document", HistoricalDataImporter.ImportFormat.PDF);
    }

    private async void OnImportHTMLClicked(object? sender, EventArgs e)
    {
        await ImportFileAsync("HTML", HistoricalDataImporter.ImportFormat.HTML);
    }

    private async void OnImportHTMLBatchClicked(object? sender, EventArgs e)
    {
        try
        {
            StatusLabel.Text = "Selecting HTML files for batch import...";

            // Note: .NET MAUI FilePicker doesn't support multi-select yet
            // We'll prompt user to select files one by one or use a folder picker
            var files = new System.Collections.Generic.List<string>();
            
            var answer = await DisplayAlert(
                "Batch HTML Import",
                "Select multiple HTML files.\n\nClick 'Add File' to keep adding files, or 'Done' when finished.",
                "Add File",
                "Done");

            while (answer)
            {
                var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".html", ".htm" } },
                    { DevicePlatform.Android, new[] { "text/html" } },
                    { DevicePlatform.iOS, new[] { "public.html" } }
                });

                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = $"Select HTML file ({files.Count + 1})",
                    FileTypes = fileTypes
                });

                if (result != null)
                {
                    files.Add(result.FullPath);
                    StatusLabel.Text = $"Added {files.Count} file(s)...";
                    
                    answer = await DisplayAlert(
                        "Batch HTML Import",
                        $"Added: {result.FileName}\n\nTotal files: {files.Count}\n\nAdd another file?",
                        "Add File",
                        "Done");
                }
                else
                {
                    break;
                }
            }

            if (!files.Any())
            {
                StatusLabel.Text = "Batch import cancelled - no files selected";
                return;
            }

            StatusLabel.Text = $"Opening batch preview for {files.Count} file(s)...";

            // Navigate to batch preview page
            var batchPreviewPage = new BatchImportPreviewPage();
            await Navigation.PushAsync(batchPreviewPage);
            
            // Load batch preview (async after navigation)
            await batchPreviewPage.LoadBatchPreviewAsync(files);
            
            StatusLabel.Text = $"Batch preview opened for {files.Count} files";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Batch preview failed: {ex.Message}", "OK");
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async Task ImportFileAsync(string dataType, HistoricalDataImporter.ImportFormat format)
    {
        try
        {
            var selectedSeason = SeasonPicker.SelectedItem as Season;
            if (selectedSeason == null)
            {
                await DisplayAlert("No Season", "Please select a target season first", "OK");
                return;
            }

            StatusLabel.Text = $"?? Selecting {dataType} file...";

            // File picker
            var fileTypes = format switch
            {
                HistoricalDataImporter.ImportFormat.CSV => new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv", ".txt" } },
                    { DevicePlatform.Android, new[] { "text/csv", "text/plain" } },
                    { DevicePlatform.iOS, new[] { "public.comma-separated-values-text", "public.plain-text" } }
                }),
                HistoricalDataImporter.ImportFormat.HTML => new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".html", ".htm", ".mhtml" } },
                    { DevicePlatform.Android, new[] { "text/html" } },
                    { DevicePlatform.iOS, new[] { "public.html" } }
                }),
                HistoricalDataImporter.ImportFormat.Image => new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".bmp" } },
                    { DevicePlatform.Android, new[] { "image/*" } },
                    { DevicePlatform.iOS, new[] { "public.image" } }
                }),
                _ => new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".*" } },
                    { DevicePlatform.Android, new[] { "*/*" } },
                    { DevicePlatform.iOS, new[] { "public.data" } }
                })
            };

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = $"Select {dataType} file",
                FileTypes = fileTypes
            });

            if (result == null)
            {
                StatusLabel.Text = "Import cancelled";
                return;
            }

            StatusLabel.Text = $"? Importing {result.FileName}...";

            // Perform import
            HistoricalDataImporter.ImportResult importResult;

            if (format == HistoricalDataImporter.ImportFormat.HTML)
            {
                importResult = await HistoricalDataImporter.ImportFromHTMLAsync(
                    result.FullPath,
                    selectedSeason.Id,
                    DataStore.Data
                );
            }
            else if (format == HistoricalDataImporter.ImportFormat.Image)
            {
                importResult = await HistoricalDataImporter.ImportFromImageAsync(
                    result.FullPath,
                    selectedSeason.Id,
                    DataStore.Data
                );
            }
            else
            {
                importResult = await HistoricalDataImporter.ImportFromSpreadsheetAsync(
                    result.FullPath,
                    selectedSeason.Id,
                    DataStore.Data
                );
            }

            // Save changes
            if (importResult.Success)
            {
                DataStore.Save();
                
                // Add to history
                _importHistory.Insert(0, new ImportHistoryItem
                {
                    FileName = result.FileName,
                    DataType = dataType,
                    Format = format,
                    Summary = importResult.Summary,
                    RecordsImported = importResult.RecordsImported,
                    Timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    CanUndo = true,
                    Icon = format switch
                    {
                        HistoricalDataImporter.ImportFormat.CSV => "??",
                        HistoricalDataImporter.ImportFormat.HTML => "??",
                        HistoricalDataImporter.ImportFormat.Image => "??",
                        _ => "??"
                    }
                });

                StatusLabel.Text = $"? Success! Imported {importResult.RecordsImported} records from {result.FileName}";
                
                if (importResult.Warnings.Any())
                {
                    await DisplayAlert("Import Complete (with warnings)", 
                        string.Join("\n", importResult.Warnings), "OK");
                }
            }
            else
            {
                var errorMsg = string.Join("\n", importResult.Errors);
                await DisplayAlert("Import Failed", errorMsg, "OK");
                StatusLabel.Text = $"? Import failed - see errors";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Import error: {ex.Message}", "OK");
            StatusLabel.Text = $"? Error: {ex.Message}";
        }
    }

    private async void OnDownloadLeagueTableTemplateClicked(object? sender, EventArgs e)
    {
        await DownloadTemplateAsync("LeagueTable", 
            "Position,Team,Played,Won,Drawn,Lost,FramesFor,FramesAgainst,Points\n" +
            "1,Team A,10,7,1,2,65,45,15\n" +
            "2,Team B,10,6,2,2,60,50,14");
    }

    private async void OnDownloadResultsTemplateClicked(object? sender, EventArgs e)
    {
        await DownloadTemplateAsync("Results",
            "Date,HomeTeam,AwayTeam,HomeScore,AwayScore\n" +
            "01/01/2024,Team A,Team B,6,4\n" +
            "08/01/2024,Team C,Team D,5,5");
    }

    private async void OnDownloadPlayersTemplateClicked(object? sender, EventArgs e)
    {
        await DownloadTemplateAsync("Players",
            "PlayerName,Team,Rating\n" +
            "John Smith,Team A,1250\n" +
            "Jane Doe,Team B,1150");
    }

    private async Task DownloadTemplateAsync(string name, string content)
    {
        try
        {
            var fileName = $"{name}_Template.csv";
            var path = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
            await System.IO.File.WriteAllTextAsync(path, content);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = $"Download {name} Template",
                File = new ShareFile(path, "text/csv")
            });

            StatusLabel.Text = $"? {name} template downloaded";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Template download failed: {ex.Message}", "OK");
        }
    }

    private async void OnUndoImportClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ImportHistoryItem item)
        {
            var confirm = await DisplayActionSheet(
                $"Undo import of {item.FileName}?",
                "Cancel",
                "Undo Import",
                "This cannot be undone");

            if (confirm == "Undo Import")
            {
                // In production, implement proper undo/rollback
                // For now, just mark as undone
                item.CanUndo = false;
                StatusLabel.Text = $"?? Manual rollback required for {item.FileName}";
                
                await DisplayAlert("Undo Import", 
                    "Automatic undo not yet implemented. Please manually remove imported records or restore from backup.", 
                    "OK");
            }
        }
    }

    private async Task ImportDocumentAsync(string dataType, HistoricalDataImporter.ImportFormat format)
    {
        try
        {
            var selectedSeason = SeasonPicker.SelectedItem as Season;
            if (selectedSeason == null)
            {
                await DisplayAlert("No Season", "Please select a target season first", "OK");
                return;
            }

            StatusLabel.Text = $"Selecting {dataType} file...";

            // File picker for document types
            var fileTypes = format switch
            {
                HistoricalDataImporter.ImportFormat.Word => new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".docx", ".doc" } },
                    { DevicePlatform.Android, new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/msword" } },
                    { DevicePlatform.iOS, new[] { "org.openxmlformats.wordprocessingml.document", "com.microsoft.word.doc" } }
                }),
                HistoricalDataImporter.ImportFormat.PowerPoint => new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".pptx", ".ppt" } },
                    { DevicePlatform.Android, new[] { "application/vnd.openxmlformats-officedocument.presentationml.presentation", "application/vnd.ms-powerpoint" } },
                    { DevicePlatform.iOS, new[] { "org.openxmlformats.presentationml.presentation", "com.microsoft.powerpoint.ppt" } }
                }),
                HistoricalDataImporter.ImportFormat.Excel => new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".xlsx", ".xls" } },
                    { DevicePlatform.Android, new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/vnd.ms-excel" } },
                    { DevicePlatform.iOS, new[] { "org.openxmlformats.spreadsheetml.sheet", "com.microsoft.excel.xls" } }
                }),
                HistoricalDataImporter.ImportFormat.PDF => new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".pdf" } },
                    { DevicePlatform.Android, new[] { "application/pdf" } },
                    { DevicePlatform.iOS, new[] { "com.adobe.pdf" } }
                }),
                _ => new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".*" } },
                    { DevicePlatform.Android, new[] { "*/*" } },
                    { DevicePlatform.iOS, new[] { "public.data" } }
                })
            };

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = $"Select {dataType} file",
                FileTypes = fileTypes
            });

            if (result == null)
            {
                StatusLabel.Text = "Import cancelled";
                return;
            }

            var extension = Path.GetExtension(result.FileName).ToLower();
            
            // Show conversion notice for .doc files
            if (extension == ".doc")
            {
                StatusLabel.Text = $"Converting {result.FileName} (.doc ? .docx)...";
                await Task.Delay(100); // Let UI update
            }
            else
            {
                StatusLabel.Text = $"Importing {result.FileName}...";
            }

            // Perform document import
            var importResult = await HistoricalDataImporter.ImportFromDocumentAsync(
                result.FullPath,
                selectedSeason.Id,
                DataStore.Data
            );

            // Save changes
            if (importResult.Success)
            {
                DataStore.Save();
                
                // Add to history
                _importHistory.Insert(0, new ImportHistoryItem
                {
                    FileName = result.FileName,
                    DataType = dataType,
                    Format = format,
                    Summary = importResult.Summary,
                    RecordsImported = importResult.RecordsImported,
                    Timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    CanUndo = true,
                    Icon = format switch
                    {
                        HistoricalDataImporter.ImportFormat.Word => "[W]",
                        HistoricalDataImporter.ImportFormat.PowerPoint => "[P]",
                        HistoricalDataImporter.ImportFormat.Excel => "[X]",
                        HistoricalDataImporter.ImportFormat.PDF => "[PDF]",
                        _ => "[DOC]"
                    }
                });

                var statusMessage = extension == ".doc" 
                    ? $"Success! Converted and imported {importResult.RecordsImported} records from {result.FileName}"
                    : $"Success! Imported {importResult.RecordsImported} records from {result.FileName}";
                
                StatusLabel.Text = statusMessage;
                
                if (importResult.Warnings.Any())
                {
                    var warningMessage = string.Join("\n", importResult.Warnings);
                    if (extension == ".doc")
                    {
                        warningMessage = $"Legacy .doc file was automatically converted to .docx!\n\n{warningMessage}";
                    }
                    await DisplayAlert("Import Complete (with notes)", warningMessage, "OK");
                }
                else if (extension == ".doc")
                {
                    await DisplayAlert("Success!", 
                        $"Legacy .doc file was automatically converted and imported!\n\n" +
                        $"Imported {importResult.RecordsImported} records.\n\n" +
                        $"TIP: For best results, save your files as .docx in Word.", 
                        "OK");
                }
            }
            else
            {
                var errorMsg = string.Join("\n", importResult.Errors);
                await DisplayAlert("Import Failed", errorMsg, "OK");
                StatusLabel.Text = "Import failed - see errors";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Import error: {ex.Message}", "OK");
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }
}

public class ImportHistoryItem
{
    public string FileName { get; set; } = "";
    public string DataType { get; set; } = "";
    public HistoricalDataImporter.ImportFormat Format { get; set; }
    public string Summary { get; set; } = "";
    public int RecordsImported { get; set; }
    public string Timestamp { get; set; } = "";
    public bool CanUndo { get; set; }
    public string Icon { get; set; } = "??";
}
