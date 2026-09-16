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

    [Fact]
    public void Apply_WritesResultsOntoTheCompetitionsOwnMatches()
    {
        var competition = GroupStage();
        var match = competition.Groups[0].Matches[0];

        var applied = CompetitionNightService.Apply(competition, new[]
        {
            new CompetitionNightService.CollectedMatch(match.Id, 2, 1, Ann, IsComplete: true),
        });

        Assert.Equal(1, applied);
        Assert.Equal(2, match.Participant1Score);
        Assert.Equal(1, match.Participant2Score);
        Assert.Equal(Ann, match.WinnerId);
        Assert.True(match.IsComplete);
    }

    [Fact]
    public void Apply_IgnoresAMatchThatWasNeverFinished()
    {
        var competition = GroupStage();
        var match = competition.Groups[0].Matches[0];

        var applied = CompetitionNightService.Apply(competition, new[]
        {
            new CompetitionNightService.CollectedMatch(match.Id, 1, 0, null, IsComplete: false),
        });

        Assert.Equal(0, applied);
        Assert.False(match.IsComplete);
        Assert.Equal(0, match.Participant1Score);
    }

    [Fact]
    public void Apply_IgnoresAResultForAMatchThisCompetitionDoesNotHave()
    {
        var competition = GroupStage();

        var applied = CompetitionNightService.Apply(competition, new[]
        {
            new CompetitionNightService.CollectedMatch(Guid.NewGuid(), 3, 0, Ann, IsComplete: true),
        });

        Assert.Equal(0, applied);
    }
}
