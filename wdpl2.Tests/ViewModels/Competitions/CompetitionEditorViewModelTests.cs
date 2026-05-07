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
}
