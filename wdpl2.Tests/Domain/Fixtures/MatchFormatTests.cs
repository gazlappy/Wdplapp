using Wdpl2.Domain.Fixtures;
using Wdpl2.Models;

namespace wdpl2.Tests;

/// <summary>
/// Pins how many frames a match has.
/// </summary>
/// <remarks>
/// Getting this wrong opens a scorecard with the wrong number of frames, which
/// is only noticed on a match night with two captains waiting. The precedence
/// had been duplicated and the live-scoring path was skipping the season's own
/// settings entirely.
/// </remarks>
public class MatchFormatTests
{
    private static AppSettings Settings(int defaultFrames) =>
        new() { DefaultFramesPerMatch = defaultFrames };

    [Fact]
    public void FallsBackToTheLeagueStandardWhenNothingIsSet()
    {
        var format = MatchFormat.For(null, null);

        Assert.Equal(15, format.TotalFrames);
        Assert.False(format.HasDoubles);
    }

    [Fact]
    public void UsesTheAppDefaultWhenTheSeasonSaysNothing()
    {
        var season = new Season { FramesPerMatch = 0 };

        Assert.Equal(11, MatchFormat.For(season, Settings(11)).TotalFrames);
    }

    [Fact]
    public void TheSeasonOverridesTheAppDefault()
    {
        // This is the case the live-scoring path used to miss: it read only the
        // app default, so a season on a different count opened the wrong card.
        var season = new Season { FramesPerMatch = 9 };

        Assert.Equal(9, MatchFormat.For(season, Settings(15)).TotalFrames);
    }

    [Fact]
    public void ADoublesSeasonDescribesItselfAsASplit()
    {
        var season = new Season
        {
            FramesPerMatch = 15,     // ignored: the split is more specific
            IncludeDoubles = true,
            SinglesFrameCount = 10,
            DoublesFrameCount = 2,
        };

        var format = MatchFormat.For(season, Settings(15));

        Assert.Equal(12, format.TotalFrames);
        Assert.Equal(10, format.SinglesFrames);
        Assert.Equal(2, format.DoublesFrames);
        Assert.True(format.HasDoubles);
    }

    [Fact]
    public void DoublesAreThePlayedAtTheEndOfTheNight()
    {
        var season = new Season
        {
            IncludeDoubles = true,
            SinglesFrameCount = 10,
            DoublesFrameCount = 2,
        };

        // 12 frames, so the doubles are frames 11 and 12.
        Assert.Equal(new[] { 11, 12 }, MatchFormat.For(season, Settings(15)).DoublesFrameNumbers());
    }

    [Fact]
    public void DoublesEnabledWithNoCountsIsNotASplit()
    {
        // The flag alone says nothing about how many, so the ordinary count
        // applies rather than collapsing the match to zero frames.
        var season = new Season
        {
            IncludeDoubles = true,
            SinglesFrameCount = 0,
            DoublesFrameCount = 0,
            FramesPerMatch = 15,
        };

        var format = MatchFormat.For(season, Settings(15));

        Assert.Equal(15, format.TotalFrames);
        Assert.False(format.HasDoubles);
        Assert.Empty(format.DoublesFrameNumbers());
    }

    [Fact]
    public void ASinglesSeasonHasNoDoublesFrames()
    {
        var format = MatchFormat.For(new Season { FramesPerMatch = 15 }, Settings(15));

        Assert.Empty(format.DoublesFrameNumbers());
        Assert.Equal(15, format.SinglesFrames);
    }

    [Fact]
    public void MatchesTheLeaguesCurrentSeason()
    {
        // WINTER 26-27 UNITED: FramesPerMatch 15, no doubles, app default 15.
        var season = new Season { Name = "WINTER 26-27 UNITED", FramesPerMatch = 15 };

        var format = MatchFormat.For(season, Settings(15));

        Assert.Equal(15, format.TotalFrames);
        Assert.False(format.HasDoubles);
        Assert.Equal("15 frames", format.ToString());
    }

    [Fact]
    public void DescribesItselfForAConfirmationDialog()
    {
        var doubles = new Season { IncludeDoubles = true, SinglesFrameCount = 10, DoublesFrameCount = 2 };

        Assert.Equal("12 frames (10 singles, 2 doubles)", MatchFormat.For(doubles, Settings(15)).ToString());
    }
}
