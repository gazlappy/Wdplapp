using Wdpl2.Domain.Players;
using Wdpl2.Models;

namespace wdpl2.Tests;

/// <summary>
/// The league writes players' names in capitals.
/// </summary>
/// <remarks>
/// Held on the model rather than at the twenty-odd places a player is created,
/// so it is true of every one of them - the six importers, a captain's addition
/// collected from the website, a season copied forward, a name typed into the
/// Players page, and whatever gets written next.
/// </remarks>
public class PlayerNameCaseTests
{
    [Theory]
    [InlineData("Dave", "Marsh", "DAVE", "MARSH")]
    [InlineData("dave", "marsh", "DAVE", "MARSH")]
    [InlineData("DAVE", "MARSH", "DAVE", "MARSH")]
    [InlineData("Ryan", "McLoughlin", "RYAN", "MCLOUGHLIN")]
    [InlineData("Jo", "O'Brien", "JO", "O'BRIEN")]
    [InlineData("", "", "", "")]
    public void APlayersNameIsStoredInCapitals(string first, string last, string wantFirst, string wantLast)
    {
        var player = new Player { FirstName = first, LastName = last };

        Assert.Equal(wantFirst, player.FirstName);
        Assert.Equal(wantLast, player.LastName);
    }

    [Fact]
    public void TheWholeNameFollowsToo()
    {
        var player = new Player { FirstName = "Dave", LastName = "Marsh" };
        Assert.Equal("DAVE MARSH", player.FullName);

        player.Name = "Gary Wilkes";
        Assert.Equal("GARY WILKES", player.Name);
    }

    /// <summary>
    /// It holds however the player was made, not only through an initialiser.
    /// </summary>
    [Fact]
    public void ANameSetLaterIsCapitalisedAsWell()
    {
        var player = new Player();
        player.FirstName = "Sam";
        player.LastName = "Dowding";

        Assert.Equal("SAM DOWDING", player.FullName);
    }

    [Fact]
    public void ANullNameIsEmptyRatherThanNull()
    {
        var player = new Player { FirstName = null!, LastName = null! };

        Assert.Equal("", player.FirstName);
        Assert.Equal("", player.LastName);
    }

    // ------------------------------------------------- what it must not break

    /// <summary>
    /// Finding one person recorded twice does not care about case, and must
    /// not start caring now that half a league is in capitals and half is not.
    /// </summary>
    [Fact]
    public void LinkingStillPairsACapitalisedNameWithAMixedOne()
    {
        var seasonOne = Guid.NewGuid();
        var seasonTwo = Guid.NewGuid();

        var league = new LeagueData();
        league.Seasons.Add(new Season { Id = seasonOne, Name = "2024/25", StartDate = new DateTime(2024, 9, 1) });
        league.Seasons.Add(new Season { Id = seasonTwo, Name = "2025/26", StartDate = new DateTime(2025, 9, 1) });

        // Both end up in capitals now, which is the point - they were the same
        // person all along and the case was never what separated them.
        league.Players.Add(new Player { Id = Guid.NewGuid(), FirstName = "SCOTT", LastName = "BATES", SeasonId = seasonOne });
        league.Players.Add(new Player { Id = Guid.NewGuid(), FirstName = "Scott", LastName = "Bates", SeasonId = seasonTwo });

        var found = Assert.Single(PlayerLinks.Find(league));
        Assert.Equal(PlayerLinks.Confidence.Certain, found.Confidence);
    }

    /// <summary>
    /// Two people are still two people, capitals or not.
    /// </summary>
    [Fact]
    public void LinkingStillKeepsTwoPeopleApart()
    {
        var season = Guid.NewGuid();

        var league = new LeagueData();
        league.Seasons.Add(new Season { Id = season, Name = "2025/26", StartDate = new DateTime(2025, 9, 1) });
        league.Players.Add(new Player { Id = Guid.NewGuid(), FirstName = "Jack", LastName = "Martin", SeasonId = season });
        league.Players.Add(new Player { Id = Guid.NewGuid(), FirstName = "JOEL", LastName = "MARTIN", SeasonId = season });

        Assert.Empty(PlayerLinks.Find(league));
    }

    /// <summary>
    /// Suggesting who a captain's new player might be still works.
    /// </summary>
    /// <remarks>
    /// Captains type in lower case on a phone. The squad they are matched
    /// against is now in capitals, so a matcher that compared them directly
    /// would have stopped finding anybody.
    /// </remarks>
    [Fact]
    public void ACaptainsLowerCaseNameStillFindsTheirPlayer()
    {
        var season = Guid.NewGuid();
        var squad = new List<Player>
        {
            new() { Id = Guid.NewGuid(), FirstName = "Ryan", LastName = "Foulkes", SeasonId = season },
        };

        var found = Wdpl2.Services.Web.CaptainPlayerMatcher.Suggest("ryan foulkes", season, squad);

        Assert.True(Assert.Single(found).IsExact);
    }
}
