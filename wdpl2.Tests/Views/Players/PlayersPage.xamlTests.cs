using System;
using Microsoft.Maui.Graphics;
using Wdpl2.Views;

namespace wdpl2.Tests;

/// <summary>
/// Tests for PlayersPage and related types.
/// </summary>
public class PlayersPageXamlTests
{
    #region FullName Tests

    [Fact]
    public void PlayerListItem_FullName_WithFirstAndLast_ReturnsCombined()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = "John",
            Last = "Doe"
        };

        // Act
        var result = playerListItem.FullName;

        // Assert
        Assert.Equal("John Doe", result);
    }

    [Fact]
    public void PlayerListItem_FullName_WithFirstOnly_ReturnsFirst()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = "John",
            Last = null
        };

        // Act
        var result = playerListItem.FullName;

        // Assert
        Assert.Equal("John", result);
    }

    [Fact]
    public void PlayerListItem_FullName_WithLastOnly_ReturnsLast()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = null,
            Last = "Doe"
        };

        // Act
        var result = playerListItem.FullName;

        // Assert
        Assert.Equal("Doe", result);
    }

    [Fact]
    public void PlayerListItem_FullName_WithBothNull_ReturnsEmpty()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = null,
            Last = null
        };

        // Act
        var result = playerListItem.FullName;

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void PlayerListItem_FullName_WithWhitespaceFirst_ReturnsLastOnly()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = "   ",
            Last = "Doe"
        };

        // Act
        var result = playerListItem.FullName;

        // Assert
        Assert.Equal("Doe", result);
    }

    [Fact]
    public void PlayerListItem_FullName_WithWhitespaceLast_ReturnsFirstOnly()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = "John",
            Last = "   "
        };

        // Act
        var result = playerListItem.FullName;

        // Assert
        Assert.Equal("John", result);
    }

    [Fact]
    public void PlayerListItem_FullName_WithEmptyStrings_ReturnsEmpty()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = "",
            Last = ""
        };

        // Act
        var result = playerListItem.FullName;

        // Assert
        Assert.Equal("", result);
    }

    #endregion

    #region DisplayTeamLabel Tests

    [Fact]
    public void PlayerListItem_DisplayTeamLabel_WithEmptyTeamLabel_ReturnsPlaceholder()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            TeamLabel = ""
        };

        // Act
        var result = playerListItem.DisplayTeamLabel;

        // Assert
        Assert.Equal("+ team", result);
    }

    [Fact]
    public void PlayerListItem_DisplayTeamLabel_WithNullTeamLabel_ReturnsPlaceholder()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            TeamLabel = null!
        };

        // Act
        var result = playerListItem.DisplayTeamLabel;

        // Assert
        Assert.Equal("+ team", result);
    }

    [Fact]
    public void PlayerListItem_DisplayTeamLabel_WithTeamLabel_ReturnsTeamLabel()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            TeamLabel = "Blue Team"
        };

        // Act
        var result = playerListItem.DisplayTeamLabel;

        // Assert
        Assert.Equal("Blue Team", result);
    }

    #endregion

    #region TeamLabelColor Tests

    [Fact]
    public void PlayerListItem_TeamLabelColor_WithEmptyTeamLabel_ReturnsGrayColor()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            TeamLabel = ""
        };

        // Act
        var result = playerListItem.TeamLabelColor;

        // Assert
        Assert.Equal(Color.FromArgb("#9CA3AF"), result);
    }

    [Fact]
    public void PlayerListItem_TeamLabelColor_WithNullTeamLabel_ReturnsGrayColor()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            TeamLabel = null!
        };

        // Act
        var result = playerListItem.TeamLabelColor;

        // Assert
        Assert.Equal(Color.FromArgb("#9CA3AF"), result);
    }

    [Fact]
    public void PlayerListItem_TeamLabelColor_WithTeamLabel_ReturnsBlueColor()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            TeamLabel = "Blue Team"
        };

        // Act
        var result = playerListItem.TeamLabelColor;

        // Assert
        Assert.Equal(Color.FromArgb("#3B82F6"), result);
    }

    #endregion

    #region Initials Tests

    [Fact]
    public void PlayerListItem_Initials_WithFirstAndLast_ReturnsInitials()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = "John",
            Last = "Doe"
        };

        // Act
        var result = playerListItem.Initials;

        // Assert
        Assert.Equal("JD", result);
    }

    [Fact]
    public void PlayerListItem_Initials_WithFirstOnly_ReturnsFirstInitial()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = "John",
            Last = null
        };

        // Act
        var result = playerListItem.Initials;

        // Assert
        Assert.Equal("J", result);
    }

    [Fact]
    public void PlayerListItem_Initials_WithLastOnly_ReturnsLastInitial()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = null,
            Last = "Doe"
        };

        // Act
        var result = playerListItem.Initials;

        // Assert
        Assert.Equal("D", result);
    }

    [Fact]
    public void PlayerListItem_Initials_WithBothNull_ReturnsEmpty()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = null,
            Last = null
        };

        // Act
        var result = playerListItem.Initials;

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void PlayerListItem_Initials_WithWhitespaceFirst_ReturnsLastInitialOnly()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = "   ",
            Last = "Doe"
        };

        // Act
        var result = playerListItem.Initials;

        // Assert
        Assert.Equal("D", result);
    }

    [Fact]
    public void PlayerListItem_Initials_WithWhitespaceLast_ReturnsFirstInitialOnly()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = "John",
            Last = "   "
        };

        // Act
        var result = playerListItem.Initials;

        // Assert
        Assert.Equal("J", result);
    }

    [Fact]
    public void PlayerListItem_Initials_WithEmptyStrings_ReturnsEmpty()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            First = "",
            Last = ""
        };

        // Act
        var result = playerListItem.Initials;

        // Assert
        Assert.Equal("", result);
    }

    #endregion

    #region TransferBadge Tests

    [Fact]
    public void PlayerListItem_TransferBadge_WithHasTransfersTrue_ReturnsEmoji()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            HasTransfers = true
        };

        // Act
        var result = playerListItem.TransferBadge;

        // Assert
        Assert.Equal("\U0001F504", result);
    }

    [Fact]
    public void PlayerListItem_TransferBadge_WithHasTransfersFalse_ReturnsEmpty()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            HasTransfers = false
        };

        // Act
        var result = playerListItem.TransferBadge;

        // Assert
        Assert.Equal("", result);
    }

    #endregion

    #region StatusLabel Tests

    [Fact]
    public void PlayerListItem_StatusLabel_WithIsActiveTrue_ReturnsEmpty()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            IsActive = true
        };

        // Act
        var result = playerListItem.StatusLabel;

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void PlayerListItem_StatusLabel_WithIsActiveFalse_ReturnsInactive()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            IsActive = false
        };

        // Act
        var result = playerListItem.StatusLabel;

        // Assert
        Assert.Equal("\u26AA Inactive", result);
    }

    #endregion

    #region ShowStatusLabel Tests

    [Fact]
    public void PlayerListItem_ShowStatusLabel_WithIsActiveTrue_ReturnsFalse()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            IsActive = true
        };

        // Act
        var result = playerListItem.ShowStatusLabel;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void PlayerListItem_ShowStatusLabel_WithIsActiveFalse_ReturnsTrue()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            IsActive = false
        };

        // Act
        var result = playerListItem.ShowStatusLabel;

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Opacity Tests

    [Fact]
    public void PlayerListItem_Opacity_WithIsActiveTrue_ReturnsOne()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            IsActive = true
        };

        // Act
        var result = playerListItem.Opacity;

        // Assert
        Assert.Equal(1.0, result);
    }

    [Fact]
    public void PlayerListItem_Opacity_WithIsActiveFalse_ReturnsPointSix()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            IsActive = false
        };

        // Act
        var result = playerListItem.Opacity;

        // Assert
        Assert.Equal(0.6, result);
    }

    #endregion

    #region AvatarColor Tests

    [Fact]
    public void PlayerListItem_AvatarColor_WithIsActiveTrue_ReturnsBlueColor()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            IsActive = true
        };

        // Act
        var result = playerListItem.AvatarColor;

        // Assert
        Assert.Equal(Color.FromArgb("#3B82F6"), result);
    }

    [Fact]
    public void PlayerListItem_AvatarColor_WithIsActiveFalse_ReturnsGrayColor()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            IsActive = false
        };

        // Act
        var result = playerListItem.AvatarColor;

        // Assert
        Assert.Equal(Color.FromArgb("#9CA3AF"), result);
    }

    #endregion

    #region NameDecoration Tests

    [Fact]
    public void PlayerListItem_NameDecoration_WithIsActiveTrue_ReturnsNone()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            IsActive = true
        };

        // Act
        var result = playerListItem.NameDecoration;

        // Assert
        Assert.Equal(TextDecorations.None, result);
    }

    [Fact]
    public void PlayerListItem_NameDecoration_WithIsActiveFalse_ReturnsStrikethrough()
    {
        // Arrange
        var playerListItem = new PlayersPage.PlayerListItem
        {
            IsActive = false
        };

        // Act
        var result = playerListItem.NameDecoration;

        // Assert
        Assert.Equal(TextDecorations.Strikethrough, result);
    }

    #endregion

    #region HeadToHeadItem TotalFrames Tests

    [Fact]
    public void HeadToHeadItem_TotalFrames_WithWinsAndLosses_ReturnsSum()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 5,
            Losses = 3
        };

        // Act
        var result = item.TotalFrames;

        // Assert
        Assert.Equal(8, result);
    }

    [Fact]
    public void HeadToHeadItem_TotalFrames_WithZeroWinsAndLosses_ReturnsZero()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 0,
            Losses = 0
        };

        // Act
        var result = item.TotalFrames;

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void HeadToHeadItem_TotalFrames_WithOnlyWins_ReturnsWins()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 10,
            Losses = 0
        };

        // Act
        var result = item.TotalFrames;

        // Assert
        Assert.Equal(10, result);
    }

    [Fact]
    public void HeadToHeadItem_TotalFrames_WithOnlyLosses_ReturnsLosses()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 0,
            Losses = 7
        };

        // Act
        var result = item.TotalFrames;

        // Assert
        Assert.Equal(7, result);
    }

    #endregion

    #region HeadToHeadItem WinPercentage Tests

    [Fact]
    public void HeadToHeadItem_WinPercentage_WithZeroTotalFrames_ReturnsZero()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 0,
            Losses = 0
        };

        // Act
        var result = item.WinPercentage;

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void HeadToHeadItem_WinPercentage_WithAllWins_ReturnsHundred()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 10,
            Losses = 0
        };

        // Act
        var result = item.WinPercentage;

        // Assert
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void HeadToHeadItem_WinPercentage_WithAllLosses_ReturnsZero()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 0,
            Losses = 10
        };

        // Act
        var result = item.WinPercentage;

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void HeadToHeadItem_WinPercentage_WithMixedRecord_ReturnsCorrectPercentage()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 5,
            Losses = 5
        };

        // Act
        var result = item.WinPercentage;

        // Assert
        Assert.Equal(50.0, result);
    }

    [Fact]
    public void HeadToHeadItem_WinPercentage_WithPartialWins_ReturnsCorrectPercentage()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 7,
            Losses = 3
        };

        // Act
        var result = item.WinPercentage;

        // Assert
        Assert.Equal(70.0, result);
    }

    #endregion

    #region HeadToHeadItem RecordText Tests

    [Fact]
    public void HeadToHeadItem_RecordText_WithWinsAndLosses_ReturnsFormattedString()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 5,
            Losses = 3
        };

        // Act
        var result = item.RecordText;

        // Assert
        Assert.Equal("5-3", result);
    }

    [Fact]
    public void HeadToHeadItem_RecordText_WithZeroWinsAndLosses_ReturnsZeroDashZero()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 0,
            Losses = 0
        };

        // Act
        var result = item.RecordText;

        // Assert
        Assert.Equal("0-0", result);
    }

    [Fact]
    public void HeadToHeadItem_RecordText_WithLargeNumbers_ReturnsFormattedString()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 100,
            Losses = 50
        };

        // Act
        var result = item.RecordText;

        // Assert
        Assert.Equal("100-50", result);
    }

    #endregion

    #region HeadToHeadItem RecordColor Tests

    [Fact]
    public void HeadToHeadItem_RecordColor_WithMoreWins_ReturnsGreenColor()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 5,
            Losses = 3
        };

        // Act
        var result = item.RecordColor;

        // Assert
        Assert.Equal(Color.FromArgb("#10B981"), result);
    }

    [Fact]
    public void HeadToHeadItem_RecordColor_WithMoreLosses_ReturnsRedColor()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 3,
            Losses = 5
        };

        // Act
        var result = item.RecordColor;

        // Assert
        Assert.Equal(Color.FromArgb("#EF4444"), result);
    }

    [Fact]
    public void HeadToHeadItem_RecordColor_WithEqualWinsAndLosses_ReturnsGrayColor()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 5,
            Losses = 5
        };

        // Act
        var result = item.RecordColor;

        // Assert
        Assert.Equal(Color.FromArgb("#6B7280"), result);
    }

    [Fact]
    public void HeadToHeadItem_RecordColor_WithZeroWinsAndLosses_ReturnsGrayColor()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            Wins = 0,
            Losses = 0
        };

        // Act
        var result = item.RecordColor;

        // Assert
        Assert.Equal(Color.FromArgb("#6B7280"), result);
    }

    #endregion

    #region HeadToHeadItem HasMultipleSeasons Tests

    [Fact]
    public void HeadToHeadItem_HasMultipleSeasons_WithEmptyList_ReturnsFalse()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            SeasonBreakdown = new()
        };

        // Act
        var result = item.HasMultipleSeasons;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HeadToHeadItem_HasMultipleSeasons_WithOneSeason_ReturnsFalse()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            SeasonBreakdown = new()
            {
                new PlayersPage.SeasonRecord { SeasonName = "2023", Record = "5-3" }
            }
        };

        // Act
        var result = item.HasMultipleSeasons;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HeadToHeadItem_HasMultipleSeasons_WithTwoSeasons_ReturnsTrue()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            SeasonBreakdown = new()
            {
                new PlayersPage.SeasonRecord { SeasonName = "2023", Record = "5-3" },
                new PlayersPage.SeasonRecord { SeasonName = "2024", Record = "3-2" }
            }
        };

        // Act
        var result = item.HasMultipleSeasons;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HeadToHeadItem_HasMultipleSeasons_WithMultipleSeasons_ReturnsTrue()
    {
        // Arrange
        var item = new PlayersPage.HeadToHeadItem
        {
            SeasonBreakdown = new()
            {
                new PlayersPage.SeasonRecord { SeasonName = "2022", Record = "2-1" },
                new PlayersPage.SeasonRecord { SeasonName = "2023", Record = "5-3" },
                new PlayersPage.SeasonRecord { SeasonName = "2024", Record = "3-2" }
            }
        };

        // Act
        var result = item.HasMultipleSeasons;

        // Assert
        Assert.True(result);
    }

    #endregion

    #region PlayersPage Constructor, OnAppearing, and OnDisappearing Tests
    
    // Note: The PlayersPage constructor, OnAppearing(), and OnDisappearing() methods cannot be unit tested
    // without a MAUI application context because:
    // 1. InitializeComponent() requires XAML compilation and a running MAUI UI context
    // 2. UI controls (PlayersList, TeamPicker, SearchEntry, etc.) are initialized through XAML
    // 3. DataStore.Data depends on static initialization that requires FileSystem.AppDataDirectory
    // 4. These methods interact with UI lifecycle events that require a ContentPage to be properly initialized
    //
    // To test these methods, use MAUI integration/UI tests with a test host, or refactor to use
    // dependency injection to make the dependencies testable.
    //
    // Reference: Repository insight on MAUI ContentPage testing limitations.

    #endregion
}
