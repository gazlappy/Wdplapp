using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class ImportPreviewPage : ContentPage
{
    private ImportPreview? _preview;
    private readonly ObservableCollection<Season> _seasons = new();
    private readonly ObservableCollection<DivisionPreview> _divisions = new();
    private readonly ObservableCollection<TeamPreview> _teams = new();
    private readonly ObservableCollection<PlayerPreview> _players = new();
    private readonly ObservableCollection<CompetitionWinner> _competitions = new();

    public ImportPreviewPage()
    {
        InitializeComponent();
        
        SeasonPicker.ItemsSource = _seasons;
        DivisionsList.ItemsSource = _divisions;
        TeamsList.ItemsSource = _teams;
        PlayersList.ItemsSource = _players;
        CompetitionsList.ItemsSource = _competitions;
    }

    public async Task LoadPreviewAsync(string filePath)
    {
        try
        {
            // Show loading
            ImportButton.IsEnabled = false;
            FileNameLabel.Text = $"File: {System.IO.Path.GetFileName(filePath)}";

            // Extract preview data
            _preview = await ImportPreviewService.ExtractPreviewAsync(filePath, DataStore.Data);

            if (_preview == null || _preview.HasErrors)
            {
                await DisplayAlert("Extraction Failed", 
                    string.Join("\n", _preview?.Errors ?? new System.Collections.Generic.List<string> { "Unknown error" }), 
                    "OK");
                await Navigation.PopAsync();
                return;
            }

            // Load seasons
            LoadSeasons();

            // Display preview data
            DisplayPreview();

            // Show warnings/errors
            ShowAlerts();

            ImportButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load preview: {ex.Message}", "OK");
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

        // Auto-select detected season if found
        if (_preview?.DetectedSeason?.IsExisting == true && 
            _preview.DetectedSeason.ExistingSeasonId.HasValue)
        {
            var detectedSeason = _seasons.FirstOrDefault(s => 
                s.Id == _preview.DetectedSeason.ExistingSeasonId);
            
            if (detectedSeason != null)
            {
                SeasonPicker.SelectedItem = detectedSeason;
                SeasonInfoLabel.Text = $"Detected: {_preview.DetectedSeason.Name}";
                SeasonInfoLabel.IsVisible = true;
            }
        }
        else if (_preview?.DetectedSeason != null)
        {
            SeasonInfoLabel.Text = $"Detected: {_preview.DetectedSeason.Name} (New season will be created)";
            SeasonInfoLabel.IsVisible = true;
        }
    }

    private void DisplayPreview()
    {
        if (_preview == null) return;

        // Update summary
        SummaryLabel.Text = $"Records: {_preview.TotalRecords} | " +
                           $"Divisions: {_preview.Divisions.Count} | " +
                           $"Teams: {_preview.Teams.Count} | " +
                           $"Players: {_preview.Players.Count} | " +
                           $"Competitions: {_preview.Competitions.Count}";

        // Divisions
        if (_preview.Divisions.Any())
        {
            DivisionsBorder.IsVisible = true;
            DivisionsCountLabel.Text = $"({_preview.Divisions.Count})";
            _divisions.Clear();
            foreach (var div in _preview.Divisions)
            {
                _divisions.Add(div);
            }
        }

        // Teams
        if (_preview.Teams.Any())
        {
            TeamsBorder.IsVisible = true;
            TeamsCountLabel.Text = $"({_preview.Teams.Count})";
            _teams.Clear();
            foreach (var team in _preview.Teams)
            {
                _teams.Add(team);
            }
        }

        // Players
        if (_preview.Players.Any())
        {
            PlayersBorder.IsVisible = true;
            PlayersCountLabel.Text = $"({_preview.Players.Count})";
            _players.Clear();
            foreach (var player in _preview.Players)
            {
                _players.Add(player);
            }
        }

        // Competitions
        if (_preview.Competitions.Any())
        {
            CompetitionsBorder.IsVisible = true;
            CompetitionsCountLabel.Text = $"({_preview.Competitions.Count})";
            _competitions.Clear();
            foreach (var comp in _preview.Competitions)
            {
                _competitions.Add(comp);
            }
        }
    }

    private void ShowAlerts()
    {
        if (_preview == null) return;

        AlertsStack.Children.Clear();

        if (_preview.HasErrors || _preview.HasWarnings)
        {
            AlertsBorder.IsVisible = true;

            // Show errors
            foreach (var error in _preview.Errors)
            {
                AlertsStack.Children.Add(new Label
                {
                    Text = $"? {error}",
                    TextColor = Colors.White,
                    FontSize = 12
                });
            }

            // Show warnings
            foreach (var warning in _preview.Warnings)
            {
                AlertsStack.Children.Add(new Label
                {
                    Text = $"?? {warning}",
                    TextColor = Colors.White,
                    FontSize = 12
                });
            }
        }
    }

    private async void OnCreateSeasonClicked(object? sender, EventArgs e)
    {
        var seasonName = await DisplayPromptAsync(
            "New Season",
            "Enter season name:",
            initialValue: _preview?.DetectedSeason?.Name ?? $"Season {DateTime.Now.Year}");

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

    private void OnSelectAllDivisionsClicked(object? sender, EventArgs e)
    {
        var allSelected = _divisions.All(d => d.Include);
        foreach (var division in _divisions)
        {
            division.Include = !allSelected;
        }
        
        // Force UI update
        DivisionsList.ItemsSource = null;
        DivisionsList.ItemsSource = _divisions;
    }

    private void OnSelectAllTeamsClicked(object? sender, EventArgs e)
    {
        var allSelected = _teams.All(t => t.Include);
        foreach (var team in _teams)
        {
            team.Include = !allSelected;
        }
        
        TeamsList.ItemsSource = null;
        TeamsList.ItemsSource = _teams;
    }

    private void OnSelectAllPlayersClicked(object? sender, EventArgs e)
    {
        var allSelected = _players.All(p => p.Include);
        foreach (var player in _players)
        {
            player.Include = !allSelected;
        }
        
        PlayersList.ItemsSource = null;
        PlayersList.ItemsSource = _players;
    }

    private void OnSelectAllCompetitionsClicked(object? sender, EventArgs e)
    {
        var allSelected = _competitions.All(c => c.Include);
        foreach (var comp in _competitions)
        {
            comp.Include = !allSelected;
        }
        
        CompetitionsList.ItemsSource = null;
        CompetitionsList.ItemsSource = _competitions;
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_preview == null)
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

            // Count selected records
            var selectedCount = _divisions.Count(d => d.Include) +
                              _teams.Count(t => t.Include) +
                              _players.Count(p => p.Include) +
                              _competitions.Count(c => c.Include);

            if (selectedCount == 0)
            {
                await DisplayAlert("Nothing Selected", "Please select at least one item to import", "OK");
                return;
            }

            // Confirm import
            var confirm = await DisplayAlert(
                "Confirm Import",
                $"Import {selectedCount} selected record(s) into {selectedSeason.Name}?\n\n" +
                $"This will add new teams, players, and competitions to the database.",
                "Yes, Import",
                "Cancel");

            if (!confirm)
                return;

            // Show loading
            ImportButton.IsEnabled = false;
            ImportButton.Text = "Importing...";

            // Apply import
            var result = await ImportPreviewService.ApplyPreviewAsync(
                _preview,
                selectedSeason.Id,
                DataStore.Data);

            if (result.Success)
            {
                // Save changes
                DataStore.Save();

                await DisplayAlert(
                    "Import Complete!",
                    result.Summary + "\n\n" +
                    $"Divisions: {result.DivisionsCreated}\n" +
                    $"Teams: {result.TeamsCreated}\n" +
                    $"Players: {result.PlayersCreated}\n" +
                    $"Competitions: {result.CompetitionsCreated}\n" +
                    $"Updated: {result.RecordsUpdated}",
                    "OK");

                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlert(
                    "Import Failed",
                    string.Join("\n", result.Errors),
                    "OK");
                
                ImportButton.IsEnabled = true;
                ImportButton.Text = "Import Selected";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Import failed: {ex.Message}", "OK");
            ImportButton.IsEnabled = true;
            ImportButton.Text = "Import Selected";
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Cancel Import",
            "Are you sure you want to cancel? All preview data will be lost.",
            "Yes, Cancel",
            "No");

        if (confirm)
        {
            await Navigation.PopAsync();
        }
    }
}
