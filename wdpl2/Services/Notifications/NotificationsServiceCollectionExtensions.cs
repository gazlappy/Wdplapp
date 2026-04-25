using Microsoft.Extensions.DependencyInjection;

namespace Wdpl2.Services;

/// <summary>
/// DI registrations for local notifications and match reminders.
/// </summary>
public static class NotificationsServiceCollectionExtensions
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddSingleton<INotificationService, LocalNotificationService>();
        services.AddSingleton<MatchReminderService>();

        return services;
    }
}
