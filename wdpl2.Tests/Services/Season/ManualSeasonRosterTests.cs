using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Tests;

[CollectionDefinition("Manual season persistence", DisableParallelization = true)]
public class ManualSeasonPersistenceCollection { }

[Collection("Manual season persistence")]
public class ManualSeasonRosterTests
{
    [Fact]
    public void HistoricalVenues_CopyDetailsAndTablesWithoutSharingIdsOrObjects()
    {
        var source = new Venue
        {
            Name = "Pub", Address = "High Street", Notes = "Upstairs",
            SeasonId = Guid.NewGuid(), ModifiedDate = DateTime.UtcNow,
            Tables = [new VenueTable { Label = "Main", MaxTeams = 4 }]
        };
        var before = ImportWorkspace.Clone(source);
        var draft = new ManualSeasonRoster();
        var copied = draft.AddHistoricalVenue(source);
        Assert.NotEqual(source.Id, copied.Id);
        Assert.Null(copied.SeasonId);
        Assert.Null(copied.ModifiedDate);
        Assert.Equal(source.Name, copied.Name);
        Assert.Equal(source.Address, copied.Address);
        Assert.Equal(source.Notes, copied.Notes);
        var table = Assert.Single(copied.Tables);
        Assert.NotEqual(source.Tables[0].Id, table.Id);
        Assert.Equal("Main", table.Label);
        Assert.Equal(4, table.MaxTeams);
        copied.Address = "Changed";
        table.Label = "Changed";
        copied.Tables.Clear();
        Assert.True(ImportWorkspace.Equal(before, source));
    }

    [Fact]
    public void VenueSelections_DeduplicateOnlySourceRecord_AndCanBeRemovedAndReadded()
    {
        var source = new Venue { Name = "Pub", SeasonId = Guid.NewGuid() };
        var otherSeason = new Venue { Name = "Pub", SeasonId = Guid.NewGuid() };
        var draft = new ManualSeasonRoster();
        var first = draft.AddHistoricalVenue(source);
        Assert.Same(first, draft.AddHistoricalVenue(ImportWorkspace.Clone(source)));
        Assert.Same(first, draft.FindVenue(source));
        var second = draft.AddHistoricalVenue(otherSeason);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, draft.Venues.Count);
        draft.RemoveVenue(first.Id);
        Assert.Null(draft.FindVenue(source));
        Assert.Same(second, Assert.Single(draft.Venues));
        Assert.NotEqual(first.Id, draft.AddHistoricalVenue(source).Id);
        Assert.Equal(2, draft.Venues.Count);
    }

    [Fact]
    public void HistoricalCopies_HaveNewIdsAndNoSeasonSpecificLinks()
    {
        var source = new Team { Name = "Pub", SeasonId = Guid.NewGuid(), DivisionId = Guid.NewGuid(), VenueId = Guid.NewGuid(), CaptainPlayerId = Guid.NewGuid(), CaptainPin = "1234" };
        var player = new Player { Name = "J. Smith", TeamId = source.Id, SeasonId = source.SeasonId, IsActive = false, DeactivationReason = "Old" };
        var draft = new ManualSeasonRoster();
        var copiedTeam = draft.AddHistoricalTeam(source);
        var copiedPlayer = draft.AssignPlayer(player, copiedTeam.Id);
        Assert.NotEqual(source.Id, copiedTeam.Id);
        Assert.Equal(source.Id, copiedTeam.GlobalTeamId);
        Assert.Null(copiedTeam.SeasonId);
        Assert.Null(copiedTeam.DivisionId);
        Assert.Null(copiedTeam.VenueId);
        Assert.Null(copiedTeam.CaptainPlayerId);
        Assert.Null(copiedTeam.CaptainPin);
        Assert.NotEqual(player.Id, copiedPlayer.Id);
        Assert.Equal(player.Id, copiedPlayer.GlobalPlayerId);
        Assert.Equal("J. Smith", copiedPlayer.Name);
        Assert.True(copiedPlayer.IsActive);
        Assert.Null(copiedPlayer.DeactivationReason);
        Assert.Empty(copiedPlayer.TransferHistory);
        Assert.Empty(copiedPlayer.Availability);
        Assert.Equal(source.Id, player.TeamId);
        Assert.False(player.IsActive);
    }

    [Fact]
    public void AssigningAgain_MovesDraftWithoutDuplicatingPlayer()
    {
        var draft = new ManualSeasonRoster();
        var first = draft.AddTeam("First");
        var second = draft.AddTeam("Second");
        var source = new Player { FirstName = "John", LastName = "Smith" };
        var player = draft.AssignPlayer(source, first.Id);
        Assert.Same(player, draft.AssignPlayer(source, second.Id));
        Assert.Single(draft.Players);
        Assert.Equal(second.Id, player.TeamId);
        draft.MovePlayer(player.Id, first.Id);
        Assert.Throws<InvalidOperationException>(() => draft.RemoveTeam(first.Id));
        draft.RemovePlayer(player.Id);
        draft.RemoveTeam(first.Id);
        Assert.Single(draft.Teams);
    }

    [Fact]
    public void KnownGlobalIdentity_IsReusedAcrossSourceSeasons_NotNames()
    {
        var identity = Guid.NewGuid();
        var draft = new ManualSeasonRoster();
        var first = draft.AddHistoricalTeam(new Team { Name = "Pub", GlobalTeamId = identity });
        Assert.Same(first, draft.AddHistoricalTeam(new Team { Name = "Renamed", GlobalTeamId = identity }));
        draft.AddHistoricalTeam(new Team { Name = "Pub" });
        Assert.Equal(2, draft.Teams.Count);
        var playerId = Guid.NewGuid();
        var one = draft.AssignPlayer(new Player { Name = "Alex Smith", GlobalPlayerId = playerId }, first.Id);
        Assert.Same(one, draft.AssignPlayer(new Player { Name = "Alex Smith", GlobalPlayerId = playerId }, first.Id));
        draft.AssignPlayer(new Player { Name = "Alex Smith" }, first.Id);
        Assert.Equal(2, draft.Players.Count);
    }

    [Fact]
    public void SourceRoster_FiltersExactSeasonAndHistoricalTeam()
    {
        var season = Guid.NewGuid();
        var team = Guid.NewGuid();
        var wanted = new Player { SeasonId = season, TeamId = team };
        var unassigned = new Player { SeasonId = season };
        var data = new LeagueData { Players = [wanted, unassigned, new Player { SeasonId = Guid.NewGuid(), TeamId = team }, new Player { SeasonId = season, TeamId = Guid.NewGuid() }] };
        Assert.Same(wanted, Assert.Single(ManualSeasonRoster.SourceRoster(data, season, team)));
        Assert.Same(unassigned, Assert.Single(ManualSeasonRoster.SourceRoster(data, season, null)));
    }

    [Fact]
    public void InvalidDestination_IsRejectedWithoutAddingPlayer()
    {
        var draft = new ManualSeasonRoster();
        Assert.Throws<InvalidOperationException>(() => draft.AssignPlayer(new Player(), Guid.NewGuid()));
        Assert.Empty(draft.Players);
    }

    [Fact]
    public async Task Save_StagesOneCommit_WithoutMutatingSourceOrActivating()
    {
        var oldSeason = new Season { Name = "Winter 2000", IsLocked = true };
        var venue = new Venue { Name = "Pub", SeasonId = oldSeason.Id, Tables = [new VenueTable { Label = "Main" }] };
        var team = new Team { Name = "Pub", SeasonId = oldSeason.Id, VenueId = venue.Id };
        var player = new Player { Name = "Alex Smith", SeasonId = oldSeason.Id, TeamId = team.Id };
        var baseline = new LeagueData { Seasons = [oldSeason], Venues = [venue], Teams = [team], Players = [player] };
        var before = ImportWorkspace.Clone(baseline);
        LeagueData? saved = null;
        var store = new Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(baseline);
        store.Setup(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()))
            .Callback<LeagueData, LeagueData, CancellationToken>((_, after, _) => saved = after).Returns(Task.CompletedTask);
        var draft = new ManualSeasonRoster();
        var copiedVenue = draft.AddHistoricalVenue(venue);
        var copied = draft.AddHistoricalTeam(team);
        draft.AssignPlayer(player, copied.Id);
        var created = new Season { Name = "Summer 2000" };
        await draft.SaveAsync(store.Object, created);
        Assert.True(ImportWorkspace.Equal(before, baseline));
        Assert.NotNull(saved);
        Assert.False(saved.Seasons.Single(s => s.Id == created.Id).IsActive);
        Assert.Equal(created.Id, saved.Venues.Single(v => v.Id == copiedVenue.Id).SeasonId);
        Assert.True(ImportWorkspace.Equal(venue, saved.Venues.Single(v => v.Id == venue.Id)));
        Assert.Null(saved.Teams.Single(t => t.Id == copied.Id).VenueId);
        Assert.Equal(created.Id, saved.Teams.Single(t => t.Id == copied.Id).SeasonId);
        Assert.Equal(copied.Id, saved.Players.Single(p => p.SeasonId == created.Id).TeamId);
        Assert.Empty(ImportPlacementValidator.Validate(saved));
        store.Verify(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmptyManualSeason_CanBeSaved_AndInvalidDatesNeverCommit()
    {
        var store = new Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(new LeagueData());
        store.Setup(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var draft = new ManualSeasonRoster();
        await draft.SaveAsync(store.Object, new Season { Name = "Empty" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => draft.SaveAsync(store.Object,
            new Season { Name = "Bad dates", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(-1) }));
        store.Verify(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SqliteSave_PersistsWholeRoster()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = new LeagueContext(new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var store = new SqliteDataStore(context);
        var draft = new ManualSeasonRoster();
        var venue = draft.AddHistoricalVenue(new Venue { Name = "Pub", Address = "High Street", Notes = "Upstairs", Tables = [new VenueTable { Label = "Main", MaxTeams = 3 }] });
        var removed = draft.AddHistoricalVenue(new Venue { Name = "Removed" });
        draft.RemoveVenue(removed.Id);
        var team = draft.AddTeam("New team");
        draft.AssignPlayer(new Player { FirstName = "John", LastName = "Smith" }, team.Id);
        var season = new Season { Name = "Summer" };
        await draft.SaveAsync(store, season);
        Assert.False((await context.Seasons.AsNoTracking().SingleAsync()).IsActive);
        Assert.Equal(season.Id, (await context.Teams.AsNoTracking().SingleAsync()).SeasonId);
        Assert.Equal(team.Id, (await context.Players.AsNoTracking().SingleAsync()).TeamId);
        var savedVenue = await context.Venues.AsNoTracking().SingleAsync();
        Assert.Equal(venue.Id, savedVenue.Id);
        Assert.Equal(season.Id, savedVenue.SeasonId);
        Assert.Equal("High Street", savedVenue.Address);
        Assert.Equal("Upstairs", savedVenue.Notes);
        var savedTable = Assert.Single(savedVenue.Tables);
        Assert.Equal(venue.Tables[0].Id, savedTable.Id);
        Assert.Equal("Main", savedTable.Label);
        Assert.Equal(3, savedTable.MaxTeams);
    }

    [Fact]
    public async Task FailedSave_LeavesDraftAndSourceIntactForRetry()
    {
        var original = new LeagueData();
        var store = new Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(original);
        store.Setup(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed"));
        var draft = new ManualSeasonRoster();
        draft.AddTeam("Team");
        var venue = draft.AddHistoricalVenue(new Venue { Name = "Pub", Tables = [new VenueTable { Label = "Main" }] });
        await Assert.ThrowsAsync<InvalidOperationException>(() => draft.SaveAsync(store.Object, new Season { Name = "New" }));
        Assert.Empty(original.Seasons);
        Assert.Empty(original.Teams);
        Assert.Empty(original.Venues);
        Assert.Same(venue, Assert.Single(draft.Venues));
        Assert.Null(venue.SeasonId);
        Assert.Single(draft.Teams);
    }
}
