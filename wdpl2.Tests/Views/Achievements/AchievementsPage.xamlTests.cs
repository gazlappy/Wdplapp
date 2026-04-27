using Wdpl2.Views;

namespace wdpl2.Tests;

/// <summary>
/// Tests for AchievementsPage and related types.
/// </summary>
/// <remarks>
/// Note: The AchievementsPage constructor cannot be unit tested because it depends on:
/// 1. XAML infrastructure (InitializeComponent) which requires a running MAUI application
/// 2. Static DataStore which has a static constructor that depends on FileSystem.AppDataDirectory
/// 3. Static SeasonService for current season management
/// 
/// These dependencies require a full MAUI application context and should be tested
/// through integration/UI tests rather than unit tests. Refactoring to use dependency
/// injection would make the constructor unit-testable.
/// </remarks>
public class AchievementsPageTests
{
    [Fact]
    public void PlayerOption_ToString_ReturnsDisplayName()
    {
        // Arrange
        var playerOption = new PlayerOption
        {
            DisplayName = "John Doe",
            SubText = "2 season(s)",
            GlobalPlayerId = Guid.NewGuid()
        };

        // Act
        var result = playerOption.ToString();

        // Assert
        Assert.Equal("John Doe", result);
    }

    [Fact]
    public void PlayerOption_ToString_WithEmptyDisplayName_ReturnsEmptyString()
    {
        // Arrange
        var playerOption = new PlayerOption
        {
            DisplayName = "",
            SubText = "1 season",
            GlobalPlayerId = Guid.NewGuid()
        };

        // Act
        var result = playerOption.ToString();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void PlayerOption_ToString_WithSpecialCharacters_ReturnsDisplayName()
    {
        // Arrange
        var playerOption = new PlayerOption
        {
            DisplayName = "O'Brien, Patrick-James",
            SubText = "3 season(s)",
            GlobalPlayerId = Guid.NewGuid()
        };

        // Act
        var result = playerOption.ToString();

        // Assert
        Assert.Equal("O'Brien, Patrick-James", result);
    }

    [Fact]
    public void PlayerOption_ToString_WithWhitespace_ReturnsDisplayNameWithWhitespace()
    {
        // Arrange
        var playerOption = new PlayerOption
        {
            DisplayName = "  John  Doe  ",
            SubText = "1 season",
            GlobalPlayerId = Guid.NewGuid()
        };

        // Act
        var result = playerOption.ToString();

        // Assert
        Assert.Equal("  John  Doe  ", result);
    }

    [Fact]
    public void PlayerOption_ToString_WithUnicodeCharacters_ReturnsDisplayName()
    {
        // Arrange
        var playerOption = new PlayerOption
        {
            DisplayName = "José García-López",
            SubText = "2 season(s)",
            GlobalPlayerId = Guid.NewGuid()
        };

        // Act
        var result = playerOption.ToString();

        // Assert
        Assert.Equal("José García-López", result);
    }
}
