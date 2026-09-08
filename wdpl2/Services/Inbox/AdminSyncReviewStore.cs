using System.Text.Json;

namespace Wdpl2.Services.Inbox;

public sealed class AdminSyncReviewState
{
    public string ApiBaseUri { get; set; } = "";
    public string BackendId { get; set; } = "";
    public long AppliedThrough { get; set; }
    public long DownloadedThrough { get; set; }
    public List<AdminSyncReviewItem> Pending { get; set; } = [];
    public Dictionary<string, long> ReviewedRevisions { get; set; } = [];
}

public sealed class AdminSyncReviewItem
{
    public AdminSyncChange Change { get; set; } = null!;
    public JsonElement? LocalSnapshot { get; set; }
    public Guid ResolutionRequestId { get; set; } = Guid.NewGuid();
    public string? ResolutionMode { get; set; }
    public JsonElement? ResolutionPayload { get; set; }
}

/// <summary>Durable private review queue. Queueing never advances the applied cursor.</summary>
public sealed class AdminSyncReviewStore(string filePath)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<AdminSyncReviewState> LoadAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try { return await ReadAsync(ct); }
        finally { Gate.Release(); }
    }

    public async Task StageAsync(Uri baseUri, AdminSyncBatch batch,
        Func<AdminSyncChange, JsonElement?> localSnapshot, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var state = await ReadAsync(ct);
            if ((state.ApiBaseUri.Length > 0 && state.ApiBaseUri != baseUri.AbsoluteUri) ||
                (state.BackendId.Length > 0 && state.BackendId != batch.BackendId))
                throw new InvalidOperationException("The backend changed. Reconcile the previous review queue before switching servers.");
            if (batch.After != state.DownloadedThrough || batch.Through < batch.After || !Guid.TryParse(batch.BackendId, out _))
                throw new InvalidOperationException("The downloaded batch no longer matches the saved review queue. Refresh first.");
            var cursor = batch.After;
            foreach (var change in batch.Items)
            {
                if (change.Sequence != ++cursor) throw new InvalidOperationException("The review batch contains a sequence gap.");
                state.Pending.Add(new() { Change = change, LocalSnapshot = localSnapshot(change)?.Clone() });
            }
            if (cursor != batch.Through) throw new InvalidOperationException("The review batch is incomplete.");
            state.ApiBaseUri = baseUri.AbsoluteUri;
            state.BackendId = batch.BackendId;
            state.DownloadedThrough = batch.Through;
            await WriteAsync(state, ct);
        }
        finally { Gate.Release(); }
    }

    public async Task<AdminSyncReviewItem> PrepareResolutionAsync(long sequence, string mode, JsonElement payload, CancellationToken ct = default)
    {
        if (mode is not ("server" or "local")) throw new ArgumentException("Unknown resolution mode.", nameof(mode));
        await Gate.WaitAsync(ct);
        try
        {
            var state = await ReadAsync(ct);
            var item = state.Pending.FirstOrDefault();
            if (item == null || item.Change.Sequence != sequence || sequence != state.AppliedThrough + 1)
                throw new InvalidOperationException("Resolve the first pending record before continuing.");
            if (item.ResolutionMode != null && (item.ResolutionMode != mode || item.ResolutionPayload is not { } saved || !JsonElement.DeepEquals(saved, payload)))
                throw new InvalidOperationException("This resolution has already started. Retry its original choice and payload before making a different decision.");
            item.ResolutionMode = mode;
            item.ResolutionPayload = payload.Clone();
            await WriteAsync(state, ct);
            return item;
        }
        finally { Gate.Release(); }
    }

    // Caller must persist domain data and obtain a matching server resolution receipt
    // before this checkpoint. If interrupted, the stable request ID permits retry.
    public async Task CompleteAsync(long sequence, Guid requestId, long resolvedRevision, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var state = await ReadAsync(ct);
            var first = state.Pending.FirstOrDefault();
            if (first == null || first.Change.Sequence != sequence || first.ResolutionRequestId != requestId ||
                sequence != state.AppliedThrough + 1 || resolvedRevision < first.Change.Revision)
                throw new InvalidOperationException("The resolution does not match the next pending review record.");
            state.ReviewedRevisions[first.Change.Kind + ":" + first.Change.Id] = resolvedRevision;
            state.AppliedThrough = sequence;
            state.Pending.RemoveAt(0);
            await WriteAsync(state, ct);
        }
        finally { Gate.Release(); }
    }

    private async Task<AdminSyncReviewState> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(filePath)) return new();
        using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<AdminSyncReviewState>(stream, cancellationToken: ct)
            ?? throw new JsonException("The synchronization review file is invalid. Restore it from backup; do not reset the cursor.");
    }

    private async Task WriteAsync(AdminSyncReviewState state, CancellationToken ct)
    {
        var path = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }
            ct.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
