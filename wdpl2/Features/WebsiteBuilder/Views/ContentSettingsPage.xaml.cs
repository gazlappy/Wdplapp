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
        
        ShowStandingsCheck.IsToggled = settings.ShowStandings;
        ShowFixturesCheck.IsToggled = settings.ShowFixtures;
        ShowLiveScoresCheck.IsToggled = settings.ShowLiveScores;
        ShowResultsCheck.IsToggled = settings.ShowResults;
        ShowPlayerStatsCheck.IsToggled = settings.ShowPlayerStats;
        ShowDivisionsCheck.IsToggled = settings.ShowDivisions;
        ShowCompetitionsCheck.IsToggled = settings.ShowCompetitions;
        ShowGalleryCheck.IsToggled = settings.ShowGallery;
        ShowTopScorersCheck.IsToggled = settings.ShowTopScorers;
        ShowRecentFormCheck.IsToggled = settings.ShowRecentForm;
        ShowNewsCheck.IsToggled = settings.ShowNews;
        ShowRowsReportsCheck.IsToggled = settings.ShowRowsReports;
        ShowSponsorsCheck.IsToggled = settings.ShowSponsors;
        ShowRulesCheck.IsToggled = settings.ShowRules;
        ShowContactPageCheck.IsToggled = settings.ShowContactPage;
        ShowPoolGameCheck.IsToggled = settings.ShowPoolGame;
        ShowHistoryCheck.IsToggled = settings.ShowHistory;

        HomeRecentResultsCountEntry.Text = settings.HomeRecentResultsCount.ToString();
        HomeUpcomingFixturesCountEntry.Text = settings.HomeUpcomingFixturesCount.ToString();
        HomeLeagueLeadersCountEntry.Text = settings.HomeLeagueLeadersCount.ToString();
        StatsColumnsEntry.Text = settings.StatsColumns.ToString();

        HomeWelcomeTitleEntry.Text = settings.HomeWelcomeTitle;
        StandingsPageTitleEntry.Text = settings.StandingsPageTitle;
        FixturesPageTitleEntry.Text = settings.FixturesPageTitle;
        ResultsPageTitleEntry.Text = settings.ResultsPageTitle;
        PlayersPageTitleEntry.Text = settings.PlayersPageTitle;
        DivisionsPageTitleEntry.Text = settings.DivisionsPageTitle;
        CompetitionsPageTitleEntry.Text = settings.CompetitionsPageTitle;
        GalleryPageTitleEntry.Text = settings.GalleryPageTitle;
        NewsPageTitleEntry.Text = settings.NewsPageTitle;
        RowsReportsPageTitleEntry.Text = settings.RowsReportsPageTitle;
        SponsorsPageTitleEntry.Text = settings.SponsorsPageTitle;
        RulesPageTitleEntry.Text = settings.RulesPageTitle;
        ContactPageTitleEntry.Text = settings.ContactPageTitle;
        EntryFormsPageTitleEntry.Text = settings.EntryFormsPageTitle;

        HomeNavLabelEntry.Text = settings.HomeNavLabel;
        StandingsNavLabelEntry.Text = settings.StandingsNavLabel;
        FixturesNavLabelEntry.Text = settings.FixturesNavLabel;
        ResultsNavLabelEntry.Text = settings.ResultsNavLabel;
        PlayersNavLabelEntry.Text = settings.PlayersNavLabel;
        DivisionsNavLabelEntry.Text = settings.DivisionsNavLabel;
        CompetitionsNavLabelEntry.Text = settings.CompetitionsNavLabel;
        PoolGameNavLabelEntry.Text = settings.PoolGameNavLabel;
        GalleryNavLabelEntry.Text = settings.GalleryNavLabel;
        NewsNavLabelEntry.Text = settings.NewsNavLabel;
        RowsReportsNavLabelEntry.Text = settings.RowsReportsNavLabel;
        SponsorsNavLabelEntry.Text = settings.SponsorsNavLabel;
        RulesNavLabelEntry.Text = settings.RulesNavLabel;
        EntryFormsNavLabelEntry.Text = settings.EntryFormsNavLabel;
        ContactNavLabelEntry.Text = settings.ContactNavLabel;
        HistoryNavLabelEntry.Text = settings.HistoryNavLabel;
        HistoryPageTitleEntry.Text = settings.HistoryPageTitle;
    }

    // Navigation to page-specific settings
    private async void OnStandingsSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new StandingsSettingsPage());
    
    private async void OnFixturesSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new FixturesSettingsPage());
    
    private async void OnLiveScoresSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new LiveScoresSettingsPage());

    private async void OnResultsSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new ResultsSettingsPage());
    
    private async void OnPlayersSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new PlayersSettingsPage());
    
    private async void OnDivisionsSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new DivisionsSettingsPage());

    private async void OnCompetitionsSettingsClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new CompetitionsSettingsPage());

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.ShowStandings = ShowStandingsCheck.IsToggled;
            settings.ShowFixtures = ShowFixturesCheck.IsToggled;
            settings.ShowLiveScores = ShowLiveScoresCheck.IsToggled;
            settings.ShowResults = ShowResultsCheck.IsToggled;
            settings.ShowPlayerStats = ShowPlayerStatsCheck.IsToggled;
            settings.ShowDivisions = ShowDivisionsCheck.IsToggled;
            settings.ShowCompetitions = ShowCompetitionsCheck.IsToggled;
            settings.ShowGallery = ShowGalleryCheck.IsToggled;
            settings.ShowTopScorers = ShowTopScorersCheck.IsToggled;
            settings.ShowRecentForm = ShowRecentFormCheck.IsToggled;
            settings.ShowNews = ShowNewsCheck.IsToggled;
            settings.ShowRowsReports = ShowRowsReportsCheck.IsToggled;
            settings.ShowSponsors = ShowSponsorsCheck.IsToggled;
            settings.ShowRules = ShowRulesCheck.IsToggled;
            settings.ShowContactPage = ShowContactPageCheck.IsToggled;
            settings.ShowPoolGame = ShowPoolGameCheck.IsToggled;
            settings.ShowHistory = ShowHistoryCheck.IsToggled;

            if (int.TryParse(HomeRecentResultsCountEntry.Text, out int recentResults))
                settings.HomeRecentResultsCount = recentResults;
            if (int.TryParse(HomeUpcomingFixturesCountEntry.Text, out int upcomingFixtures))
                settings.HomeUpcomingFixturesCount = upcomingFixtures;
            if (int.TryParse(HomeLeagueLeadersCountEntry.Text, out int leagueLeaders))
                settings.HomeLeagueLeadersCount = leagueLeaders;
            if (int.TryParse(StatsColumnsEntry.Text, out int statsColumns))
                settings.StatsColumns = Math.Clamp(statsColumns, 2, 6);

            if (!string.IsNullOrWhiteSpace(HomeWelcomeTitleEntry.Text))
                settings.HomeWelcomeTitle = HomeWelcomeTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(StandingsPageTitleEntry.Text))
                settings.StandingsPageTitle = StandingsPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(FixturesPageTitleEntry.Text))
                settings.FixturesPageTitle = FixturesPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(ResultsPageTitleEntry.Text))
                settings.ResultsPageTitle = ResultsPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(PlayersPageTitleEntry.Text))
                settings.PlayersPageTitle = PlayersPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(DivisionsPageTitleEntry.Text))
                settings.DivisionsPageTitle = DivisionsPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(CompetitionsPageTitleEntry.Text))
                settings.CompetitionsPageTitle = CompetitionsPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(GalleryPageTitleEntry.Text))
                settings.GalleryPageTitle = GalleryPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(NewsPageTitleEntry.Text))
                settings.NewsPageTitle = NewsPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(RowsReportsPageTitleEntry.Text))
                settings.RowsReportsPageTitle = RowsReportsPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(SponsorsPageTitleEntry.Text))
                settings.SponsorsPageTitle = SponsorsPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(RulesPageTitleEntry.Text))
                settings.RulesPageTitle = RulesPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(ContactPageTitleEntry.Text))
                settings.ContactPageTitle = ContactPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(EntryFormsPageTitleEntry.Text))
                settings.EntryFormsPageTitle = EntryFormsPageTitleEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(HistoryPageTitleEntry.Text))
                settings.HistoryPageTitle = HistoryPageTitleEntry.Text.Trim();

            if (!string.IsNullOrWhiteSpace(HomeNavLabelEntry.Text))
                settings.HomeNavLabel = HomeNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(StandingsNavLabelEntry.Text))
                settings.StandingsNavLabel = StandingsNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(FixturesNavLabelEntry.Text))
                settings.FixturesNavLabel = FixturesNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(ResultsNavLabelEntry.Text))
                settings.ResultsNavLabel = ResultsNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(PlayersNavLabelEntry.Text))
                settings.PlayersNavLabel = PlayersNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(DivisionsNavLabelEntry.Text))
                settings.DivisionsNavLabel = DivisionsNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(CompetitionsNavLabelEntry.Text))
                settings.CompetitionsNavLabel = CompetitionsNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(PoolGameNavLabelEntry.Text))
                settings.PoolGameNavLabel = PoolGameNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(GalleryNavLabelEntry.Text))
                settings.GalleryNavLabel = GalleryNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(NewsNavLabelEntry.Text))
                settings.NewsNavLabel = NewsNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(RowsReportsNavLabelEntry.Text))
                settings.RowsReportsNavLabel = RowsReportsNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(SponsorsNavLabelEntry.Text))
                settings.SponsorsNavLabel = SponsorsNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(RulesNavLabelEntry.Text))
                settings.RulesNavLabel = RulesNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(EntryFormsNavLabelEntry.Text))
                settings.EntryFormsNavLabel = EntryFormsNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(ContactNavLabelEntry.Text))
                settings.ContactNavLabel = ContactNavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(HistoryNavLabelEntry.Text))
                settings.HistoryNavLabel = HistoryNavLabelEntry.Text.Trim();

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
