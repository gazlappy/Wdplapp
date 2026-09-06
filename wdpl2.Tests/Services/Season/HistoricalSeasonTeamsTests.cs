using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Tests;

[Collection("Manual season persistence")]
public class HistoricalSeasonTeamsTests
{
    private static Mock<IDataStore> Store(LeagueData data)
    {
        var store = new Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(data);
        store.Setup(s => s.GetSeasonsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => data.Seasons.ToList());
        store.Setup(s => s.GetTeamsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? season, CancellationToken _) => data.Teams.Where(t => t.SeasonId == season).ToList());
        store.Setup(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return store;
    }

    [Fact]
    public async Task SqliteCopy_PreservesHistoryAndActivation_WithoutOldPlacementsOrCredentials()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = new LeagueContext(new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var old = new Season { Name = "Winter", IsLocked = true, IsActive = true };
        var current = new Season { Name = "Summer", IsActive = false };
        var division = new Division { Name = "First", SeasonId = old.Id };
        var venue = new Venue { Name = "Pub", SeasonId = old.Id, Tables = [new VenueTable { Label = "Main" }] };
        var source = new Team
        {
            Name = "Pub", SeasonId = old.Id, DivisionId = division.Id, VenueId = venue.Id,
            TableId = venue.Tables[0].Id, ProvidesFood = true, LogoCatalogId = "crest",
            Captain = "Old captain", CaptainEmail = "old@example.com", CaptainPhone = "123", CaptainPin = "1234", Notes = "Old notes"
        };
        context.AddRange(old, current, division, venue, source);
        await context.SaveChangesAsync();
        var before = ImportWorkspace.Clone(source);
        var store = new SqliteDataStore(context);
        await store.SaveAsync();
        var draft = new HistoricalSeasonTeams(store, current.Id);
        draft.Add(source.Id);
        Assert.Single(await context.Teams.ToListAsync());
        await draft.SaveAsync();
        context.ChangeTracker.Clear();
        var copied = await context.Teams.AsNoTracking().SingleAsync(t => t.SeasonId == current.Id);
        Assert.NotEqual(source.Id, copied.Id);
        Assert.Equal(source.Id, copied.GlobalTeamId);
        Assert.Equal("Pub", copied.Name);
        Assert.True(copied.ProvidesFood);
        Assert.Equal("crest", copied.LogoCatalogId);
        Assert.Null(copied.DivisionId);
        Assert.Null(copied.VenueId);
        Assert.Null(copied.TableId);
        Assert.Null(copied.CaptainPlayerId);
        Assert.Null(copied.Captain);
        Assert.Null(copied.CaptainPin);
        Assert.Null(copied.CaptainEmail);
        Assert.Null(copied.CaptainPhone);
        Assert.Null(copied.Notes);
        Assert.Empty(await context.Players.ToListAsync());
        Assert.True(ImportWorkspace.Equal(before, await context.Teams.AsNoTracking().SingleAsync(t => t.Id == source.Id)));
        Assert.False((await context.Seasons.AsNoTracking().SingleAsync(s => s.Id == current.Id)).IsActive);
        Assert.True((await context.Seasons.AsNoTracking().SingleAsync(s => s.Id == old.Id)).IsActive);
        await Assert.ThrowsAsync<InvalidOperationException>(() => draft.SaveAsync());
        Assert.Equal("Already in this season", new HistoricalSeasonTeams(store, current.Id).UnavailableReason(source));
    }

    [Fact]
    public async Task Selection_UsesIdsNotNames_AndCanRemoveAndReadd()
    {
        var old = new Season();
        var other = new Season();
        var current = new Season();
        var identity = Guid.NewGuid();
        var first = new Team { SeasonId = old.Id, Name = "Pub", GlobalTeamId = identity };
        var same = new Team { SeasonId = other.Id, Name = "Renamed", GlobalTeamId = identity };
        var namesake = new Team { SeasonId = old.Id, Name = "Pub" };
        var data = new LeagueData { Seasons = [old, other, current], Teams = [first, same, namesake] };
        var draft = new HistoricalSeasonTeams(Store(data).Object, current.Id);
        Assert.Equal(2, draft.SourceTeams(old.Id, "pub").Count);
        Assert.Empty(draft.SourceTeams(other.Id, "pub"));
        draft.Add(first.Id);
        draft.Add(same.Id);
        Assert.Single(draft.Selected);
        draft.Add(namesake.Id);
        Assert.Equal(2, draft.Selected.Count);
        draft.Remove(first.Id);
        Assert.False(draft.IsSelected(same));
        draft.Add(first.Id);
        await draft.SaveAsync();
        Assert.Equal(2, draft.Preview.Teams.Count(t => t.SeasonId == current.Id));
        Assert.Equal(3, data.Teams.Count);
    }

    [Fact]
    public async Task ExistingOrNewlyAddedIdentity_IsNotDuplicated()
    {
        var old = new Season();
        var current = new Season();
        var source = new Team { SeasonId = old.Id, Name = "Pub" };
        var data = new LeagueData { Seasons = [old, current], Teams = [source] };
        var store = Store(data);
        var draft = new HistoricalSeasonTeams(store.Object, current.Id);
        draft.Add(source.Id);
        var existing = new Team { SeasonId = current.Id, GlobalTeamId = source.Id, Name = "Renamed" };
        data.Teams.Add(existing);
        await Assert.ThrowsAsync<InvalidOperationException>(() => draft.SaveAsync());
        Assert.Single(draft.Preview.Teams);
        var reopened = new HistoricalSeasonTeams(store.Object, current.Id);
        Assert.Equal("Already in this season", reopened.UnavailableReason(source));
        Assert.Throws<InvalidOperationException>(() => reopened.Add(source.Id));
        store.Verify(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DestinationLockedOrDeletedAfterPreview_RejectsSave(bool locked)
    {
        var old = new Season();
        var current = new Season();
        var source = new Team { SeasonId = old.Id, Name = "Pub" };
        var data = new LeagueData { Seasons = [old, current], Teams = [source] };
        var store = Store(data);
        var draft = new HistoricalSeasonTeams(store.Object, current.Id);
        draft.Add(source.Id);
        if (locked) current.IsLocked = true;
        else data.Seasons.Remove(current);
        await Assert.ThrowsAsync<InvalidOperationException>(() => draft.SaveAsync());
        Assert.Single(draft.Selected);
        Assert.Single(draft.Preview.Teams);
        store.Verify(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailedCommit_RetainsSelectionForRetryWithoutMutatingSource()
    {
        var old = new Season();
        var current = new Season();
        var source = new Team { SeasonId = old.Id, Name = "Pub" };
        var data = new LeagueData { Seasons = [old, current], Teams = [source] };
        var before = ImportWorkspace.Clone(data);
        var store = Store(data);
        store.SetupSequence(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Failed")).Returns(Task.CompletedTask);
        var draft = new HistoricalSeasonTeams(store.Object, current.Id);
        draft.Add(source.Id);
        await Assert.ThrowsAsync<IOException>(() => draft.SaveAsync());
        Assert.Single(draft.Selected);
        Assert.Single(draft.Preview.Teams);
        await draft.SaveAsync();
        Assert.Single(draft.Preview.Teams.Where(t => t.SeasonId == current.Id));
        Assert.True(ImportWorkspace.Equal(before, data));
    }

    [Fact]
    public async Task InvalidSourcesAndEmptySelection_NeverCommit()
    {
        var old = new Season();
        var current = new Season();
        var own = new Team { SeasonId = current.Id, Name = "Own" };
        var invalid = new Team { SeasonId = old.Id, Name = " " };
        var orphan = new Team { Name = "No season" };
        var data = new LeagueData { Seasons = [old, current], Teams = [own, invalid, orphan] };
        var store = Store(data);
        var draft = new HistoricalSeasonTeams(store.Object, current.Id);
        foreach (var id in new[] { own.Id, invalid.Id, orphan.Id, Guid.NewGuid() })
            Assert.Throws<InvalidOperationException>(() => draft.Add(id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => draft.SaveAsync());
        Assert.Throws<InvalidOperationException>(() => new HistoricalSeasonTeams(store.Object, Guid.NewGuid()));
        current.IsLocked = true;
        Assert.Throws<InvalidOperationException>(() => new HistoricalSeasonTeams(store.Object, current.Id));
        store.Verify(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
