using Microsoft.Extensions.DependencyInjection;
using Wdpl2.Services.Inbox;

namespace Wdpl2.Services;

/// <summary>
/// DI registrations for cross-cutting application services
/// (season management, theming, etc.).
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCoreAppServices(this IServiceCollection services)
    {
        services.AddSingleton<ISeasonService, SeasonService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IWebInboxService, HttpWebInboxService>();
        services.AddSingleton<IWebPublishService, HttpWebPublishService>();

        return services;
    }
}
