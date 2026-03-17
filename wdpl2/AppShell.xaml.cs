namespace Wdpl2;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for programmatic navigation
        Routing.RegisterRoute("import", typeof(Views.ImportPage));
        Routing.RegisterRoute("careerstats", typeof(Views.CareerStatsPage));
        Routing.RegisterRoute("framestats", typeof(Views.FrameStatsPage));
        Routing.RegisterRoute("achievements", typeof(Views.AchievementsPage));
        Routing.RegisterRoute("seasonawards", typeof(Views.SeasonAwardsPage));
        Routing.RegisterRoute("matchday", typeof(Views.MatchDayDashboardPage));
        Routing.RegisterRoute("teamanalytics", typeof(Views.TeamAnalyticsPage));
        Routing.RegisterRoute("whatif", typeof(Views.WhatIfSimulatorPage));
        Routing.RegisterRoute("playerprofile", typeof(Views.PlayerProfilePage));
        Routing.RegisterRoute("playerresults", typeof(Views.PlayerResultsPage));
        Routing.RegisterRoute("seasonsetup", typeof(Views.SeasonSetupPage));
        Routing.RegisterRoute("search", typeof(Views.SearchPage));
    }
}
