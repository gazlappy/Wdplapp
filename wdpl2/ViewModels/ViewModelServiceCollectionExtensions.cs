using Microsoft.Extensions.DependencyInjection;
using Wdpl2.ViewModels.Inbox;

namespace Wdpl2.ViewModels;

/// <summary>
/// DI registrations for ViewModels.
/// </summary>
public static class ViewModelServiceCollectionExtensions
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<CompetitionsViewModel>();
        services.AddTransient<VenuesViewModel>();
        services.AddTransient<DivisionsViewModel>();
        services.AddTransient<PlayersViewModel>();
        services.AddTransient<TeamsViewModel>();
        services.AddTransient<SeasonsViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
