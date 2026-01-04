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
        ShowGalleryCheck.IsChecked = settings.ShowGallery;
        ShowTopScorersCheck.IsChecked = settings.ShowTopScorers;
        ShowRecentFormCheck.IsChecked = settings.ShowRecentForm;
        ShowNewsCheck.IsChecked = settings.ShowNews;
        ShowSponsorsCheck.IsChecked = settings.ShowSponsors;
        ShowRulesCheck.IsChecked = settings.ShowRules;
        ShowContactPageCheck.IsChecked = settings.ShowContactPage;
        
        var layouts = (HomeLayoutPicker.ItemsSource as IList<string>)!;
        var layoutIndex = layouts.IndexOf(settings.HomeLayout);
        if (layoutIndex >= 0) HomeLayoutPicker.SelectedIndex = layoutIndex;
        
        HomeShowWelcomeSectionCheck.IsChecked = settings.HomeShowWelcomeSection;
        HomeShowQuickStatsCheck.IsChecked = settings.HomeShowQuickStats;
        HomeShowRecentResultsCheck.IsChecked = settings.HomeShowRecentResults;
        HomeShowUpcomingFixturesCheck.IsChecked = settings.HomeShowUpcomingFixtures;
        HomeShowLeagueLeadersCheck.IsChecked = settings.HomeShowLeagueLeaders;
        HomeShowLatestNewsCheck.IsChecked = settings.HomeShowLatestNews;
        HomeShowSponsorsCheck.IsChecked = settings.HomeShowSponsors;
        
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
            settings.ShowGallery = ShowGalleryCheck.IsChecked;
            settings.ShowTopScorers = ShowTopScorersCheck.IsChecked;
            settings.ShowRecentForm = ShowRecentFormCheck.IsChecked;
            settings.ShowNews = ShowNewsCheck.IsChecked;
            settings.ShowSponsors = ShowSponsorsCheck.IsChecked;
            settings.ShowRules = ShowRulesCheck.IsChecked;
            settings.ShowContactPage = ShowContactPageCheck.IsChecked;
            
            settings.HomeLayout = HomeLayoutPicker.SelectedItem?.ToString() ?? "standard";
            
            settings.HomeShowWelcomeSection = HomeShowWelcomeSectionCheck.IsChecked;
            settings.HomeShowQuickStats = HomeShowQuickStatsCheck.IsChecked;
            settings.HomeShowRecentResults = HomeShowRecentResultsCheck.IsChecked;
            settings.HomeShowUpcomingFixtures = HomeShowUpcomingFixturesCheck.IsChecked;
            settings.HomeShowLeagueLeaders = HomeShowLeagueLeadersCheck.IsChecked;
            settings.HomeShowLatestNews = HomeShowLatestNewsCheck.IsChecked;
            settings.HomeShowSponsors = HomeShowSponsorsCheck.IsChecked;
            
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
