using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for SeasonScheduler — generates match night schedules.
/// </summary>
public class SeasonSchedulerTests
{
    [Fact]
    public void GenerateMatchNights_NullSeason_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => SeasonScheduler.GenerateMatchNights(null!));
    }

    [Fact]
    public void GenerateMatchNights_EndDateBeforeStartDate_ReturnsEmptyList()
    {
        // Arrange
        var season = new Season
        {
            StartDate = new DateTime(2025, 2, 1),
            EndDate = new DateTime(2025, 1, 1),
            MatchDayOfWeek = DayOfWeek.Tuesday,
            MatchStartTime = new TimeSpan(20, 0, 0)
        };

        // Act
        var result = SeasonScheduler.GenerateMatchNights(season);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GenerateMatchNights_StartDateOnMatchDay_IncludesStartDate()
    {
        // Arrange - Tuesday, Feb 4, 2025
        var season = new Season
        {
            StartDate = new DateTime(2025, 2, 4),
            EndDate = new DateTime(2025, 2, 4),
            MatchDayOfWeek = DayOfWeek.Tuesday,
            MatchStartTime = new TimeSpan(20, 0, 0)
        };

        // Act
        var result = SeasonScheduler.GenerateMatchNights(season);

        // Assert
        Assert.Single(result);
        Assert.Equal(new DateTime(2025, 2, 4, 20, 0, 0), result[0]);
    }

    [Fact]
    public void GenerateMatchNights_StartDateNotOnMatchDay_FindsFirstMatchDay()
    {
        // Arrange - Start on Monday Feb 3, match on Tuesday
        var season = new Season
        {
            StartDate = new DateTime(2025, 2, 3), // Monday
            EndDate = new DateTime(2025, 2, 4),   // Tuesday
            MatchDayOfWeek = DayOfWeek.Tuesday,
            MatchStartTime = new TimeSpan(19, 30, 0)
        };

        // Act
        var result = SeasonScheduler.GenerateMatchNights(season);

        // Assert
        Assert.Single(result);
        Assert.Equal(new DateTime(2025, 2, 4, 19, 30, 0), result[0]);
    }

    [Fact]
    public void GenerateMatchNights_MultipleWeeks_ReturnsAllMatchNights()
    {
        // Arrange - 4 weeks of Wednesdays
        var season = new Season
        {
            StartDate = new DateTime(2025, 2, 5),  // Wednesday
            EndDate = new DateTime(2025, 2, 26),   // Wednesday
            MatchDayOfWeek = DayOfWeek.Wednesday,
            MatchStartTime = new TimeSpan(20, 0, 0)
        };

        // Act
        var result = SeasonScheduler.GenerateMatchNights(season);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal(new DateTime(2025, 2, 5, 20, 0, 0), result[0]);
        Assert.Equal(new DateTime(2025, 2, 12, 20, 0, 0), result[1]);
        Assert.Equal(new DateTime(2025, 2, 19, 20, 0, 0), result[2]);
        Assert.Equal(new DateTime(2025, 2, 26, 20, 0, 0), result[3]);
    }

    [Fact]
    public void GenerateMatchNights_WithBlackoutDates_SkipsBlackouts()
    {
        // Arrange - 3 Thursdays, blackout the middle one
        var season = new Season
        {
            StartDate = new DateTime(2025, 2, 6),  // Thursday
            EndDate = new DateTime(2025, 2, 20),   // Thursday
            MatchDayOfWeek = DayOfWeek.Thursday,
            MatchStartTime = new TimeSpan(20, 0, 0),
            BlackoutDates = new List<DateTime>
            {
                new DateTime(2025, 2, 13) // Second Thursday
            }
        };

        // Act
        var result = SeasonScheduler.GenerateMatchNights(season);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTime(2025, 2, 6, 20, 0, 0), result[0]);
        Assert.Equal(new DateTime(2025, 2, 20, 20, 0, 0), result[1]);
    }

    [Fact]
    public void GenerateMatchNights_WithMultipleBlackouts_SkipsAllBlackouts()
    {
        // Arrange - 5 Mondays, blackout 2nd and 4th
        var season = new Season
        {
            StartDate = new DateTime(2025, 2, 3),  // Monday
            EndDate = new DateTime(2025, 3, 3),    // Monday
            MatchDayOfWeek = DayOfWeek.Monday,
            MatchStartTime = new TimeSpan(18, 0, 0),
            BlackoutDates = new List<DateTime>
            {
                new DateTime(2025, 2, 10), // 2nd Monday
                new DateTime(2025, 2, 24)  // 4th Monday
            }
        };

        // Act
        var result = SeasonScheduler.GenerateMatchNights(season);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new DateTime(2025, 2, 3, 18, 0, 0), result[0]);
        Assert.Equal(new DateTime(2025, 2, 17, 18, 0, 0), result[1]);
        Assert.Equal(new DateTime(2025, 3, 3, 18, 0, 0), result[2]);
    }

    [Fact]
    public void GenerateMatchNights_NoBlackoutDates_IncludesAllMatchDays()
    {
        // Arrange - 3 Fridays, no blackouts
        var season = new Season
        {
            StartDate = new DateTime(2025, 2, 7),  // Friday
            EndDate = new DateTime(2025, 2, 21),   // Friday
            MatchDayOfWeek = DayOfWeek.Friday,
            MatchStartTime = new TimeSpan(21, 0, 0)
        };

        // Act
        var result = SeasonScheduler.GenerateMatchNights(season);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new DateTime(2025, 2, 7, 21, 0, 0), result[0]);
        Assert.Equal(new DateTime(2025, 2, 14, 21, 0, 0), result[1]);
        Assert.Equal(new DateTime(2025, 2, 21, 21, 0, 0), result[2]);
    }

    [Fact]
    public void GenerateMatchNights_MatchStartTimeMidnight_AddsCorrectTime()
    {
        // Arrange
        var season = new Season
        {
            StartDate = new DateTime(2025, 2, 1),  // Saturday
            EndDate = new DateTime(2025, 2, 1),
            MatchDayOfWeek = DayOfWeek.Saturday,
            MatchStartTime = TimeSpan.Zero // Midnight
        };

        // Act
        var result = SeasonScheduler.GenerateMatchNights(season);

        // Assert
        Assert.Single(result);
        Assert.Equal(new DateTime(2025, 2, 1, 0, 0, 0), result[0]);
    }

    [Fact]
    public void GenerateMatchNights_EndDateBeforeFirstMatchDay_ReturnsEmptyList()
    {
        // Arrange - Start Monday, match on Friday, but end on Wednesday
        var season = new Season
        {
            StartDate = new DateTime(2025, 2, 3),  // Monday
            EndDate = new DateTime(2025, 2, 5),    // Wednesday
            MatchDayOfWeek = DayOfWeek.Friday,
            MatchStartTime = new TimeSpan(20, 0, 0)
        };

        // Act
        var result = SeasonScheduler.GenerateMatchNights(season);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GenerateMatchNights_BlackoutOnNonMatchDay_DoesNotAffectResults()
    {
        // Arrange - Match on Tuesday, blackout on Monday
        var season = new Season
        {
            StartDate = new DateTime(2025, 2, 3),  // Monday
            EndDate = new DateTime(2025, 2, 11),   // Tuesday
            MatchDayOfWeek = DayOfWeek.Tuesday,
            MatchStartTime = new TimeSpan(20, 0, 0),
            BlackoutDates = new List<DateTime>
            {
                new DateTime(2025, 2, 3) // Monday, not a match day
            }
        };

        // Act
        var result = SeasonScheduler.GenerateMatchNights(season);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTime(2025, 2, 4, 20, 0, 0), result[0]);
        Assert.Equal(new DateTime(2025, 2, 11, 20, 0, 0), result[1]);
    }
}
