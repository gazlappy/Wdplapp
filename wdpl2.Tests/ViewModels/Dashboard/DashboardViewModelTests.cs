using Moq;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.ViewModels;

namespace Wdpl2.Tests;

/// <summary>
/// Tests for DashboardViewModel — at-a-glance league stats.
/// Verifies DI surface; full LoadDashboardAsync exercises Task.Run + RatingCalculator
/// which is integration territory.
/// </summary>
public class DashboardViewModelTests
{
    private static (Mock<ISeasonService> Season, Mock<IDataStore> DataStore) CreateMocks()
    {
        var season = new Mock<ISeasonService>();
        season.SetupGet(x => x.CurrentSeasonId).Returns((Guid?)null);

        var dataStore = new Mock<IDataStore>();
        dataStore.Setup(x => x.GetData()).Returns(new LeagueData());
        return (season, dataStore);
    }

    [Fact]
    public void Constructor_ValidDependencies_InitializesViewModel()
    {
        var (season, dataStore) = CreateMocks();

        var vm = new DashboardViewModel(season.Object, dataStore.Object);

        Assert.NotNull(vm);
        // Initial value is "No Season"; an async load fires from the constructor and
        // (with no current season) updates it to "No Season Selected".
        Assert.Contains("No Season", vm.SeasonName);
    }

    [Fact]
    public void Constructor_NullDataStore_Throws()
    {
        var season = new Mock<ISeasonService>();
        Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(season.Object, null!));
    }

    [Fact]
    public void Constructor_SubscribesToSeasonChanged()
    {
        var (season, dataStore) = CreateMocks();

        var vm = new DashboardViewModel(season.Object, dataStore.Object);

        season.VerifyAdd(s => s.SeasonChanged += It.IsAny<EventHandler<SeasonChangedEventArgs>>(), Times.Once);
    }

    [Fact]
    public void Cleanup_UnsubscribesFromSeasonChanged()
    {
        var (season, dataStore) = CreateMocks();
        var vm = new DashboardViewModel(season.Object, dataStore.Object);

        vm.Cleanup();

        season.VerifyRemove(s => s.SeasonChanged -= It.IsAny<EventHandler<SeasonChangedEventArgs>>(), Times.Once);
    }
}
