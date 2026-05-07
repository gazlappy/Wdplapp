using Moq;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for ThemeService — manages app theme (light/dark mode).
/// Uses a mocked <see cref="IDataStore"/>; methods that touch <c>Application.Current</c>
/// are tolerant of a null UI host (return early / return false).
/// </summary>
public class ThemeServiceTests
{
    private static (ThemeService Service, LeagueData Data, Mock<IDataStore> Mock) CreateService()
    {
        var data = new LeagueData();
        var mock = new Mock<IDataStore>();
        mock.Setup(x => x.GetData()).Returns(data);
        mock.Setup(x => x.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new ThemeService(mock.Object);
        return (service, data, mock);
    }

    [Fact]
    public void Constructor_SetsCurrentStaticProperty()
    {
        var (service, _, _) = CreateService();
        Assert.Same(service, ThemeService.Current);
    }

    [Fact]
    public void Constructor_NullDataStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ThemeService(null!));
    }

    [Fact]
    public void GetThemeForJs_WhenNotDarkMode_ReturnsLight()
    {
        var (service, _, _) = CreateService();
        Assert.Equal("light", service.GetThemeForJs());
    }

    [Fact]
    public void IsDarkModeActive_ApplicationCurrentNull_ReturnsFalse()
    {
        var (service, _, _) = CreateService();
        Assert.False(service.IsDarkModeActive);
    }

    [Fact]
    public void ApplyTheme_NoParameters_DoesNotThrow()
    {
        var (service, _, _) = CreateService();
        service.ApplyTheme();
    }

    [Fact]
    public void SetDarkMode_True_PersistsAndDisablesSystemTheme()
    {
        var (service, data, mock) = CreateService();
        data.Settings.UseSystemTheme = true;

        service.SetDarkMode(true);

        Assert.True(data.Settings.DarkModeEnabled);
        Assert.False(data.Settings.UseSystemTheme);
        mock.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SetDarkMode_False_PersistsAndDisablesSystemTheme()
    {
        var (service, data, mock) = CreateService();
        data.Settings.UseSystemTheme = true;
        data.Settings.DarkModeEnabled = true;

        service.SetDarkMode(false);

        Assert.False(data.Settings.DarkModeEnabled);
        Assert.False(data.Settings.UseSystemTheme);
        mock.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void UseSystemTheme_PersistsSystemThemeFlag()
    {
        var (service, data, mock) = CreateService();

        service.UseSystemTheme();

        Assert.True(data.Settings.UseSystemTheme);
        mock.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ApplyTheme_UseSystemTheme_ApplicationCurrentNull_DoesNotThrow()
    {
        var (service, _, _) = CreateService();
        service.ApplyTheme(useSystemTheme: true, darkModeEnabled: true);
        service.ApplyTheme(useSystemTheme: true, darkModeEnabled: false);
    }

    [Fact]
    public void ApplyTheme_DarkModeEnabled_ApplicationCurrentNull_DoesNotThrow()
    {
        var (service, _, _) = CreateService();
        service.ApplyTheme(useSystemTheme: false, darkModeEnabled: true);
    }

    [Fact]
    public void ApplyTheme_DarkModeDisabled_ApplicationCurrentNull_DoesNotThrow()
    {
        var (service, _, _) = CreateService();
        service.ApplyTheme(useSystemTheme: false, darkModeEnabled: false);
    }
}
