using Microsoft.Maui.Storage;
using Wdpl2.Services.Import;

namespace Wdpl2.Views;

public partial class HistoricalImportPage
{
    private ImportFileIntake? _intake;
    private bool _isBusy;
    private bool _initialized;
    private bool _returningFromPreview;
    private Guid? _targetSeasonId;

    private async void OnUnifiedImportClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;
        _isBusy = true;
        ImportEntryButton.IsEnabled = false;
        try
        {
            var source = await DisplayActionSheet("Import from", "Cancel", null,
                "Files (detect automatically)", "Scan folders", "Paradox database folder", "Historical competition entry (not implemented)");
            switch (source)
            {
                case "Files (detect automatically)":
                    await SelectImportFilesAsync();
                    break;
                case "Scan folders":
                    var page = Application.Current?.Handler?.MauiContext?.Services.GetService<SmartImportPage>()
                        ?? throw new InvalidOperationException("SmartImportPage not registered");
                    await Navigation.PushAsync(page);
                    break;
                case "Paradox database folder":
                    await SelectParadoxFolderAsync();
                    break;
                case "Historical competition entry (not implemented)":
                    await DisplayAlert("Historical competitions", "The existing historical-competition entry page is a placeholder. Import competition history from Word or HTML files instead.", "OK");
                    break;
            }
        }
        catch (OperationCanceledException) { StatusLabel.Text = "Selection cancelled. Nothing was imported."; }
        catch (Exception ex) { await DisplayAlert("Cannot prepare import", ex.Message, "OK"); }
        finally
        {
            _isBusy = false;
            ImportEntryButton.IsEnabled = true;
        }
    }

    private async Task SelectImportFilesAsync()
    {
        var selected = (await FilePicker.Default.PickMultipleAsync(new PickOptions
        {
            PickerTitle = "Select league data files"
        }))?.Take(ImportFileIntake.MaxFiles + 1).ToList();
        if (selected == null || selected.Count == 0) return;
        if (selected.Count > ImportFileIntake.MaxFiles)
            throw new InvalidDataException($"Select no more than {ImportFileIntake.MaxFiles} files.");

        var kinds = selected.Select(f => ImportFileIntake.DetectKind(f.FileName)).ToList();
        if (kinds.Contains(ImportFileKind.Paradox))
            throw new InvalidDataException("Use the Paradox database folder source so all related tables and memo files are included.");
        if (selected.Count > 1 && kinds.Any(k => k != ImportFileKind.Html))
            throw new InvalidDataException("Select one database or document at a time, or select multiple HTML files for a combined preview. Use Scan folders for multi-season discovery.");

        _intake?.Dispose();
        _intake = new ImportFileIntake(FileSystem.CacheDirectory);
        _selectedFiles.Clear();
        var issues = new List<string>();
        foreach (var file in selected)
        {
            try
            {
                using var stream = await file.OpenReadAsync();
                var prepared = await _intake.AddAsync(file.FileName, stream);
                _selectedFiles.Add(new SelectedFile { FileName = prepared.FileName, FilePath = prepared.FilePath });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                issues.Add($"{file.FileName}: {ex.Message}");
            }
        }
        if (issues.Count > 0)
            await DisplayAlert("File checks", string.Join("\n", issues.Take(12)), "OK");
        if (_selectedFiles.Count == 0)
        {
            _intake.Dispose();
            _intake = null;
            return;
        }
        _selectedImportType = kinds[0] switch
        {
            ImportFileKind.Access => ImportType.AccessDatabase,
            ImportFileKind.Word => ImportType.WordDocument,
            ImportFileKind.Spreadsheet => ImportType.ExcelSpreadsheet,
            ImportFileKind.Html => _selectedFiles.Count == 1 ? ImportType.SingleHTML : ImportType.BatchHTML,
            ImportFileKind.Pdf => ImportType.PDF,
            ImportFileKind.Sql => ImportType.SqlFile,
            _ => ImportType.None
        };
        _currentStep = 2;
        FileSelectionArea.Children.Clear();
        SelectedFilesPanel.IsVisible = true;
        Step2Title.Text = $"Review {_selectedFiles.Count} file(s)";
        Step2Description.Text = $"Detected: {kinds[0]}. Continue to the import review. Nothing has been saved yet.";
        StatusLabel.Text = issues.Count == 0 ? "File checks passed." : $"{issues.Count} file(s) were not added; review the remaining selection.";
        UpdateStepDisplay();
    }

    private async Task SelectParadoxFolderAsync()
    {
        var result = await CommunityToolkit.Maui.Storage.FolderPicker.Default.PickAsync(default);
        if (!result.IsSuccessful)
        {
            if (result.Exception is OperationCanceledException) return;
            throw new IOException("Could not open the folder. Check access permissions.", result.Exception);
        }
        var path = result.Folder?.Path;
        if (string.IsNullOrEmpty(path)) return;
        var files = Directory.EnumerateFiles(path).Where(f => Path.GetExtension(f).Equals(".db", StringComparison.OrdinalIgnoreCase)).ToList();
        if (files.Count == 0) throw new InvalidDataException("No Paradox .DB tables were found in that folder.");
        long total = 0;
        foreach (var file in files)
        {
            var (valid, error) = DataStore.ValidateImportFile(file);
            if (!valid) throw new InvalidDataException(error);
            total += new FileInfo(file).Length;
        }
        if (total > ImportFileIntake.MaxBatchBytes) throw new InvalidDataException("The Paradox tables exceed the 500 MB import limit.");
        _selectedImportType = ImportType.ParadoxFolder;
        await ProcessParadoxFolderAsync(path);
    }

    private async Task<bool> ConfirmTargetSeasonAsync()
    {
        if (_selectedImportType is not (ImportType.ExcelSpreadsheet or ImportType.PDF or ImportType.SingleHTML)) return true;
        var id = Wdpl2.Services.SeasonService.Current.CurrentSeasonId;
        var season = _dataStore.GetData().Seasons.FirstOrDefault(s => s.Id == id);
        if (season == null || season.IsLocked)
        {
            await DisplayAlert("Choose an unlocked season", "Select an unlocked working season on the Seasons page before importing this document.", "OK");
            return false;
        }
        if (!await DisplayAlert("Import target", $"Import this document into {season.Name}?", "Continue", "Cancel")) return false;
        _targetSeasonId = season.Id;
        return true;
    }
}
