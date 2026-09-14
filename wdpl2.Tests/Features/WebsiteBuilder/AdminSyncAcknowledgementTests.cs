using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services.Inbox;

namespace wdpl2.Tests;

public class AdminSyncAcknowledgementTests
{
    [Theory]
    [InlineData("lost-response")]
    [InlineData("local-change")]
    [InlineData("locked")]
    public async Task Acknowledge_PreservesRequestAndRechecksLocalApplication(string scenario)
    {
        var directory = Path.Combine(Path.GetTempPath(), "wdpl-ack-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            using var context = new LeagueContext(new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            var season = new Season { Name = "Test" };
            var home = new Team { SeasonId = season.Id, Name = "Home" };
            var away = new Team { SeasonId = season.Id, Name = "Away" };
            var fixture = new Fixture { SeasonId = season.Id, HomeTeamId = home.Id, AwayTeamId = away.Id };
            context.AddRange(season, home, away, fixture);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            var backend = Guid.NewGuid().ToString();
            var change = new AdminSyncChange(1, "scorecard", fixture.Id.ToString(), 1, season.Id.ToString(), "desktop",
                JsonSerializer.SerializeToElement(new { version = 5, state = new { home_team_id = home.Id, away_team_id = away.Id, frames = Array.Empty<object>() } }));
            var queue = new AdminSyncReviewStore(Path.Combine(directory, "review.json"));
            var snapshot = JsonSerializer.SerializeToElement(await context.Fixtures.AsNoTracking().SingleAsync());
            await queue.StageAsync(new Uri("https://example.test/api/"), new AdminSyncBatch(backend, 0, 1, [change]), _ => snapshot);
            var requestId = (await queue.LoadAsync()).Pending.Single().ResolutionRequestId;
            string? firstBody = null;
            var requests = 0;
            using var client = new AdminSyncService(new WebInboxSettings { BaseUrl = "https://example.test/api/", AdminUser = "admin", AdminPassword = "test" },
                new Handler(async request =>
                {
                    var body = await request.Content!.ReadAsStringAsync();
                    firstBody ??= body;
                    Assert.Equal(firstBody, body);
                    requests++;
                    if (scenario == "lost-response" && requests == 1) throw new HttpRequestException("Response lost");
                    if (scenario == "local-change")
                        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Fixtures SET HomeLatePenalty = 4 WHERE Id = {fixture.Id}");
                    if (scenario == "locked")
                        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE Seasons SET IsLocked = 1 WHERE Id = {season.Id}");
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(new
                        {
                            protocol = 1, backendId = backend, accepted = true, id = fixture.Id.ToString(),
                            requestId, revision = 1, sequence = 1
                        }), Encoding.UTF8, "application/json")
                    };
                }));
            var coordinator = new AdminSyncCoordinator(queue, client, new AdminScorecardPersistence(context), () => { });
            if (scenario == "lost-response")
                await Assert.ThrowsAsync<HttpRequestException>(() => coordinator.AcceptServerScorecardAsync(1));
            else
                await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.AcceptServerScorecardAsync(1));
            var pending = await queue.LoadAsync();
            Assert.Equal(0, pending.AppliedThrough);
            Assert.Equal(requestId, pending.Pending.Single().ResolutionRequestId);
            Assert.Equal("server", pending.Pending.Single().ResolutionMode);
            if (scenario == "lost-response")
            {
                await coordinator.AcceptServerScorecardAsync(1);
                var completed = await queue.LoadAsync();
                Assert.Equal(1, completed.AppliedThrough);
                Assert.Empty(completed.Pending);
                Assert.Equal(2, requests);
            }
            else if (scenario == "local-change")
                Assert.Equal(4, (await context.Fixtures.AsNoTracking().SingleAsync()).HomeLatePenalty);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
