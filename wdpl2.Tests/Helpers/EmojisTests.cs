using Wdpl2.Helpers;

namespace wdpl2.Tests;

/// <summary>
/// Tests for Emojis helper class - provides emoji constants and platform-specific font family.
/// 
/// TESTABILITY LIMITATIONS:
/// GetFontFamily() depends on Microsoft.Maui.Devices.DeviceInfo.Platform, which is a static property
/// that cannot be mocked with standard unit testing tools. The method has two branches:
/// - WinUI platform: returns "Segoe UI Emoji"
/// - Other platforms: returns null
/// 
/// These tests verify the actual behavior on the current platform. Full branch coverage requires
/// running tests on both WinUI and non-WinUI platforms (e.g., Android, iOS, macOS).
/// </summary>
public class EmojisTests
{
    [Fact]
    public void GetFontFamily_ReturnsValidValue()
    {
        // Act
        var fontFamily = Emojis.GetFontFamily();

        // Assert - verify it returns either the WinUI font or null (for other platforms)
        Assert.True(fontFamily == "Segoe UI Emoji" || fontFamily == null);
    }

    [Fact]
    public void GetFontFamily_ConsistentWithCurrentPlatform()
    {
        // Arrange
        var platform = Microsoft.Maui.Devices.DeviceInfo.Platform;
        
        // Act
        var fontFamily = Emojis.GetFontFamily();

        // Assert - verify the return value matches the expected behavior for current platform
        if (platform == Microsoft.Maui.Devices.DevicePlatform.WinUI)
        {
            Assert.Equal("Segoe UI Emoji", fontFamily);
        }
        else
        {
            Assert.Null(fontFamily);
        }
    }
}
