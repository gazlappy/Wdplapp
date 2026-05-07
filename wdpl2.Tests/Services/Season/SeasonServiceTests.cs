using Moq;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for SeasonService — managing the currently selected season across the app.
/// Uses a mocked <see cref="IDataStore"/> to avoid MAUI FileSystem infrastructure.
/// </summary>
public class SeasonServiceTests
{
    private static (SeasonService Service, LeagueData Data, Mock<IDataStore> Mock) CreateService()
    {
        var data = new LeagueData();
        var mock = new Mock<IDataStore>();
        mock.Setup(x => x.GetData()).Returns(data);
        var service = new SeasonService(mock.Object);
        return (service, data, mock);
    }

    [Fact]
    public void Constructor_SetsCurrent()
    {
        var (service, _, _) = CreateService();
        Assert.Same(service, SeasonService.Current);
    }

    [Fact]
    public void Constructor_NullDataStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SeasonService(null!));
    }

    [Fact]
    public void CurrentSeasonId_SetToNewValue_RaisesSeasonChangedEvent()
    {
        var (service, data, _) = CreateService();
        var season = new Season { Id = Guid.NewGuid(), Name = "Test Season" };
        data.Seasons.Add(season);

        SeasonChangedEventArgs? capturedArgs = null;
        service.SeasonChanged += (_, args) => capturedArgs = args;

        service.CurrentSeasonId = season.Id;

        Assert.NotNull(capturedArgs);
        Assert.Null(capturedArgs!.OldSeasonId);
        Assert.Equal(season.Id, capturedArgs.NewSeasonId);
        Assert.Equal(season, capturedArgs.NewSeason);
    }

    [Fact]
    public void CurrentSeasonId_SetToSameValue_DoesNotRaiseEvent()
    {
        var (service, _, _) = CreateService();
        var seasonId = Guid.NewGuid();
        service.CurrentSeasonId = seasonId;

        var eventRaised = false;
        service.SeasonChanged += (_, _) => eventRaised = true;

        service.CurrentSeasonId = seasonId;

        Assert.False(eventRaised);
    }

    [Fact]
    public void CurrentSeasonId_SetToNull_RaisesEventWithNullSeason()
    {
        var (service, _, _) = CreateService();
        var oldSeasonId = Guid.NewGuid();
        service.CurrentSeasonId = oldSeasonId;

        SeasonChangedEventArgs? capturedArgs = null;
        service.SeasonChanged += (_, args) => capturedArgs = args;

        service.CurrentSeasonId = null;

        Assert.NotNull(capturedArgs);
        Assert.Equal(oldSeasonId, capturedArgs!.OldSeasonId);
        Assert.Null(capturedArgs.NewSeasonId);
        Assert.Null(capturedArgs.NewSeason);
    }

    [Fact]
    public void CurrentSeasonId_SetToNonExistentId_RaisesEventWithNullSeason()
    {
        var (service, _, _) = CreateService();
        var nonExistentId = Guid.NewGuid();

        SeasonChangedEventArgs? capturedArgs = null;
        service.SeasonChanged += (_, args) => capturedArgs = args;

        service.CurrentSeasonId = nonExistentId;

        Assert.NotNull(capturedArgs);
        Assert.Null(capturedArgs!.OldSeasonId);
        Assert.Equal(nonExistentId, capturedArgs.NewSeasonId);
        Assert.Null(capturedArgs.NewSeason);
    }

    [Fact]
    public void ForceRefresh_WithCurrentSeasonId_RaisesEvent()
    {
        var (service, data, _) = CreateService();
        var season = new Season { Id = Guid.NewGuid(), Name = "Test Season" };
        data.Seasons.Add(season);
        service.CurrentSeasonId = season.Id;

        SeasonChangedEventArgs? capturedArgs = null;
        service.SeasonChanged += (_, args) => capturedArgs = args;

        service.ForceRefresh();

        Assert.NotNull(capturedArgs);
        Assert.Equal(season.Id, capturedArgs!.OldSeasonId);
        Assert.Equal(season.Id, capturedArgs.NewSeasonId);
        Assert.Equal(season, capturedArgs.NewSeason);
    }

    [Fact]
    public void ForceRefresh_WithNullCurrentSeasonId_RaisesEventWithNullSeason()
    {
        var (service, _, _) = CreateService();

        SeasonChangedEventArgs? capturedArgs = null;
        service.SeasonChanged += (_, args) => capturedArgs = args;

        service.ForceRefresh();

        Assert.NotNull(capturedArgs);
        Assert.Null(capturedArgs!.OldSeasonId);
        Assert.Null(capturedArgs.NewSeasonId);
        Assert.Null(capturedArgs.NewSeason);
    }

    [Fact]
    public void Initialize_WithActiveSeasonId_SetsCurrentSeasonId()
    {
        var (service, data, _) = CreateService();
        var activeSeasonId = Guid.NewGuid();
        data.ActiveSeasonId = activeSeasonId;

        service.Initialize();

        Assert.Equal(activeSeasonId, service.CurrentSeasonId);
    }

    [Fact]
    public void Initialize_WithoutActiveSeasonIdButActiveSeasonExists_SetsToActiveSeason()
    {
        var (service, data, _) = CreateService();
        var activeSeason = new Season { Id = Guid.NewGuid(), Name = "Active", IsActive = true };
        var inactiveSeason = new Season { Id = Guid.NewGuid(), Name = "Inactive", IsActive = false };
        data.Seasons.Add(inactiveSeason);
        data.Seasons.Add(activeSeason);

        service.Initialize();

        Assert.Equal(activeSeason.Id, service.CurrentSeasonId);
    }

    [Fact]
    public void Initialize_WithoutActiveSeasonIdOrActiveSeason_SetsToMostRecentSeason()
    {
        var (service, data, _) = CreateService();
        var oldSeason = new Season { Id = Guid.NewGuid(), Name = "Old", IsActive = false, StartDate = new DateTime(2020, 1, 1) };
        var recentSeason = new Season { Id = Guid.NewGuid(), Name = "Recent", IsActive = false, StartDate = new DateTime(2023, 1, 1) };
        data.Seasons.Add(oldSeason);
        data.Seasons.Add(recentSeason);

        service.Initialize();

        Assert.Equal(recentSeason.Id, service.CurrentSeasonId);
    }

    [Fact]
    public void Initialize_WithNoSeasons_LeavesCurrentSeasonIdNull()
    {
        var (service, _, _) = CreateService();

        service.Initialize();

        Assert.Null(service.CurrentSeasonId);
    }

    [Fact]
    public void GetCurrentSeason_WithValidCurrentSeasonId_ReturnsSeason()
    {
        var (service, data, _) = CreateService();
        var season = new Season { Id = Guid.NewGuid(), Name = "Test Season" };
        data.Seasons.Add(season);
        service.CurrentSeasonId = season.Id;

        var result = service.GetCurrentSeason();

        Assert.Equal(season, result);
    }

    [Fact]
    public void GetCurrentSeason_WithNullCurrentSeasonId_ReturnsNull()
    {
        var (service, _, _) = CreateService();

        var result = service.GetCurrentSeason();

        Assert.Null(result);
    }

    [Fact]
    public void GetCurrentSeason_WithNonExistentCurrentSeasonId_ReturnsNull()
    {
        var (service, _, _) = CreateService();
        service.CurrentSeasonId = Guid.NewGuid();

        var result = service.GetCurrentSeason();

        Assert.Null(result);
    }

    [Fact]
    public void SeasonChangedEventArgs_Constructor_SetsProperties()
    {
        var oldSeasonId = Guid.NewGuid();
        var newSeasonId = Guid.NewGuid();
        var newSeason = new Season { Id = newSeasonId, Name = "Test Season" };

        var eventArgs = new SeasonChangedEventArgs(oldSeasonId, newSeasonId, newSeason);

        Assert.Equal(oldSeasonId, eventArgs.OldSeasonId);
        Assert.Equal(newSeasonId, eventArgs.NewSeasonId);
        Assert.Equal(newSeason, eventArgs.NewSeason);
    }

    [Fact]
    public void SeasonChangedEventArgs_Constructor_WithNullValues_SetsPropertiesToNull()
    {
        var eventArgs = new SeasonChangedEventArgs(null, null, null);

        Assert.Null(eventArgs.OldSeasonId);
        Assert.Null(eventArgs.NewSeasonId);
        Assert.Null(eventArgs.NewSeason);
    }
}
