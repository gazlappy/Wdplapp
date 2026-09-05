using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Tests;

[Collection("Manual season persistence")]
public class HistoricalVenueCopyTests
{
    [Fact]
    public async Task CopyIntoExistingInactiveSeason_PreservesSourcesAndExistingVenues()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = new LeagueContext(new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var sourceSeason = new Season { Name = "Winter 2000", IsLocked = true, IsActive = true };
        var destination = new Season { Name = "Summer 2000", IsActive = false };
        var source = new Venue { Name = "Pub", SeasonId = sourceSeason.Id, Address = "High Street", Notes = "Upstairs", Tables = [new VenueTable { Label = "Main", MaxTeams = 3 }] };
        var existing = new Venue { Name = "Pub", SeasonId = destination.Id };
        context.AddRange(sourceSeason, destination, source, existing);
        await context.SaveChangesAsync();
        var store = new SqliteDataStore(context);
        var copy = new HistoricalVenueCopy(store, destination.Id);
        copy.Add(source.Id);
        copy.Add(source.Id);
        var selected = Assert.Single(copy.Selected);
        Assert.NotEqual(source.Id, selected.Id);
        Assert.NotEqual(source.Tables[0].Id, selected.Tables[0].Id);
        Assert.Equal(2, await context.Venues.CountAsync());
        await copy.SaveAsync();
        context.ChangeTracker.Clear();
        var saved = await context.Venues.AsNoTracking().SingleAsync(v => v.Id == selected.Id);
        Assert.Equal(destination.Id, saved.SeasonId);
        Assert.Equal("High Street", saved.Address);
        Assert.Equal("Upstairs", saved.Notes);
        Assert.Equal(3, Assert.Single(saved.Tables).MaxTeams);
        Assert.Equal(2, await context.Venues.CountAsync(v => v.SeasonId == destination.Id));
        Assert.True(ImportWorkspace.Equal(source, await context.Venues.AsNoTracking().SingleAsync(v => v.Id == source.Id)));
        Assert.False((await context.Seasons.AsNoTracking().SingleAsync(s => s.Id == destination.Id)).IsActive);
        Assert.True((await context.Seasons.AsNoTracking().SingleAsync(s => s.Id == sourceSeason.Id)).IsActive);
        await Assert.ThrowsAsync<InvalidOperationException>(() => copy.SaveAsync());
        Assert.Equal(3, await context.Venues.CountAsync());
    }

    [Fact]
    public async Task DestinationLockedAfterPreview_RejectsWholeCopy()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = new LeagueContext(new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var sourceSeason = new Season();
        var destination = new Season { IsActive = false };
        var source = new Venue { Name = "Pub", SeasonId = sourceSeason.Id };
        context.AddRange(sourceSeason, destination, source);
        await context.SaveChangesAsync();
        var copy = new HistoricalVenueCopy(new SqliteDataStore(context), destination.Id);
        copy.Add(source.Id);
        destination.IsLocked = true;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => copy.SaveAsync());
        Assert.Single(copy.Selected);
        Assert.Empty(await context.Venues.Where(v => v.SeasonId == destination.Id).ToListAsync());
        Assert.Single(copy.Preview.Venues);
    }

    [Fact]
    public async Task SelectionAndFailedSave_LeaveSourceSnapshotIntactForRetry()
    {
        var sourceSeason = new Season();
        var destination = new Season { IsActive = false };
        var first = new Venue { Name = "Pub", SeasonId = sourceSeason.Id };
        var second = new Venue { Name = "Pub", SeasonId = sourceSeason.Id };
        var baseline = new LeagueData { Seasons = [sourceSeason, destination], Venues = [first, second] };
        var before = ImportWorkspace.Clone(baseline);
        var store = new Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(baseline);
        store.SetupSequence(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Save failed")).Returns(Task.CompletedTask);
        var copy = new HistoricalVenueCopy(store.Object, destination.Id);
        copy.Add(first.Id);
        copy.Add(second.Id);
        copy.Remove(copy.Selected[0].Id);
        Assert.False(copy.IsSelected(first.Id));
        Assert.Single(copy.Selected);
        await Assert.ThrowsAsync<IOException>(() => copy.SaveAsync());
        Assert.True(ImportWorkspace.Equal(before, baseline));
        Assert.Equal(2, copy.Preview.Venues.Count);
        await copy.SaveAsync();
        Assert.Single(copy.Preview.Venues.Where(v => v.SeasonId == destination.Id));
        Assert.True(ImportWorkspace.Equal(before, baseline));
    }

    [Fact]
    public async Task InvalidDestinationAndSource_AreRejected()
    {
        var destination = new Season();
        var ownVenue = new Venue { SeasonId = destination.Id };
        var store = new Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(new LeagueData { Seasons = [destination], Venues = [ownVenue] });
        Assert.Throws<InvalidOperationException>(() => new HistoricalVenueCopy(store.Object, Guid.NewGuid()));
        var copy = new HistoricalVenueCopy(store.Object, destination.Id);
        Assert.Throws<InvalidOperationException>(() => copy.Add(ownVenue.Id));
        Assert.Throws<InvalidOperationException>(() => copy.Add(Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => copy.SaveAsync());
        destination.IsLocked = true;
        Assert.Throws<InvalidOperationException>(() => new HistoricalVenueCopy(store.Object, destination.Id));
        store.Verify(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
