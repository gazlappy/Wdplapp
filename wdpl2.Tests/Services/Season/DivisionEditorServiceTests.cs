using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Tests;

public sealed class DivisionEditorServiceTests
{
    [Fact]
    public async Task InactiveSeason_CreateUpdateImportDelete_PersistWithoutActivation()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = new LeagueContext(new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var working = new Season { Name = "Winter 2025", IsActive = true };
        var target = new Season { Name = "Summer 2026", IsActive = false };
        var historical = new Division { SeasonId = working.Id, Name = "Red" };
        context.AddRange(working, target, historical);
        await context.SaveChangesAsync();
        var store = new SqliteDataStore(context);
        var editor = new DivisionEditorService(store);

        await editor.SaveAsync(target.Id, null, " Red ", " New ");
        context.ChangeTracker.Clear();
        var created = await context.Divisions.AsNoTracking().SingleAsync(d => d.SeasonId == target.Id);
        Assert.Equal("Red", created.Name);
        Assert.Equal("New", created.Notes);
        Assert.False((await context.Seasons.AsNoTracking().SingleAsync(s => s.Id == target.Id)).IsActive);
        Assert.True((await context.Seasons.AsNoTracking().SingleAsync(s => s.Id == working.Id)).IsActive);

        await editor.SaveAsync(target.Id, created.Id, "Green", "Updated");
        await editor.ImportAsync(target.Id, new[] { ("Green", (string?)"CSV"), ("Yellow", (string?)null) });
        Assert.Equal("CSV", (await context.Divisions.AsNoTracking().SingleAsync(d => d.Id == created.Id)).Notes);
        Assert.Equal(2, await context.Divisions.CountAsync(d => d.SeasonId == target.Id));
        await editor.DeleteAsync(target.Id, new[] { created.Id });
        Assert.False(await context.Divisions.AnyAsync(d => d.Id == created.Id));
        Assert.Equal("Red", (await context.Divisions.AsNoTracking().SingleAsync(d => d.Id == historical.Id)).Name);
        Assert.Single(store.GetData().Divisions.Where(d => d.SeasonId == target.Id));
    }

    [Fact]
    public async Task InvalidEdits_DoNotCommitOrMutateSnapshot()
    {
        var season = new Season { IsActive = false };
        var other = new Season();
        var division = new Division { SeasonId = season.Id, Name = "Red" };
        var data = new LeagueData { Seasons = [season, other], Divisions = [division] };
        var store = new Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(data);
        var editor = new DivisionEditorService(store.Object);
        var before = ImportWorkspace.Clone(data);

        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.SaveAsync(season.Id, null, " red ", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.SaveAsync(season.Id, null, " ", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.SaveAsync(season.Id, null, new string('x', 101), null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.SaveAsync(Guid.NewGuid(), null, "New", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.SaveAsync(other.Id, division.Id, "Wrong", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.DeleteAsync(other.Id, new[] { division.Id }));
        Assert.True(ImportWorkspace.Equal(before, data));
        store.Verify(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LockedSeason_RejectsAllMutationPaths()
    {
        var season = new Season { IsLocked = true };
        var division = new Division { SeasonId = season.Id, Name = "Red" };
        var store = new Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(new LeagueData { Seasons = [season], Divisions = [division] });
        var editor = new DivisionEditorService(store.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.SaveAsync(season.Id, null, "Green", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.SaveAsync(season.Id, division.Id, "Green", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.DeleteAsync(season.Id, new[] { division.Id }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.ImportAsync(season.Id, new[] { ("Green", (string?)null) }));
        store.Verify(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReferencedDivision_BlocksWholeBulkDelete(bool teamReference)
    {
        var season = new Season();
        var first = new Division { SeasonId = season.Id, Name = "Red" };
        var second = new Division { SeasonId = season.Id, Name = "Green" };
        var data = new LeagueData { Seasons = [season], Divisions = [first, second] };
        if (teamReference) data.Teams.Add(new Team { SeasonId = season.Id, DivisionId = second.Id });
        else data.Fixtures.Add(new Fixture { SeasonId = season.Id, DivisionId = second.Id });
        var store = new Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(data);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DivisionEditorService(store.Object).DeleteAsync(season.Id, new[] { first.Id, second.Id }));
        Assert.Equal(2, data.Divisions.Count);
        store.Verify(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailedCommit_LeavesOriginalDivisionUnchanged()
    {
        var season = new Season();
        var division = new Division { SeasonId = season.Id, Name = "Red" };
        var data = new LeagueData { Seasons = [season], Divisions = [division] };
        var store = new Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(data);
        store.Setup(s => s.CommitImportAsync(It.IsAny<LeagueData>(), It.IsAny<LeagueData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Save failed"));
        await Assert.ThrowsAsync<IOException>(() => new DivisionEditorService(store.Object).SaveAsync(season.Id, division.Id, "Green", "Changed"));
        Assert.Equal("Red", division.Name);
        Assert.Null(division.Notes);
    }
}
