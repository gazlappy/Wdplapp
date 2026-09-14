using Microsoft.Extensions.DependencyInjection;
using Wdpl2.Services.Web;

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
        services.AddWebPlatform();

        return services;
    }

    /// <summary>
    /// The online backend. Registering an <see cref="IWebModule"/> here is all
    /// that is needed for it to appear on the Web Control tab and be included in
    /// the next deploy.
    /// </summary>
    private static IServiceCollection AddWebPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IWebModule, SystemWebModule>();

        services.AddSingleton<WebModuleRegistry>();
        services.AddSingleton<WebDeployService>();

        return services;
    }
}
