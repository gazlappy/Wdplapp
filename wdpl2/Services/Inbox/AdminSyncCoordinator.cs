using System.Text.Json;
using Microsoft.Maui.ApplicationModel;

namespace Wdpl2.Services.Inbox;

/// <summary>Coordinates explicit scorecard conflict choices and durable retry intent.</summary>
public sealed class AdminSyncCoordinator(AdminSyncReviewStore queue, AdminSyncService client,
    AdminScorecardPersistence persistence, Action refreshCaches)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task ResolveEntryReviewAsync(long sequence, bool keepLocal, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var state = await queue.LoadAsync(ct);
            if (state.ApiBaseUri != client.BaseUri.AbsoluteUri)
                throw new InvalidOperationException("The saved Web Inbox URL does not match this review queue.");
            var item = state.Pending.FirstOrDefault();
            if (item == null || item.Change.Sequence != sequence || item.Change.Kind != "entry_review" || item.LocalSnapshot is not { } snapshot)
                throw new InvalidOperationException("Review and explicitly map the first pending entry before applying it.");
            var before = JsonSerializer.Deserialize<Wdpl2.Models.EntryFormSubmission>(snapshot.GetRawText())!;
            var values = keepLocal ? JsonSerializer.SerializeToElement(new { status = before.Status, notes = before.Notes }) :
                AdminEntryReviewMapper.ReviewValues(item.Change.Payload);
            item = await queue.PrepareResolutionAsync(sequence, keepLocal ? "local" : "server", values, ct);
            ct.ThrowIfCancellationRequested();
            await MainThread.InvokeOnMainThreadAsync(() => DataStore.ApplyEntryReview(state.BackendId, item));
            var revision = await client.ResolveEntryReviewAsync(state.BackendId, item, ct);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AdminEntryReviewMapper.PrepareApplication(DataStore.Data, state.BackendId, item);
                refreshCaches();
            });
            await queue.CompleteAsync(sequence, item.ResolutionRequestId, revision, ct);
        }
        finally { Gate.Release(); }
    }

    public async Task AcceptServerScorecardAsync(long sequence, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var state = await queue.LoadAsync(ct);
            if (state.ApiBaseUri != client.BaseUri.AbsoluteUri)
                throw new InvalidOperationException("The saved Web Inbox URL does not match this review queue.");
            var item = state.Pending.FirstOrDefault();
            if (item == null || item.Change.Sequence != sequence || item.Change.Kind != "scorecard")
                throw new InvalidOperationException("Review the first pending scorecard before applying it.");
            item = await queue.PrepareResolutionAsync(sequence, "server", item.Change.Payload, ct);
            await persistence.ApplyServerAsync(item, ct);
            refreshCaches();
            var revision = await client.AcceptAppliedScorecardAsync(state.BackendId, item, ct);
            // The persisted application receipt verifies unchanged local data on retry.
            await persistence.ApplyServerAsync(item, ct);
            await queue.CompleteAsync(item.Change.Sequence, item.ResolutionRequestId, revision, ct);
        }
        finally { Gate.Release(); }
    }

    public async Task KeepLocalScorecardAsync(long sequence, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var state = await queue.LoadAsync(ct);
            if (state.ApiBaseUri != client.BaseUri.AbsoluteUri)
                throw new InvalidOperationException("The saved Web Inbox URL does not match this review queue.");
            var item = state.Pending.FirstOrDefault();
            if (item == null || item.Change.Sequence != sequence || item.Change.Kind != "scorecard")
                throw new InvalidOperationException("Review the first pending scorecard before applying it.");
            var frames = await persistence.CaptureLocalFramesAsync(item, ct);
            item = await queue.PrepareResolutionAsync(sequence, "local", frames, ct);
            var revision = await client.KeepLocalScorecardAsync(state.BackendId, item, ct);
            // Do not checkpoint a receipt against a local fixture changed during the request.
            await persistence.CaptureLocalFramesAsync(item, ct);
            await queue.CompleteAsync(sequence, item.ResolutionRequestId, revision, ct);
        }
        finally { Gate.Release(); }
    }
}
