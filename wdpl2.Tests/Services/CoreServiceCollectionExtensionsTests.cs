using Microsoft.Extensions.DependencyInjection;
using Moq;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for CoreServiceCollectionExtensions - DI registration extension methods.
/// </summary>
public class CoreServiceCollectionExtensionsTests
{
    private static ServiceCollection CreateServicesWithDataStore()
    {
        var services = new ServiceCollection();
        var mockDataStore = new Mock<IDataStore>();
        mockDataStore.Setup(x => x.GetData()).Returns(new LeagueData());
        services.AddSingleton(mockDataStore.Object);
        return services;
    }

    [Fact]
    public void AddCoreAppServices_RegistersSeasonService()
    {
        // Arrange
        var services = CreateServicesWithDataStore();

        // Act
        var result = services.AddCoreAppServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var seasonService = serviceProvider.GetService<ISeasonService>();
        Assert.NotNull(seasonService);
        Assert.IsType<SeasonService>(seasonService);
    }

    [Fact]
    public void AddCoreAppServices_RegistersThemeService()
    {
        // Arrange
        var services = CreateServicesWithDataStore();

        // Act
        var result = services.AddCoreAppServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var themeService = serviceProvider.GetService<IThemeService>();
        Assert.NotNull(themeService);
        Assert.IsType<ThemeService>(themeService);
    }

    [Fact]
    public void AddCoreAppServices_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddCoreAppServices();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddCoreAppServices_RegistersSeasonServiceAsSingleton()
    {
        // Arrange
        var services = CreateServicesWithDataStore();

        // Act
        services.AddCoreAppServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var seasonService1 = serviceProvider.GetService<ISeasonService>();
        var seasonService2 = serviceProvider.GetService<ISeasonService>();
        Assert.Same(seasonService1, seasonService2);
    }

    [Fact]
    public void AddCoreAppServices_RegistersThemeServiceAsSingleton()
    {
        // Arrange
        var services = CreateServicesWithDataStore();

        // Act
        services.AddCoreAppServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var themeService1 = serviceProvider.GetService<IThemeService>();
        var themeService2 = serviceProvider.GetService<IThemeService>();
        Assert.Same(themeService1, themeService2);
    }

    [Fact]
    public void AddCoreAppServices_RegistersBothServices()
    {
        // Arrange
        var services = CreateServicesWithDataStore();

        // Act
        services.AddCoreAppServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var seasonService = serviceProvider.GetService<ISeasonService>();
        var themeService = serviceProvider.GetService<IThemeService>();
        Assert.NotNull(seasonService);
        Assert.NotNull(themeService);
    }
}
