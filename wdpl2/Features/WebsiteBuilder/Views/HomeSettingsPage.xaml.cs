using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class HomeSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public HomeSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        var blocks = settings.GetEffectiveLayoutBlocks();

        // Sync switches from layout block IsEnabled state
        ShowWelcomeSwitch.IsToggled = IsBlockEnabled(blocks, "welcome", settings.HomeShowWelcomeSection);
        ShowQuickStatsSwitch.IsToggled = IsBlockEnabled(blocks, "quick-stats", settings.HomeShowQuickStats);
        ShowLeagueLeadersSwitch.IsToggled = IsBlockEnabled(blocks, "league-leaders", settings.HomeShowLeagueLeaders);
        ShowRecentResultsSwitch.IsToggled = IsBlockEnabled(blocks, "recent-results", settings.HomeShowRecentResults);
        ShowUpcomingFixturesSwitch.IsToggled = IsBlockEnabled(blocks, "upcoming-fixtures", settings.HomeShowUpcomingFixtures);
        ShowLatestNewsSwitch.IsToggled = IsBlockEnabled(blocks, "latest-news", settings.HomeShowLatestNews);
        ShowSponsorsSwitch.IsToggled = IsBlockEnabled(blocks, "sponsors", settings.HomeShowSponsors);

        // Featured pages
        var featured = settings.HomeFeaturedPages;
        FeatStandingsSwitch.IsToggled = featured.Contains("standings");
        FeatEntryFormsSwitch.IsToggled = featured.Contains("entry-forms");
        FeatCompetitionsSwitch.IsToggled = featured.Contains("competitions");
        FeatGallerySwitch.IsToggled = featured.Contains("gallery");
        FeatRulesSwitch.IsToggled = featured.Contains("rules");
        FeatContactSwitch.IsToggled = featured.Contains("contact");
        FeatRowsReportsSwitch.IsToggled = featured.Contains("rows-reports");

        // Counts
        RecentResultsCountEntry.Text = settings.HomeRecentResultsCount.ToString();
        UpcomingFixturesCountEntry.Text = settings.HomeUpcomingFixturesCount.ToString();
        LeagueLeadersCountEntry.Text = settings.HomeLeagueLeadersCount.ToString();
        StatsColumnsEntry.Text = settings.StatsColumns.ToString();
    }

    private static bool IsBlockEnabled(List<LayoutBlock> blocks, string blockType, bool fallback)
    {
        var block = blocks.Find(b => b.BlockType == blockType);
        return block?.IsEnabled ?? fallback;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;

            // Update the HomeShow* model properties
            settings.HomeShowWelcomeSection = ShowWelcomeSwitch.IsToggled;
            settings.HomeShowQuickStats = ShowQuickStatsSwitch.IsToggled;
            settings.HomeShowLeagueLeaders = ShowLeagueLeadersSwitch.IsToggled;
            settings.HomeShowRecentResults = ShowRecentResultsSwitch.IsToggled;
            settings.HomeShowUpcomingFixtures = ShowUpcomingFixturesSwitch.IsToggled;
            settings.HomeShowLatestNews = ShowLatestNewsSwitch.IsToggled;
            settings.HomeShowSponsors = ShowSponsorsSwitch.IsToggled;

            // Build featured pages list from switches
            var featured = new List<string>();
            if (FeatStandingsSwitch.IsToggled) featured.Add("standings");
            if (FeatEntryFormsSwitch.IsToggled) featured.Add("entry-forms");
            if (FeatCompetitionsSwitch.IsToggled) featured.Add("competitions");
            if (FeatGallerySwitch.IsToggled) featured.Add("gallery");
            if (FeatRulesSwitch.IsToggled) featured.Add("rules");
            if (FeatContactSwitch.IsToggled) featured.Add("contact");
            if (FeatRowsReportsSwitch.IsToggled) featured.Add("rows-reports");
            settings.HomeFeaturedPages = featured;

            // Also sync to layout blocks so the drag-drop editor stays consistent
            var blocks = settings.GetEffectiveLayoutBlocks();
            SetBlockEnabled(blocks, "welcome", ShowWelcomeSwitch.IsToggled);
            SetBlockEnabled(blocks, "quick-stats", ShowQuickStatsSwitch.IsToggled);
            SetBlockEnabled(blocks, "league-leaders", ShowLeagueLeadersSwitch.IsToggled);
            SetBlockEnabled(blocks, "recent-results", ShowRecentResultsSwitch.IsToggled);
            SetBlockEnabled(blocks, "upcoming-fixtures", ShowUpcomingFixturesSwitch.IsToggled);
            SetBlockEnabled(blocks, "latest-news", ShowLatestNewsSwitch.IsToggled);
            SetBlockEnabled(blocks, "sponsors", ShowSponsorsSwitch.IsToggled);
            SetBlockEnabled(blocks, "featured-pages", featured.Count > 0);
            settings.HomeLayoutBlocks = blocks;

            // Counts
            if (int.TryParse(RecentResultsCountEntry.Text, out int recentResults) && recentResults > 0)
                settings.HomeRecentResultsCount = recentResults;
            if (int.TryParse(UpcomingFixturesCountEntry.Text, out int upcomingFixtures) && upcomingFixtures > 0)
                settings.HomeUpcomingFixturesCount = upcomingFixtures;
            if (int.TryParse(LeagueLeadersCountEntry.Text, out int leaders) && leaders > 0)
                settings.HomeLeagueLeadersCount = leaders;
            if (int.TryParse(StatsColumnsEntry.Text, out int statsColumns))
                settings.StatsColumns = Math.Clamp(statsColumns, 2, 6);

            DataStore.Save();

            await DisplayAlert("Saved", "Home page settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }

    private static void SetBlockEnabled(List<LayoutBlock> blocks, string blockType, bool enabled)
    {
        var block = blocks.Find(b => b.BlockType == blockType);
        if (block != null)
            block.IsEnabled = enabled;
    }
}
