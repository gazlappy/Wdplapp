using Wdpl2.Domain.Fixtures;
using Wdpl2.Features.WebControl;
using Wdpl2.Models;

namespace wdpl2.Tests;

/// <summary>
/// What "open the whole night" would hand to the website.
/// </summary>
/// <remarks>
/// The risk this guards is opening too much: a match that is already being
/// scored online, or one the app has the result of, must never be offered
/// to the captains again.
/// </remarks>
public class MatchNightsTests
{
    private static readonly Guid SeasonId = Guid.NewGuid();
    private static readonly Guid Reds = Guid.NewGuid();
    private static readonly Guid Blues = Guid.NewGuid();
    private static readonly Guid Greens = Guid.NewGuid();
    private static readonly Guid Golds = Guid.NewGuid();

    private static readonly DateTime Tuesday = new(2026, 2, 3, 19, 30, 0);
    private static readonly DateTime NextTuesday = new(2026, 2, 10, 19, 30, 0);

    private static Season TheSeason() => new() { Id = SeasonId, Name = "2025/26", IsActive = true };

    private static LeagueData ALeague(params Fixture[] fixtures)
    {
        var league = new LeagueData
        {
            Seasons = { TheSeason() },
            Teams =
            {
                new Team { Id = Reds, SeasonId = SeasonId, Name = "Reds" },
                new Team { Id = Blues, SeasonId = SeasonId, Name = "Blues" },
                new Team { Id = Greens, SeasonId = SeasonId, Name = "Greens" },
                new Team { Id = Golds, SeasonId = SeasonId, Name = "Golds" },
            },
        };
        league.Fixtures.AddRange(fixtures);
        return league;
    }

    private static Fixture AFixture(Guid home, Guid away, DateTime date) => new()
    {
        Id = Guid.NewGuid(),
        SeasonId = SeasonId,
        HomeTeamId = home,
        AwayTeamId = away,
        Date = date,
    };

    private static Competition TheCup(DateTime date, bool complete = false) => new()
    {
        Id = Guid.NewGuid(),
        SeasonId = SeasonId,
        Name = "Chairman's Cup",
        Format = CompetitionFormat.TeamKnockout,
        ParticipantIds = { Reds, Blues },
        Rounds =
        {
            new CompetitionRound
            {
                Name = "Semi-Finals",
                RoundNumber = 1,
                Date = date,
                Matches =
                {
                    new CompetitionMatch
                    {
                        Slot = 0,
                        Participant1Id = Reds,
                        Participant2Id = Blues,
                        IsComplete = complete,
                    },
                },
            },
        },
    };

    [Fact]
    public void Matches_are_gathered_by_the_night_they_are_played_on()
    {
        var league = ALeague(
            AFixture(Reds, Blues, Tuesday),
            AFixture(Greens, Golds, Tuesday),
            AFixture(Reds, Greens, NextTuesday));

        var nights = MatchNights.Waiting(league, TheSeason(), []);

        Assert.Equal(2, nights.Count);
        Assert.Equal(Tuesday.Date, nights[0].Date);
        Assert.Equal(2, nights[0].Matches.Count);
        Assert.Single(nights[1].Matches);
        Assert.Equal("Reds v Blues", nights[0].Matches[0].Label);
    }

    [Fact]
    public void A_cup_tie_joins_the_league_fixtures_on_its_night()
    {
        var league = ALeague(AFixture(Greens, Golds, Tuesday));
        league.Competitions.Add(TheCup(Tuesday));

        var night = Assert.Single(MatchNights.Waiting(league, TheSeason(), []));

        Assert.Equal(2, night.Matches.Count);
        var tie = Assert.Single(night.Matches, m => m.IsCup);
        Assert.Equal("Chairman's Cup: Reds v Blues", tie.Label);
    }

    [Fact]
    public void A_match_already_open_online_is_left_out()
    {
        var open = AFixture(Reds, Blues, Tuesday);
        var league = ALeague(open, AFixture(Greens, Golds, Tuesday));

        var night = Assert.Single(MatchNights.Waiting(league, TheSeason(), [open.Id]));

        Assert.Equal("Greens v Golds", Assert.Single(night.Matches).Label);
    }

    [Fact]
    public void A_fixture_the_app_has_a_score_for_is_left_out()
    {
        var played = AFixture(Reds, Blues, Tuesday);
        played.Frames.Add(new FrameResult { Number = 1, Winner = FrameWinner.Home });

        var league = ALeague(played, AFixture(Greens, Golds, Tuesday));

        var night = Assert.Single(MatchNights.Waiting(league, TheSeason(), []));
        Assert.Equal("Greens v Golds", Assert.Single(night.Matches).Label);
    }

    [Fact]
    public void An_empty_card_already_written_out_still_counts_as_waiting()
    {
        // Frames exist but none is won: the fixture was set up, not played.
        var blank = AFixture(Reds, Blues, Tuesday);
        blank.Frames.Add(new FrameResult { Number = 1 });

        var night = Assert.Single(MatchNights.Waiting(ALeague(blank), TheSeason(), []));
        Assert.Single(night.Matches);
    }

    [Fact]
    public void A_tie_that_has_been_played_is_left_out()
    {
        var league = ALeague();
        league.Competitions.Add(TheCup(Tuesday, complete: true));

        Assert.Empty(MatchNights.Waiting(league, TheSeason(), []));
    }

    [Fact]
    public void Another_seasons_fixtures_are_left_out()
    {
        var league = ALeague(AFixture(Reds, Blues, Tuesday));
        var old = AFixture(Greens, Golds, Tuesday);
        old.SeasonId = Guid.NewGuid();
        league.Fixtures.Add(old);

        var night = Assert.Single(MatchNights.Waiting(league, TheSeason(), []));
        Assert.Equal("Reds v Blues", Assert.Single(night.Matches).Label);
    }

    [Fact]
    public void The_night_offered_first_is_tonight()
    {
        var nights = MatchNights.Waiting(
            ALeague(AFixture(Reds, Blues, Tuesday), AFixture(Greens, Golds, NextTuesday)),
            TheSeason(), []);

        Assert.Equal(0, MatchNights.Nearest(nights, Tuesday));
    }

    [Fact]
    public void Otherwise_it_is_the_next_night_to_come()
    {
        var nights = MatchNights.Waiting(
            ALeague(AFixture(Reds, Blues, Tuesday), AFixture(Greens, Golds, NextTuesday)),
            TheSeason(), []);

        Assert.Equal(1, MatchNights.Nearest(nights, Tuesday.AddDays(1)));
    }

    [Fact]
    public void With_every_night_gone_by_the_last_one_is_offered()
    {
        var nights = MatchNights.Waiting(
            ALeague(AFixture(Reds, Blues, Tuesday), AFixture(Greens, Golds, NextTuesday)),
            TheSeason(), []);

        Assert.Equal(1, MatchNights.Nearest(nights, NextTuesday.AddDays(7)));
    }

    [Fact]
    public void Nothing_waiting_means_nothing_to_offer() =>
        Assert.Equal(-1, MatchNights.Nearest([], DateTime.Today));
}
