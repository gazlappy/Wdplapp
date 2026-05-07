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
    private bool _useSystemTheme = true;
    
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
            // Load theme settings
            DarkMode = settings.DarkModeEnabled;
            UseSystemTheme = settings.UseSystemTheme;

            // Load notification settings (Phase 3)
            MatchRemindersEnabled = settings.MatchRemindersEnabled;
            ReminderHours = settings.ReminderHoursBefore;
            var idx = ReminderHourOptions.IndexOf(ReminderHours);
            SelectedReminderHoursIndex = idx < 0 ? 1 : idx; // Default to 2 hours
            ResultNotificationsEnabled = settings.ResultNotificationsEnabled;
            WeeklyFixtureListEnabled = settings.WeeklyFixtureListEnabled;

            // Load other settings if they exist
            FramesPerMatch = 8; // Default value
            AutoSave = true;
        }
    }
    
    private async Task LoadNotificationStatusAsync()
    {
        try
        {
            if (_notificationService != null)
            {
                NotificationsEnabled = await _notificationService.AreNotificationsEnabledAsync();
                PendingNotifications = await _notificationService.GetPendingNotificationCountAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsViewModel notification load error: {ex.Message}");
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
        DarkMode = false;
        UseSystemTheme = true;
        DefaultVenue = "";
        FramesPerMatch = 8;
        AutoSave = true;

        // Apply system theme
        ThemeService.Current.UseSystemTheme();

        await SaveSettingsAsync();
        SetStatus("Settings reset to defaults");
    }

    [RelayCommand]
    private Task ClearAllDataAsync()
    {
        // This would need confirmation dialog in the view
        SetStatus("Clear all data requested");
        return Task.CompletedTask;
    }
    
    // Notification Commands (NEW)
    [RelayCommand]
    private async Task RequestNotificationPermissionsAsync()
    {
        if (_notificationService != null)
        {
            var granted = await _notificationService.RequestPermissionsAsync();
            NotificationsEnabled = granted;

            if (granted)
            {
                SetStatus("✅ Notifications enabled");
            }
            else
            {
                SetStatus("❌ Notifications permission denied");
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
                    title: "ℹ️ Test Notification",
                    message: "Notifications are working! You'll get match reminders.");
                
                SetStatus("✅ Test notification sent");
            }
            catch (System.Exception ex)
            {
                SetStatus($"❌ Test failed: {ex.Message}");
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
                PendingNotifications = 0;
                SetStatus("✅ All notifications cancelled");
            }
            catch (System.Exception ex)
            {
                SetStatus($"❌ Cancel failed: {ex.Message}");
            }
        }
    }
    
    [RelayCommand]
    private async Task RefreshNotificationStatusAsync()
    {
        await LoadNotificationStatusAsync();
        SetStatus($"✅ {PendingNotifications} pending notifications");
    }

    private void SetStatus(string message)
    {
        StatusMessage = $"{System.DateTime.Now:HH:mm:ss}  {message}";
    }
    
    // Theme Commands
    [RelayCommand]
    private void SetDarkMode(bool enabled)
    {
        DarkMode = enabled;
        if (!UseSystemTheme)
        {
            ThemeService.Current.SetDarkMode(enabled);
            SetStatus(enabled ? "ℹ️ Dark mode enabled" : "ℹ️ Light mode enabled");
        }
    }

    [RelayCommand]
    private void SetSystemTheme(bool enabled)
    {
        UseSystemTheme = enabled;
        if (enabled)
        {
            ThemeService.Current.UseSystemTheme();
            SetStatus("ℹ️ Following system theme");
        }
        else
        {
            ThemeService.Current.SetDarkMode(DarkMode);
        }
    }

    [RelayCommand]
    private void ToggleDarkMode()
    {
        if (UseSystemTheme)
        {
            // First disable system theme, then toggle
            UseSystemTheme = false;
            DarkMode = true;
            ThemeService.Current.SetDarkMode(true);
        }
        else
        {
            _darkMode = !_darkMode;
            ThemeService.Current.SetDarkMode(_darkMode);
        }
    }
}
