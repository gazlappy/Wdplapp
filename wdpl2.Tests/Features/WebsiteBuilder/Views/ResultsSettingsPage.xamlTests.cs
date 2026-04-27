using Wdpl2.Views.WebsiteBuilder;

namespace wdpl2.Tests;

/// <summary>
/// Tests for ResultsSettingsPage and related types.
/// </summary>
public class ResultsSettingsPageXamlTests
{
    #region ResultsSettingsPage Constructor Tests

    // Note: The ResultsSettingsPage constructor cannot be unit tested without a MAUI application context because:
    // 1. InitializeComponent() requires XAML compilation and a running MAUI UI context
    // 2. UI controls (ShowScoreCheck, ShowDateCheck, ShowTimeCheck, etc.) are initialized through XAML
    // 3. LoadSettings() accesses DataStore.Data which depends on static initialization that requires FileSystem.AppDataDirectory
    // 4. LoadSettings() sets properties on UI controls that must be instantiated through the XAML-generated code
    //
    // To test this constructor, use MAUI integration/UI tests with a test host, or refactor to use
    // dependency injection to make the dependencies testable.
    //
    // Reference: Repository insight on MAUI ContentPage testing limitations.

    #endregion
}
