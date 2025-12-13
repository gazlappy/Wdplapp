using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

/// <summary>
/// Dedicated page for importing WDPL SQL dump files from phpMyAdmin/VBA Access
/// </summary>
public partial class SqlImportPage : ContentPage
{
    private string? _selectedFilePath;
    private SqlFileImporter.SqlImportResult? _lastImportResult;
    private SqlFileImporter.ParsedSqlData? _parsedData;

    public SqlImportPage()
    {
        InitializeComponent();
        BuildUI();
    }

    private void BuildUI()
    {
        Title = "SQL Import";

        var layout = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        // Header
        layout.Children.Add(new Label
        {
            Text = "??? Import WDPL SQL Dump",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        layout.Children.Add(new Label
        {
            Text = "Two-step import process:\n1. Preview parsed data\n2. Confirm and import",
            FontSize = 14,
            TextColor = Colors.Gray,
            Margin = new Thickness(0, 0, 0, 16)
        });

        // Info frame
        var infoFrame = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#90CAF9"),
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            Padding = 16,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var infoLayout = new VerticalStackLayout { Spacing = 8 };
        
        infoLayout.Children.Add(new Label
        {
            Text = "?? Supported VBA/WDPL Tables:",
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1976D2")
        });

        var tables = new[]
        {
            "? tblleague - Season and league settings",
            "? tbldivisions - Divisions configuration",
            "? tblfixtures - Fixture scheduling",
            "? tblmatchdetail/tblplayerresult - Frame results",
            "? Automatically creates placeholder teams/players",
            "? Preserves VBA IDs for reference"
        };

        foreach (var table in tables)
        {
            infoLayout.Children.Add(new Label
            {
                Text = table,
                FontSize = 13,
                TextColor = Color.FromArgb("#424242"),
                Margin = new Thickness(8, 0, 0, 0)
            });
        }

        infoFrame.Content = infoLayout;
        layout.Children.Add(infoFrame);

        // File selection
        var fileFrame = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#E0E0E0"),
            Padding = 16,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var fileLayout = new VerticalStackLayout { Spacing = 12 };

        var selectBtn = new Button
        {
            Text = "?? Select SQL File (*.sql)",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            FontSize = 16,
            HeightRequest = 50,
            CornerRadius = 8
        };
        selectBtn.Clicked += OnSelectFileClicked;
        fileLayout.Children.Add(selectBtn);

        var fileLabel = new Label
        {
            Text = "No file selected",
            TextColor = Colors.Gray,
            HorizontalTextAlignment = TextAlignment.Center,
            StyleId = "FileLabel"
        };
        fileLayout.Children.Add(fileLabel);

        fileFrame.Content = fileLayout;
        layout.Children.Add(fileFrame);

        // Preview section (hidden initially)
        var previewFrame = new Border
        {
            StrokeThickness = 2,
            Stroke = Color.FromArgb("#2196F3"),
            Padding = 16,
            IsVisible = false,
            StyleId = "PreviewFrame",
            Margin = new Thickness(0, 0, 0, 16)
        };

        var previewLayout = new VerticalStackLayout { Spacing = 8 };
        previewLayout.Children.Add(new Label
        {
            Text = "?? Data Preview",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        });

        var previewScroll = new ScrollView { HeightRequest = 200 };
        var previewText = new Label
        {
            Text = "",
            FontFamily = "Courier New",
            FontSize = 12,
            StyleId = "PreviewText"
        };
        previewScroll.Content = previewText;
        previewLayout.Children.Add(previewScroll);

        previewFrame.Content = previewLayout;
        layout.Children.Add(previewFrame);

        // Buttons row
        var buttonsLayout = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Fill
        };

        // Preview button
        var previewBtn = new Button
        {
            Text = "??? Preview Data",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            FontSize = 16,
            HeightRequest = 54,
            CornerRadius = 8,
            IsEnabled = false,
            StyleId = "PreviewButton",
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        previewBtn.Clicked += OnPreviewClicked;
        buttonsLayout.Children.Add(previewBtn);

        // Import button (hidden until preview)
        var importBtn = new Button
        {
            Text = "? Confirm Import",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            FontSize = 16,
            HeightRequest = 54,
            CornerRadius = 8,
            IsVisible = false,
            StyleId = "ImportButton",
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        importBtn.Clicked += OnImportClicked;
        buttonsLayout.Children.Add(importBtn);

        // Rollback button
        var rollbackBtn = new Button
        {
            Text = "?? Rollback",
            BackgroundColor = Color.FromArgb("#F44336"),
            TextColor = Colors.White,
            FontSize = 16,
            HeightRequest = 54,
            CornerRadius = 8,
            IsVisible = false,
            StyleId = "RollbackButton",
            WidthRequest = 140
        };
        rollbackBtn.Clicked += OnRollbackClicked;
        buttonsLayout.Children.Add(rollbackBtn);

        layout.Children.Add(buttonsLayout);

        // Loading
        var loading = new ActivityIndicator
        {
            IsRunning = false,
            IsVisible = false,
            Color = Color.FromArgb("#2196F3"),
            StyleId = "Loading"
        };
        layout.Children.Add(loading);

        // Status
        var statusLabel = new Label
        {
            Text = "Select a SQL file to begin",
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Colors.Gray,
            StyleId = "StatusLabel"
        };
        layout.Children.Add(statusLabel);

        // Results
        var resultsFrame = new Border
        {
            StrokeThickness = 2,
            Stroke = Color.FromArgb("#4CAF50"),
            Padding = 16,
            IsVisible = false,
            StyleId = "ResultsFrame",
            Margin = new Thickness(0, 16, 0, 0)
        };

        var resultsLayout = new VerticalStackLayout { Spacing = 8 };
        resultsLayout.Children.Add(new Label
        {
            Text = "? Import Results",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        });

        var resultsText = new Label
        {
            Text = "",
            FontFamily = "Courier New",
            FontSize = 12,
            StyleId = "ResultsText"
        };
        resultsLayout.Children.Add(resultsText);

        resultsFrame.Content = resultsLayout;
        layout.Children.Add(resultsFrame);

        // Warnings/Errors
        var warningsFrame = new Border
        {
            StrokeThickness = 2,
            Stroke = Color.FromArgb("#FF9800"),
            Padding = 16,
            IsVisible = false,
            StyleId = "WarningsFrame",
            Margin = new Thickness(0, 16, 0, 0)
        };

        var warningsLayout = new VerticalStackLayout { Spacing = 8 };
        warningsLayout.Children.Add(new Label
        {
            Text = "?? Import Log",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        });

        var warningsScroll = new ScrollView { HeightRequest = 200 };
        var warningsText = new Label
        {
            Text = "",
            FontFamily = "Courier New",
            FontSize = 11,
            StyleId = "WarningsText"
        };
        warningsScroll.Content = warningsText;
        warningsLayout.Children.Add(warningsScroll);

        warningsFrame.Content = warningsLayout;
        layout.Children.Add(warningsFrame);

        Content = new ScrollView { Content = layout };
    }

    private async void OnSelectFileClicked(object? sender, EventArgs e)
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".sql" } },
                    { DevicePlatform.MacCatalyst, new[] { "sql" } },
                    { DevicePlatform.Android, new[] { "*.sql" } },
                    { DevicePlatform.iOS, new[] { "sql" } }
                });

            var options = new PickOptions
            {
                PickerTitle = "Select WDPL SQL dump file",
                FileTypes = customFileType
            };

            var result = await FilePicker.PickAsync(options);

            if (result != null)
            {
                _selectedFilePath = result.FullPath;
                _parsedData = null;
                
                var fileLabel = FindElement<Label>("FileLabel");
                if (fileLabel != null)
                {
                    var fileInfo = new FileInfo(result.FullPath);
                    fileLabel.Text = $"?? {Path.GetFileName(result.FullPath)} ({fileInfo.Length / 1024}KB)";
                }

                var previewBtn = FindElement<Button>("PreviewButton");
                if (previewBtn != null)
                    previewBtn.IsEnabled = true;

                var importBtn = FindElement<Button>("ImportButton");
                if (importBtn != null)
                    importBtn.IsVisible = false;

                var statusLabel = FindElement<Label>("StatusLabel");
                if (statusLabel != null)
                    statusLabel.Text = "Click 'Preview Data' to analyze the SQL file";

                // Hide previous results
                var resultsFrame = FindElement<Border>("ResultsFrame");
                if (resultsFrame != null)
                    resultsFrame.IsVisible = false;

                var warningsFrame = FindElement<Border>("WarningsFrame");
                if (warningsFrame != null)
                    warningsFrame.IsVisible = false;

                var previewFrame = FindElement<Border>("PreviewFrame");
                if (previewFrame != null)
                    previewFrame.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select file: {ex.Message}", "OK");
        }
    }

    private async void OnPreviewClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath))
        {
            await DisplayAlert("No File", "Please select a SQL file first", "OK");
            return;
        }

        try
        {
            var loading = FindElement<ActivityIndicator>("Loading");
            var statusLabel = FindElement<Label>("StatusLabel");
            var previewBtn = FindElement<Button>("PreviewButton");

            if (loading != null)
            {
                loading.IsRunning = true;
                loading.IsVisible = true;
            }

            if (previewBtn != null)
                previewBtn.IsEnabled = false;

            if (statusLabel != null)
                statusLabel.Text = "?? Parsing SQL file...";

            // Parse SQL file
            _parsedData = await SqlFileImporter.ParseSqlFileAsync(_selectedFilePath);

            if (statusLabel != null)
                statusLabel.Text = "? Preview ready - review data before importing";

            // Build preview text
            var preview = new System.Text.StringBuilder();
            preview.AppendLine($"SQL Dialect: {_parsedData.DetectedDialect}");
            preview.AppendLine($"Tables Found: {_parsedData.Tables.Count}");
            preview.AppendLine();

            foreach (var table in _parsedData.Tables.OrderBy(t => t.Key))
            {
                preview.AppendLine($"?? {table.Key}: {table.Value.Count} rows");
                
                // Show sample column names if available
                if (table.Value.Any())
                {
                    var sampleRow = table.Value.First();
                    var columns = string.Join(", ", sampleRow.Keys.Take(5));
                    if (sampleRow.Keys.Count > 5)
                        columns += $", ... ({sampleRow.Keys.Count} total)";
                    preview.AppendLine($"   Columns: {columns}");
                }
            }

            preview.AppendLine();
            preview.AppendLine("Expected Import:");
            
            // Estimate what will be imported
            if (_parsedData.Tables.ContainsKey("tblleague"))
            {
                var leagueData = _parsedData.Tables["tblleague"].FirstOrDefault();
                if (leagueData != null)
                {
                    var seasonName = leagueData.ContainsKey("SeasonName") ? leagueData["SeasonName"] : "Unknown";
                    var seasonYear = leagueData.ContainsKey("SeasonYear") ? leagueData["SeasonYear"] : "????";
                    preview.AppendLine($"• Season: {seasonName} {seasonYear}");
                }
            }

            if (_parsedData.Tables.ContainsKey("tbldivisions"))
                preview.AppendLine($"• Divisions: {_parsedData.Tables["tbldivisions"].Count}");

            if (_parsedData.Tables.ContainsKey("tblfixtures"))
                preview.AppendLine($"• Fixtures: {_parsedData.Tables["tblfixtures"].Count}");

            if (_parsedData.Tables.ContainsKey("tblmatchdetail") || _parsedData.Tables.ContainsKey("tblplayerresult"))
            {
                var detailTable = _parsedData.Tables.ContainsKey("tblmatchdetail") ? 
                    _parsedData.Tables["tblmatchdetail"] : _parsedData.Tables["tblplayerresult"];
                preview.AppendLine($"• Frame Results: {detailTable.Count}");
            }

            preview.AppendLine();
            preview.AppendLine("?? Note: Teams and players will be created");
            preview.AppendLine("   with placeholder names and must be");
            preview.AppendLine("   updated manually after import.");

            // Show preview
            var previewFrame = FindElement<Border>("PreviewFrame");
            var previewText = FindElement<Label>("PreviewText");
            if (previewFrame != null && previewText != null)
            {
                previewFrame.IsVisible = true;
                previewText.Text = preview.ToString();
            }

            // Show import button
            var importBtn = FindElement<Button>("ImportButton");
            if (importBtn != null)
                importBtn.IsVisible = true;
        }
        catch (Exception ex)
        {
            var statusLabel = FindElement<Label>("StatusLabel");
            if (statusLabel != null)
                statusLabel.Text = "? Preview failed";

            await DisplayAlert("Error", $"Failed to parse SQL file:\n\n{ex.Message}", "OK");
        }
        finally
        {
            var loading = FindElement<ActivityIndicator>("Loading");
            var previewBtn = FindElement<Button>("PreviewButton");

            if (loading != null)
            {
                loading.IsRunning = false;
                loading.IsVisible = false;
            }

            if (previewBtn != null)
                previewBtn.IsEnabled = true;
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath) || _parsedData == null)
        {
            await DisplayAlert("No Preview", "Please preview the data first", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Confirm Import",
            $"Import WDPL data from:\n{Path.GetFileName(_selectedFilePath)}?\n\n" +
            "This will create:\n" +
            "• Season (if not exists)\n" +
            "• Divisions\n" +
            "• Teams (with placeholder names)\n" +
            "• Players (with placeholder names)\n" +
            "• Fixtures\n" +
            "• Frame results\n\n" +
            "?? You'll need to update team/player names manually",
            "Import Now",
            "Cancel");

        if (!confirm) return;

        try
        {
            var loading = FindElement<ActivityIndicator>("Loading");
            var statusLabel = FindElement<Label>("StatusLabel");
            var importBtn = FindElement<Button>("ImportButton");

            if (loading != null)
            {
                loading.IsRunning = true;
                loading.IsVisible = true;
            }

            if (importBtn != null)
                importBtn.IsEnabled = false;

            if (statusLabel != null)
                statusLabel.Text = "?? Importing data...";

            // Import SQL file
            var (importedData, result) = await SqlFileImporter.ImportFromSqlFileAsync(
                _selectedFilePath,
                DataStore.Data,
                false);

            _lastImportResult = result;

            if (statusLabel != null)
                statusLabel.Text = "? Import completed";

            // Merge imported data
            DataStore.Data.Divisions.AddRange(importedData.Divisions);
            DataStore.Data.Teams.AddRange(importedData.Teams);
            DataStore.Data.Fixtures.AddRange(importedData.Fixtures);
            // Note: Frames are already part of fixtures, no need to add separately
            
            // Save
            DataStore.Save();

            // Show rollback button
            var rollbackBtn = FindElement<Button>("RollbackButton");
            if (rollbackBtn != null)
                rollbackBtn.IsVisible = true;

            // Show results
            var resultsFrame = FindElement<Border>("ResultsFrame");
            var resultsText = FindElement<Label>("ResultsText");
            if (resultsFrame != null && resultsText != null)
            {
                resultsFrame.IsVisible = true;
                resultsText.Text = 
                    $"SQL Dialect: {result.DetectedDialect}\n\n" +
                    result.Summary;
            }

            // Show warnings/errors
            if (result.Warnings.Any() || result.Errors.Any())
            {
                var warningsFrame = FindElement<Border>("WarningsFrame");
                var warningsText = FindElement<Label>("WarningsText");
                if (warningsFrame != null && warningsText != null)
                {
                    warningsFrame.IsVisible = true;
                    
                    var combined = new List<string>();
                    combined.AddRange(result.Warnings.Select(w => $"?? {w}"));
                    combined.AddRange(result.Errors.Select(e => $"? {e}"));
                    
                    warningsText.Text = string.Join("\n\n", combined);
                }
            }

            if (result.Success)
            {
                await DisplayAlert("Success", 
                    $"Imported successfully!\n\n{result.Summary}\n\n" +
                    $"?? Next steps:\n" +
                    $"1. Update team names in Seasons page\n" +
                    $"2. Update player names in each team\n" +
                    $"3. Activate the season when ready", 
                    "OK");
            }
            else
            {
                await DisplayAlert("Partial Import",
                    $"Import completed with errors.\n\nCheck the log below for details.\n\n" +
                    $"You can use the Rollback button to undo this import.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            var statusLabel = FindElement<Label>("StatusLabel");
            if (statusLabel != null)
                statusLabel.Text = "? Import failed";

            await DisplayAlert("Error", $"Import failed:\n\n{ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"SQL Import Error: {ex}");
        }
        finally
        {
            var loading = FindElement<ActivityIndicator>("Loading");
            var importBtn = FindElement<Button>("ImportButton");

            if (loading != null)
            {
                loading.IsRunning = false;
                loading.IsVisible = false;
            }

            if (importBtn != null)
                importBtn.IsEnabled = true;
        }
    }

    private async void OnRollbackClicked(object? sender, EventArgs e)
    {
        if (_lastImportResult == null)
        {
            await DisplayAlert("No Import", "No recent import to rollback", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Confirm Rollback",
            $"Remove all data from last import:\n\n" +
            $"• {_lastImportResult.ImportedSeasonIds.Count} Seasons\n" +
            $"• {_lastImportResult.ImportedDivisionIds.Count} Divisions\n" +
            $"• {_lastImportResult.TeamsImported} Teams\n" +
            $"• {_lastImportResult.PlayersImported} Players\n" +
            $"• {_lastImportResult.FixturesImported} Fixtures\n" +
            $"• {_lastImportResult.FramesImported} Frames\n\n" +
            "This cannot be undone!",
            "Rollback",
            "Cancel");

        if (!confirm) return;

        try
        {
            SqlFileImporter.RollbackImport(DataStore.Data, _lastImportResult);
            DataStore.Save();

            var rollbackBtn = FindElement<Button>("RollbackButton");
            if (rollbackBtn != null)
                rollbackBtn.IsVisible = false;

            var resultsFrame = FindElement<Border>("ResultsFrame");
            if (resultsFrame != null)
                resultsFrame.IsVisible = false;

            var warningsFrame = FindElement<Border>("WarningsFrame");
            if (warningsFrame != null)
                warningsFrame.IsVisible = false;

            _lastImportResult = null;

            var statusLabel = FindElement<Label>("StatusLabel");
            if (statusLabel != null)
                statusLabel.Text = "? Import rolled back";

            await DisplayAlert("Success", 
                "Import has been rolled back successfully.", 
                "OK");

            await Shell.Current.GoToAsync("//Seasons");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Rollback failed:\n\n{ex.Message}", "OK");
        }
    }

    private T? FindElement<T>(string styleId) where T : Element
    {
        if (Content is ScrollView scroll && scroll.Content is VerticalStackLayout layout)
        {
            return FindInChildren<T>(layout, styleId);
        }
        return null;
    }

    private T? FindInChildren<T>(Layout layout, string styleId) where T : Element
    {
        foreach (var child in layout.Children)
        {
            if (child is T element && element.StyleId == styleId)
                return element;

            if (child is Layout childLayout)
            {
                var found = FindInChildren<T>(childLayout, styleId);
                if (found != null)
                    return found;
            }

            if (child is Border border && border.Content is Layout borderLayout)
            {
                var found = FindInChildren<T>(borderLayout, styleId);
                if (found != null)
                    return found;
            }

            if (child is ScrollView scrollView && scrollView.Content is Layout scrollLayout)
            {
                var found = FindInChildren<T>(scrollLayout, styleId);
                if (found != null)
                    return found;
            }
        }
        return null;
    }
}
