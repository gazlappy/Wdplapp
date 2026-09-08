using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Wdpl2.Services.Inbox;

namespace wdpl2.Tests;

public class AdminSyncServiceTests
{
    private const string Backend = "00000000-0000-0000-0000-000000000001";
    [Theory]
    [InlineData("server", "web", "valid")]
    [InlineData("local", "web", "valid")]
    [InlineData("server", "desktop", "valid")]
    [InlineData("local", "desktop", "valid")]
    [InlineData("server", "web", "request")]
    [InlineData("server", "web", "backend")]
    [InlineData("server", "web", "revision")]
    [InlineData("server", "web", "sequence")]
    [InlineData("server", "web", "id")]
    [InlineData("server", "desktop", "sequence")]
    public async Task EntryReview_RequiresExactResolutionReceipt(string mode, string source, string fault)
    {
        var values = JsonSerializer.SerializeToElement(new { status = "confirmed", notes = "Reviewed" });
        var item = new AdminSyncReviewItem
        {
            Change = new(1, "entry_review", "7", 3, null, source, values),
            ResolutionMode = mode, ResolutionPayload = values
        };
        var echo = mode == "server" && source == "desktop";
        var revision = echo ? 3 : 4;
        using var service = new AdminSyncService(Settings(), new Handler(request =>
        {
            Assert.Equal("https://example.test/api/admin/sync-entry-review.php", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            Assert.Equal(mode, body.RootElement.GetProperty("mode").GetString());
            Assert.Equal("Reviewed", body.RootElement.GetProperty("notes").GetString());
            Assert.Equal(item.ResolutionRequestId, body.RootElement.GetProperty("request_id").GetGuid());
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    protocol = 1, accepted = true, backendId = fault == "backend" ? Guid.NewGuid().ToString() : Backend,
                    id = fault == "id" ? "8" : "7", requestId = fault == "request" ? Guid.NewGuid() : item.ResolutionRequestId,
                    revision = fault == "revision" ? revision + 1 : revision,
                    sequence = fault == "sequence" ? (echo ? 2 : 1) : (echo ? 1 : 2)
                }), Encoding.UTF8, "application/json")
            };
        }));
        if (fault == "valid") Assert.Equal(revision, await service.ResolveEntryReviewAsync(Backend, item));
        else await Assert.ThrowsAsync<JsonException>(() => service.ResolveEntryReviewAsync(Backend, item));
    }

    [Fact]
    public async Task EntryReview_RejectsUnpreparedDecisionBeforeSending()
    {
        using var service = new AdminSyncService(Settings(), new Handler(_ => throw new InvalidOperationException("Must not send")));
        var item = new AdminSyncReviewItem
        {
            Change = new(1, "entry_review", "1", 1, null, "web", JsonSerializer.SerializeToElement(new { status = "pending", notes = "" }))
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveEntryReviewAsync(Backend, item));
    }

    private static WebInboxSettings Settings() => new() { BaseUrl = "https://example.test/api/", AdminUser = "admin", AdminPassword = "private" };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(send(request));
    }
    private static HttpResponseMessage Page(long sequence, long through, long? next, string backend = Backend) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            protocol = 1, backendId = backend, through, nextAfter = next,
            items = new[] { new { sequence, kind = "scorecard", id = "fixture-1", revision = sequence, seasonId = (string?)null, source = "web", payload = new { notes = "review" } } }
        }), Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task Fetch_PreservesBackendAndSnapshotAcrossPages()
    {
        int calls = 0;
        using var service = new AdminSyncService(Settings(), new Handler(request =>
        {
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            if (++calls == 1) { Assert.Equal("?after=0", request.RequestUri!.Query); return Page(1, 2, 1); }
            Assert.Contains("backendId=" + Backend, request.RequestUri!.Query);
            Assert.Contains("through=2", request.RequestUri.Query);
            return Page(2, 2, null);
        }));
        var result = await service.FetchAsync(null, 0);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(0, result.After);
        Assert.Equal(2, result.Through);
        Assert.Equal("review", result.Items[0].Payload.GetProperty("notes").GetString());
    }

    [Theory]
    [InlineData(2, 2, null)]
    [InlineData(1, 2, null)]
    [InlineData(1L, 2L, 0L)]
    public async Task Fetch_RejectsGapsEarlyTerminationAndInvalidCursors(long sequence, long through, long? next)
    {
        using var service = new AdminSyncService(Settings(), new Handler(_ => Page(sequence, through, next)));
        await Assert.ThrowsAsync<JsonException>(() => service.FetchAsync(null, 0));
    }

    [Fact]
    public async Task Fetch_RejectsBackendChange()
    {
        using var service = new AdminSyncService(Settings(), new Handler(_ => Page(1, 1, null, Guid.NewGuid().ToString())));
        await Assert.ThrowsAsync<JsonException>(() => service.FetchAsync(Backend, 0));
    }

    [Fact]
    public async Task Fetch_FailedSecondPageReturnsNoPartialBatch()
    {
        int calls = 0;
        using var service = new AdminSyncService(Settings(), new Handler(_ => ++calls == 1 ? Page(1, 2, 1) : new(HttpStatusCode.InternalServerError)));
        await Assert.ThrowsAsync<HttpRequestException>(() => service.FetchAsync(null, 0));
    }

    [Fact]
    public void Constructor_RejectsCertificateBypass()
    {
        var settings = Settings();
        settings.IgnoreSslErrors = true;
        Assert.Throws<InvalidOperationException>(() => new AdminSyncService(settings));
    }

    [Fact]
    public async Task KeepLocal_SendsFrozenFramesAndRequiresNewRevision()
    {
        var item = new AdminSyncReviewItem
        {
            Change = new(1, "scorecard", "fixture-1", 1, Guid.NewGuid().ToString(), "desktop", JsonSerializer.SerializeToElement(new { version = 5 })),
            ResolutionMode = "local",
            ResolutionPayload = JsonSerializer.SerializeToElement(Array.Empty<object>())
        };
        using var service = new AdminSyncService(Settings(), new Handler(request =>
        {
            Assert.Equal("https://example.test/api/admin/sync-keep-local.php", request.RequestUri!.AbsoluteUri);
            using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            Assert.Equal(item.ResolutionRequestId, body.RootElement.GetProperty("request_id").GetGuid());
            Assert.Equal(5, body.RootElement.GetProperty("expected_version").GetInt64());
            Assert.Empty(body.RootElement.GetProperty("frames").EnumerateArray());
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    protocol = 1, backendId = Backend, accepted = true, id = "fixture-1",
                    requestId = item.ResolutionRequestId, revision = 2, sequence = 2
                }), Encoding.UTF8, "application/json")
            };
        }));
        Assert.Equal(2, await service.KeepLocalScorecardAsync(Backend, item));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Accept_RequiresMatchingRequestReceipt(bool matching)
    {
        var item = new AdminSyncReviewItem
        {
            Change = new(1, "scorecard", "fixture-1", 1, null, "web", JsonSerializer.SerializeToElement(new { version = 5 }))
        };
        using var service = new AdminSyncService(Settings(), new Handler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://example.test/api/admin/sync-accept.php", request.RequestUri!.AbsoluteUri);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    protocol = 1, backendId = Backend, accepted = true, id = "fixture-1",
                    requestId = matching ? item.ResolutionRequestId : Guid.NewGuid(), revision = 2, sequence = 2
                }), Encoding.UTF8, "application/json")
            };
        }));
        if (matching) Assert.Equal(2, await service.AcceptAppliedScorecardAsync(Backend, item));
        else await Assert.ThrowsAsync<JsonException>(() => service.AcceptAppliedScorecardAsync(Backend, item));
    }
}
