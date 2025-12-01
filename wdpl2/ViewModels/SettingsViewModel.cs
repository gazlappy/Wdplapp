using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.ViewModels;

/// <summary>
/// ViewModel for SettingsPage - manages app settings
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IDataStore _dataStore;
    private readonly INotificationService? _notificationService;
    private readonly MatchReminderService? _matchReminderService;
    
    [ObservableProperty]
    private string _statusMessage = "";
    
    [ObservableProperty]
    private bool _darkMode;
    
    [ObservableProperty]
    private string _defaultVenue = "";
    
    [ObservableProperty]
    private int _framesPerMatch = 8;
    
    [ObservableProperty]
    private bool _autoSave = true;
    
    // Notification Settings (NEW)
    [ObservableProperty]
    private bool _notificationsEnabled;
    
    [ObservableProperty]
    private bool _matchRemindersEnabled = true;
    
    [ObservableProperty]
    private int _reminderHours = 2;
    
    [ObservableProperty]
    private int _selectedReminderHoursIndex = 1; // Index for picker (0=1hr, 1=2hrs, etc.)
    
    [ObservableProperty]
    private bool _resultNotificationsEnabled = false;
    
    [ObservableProperty]
    private bool _weeklyFixtureListEnabled = false;
    
    [ObservableProperty]
    private int _pendingNotifications;
    
    // NEW: Available reminder hour options
    public List<int> ReminderHourOptions { get; } = new() { 1, 2, 4, 6, 12, 24 };

    public SettingsViewModel(
        IDataStore dataStore, 
        INotificationService? notificationService = null,
        MatchReminderService? matchReminderService = null)
    {
        _dataStore = dataStore;
        _notificationService = notificationService;
        _matchReminderService = matchReminderService;
        
        LoadSettings();
        _ = LoadNotificationStatusAsync();
    }

    private void LoadSettings()
    {
        var settings = _dataStore.GetData().Settings;
        if (settings != null)
        {
            // Load notification settings (Phase 3)
            _matchRemindersEnabled = settings.MatchRemindersEnabled;
            _reminderHours = settings.ReminderHoursBefore;
            _selectedReminderHoursIndex = ReminderHourOptions.IndexOf(_reminderHours);
            if (_selectedReminderHoursIndex < 0) _selectedReminderHoursIndex = 1; // Default to 2 hours
            _resultNotificationsEnabled = settings.ResultNotificationsEnabled;
            _weeklyFixtureListEnabled = settings.WeeklyFixtureListEnabled;
            
            // Load other settings if they exist
            _framesPerMatch = 8; // Default value
            _autoSave = true;
        }
    }
    
    private async Task LoadNotificationStatusAsync()
    {
        if (_notificationService != null)
        {
            _notificationsEnabled = await _notificationService.AreNotificationsEnabledAsync();
            _pendingNotifications = await _notificationService.GetPendingNotificationCountAsync();
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            // Save settings logic here
            await _dataStore.SaveAsync();
            SetStatus("Settings saved");
        }
        catch (System.Exception ex)
        {
            SetStatus($"Error saving settings: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        _darkMode = false;
        _defaultVenue = "";
        _framesPerMatch = 8;
        _autoSave = true;
        
        await SaveSettingsAsync();
        SetStatus("Settings reset to defaults");
    }

    [RelayCommand]
    private async Task ClearAllDataAsync()
    {
        // This would need confirmation dialog in the view
        SetStatus("Clear all data requested");
    }
    
    // Notification Commands (NEW)
    [RelayCommand]
    private async Task RequestNotificationPermissionsAsync()
    {
        if (_notificationService != null)
        {
            var granted = await _notificationService.RequestPermissionsAsync();
            _notificationsEnabled = granted;
            
            if (granted)
            {
                SetStatus("? Notifications enabled");
            }
            else
            {
                SetStatus("? Notifications permission denied");
            }
        }
    }
    
    [RelayCommand]
    private async Task TestNotificationAsync()
    {
        if (_notificationService != null)
        {
            try
            {
                await _notificationService.ShowNotificationAsync(
                    id: 99999,
                    title: "?? Test Notification",
                    message: "Notifications are working! You'll get match reminders.");
                
                SetStatus("? Test notification sent");
            }
            catch (System.Exception ex)
            {
                SetStatus($"? Test failed: {ex.Message}");
            }
        }
    }
    
    [RelayCommand]
    private async Task CancelAllNotificationsAsync()
    {
        if (_matchReminderService != null)
        {
            try
            {
                await _matchReminderService.CancelAllMatchRemindersAsync();
                _pendingNotifications = 0;
                SetStatus("? All notifications cancelled");
            }
            catch (System.Exception ex)
            {
                SetStatus($"? Cancel failed: {ex.Message}");
            }
        }
    }
    
    [RelayCommand]
    private async Task RefreshNotificationStatusAsync()
    {
        await LoadNotificationStatusAsync();
        SetStatus($"? {_pendingNotifications} pending notifications");
    }

    private void SetStatus(string message)
    {
        _statusMessage = $"{System.DateTime.Now:HH:mm:ss}  {message}";
    }
}
