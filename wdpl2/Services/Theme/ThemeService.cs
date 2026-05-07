namespace Wdpl2.Services;

/// <summary>
/// Interface for managing app theme (light/dark mode).
/// </summary>
public interface IThemeService
{
    void ApplyTheme();
    void ApplyTheme(bool useSystemTheme, bool darkModeEnabled);
    void SetDarkMode(bool enabled);
    void UseSystemTheme();
    bool IsDarkModeActive { get; }
    string GetThemeForJs();
}

/// <summary>
/// Service for managing app theme (light/dark mode).
/// Registered as a singleton in DI. Use <see cref="Current"/> for non-DI contexts.
/// </summary>
public class ThemeService : IThemeService
{
    /// <summary>
    /// Static accessor for Views and other non-DI contexts.
    /// </summary>
    public static ThemeService Current { get; private set; } = null!;

    private readonly IDataStore _dataStore;

    public ThemeService(IDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        Current = this;
    }

    /// <summary>
    /// Apply the theme based on app settings
    /// </summary>
    public void ApplyTheme()
    {
        var settings = _dataStore.GetData().Settings;
        ApplyTheme(settings.UseSystemTheme, settings.DarkModeEnabled);
    }

    /// <summary>
    /// Apply the theme with specific settings
    /// </summary>
    public void ApplyTheme(bool useSystemTheme, bool darkModeEnabled)
    {
        if (Application.Current == null) return;

        if (useSystemTheme)
        {
            Application.Current.UserAppTheme = AppTheme.Unspecified;
        }
        else
        {
            Application.Current.UserAppTheme = darkModeEnabled ? AppTheme.Dark : AppTheme.Light;
        }
    }

    /// <summary>
    /// Toggle dark mode and save the setting
    /// </summary>
    public void SetDarkMode(bool enabled)
    {
        var settings = _dataStore.GetData().Settings;
        settings.DarkModeEnabled = enabled;
        settings.UseSystemTheme = false;
        _ = _dataStore.SaveAsync();
        ApplyTheme();
    }

    /// <summary>
    /// Enable system theme following
    /// </summary>
    public void UseSystemTheme()
    {
        var settings = _dataStore.GetData().Settings;
        settings.UseSystemTheme = true;
        _ = _dataStore.SaveAsync();
        ApplyTheme();
    }

    /// <summary>
    /// Get whether dark mode is currently active
    /// </summary>
    public bool IsDarkModeActive
    {
        get
        {
            if (Application.Current == null) return false;

            var settings = _dataStore.GetData().Settings;
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
    public string GetThemeForJs() => IsDarkModeActive ? "dark" : "light";
}
