using Moq;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.ViewModels;

namespace Wdpl2.Tests;

/// <summary>
/// Tests for CompetitionEditorViewModel — verifies DI surface and that lookups
/// resolve from the injected <see cref="IDataStore"/> rather than static state.
/// </summary>
public class CompetitionEditorViewModelTests
{
    private static (Mock<IDataStore> DataStore, LeagueData Data) CreateStore()
    {
        var data = new LeagueData();
        var mock = new Mock<IDataStore>();
        mock.Setup(x => x.GetData()).Returns(data);
        return (mock, data);
    }

    [Fact]
    public void Constructor_ValidDependencies_InitializesViewModel()
    {
        var (store, _) = CreateStore();
        var competition = new Competition { Id = Guid.NewGuid(), Name = "Cup", Format = CompetitionFormat.SinglesKnockout };

        var vm = new CompetitionEditorViewModel(store.Object, competition, currentSeasonId: null);

        Assert.NotNull(vm);
        Assert.Equal("Cup", vm.Name);
        Assert.Same(competition, vm.Competition);
    }

    [Fact]
    public async Task GetAvailableVenuesAsync_FiltersBySeason()
    {
        var (store, data) = CreateStore();
        var seasonId = Guid.NewGuid();
        data.Venues.Add(new Venue { Id = Guid.NewGuid(), Name = "Alpha", SeasonId = seasonId });
        data.Venues.Add(new Venue { Id = Guid.NewGuid(), Name = "Beta", SeasonId = seasonId });
        data.Venues.Add(new Venue { Id = Guid.NewGuid(), Name = "Other", SeasonId = Guid.NewGuid() });
        var competition = new Competition { Id = Guid.NewGuid(), Name = "Cup", SeasonId = seasonId };

        var vm = new CompetitionEditorViewModel(store.Object, competition, seasonId);
        var venues = await vm.GetAvailableVenuesAsync();

        Assert.Equal(2, venues.Count);
        Assert.Equal(new[] { "Alpha", "Beta" }, venues.Select(v => v.Name));
    }

    [Fact]
    public async Task GetAvailablePlayersAsync_ExcludesExistingParticipants()
    {
        var (store, data) = CreateStore();
        var seasonId = Guid.NewGuid();
        var p1 = new Player { Id = Guid.NewGuid(), FirstName = "Alice", SeasonId = seasonId };
        var p2 = new Player { Id = Guid.NewGuid(), FirstName = "Bob", SeasonId = seasonId };
        data.Players.Add(p1);
        data.Players.Add(p2);
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            ParticipantIds = new List<Guid> { p1.Id }
        };

        var vm = new CompetitionEditorViewModel(store.Object, competition, seasonId);
        var available = await vm.GetAvailablePlayersAsync();

        Assert.Single(available);
        Assert.Equal(p2.Id, available[0].Id);
    }

    [Fact]
    public async Task GetAvailableTeamsAsync_ExcludesExistingParticipants()
    {
        var (store, data) = CreateStore();
        var seasonId = Guid.NewGuid();
        var t1 = new Team { Id = Guid.NewGuid(), Name = "Aces", SeasonId = seasonId };
        var t2 = new Team { Id = Guid.NewGuid(), Name = "Bandits", SeasonId = seasonId };
        data.Teams.Add(t1);
        data.Teams.Add(t2);
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            ParticipantIds = new List<Guid> { t1.Id }
        };

        var vm = new CompetitionEditorViewModel(store.Object, competition, seasonId);
        var available = await vm.GetAvailableTeamsAsync();

        Assert.Single(available);
        Assert.Equal(t2.Id, available[0].Id);
    }

    // ------------------------------------------------------------------ byes

    /// <summary>A two-round knockout: two first-round matches feeding a final.</summary>
    private static Competition TwoRoundBracket(out Guid teamA, out Guid teamB, out Guid teamC)
    {
        teamA = Guid.NewGuid();
        teamB = Guid.NewGuid();
        teamC = Guid.NewGuid();

        var round1 = new CompetitionRound { RoundNumber = 1 };
        // Match 0 is a real tie; match 1 has nobody for team C to play.
        round1.Matches.Add(new CompetitionMatch { Participant1Id = teamA, Participant2Id = teamB });
        round1.Matches.Add(new CompetitionMatch { Participant1Id = teamC, Participant2Id = null });

        var round2 = new CompetitionRound { RoundNumber = 2 };
        round2.Matches.Add(new CompetitionMatch());

        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            Name = "Team KO",
            Format = CompetitionFormat.TeamKnockout,
        };
        competition.Rounds.Add(round1);
        competition.Rounds.Add(round2);

        return competition;
    }

    [Fact]
    public void ByeCandidate_IsOnlyTheTeamWithNoOpponent()
    {
        var one = Guid.NewGuid();

        Assert.Equal(one, CompetitionEditorViewModel.ByeCandidate(
            new CompetitionMatch { Participant1Id = one }));

        Assert.Equal(one, CompetitionEditorViewModel.ByeCandidate(
            new CompetitionMatch { Participant2Id = one }));

        // Two teams is a match to be played; none is a slot not drawn yet.
        Assert.Null(CompetitionEditorViewModel.ByeCandidate(
            new CompetitionMatch { Participant1Id = one, Participant2Id = Guid.NewGuid() }));

        Assert.Null(CompetitionEditorViewModel.ByeCandidate(new CompetitionMatch()));
    }

    [Fact]
    public async Task GiveBye_SendsTheTeamIntoTheNextRound()
    {
        var (store, _) = CreateStore();
        var competition = TwoRoundBracket(out _, out _, out var teamC);
        var bye = competition.Rounds[0].Matches[1];

        var vm = new CompetitionEditorViewModel(store.Object, competition, currentSeasonId: null);
        await vm.GiveByeAsync(bye.Id);

        Assert.True(bye.IsComplete);
        Assert.Equal(teamC, bye.WinnerId);

        // Match 1 of round 1 feeds the second slot of the final.
        Assert.Equal(teamC, competition.Rounds[1].Matches[0].Participant2Id);
        Assert.Null(competition.Rounds[1].Matches[0].Participant1Id);
    }

    [Fact]
    public async Task GiveBye_RefusesAMatchThatHasTwoTeams()
    {
        var (store, _) = CreateStore();
        var competition = TwoRoundBracket(out _, out _, out _);
        var real = competition.Rounds[0].Matches[0];

        var vm = new CompetitionEditorViewModel(store.Object, competition, currentSeasonId: null);
        await vm.GiveByeAsync(real.Id);

        Assert.False(real.IsComplete);
        Assert.Null(real.WinnerId);
        Assert.Null(competition.Rounds[1].Matches[0].Participant1Id);
    }

    [Fact]
    public async Task UndoBye_TakesTheTeamBackOutOfTheNextRound()
    {
        var (store, _) = CreateStore();
        var competition = TwoRoundBracket(out _, out _, out _);
        var bye = competition.Rounds[0].Matches[1];

        var vm = new CompetitionEditorViewModel(store.Object, competition, currentSeasonId: null);
        await vm.GiveByeAsync(bye.Id);
        await vm.UndoByeAsync(bye.Id);

        Assert.False(bye.IsComplete);
        Assert.Null(bye.WinnerId);
        Assert.Null(competition.Rounds[1].Matches[0].Participant2Id);
    }

    [Fact]
    public async Task GivingAByeDoesNotDisturbTheOtherHalfOfTheDraw()
    {
        var (store, _) = CreateStore();
        var competition = TwoRoundBracket(out var teamA, out var teamB, out _);
        var real = competition.Rounds[0].Matches[0];
        var bye = competition.Rounds[0].Matches[1];

        var vm = new CompetitionEditorViewModel(store.Object, competition, currentSeasonId: null);
        await vm.GiveByeAsync(bye.Id);

        Assert.Equal(teamA, real.Participant1Id);
        Assert.Equal(teamB, real.Participant2Id);
        Assert.False(real.IsComplete);
    }

    [Fact]
    public async Task IsBye_RecognisesOneItGave_AndNotAPlayedMatch()
    {
        var (store, _) = CreateStore();
        var competition = TwoRoundBracket(out var teamA, out _, out _);
        var bye = competition.Rounds[0].Matches[1];
        var real = competition.Rounds[0].Matches[0];

        var vm = new CompetitionEditorViewModel(store.Object, competition, currentSeasonId: null);
        await vm.GiveByeAsync(bye.Id);

        Assert.True(CompetitionEditorViewModel.IsBye(bye));

        real.WinnerId = teamA;
        real.IsComplete = true;
        Assert.False(CompetitionEditorViewModel.IsBye(real));
    }

    [Fact]
    public async Task FillingTheEmptySlotOfAByeCancelsIt()
    {
        var (store, _) = CreateStore();
        var competition = TwoRoundBracket(out _, out _, out var teamC);
        var bye = competition.Rounds[0].Matches[1];
        var latecomer = Guid.NewGuid();

        var vm = new CompetitionEditorViewModel(store.Object, competition, currentSeasonId: null);
        await vm.GiveByeAsync(bye.Id);
        Assert.Equal(teamC, competition.Rounds[1].Matches[0].Participant2Id);

        // An opponent turns up after the bye was given.
        await vm.AssignParticipantToMatchAsync(bye.Id, isSlot1: false, latecomer);

        Assert.False(bye.IsComplete);
        Assert.Null(bye.WinnerId);
        Assert.Equal(teamC, bye.Participant1Id);
        Assert.Equal(latecomer, bye.Participant2Id);

        // ...and the team it had sent through is back out of the final.
        Assert.Null(competition.Rounds[1].Matches[0].Participant2Id);
    }
}
