using System.Text.Json;
using Wdpl2.Domain.Competitions;
using Wdpl2.Models;
using Wdpl2.Services.Web;

namespace wdpl2.Tests;

/// <summary>
/// Team knockout ties, on their way to the website and back.
/// </summary>
/// <remarks>
/// A cup tie is scored exactly like a league night, so it travels through the
/// fixtures table rather than growing a second kind of card. That decision has
/// one hazard: a cup tie that leaked into the league's own reckoning would add
/// phantom points to a table. These tests hold that line.
/// </remarks>
public class CupTiePublishTests
{
    private static readonly Guid SeasonId = Guid.NewGuid();
    private static readonly Guid DivisionId = Guid.NewGuid();

    private static readonly Guid Reds = Guid.NewGuid();
    private static readonly Guid Blues = Guid.NewGuid();
    private static readonly Guid Greens = Guid.NewGuid();
    private static readonly Guid Golds = Guid.NewGuid();

    private static readonly Guid SemiOne = Guid.NewGuid();
    private static readonly Guid SemiTwo = Guid.NewGuid();
    private static readonly Guid TheFinal = Guid.NewGuid();

    private static Season TheSeason() => new()
    {
        Id = SeasonId,
        Name = "2025/26",
        StartDate = new DateTime(2025, 9, 1),
        EndDate = new DateTime(2026, 5, 1),
        IsActive = true,
    };

    private static Competition TheCup() => new()
    {
        Id = Guid.NewGuid(),
        SeasonId = SeasonId,
        Name = "Chairman's Cup",
        Format = CompetitionFormat.TeamKnockout,
        ParticipantIds = { Reds, Blues, Greens, Golds },
        Rounds =
        {
            new CompetitionRound
            {
                Name = "Semi-Finals",
                RoundNumber = 1,
                Date = new DateTime(2026, 2, 4),
                Matches =
                {
                    new CompetitionMatch { Id = SemiOne, Slot = 0, Participant1Id = Reds, Participant2Id = Blues },
                    new CompetitionMatch { Id = SemiTwo, Slot = 1, Participant1Id = Greens, Participant2Id = Golds },
                },
            },
            new CompetitionRound
            {
                Name = "Final",
                RoundNumber = 2,
                Date = new DateTime(2026, 3, 4),
                Matches = { new CompetitionMatch { Id = TheFinal, Slot = 0 } },
            },
        },
    };

    private static LeagueData TheLeague(Competition cup)
    {
        var league = new LeagueData();
        league.Seasons.Add(TheSeason());
        league.Divisions.Add(new Division { Id = DivisionId, SeasonId = SeasonId, Name = "Division One" });

        foreach (var (id, name) in new[]
                 {
                     (Reds, "Reds"), (Blues, "Blues"), (Greens, "Greens"), (Golds, "Golds"),
                 })
        {
            league.Teams.Add(new Team { Id = id, SeasonId = SeasonId, DivisionId = DivisionId, Name = name });
        }

        league.Competitions.Add(cup);
        return league;
    }

    /// <summary>A league night the two cup teams also played, 8-7 to the Reds.</summary>
    private static Fixture LeagueNight()
    {
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            DivisionId = DivisionId,
            HomeTeamId = Reds,
            AwayTeamId = Blues,
            Date = new DateTime(2025, 10, 1),
        };

        for (var n = 1; n <= 15; n++)
        {
            fixture.Frames.Add(new FrameResult
            {
                Number = n,
                Winner = n <= 8 ? FrameWinner.Home : FrameWinner.Away,
            });
        }

        return fixture;
    }

    [Fact]
    public void CupTies_AreOnlyTheOnesWithBothTeamsKnown()
    {
        var ties = CupTie.For(TheLeague(TheCup()), TheSeason());

        Assert.Equal(2, ties.Count);
        Assert.Contains(ties, t => t.Id == SemiOne);
        Assert.Contains(ties, t => t.Id == SemiTwo);

        // The final has nobody in it yet, so there is nobody to hand a card to.
        Assert.DoesNotContain(ties, t => t.Id == TheFinal);
    }

    [Fact]
    public void CupTie_CarriesTheRoundItBelongsTo()
    {
        var tie = CupTie.For(TheLeague(TheCup()), TheSeason()).Single(t => t.Id == SemiOne);

        Assert.Equal("Chairman's Cup", tie.CompetitionName);
        Assert.Equal("Semi-Finals", tie.RoundName);
        Assert.Equal(new DateTime(2026, 2, 4), tie.Date);
        Assert.Equal(Reds, tie.HomeTeamId);
        Assert.Equal(Blues, tie.AwayTeamId);
    }

    [Fact]
    public void PublishedSnapshot_MarksCupTiesAsCup()
    {
        var league = TheLeague(TheCup());
        league.Fixtures.Add(LeagueNight());

        var (payload, counts) = LeagueSnapshot.Build(league, TheSeason(), new AppSettings());

        var fixtures = Json(payload).GetProperty("fixtures");
        var kinds = fixtures.EnumerateArray().Select(f => f.GetProperty("kind").GetString()).ToList();

        Assert.Equal(3, counts.Fixtures);
        Assert.Equal(1, kinds.Count(k => k == "league"));
        Assert.Equal(2, kinds.Count(k => k == "cup"));
    }

    [Fact]
    public void PublishedCupTies_CarryTheirOwnName()
    {
        var league = TheLeague(TheCup());
        league.Fixtures.Add(LeagueNight());

        var (payload, _) = LeagueSnapshot.Build(league, TheSeason(), new AppSettings());

        var fixtures = Json(payload).GetProperty("fixtures").EnumerateArray().ToList();

        // A captain signing in on the competitions page picks their tie out of
        // a list. Without this the list can only call it "a cup tie".
        var tie = fixtures.Single(f => f.GetProperty("id").GetGuid() == SemiOne);
        Assert.Equal("Chairman's Cup — Semi-Finals", tie.GetProperty("label").GetString());

        // A league night has no such name and must not invent one.
        var night = fixtures.Single(f => f.GetProperty("kind").GetString() == "league");
        Assert.Equal(JsonValueKind.Null, night.GetProperty("label").ValueKind);
    }

    [Fact]
    public void PublishedSnapshot_KeepsCupTiesOutOfTheTable()
    {
        var league = TheLeague(TheCup());
        league.Fixtures.Add(LeagueNight());

        // A cup tie the Blues won heavily. If it reached the standings at all,
        // the Reds' 8-7 league win would stop being the only result in the table.
        var cupTie = league.Competitions[0].Rounds[0].Matches[0];
        cupTie.Participant1Score = 3;
        cupTie.Participant2Score = 12;
        cupTie.WinnerId = Blues;
        cupTie.IsComplete = true;

        var (payload, _) = LeagueSnapshot.Build(league, TheSeason(), new AppSettings());
        var standings = Json(payload).GetProperty("standings");

        var reds = Row(standings, Reds);
        var blues = Row(standings, Blues);

        Assert.Equal(1, reds.GetProperty("played").GetInt32());
        Assert.Equal(8, reds.GetProperty("framesFor").GetInt32());
        Assert.Equal(1, blues.GetProperty("played").GetInt32());
        Assert.Equal(7, blues.GetProperty("framesFor").GetInt32());
    }

    [Fact]
    public void Advancing_PutsTheWinnerInTheRightHalfOfTheFinal()
    {
        var cup = TheCup();
        var round = cup.Rounds[0];

        // The top semi feeds the top slot of the final, the bottom semi the bottom.
        round.Matches[0].WinnerId = Blues;
        BracketAdvance.Advance(cup, round, round.Matches[0]);

        round.Matches[1].WinnerId = Golds;
        BracketAdvance.Advance(cup, round, round.Matches[1]);

        var final = cup.Rounds[1].Matches[0];
        Assert.Equal(Blues, final.Participant1Id);
        Assert.Equal(Golds, final.Participant2Id);
    }

    [Fact]
    public void ClearingAnAdvance_TakesBackOnlyThatHalf()
    {
        var cup = TheCup();
        var round = cup.Rounds[0];

        round.Matches[0].WinnerId = Blues;
        BracketAdvance.Advance(cup, round, round.Matches[0]);
        round.Matches[1].WinnerId = Golds;
        BracketAdvance.Advance(cup, round, round.Matches[1]);

        BracketAdvance.Clear(cup, round, round.Matches[1]);

        var final = cup.Rounds[1].Matches[0];
        Assert.Equal(Blues, final.Participant1Id);
        Assert.Null(final.Participant2Id);
    }

    [Fact]
    public void FinishedTies_StayPublishedSoACollectedCardStillHasARow()
    {
        var league = TheLeague(TheCup());
        var played = league.Competitions[0].Rounds[0].Matches[0];
        played.IsComplete = true;
        played.WinnerId = Reds;

        var ties = CupTie.For(league, TheSeason());

        // Publishing replaces the season's fixtures wholesale, so dropping a
        // finished tie would delete the row its collected card points at.
        Assert.Contains(ties, t => t.Id == SemiOne && t.IsComplete);
    }

    [Fact]
    public void Locating_FindsTheTieInItsOwnRound()
    {
        var cup = TheCup();

        var found = CupTie.Locate(cup, SemiTwo);

        Assert.NotNull(found);
        Assert.Equal("Semi-Finals", found!.Value.Round.Name);
        Assert.Equal(Greens, found.Value.Match.Participant1Id);

        Assert.Null(CupTie.Locate(cup, Guid.NewGuid()));
    }

    [Fact]
    public void TheTossDecidesHomeAndAway_SoTheScoreIsMatchedBackByTeam()
    {
        var cup = TheCup();
        var match = cup.Rounds[0].Matches[0];   // the draw listed Reds first

        // The Blues won the toss, so they were home on the card and won 8-5.
        CupTie.ApplyScore(match, homeTeamId: Blues, homeScore: 8, awayScore: 5);

        // Recorded the draw's way round: the Reds are still slot one.
        Assert.Equal(5, match.Participant1Score);
        Assert.Equal(8, match.Participant2Score);
    }

    [Fact]
    public void TossGoingTheDrawsWay_LeavesTheScoreWhereItIs()
    {
        var cup = TheCup();
        var match = cup.Rounds[0].Matches[0];

        CupTie.ApplyScore(match, homeTeamId: Reds, homeScore: 8, awayScore: 5);

        Assert.Equal(8, match.Participant1Score);
        Assert.Equal(5, match.Participant2Score);
    }

    [Fact]
    public void TheCardNamesTheWinningTeam_NotJustTheWinningColumn()
    {
        // The card as the website hands it back after a toss that made the
        // Blues home: home is the Blues, and home won it.
        var card = new ScorecardState
        {
            IsCup = true,
            HomeTeamId = Blues,
            AwayTeamId = Reds,
            HomeScore = 8,
            AwayScore = 5,
            DecidedBy = FrameWinner.Home,
        };

        Assert.Equal(Blues, card.WinnerTeamId);
    }

    [Fact]
    public void ATieNobodyHasWonYet_NamesNoWinner()
    {
        // Seven all with one to play is not a draw - a cup tie cannot be drawn.
        // It is simply not finished, and nothing may go into the bracket.
        var card = new ScorecardState
        {
            IsCup = true,
            HomeTeamId = Blues,
            AwayTeamId = Reds,
            HomeScore = 7,
            AwayScore = 7,
            DecidedBy = FrameWinner.None,
        };

        Assert.Null(card.WinnerTeamId);
    }

    private static JsonElement Json(object payload) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(payload));

    private static JsonElement Row(JsonElement standings, Guid teamId) =>
        standings.EnumerateArray().Single(s => s.GetProperty("teamId").GetGuid() == teamId);
}
