using Microsoft.Extensions.DependencyInjection;
using Wdpl2;

namespace wdpl2.Tests;

/// <summary>
/// Tests for MauiProgram - MAUI app initialization and database setup.
/// 
/// NOTE: Both methods in MauiProgram are infrastructure code that cannot be unit tested
/// because they require the full MAUI runtime environment:
/// 
/// 1. CreateMauiApp() - Requires MauiApp.CreateBuilder() and UI framework initialization
/// 2. InitializeDatabaseAsync() - Requires FileSystem.AppDataDirectory which needs MAUI runtime
/// 
/// These methods should be tested through integration tests where the MAUI app is fully initialized.
/// </summary>
public class MauiProgramTests
{
    [Fact(Skip = "Crashes test host: CreateMauiApp requires full MAUI runtime with UI components")]
    public void CreateMauiApp_ReturnsNonNullMauiApp()
    {
        // This test cannot run in unit test environment because:
        // - MauiApp.CreateBuilder() requires MAUI application host
        // - UseMauiApp<App>() requires UI framework initialization  
        // - UseMauiCommunityToolkit(), UseLocalNotification(), UseOcr() require platform services
        // - ConfigureFonts() requires font loading infrastructure
        // 
        // Error: System.TypeInitializationException from Microsoft.Maui.Handlers.ViewHandler
        // Cause: System.Runtime.InteropServices.COMException - UI components need app host
        //
        // This method should be validated through integration tests.
        
        // Arrange & Act
        var app = MauiProgram.CreateMauiApp();

        // Assert
        Assert.NotNull(app);
    }

    [Fact(Skip = "Crashes test host: InitializeDatabaseAsync requires MAUI FileSystem.AppDataDirectory")]
    public async Task InitializeDatabaseAsync_ValidServiceProvider_InitializesDatabase()
    {
        // This test cannot run in unit test environment because:
        // - LeagueContext.GetDatabasePath() calls FileSystem.AppDataDirectory (line 55)
        // - DataMigrationService constructor calls FileSystem.AppDataDirectory (line 52)
        // - FileSystem.AppDataDirectory requires MainThread.IsMainThread check
        // - MainThread check requires Microsoft.UI.Dispatching.DispatcherQueue
        // - DispatcherQueue requires MAUI application host and UI thread
        //
        // Error: System.Runtime.InteropServices.COMException - ClassFactory cannot supply requested class
        // Stack: DispatcherQueue.GetForCurrentThread() -> FileSystemImplementation.get_AppDataDirectory()
        //
        // This method should be validated through integration tests where MAUI runtime is initialized.

        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        await MauiProgram.InitializeDatabaseAsync(serviceProvider);

        // Assert - Would verify database initialization if this could run
    }
}
