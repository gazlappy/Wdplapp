using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services.Web;

namespace wdpl2.Tests;

/// <summary>
/// What gets published for a competition night, and what comes back.
/// </summary>
public class CompetitionNightServiceTests
{
    private static readonly Guid Ann = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly Guid Cal = Guid.NewGuid();

    private static Dictionary<Guid, string> Names() => new()
    {
        [Ann] = "Ann Reid",
        [Bob] = "Bob Crane",
        [Cal] = "Cal Dow",
    };

    private static Competition GroupStage(string? pin = "12345")
    {
        var group = new CompetitionGroup
        {
            Name = "Group A",
            GroupNumber = 1,
            VenueName = "The Bell",
            TableLabel = "Table 2",
            OrganiserParticipantId = Ann,
            RunnerPin = pin,
            ParticipantIds = { Ann, Bob, Cal },
        };

        group.Matches.Add(new CompetitionMatch { Participant1Id = Ann, Participant2Id = Bob });
        group.Matches.Add(new CompetitionMatch { Participant1Id = Ann, Participant2Id = Cal });

        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            Name = "Singles Cup",
            Format = CompetitionFormat.SinglesGroupStage,
            BestOf = 3,
        };
        competition.Groups.Add(group);
        return competition;
    }

    [Fact]
    public void Build_MakesOneSessionPerGroup()
    {
        var competition = GroupStage();

        var sessions = CompetitionNightService.Build(competition, Names());

        var session = Assert.Single(sessions);
        Assert.Equal("group", session.Kind);
        Assert.Equal("Group A", session.Name);
        Assert.Equal("Ann Reid", session.OrganiserName);
        Assert.Equal("The Bell", session.VenueName);
        Assert.Equal(3, session.ParticipantIds.Count);
        Assert.Equal(2, session.Matches.Count);
    }

    [Fact]
    public void Build_SkipsAKnockoutRoundThatIsAlreadyDecided()
    {
        var competition = GroupStage();
        competition.Groups.Clear();

        var done = new CompetitionRound { RoundNumber = 1, Name = "Quarter-Finals" };
        done.Matches.Add(new CompetitionMatch
        {
            Participant1Id = Ann, Participant2Id = Bob, WinnerId = Ann, IsComplete = true,
        });

        var live = new CompetitionRound { RoundNumber = 2, Name = "Semi-Finals", RunnerPin = "54321" };
        live.Matches.Add(new CompetitionMatch { Participant1Id = Ann, Participant2Id = Cal });

        competition.Rounds.Add(done);
        competition.Rounds.Add(live);

        var sessions = CompetitionNightService.Build(competition, Names());

        var session = Assert.Single(sessions);
        Assert.Equal("Semi-Finals", session.Name);
        Assert.Equal("round", session.Kind);
    }

    [Fact]
    public void Build_LeavesGroupStageRoundsToTheirGroups()
    {
        var competition = GroupStage();
        competition.Rounds.Add(new CompetitionRound
        {
            RoundNumber = 1, Name = "Group matches", IsGroupStage = true,
            Matches = { new CompetitionMatch { Participant1Id = Ann, Participant2Id = Bob } },
        });

        // One session for the group, and nothing extra for the round that
        // describes the same matches.
        Assert.Single(CompetitionNightService.Build(competition, Names()));
    }

    [Fact]
    public void Payload_CarriesTheHashAndNeverThePin()
    {
        var competition = GroupStage(pin: "24680");
        var sessions = CompetitionNightService.Build(competition, Names());

        var (payload, skipped) = CompetitionNightService.BuildPayload(competition, sessions, Names());
        var json = JsonSerializer.Serialize(payload);

        Assert.Empty(skipped);
        Assert.DoesNotContain("24680", json);
        Assert.Contains("pbkdf2-sha256$", json);
        Assert.Contains("Ann Reid", json);
    }

    [Fact]
    public void Payload_LeavesOutASessionWithNoPin()
    {
        var competition = GroupStage(pin: null);
        var sessions = CompetitionNightService.Build(competition, Names());

        var (payload, skipped) = CompetitionNightService.BuildPayload(competition, sessions, Names());

        // Publishing it would list the players with no way to open it.
        Assert.Equal(new[] { "Group A" }, skipped);
        Assert.DoesNotContain("Ann Reid", JsonSerializer.Serialize(payload));
    }

    [Fact]
    public void Stable_GivesTheSameSessionIdEveryTime()
    {
        var competitionId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var first = CompetitionNightService.Stable(competitionId, groupId);

        // A republish must land on the same row, or results entered against the
        // last one are orphaned.
        Assert.Equal(first, CompetitionNightService.Stable(competitionId, groupId));
        Assert.NotEqual(first, CompetitionNightService.Stable(competitionId, Guid.NewGuid()));
        Assert.NotEqual(first, CompetitionNightService.Stable(Guid.NewGuid(), groupId));
    }

    [Fact]
    public void NewPin_IsFiveDigitsAndAvoidsOnesInUse()
    {
        var taken = new List<string?>();

        for (var i = 0; i < 25; i++)
        {
            var pin = CompetitionNightService.NewPin(taken);

            Assert.Equal(CompetitionNightService.PinLength, pin.Length);
            Assert.All(pin, c => Assert.True(char.IsDigit(c), $"'{c}' is not a digit"));
            Assert.DoesNotContain(pin, taken);

            taken.Add(pin);
        }
    }

    // ------------------------------------------------ collecting the night back

    /// <summary>A knockout as it came back from the venue: 4 drawn, one bye.</summary>
    private static CompetitionNightService.CollectedSession Played(Guid groupId)
    {
        var r1m0 = Guid.NewGuid();
        var r1m1 = Guid.NewGuid();
        var final = Guid.NewGuid();

        return new CompetitionNightService.CollectedSession(
            SessionId: Guid.NewGuid(),
            RefId: groupId,
            Kind: "group",
            DrawOrder: new[] { Ann, Cal, Bob },
            Matches: new[]
            {
                // Ann drawn first, Bob third: they meet in round one.
                new CompetitionNightService.CollectedMatch(
                    r1m0, 1, 0, Ann, Bob, 2, 1, Ann, IsComplete: true),

                // Cal drawn second, into the bottom half, with nobody to play.
                new CompetitionNightService.CollectedMatch(
                    r1m1, 1, 1, Cal, null, 0, 0, Cal, IsComplete: true),

                new CompetitionNightService.CollectedMatch(
                    final, 2, 0, Ann, Cal, 2, 0, Ann, IsComplete: true),
            });
    }

    [Fact]
    public void Apply_RebuildsTheGroupAsItWasActuallyPlayed()
    {
        var competition = GroupStage();
        var group = competition.Groups[0];
        var before = group.Matches.Select(m => m.Id).ToList();

        var applied = CompetitionNightService.Apply(competition, Played(group.Id));

        Assert.Equal(3, applied);
        Assert.Equal(3, group.Matches.Count);

        // The draw happened at the venue, so what was pencilled in beforehand
        // is not what was played.
        Assert.DoesNotContain(group.Matches, m => before.Contains(m.Id));
    }

    [Fact]
    public void Apply_KeepsTheTreeInOrderWithItsRoundsAndSlots()
    {
        var competition = GroupStage();
        var group = competition.Groups[0];

        CompetitionNightService.Apply(competition, Played(group.Id));

        Assert.Equal(new[] { 1, 1, 2 }, group.Matches.Select(m => m.RoundNumber));
        Assert.Equal(new[] { 0, 1, 0 }, group.Matches.Select(m => m.Slot));

        var final = group.Matches.Last();
        Assert.Equal(Ann, final.WinnerId);
        Assert.True(final.IsComplete);
    }

    [Fact]
    public void Apply_RemembersTheOrderTheyCameOutOfTheBag()
    {
        var competition = GroupStage();
        var group = competition.Groups[0];

        CompetitionNightService.Apply(competition, Played(group.Id));

        // The one thing that cannot be worked back out from the results.
        Assert.Equal(new[] { Ann, Cal, Bob }, group.DrawOrder);
    }

    [Fact]
    public void Apply_RebuildsTheTableFromTheTree()
    {
        var competition = GroupStage();
        var group = competition.Groups[0];

        // A table left over from when this was a round robin, with the wrong
        // player on top.
        group.Standings.Add(new GroupStanding { ParticipantId = Bob, Position = 1, Points = 4 });

        CompetitionNightService.Apply(competition, Played(group.Id));

        // The rest of the app reads who went through from the standings, so
        // they have to describe the knockout that was actually played.
        Assert.Equal(Ann, group.Standings[0].ParticipantId);
        Assert.Equal(1, group.Standings[0].Position);

        Assert.Equal(Cal, group.Standings[1].ParticipantId);
        Assert.Equal(2, group.Standings[1].Position);

        Assert.DoesNotContain(group.Standings, s => s.ParticipantId == Bob && s.Position == 1);
    }

    [Fact]
    public void Apply_CarriesAByeThroughAsAWin()
    {
        var competition = GroupStage();
        var group = competition.Groups[0];

        CompetitionNightService.Apply(competition, Played(group.Id));

        var bye = group.Matches.Single(m => m.Participant2Id is null);

        Assert.Equal(Cal, bye.WinnerId);
        Assert.True(bye.IsComplete);
        Assert.Equal(0, bye.Participant1Score);
    }

    [Fact]
    public void Apply_WritesIntoARoundWhenThatIsWhatWasRun()
    {
        var competition = GroupStage();
        competition.Groups.Clear();

        var round = new CompetitionRound { RoundNumber = 1, Name = "Semi-finals" };
        round.Matches.Add(new CompetitionMatch { Participant1Id = Ann, Participant2Id = Bob });
        competition.Rounds.Add(round);

        var played = Played(round.Id) with { Kind = "round" };
        var applied = CompetitionNightService.Apply(competition, played);

        Assert.Equal(3, applied);
        Assert.Equal(3, round.Matches.Count);
        Assert.Equal(new[] { Ann, Cal, Bob }, round.DrawOrder);
    }

    [Fact]
    public void Apply_WritesNothingForAGroupThisCompetitionDoesNotHave()
    {
        var competition = GroupStage();
        var original = competition.Groups[0].Matches.Count;

        var applied = CompetitionNightService.Apply(competition, Played(Guid.NewGuid()));

        Assert.Equal(0, applied);
        Assert.Equal(original, competition.Groups[0].Matches.Count);
    }

    [Fact]
    public void Apply_LeavesTheGroupAloneWhenNothingWasPlayed()
    {
        var competition = GroupStage();
        var group = competition.Groups[0];
        var before = group.Matches.Count;

        var empty = Played(group.Id) with { Matches = Array.Empty<CompetitionNightService.CollectedMatch>() };
        var applied = CompetitionNightService.Apply(competition, empty);

        Assert.Equal(0, applied);
        Assert.Equal(before, group.Matches.Count);
    }
}
