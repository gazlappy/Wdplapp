using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for ThemeService - manages app theme (light/dark mode).
/// 
/// TESTABILITY LIMITATIONS:
/// This class has static dependencies that prevent comprehensive unit testing:
/// 1. Application.Current (MAUI static) - Required for theme application (lines 46-55)
/// 2. DataStore (static class) - Required for settings persistence (lines 37, 63-66, 75-77)
/// 
/// COVERAGE STATUS:
/// - Constructor: Fully tested ✓
/// - ApplyTheme(bool, bool): Partially tested (early return path only, lines 48-55 require Application.Current)
/// - ApplyTheme(): Requires DataStore, test skipped
/// - SetDarkMode(): Requires DataStore, test skipped
/// - UseSystemTheme(): Requires DataStore, test skipped
/// 
/// These dependencies require MAUI runtime context. Full testing would require:
/// - Integration tests with MAUI infrastructure, OR
/// - Refactoring to use dependency injection (IApplication, IDataStore interfaces)
/// </summary>
public class ThemeServiceTests
{
    [Fact]
    public void Constructor_SetsCurrentStaticProperty()
    {
        // Arrange & Act
        var service = new ThemeService();

        // Assert
        Assert.Same(service, ThemeService.Current);
    }

    [Fact]
    public void GetThemeForJs_WhenNotDarkMode_ReturnsLight()
    {
        // Arrange
        var service = new ThemeService();
        // Application.Current will be null, IsDarkModeActive returns false

        // Act
        var theme = service.GetThemeForJs();

        // Assert
        Assert.Equal("light", theme);
    }

    [Fact]
    public void IsDarkModeActive_ApplicationCurrentNull_ReturnsFalse()
    {
        // Arrange
        var service = new ThemeService();
        // Application.Current will be null in test context

        // Act
        var isDark = service.IsDarkModeActive;

        // Assert
        Assert.False(isDark);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data.Settings cannot be accessed in unit tests")]
    public void ApplyTheme_NoParameters_DoesNotThrow()
    {
        // Arrange
        var service = new ThemeService();

        // Act & Assert
        // Accesses DataStore.Data.Settings and calls ApplyTheme with those values
        // Application.Current will be null, so it returns early without setting theme
        service.ApplyTheme();
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data.Settings cannot be accessed in unit tests")]
    public void SetDarkMode_True_DoesNotThrow()
    {
        // Arrange
        var service = new ThemeService();

        // Act & Assert
        // Sets DarkModeEnabled = true, UseSystemTheme = false, saves, and applies theme
        // DataStore.Data.Settings is accessible in test context
        service.SetDarkMode(true);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data.Settings cannot be accessed in unit tests")]
    public void SetDarkMode_False_DoesNotThrow()
    {
        // Arrange
        var service = new ThemeService();

        // Act & Assert
        // Sets DarkModeEnabled = false, UseSystemTheme = false, saves, and applies theme
        service.SetDarkMode(false);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data.Settings cannot be accessed in unit tests")]
    public void UseSystemTheme_DoesNotThrow()
    {
        // Arrange
        var service = new ThemeService();

        // Act & Assert
        // Sets UseSystemTheme = true, saves, and applies theme
        service.UseSystemTheme();
    }

    [Fact]
    public void ApplyTheme_UseSystemTheme_ApplicationCurrentNull_DoesNotThrow()
    {
        // Arrange
        var service = new ThemeService();

        // Act & Assert
        service.ApplyTheme(useSystemTheme: true, darkModeEnabled: true);
        service.ApplyTheme(useSystemTheme: true, darkModeEnabled: false);
    }

    [Fact]
    public void ApplyTheme_DarkModeEnabled_ApplicationCurrentNull_DoesNotThrow()
    {
        // Arrange
        var service = new ThemeService();

        // Act & Assert
        service.ApplyTheme(useSystemTheme: false, darkModeEnabled: true);
    }

    [Fact]
    public void ApplyTheme_DarkModeDisabled_ApplicationCurrentNull_DoesNotThrow()
    {
        // Arrange
        var service = new ThemeService();

        // Act & Assert
        service.ApplyTheme(useSystemTheme: false, darkModeEnabled: false);
    }
}
