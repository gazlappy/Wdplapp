using Moq;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.ViewModels;

namespace wdpl2.Tests;

/// <summary>
/// Tests for LeagueTablesViewModel — displays league standings.
/// </summary>
public class LeagueTablesViewModelTests
{
    [Fact]
    public void Constructor_ValidParameters_InitializesViewModel()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();
        var mockSeasonService = new Mock<ISeasonService>();

        // Act
        var viewModel = new LeagueTablesViewModel(mockDataStore.Object, mockSeasonService.Object);

        // Assert
        Assert.NotNull(viewModel);
        mockSeasonService.VerifyAdd(s => s.SeasonChanged += It.IsAny<EventHandler<SeasonChangedEventArgs>>(), Times.Once);
    }

    [Fact]
    public void Constructor_ValidParameters_StoresDataStore()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();
        var mockSeasonService = new Mock<ISeasonService>();

        // Act
        var viewModel = new LeagueTablesViewModel(mockDataStore.Object, mockSeasonService.Object);

        // Assert
        Assert.NotNull(viewModel);
    }

    [Fact]
    public void OnSeasonChanged_WhenRaised_UpdatesCurrentSeasonId()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();
        var mockSeasonService = new Mock<ISeasonService>();
        var viewModel = new LeagueTablesViewModel(mockDataStore.Object, mockSeasonService.Object);
        var newSeasonId = Guid.NewGuid();
        var season = new Season { Id = newSeasonId, Name = "Test Season" };
        var eventArgs = new SeasonChangedEventArgs(null, newSeasonId, season);

        // Act
        mockSeasonService.Raise(s => s.SeasonChanged += null, eventArgs);

        // Assert
        Assert.Equal(newSeasonId, viewModel.CurrentSeasonId);
    }

    [Fact]
    public void OnSeasonChanged_WithNullSeasonId_UpdatesCurrentSeasonIdToNull()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();
        var mockSeasonService = new Mock<ISeasonService>();
        var viewModel = new LeagueTablesViewModel(mockDataStore.Object, mockSeasonService.Object);
        var eventArgs = new SeasonChangedEventArgs(Guid.NewGuid(), null, null);

        // Act
        mockSeasonService.Raise(s => s.SeasonChanged += null, eventArgs);

        // Assert
        Assert.Null(viewModel.CurrentSeasonId);
    }
}
