using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// A group drawn out and played as a knockout has to reach the website as one.
/// </summary>
/// <remarks>
/// The night is run on the website and collected back into the app, and the app
/// is what publishes. If the generator only knew how to draw a league table, the
/// competition people played would not be the one they could read afterwards.
/// </remarks>
public class KnockoutGroupPublishTests
{
    private static readonly Guid Ann = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly Guid Cal = Guid.NewGuid();

    /// <summary>A season with one competition, one group, played as a knockout.</summary>
    private static (LeagueData League, WebsiteSettings Settings) Played(bool finished = true)
    {
        var seasonId = Guid.NewGuid();
        var league = new LeagueData();
        league.Seasons.Add(new Season { Id = seasonId, Name = "2026", IsActive = true });

        league.Players.Add(new Player { Id = Ann, FirstName = "Ann", LastName = "Reid", SeasonId = seasonId });
        league.Players.Add(new Player { Id = Bob, FirstName = "Bob", LastName = "Crane", SeasonId = seasonId });
        league.Players.Add(new Player { Id = Cal, FirstName = "Cal", LastName = "Dow", SeasonId = seasonId });

        var group = new CompetitionGroup
        {
            Name = "Group A",
            GroupNumber = 1,
            GroupRound = 1,
            ParticipantIds = { Ann, Bob, Cal },
            DrawOrder = { Ann, Cal, Bob },
        };

        // Ann drawn first and Bob third, so they meet; Cal drawn second has a bye.
        group.Matches.Add(new CompetitionMatch
        {
            RoundNumber = 1, Slot = 0,
            Participant1Id = Ann, Participant2Id = Bob,
            Participant1Score = 2, Participant2Score = 1,
            WinnerId = Ann, IsComplete = true,
        });
        group.Matches.Add(new CompetitionMatch
        {
            RoundNumber = 1, Slot = 1,
            Participant1Id = Cal, Participant2Id = null,
            WinnerId = Cal, IsComplete = true,
        });
        group.Matches.Add(new CompetitionMatch
        {
            RoundNumber = 2, Slot = 0,
            Participant1Id = Ann, Participant2Id = Cal,
            Participant1Score = finished ? 2 : 0, Participant2Score = 0,
            WinnerId = finished ? Ann : null, IsComplete = finished,
        });

        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            Name = "Singles Cup",
            Format = CompetitionFormat.SinglesGroupStage,
            BestOf = 3,
            ShowOnWebsite = true,
            Status = CompetitionStatus.InProgress,
        };
        competition.Groups.Add(group);
        league.Competitions.Add(competition);

        var settings = new WebsiteSettings
        {
            LeagueName = "Test League",
            SelectedTemplate = "modern",
            ShowCompetitions = true,
        };

        return (league, settings);
    }

    private static string CompetitionsPage(LeagueData league, WebsiteSettings settings)
    {
        var pages = new WebsiteGenerator(league, settings).GenerateWebsite();

        var key = pages.Keys.FirstOrDefault(k => k.Contains("competition", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(key);

        return pages[key!];
    }

    [Fact]
    public void ThePublishedPageShowsTheTiesThatWerePlayed()
    {
        var (league, settings) = Played();

        var html = CompetitionsPage(league, settings);

        Assert.Contains("Ann Reid", html);
        Assert.Contains("Bob Crane", html);
        Assert.Contains("Cal Dow", html);

        // The scoreline of the tie they actually played.
        Assert.Contains("2&ndash;1", html);
    }

    [Fact]
    public void RoundsAreNamedCountingBackFromTheFinal()
    {
        var (league, settings) = Played();

        var html = CompetitionsPage(league, settings);

        Assert.Contains("Final", html);
        Assert.Contains("Semi-finals", html);
    }

    [Fact]
    public void AByeIsShownAsOne()
    {
        var (league, settings) = Played();

        var html = CompetitionsPage(league, settings);

        Assert.Contains("bye", html);
    }

    [Fact]
    public void TheWinnerOfTheGroupIsMarkedThrough()
    {
        var (league, settings) = Played();

        var html = CompetitionsPage(league, settings);

        // The badge's text, not its class: the class also appears in the page's
        // own CSS, so matching it would pass whether anybody was marked or not.
        Assert.Contains("Through</span>", html);
    }

    [Fact]
    public void NobodyIsMarkedThroughUntilTheFinalIsPlayed()
    {
        var (league, settings) = Played(finished: false);

        var html = CompetitionsPage(league, settings);

        Assert.DoesNotContain("Through</span>", html);
    }

    [Fact]
    public void AKnockoutGroupIsNotGivenALeagueTable()
    {
        var (league, settings) = Played();

        var html = CompetitionsPage(league, settings);

        // "Pts" is the giveaway column of the round-robin table.
        Assert.DoesNotContain("<th>Pts</th>", html);
    }
}
