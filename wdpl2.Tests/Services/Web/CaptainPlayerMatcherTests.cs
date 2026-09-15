using Wdpl2.Models;
using Wdpl2.Services.Web;

namespace wdpl2.Tests;

/// <summary>
/// Matching proposes; it never decides. These pin what gets offered.
/// </summary>
public class CaptainPlayerMatcherTests
{
    private static readonly Guid Season = Guid.NewGuid();

    private static Player Make(string first, string last, Guid? teamId = null, Guid? seasonId = null) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = first,
        LastName = last,
        TeamId = teamId,
        SeasonId = seasonId ?? Season,
    };

    [Fact]
    public void SingleWord_MatchesEitherPartOfAName()
    {
        // The case that started this: "Test test" on file with no team, and a
        // captain who typed "test" on the night.
        var onFile = Make("Test", "test");

        var found = CaptainPlayerMatcher.Suggest("test", Season, new[] { onFile });

        Assert.Single(found);
        Assert.Equal(onFile.Id, found[0].Player.Id);
        Assert.False(found[0].IsExact);
    }

    [Fact]
    public void FullName_MatchesRegardlessOfCaseAndSpacing()
    {
        var onFile = Make("Dave", "Smith");

        var found = CaptainPlayerMatcher.Suggest("  dave   SMITH ", Season, new[] { onFile });

        Assert.Single(found);
        Assert.True(found[0].IsExact);
    }

    [Fact]
    public void ExactMatchesAreOfferedFirst()
    {
        var exact = Make("Dave", "Smith");
        var partial = Make("Dave", "Jones");

        var found = CaptainPlayerMatcher.Suggest("Dave Smith", Season, new[] { partial, exact });

        Assert.Equal(exact.Id, found[0].Player.Id);
        Assert.True(found[0].IsExact);
    }

    [Fact]
    public void PlayersWithATeamAreNotOfferedByDefault()
    {
        var placed = Make("Dave", "Smith", teamId: Guid.NewGuid());

        Assert.Empty(CaptainPlayerMatcher.Suggest("Dave Smith", Season, new[] { placed }));

        // ...but can be asked for, for a player moving mid-season.
        Assert.Single(CaptainPlayerMatcher.Suggest(
            "Dave Smith", Season, new[] { placed }, unassignedOnly: false));
    }

    [Fact]
    public void OtherSeasonsAreNeverOffered()
    {
        var elsewhere = Make("Dave", "Smith", seasonId: Guid.NewGuid());

        Assert.Empty(CaptainPlayerMatcher.Suggest("Dave Smith", Season, new[] { elsewhere }));
    }

    [Fact]
    public void ADifferentNameIsNotAMatch()
    {
        var onFile = Make("Dave", "Smith");

        Assert.Empty(CaptainPlayerMatcher.Suggest("Gary Deville", Season, new[] { onFile }));
        Assert.Empty(CaptainPlayerMatcher.Suggest("   ", Season, new[] { onFile }));
    }

    [Fact]
    public void SingleWordDoesNotMatchAPartialSpelling()
    {
        // "Dav" is not "Dave"; offering it would invite a wrong merge.
        var onFile = Make("Dave", "Smith");

        Assert.Empty(CaptainPlayerMatcher.Suggest("Dav", Season, new[] { onFile }));
    }
}
