using Wdpl2.WinUI;

namespace wdpl2.Tests;

/// <summary>
/// Tests for Windows platform App and related types.
/// </summary>
/// <remarks>
/// Note: The App constructor and CreateMauiApp method cannot be unit tested because they depend on:
/// 1. XAML infrastructure (InitializeComponent) which requires a running WinUI application
/// 2. MauiWinUIApplication base class which requires WinUI runtime environment
/// 3. UnhandledException event which is part of WinUI application lifecycle
/// 4. Full MAUI application initialization with platform-specific handlers and services
/// 
/// These dependencies require a full WinUI/MAUI application context with proper platform
/// initialization and should be tested through integration/UI tests rather than unit tests.
/// The App class is a platform-specific entry point that bootstraps the MAUI application
/// and cannot be instantiated outside of its intended WinUI runtime environment.
/// 
/// Attempting to test these methods results in System.Runtime.InteropServices.COMException
/// during ViewHandler initialization, as the WinUI framework components (FocusManager, etc.)
/// are not available in a unit test context.
/// </remarks>
public class AppTests
{
    [Fact(Skip = "Cannot unit test: requires WinUI application context and MAUI infrastructure")]
    public void App_Constructor_InitializesUnhandledExceptionHandler()
    {
        // This test is skipped because:
        // - App inherits from MauiWinUIApplication which requires WinUI runtime
        // - InitializeComponent() requires XAML parser and WinUI application host
        // - UnhandledException event is part of WinUI lifecycle, not available in unit tests
        //
        // The constructor should be validated through integration tests where the
        // full WinUI application context is available.
    }

    [Fact(Skip = "Cannot unit test: requires WinUI application context and MAUI infrastructure")]
    public void CreateMauiApp_ReturnsMauiApp()
    {
        // This test is skipped because:
        // - MauiProgram.CreateMauiApp() initializes full MAUI framework
        // - UseMauiApp<App>() attempts to instantiate App with WinUI dependencies
        // - Platform handlers (ViewHandler) require WinUI FocusManager and other COM components
        // - Results in COMException: ViewHandler type initializer fails in unit test context
        //
        // The CreateMauiApp method should be validated through integration tests where
        // the WinUI/MAUI platform is properly initialized.
    }
}
