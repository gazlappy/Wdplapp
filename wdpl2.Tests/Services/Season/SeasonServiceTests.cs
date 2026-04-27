using Wdpl2;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for SeasonService — managing the currently selected season across the app.
/// </summary>
/// <remarks>
/// Note: Most SeasonService methods cannot be fully unit tested because they depend on DataStore.Data,
/// which requires MAUI FileSystem infrastructure (FileSystem.AppDataDirectory). The static DataStore
/// class initializes readonly fields that access MAUI platform services unavailable in unit tests.
/// 
/// SeasonService.Current property can be tested because it doesn't access DataStore until methods are called.
/// Other methods would require either:
/// 1. Refactoring SeasonService to use dependency injection instead of static DataStore
/// 2. Running tests in a MAUI test host with platform infrastructure initialized
/// 3. Making DataStore testable through partial class extensions or conditional compilation
/// </remarks>
public class SeasonServiceTests
{
    [Fact]
    public void Constructor_SetsCurrent()
    {
        // Arrange & Act
        var service = new SeasonService();

        // Assert
        Assert.Same(service, SeasonService.Current);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void CurrentSeasonId_SetToNewValue_RaisesSeasonChangedEvent()
    {
        // Arrange
        var season = new Season { Id = Guid.NewGuid(), Name = "Test Season" };
        DataStore.Data.Seasons.Clear();
        DataStore.Data.Seasons.Add(season);

        var service = new SeasonService();
        SeasonChangedEventArgs? capturedArgs = null;
        service.SeasonChanged += (sender, args) => capturedArgs = args;

        // Act
        service.CurrentSeasonId = season.Id;

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Null(capturedArgs.OldSeasonId);
        Assert.Equal(season.Id, capturedArgs.NewSeasonId);
        Assert.Equal(season, capturedArgs.NewSeason);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void CurrentSeasonId_SetToSameValue_DoesNotRaiseEvent()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var service = new SeasonService { CurrentSeasonId = seasonId };
        var eventRaised = false;
        service.SeasonChanged += (sender, args) => eventRaised = true;

        // Act
        service.CurrentSeasonId = seasonId;

        // Assert
        Assert.False(eventRaised);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void CurrentSeasonId_SetToNull_RaisesEventWithNullSeason()
    {
        // Arrange
        var oldSeasonId = Guid.NewGuid();
        var service = new SeasonService { CurrentSeasonId = oldSeasonId };
        SeasonChangedEventArgs? capturedArgs = null;
        service.SeasonChanged += (sender, args) => capturedArgs = args;

        // Act
        service.CurrentSeasonId = null;

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal(oldSeasonId, capturedArgs.OldSeasonId);
        Assert.Null(capturedArgs.NewSeasonId);
        Assert.Null(capturedArgs.NewSeason);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void CurrentSeasonId_SetToNonExistentId_RaisesEventWithNullSeason()
    {
        // Arrange
        DataStore.Data.Seasons.Clear();
        var service = new SeasonService();
        var nonExistentId = Guid.NewGuid();
        SeasonChangedEventArgs? capturedArgs = null;
        service.SeasonChanged += (sender, args) => capturedArgs = args;

        // Act
        service.CurrentSeasonId = nonExistentId;

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Null(capturedArgs.OldSeasonId);
        Assert.Equal(nonExistentId, capturedArgs.NewSeasonId);
        Assert.Null(capturedArgs.NewSeason);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void ForceRefresh_WithCurrentSeasonId_RaisesEvent()
    {
        // Arrange
        var season = new Season { Id = Guid.NewGuid(), Name = "Test Season" };
        DataStore.Data.Seasons.Clear();
        DataStore.Data.Seasons.Add(season);

        var service = new SeasonService { CurrentSeasonId = season.Id };
        SeasonChangedEventArgs? capturedArgs = null;
        service.SeasonChanged += (sender, args) => capturedArgs = args;

        // Act
        service.ForceRefresh();

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal(season.Id, capturedArgs.OldSeasonId);
        Assert.Equal(season.Id, capturedArgs.NewSeasonId);
        Assert.Equal(season, capturedArgs.NewSeason);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void ForceRefresh_WithNullCurrentSeasonId_RaisesEventWithNullSeason()
    {
        // Arrange
        var service = new SeasonService();
        SeasonChangedEventArgs? capturedArgs = null;
        service.SeasonChanged += (sender, args) => capturedArgs = args;

        // Act
        service.ForceRefresh();

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Null(capturedArgs.OldSeasonId);
        Assert.Null(capturedArgs.NewSeasonId);
        Assert.Null(capturedArgs.NewSeason);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void Initialize_WithActiveSeasonId_SetsCurrentSeasonId()
    {
        // Arrange
        var activeSeasonId = Guid.NewGuid();
        DataStore.Data.ActiveSeasonId = activeSeasonId;
        var service = new SeasonService();

        // Act
        service.Initialize();

        // Assert
        Assert.Equal(activeSeasonId, service.CurrentSeasonId);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void Initialize_WithoutActiveSeasonIdButActiveSeasonExists_SetsToActiveSeason()
    {
        // Arrange
        var activeSeason = new Season { Id = Guid.NewGuid(), Name = "Active Season", IsActive = true };
        var inactiveSeason = new Season { Id = Guid.NewGuid(), Name = "Inactive Season", IsActive = false };
        DataStore.Data.ActiveSeasonId = null;
        DataStore.Data.Seasons.Clear();
        DataStore.Data.Seasons.Add(inactiveSeason);
        DataStore.Data.Seasons.Add(activeSeason);

        var service = new SeasonService();

        // Act
        service.Initialize();

        // Assert
        Assert.Equal(activeSeason.Id, service.CurrentSeasonId);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void Initialize_WithoutActiveSeasonIdOrActiveSeason_SetsToMostRecentSeason()
    {
        // Arrange
        var oldSeason = new Season
        {
            Id = Guid.NewGuid(),
            Name = "Old Season",
            IsActive = false,
            StartDate = new DateTime(2020, 1, 1)
        };
        var recentSeason = new Season
        {
            Id = Guid.NewGuid(),
            Name = "Recent Season",
            IsActive = false,
            StartDate = new DateTime(2023, 1, 1)
        };
        DataStore.Data.ActiveSeasonId = null;
        DataStore.Data.Seasons.Clear();
        DataStore.Data.Seasons.Add(oldSeason);
        DataStore.Data.Seasons.Add(recentSeason);

        var service = new SeasonService();

        // Act
        service.Initialize();

        // Assert
        Assert.Equal(recentSeason.Id, service.CurrentSeasonId);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void Initialize_WithNoSeasons_LeavesCurrentSeasonIdNull()
    {
        // Arrange
        DataStore.Data.ActiveSeasonId = null;
        DataStore.Data.Seasons.Clear();
        var service = new SeasonService();

        // Act
        service.Initialize();

        // Assert
        Assert.Null(service.CurrentSeasonId);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void GetCurrentSeason_WithValidCurrentSeasonId_ReturnsSeason()
    {
        // Arrange
        var season = new Season { Id = Guid.NewGuid(), Name = "Test Season" };
        DataStore.Data.Seasons.Clear();
        DataStore.Data.Seasons.Add(season);

        var service = new SeasonService { CurrentSeasonId = season.Id };

        // Act
        var result = service.GetCurrentSeason();

        // Assert
        Assert.Equal(season, result);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void GetCurrentSeason_WithNullCurrentSeasonId_ReturnsNull()
    {
        // Arrange
        var service = new SeasonService();

        // Act
        var result = service.GetCurrentSeason();

        // Assert
        Assert.Null(result);
    }

    [Fact(Skip = "Requires MAUI FileSystem infrastructure - DataStore.Data cannot be accessed in unit tests")]
    public void GetCurrentSeason_WithNonExistentCurrentSeasonId_ReturnsNull()
    {
        // Arrange
        DataStore.Data.Seasons.Clear();
        var service = new SeasonService { CurrentSeasonId = Guid.NewGuid() };

        // Act
        var result = service.GetCurrentSeason();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SeasonChangedEventArgs_Constructor_SetsProperties()
    {
        // Arrange
        var oldSeasonId = Guid.NewGuid();
        var newSeasonId = Guid.NewGuid();
        var newSeason = new Season { Id = newSeasonId, Name = "Test Season" };

        // Act
        var eventArgs = new SeasonChangedEventArgs(oldSeasonId, newSeasonId, newSeason);

        // Assert
        Assert.Equal(oldSeasonId, eventArgs.OldSeasonId);
        Assert.Equal(newSeasonId, eventArgs.NewSeasonId);
        Assert.Equal(newSeason, eventArgs.NewSeason);
    }

    [Fact]
    public void SeasonChangedEventArgs_Constructor_WithNullValues_SetsPropertiesToNull()
    {
        // Arrange & Act
        var eventArgs = new SeasonChangedEventArgs(null, null, null);

        // Assert
        Assert.Null(eventArgs.OldSeasonId);
        Assert.Null(eventArgs.NewSeasonId);
        Assert.Null(eventArgs.NewSeason);
    }
}
