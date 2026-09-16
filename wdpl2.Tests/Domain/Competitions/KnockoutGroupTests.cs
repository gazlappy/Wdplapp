using Wdpl2.Domain.Competitions;
using Wdpl2.Models;

namespace wdpl2.Tests;

/// <summary>
/// Reading a group that was drawn out and played as a knockout.
/// </summary>
/// <remarks>
/// A round-robin group is read off its table; a knockout has no table. These
/// pin how the same questions get answered from the tree instead — including
/// the one the league hits in practice: a competition set to send two through
/// from a group that only decides one.
/// </remarks>
public class KnockoutGroupTests
{
    private static readonly Guid Ann = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly Guid Cal = Guid.NewGuid();
    private static readonly Guid Dee = Guid.NewGuid();

    /// <summary>Four drawn, played through to a final. Ann beats Cal in it.</summary>
    private static CompetitionGroup Finished(bool finalPlayed = true)
    {
        var group = new CompetitionGroup
        {
            Name = "Group D",
            ParticipantIds = { Ann, Bob, Cal, Dee },
            DrawOrder = { Ann, Cal, Bob, Dee },
        };

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
            Participant1Id = Cal, Participant2Id = Dee,
            Participant1Score = 2, Participant2Score = 0,
            WinnerId = Cal, IsComplete = true,
        });
        group.Matches.Add(new CompetitionMatch
        {
            RoundNumber = 2, Slot = 0,
            Participant1Id = Ann, Participant2Id = Cal,
            Participant1Score = finalPlayed ? 2 : 0, Participant2Score = finalPlayed ? 1 : 0,
            WinnerId = finalPlayed ? Ann : null, IsComplete = finalPlayed,
        });

        return group;
    }

    private static CompetitionGroup RoundRobin()
    {
        var group = new CompetitionGroup { Name = "Group A", ParticipantIds = { Ann, Bob } };
        group.Matches.Add(new CompetitionMatch { Participant1Id = Ann, Participant2Id = Bob });
        return group;
    }

    [Fact]
    public void AGroupWithATreeIsAKnockout()
    {
        Assert.True(KnockoutGroup.IsKnockout(Finished()));
        Assert.False(KnockoutGroup.IsKnockout(RoundRobin()));
    }

    [Fact]
    public void TheWinnerIsWhoeverWonTheFinal()
    {
        Assert.Equal(Ann, KnockoutGroup.Winner(Finished()));
        Assert.Equal(Cal, KnockoutGroup.RunnerUp(Finished()));
    }

    [Fact]
    public void NobodyHasWonUntilTheFinalIsPlayed()
    {
        var group = Finished(finalPlayed: false);

        Assert.Null(KnockoutGroup.Winner(group));
        Assert.Null(KnockoutGroup.RunnerUp(group));
        Assert.Equal(0, KnockoutGroup.PlacesDecided(group));
    }

    [Fact]
    public void AKnockoutDecidesTwoPlaces()
    {
        // The league's competitions are set to send two through from a group.
        // A knockout can answer that: the winner and the beaten finalist.
        Assert.Equal(2, KnockoutGroup.PlacesDecided(Finished()));
    }

    [Fact]
    public void StandingsPutTheWinnerFirstAndTheBeatenFinalistSecond()
    {
        var standings = KnockoutGroup.Standings(Finished());

        Assert.Equal(Ann, standings[0].ParticipantId);
        Assert.Equal(1, standings[0].Position);

        Assert.Equal(Cal, standings[1].ParticipantId);
        Assert.Equal(2, standings[1].Position);
    }

    [Fact]
    public void EverybodyElseIsUnplaced()
    {
        var standings = KnockoutGroup.Standings(Finished());

        // Bob and Dee went out in the first round and never met, so a knockout
        // has nothing to say about which of them finished higher.
        foreach (var row in standings.Where(r => r.ParticipantId != Ann && r.ParticipantId != Cal))
        {
            Assert.Equal(0, row.Position);
        }
    }

    [Fact]
    public void StandingsCarryTheFramesThatWerePlayed()
    {
        var standings = KnockoutGroup.Standings(Finished());

        var ann = standings.Single(r => r.ParticipantId == Ann);

        // Two ties won: 2-1 and 2-1.
        Assert.Equal(2, ann.Played);
        Assert.Equal(2, ann.Won);
        Assert.Equal(0, ann.Lost);
        Assert.Equal(4, ann.FramesFor);
        Assert.Equal(2, ann.FramesAgainst);
    }

    [Fact]
    public void AByeCountsForNothingButGettingThrough()
    {
        var group = new CompetitionGroup { Name = "Group B", ParticipantIds = { Ann, Bob, Cal } };

        group.Matches.Add(new CompetitionMatch
        {
            RoundNumber = 1, Slot = 0,
            Participant1Id = Ann, Participant2Id = Bob,
            Participant1Score = 2, Participant2Score = 0,
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
            Participant1Score = 1, Participant2Score = 2,
            WinnerId = Cal, IsComplete = true,
        });

        var cal = KnockoutGroup.Standings(group).Single(r => r.ParticipantId == Cal);

        // Cal played once, not twice: the bye was not a match.
        Assert.Equal(1, cal.Played);
        Assert.Equal(1, cal.Won);
        Assert.Equal(Cal, KnockoutGroup.Winner(group));
    }

    [Fact]
    public void ARoundRobinKeepsTheStandingsItAlreadyHad()
    {
        var group = RoundRobin();
        group.Standings.Add(new GroupStanding { ParticipantId = Ann, Position = 1, Points = 2 });

        // Nothing to derive from, so the table it was given stands.
        Assert.Same(group.Standings, KnockoutGroup.Standings(group));
    }

    [Fact]
    public void TwoGoThroughWhenTheCompetitionAsksForTwo()
    {
        // The league's own setup: eight groups, two through from each.
        var through = KnockoutGroup.Through(Finished(), places: 2);

        Assert.Equal(new[] { Ann, Cal }, through);
    }

    [Fact]
    public void OnlyTheWinnerGoesThroughWhenTheCompetitionAsksForOne()
    {
        Assert.Equal(new[] { Ann }, KnockoutGroup.Through(Finished(), places: 1));
    }

    [Fact]
    public void AskingForMoreThanAKnockoutDecidesGivesWhatItHas()
    {
        // Beyond the two finalists there is nothing to say, so it stops rather
        // than padding the list with players who never met.
        Assert.Equal(new[] { Ann, Cal }, KnockoutGroup.Through(Finished(), places: 4));
    }

    [Fact]
    public void NobodyGoesThroughBeforeTheFinalIsPlayed()
    {
        Assert.Empty(KnockoutGroup.Through(Finished(finalPlayed: false), places: 2));
    }
}
