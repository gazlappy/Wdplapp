using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Tests;

[Collection("Manual season persistence")]
public class HistoricalTeamPlayersTests
{
    private static Mock<IDataStore> Store(LeagueData data)
    {
        var store = new Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(data);
        store.Setup(s => s.GetTeamsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? season, CancellationToken _) => data.Teams.Where(t => t.SeasonId == season).ToList());
        store.Setup(s => s.GetPlayersAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? season, CancellationToken _) => data.Players.Where(p => p.SeasonId == season).ToList());
        store.Setup(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return store;
    }

    [Fact]
    public async Task SqliteSave_AddsToExistingInactiveTeam_WithoutChangingHistory()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = new LeagueContext(new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var old = new Season { Name = "Winter 2000", IsLocked = true, IsActive = true };
        var current = new Season { Name = "Summer 2000", IsActive = false };
        var oldTeam = new Team { Name = "Old team", SeasonId = old.Id };
        var team = new Team { Name = "New team", SeasonId = current.Id };
        var source = new Player { FirstName = "Alex", LastName = "Smith", SeasonId = old.Id, TeamId = oldTeam.Id, IsActive = false, DeactivationReason = "Old", Notes = "Historical notes" };
        context.AddRange(old, current, oldTeam, team, source);
        await context.SaveChangesAsync();
        var before = ImportWorkspace.Clone(source);
        var store = new SqliteDataStore(context);
        await store.SaveAsync();
        var draft = new HistoricalTeamPlayers(store, team.Id);
        draft.Add(source.Id);
        Assert.Equal(1, await context.Players.CountAsync());
        await draft.SaveAsync();
        context.ChangeTracker.Clear();
        var copied = await context.Players.AsNoTracking().SingleAsync(p => p.SeasonId == current.Id);
        Assert.NotEqual(source.Id, copied.Id);
        Assert.Equal(source.Id, copied.GlobalPlayerId);
        Assert.Equal(team.Id, copied.TeamId);
        Assert.Equal("Alex Smith", copied.Name);
        Assert.True(copied.IsActive);
        Assert.Null(copied.DeactivationReason);
        Assert.Null(copied.Notes);
        Assert.Empty(copied.TransferHistory);
        Assert.Empty(copied.Availability);
        Assert.True(ImportWorkspace.Equal(before, await context.Players.AsNoTracking().SingleAsync(p => p.Id == source.Id)));
        Assert.False((await context.Seasons.AsNoTracking().SingleAsync(s => s.Id == current.Id)).IsActive);
        Assert.Equal(2, await context.Teams.CountAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => draft.SaveAsync());
        var reopened = new HistoricalTeamPlayers(store, team.Id);
        Assert.Equal("Already on this team", reopened.UnavailableReason(source));
    }

    [Fact]
    public async Task ExistingUnassignedIdentity_IsAssignedWithoutResettingItsHistoryOrStatus()
    {
        var old = new Season();
        var current = new Season { IsActive = false };
        var team = new Team { SeasonId = current.Id };
        var source = new Player { SeasonId = old.Id, FirstName = "Alex" };
        var existing = new Player { SeasonId = current.Id, GlobalPlayerId = source.Id, FirstName = "Alex", IsActive = false, Notes = "Keep", TransferHistory = [new PlayerTransfer { FromTeamName = "Earlier" }] };
        var data = new LeagueData { Seasons = [old, current], Teams = [team], Players = [source, existing] };
        var store = Store(data);
        LeagueData? saved = null;
        store.Setup(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()))
            .Callback<LeagueData, LeagueData, CancellationToken>((_, after, _) => saved = ImportWorkspace.Clone(after)).Returns(Task.CompletedTask);
        var draft = new HistoricalTeamPlayers(store.Object, team.Id);
        Assert.True(draft.WillAssignExisting(source));
        draft.Add(source.Id);
        await draft.SaveAsync();
        var assigned = Assert.Single(saved!.Players.Where(p => p.SeasonId == current.Id));
        Assert.Equal(existing.Id, assigned.Id);
        Assert.Equal(team.Id, assigned.TeamId);
        Assert.False(assigned.IsActive);
        Assert.Equal("Keep", assigned.Notes);
        Assert.Single(assigned.TransferHistory);
        Assert.Null(existing.TeamId);
    }

    [Fact]
    public void Selection_UsesExplicitIdentityNotNames_AndFiltersSeasonAndFormerTeam()
    {
        var old = new Season();
        var other = new Season();
        var current = new Season();
        var team = new Team { SeasonId = current.Id };
        var former = new Team { SeasonId = old.Id };
        var identity = Guid.NewGuid();
        var first = new Player { SeasonId = old.Id, TeamId = former.Id, Name = "J. Smith", GlobalPlayerId = identity };
        var same = new Player { SeasonId = other.Id, Name = "Renamed", GlobalPlayerId = identity };
        var namesake = new Player { SeasonId = old.Id, Name = "J. Smith" };
        var data = new LeagueData { Seasons = [old, other, current], Teams = [team, former], Players = [first, same, namesake] };
        var draft = new HistoricalTeamPlayers(Store(data).Object, team.Id);
        Assert.Equal(first.Id, Assert.Single(draft.SourcePlayers(old.Id, former.Id, "smith")).Id);
        Assert.Equal(namesake.Id, Assert.Single(draft.SourcePlayers(old.Id, null, null)).Id);
        draft.Add(first.Id);
        draft.Add(same.Id);
        Assert.Single(draft.Selected);
        draft.Add(namesake.Id);
        Assert.Equal(2, draft.Selected.Count);
        draft.Remove(first.Id);
        Assert.False(draft.IsSelected(same));
        Assert.Single(draft.Selected);
    }

    [Fact]
    public async Task AssignedAndAmbiguousIdentities_AreNotDuplicatedOrMoved()
    {
        var old = new Season();
        var current = new Season();
        var team = new Team { SeasonId = current.Id };
        var otherTeam = new Team { SeasonId = current.Id };
        var source = new Player { SeasonId = old.Id };
        var existing = new Player { SeasonId = current.Id, GlobalPlayerId = source.Id, TeamId = otherTeam.Id };
        var data = new LeagueData { Seasons = [old, current], Teams = [team, otherTeam], Players = [source, existing] };
        var draft = new HistoricalTeamPlayers(Store(data).Object, team.Id);
        Assert.Contains("transfer", draft.UnavailableReason(source));
        Assert.Throws<InvalidOperationException>(() => draft.Add(source.Id));
        Assert.Throws<InvalidOperationException>(() => draft.Add(existing.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => draft.SaveAsync());
        data.Players.Add(new Player { SeasonId = current.Id, GlobalPlayerId = source.Id });
        var ambiguous = new HistoricalTeamPlayers(Store(data).Object, team.Id);
        Assert.Contains("Multiple", ambiguous.UnavailableReason(source));
        Assert.Throws<InvalidOperationException>(() => ambiguous.Add(source.Id));
    }

    [Fact]
    public async Task FailedSave_RetainsSelectionAndSnapshotForRetry()
    {
        var old = new Season();
        var current = new Season();
        var team = new Team { SeasonId = current.Id };
        var source = new Player { SeasonId = old.Id, FirstName = "Alex" };
        var data = new LeagueData { Seasons = [old, current], Teams = [team], Players = [source] };
        var store = Store(data);
        store.SetupSequence(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Failed")).Returns(Task.CompletedTask);
        var draft = new HistoricalTeamPlayers(store.Object, team.Id);
        draft.Add(source.Id);
        await Assert.ThrowsAsync<IOException>(() => draft.SaveAsync());
        Assert.Single(draft.Selected);
        Assert.Single(draft.Preview.Players);
        Assert.Single(data.Players);
        await draft.SaveAsync();
        Assert.Single(draft.Preview.Players.Where(p => p.SeasonId == current.Id));
    }

    [Fact]
    public async Task DestinationRosterChangedAfterPreview_RejectsDuplicate()
    {
        var old = new Season();
        var current = new Season();
        var team = new Team { SeasonId = current.Id };
        var source = new Player { SeasonId = old.Id };
        var data = new LeagueData { Seasons = [old, current], Teams = [team], Players = [source] };
        var store = Store(data);
        var draft = new HistoricalTeamPlayers(store.Object, team.Id);
        draft.Add(source.Id);
        data.Players.Add(new Player { SeasonId = current.Id, GlobalPlayerId = source.Id, TeamId = team.Id });
        await Assert.ThrowsAsync<InvalidOperationException>(() => draft.SaveAsync());
        store.Verify(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LockedAfterPreview_RejectsAtomicSave()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = new LeagueContext(new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var old = new Season();
        var current = new Season();
        var team = new Team { SeasonId = current.Id };
        var source = new Player { SeasonId = old.Id, FirstName = "Alex" };
        context.AddRange(old, current, team, source);
        await context.SaveChangesAsync();
        var store = new SqliteDataStore(context);
        await store.SaveAsync();
        var draft = new HistoricalTeamPlayers(store, team.Id);
        draft.Add(source.Id);
        current.IsLocked = true;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => draft.SaveAsync());
        Assert.Empty(await context.Players.Where(p => p.SeasonId == current.Id).ToListAsync());
        Assert.Single(draft.Selected);
    }
}
