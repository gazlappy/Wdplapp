using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

public class FixtureNumberPersistenceTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("locked")]
    [InlineData("played")]
    [InlineData("frames")]
    [InlineData("added")]
    [InlineData("stale")]
    [InlineData("placement")]
    [InlineData("canceled")]
    [InlineData("discard")]
    [InlineData("linked-valid")]
    [InlineData("linked-locked")]
    [InlineData("linked-played")]
    [InlineData("linked-frames")]
    [InlineData("linked-added")]
    [InlineData("linked-stale")]
    [InlineData("linked-placement")]
    [InlineData("linked-canceled")]
    [InlineData("linked-discard")]
    [InlineData("reverse-valid")]
    [InlineData("reverse-linked-valid")]
    [InlineData("reverse-linked-locked")]
    [InlineData("reverse-linked-played")]
    [InlineData("reverse-linked-frames")]
    [InlineData("reverse-linked-added")]
    [InlineData("reverse-linked-stale")]
    [InlineData("reverse-linked-placement")]
    [InlineData("reverse-linked-canceled")]
    [InlineData("reverse-linked-discard")]
    [InlineData("bye-valid")]
    [InlineData("bye-linked-valid")]
    [InlineData("bye-linked-locked")]
    [InlineData("bye-linked-played")]
    [InlineData("bye-linked-frames")]
    [InlineData("bye-linked-added")]
    [InlineData("bye-linked-stale")]
    [InlineData("bye-linked-placement")]
    [InlineData("bye-linked-canceled")]
    [InlineData("bye-linked-discard")]
    public async Task ReviewedSave_RechecksCurrentDatabaseAndPreservesRejectedSchedules(string scenario)
    {
        bool firstBye = scenario.StartsWith("bye-", StringComparison.Ordinal);
        if (firstBye) scenario = scenario[4..];
        bool reverse = scenario.StartsWith("reverse-", StringComparison.Ordinal);
        if (reverse) scenario = scenario[8..];
        int destination = reverse ? 2 : 3;
        bool linked = scenario.StartsWith("linked-", StringComparison.Ordinal);
        if (linked) scenario = scenario[7..];
        var (data, season, fixtures) = FixtureNumberEditorTests.Setup(linked, secondCount: firstBye ? 3 : 4);
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options;
        using var context = new LeagueContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Seasons.AddRange(data.Seasons);
        context.Divisions.AddRange(data.Divisions);
        context.Venues.AddRange(data.Venues);
        context.Teams.AddRange(data.Teams);
        context.Fixtures.AddRange(fixtures);
        await context.SaveChangesAsync();
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        Guid? byeTeamId = null;
        if (firstBye)
        {
            var division = data.Divisions[1].Id;
            var currentBye = editor.OpeningByeTeams(division).Single().Id;
            byeTeamId = data.Teams.First(t => t.DivisionId == division && t.Id != currentBye).Id;
            var proposal = editor.PrepareFirstBye(division, byeTeamId.Value);
            Assert.Equal(linked, proposal.IncludesOtherDivisions);
            if (scenario != "discard") editor.ApplySwap(proposal);
        }
        else if (linked)
        {
            var proposal = editor.PrepareSwap(data.Divisions[0].Id, 1, destination);
            Assert.True(proposal.IncludesOtherDivisions);
            if (scenario != "discard") editor.ApplySwap(proposal);
        }
        else editor.Swap(data.Divisions[0].Id, 1, destination);
        switch (scenario)
        {
            case "locked": data.Seasons[0].IsLocked = true; break;
            case "played": fixtures[0].HomeLatePenalty = 1; break;
            case "frames": fixtures[0].Frames.Add(new FrameResult { Number = 1 }); break;
            case "added": context.Fixtures.Add(new Fixture { SeasonId = season, HomeTeamId = fixtures[0].HomeTeamId, AwayTeamId = fixtures[0].AwayTeamId, Date = fixtures[0].Date }); break;
            case "stale": fixtures[0].Date = fixtures[0].Date.AddDays(1); break;
            case "placement": data.Teams[0].TableId = null; break;
        }
        await context.SaveChangesAsync();
        var baseline = await context.Fixtures.AsNoTracking().OrderBy(f => f.Id).ToListAsync();
        string before = JsonSerializer.Serialize(baseline);
        var store = new SqliteDataStore(context);
        if (scenario == "valid") await store.SaveFixtureNumbersAsync(editor);
        else if (scenario == "canceled")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveFixtureNumbersAsync(editor, new CancellationToken(true)));
        else if (scenario != "discard")
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveFixtureNumbersAsync(editor));
        using var reloaded = new LeagueContext(options);
        var after = await reloaded.Fixtures.AsNoTracking().OrderBy(f => f.Id).ToListAsync();
        if (scenario != "valid") Assert.Equal(before, JsonSerializer.Serialize(after));
        else
        {
            Assert.NotEqual(before, JsonSerializer.Serialize(after));
            Assert.Equal(baseline.Select(f => f.Id), after.Select(f => f.Id));
            if (firstBye)
                Assert.DoesNotContain(after, f => f.Date.Date == editor.OpeningDate && (f.HomeTeamId == byeTeamId || f.AwayTeamId == byeTeamId));
            var mapping = SharedFixtureSheetSchedule.Create(data.Divisions, data.Teams, after).TeamNumbers;
            Assert.Equal(editor.Numbers.OrderBy(p => p.Key), mapping.OrderBy(p => p.Key));
            if (linked)
            {
                foreach (var division in data.Divisions)
                    Assert.Contains(after.Where(f => f.DivisionId == division.Id), f => baseline.Single(b => b.Id == f.Id).Date != f.Date);
            }
            else
            {
                foreach (var fixture in baseline.Where(f => f.DivisionId == data.Divisions[firstBye ? 0 : 1].Id))
                    Assert.Equal(JsonSerializer.Serialize(fixture), JsonSerializer.Serialize(after.Single(f => f.Id == fixture.Id)));
            }
        }
    }

    [Fact]
    public async Task GeneratedDraft_CanSaveAfterReviewWithoutExistingFixtures()
    {
        var (data, season, fixtures) = FixtureNumberEditorTests.Setup();
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options;
        using var context = new LeagueContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Seasons.AddRange(data.Seasons);
        context.Divisions.AddRange(data.Divisions);
        context.Venues.AddRange(data.Venues);
        context.Teams.AddRange(data.Teams);
        await context.SaveChangesAsync();
        var editor = new FixtureNumberEditor(data, season, fixtures, Array.Empty<Fixture>());
        editor.Swap(data.Divisions[0].Id, 1, 3);
        await new SqliteDataStore(context).SaveFixtureNumbersAsync(editor);
        Assert.Equal(fixtures.Count, await context.Fixtures.CountAsync());
    }
}
