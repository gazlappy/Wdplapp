using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Services;

namespace Wdpl2.Views
{
    public partial class ImportPage : ContentPage
    {
        private string? _selectedFilePath;

        public ImportPage()
        {
            InitializeComponent();
        }

        private async void OnSelectFileClicked(object sender, EventArgs e)
        {
            try
            {
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".accdb", ".mdb" } },
                        { DevicePlatform.MacCatalyst, new[] { "accdb", "mdb" } },
                        { DevicePlatform.Android, new[] { "application/msaccess", "application/x-msaccess" } },
                        { DevicePlatform.iOS, new[] { "accdb", "mdb" } }
                    });

                var options = new PickOptions
                {
                    PickerTitle = "Select Access Database",
                    FileTypes = customFileType
                };

                var result = await FilePicker.PickAsync(options);

                if (result != null)
                {
                    _selectedFilePath = result.FullPath;
                    SelectedFileLabel.Text = $"Selected: {Path.GetFileName(result.FullPath)}";
                    ImportBtn.IsEnabled = true;
                    StatusLabel.Text = "Ready to import";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to select file: {ex.Message}", "OK");
            }
        }

        private async void OnImportClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                await DisplayAlert("No File", "Please select a database file first.", "OK");
                return;
            }

            var confirm = await DisplayAlert(
                "Confirm Import",
                "This will import all data from the Access database and merge it with your existing data. Continue?",
                "Yes, Import",
                "Cancel");

            if (!confirm) return;

            try
            {
                LoadingIndicator.IsRunning = true;
                LoadingIndicator.IsVisible = true;
                ImportBtn.IsEnabled = false;
                SelectFileBtn.IsEnabled = false;
                StatusLabel.Text = "Importing... Please wait.";
                ResultsBorder.IsVisible = false;
                ErrorsBorder.IsVisible = false;

                // Run import
                var importer = new AccessDatabaseImporter(_selectedFilePath);
                var (importedData, summary) = await importer.ImportAllAsync();

                if (summary.Success)
                {
                    // Merge with existing data
                    await MergeImportedData(importedData);

                    // Show success
                    ResultsBorder.IsVisible = true;
                    ResultsLabel.Text = summary.Summary;
                    StatusLabel.Text = "✅ Import completed successfully!";
                    StatusLabel.TextColor = Colors.Green;

                    await DisplayAlert("Success", "Data imported successfully! The app will refresh.", "OK");

                    // Navigate back or refresh
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    ErrorsBorder.IsVisible = true;
                    ErrorsLabel.Text = string.Join("\n", summary.Errors);
                    StatusLabel.Text = "❌ Import failed";
                    StatusLabel.TextColor = Colors.Red;

                    await DisplayAlert("Import Failed", summary.Message, "OK");
                }
            }
            catch (Exception ex)
            {
                ErrorsBorder.IsVisible = true;
                ErrorsLabel.Text = ex.ToString();
                StatusLabel.Text = "❌ Import failed";
                StatusLabel.TextColor = Colors.Red;

                await DisplayAlert("Error", $"Import failed: {ex.Message}", "OK");
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                ImportBtn.IsEnabled = true;
                SelectFileBtn.IsEnabled = true;
            }
        }

        private async Task MergeImportedData(Models.LeagueData importedData)
        {
            await Task.Run(() =>
            {
                // Merge divisions
                foreach (var div in importedData.Divisions)
                {
                    if (!DataStore.Data.Divisions.Any(d => d.Name.Equals(div.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        DataStore.Data.Divisions.Add(div);
                    }
                }

                // Merge venues
                foreach (var venue in importedData.Venues)
                {
                    if (!DataStore.Data.Venues.Any(v => v.Name.Equals(venue.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        DataStore.Data.Venues.Add(venue);
                    }
                }

                // Merge teams
                foreach (var team in importedData.Teams)
                {
                    if (!DataStore.Data.Teams.Any(t => t.Name != null && t.Name.Equals(team.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        DataStore.Data.Teams.Add(team);
                    }
                }

                // Merge players
                foreach (var player in importedData.Players)
                {
                    var fullName = player.FullName;
                    if (!DataStore.Data.Players.Any(p => p.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase)))
                    {
                        DataStore.Data.Players.Add(player);
                    }
                }

                // Merge seasons
                foreach (var season in importedData.Seasons)
                {
                    if (!DataStore.Data.Seasons.Any(s => s.Name.Equals(season.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        DataStore.Data.Seasons.Add(season);
                    }
                }

                // Add all fixtures (no deduplication needed - each is unique)
                DataStore.Data.Fixtures.AddRange(importedData.Fixtures);

                // Save merged data
                DataStore.Save();
            });
        }
    }
}