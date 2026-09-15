using Wdpl2.Domain.Fixtures;
using Wdpl2.Models;

namespace wdpl2.Tests;

/// <summary>
/// Pins where the match format comes from.
/// </summary>
/// <remarks>
/// One source only: the app's Settings. Seasons carry their own FramesPerMatch
/// and doubles fields and fixtures can carry pre-built frames, but neither is
/// consulted - a number settable in three places is a number nobody can be
/// sure of, and being wrong lands on a match night with captains waiting.
/// </remarks>
public class MatchFormatTests
{
    [Fact]
    public void ReadsTheFrameCountFromSettings()
    {
        var format = MatchFormat.From(new AppSettings { DefaultFramesPerMatch = 15, MaxFramesPerPlayer = 3 });

        Assert.Equal(15, format.TotalFrames);
        Assert.Equal(3, format.MaxFramesPerPlayer);
    }

    [Fact]
    public void ChangingSettingsChangesTheCard()
    {
        var format = MatchFormat.From(new AppSettings { DefaultFramesPerMatch = 9, MaxFramesPerPlayer = 2 });

        Assert.Equal(9, format.TotalFrames);
        Assert.Equal(2, format.MaxFramesPerPlayer);
    }

    [Fact]
    public void FallsBackToTheLeagueStandardWhenSettingsAreMissing()
    {
        var format = MatchFormat.From(null);

        Assert.Equal(15, format.TotalFrames);
        Assert.Equal(3, format.MaxFramesPerPlayer);
    }

    [Fact]
    public void IgnoresNonsenseValues()
    {
        // A zero or negative count would open a card with no frames on it.
        var format = MatchFormat.From(new AppSettings { DefaultFramesPerMatch = 0, MaxFramesPerPlayer = -1 });

        Assert.Equal(15, format.TotalFrames);
        Assert.Equal(3, format.MaxFramesPerPlayer);
    }

    [Fact]
    public void DescribesItselfForTheConfirmationDialog()
    {
        var format = MatchFormat.From(new AppSettings { DefaultFramesPerMatch = 15, MaxFramesPerPlayer = 3 });

        Assert.Equal("15 frames, max 3 per player", format.ToString());
    }
}
