using System;
using System.Threading.Tasks;

namespace Wdpl2.Services;

/// <summary>
/// Interface for local notification service
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Request notification permissions from the user
    /// </summary>
    Task<bool> RequestPermissionsAsync();
    
    /// <summary>
    /// Check if notifications are currently enabled
    /// </summary>
    Task<bool> AreNotificationsEnabledAsync();
    
    /// <summary>
    /// Schedule a notification for a specific date/time
    /// </summary>
    Task ScheduleNotificationAsync(
        int id,
        string title,
        string message,
        DateTime scheduledTime,
        string? soundFileName = null);
    
    /// <summary>
    /// Cancel a scheduled notification
    /// </summary>
    Task CancelNotificationAsync(int id);
    
    /// <summary>
    /// Cancel all scheduled notifications
    /// </summary>
    Task CancelAllNotificationsAsync();
    
    /// <summary>
    /// Show an immediate notification
    /// </summary>
    Task ShowNotificationAsync(
        int id,
        string title,
        string message,
        string? soundFileName = null);
    
    /// <summary>
    /// Get count of pending notifications
    /// </summary>
    Task<int> GetPendingNotificationCountAsync();
}

/// <summary>
/// Notification data for scheduled notifications
/// </summary>
public class NotificationRequest
{
    public int NotificationId { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime ScheduledTime { get; set; }
    public string? SoundFileName { get; set; }
}
