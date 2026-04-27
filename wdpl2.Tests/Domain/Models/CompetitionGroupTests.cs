using Wdpl2.Models;

namespace wdpl2.Tests;

/// <summary>
/// Tests for CompetitionGroup and GroupStanding domain models.
/// </summary>
public class CompetitionGroupTests
{
    [Fact]
    public void VenueDisplay_WithVenueAndTable_ReturnsVenueAndTableLabel()
    {
        // Arrange
        var group = new CompetitionGroup
        {
            VenueId = Guid.NewGuid(),
            VenueName = "Test Venue",
            TableLabel = "Table 1"
        };

        // Act
        var result = group.VenueDisplay;

        // Assert
        Assert.Equal("Test Venue — Table 1", result);
    }

    [Fact]
    public void VenueDisplay_WithVenueButNoTableLabel_ReturnsVenueOnly()
    {
        // Arrange
        var group = new CompetitionGroup
        {
            VenueId = Guid.NewGuid(),
            VenueName = "Test Venue",
            TableLabel = null
        };

        // Act
        var result = group.VenueDisplay;

        // Assert
        Assert.Equal("Test Venue", result);
    }

    [Fact]
    public void VenueDisplay_WithVenueButEmptyTableLabel_ReturnsVenueOnly()
    {
        // Arrange
        var group = new CompetitionGroup
        {
            VenueId = Guid.NewGuid(),
            VenueName = "Test Venue",
            TableLabel = ""
        };

        // Act
        var result = group.VenueDisplay;

        // Assert
        Assert.Equal("Test Venue", result);
    }

    [Fact]
    public void VenueDisplay_WithoutVenueId_ReturnsEmptyString()
    {
        // Arrange
        var group = new CompetitionGroup
        {
            VenueId = null,
            VenueName = "Test Venue",
            TableLabel = "Table 1"
        };

        // Act
        var result = group.VenueDisplay;

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void VenueDisplay_WithVenueIdButNullVenueName_ReturnsEmptyString()
    {
        // Arrange
        var group = new CompetitionGroup
        {
            VenueId = Guid.NewGuid(),
            VenueName = null,
            TableLabel = "Table 1"
        };

        // Act
        var result = group.VenueDisplay;

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void VenueDisplay_WithVenueIdButEmptyVenueName_ReturnsEmptyString()
    {
        // Arrange
        var group = new CompetitionGroup
        {
            VenueId = Guid.NewGuid(),
            VenueName = "",
            TableLabel = "Table 1"
        };

        // Act
        var result = group.VenueDisplay;

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void ToString_ReturnsName()
    {
        // Arrange
        var group = new CompetitionGroup
        {
            Name = "Group A"
        };

        // Act
        var result = group.ToString();

        // Assert
        Assert.Equal("Group A", result);
    }

    [Fact]
    public void FrameDifference_WithPositiveDifference_ReturnsCorrectValue()
    {
        // Arrange
        var standing = new GroupStanding
        {
            FramesFor = 10,
            FramesAgainst = 5
        };

        // Act
        var result = standing.FrameDifference;

        // Assert
        Assert.Equal(5, result);
    }

    [Fact]
    public void FrameDifference_WithNegativeDifference_ReturnsCorrectValue()
    {
        // Arrange
        var standing = new GroupStanding
        {
            FramesFor = 5,
            FramesAgainst = 10
        };

        // Act
        var result = standing.FrameDifference;

        // Assert
        Assert.Equal(-5, result);
    }

    [Fact]
    public void FrameDifference_WithZeroDifference_ReturnsZero()
    {
        // Arrange
        var standing = new GroupStanding
        {
            FramesFor = 7,
            FramesAgainst = 7
        };

        // Act
        var result = standing.FrameDifference;

        // Assert
        Assert.Equal(0, result);
    }
}
