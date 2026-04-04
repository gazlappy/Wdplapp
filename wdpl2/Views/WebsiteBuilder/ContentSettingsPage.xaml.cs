using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class ContentSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public ContentSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        ShowStandingsCheck.IsChecked = settings.ShowStandings;
        ShowFixturesCheck.IsChecked = settings.ShowFixtures;
        ShowResultsCheck.IsChecked = settings.ShowResults;
        ShowPlayerStatsCheck.IsChecked = settings.ShowPlayerStats;
        ShowDivisionsCheck.IsChecked = settings.ShowDivisions;
        ShowCompetitionsCheck.IsChecked = settings.ShowCompetitions;
        ShowGalleryCheck.IsChecked = settings.ShowGallery;
        ShowTopScorersCheck.IsChecked = settings.ShowTopScorers;
        ShowRecentFormCheck.IsChecked = settings.ShowRecentForm;
        ShowNewsCheck.IsChecked = settings.ShowNews;
        ShowRowsReportsCheck.IsChecked = settings.ShowRowsReports;
        ShowSponsorsCheck.IsChecked = settings.ShowSponsors;
        ShowRulesCheck.IsChecked = settings.ShowRules;
        ShowContactPageCheck.IsChecked = settings.ShowContactPage;
        ShowPoolGameCheck.IsChecked = settings.ShowPoolGame;

        HomeRecentResultsCountEntry.Text = settings.HomeRecentResultsCount.ToString();
        HomeUpcomingFixturesCountEntry.Text = settings.HomeUpcomingFixturesCount.ToString();
        HomeLeagueLeadersCountEntry.Text = settings.HomeLeagueLeadersCount.ToString();
        StatsColumnsEntry.Text = settings.StatsColumns.ToString();
    }

    // Navigation to page-specific settings
    private async void OnStandingsSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new StandingsSettingsPage());
    
    private async void OnFixturesSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new FixturesSettingsPage());
    
    private async void OnResultsSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new ResultsSettingsPage());
    
    private async void OnPlayersSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new PlayersSettingsPage());
    
    private async void OnDivisionsSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new DivisionsSettingsPage());

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.ShowStandings = ShowStandingsCheck.IsChecked;
            settings.ShowFixtures = ShowFixturesCheck.IsChecked;
            settings.ShowResults = ShowResultsCheck.IsChecked;
            settings.ShowPlayerStats = ShowPlayerStatsCheck.IsChecked;
            settings.ShowDivisions = ShowDivisionsCheck.IsChecked;
            settings.ShowCompetitions = ShowCompetitionsCheck.IsChecked;
            settings.ShowGallery = ShowGalleryCheck.IsChecked;
            settings.ShowTopScorers = ShowTopScorersCheck.IsChecked;
            settings.ShowRecentForm = ShowRecentFormCheck.IsChecked;
            settings.ShowNews = ShowNewsCheck.IsChecked;
            settings.ShowRowsReports = ShowRowsReportsCheck.IsChecked;
            settings.ShowSponsors = ShowSponsorsCheck.IsChecked;
            settings.ShowRules = ShowRulesCheck.IsChecked;
            settings.ShowContactPage = ShowContactPageCheck.IsChecked;
            settings.ShowPoolGame = ShowPoolGameCheck.IsChecked;

            if (int.TryParse(HomeRecentResultsCountEntry.Text, out int recentResults))
                settings.HomeRecentResultsCount = recentResults;
            if (int.TryParse(HomeUpcomingFixturesCountEntry.Text, out int upcomingFixtures))
                settings.HomeUpcomingFixturesCount = upcomingFixtures;
            if (int.TryParse(HomeLeagueLeadersCountEntry.Text, out int leagueLeaders))
                settings.HomeLeagueLeadersCount = leagueLeaders;
            if (int.TryParse(StatsColumnsEntry.Text, out int statsColumns))
                settings.StatsColumns = Math.Clamp(statsColumns, 2, 6);
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Content settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
