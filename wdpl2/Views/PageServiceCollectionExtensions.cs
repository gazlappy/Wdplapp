using Microsoft.Extensions.DependencyInjection;

namespace Wdpl2.Views;

/// <summary>
/// DI registrations for Pages constructed via dependency injection.
/// (Pages also navigated via Shell routes are registered in <see cref="AppShell"/>.)
/// </summary>
public static class PageServiceCollectionExtensions
{
    public static IServiceCollection AddPages(this IServiceCollection services)
    {
        services.AddTransient<DashboardPage>();
        services.AddTransient<CompetitionsPage>();
        services.AddTransient<VenuesPage>();
        services.AddTransient<DivisionsPage>();
        services.AddTransient<PlayersPage>();
        services.AddTransient<TeamsPage>();
        services.AddTransient<SeasonsPage>();
        services.AddTransient<SeasonSetupPage>();
        services.AddTransient<FixturesPage>();
        services.AddTransient<LeagueTablesPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<SearchPage>();
        services.AddTransient<SqlImportPage>();
        services.AddTransient<PlayerResultsPage>();
        services.AddTransient<SeasonComparisonPage>();
        services.AddTransient<CalendarOptionsPage>();
        services.AddTransient<SeasonScenarioPage>();
        services.AddTransient<CareerStatsPage>();
        services.AddTransient<MatchDayDashboardPage>();
        services.AddTransient<SmartImportPage>();
        services.AddTransient<Logos.LogosHubPage>();
        services.AddTransient<SeasonAwardsPage>();
        services.AddTransient<TeamAnalyticsPage>();
        services.AddTransient<FrameStatsPage>();
        services.AddTransient<AchievementsPage>();
        services.AddTransient<ImportHistoricalDataPage>();
        services.AddTransient<BatchImportPreviewPage>();
        services.AddTransient<CalendarPage>();
        services.AddTransient<Inbox.InboxPage>();

        return services;
    }
}
