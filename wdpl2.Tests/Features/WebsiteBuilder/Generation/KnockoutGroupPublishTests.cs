using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// What a knockout group publishes: who came through, and not the ties.
/// </summary>
/// <remarks>
/// The competitions page says where a competition stands. The ties themselves
/// belong on the card that was played — listing every scoreline for eight
/// groups buries the one thing anybody came to the page to read.
/// </remarks>
public class KnockoutGroupPublishTests
{
    private static readonly Guid Ann = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly Guid Cal = Guid.NewGuid();
    private static readonly Guid Dee = Guid.NewGuid();

    /// <summary>
    /// A season with one group, played as a knockout.
    /// </summary>
    /// <param name="oneRound">
    /// True for the shape a group takes when two go through: a single round of
    /// two ties, no final, and two qualifiers.
    /// </param>
    private static (LeagueData League, WebsiteSettings Settings) Played(
        bool oneRound = true, bool finished = true, int through = 2)
    {
        var seasonId = Guid.NewGuid();
        var league = new LeagueData();
        league.Seasons.Add(new Season { Id = seasonId, Name = "2026", IsActive = true });

        league.Players.Add(new Player { Id = Ann, FirstName = "Ann", LastName = "Reid", SeasonId = seasonId });
        league.Players.Add(new Player { Id = Bob, FirstName = "Bob", LastName = "Crane", SeasonId = seasonId });
        league.Players.Add(new Player { Id = Cal, FirstName = "Cal", LastName = "Dow", SeasonId = seasonId });
        league.Players.Add(new Player { Id = Dee, FirstName = "Dee", LastName = "Marsh", SeasonId = seasonId });

        var group = new CompetitionGroup
        {
            Name = "Group A",
            GroupNumber = 1,
            GroupRound = 1,
            ParticipantIds = { Ann, Bob, Cal, Dee },
            DrawOrder = { Ann, Cal, Bob, Dee },
        };

        group.Matches.Add(new CompetitionMatch
        {
            RoundNumber = 1, Slot = 0,
            Participant1Id = Ann, Participant2Id = Bob,
            Participant1Score = finished ? 2 : 0, Participant2Score = finished ? 1 : 0,
            WinnerId = finished ? Ann : null, IsComplete = finished,
        });
        group.Matches.Add(new CompetitionMatch
        {
            RoundNumber = 1, Slot = 1,
            Participant1Id = Cal, Participant2Id = Dee,
            Participant1Score = finished ? 2 : 0, Participant2Score = 0,
            WinnerId = finished ? Cal : null, IsComplete = finished,
        });

        if (!oneRound)
        {
            // Played on to a final, which is what happens when one goes through.
            group.Matches.Add(new CompetitionMatch
            {
                RoundNumber = 2, Slot = 0,
                Participant1Id = Ann, Participant2Id = Cal,
                Participant1Score = finished ? 2 : 0, Participant2Score = 0,
                WinnerId = finished ? Ann : null, IsComplete = finished,
            });
        }

        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            Name = "Singles Cup",
            Format = CompetitionFormat.SinglesGroupStage,
            BestOf = 3,
            ShowOnWebsite = true,
            Status = CompetitionStatus.InProgress,
            GroupSettings = new GroupStageSettings { NumberOfGroups = 1, TopPlayersAdvance = through },
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
    public void BothQualifiersAreMarkedThrough()
    {
        var (league, settings) = Played();

        var html = CompetitionsPage(league, settings);

        // A single round has no final, so reading the winner off one would have
        // marked nobody at all.
        Assert.Equal(2, Occurrences(html, "Through</span>"));
    }

    [Fact]
    public void AGroupPlayedToAFinalStillMarksItsWinnerAndRunnerUp()
    {
        var (league, settings) = Played(oneRound: false);

        var html = CompetitionsPage(league, settings);

        Assert.Equal(2, Occurrences(html, "Through</span>"));
    }

    [Fact]
    public void OnlyTheWinnerIsMarkedWhenOneGoesThrough()
    {
        var (league, settings) = Played(oneRound: false, through: 1);

        var html = CompetitionsPage(league, settings);

        Assert.Equal(1, Occurrences(html, "Through</span>"));
    }

    [Fact]
    public void NobodyIsMarkedUntilTheRoundIsPlayed()
    {
        var (league, settings) = Played(finished: false);

        var html = CompetitionsPage(league, settings);

        Assert.DoesNotContain("Through</span>", html);
    }

    [Fact]
    public void TheTiesThemselvesAreNotListed()
    {
        var (league, settings) = Played();

        var html = CompetitionsPage(league, settings);

        // The scoreline of a tie that was played. Its absence is the point:
        // eight groups of these is what made the page unreadable.
        Assert.DoesNotContain("2&ndash;1", html);
        Assert.DoesNotContain("Semi-finals", html);
    }

    [Fact]
    public void ThePlayersAreStillNamed()
    {
        var (league, settings) = Played();

        var html = CompetitionsPage(league, settings);

        Assert.Contains("Ann Reid", html);
        Assert.Contains("Bob Crane", html);
        Assert.Contains("Cal Dow", html);
        Assert.Contains("Dee Marsh", html);
    }

    [Fact]
    public void AKnockoutGroupIsNotGivenALeagueTable()
    {
        var (league, settings) = Played();

        var html = CompetitionsPage(league, settings);

        // "Pts" is the giveaway column of the round-robin table.
        Assert.DoesNotContain("<th>Pts</th>", html);
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        var at = 0;

        while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }

        return count;
    }
}
