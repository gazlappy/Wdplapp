using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services.Inbox;

namespace wdpl2.Tests;

public class AdminScorecardPersistenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Apply_ChecksCurrentDatabaseBeforeCommitting(bool locked, bool changed)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = new LeagueContext(new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var season = new Season { Name = "Summer", IsLocked = locked };
        var home = new Team { Name = "Home", SeasonId = season.Id };
        var away = new Team { Name = "Away", SeasonId = season.Id };
        var fixture = new Fixture { SeasonId = season.Id, HomeTeamId = home.Id, AwayTeamId = away.Id, HomeLatePenalty = 2 };
        context.AddRange(season, home, away, fixture);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var saved = await context.Fixtures.AsNoTracking().SingleAsync();
        var item = new AdminSyncReviewItem
        {
            LocalSnapshot = JsonSerializer.SerializeToElement(saved),
            Change = new(1, "scorecard", fixture.Id.ToString(), 1, season.Id.ToString(), "web",
                JsonSerializer.SerializeToElement(new { state = new { home_team_id = home.Id, away_team_id = away.Id, frames = Array.Empty<object>() } }))
        };
        if (changed) await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Fixtures SET HomeLatePenalty = 3 WHERE Id = {fixture.Id}");
        var service = new AdminScorecardPersistence(context);
        if (locked || changed) await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyServerAsync(item));
        else
        {
            var first = await service.ApplyServerAsync(item);
            var retry = await service.ApplyServerAsync(item);
            Assert.Equal(first.ModifiedDate, retry.ModifiedDate);
        }
        context.ChangeTracker.Clear();
        var result = await context.Fixtures.AsNoTracking().SingleAsync();
        Assert.Equal(changed ? 3 : 2, result.HomeLatePenalty);
        Assert.Equal(fixture.Id, result.Id);
        Assert.Equal(locked, (await context.Seasons.AsNoTracking().SingleAsync()).IsLocked);
    }
}
