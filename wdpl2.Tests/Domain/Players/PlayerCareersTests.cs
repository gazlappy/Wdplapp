using Wdpl2.Domain.Players;
using Wdpl2.Models;

namespace wdpl2.Tests;

/// <summary>
/// Who the analytics think a player is.
/// </summary>
/// <remarks>
/// Four pages used to answer this separately and all four were wrong: career
/// stats dropped anyone who had only played one season, two pages threw away
/// every row but the biggest when two people shared a name, and the profile
/// page merged people by name whether or not the league had said they were the
/// same. These are those four bugs, kept where they cannot come back.
/// </remarks>
public class PlayerCareersTests
{
    private static readonly Guid LastSeason = Guid.NewGuid();
    private static readonly Guid ThisSeason = Guid.NewGuid();

    private static LeagueData League()
    {
        var league = new LeagueData();
        league.Seasons.Add(new Season { Id = LastSeason, Name = "2024/25", StartDate = new DateTime(2024, 9, 1) });
        league.Seasons.Add(new Season { Id = ThisSeason, Name = "2025/26", StartDate = new DateTime(2025, 9, 1) });
        return league;
    }

    private static Player Add(LeagueData league, string first, string last, Guid season, Guid? identity = null)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            SeasonId = season,
            GlobalPlayerId = identity,
        };

        league.Players.Add(player);
        return player;
    }

    /// <summary>
    /// A player who has only ever played one season still has a career.
    /// </summary>
    /// <remarks>
    /// Career stats asked for a GlobalPlayerId, which only a linked row has -
    /// so on this league 669 of 2,292 rows had no career at all.
    /// </remarks>
    [Fact]
    public void All_IncludesSomebodyWhoHasNeverBeenLinked()
    {
        var league = League();
        Add(league, "Dave", "Marsh", ThisSeason);

        var career = Assert.Single(PlayerCareers.All(league));

        Assert.Equal("DAVE MARSH", career.Name);
        Assert.Equal(1, career.Seasons);
        Assert.False(career.IsLinked);
    }

    [Fact]
    public void All_PutsLinkedRowsIntoOneCareer()
    {
        var league = League();
        var first = Add(league, "Dave", "Marsh", LastSeason);
        first.GlobalPlayerId = first.Id;
        Add(league, "Dave", "Marsh", ThisSeason, identity: first.Id);

        var career = Assert.Single(PlayerCareers.All(league));

        Assert.Equal(2, career.Rows.Count);
        Assert.Equal(2, career.Seasons);
        Assert.True(career.IsLinked);
    }

    /// <summary>
    /// Two people of the same name are two people, and both keep their record.
    /// </summary>
    /// <remarks>
    /// The old pages deduped by name and kept whichever set had more rows, so
    /// the other player's whole record simply vanished from the list.
    /// </remarks>
    [Fact]
    public void All_KeepsTwoPeopleWhoShareAName()
    {
        var league = League();
        var busy = Add(league, "Dave", "Smith", LastSeason);
        busy.GlobalPlayerId = busy.Id;
        Add(league, "Dave", "Smith", ThisSeason, identity: busy.Id);

        var other = Add(league, "Dave", "Smith", ThisSeason);

        var careers = PlayerCareers.All(league);

        Assert.Equal(2, careers.Count);
        Assert.Contains(careers, c => c.Id == busy.Id && c.Rows.Count == 2);
        Assert.Contains(careers, c => c.Id == other.Id && c.Rows.Count == 1);
    }

    [Fact]
    public void All_AccountsForEveryNamedRow()
    {
        var league = League();
        var linked = Add(league, "Dave", "Marsh", LastSeason);
        linked.GlobalPlayerId = linked.Id;
        Add(league, "Dave", "Marsh", ThisSeason, identity: linked.Id);
        Add(league, "Gary", "Wilkes", ThisSeason);
        Add(league, "Sam", "Dowding", LastSeason);

        var careers = PlayerCareers.All(league);

        // Nothing is dropped: the rows in the careers are all the rows there are.
        Assert.Equal(league.Players.Count, careers.Sum(c => c.Rows.Count));
    }

    [Fact]
    public void All_LeavesOutARowWithNoName()
    {
        var league = League();
        Add(league, "", "", ThisSeason);
        Add(league, "Dave", "Marsh", ThisSeason);

        Assert.Single(PlayerCareers.All(league));
    }

    /// <summary>
    /// Somebody on the books three ways is known by the fullest of them.
    /// </summary>
    /// <remarks>
    /// All three arrive in capitals, because that is how a name is stored - so
    /// the only thing left to choose between them is which says the most.
    /// </remarks>
    [Fact]
    public void All_IsKnownByTheFullestSpelling()
    {
        var league = League();
        var first = Add(league, "DAVID", "HOWELL", LastSeason);
        first.GlobalPlayerId = first.Id;
        Add(league, "David", "Howell", ThisSeason, identity: first.Id);
        Add(league, "Dave", "Howell", ThisSeason, identity: first.Id);

        Assert.Equal("DAVID HOWELL", Assert.Single(PlayerCareers.All(league)).Name);
    }

    // -------------------------------------------------------------------- one

    [Fact]
    public void For_FindsACareerByItsIdentity()
    {
        var league = League();
        var first = Add(league, "Dave", "Marsh", LastSeason);
        first.GlobalPlayerId = first.Id;
        Add(league, "Dave", "Marsh", ThisSeason, identity: first.Id);

        var career = PlayerCareers.For(league, first.Id);

        Assert.NotNull(career);
        Assert.Equal(2, career!.Rows.Count);
    }

    /// <summary>
    /// A row's own id finds the career it belongs to.
    /// </summary>
    /// <remarks>
    /// Pages are navigated to with whichever of the two the caller happened to
    /// hold, so both have to work or a profile opens empty.
    /// </remarks>
    [Fact]
    public void For_FindsACareerFromAnyRowInIt()
    {
        var league = League();
        var first = Add(league, "Dave", "Marsh", LastSeason);
        first.GlobalPlayerId = first.Id;
        var second = Add(league, "Dave", "Marsh", ThisSeason, identity: first.Id);

        var career = PlayerCareers.For(league, second.Id);

        Assert.NotNull(career);
        Assert.Equal(first.Id, career!.Id);
        Assert.Equal(2, career.Rows.Count);
    }

    /// <summary>
    /// A profile shows the person asked for, not everybody of that name.
    /// </summary>
    /// <remarks>
    /// This is the one that overrode the secretary: having decided two Dave
    /// Smiths were different people and deliberately not linked them, the
    /// profile page swept them together anyway on the strength of the name.
    /// </remarks>
    [Fact]
    public void For_DoesNotSweepInSomebodyOfTheSameName()
    {
        var league = League();
        var one = Add(league, "Dave", "Smith", ThisSeason);
        Add(league, "Dave", "Smith", ThisSeason);

        var career = PlayerCareers.For(league, one.Id);

        Assert.NotNull(career);
        Assert.Equal(one.Id, Assert.Single(career!.Rows).Id);
    }

    [Fact]
    public void For_SaysNothingAboutSomebodyWhoIsNotThere()
    {
        Assert.Null(PlayerCareers.For(League(), Guid.NewGuid()));
    }
}
