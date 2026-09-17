using Wdpl2.Domain.Fixtures;
using Wdpl2.Domain.Players;
using Wdpl2.Models;

namespace wdpl2.Tests;

/// <summary>
/// Finding one person recorded twice, and putting them back together.
/// </summary>
/// <remarks>
/// The risk in a merge is not the rows it deletes - it is the reference it
/// forgets. A frame still naming a player who no longer exists shows a blank
/// slot and takes a result out of that player's record, and nothing announces
/// it. So the walk over every place a player id is stored is what most of
/// these tests are about.
/// </remarks>
public class PlayerMergeTests
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

        var found = Assert.Single(PlayerMerge.Find(league));

        Assert.Equal(PlayerMerge.Confidence.Certain, found.Confidence);
        Assert.Equal(2, found.Players.Count);

        // One row each season is not a clash - linking them is all that is needed.
        Assert.False(found.Collapses);
    }

    [Fact]
    public void Find_SaysWhenMergingWouldDeleteARow()
    {
        var league = League();
        Add(league, "Dave", "Marsh", ThisSeason);
        Add(league, "Dave", "Marsh", ThisSeason);

        var found = Assert.Single(PlayerMerge.Find(league));

        Assert.True(found.Collapses);
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

        Assert.Empty(PlayerMerge.Find(league));
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

        Assert.Single(PlayerMerge.Find(league));
    }

    [Fact]
    public void Find_PairsAnInitialWithAForename()
    {
        var league = League();
        Add(league, "D", "Marsh", LastSeason);
        Add(league, "Dave", "Marsh", ThisSeason);

        var found = Assert.Single(PlayerMerge.Find(league));

        Assert.Equal(PlayerMerge.Confidence.Likely, found.Confidence);

        // The fuller spelling is the one worth offering to keep.
        Assert.Equal("Dave Marsh", found.Name);
    }

    [Fact]
    public void Find_PairsAOneLetterTypo()
    {
        var league = League();
        Add(league, "Ryan", "Foulkes", ThisSeason);
        Add(league, "Rian", "Foulkes", ThisSeason);

        var found = Assert.Single(PlayerMerge.Find(league));
        Assert.Equal(PlayerMerge.Confidence.Likely, found.Confidence);
    }

    /// <summary>
    /// Two short forenames one letter apart are usually two people.
    /// </summary>
    /// <remarks>
    /// Brothers on the same team are the normal case here, and offering to
    /// merge them is worse than missing a typo: one is a suggestion nobody
    /// acts on, the other invites deleting a real player.
    /// </remarks>
    [Fact]
    public void Find_LeavesShortNamesAlone()
    {
        var league = League();
        Add(league, "Jon", "Marsh", ThisSeason);
        Add(league, "Ian", "Marsh", ThisSeason);

        Assert.Empty(PlayerMerge.Find(league));
    }

    [Fact]
    public void Find_DoesNotPutOnePlayerInTwoGroups()
    {
        var league = League();
        Add(league, "Dave", "Marsh", LastSeason);
        Add(league, "Dave", "Marsh", ThisSeason);
        Add(league, "D", "Marsh", ThisSeason);

        var found = Assert.Single(PlayerMerge.Find(league));
        Assert.Equal(3, found.Players.Count);
    }

    // ----------------------------------------------------------------- merge

    [Fact]
    public void Apply_LinksSeasonsWithoutDeletingAnything()
    {
        var league = League();
        var older = Add(league, "Dave", "Marsh", LastSeason);
        var newer = Add(league, "Dave", "Marsh", ThisSeason);

        var report = PlayerMerge.Apply(league, newer.Id, new[] { older.Id });

        Assert.Equal(0, report.Removed);
        Assert.Equal(2, league.Players.Count);
        Assert.Equal(newer.Id, older.GlobalPlayerId);
        Assert.Equal(newer.Id, newer.GlobalPlayerId);
    }

    [Fact]
    public void Apply_CollapsesTwoRowsInOneSeason()
    {
        var league = League();
        var keep = Add(league, "Dave", "Marsh", ThisSeason);
        var drop = Add(league, "Dave", "Marsh", ThisSeason);

        var report = PlayerMerge.Apply(league, keep.Id, new[] { drop.Id });

        Assert.Equal(1, report.Removed);
        Assert.Equal(keep.Id, Assert.Single(league.Players).Id);
    }

    [Fact]
    public void Apply_MovesEveryFrameOntoTheSurvivor()
    {
        var league = League();
        var keep = Add(league, "Dave", "Marsh", ThisSeason);
        var drop = Add(league, "Dave", "Marsh", ThisSeason);

        var fixture = new Fixture { Id = Guid.NewGuid(), SeasonId = ThisSeason };
        fixture.Frames.Add(new FrameResult { Number = 1, HomePlayerId = drop.Id, Winner = FrameWinner.Home });
        fixture.Frames.Add(new FrameResult { Number = 2, AwayPlayerId = drop.Id });
        fixture.Frames.Add(new FrameResult { Number = 3, IsDoubles = true, HomePlayer2Id = drop.Id });
        fixture.Frames.Add(new FrameResult { Number = 4, IsDoubles = true, AwayPlayer2Id = drop.Id });
        league.Fixtures.Add(fixture);

        var report = PlayerMerge.Apply(league, keep.Id, new[] { drop.Id });

        Assert.Equal(4, report.References);
        Assert.All(fixture.Frames, f => Assert.DoesNotContain(drop.Id, new[]
        {
            f.HomePlayerId, f.AwayPlayerId, f.HomePlayer2Id, f.AwayPlayer2Id,
        }));
        Assert.Equal(keep.Id, fixture.Frames[0].HomePlayerId);
        Assert.Equal(keep.Id, fixture.Frames[3].AwayPlayer2Id);
    }

    [Fact]
    public void Apply_MovesEverythingElseThatNamesThem()
    {
        var league = League();
        var keep = Add(league, "Dave", "Marsh", ThisSeason);
        var drop = Add(league, "Dave", "Marsh", ThisSeason);

        var team = new Team { Id = Guid.NewGuid(), SeasonId = ThisSeason, CaptainPlayerId = drop.Id };
        league.Teams.Add(team);

        var other = Add(league, "Someone", "Else", LastSeason);
        other.GlobalPlayerId = drop.Id;

        league.DoublesPairings.Add(new DoublesPairing { SeasonId = ThisSeason, Player1Id = drop.Id });

        var webId = Guid.NewGuid();
        league.CollectedWebPlayers[webId] = drop.Id;

        var group = new CompetitionGroup { Id = Guid.NewGuid(), Name = "A", OrganiserParticipantId = drop.Id };
        group.ParticipantIds.Add(drop.Id);
        group.Matches.Add(new CompetitionMatch { Participant1Id = drop.Id, WinnerId = drop.Id });
        group.Standings.Add(new GroupStanding { ParticipantId = drop.Id, Position = 1 });

        var comp = new Competition { Id = Guid.NewGuid(), SeasonId = ThisSeason, Name = "Singles" };
        comp.ParticipantIds.Add(drop.Id);
        comp.NoShowIds.Add(drop.Id);
        comp.DoublesTeams.Add(new DoublesTeam { Id = Guid.NewGuid(), Player1Id = drop.Id, Player2Id = keep.Id });
        comp.Rounds.Add(new CompetitionRound
        {
            RoundNumber = 1,
            OrganiserParticipantId = drop.Id,
            Matches = { new CompetitionMatch { Participant2Id = drop.Id } },
        });
        comp.Groups.Add(group);
        league.Competitions.Add(comp);

        PlayerMerge.Apply(league, keep.Id, new[] { drop.Id });

        Assert.Equal(keep.Id, team.CaptainPlayerId);
        Assert.Equal(keep.Id, other.GlobalPlayerId);
        Assert.Equal(keep.Id, league.DoublesPairings[0].Player1Id);
        Assert.Equal(keep.Id, league.CollectedWebPlayers[webId]);

        Assert.Equal(new[] { keep.Id }, comp.ParticipantIds);
        Assert.Equal(new[] { keep.Id }, comp.NoShowIds);
        Assert.Equal(keep.Id, comp.DoublesTeams[0].Player1Id);
        Assert.Equal(keep.Id, comp.Rounds[0].OrganiserParticipantId);
        Assert.Equal(keep.Id, comp.Rounds[0].Matches[0].Participant2Id);

        Assert.Equal(keep.Id, group.OrganiserParticipantId);
        Assert.Equal(new[] { keep.Id }, group.ParticipantIds);
        Assert.Equal(keep.Id, group.Matches[0].Participant1Id);
        Assert.Equal(keep.Id, group.Matches[0].WinnerId);
        Assert.Equal(keep.Id, group.Standings[0].ParticipantId);
    }

    /// <summary>
    /// A list that named both of them must not end up naming one twice.
    /// </summary>
    [Fact]
    public void Apply_DoesNotLeaveTheSurvivorInAListTwice()
    {
        var league = League();
        var keep = Add(league, "Dave", "Marsh", ThisSeason);
        var drop = Add(league, "Dave", "Marsh", ThisSeason);

        var comp = new Competition { Id = Guid.NewGuid(), SeasonId = ThisSeason, Name = "Singles" };
        comp.ParticipantIds.AddRange(new[] { keep.Id, drop.Id });
        league.Competitions.Add(comp);

        PlayerMerge.Apply(league, keep.Id, new[] { drop.Id });

        Assert.Equal(new[] { keep.Id }, comp.ParticipantIds);
    }

    [Fact]
    public void Apply_CollapsesAndLinksInOneGo()
    {
        var league = League();
        var older = Add(league, "Dave", "Marsh", LastSeason);
        var keep = Add(league, "Dave", "Marsh", ThisSeason);
        var drop = Add(league, "D", "Marsh", ThisSeason);

        var report = PlayerMerge.Apply(league, keep.Id, new[] { older.Id, drop.Id });

        // The two in this season become one; last season's is kept and linked.
        Assert.Equal(1, report.Removed);
        Assert.Equal(2, league.Players.Count);
        Assert.Equal(keep.Id, older.GlobalPlayerId);
        Assert.Equal(keep.Id, keep.GlobalPlayerId);
        Assert.DoesNotContain(league.Players, p => p.Id == drop.Id);
    }

    /// <summary>
    /// Two rows in a season neither of which is the keeper still collapse.
    /// </summary>
    [Fact]
    public void Apply_CollapsesASeasonTheKeeperIsNotIn()
    {
        var league = League();
        var keep = Add(league, "Dave", "Marsh", ThisSeason);
        var one = Add(league, "Dave", "Marsh", LastSeason, team: Guid.NewGuid());
        var two = Add(league, "Dave", "Marsh", LastSeason);

        var report = PlayerMerge.Apply(league, keep.Id, new[] { one.Id, two.Id });

        Assert.Equal(1, report.Removed);
        Assert.Equal(2, league.Players.Count);

        // The row with a team is the one last season actually played.
        Assert.Contains(league.Players, p => p.Id == one.Id);
        Assert.Equal(keep.Id, one.GlobalPlayerId);
    }

    [Fact]
    public void Apply_KeepsWhatTheLosingRowHadAndTheSurvivorLacked()
    {
        var league = League();
        var keep = Add(league, "Dave", "Marsh", ThisSeason);
        keep.IsActive = false;

        var team = Guid.NewGuid();
        var drop = Add(league, "Dave", "Marsh", ThisSeason, team);
        drop.Notes = "Plays Thursdays";

        PlayerMerge.Apply(league, keep.Id, new[] { drop.Id });

        Assert.Equal(team, keep.TeamId);
        Assert.Equal("Plays Thursdays", keep.Notes);
        Assert.True(keep.IsActive);
    }

    [Fact]
    public void Apply_RefusesANonsenseMerge()
    {
        var league = League();
        var only = Add(league, "Dave", "Marsh", ThisSeason);

        Assert.Equal(0, PlayerMerge.Apply(league, only.Id, Array.Empty<Guid>()).Removed);
        Assert.Equal(0, PlayerMerge.Apply(league, Guid.NewGuid(), new[] { only.Id }).Removed);
        Assert.Single(league.Players);
    }

    [Fact]
    public void References_CountsWithoutChangingAnything()
    {
        var league = League();
        var player = Add(league, "Dave", "Marsh", ThisSeason);

        var fixture = new Fixture { Id = Guid.NewGuid(), SeasonId = ThisSeason };
        fixture.Frames.Add(new FrameResult { Number = 1, HomePlayerId = player.Id });
        fixture.Frames.Add(new FrameResult { Number = 2, HomePlayerId = player.Id });
        league.Fixtures.Add(fixture);

        Assert.Equal(2, PlayerMerge.References(league, player.Id));
        Assert.Equal(player.Id, fixture.Frames[0].HomePlayerId);
    }

    /// <summary>
    /// Merging keeps an existing cross-season identity rather than inventing one.
    /// </summary>
    /// <remarks>
    /// A player already linked across three seasons who picks up a fourth must
    /// not have the other three quietly detached from their career.
    /// </remarks>
    [Fact]
    public void Apply_KeepsTheIdentityTheKeeperAlreadyHad()
    {
        var league = League();
        var identity = Guid.NewGuid();

        var keep = Add(league, "Dave", "Marsh", ThisSeason);
        keep.GlobalPlayerId = identity;

        var older = Add(league, "Dave", "Marsh", LastSeason);

        PlayerMerge.Apply(league, keep.Id, new[] { older.Id });

        Assert.Equal(identity, keep.GlobalPlayerId);
        Assert.Equal(identity, older.GlobalPlayerId);
    }
}
