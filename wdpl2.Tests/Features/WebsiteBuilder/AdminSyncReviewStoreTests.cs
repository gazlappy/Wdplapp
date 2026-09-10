using System.Text.Json;
using Wdpl2.Services.Inbox;

namespace wdpl2.Tests;

public sealed class AdminSyncReviewStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wdpl-sync-" + Guid.NewGuid().ToString("N"));
    private static readonly Uri BaseUri = new("https://example.test/api/");
    private readonly string _backend = Guid.NewGuid().ToString();
    private AdminSyncReviewStore Store() => new(Path.Combine(_directory, "review.json"));
    private AdminSyncBatch Batch() => new(_backend, 0, 1,
        [new(1, "scorecard", "fixture-1", 1, null, "web", JsonSerializer.SerializeToElement(new { notes = "server" }))]);

    [Fact]
    public async Task Stage_PersistsBothVersionsWithoutAdvancingAppliedCursor()
    {
        await Store().StageAsync(BaseUri, Batch(), _ => JsonSerializer.SerializeToElement(new { notes = "local" }));
        var state = await Store().LoadAsync();
        Assert.Equal(0, state.AppliedThrough);
        Assert.Equal(1, state.DownloadedThrough);
        var item = Assert.Single(state.Pending);
        Assert.Equal("local", item.LocalSnapshot!.Value.GetProperty("notes").GetString());
        Assert.Equal("server", item.Change.Payload.GetProperty("notes").GetString());
        Assert.Equal(item.ResolutionRequestId, Assert.Single((await Store().LoadAsync()).Pending).ResolutionRequestId);
    }

    [Fact]
    public async Task Stage_RejectsStaleBatchWithoutDuplicatingQueue()
    {
        await Store().StageAsync(BaseUri, Batch(), _ => null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().StageAsync(BaseUri, Batch(), _ => null));
        Assert.Single((await Store().LoadAsync()).Pending);
    }

    [Fact]
    public async Task Stage_UnmappedEntryRemainsPendingAcrossReloads()
    {
        var batch = new AdminSyncBatch(_backend, 0, 1,
            [new(1, "entry_review", "7", 1, null, "web", JsonSerializer.SerializeToElement(new { status = "confirmed", notes = "Reviewed" }))]);
        await Store().StageAsync(BaseUri, batch, _ => null);
        var state = await Store().LoadAsync();
        Assert.Null(Assert.Single(state.Pending).LocalSnapshot);
        Assert.Equal(0, state.AppliedThrough);
        Assert.Equal(1, state.DownloadedThrough);
        await Store().StageAsync(BaseUri, new(_backend, 1, 1, []), _ => throw new InvalidOperationException());
        var reloaded = await Store().LoadAsync();
        Assert.Equal(state.Pending[0].ResolutionRequestId, Assert.Single(reloaded.Pending).ResolutionRequestId);
        Assert.Equal(0, reloaded.AppliedThrough);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Stage_BackendSwitchCannotReplacePendingReview(bool changeUrl)
    {
        await Store().StageAsync(BaseUri, Batch(), _ => JsonSerializer.SerializeToElement(new { notes = "local" }));
        var before = await Store().LoadAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().StageAsync(
            changeUrl ? new Uri("https://other.test/api/") : BaseUri,
            new(changeUrl ? _backend : Guid.NewGuid().ToString(), 1, 1, []), _ => null));
        var after = await Store().LoadAsync();
        Assert.Equal(before.BackendId, after.BackendId);
        Assert.Equal(before.ApiBaseUri, after.ApiBaseUri);
        Assert.Equal(before.Pending[0].ResolutionRequestId, Assert.Single(after.Pending).ResolutionRequestId);
        Assert.Equal(0, after.AppliedThrough);
    }

    [Fact]
    public async Task Stage_FailedLocalSnapshotDoesNotWritePartialState()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().StageAsync(BaseUri, Batch(), _ => throw new InvalidOperationException("capture failed")));
        Assert.Empty((await Store().LoadAsync()).Pending);
    }

    [Fact]
    public async Task Complete_RequiresMatchingRequestAndPersistsRevision()
    {
        await Store().StageAsync(BaseUri, Batch(), _ => null);
        var item = Assert.Single((await Store().LoadAsync()).Pending);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().CompleteAsync(1, Guid.NewGuid(), 2));
        await Store().CompleteAsync(1, item.ResolutionRequestId, 2);
        var state = await Store().LoadAsync();
        Assert.Equal(1, state.AppliedThrough);
        Assert.Equal(2, state.ReviewedRevisions["scorecard:fixture-1"]);
        Assert.Empty(state.Pending);
    }

    [Fact]
    public async Task Resolution_ReloadsOriginalIntentAndRejectsChangedRetries()
    {
        await Store().StageAsync(BaseUri, Batch(), _ => null);
        var payload = JsonSerializer.SerializeToElement(new[] { new { number = 1 } });
        var first = await Store().PrepareResolutionAsync(1, "local", payload);
        var retry = await Store().PrepareResolutionAsync(1, "local", payload);
        Assert.Equal(first.ResolutionRequestId, retry.ResolutionRequestId);
        Assert.Equal("local", Assert.Single((await Store().LoadAsync()).Pending).ResolutionMode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().PrepareResolutionAsync(1, "server", payload));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().PrepareResolutionAsync(1, "local", JsonSerializer.SerializeToElement(Array.Empty<object>())));
        Assert.Equal(0, (await Store().LoadAsync()).AppliedThrough);
    }

    [Fact]
    public async Task RefreshLocal_PreservesCursorsAndRejectsOldPreviewAndStartedDecision()
    {
        var entry = new Wdpl2.Models.EntryFormSubmission { SourceBackendId = _backend, SourceClientId = "client-1", SourceSubmissionSequence = 7 };
        var payload = JsonSerializer.SerializeToElement(new { clientId = "client-1", submissionSequence = 7 });
        await Store().StageAsync(BaseUri, new(_backend, 0, 1, [new(1, "entry_review", "7", 1, null, "web", payload)]), _ => JsonSerializer.SerializeToElement(entry));
        var expected = (await Store().LoadAsync()).Pending[0];
        entry.Notes = "Updated locally";
        var snapshot = JsonSerializer.SerializeToElement(entry);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().RefreshLocalSnapshotAsync(Guid.NewGuid().ToString(), expected, snapshot));
        await Store().RefreshLocalSnapshotAsync(_backend, expected, snapshot);
        var state = await Store().LoadAsync();
        var current = Assert.Single(state.Pending);
        Assert.Equal(0, state.AppliedThrough);
        Assert.Equal(1, state.DownloadedThrough);
        Assert.Empty(state.ReviewedRevisions);
        Assert.Equal(expected.Change.Revision, current.Change.Revision);
        Assert.True(JsonElement.DeepEquals(expected.Change.Payload, current.Change.Payload));
        Assert.NotEqual(expected.ResolutionRequestId, current.ResolutionRequestId);
        Assert.Equal("Updated locally", current.LocalSnapshot!.Value.GetProperty("Notes").GetString());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().RefreshLocalSnapshotAsync(_backend, expected, snapshot));
        await Store().PrepareResolutionAsync(1, "local", payload);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().RefreshLocalSnapshotAsync(_backend, current, snapshot));
        Assert.Equal("local", (await Store().LoadAsync()).Pending[0].ResolutionMode);
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
    [Fact]
    public async Task Link_AttachesSnapshotWithoutAcknowledgmentAndRejectsReplacement()
    {
        var payload = JsonSerializer.SerializeToElement(new { submissionSequence = 7, clientId = "client-1" });
        await Store().StageAsync(BaseUri, new(_backend, 0, 1, [new(1, "entry_review", "7", 1, null, "web", payload)]), _ => null);
        var request = (await Store().LoadAsync()).Pending[0].ResolutionRequestId;
        var entry = new Wdpl2.Models.EntryFormSubmission { SourceBackendId = _backend, SourceClientId = "client-1", SourceSubmissionSequence = 7 };
        var snapshot = JsonSerializer.SerializeToElement(entry);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().AttachLinkedEntryAsync(_backend, Guid.NewGuid(), snapshot));
        await Store().AttachLinkedEntryAsync(_backend, request, snapshot);
        await Store().AttachLinkedEntryAsync(_backend, request, snapshot);
        var state = await Store().LoadAsync();
        Assert.Equal(0, state.AppliedThrough);
        Assert.Equal(1, state.DownloadedThrough);
        Assert.Equal(request, state.Pending[0].ResolutionRequestId);
        Assert.Null(state.Pending[0].ResolutionMode);
        entry.Notes = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().AttachLinkedEntryAsync(_backend, request, JsonSerializer.SerializeToElement(entry)));
        await Store().PrepareResolutionAsync(1, "server", payload);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().AttachLinkedEntryAsync(_backend, request, snapshot));
    }
}
