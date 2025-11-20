using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class ImportHistoricalDataPage : ContentPage
{
    private readonly ObservableCollection<SeasonCopyService.HistoricalDivision> _divisions = new();
    private readonly ObservableCollection<SeasonCopyService.HistoricalVenue> _venues = new();
    private readonly ObservableCollection<SeasonCopyService.HistoricalTeam> _teams = new();
    private readonly ObservableCollection<SeasonCopyService.HistoricalPlayer> _players = new();

    private Guid? _targetSeasonId;
    private string _currentTab = "Divisions";

    public ImportHistoricalDataPage(Guid? targetSeasonId = null)
    {
        InitializeComponent();

        _targetSeasonId = targetSeasonId ?? SeasonService.CurrentSeasonId;

        DivisionsList.ItemsSource = _divisions;
        VenuesList.ItemsSource = _venues;
        TeamsList.ItemsSource = _teams;
        PlayersList.ItemsSource = _players;

        LoadData();
    }

    private void LoadData()
    {
        if (!_targetSeasonId.HasValue)
        {
            SeasonLabel.Text = "?? No season selected. Please select a season first.";
            ImportBtn.IsEnabled = false;
            return;
        }

        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _targetSeasonId);
        SeasonLabel.Text = $"Target Season: {season?.Name ?? "Unknown"}";

        // Load historical data
        var historicalDivisions = SeasonCopyService.GetHistoricalDivisions(DataStore.Data, _targetSeasonId.Value);
        var historicalVenues = SeasonCopyService.GetHistoricalVenues(DataStore.Data, _targetSeasonId.Value);
        var historicalTeams = SeasonCopyService.GetHistoricalTeams(DataStore.Data, _targetSeasonId.Value);
        var historicalPlayers = SeasonCopyService.GetHistoricalPlayers(DataStore.Data, _targetSeasonId.Value);

        _divisions.Clear();
        foreach (var div in historicalDivisions)
            _divisions.Add(div);

        _venues.Clear();
        foreach (var venue in historicalVenues)
            _venues.Add(venue);

        _teams.Clear();
        foreach (var team in historicalTeams)
            _teams.Add(team);

        _players.Clear();
        foreach (var player in historicalPlayers)
            _players.Add(player);

        UpdateStatusLabel();
    }

    // ========== TAB SWITCHING ==========

    private void OnDivisionsTabClicked(object? sender, EventArgs e)
    {
        _currentTab = "Divisions";
        UpdateTabs();
    }

    private void OnVenuesTabClicked(object? sender, EventArgs e)
    {
        _currentTab = "Venues";
        UpdateTabs();
    }

    private void OnTeamsTabClicked(object? sender, EventArgs e)
    {
        _currentTab = "Teams";
        UpdateTabs();
    }

    private void OnPlayersTabClicked(object? sender, EventArgs e)
    {
        _currentTab = "Players";
        UpdateTabs();
    }

    private void UpdateTabs()
    {
        // Get styles from Application resources (safer than page-level Resources)
        var primaryStyle = (Style)Application.Current.Resources["PrimaryButtonStyle"];
        var secondaryStyle = (Style)Application.Current.Resources["SecondaryButtonStyle"];
        
        // Update button styles
        DivisionsTabBtn.Style = _currentTab == "Divisions" ? primaryStyle : secondaryStyle;
        VenuesTabBtn.Style = _currentTab == "Venues" ? primaryStyle : secondaryStyle;
        TeamsTabBtn.Style = _currentTab == "Teams" ? primaryStyle : secondaryStyle;
        PlayersTabBtn.Style = _currentTab == "Players" ? primaryStyle : secondaryStyle;

        // Show/hide tabs
        DivisionsTab.IsVisible = _currentTab == "Divisions";
        VenuesTab.IsVisible = _currentTab == "Venues";
        TeamsTab.IsVisible = _currentTab == "Teams";
        PlayersTab.IsVisible = _currentTab == "Players";
    }

    // ========== SELECT ALL / CLEAR ==========

    private void OnSelectAllDivisions(object? sender, EventArgs e)
    {
        foreach (var div in _divisions)
            div.IsSelected = true;
        
        // Force UI refresh
        DivisionsList.ItemsSource = null;
        DivisionsList.ItemsSource = _divisions;
        UpdateStatusLabel();
    }

    private void OnClearAllDivisions(object? sender, EventArgs e)
    {
        foreach (var div in _divisions)
            div.IsSelected = false;
        
        DivisionsList.ItemsSource = null;
        DivisionsList.ItemsSource = _divisions;
        UpdateStatusLabel();
    }

    private void OnSelectAllVenues(object? sender, EventArgs e)
    {
        foreach (var venue in _venues)
            venue.IsSelected = true;
        
        VenuesList.ItemsSource = null;
        VenuesList.ItemsSource = _venues;
        UpdateStatusLabel();
    }

    private void OnClearAllVenues(object? sender, EventArgs e)
    {
        foreach (var venue in _venues)
            venue.IsSelected = false;
        
        VenuesList.ItemsSource = null;
        VenuesList.ItemsSource = _venues;
        UpdateStatusLabel();
    }

    private void OnSelectAllTeams(object? sender, EventArgs e)
    {
        foreach (var team in _teams)
            team.IsSelected = true;
        
        TeamsList.ItemsSource = null;
        TeamsList.ItemsSource = _teams;
        UpdateStatusLabel();
    }

    private void OnClearAllTeams(object? sender, EventArgs e)
    {
        foreach (var team in _teams)
            team.IsSelected = false;
        
        TeamsList.ItemsSource = null;
        TeamsList.ItemsSource = _teams;
        UpdateStatusLabel();
    }

    private void OnSelectAllPlayers(object? sender, EventArgs e)
    {
        foreach (var player in _players)
            player.IsSelected = true;
        
        PlayersList.ItemsSource = null;
        PlayersList.ItemsSource = _players;
        UpdateStatusLabel();
    }

    private void OnClearAllPlayers(object? sender, EventArgs e)
    {
        foreach (var player in _players)
            player.IsSelected = false;
        
        PlayersList.ItemsSource = null;
        PlayersList.ItemsSource = _players;
        UpdateStatusLabel();
    }

    // ========== IMPORT ==========

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        if (!_targetSeasonId.HasValue)
        {
            await DisplayAlert("Error", "No target season selected.", "OK");
            return;
        }

        var divCount = _divisions.Count(d => d.IsSelected);
        var venCount = _venues.Count(v => v.IsSelected);
        var teamCount = _teams.Count(t => t.IsSelected);
        var playerCount = _players.Count(p => p.IsSelected);

        var total = divCount + venCount + teamCount + playerCount;

        if (total == 0)
        {
            await DisplayAlert("Nothing Selected", "Please select at least one item to import.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Import Confirmation",
            $"Import the following to this season?\n\n" +
            $"• {divCount} Division(s)\n" +
            $"• {venCount} Venue(s)\n" +
            $"• {teamCount} Team(s)\n" +
            $"• {playerCount} Player(s)\n\n" +
            $"Duplicates will be skipped automatically.",
            "Import",
            "Cancel");

        if (!confirm) return;

        try
        {
            var result = SeasonCopyService.CopyAllToSeason(
                DataStore.Data,
                _divisions.ToList(),
                _venues.ToList(),
                _teams.ToList(),
                _players.ToList(),
                _targetSeasonId.Value);

            DataStore.Save();

            var message = $"Successfully imported:\n\n" +
                         $"• {result.divisions} Division(s)\n" +
                         $"• {result.venues} Venue(s)\n" +
                         $"• {result.teams} Team(s)\n" +
                         $"• {result.players} Player(s)";

            var skipped = total - (result.divisions + result.venues + result.teams + result.players);
            if (skipped > 0)
                message += $"\n\n{skipped} item(s) skipped (already exist in target season)";

            await DisplayAlert("Import Complete", message, "OK");

            // Reload data to remove imported items
            LoadData();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Import failed: {ex.Message}", "OK");
        }
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private void UpdateStatusLabel()
    {
        var divCount = _divisions.Count(d => d.IsSelected);
        var venCount = _venues.Count(v => v.IsSelected);
        var teamCount = _teams.Count(t => t.IsSelected);
        var playerCount = _players.Count(p => p.IsSelected);

        var total = divCount + venCount + teamCount + playerCount;

        if (total == 0)
        {
            StatusLabel.Text = "Select items to import";
        }
        else
        {
            StatusLabel.Text = $"Selected: {divCount} div, {venCount} venues, {teamCount} teams, {playerCount} players ({total} total)";
        }
    }
}
