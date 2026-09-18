using Wdpl2.Domain.Fixtures;
using Wdpl2.Domain.Players;
using Wdpl2.Models;

namespace wdpl2.Tests;

/// <summary>
/// Finding one person recorded more than once, and tying those rows together.
/// </summary>
/// <remarks>
/// Linking writes one field and deletes nothing, so the risk is not in what it
/// changes - it is in what it claims. A wrong link merges two people's careers
/// in every stat the league publishes, so the finder has to be careful about
/// what it proposes, and the link has to be undoable.
/// </remarks>
public class PlayerLinksTests
{
    private static readonly Guid LastSeason = Guid.NewGuid();
    private static readonly Guid ThisSeason = Guid.NewGuid();

    private static LeagueData League()
    {
        var league = new LeagueData();
        league.Seasons.Add(new Season { Id = LastSeason, Name = "2024/25", StartDate = new DateTime(2024, 9, 1) });
        league.Seasons.Add(new Season { Id = ThisSeason, Name = "2025/26", StartDate = new DateTime(2025, 9, 1), IsActive = true });
        return league;
    }

    private static Player Add(LeagueData league, string first, string last, Guid season, Guid? team = null)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            SeasonId = season,
            TeamId = team,
        };

        league.Players.Add(player);
        return player;
    }

    // ------------------------------------------------------------------ find

    [Fact]
    public void Find_PairsTheSameNameAcrossSeasons()
    {
        var league = League();
        Add(league, "Dave", "Marsh", LastSeason);
        Add(league, "Dave", "Marsh", ThisSeason);

        var found = Assert.Single(PlayerLinks.Find(league));

        Assert.Equal(PlayerLinks.Confidence.Certain, found.Confidence);
        Assert.Equal(2, found.Players.Count);

        // One row each season is exactly right - linking is the whole fix.
        Assert.True(found.Tidy);
    }

    [Fact]
    public void Find_SaysWhenASeasonHasTwoRows()
    {
        var league = League();
        Add(league, "Dave", "Marsh", ThisSeason);
        Add(league, "Dave", "Marsh", ThisSeason);

        var found = Assert.Single(PlayerLinks.Find(league));

        Assert.False(found.Tidy);
        Assert.Equal(1, found.SeasonsWithClash);
    }

    [Fact]
    public void Find_IgnoresPeopleAlreadyLinked()
    {
        var league = League();
        var first = Add(league, "Dave", "Marsh", LastSeason);
        var second = Add(league, "Dave", "Marsh", ThisSeason);

        first.GlobalPlayerId = first.Id;
        second.GlobalPlayerId = first.Id;

        Assert.Empty(PlayerLinks.Find(league));
    }

    [Fact]
    public void Find_StillCatchesTwoLinkedRowsInOneSeason()
    {
        var league = League();
        var first = Add(league, "Dave", "Marsh", ThisSeason);
        var second = Add(league, "Dave", "Marsh", ThisSeason);

        // Linked, but both in the same season - which is the thing that is wrong.
        first.GlobalPlayerId = first.Id;
        second.GlobalPlayerId = first.Id;

        Assert.Single(PlayerLinks.Find(league));
    }

    [Fact]
    public void Find_PairsABareInitialWithTheOnlyNameItCanMean()
    {
        var league = League();
        Add(league, "D", "Marsh", LastSeason);
        Add(league, "Dave", "Marsh", ThisSeason);

        var found = Assert.Single(PlayerLinks.Find(league));

        Assert.Equal(PlayerLinks.Confidence.Likely, found.Confidence);

        // The fuller spelling is the one worth being known by.
        Assert.Equal("DAVE MARSH", found.Name);
    }

    /// <summary>
    /// An initial that could mean two of them means neither.
    /// </summary>
    /// <remarks>
    /// The books hold a Jamie Smith and a Jez Smith. Which of them J Smith is
    /// cannot be worked out from the name, and guessing would not only get it
    /// wrong half the time - it would drag Jamie and Jez into one set as well.
    /// </remarks>
    [Fact]
    public void Find_LeavesAnAmbiguousInitialAlone()
    {
        var league = League();
        Add(league, "J", "Smith", LastSeason);
        Add(league, "Jamie", "Smith", ThisSeason);
        Add(league, "Jez", "Smith", ThisSeason);

        Assert.Empty(PlayerLinks.Find(league));
    }

    [Fact]
    public void Find_PairsAOneLetterTypo()
    {
        var league = League();
        Add(league, "Ryan", "Foulkes", ThisSeason);
        Add(league, "Rian", "Foulkes", ThisSeason);

        var found = Assert.Single(PlayerLinks.Find(league));
        Assert.Equal(PlayerLinks.Confidence.Likely, found.Confidence);
    }

    [Fact]
    public void Find_PairsANameWithItsShortForm()
    {
        var league = League();
        Add(league, "Michael", "Griffiths", LastSeason);
        Add(league, "Mike", "Griffiths", ThisSeason);

        var found = Assert.Single(PlayerLinks.Find(league));
        Assert.Equal(PlayerLinks.Confidence.Likely, found.Confidence);
        Assert.Equal(2, found.Players.Count);
    }

    [Fact]
    public void Find_PairsANameWithItsOwnBeginning()
    {
        var league = League();
        Add(league, "Trevor", "Hull", LastSeason);
        Add(league, "Trev", "Hull", ThisSeason);

        Assert.Single(PlayerLinks.Find(league));
    }

    /// <summary>
    /// Sharing a surname and a first letter means nothing at all.
    /// </summary>
    /// <remarks>
    /// Every one of these is two people on this league's books, and every one
    /// of them was offered as a match by the first version of the finder. A
    /// wrong link merges two careers in every stat the league publishes, which
    /// is far worse than missing one - so these are the cases that matter.
    /// </remarks>
    [Theory]
    [InlineData("Jack", "Joel", "Martin")]
    [InlineData("Dave", "Donna", "Smith")]
    [InlineData("Linda", "Luke", "Perry")]
    [InlineData("Liam", "Lisa", "Moore")]
    [InlineData("Mark", "Mike", "Kerslake")]
    [InlineData("Mike", "Matt", "Smith")]
    [InlineData("Justin", "Jason", "Jenkins")]
    [InlineData("Darren", "David", "Radford")]
    [InlineData("Jeremy", "John", "Roberts")]
    [InlineData("Kelly", "Kim", "Thorne")]
    [InlineData("Aiden", "Ali", "Hodge")]
    [InlineData("Jamie", "Jez", "Smith")]
    public void Find_KeepsTwoPeopleApart(string first, string second, string surname)
    {
        var league = League();
        Add(league, first, surname, ThisSeason);
        Add(league, second, surname, ThisSeason);

        Assert.Empty(PlayerLinks.Find(league));
    }

    /// <summary>
    /// Two short forenames one letter apart are usually two people.
    /// </summary>
    /// <remarks>
    /// Brothers on the same team are the normal case, and proposing to link
    /// them is worse than missing a typo: a missed typo is a career counted in
    /// halves, a wrong link is two careers counted as one.
    /// </remarks>
    [Theory]
    [InlineData("Jon", "Ian")]
    [InlineData("Kim", "Kit")]
    [InlineData("Dan", "Don")]
    [InlineData("Jan", "Jon")]
    public void Find_LeavesShortNamesAlone(string first, string second)
    {
        var league = League();
        Add(league, first, "Marsh", ThisSeason);
        Add(league, second, "Marsh", ThisSeason);

        Assert.Empty(PlayerLinks.Find(league));
    }

    /// <summary>A short form is not a licence to swallow a longer name.</summary>
    [Fact]
    public void Find_DoesNotLetATwoLetterStartSwallowAName()
    {
        var league = League();
        Add(league, "Jo", "Martin", LastSeason);
        Add(league, "Joel", "Martin", ThisSeason);

        Assert.Empty(PlayerLinks.Find(league));
    }

    [Fact]
    public void Find_DoesNotPutOnePlayerInTwoGroups()
    {
        var league = League();
        Add(league, "Dave", "Marsh", LastSeason);
        Add(league, "Dave", "Marsh", ThisSeason);
        Add(league, "D", "Marsh", ThisSeason);

        var found = Assert.Single(PlayerLinks.Find(league));
        Assert.Equal(3, found.Players.Count);
        Assert.Equal(PlayerLinks.Confidence.Likely, found.Confidence);
    }

    // ------------------------------------------------------------------ link

    [Fact]
    public void Link_TiesRowsTogetherAndDeletesNothing()
    {
        var league = League();
        var older = Add(league, "Dave", "Marsh", LastSeason);
        var newer = Add(league, "Dave", "Marsh", ThisSeason);

        var report = PlayerLinks.Link(league, newer.Id, new[] { older.Id });

        Assert.Equal(2, report.Linked);
        Assert.Equal(2, league.Players.Count);
        Assert.Equal(newer.Id, older.GlobalPlayerId);
        Assert.Equal(newer.Id, newer.GlobalPlayerId);
    }

    [Fact]
    public void Link_LeavesEveryFrameExactlyWhereItWas()
    {
        var league = League();
        var older = Add(league, "Dave", "Marsh", LastSeason);
        var newer = Add(league, "Dave", "Marsh", ThisSeason);

        var fixture = new Fixture { Id = Guid.NewGuid(), SeasonId = LastSeason };
        fixture.Frames.Add(new FrameResult { Number = 1, HomePlayerId = older.Id, Winner = FrameWinner.Home });
        league.Fixtures.Add(fixture);

        PlayerLinks.Link(league, newer.Id, new[] { older.Id });

        // The whole point: last season's frame still belongs to last season's row.
        Assert.Equal(older.Id, fixture.Frames[0].HomePlayerId);
    }

    [Fact]
    public void Link_KeepsBothRowsWhenTheyShareASeason()
    {
        var league = League();
        var first = Add(league, "Dave", "Marsh", ThisSeason);
        var second = Add(league, "Dave", "Marsh", ThisSeason);

        PlayerLinks.Link(league, first.Id, new[] { second.Id });

        // Their record is counted together, but the roster still has two rows -
        // which is why the page says so rather than calling it fixed.
        Assert.Equal(2, league.Players.Count);
        Assert.Equal(first.Id, second.GlobalPlayerId);
    }

    /// <summary>
    /// Linking keeps an existing identity rather than inventing a new one.
    /// </summary>
    /// <remarks>
    /// A player already linked across three seasons who picks up a fourth must
    /// not have the other three quietly detached from their career.
    /// </remarks>
    [Fact]
    public void Link_KeepsTheIdentityTheKeeperAlreadyHad()
    {
        var league = League();
        var identity = Guid.NewGuid();

        var keep = Add(league, "Dave", "Marsh", ThisSeason);
        keep.GlobalPlayerId = identity;

        var older = Add(league, "Dave", "Marsh", LastSeason);

        PlayerLinks.Link(league, keep.Id, new[] { older.Id });

        Assert.Equal(identity, keep.GlobalPlayerId);
        Assert.Equal(identity, older.GlobalPlayerId);
    }

    [Fact]
    public void Link_CountsOnlyWhatItActuallyChanged()
    {
        var league = League();
        var keep = Add(league, "Dave", "Marsh", ThisSeason);
        var older = Add(league, "Dave", "Marsh", LastSeason);

        keep.GlobalPlayerId = keep.Id;
        older.GlobalPlayerId = keep.Id;

        Assert.Equal(0, PlayerLinks.Link(league, keep.Id, new[] { older.Id }).Linked);
    }

    [Fact]
    public void Link_RefusesNonsense()
    {
        var league = League();
        var only = Add(league, "Dave", "Marsh", ThisSeason);

        Assert.Equal(0, PlayerLinks.Link(league, only.Id, Array.Empty<Guid>()).Linked);
        Assert.Equal(0, PlayerLinks.Link(league, Guid.NewGuid(), new[] { only.Id }).Linked);
        Assert.Null(only.GlobalPlayerId);
    }

    [Fact]
    public void Unlink_TakesThemBackApart()
    {
        var league = League();
        var older = Add(league, "Dave", "Marsh", LastSeason);
        var newer = Add(league, "Dave", "Marsh", ThisSeason);

        PlayerLinks.Link(league, newer.Id, new[] { older.Id });
        var report = PlayerLinks.Unlink(league, new[] { older.Id, newer.Id });

        Assert.Equal(2, report.Linked);
        Assert.Null(older.GlobalPlayerId);
        Assert.Null(newer.GlobalPlayerId);

        // And so the finder offers them again.
        Assert.Single(PlayerLinks.Find(league));
    }

    // -------------------------------------------------------------- link all

    [Fact]
    public void LinkAll_TakesTheSetsWithNothingToDecide()
    {
        var league = League();

        // One row per season: nothing to choose between them.
        Add(league, "Dave", "Marsh", LastSeason);
        Add(league, "Dave", "Marsh", ThisSeason);

        // Two rows in one season: left for a person to look at.
        var clashOne = Add(league, "Gary", "Wilkes", ThisSeason);
        var clashTwo = Add(league, "Gary", "Wilkes", ThisSeason);

        var report = PlayerLinks.LinkAll(league, PlayerLinks.Find(league));

        Assert.Equal(1, report.Groups);
        Assert.Equal(2, report.Linked);

        Assert.Null(clashOne.GlobalPlayerId);
        Assert.Null(clashTwo.GlobalPlayerId);

        // The clash is all that is left to look at.
        var left = Assert.Single(PlayerLinks.Find(league));
        Assert.False(left.Tidy);
    }

    [Fact]
    public void LinkAll_KnowsTheFullestSpelling()
    {
        var league = League();
        var initial = Add(league, "D", "Marsh", LastSeason);
        var full = Add(league, "Dave", "Marsh", ThisSeason);

        PlayerLinks.LinkAll(league, PlayerLinks.Find(league));

        Assert.Equal(full.Id, full.GlobalPlayerId);
        Assert.Equal(full.Id, initial.GlobalPlayerId);
    }

    // ------------------------------------------------------------ references

    /// <summary>
    /// Everywhere a player id is stored, counted in one walk.
    /// </summary>
    /// <remarks>
    /// This is shown so the secretary can tell which row a season actually
    /// played. It is one pass for everybody because asking per row - which is
    /// what the first version did - turned a page of four hundred names into
    /// forty million passes over the fixtures and hung the app.
    /// </remarks>
    [Fact]
    public void CountReferences_FindsEveryPlaceAPlayerIsNamed()
    {
        var league = League();
        var player = Add(league, "Dave", "Marsh", ThisSeason);
        var other = Add(league, "Gary", "Wilkes", ThisSeason);

        var fixture = new Fixture { Id = Guid.NewGuid(), SeasonId = ThisSeason };
        fixture.Frames.Add(new FrameResult { Number = 1, HomePlayerId = player.Id });
        fixture.Frames.Add(new FrameResult { Number = 2, AwayPlayerId = player.Id });
        fixture.Frames.Add(new FrameResult { Number = 3, IsDoubles = true, HomePlayer2Id = player.Id });
        fixture.Frames.Add(new FrameResult { Number = 4, IsDoubles = true, AwayPlayer2Id = player.Id });
        league.Fixtures.Add(fixture);

        league.Teams.Add(new Team { Id = Guid.NewGuid(), SeasonId = ThisSeason, CaptainPlayerId = player.Id });
        league.DoublesPairings.Add(new DoublesPairing { SeasonId = ThisSeason, Player1Id = player.Id });

        var group = new CompetitionGroup { Id = Guid.NewGuid(), Name = "A", OrganiserParticipantId = player.Id };
        group.ParticipantIds.Add(player.Id);
        group.Matches.Add(new CompetitionMatch { Participant1Id = player.Id, WinnerId = player.Id });
        group.Standings.Add(new GroupStanding { ParticipantId = player.Id, Position = 1 });

        var comp = new Competition { Id = Guid.NewGuid(), SeasonId = ThisSeason, Name = "Singles" };
        comp.ParticipantIds.Add(player.Id);
        comp.NoShowIds.Add(player.Id);
        comp.DoublesTeams.Add(new DoublesTeam { Id = Guid.NewGuid(), Player1Id = player.Id, Player2Id = other.Id });
        comp.Rounds.Add(new CompetitionRound
        {
            RoundNumber = 1,
            OrganiserParticipantId = player.Id,
            Matches = { new CompetitionMatch { Participant2Id = player.Id } },
        });
        comp.Groups.Add(group);
        league.Competitions.Add(comp);

        var counts = PlayerLinks.CountReferences(league);

        // 4 frames, captain, doubles pairing, 2 competition lists, doubles team,
        // round organiser, round match, group organiser, group list, 2 in the
        // group match, group standing.
        Assert.Equal(16, counts[player.Id]);
        Assert.Equal(1, counts[other.Id]);
    }

    [Fact]
    public void CountReferences_SaysNothingAboutAPlayerNobodyNames()
    {
        var league = League();
        var player = Add(league, "Dave", "Marsh", ThisSeason);

        Assert.False(PlayerLinks.CountReferences(league).ContainsKey(player.Id));
    }
}
