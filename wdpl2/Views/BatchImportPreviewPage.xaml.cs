using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class BatchImportPreviewPage : ContentPage
{
    private BatchImportPreview? _batchPreview;
    private readonly ObservableCollection<Season> _seasons = new();
    private readonly ObservableCollection<ImportFilePreview> _files = new();

    public BatchImportPreviewPage()
    {
        InitializeComponent();
        
        SeasonPicker.ItemsSource = _seasons;
        FilesList.ItemsSource = _files;
    }

    public async Task LoadBatchPreviewAsync(System.Collections.Generic.List<string> filePaths)
    {
        try
        {
            // Show loading
            ImportButton.IsEnabled = false;
            SummaryLabel.Text = $"Loading {filePaths.Count} file(s)...";

            // Create batch preview
            _batchPreview = await BatchHtmlImportService.CreateBatchPreviewAsync(
                filePaths, 
                DataStore.Data);

            if (_batchPreview == null || _batchPreview.IsEmpty)
            {
                await DisplayAlert("No Data", "No valid data found in selected files", "OK");
                await Navigation.PopAsync();
                return;
            }

            // Load seasons
            LoadSeasons();

            // Display files
            DisplayFiles();

            // Show summary
            UpdateSummary();

            // Show warnings/errors
            ShowAlerts();

            ImportButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load batch preview: {ex.Message}", "OK");
            await Navigation.PopAsync();
        }
    }

    private void LoadSeasons()
    {
        _seasons.Clear();
        
        foreach (var season in DataStore.Data.Seasons.OrderByDescending(s => s.StartDate))
        {
            _seasons.Add(season);
        }

        // Auto-select first season
        if (_seasons.Any())
        {
            SeasonPicker.SelectedIndex = 0;
        }
    }

    private void DisplayFiles()
    {
        if (_batchPreview == null) return;

        _files.Clear();
        foreach (var file in _batchPreview.Files)
        {
            _files.Add(file);
        }
    }

    private void UpdateSummary()
    {
        if (_batchPreview == null) return;

        SummaryLabel.Text = $"Files: {_batchPreview.TotalFiles} ({_batchPreview.SelectedFiles} selected) | Records: {_batchPreview.TotalRecords}";
    }

    private void ShowAlerts()
    {
        if (_batchPreview == null) return;

        AlertsStack.Children.Clear();

        var hasIssues = false;

        // Show file-level errors
        var filesWithErrors = _batchPreview.Files.Where(f => f.HasErrors).ToList();
        if (filesWithErrors.Any())
        {
            hasIssues = true;
            AlertsStack.Children.Add(new Label
            {
                Text = $"? {filesWithErrors.Count} file(s) have errors",
                TextColor = Colors.White,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold
            });
        }

        // Show file-level warnings
        var filesWithWarnings = _batchPreview.Files.Where(f => f.HasWarnings).ToList();
        if (filesWithWarnings.Any())
        {
            hasIssues = true;
            AlertsStack.Children.Add(new Label
            {
                Text = $"? {filesWithWarnings.Count} file(s) have warnings",
                TextColor = Colors.White,
                FontSize = 12
            });
        }

        AlertsBorder.IsVisible = hasIssues;
    }

    private void OnSeasonChanged(object? sender, EventArgs e)
    {
        var season = SeasonPicker.SelectedItem as Season;
        if (season != null && _batchPreview != null)
        {
            _batchPreview.TargetSeasonId = season.Id;
            _batchPreview.TargetSeasonName = season.Name;
            SeasonLabel.Text = $"Target Season: {season.Name}";
        }
    }

    private async void OnCreateSeasonClicked(object? sender, EventArgs e)
    {
        var seasonName = await DisplayPromptAsync(
            "New Season",
            "Enter season name:",
            initialValue: $"Season {DateTime.Now.Year}");

        if (string.IsNullOrWhiteSpace(seasonName))
            return;

        var season = new Season
        {
            Id = Guid.NewGuid(),
            Name = seasonName,
            StartDate = DateTime.Now,
            IsActive = false
        };

        DataStore.Data.Seasons.Add(season);
        DataStore.Save();

        _seasons.Add(season);
        SeasonPicker.SelectedItem = season;

        await DisplayAlert("Success", $"Created season: {seasonName}", "OK");
    }

    private void OnSelectAllFilesClicked(object? sender, EventArgs e)
    {
        if (_batchPreview == null) return;

        foreach (var file in _batchPreview.Files)
        {
            file.Include = true;
        }
        
        RefreshFilesList();
        UpdateSummary();
    }

    private void OnDeselectAllFilesClicked(object? sender, EventArgs e)
    {
        if (_batchPreview == null) return;

        foreach (var file in _batchPreview.Files)
        {
            file.Include = false;
        }
        
        RefreshFilesList();
        UpdateSummary();
    }

    private void OnExpandAllClicked(object? sender, EventArgs e)
    {
        if (_batchPreview == null) return;

        foreach (var file in _batchPreview.Files)
        {
            file.IsExpanded = true;
        }
        
        RefreshFilesList();
    }

    private void OnCollapseAllClicked(object? sender, EventArgs e)
    {
        if (_batchPreview == null) return;

        foreach (var file in _batchPreview.Files)
        {
            file.IsExpanded = false;
        }
        
        RefreshFilesList();
    }

    private void OnToggleFileClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ImportFilePreview file)
        {
            file.IsExpanded = !file.IsExpanded;
            RefreshFilesList();
        }
    }

    private void RefreshFilesList()
    {
        // Force UI refresh
        var temp = FilesList.ItemsSource;
        FilesList.ItemsSource = null;
        FilesList.ItemsSource = temp;
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_batchPreview == null)
            {
                await DisplayAlert("Error", "No preview data available", "OK");
                return;
            }

            var selectedSeason = SeasonPicker.SelectedItem as Season;
            if (selectedSeason == null)
            {
                await DisplayAlert("No Season", "Please select a target season", "OK");
                return;
            }

            var selectedFiles = _batchPreview.Files.Count(f => f.Include);
            if (selectedFiles == 0)
            {
                await DisplayAlert("Nothing Selected", "Please select at least one file to import", "OK");
                return;
            }

            // Confirm import
            var totalRecords = _batchPreview.TotalRecords;
            var confirm = await DisplayAlert(
                "Confirm Batch Import",
                $"Import {selectedFiles} file(s) containing {totalRecords} record(s) into {selectedSeason.Name}?\n\n" +
                $"This will add teams, players, and divisions to the database.",
                "Yes, Import All",
                "Cancel");

            if (!confirm)
                return;

            // Show progress
            ImportButton.IsEnabled = false;
            ImportButton.Text = "Importing...";
            ProgressBorder.IsVisible = true;
            ProgressBar.Progress = 0;

            // Create progress handler
            var progress = new Progress<BatchImportProgress>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProgressBar.Progress = p.PercentComplete / 100.0;
                    ProgressLabel.Text = $"Processing: {p.CurrentFile}";
                    ProgressDetailLabel.Text = $"File {p.FilesProcessed + 1} of {p.TotalFiles}";
                });
            });

            // Perform batch import
            var result = await BatchHtmlImportService.ApplyBatchImportAsync(
                _batchPreview,
                selectedSeason.Id,
                DataStore.Data,
                progress);

            // Hide progress
            ProgressBorder.IsVisible = false;

            if (result.Success)
            {
                // Save changes
                DataStore.Save();

                await DisplayAlert(
                    "Batch Import Complete!",
                    result.DetailedSummary,
                    "OK");

                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlert(
                    "Batch Import Completed with Errors",
                    result.DetailedSummary + "\n\n" +
                    "Errors:\n" + string.Join("\n", result.Errors),
                    "OK");
                
                ImportButton.IsEnabled = true;
                ImportButton.Text = "Import All Selected";
                
                // Refresh file list to show updated statuses
                RefreshFilesList();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Batch import failed: {ex.Message}", "OK");
            ImportButton.IsEnabled = true;
            ImportButton.Text = "Import All Selected";
            ProgressBorder.IsVisible = false;
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Cancel Batch Import",
            "Are you sure you want to cancel? All preview data will be lost.",
            "Yes, Cancel",
            "No");

        if (confirm)
        {
            await Navigation.PopAsync();
        }
    }
}
