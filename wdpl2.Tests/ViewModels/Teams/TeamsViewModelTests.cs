using Moq;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.ViewModels;

namespace wdpl2.Tests;

/// <summary>
/// Tests for TeamsViewModel — team list and CRUD operations.
/// </summary>
public class TeamsViewModelTests
{
    [Fact]
    public void Constructor_ValidParameters_InitializesViewModel()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();
        var mockSeasonService = new Mock<ISeasonService>();

        // Act
        var viewModel = new TeamsViewModel(mockDataStore.Object, mockSeasonService.Object);

        // Assert
        Assert.NotNull(viewModel);
    }

    [Fact]
    public async Task Constructor_ValidParameters_SubscribesToSeasonChangedEvent()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();
        var mockSeasonService = new Mock<ISeasonService>();
        mockDataStore.Setup(ds => ds.GetTeamsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>());
        mockDataStore.Setup(ds => ds.GetPlayersAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Player>());
        mockDataStore.Setup(ds => ds.GetVenuesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Venue>());

        // Act
        var viewModel = new TeamsViewModel(mockDataStore.Object, mockSeasonService.Object);
        await Task.Delay(100); // Allow SafeFireAndForget to complete

        // Trigger the event
        var eventArgs = new SeasonChangedEventArgs(Guid.NewGuid(), Guid.NewGuid(), new Season { Name = "Test Season" });
        mockSeasonService.Raise(s => s.SeasonChanged += null, eventArgs);
        await Task.Delay(100); // Allow event handler to complete

        // Assert - verify that the event handler triggered data loads
        mockDataStore.Verify(ds => ds.GetTeamsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task OnSeasonChanged_WhenTriggered_LoadsTeams()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();
        var mockSeasonService = new Mock<ISeasonService>();
        var newSeasonId = Guid.NewGuid();
        var season = new Season { Id = newSeasonId, Name = "New Season" };
        
        mockDataStore.Setup(ds => ds.GetTeamsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>());
        mockDataStore.Setup(ds => ds.GetPlayersAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Player>());
        mockDataStore.Setup(ds => ds.GetVenuesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Venue>());

        var viewModel = new TeamsViewModel(mockDataStore.Object, mockSeasonService.Object);
        await Task.Delay(100); // Allow constructor's SafeFireAndForget to complete
        mockDataStore.Invocations.Clear();

        // Act
        var eventArgs = new SeasonChangedEventArgs(Guid.NewGuid(), newSeasonId, season);
        mockSeasonService.Raise(s => s.SeasonChanged += null, eventArgs);
        await Task.Delay(100); // Allow event handler to complete

        // Assert
        mockDataStore.Verify(ds => ds.GetTeamsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnSeasonChanged_WhenTriggered_LoadsReferenceData()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();
        var mockSeasonService = new Mock<ISeasonService>();
        var newSeasonId = Guid.NewGuid();
        var season = new Season { Id = newSeasonId, Name = "New Season" };
        
        mockDataStore.Setup(ds => ds.GetTeamsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>());
        mockDataStore.Setup(ds => ds.GetPlayersAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Player>());
        mockDataStore.Setup(ds => ds.GetVenuesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Venue>());

        var viewModel = new TeamsViewModel(mockDataStore.Object, mockSeasonService.Object);
        await Task.Delay(100); // Allow constructor's SafeFireAndForget to complete
        mockDataStore.Invocations.Clear();

        // Act
        var eventArgs = new SeasonChangedEventArgs(Guid.NewGuid(), newSeasonId, season);
        mockSeasonService.Raise(s => s.SeasonChanged += null, eventArgs);
        await Task.Delay(100); // Allow event handler to complete

        // Assert
        mockDataStore.Verify(ds => ds.GetPlayersAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
        mockDataStore.Verify(ds => ds.GetVenuesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnSeasonChanged_WithNullSeason_DoesNotLoadData()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();
        var mockSeasonService = new Mock<ISeasonService>();
        
        mockDataStore.Setup(ds => ds.GetTeamsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>());
        mockDataStore.Setup(ds => ds.GetPlayersAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Player>());
        mockDataStore.Setup(ds => ds.GetVenuesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Venue>());

        var viewModel = new TeamsViewModel(mockDataStore.Object, mockSeasonService.Object);
        await Task.Delay(100); // Allow constructor's SafeFireAndForget to complete
        mockDataStore.Invocations.Clear();

        // Act
        var eventArgs = new SeasonChangedEventArgs(null, null, null);
        mockSeasonService.Raise(s => s.SeasonChanged += null, eventArgs);
        await Task.Delay(100); // Allow event handler to complete

        // Assert - With null season, loads return early without hitting datastore
        mockDataStore.Verify(ds => ds.GetTeamsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        mockDataStore.Verify(ds => ds.GetPlayersAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnSeasonChanged_MultipleInvocations_TriggersLoadsEachTime()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();
        var mockSeasonService = new Mock<ISeasonService>();
        var season1 = new Season { Id = Guid.NewGuid(), Name = "Season 1" };
        var season2 = new Season { Id = Guid.NewGuid(), Name = "Season 2" };
        
        mockDataStore.Setup(ds => ds.GetTeamsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>());
        mockDataStore.Setup(ds => ds.GetPlayersAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Player>());
        mockDataStore.Setup(ds => ds.GetVenuesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Venue>());

        var viewModel = new TeamsViewModel(mockDataStore.Object, mockSeasonService.Object);
        await Task.Delay(100); // Allow constructor's SafeFireAndForget to complete
        mockDataStore.Invocations.Clear();

        // Act
        var eventArgs1 = new SeasonChangedEventArgs(null, season1.Id, season1);
        mockSeasonService.Raise(s => s.SeasonChanged += null, eventArgs1);
        await Task.Delay(100);

        var eventArgs2 = new SeasonChangedEventArgs(season1.Id, season2.Id, season2);
        mockSeasonService.Raise(s => s.SeasonChanged += null, eventArgs2);
        await Task.Delay(100);

        // Assert
        mockDataStore.Verify(ds => ds.GetTeamsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockDataStore.Verify(ds => ds.GetPlayersAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
