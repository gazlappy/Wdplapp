using System;
using System.Threading.Tasks;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;

namespace Wdpl2.Services;

/// <summary>
/// Local notification service implementation using Plugin.LocalNotification
/// </summary>
public class LocalNotificationService : INotificationService
{
    public async Task<bool> RequestPermissionsAsync()
    {
        try
        {
            if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
            {
                await LocalNotificationCenter.Current.RequestNotificationPermission();
            }
            
            return await LocalNotificationCenter.Current.AreNotificationsEnabled();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification permission error: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> AreNotificationsEnabledAsync()
    {
        try
        {
            return await LocalNotificationCenter.Current.AreNotificationsEnabled();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Check notifications enabled error: {ex.Message}");
            return false;
        }
    }

    public async Task ScheduleNotificationAsync(
        int id, 
        string title, 
        string message, 
        DateTime scheduledTime,
        string? soundFileName = null)
    {
        try
        {
            var notification = new Plugin.LocalNotification.NotificationRequest
            {
                NotificationId = id,
                Title = title,
                Subtitle = message,
                Schedule = new Plugin.LocalNotification.NotificationRequestSchedule
                {
                    NotifyTime = scheduledTime
                },
                Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
                {
                    IconSmallName = new Plugin.LocalNotification.AndroidOption.AndroidIcon("appicon"),
                    Priority = Plugin.LocalNotification.AndroidOption.AndroidPriority.High,
                    VibrationPattern = new long[] { 500, 500 },
                    AutoCancel = true
                }
            };

            await LocalNotificationCenter.Current.Show(notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Schedule notification error: {ex.Message}");
            throw;
        }
    }

    public Task CancelNotificationAsync(int id)
    {
        try
        {
            LocalNotificationCenter.Current.Cancel(id);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cancel notification error: {ex.Message}");
            return Task.CompletedTask;
        }
    }

    public Task CancelAllNotificationsAsync()
    {
        try
        {
            LocalNotificationCenter.Current.ClearAll();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cancel all notifications error: {ex.Message}");
            return Task.CompletedTask;
        }
    }

    public async Task ShowNotificationAsync(
        int id, 
        string title, 
        string message,
        string? soundFileName = null)
    {
        try
        {
            var notification = new Plugin.LocalNotification.NotificationRequest
            {
                NotificationId = id,
                Title = title,
                Subtitle = message,
                Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
                {
                    IconSmallName = new Plugin.LocalNotification.AndroidOption.AndroidIcon("appicon"),
                    Priority = Plugin.LocalNotification.AndroidOption.AndroidPriority.High,
                    VibrationPattern = new long[] { 500, 500 },
                    AutoCancel = true
                }
            };

            await LocalNotificationCenter.Current.Show(notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Show notification error: {ex.Message}");
            throw;
        }
    }

    public async Task<int> GetPendingNotificationCountAsync()
    {
        try
        {
            var pending = await LocalNotificationCenter.Current.GetPendingNotificationList();
            return pending?.Count ?? 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Get pending notifications error: {ex.Message}");
            return 0;
        }
    }
}
