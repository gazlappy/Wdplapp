namespace Wdpl2.Services;

/// <summary>
/// Service for managing app theme (light/dark mode)
/// </summary>
public static class ThemeService
{
    /// <summary>
    /// Apply the theme based on app settings
    /// </summary>
    public static void ApplyTheme()
    {
        var settings = Wdpl2.DataStore.Data.Settings;
        ApplyTheme(settings.UseSystemTheme, settings.DarkModeEnabled);
    }

    /// <summary>
    /// Apply the theme with specific settings
    /// </summary>
    public static void ApplyTheme(bool useSystemTheme, bool darkModeEnabled)
    {
        if (Application.Current == null) return;

        if (useSystemTheme)
        {
            // Follow system theme
            Application.Current.UserAppTheme = AppTheme.Unspecified;
        }
        else
        {
            // Use explicit theme setting
            Application.Current.UserAppTheme = darkModeEnabled ? AppTheme.Dark : AppTheme.Light;
        }
    }

    /// <summary>
    /// Toggle dark mode and save the setting
    /// </summary>
    public static void SetDarkMode(bool enabled)
    {
        var settings = Wdpl2.DataStore.Data.Settings;
        settings.DarkModeEnabled = enabled;
        settings.UseSystemTheme = false; // Disable system theme when manually setting
        Wdpl2.DataStore.Save();
        ApplyTheme();
    }

    /// <summary>
    /// Enable system theme following
    /// </summary>
    public static void UseSystemTheme()
    {
        var settings = Wdpl2.DataStore.Data.Settings;
        settings.UseSystemTheme = true;
        Wdpl2.DataStore.Save();
        ApplyTheme();
    }

    /// <summary>
    /// Get whether dark mode is currently active
    /// </summary>
    public static bool IsDarkModeActive
    {
        get
        {
            if (Application.Current == null) return false;
            
            var settings = Wdpl2.DataStore.Data.Settings;
            if (settings.UseSystemTheme)
            {
                return Application.Current.RequestedTheme == AppTheme.Dark;
            }
            return settings.DarkModeEnabled;
        }
    }

    /// <summary>
    /// Get current theme for JavaScript injection (pool game, etc.)
    /// </summary>
    public static string GetThemeForJs() => IsDarkModeActive ? "dark" : "light";
}
