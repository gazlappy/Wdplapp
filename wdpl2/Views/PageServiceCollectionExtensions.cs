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
        services.AddTransient<FixturesPage>();
        services.AddTransient<LeagueTablesPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<SearchPage>();
        services.AddTransient<SqlImportPage>();

        return services;
    }
}
