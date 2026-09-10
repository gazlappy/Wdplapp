using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Wdpl2.Services.Inbox;

public sealed record AdminSyncChange(long Sequence, string Kind, string Id, long Revision,
    string? SeasonId, string Source, JsonElement Payload);

public sealed record AdminSyncBatch(string BackendId, long After, long Through, IReadOnlyList<AdminSyncChange> Items);
public sealed record AdminSyncCurrent(AdminSyncChange Change, bool LiveMatchesJournal, JsonElement? Live);

/// <summary>Downloads a complete review batch without mutating local records or acknowledging changes.</summary>
public sealed class AdminSyncService : IDisposable
{
    private readonly HttpClient _http;
    private readonly AuthenticationHeaderValue _authorization;
    public Uri BaseUri { get; }

    public AdminSyncService(WebInboxSettings settings, HttpMessageHandler? handler = null)
    {
        BaseUri = HostedEntryFormsService.ValidateBaseUri(settings.BaseUrl);
        if (settings.IgnoreSslErrors || string.IsNullOrWhiteSpace(settings.AdminUser) ||
            string.IsNullOrEmpty(settings.AdminPassword) || settings.AdminUser.Contains(':'))
            throw new InvalidOperationException("Synchronization requires a valid HTTPS certificate and saved Web Inbox admin credentials.");
        _authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{settings.AdminUser}:{settings.AdminPassword}")));
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
    }

    public async Task<AdminSyncBatch> FetchAsync(string? backendId, long after, CancellationToken ct = default)
    {
        if (after < 0 || (after > 0 && !Guid.TryParse(backendId, out _)))
            throw new InvalidOperationException("A saved backend identity is required to resume synchronization.");
        var items = new List<AdminSyncChange>();
        long cursor = after;
        long? through = null;
        long bytes = 0;
        do
        {
            var query = $"admin/sync-changes.php?after={cursor}";
            if (!string.IsNullOrEmpty(backendId)) query += "&backendId=" + Uri.EscapeDataString(backendId);
            if (through.HasValue) query += $"&through={through.Value}";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, query));
            request.Headers.Authorization = _authorization;
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Synchronization returned HTTP {(int)response.StatusCode}. No changes were applied or acknowledged.");
            if (response.Content.Headers.ContentType?.MediaType != "application/json")
                throw new JsonException("Synchronization endpoint did not return JSON.");
            using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[32768];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                bytes += read;
                if (buffer.Length + read > 24 * 1024 * 1024 || bytes > 64 * 1024 * 1024)
                    throw new InvalidOperationException("Synchronization batch is too large. No partial batch was applied.");
                buffer.Write(chunk, 0, read);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            var root = document.RootElement;
            var receivedBackend = root.GetProperty("backendId").GetString();
            var receivedThrough = root.GetProperty("through").GetInt64();
            if (root.GetProperty("protocol").GetInt32() != 1 || !Guid.TryParse(receivedBackend, out _) ||
                (backendId != null && backendId != receivedBackend) || receivedThrough < cursor ||
                (through.HasValue && receivedThrough != through))
                throw new JsonException("Synchronization identity or snapshot changed. Reconciliation is required.");
            backendId = receivedBackend!;
            through = receivedThrough;
            var pageStart = cursor;
            var page = root.GetProperty("items");
            if (page.GetArrayLength() > 20) throw new JsonException("Synchronization page exceeds its contract.");
            foreach (var item in page.EnumerateArray())
            {
                var sequence = item.GetProperty("sequence").GetInt64();
                var revision = item.GetProperty("revision").GetInt64();
                var kind = item.GetProperty("kind").GetString();
                var id = item.GetProperty("id").GetString();
                var source = item.GetProperty("source").GetString();
                var payload = item.GetProperty("payload");
                if (sequence != cursor + 1 || sequence > through || revision < 1 ||
                    kind is not ("scorecard" or "entry_review") || string.IsNullOrEmpty(id) || id.Length > 160 ||
                    source is not ("web" or "desktop") || payload.ValueKind != JsonValueKind.Object)
                    throw new JsonException("Invalid or incomplete synchronization record.");
                items.Add(new(sequence, kind, id, revision, item.GetProperty("seasonId").GetString(), source, payload.Clone()));
                cursor = sequence;
                if (items.Count > 10000) throw new InvalidOperationException("Too many changes for one review batch.");
            }
            var next = root.GetProperty("nextAfter");
            if (next.ValueKind == JsonValueKind.Null)
            {
                if (cursor != through) throw new JsonException("Synchronization batch ended before its snapshot boundary.");
                return new(backendId, after, through.Value, items);
            }
            if (next.GetInt64() != cursor || cursor <= pageStart || cursor >= through)
                throw new JsonException("Synchronization pagination did not advance correctly.");
        } while (true);
    }

    public Task<long> AcceptAppliedScorecardAsync(string backendId, AdminSyncReviewItem item, CancellationToken ct = default) =>
        ResolveScorecardAsync(backendId, item, false, ct);

    public Task<long> KeepLocalScorecardAsync(string backendId, AdminSyncReviewItem item, CancellationToken ct = default) =>
        ResolveScorecardAsync(backendId, item, true, ct);

    private async Task<long> ResolveScorecardAsync(string backendId, AdminSyncReviewItem item, bool keepLocal, CancellationToken ct)
    {
        if (!Guid.TryParse(backendId, out _) || item.Change.Kind != "scorecard" ||
            item.ResolutionRequestId == Guid.Empty || item.Change.Revision < 1)
            throw new InvalidOperationException("A reviewed scorecard with a stable request and backend identity is required.");
        var version = item.Change.Payload.GetProperty("version").GetInt64();
        if (keepLocal && (item.ResolutionMode != "local" || item.ResolutionPayload is not { ValueKind: JsonValueKind.Array }))
            throw new InvalidOperationException("Save the reviewed local frame payload before sending it.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, keepLocal ? "admin/sync-keep-local.php" : "admin/sync-accept.php"));
        request.Headers.Authorization = _authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            backend_id = backendId, id = item.Change.Id, expected_revision = item.Change.Revision,
            expected_version = version, request_id = item.ResolutionRequestId, applied = true,
            season_id = item.Change.SeasonId, frames = keepLocal ? item.ResolutionPayload : null
        }), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Acceptance returned HTTP {(int)response.StatusCode}. Keep this review item; a conflict requires fresh review, other failures may be retried with the same request ID.");
        if (response.Content.Headers.ContentType?.MediaType != "application/json")
            throw new JsonException("Acceptance did not return a JSON receipt.");
        await response.Content.LoadIntoBufferAsync(65536, timeout.Token);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        var receipt = document.RootElement;
        var revision = receipt.GetProperty("revision").GetInt64();
        var expectedRevision = !keepLocal && item.Change.Source == "desktop" ? item.Change.Revision : checked(item.Change.Revision + 1);
        if (receipt.GetProperty("protocol").GetInt32() != 1 || !receipt.GetProperty("accepted").GetBoolean() ||
            receipt.GetProperty("backendId").GetString() != backendId || receipt.GetProperty("id").GetString() != item.Change.Id ||
            !Guid.TryParse(receipt.GetProperty("requestId").GetString(), out var requestId) || requestId != item.ResolutionRequestId ||
            revision != expectedRevision || receipt.GetProperty("sequence").GetInt64() < item.Change.Sequence ||
            ((keepLocal || item.Change.Source == "web") && receipt.GetProperty("sequence").GetInt64() == item.Change.Sequence))
            throw new JsonException("Acceptance receipt does not match the reviewed scorecard. The review queue must not advance.");
        return revision;
    }

    public async Task<long> ResolveEntryReviewAsync(string backendId, AdminSyncReviewItem item, CancellationToken ct = default)
    {
        if (!Guid.TryParse(backendId, out _) || item.Change.Kind != "entry_review" ||
            item.ResolutionRequestId == Guid.Empty || item.Change.Revision < 1 ||
            item.Change.Source is not ("web" or "desktop") || item.ResolutionMode is not ("server" or "local") ||
            item.ResolutionPayload is not { ValueKind: JsonValueKind.Object } payload)
            throw new InvalidOperationException("Save a reviewed entry decision with a stable backend and request identity first.");
        var review = AdminEntryReviewMapper.ReviewValues(payload);
        if (item.ResolutionMode == "server" && !JsonElement.DeepEquals(review, AdminEntryReviewMapper.ReviewValues(item.Change.Payload)))
            throw new InvalidOperationException("The saved server choice differs from the downloaded review.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, "admin/sync-entry-review.php"));
        request.Headers.Authorization = _authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            backend_id = backendId, id = item.Change.Id, expected_revision = item.Change.Revision,
            request_id = item.ResolutionRequestId, mode = item.ResolutionMode, applied = true,
            status = review.GetProperty("status").GetString(), notes = review.GetProperty("notes").GetString()
        }), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Entry review returned HTTP {(int)response.StatusCode}. Keep the saved decision; conflicts require fresh review, other failures can be retried.");
        if (response.Content.Headers.ContentType?.MediaType != "application/json")
            throw new JsonException("Entry review did not return a JSON receipt.");
        await response.Content.LoadIntoBufferAsync(65536, timeout.Token);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        var receipt = document.RootElement;
        var echo = item.ResolutionMode == "server" && item.Change.Source == "desktop";
        var expectedRevision = echo ? item.Change.Revision : checked(item.Change.Revision + 1);
        var sequence = receipt.GetProperty("sequence").GetInt64();
        if (receipt.GetProperty("protocol").GetInt32() != 1 || !receipt.GetProperty("accepted").GetBoolean() ||
            receipt.GetProperty("backendId").GetString() != backendId || receipt.GetProperty("id").GetString() != item.Change.Id ||
            !Guid.TryParse(receipt.GetProperty("requestId").GetString(), out var requestId) || requestId != item.ResolutionRequestId ||
            receipt.GetProperty("revision").GetInt64() != expectedRevision ||
            (echo ? sequence != item.Change.Sequence : sequence <= item.Change.Sequence))
            throw new JsonException("Entry review receipt does not match the saved decision. The review queue must not advance.");
        return expectedRevision;
    }

    public async Task<JsonElement> FetchEntryForLinkAsync(string backendId, AdminSyncChange change, CancellationToken ct = default)
    {
        if (!Guid.TryParse(backendId, out _) || change.Kind != "entry_review" ||
            !long.TryParse(change.Id, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) || id < 1 ||
            change.Payload.GetProperty("submissionSequence").GetInt64() != id)
            throw new InvalidOperationException("An explicit backend and submission identity are required.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri,
            $"admin/entry-reviews.php?after={id - 1}&through={id}&backendId={Uri.EscapeDataString(backendId)}"));
        request.Headers.Authorization = _authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType != "application/json") throw new JsonException("Submission endpoint did not return JSON.");
        await response.Content.LoadIntoBufferAsync(131072, timeout.Token);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        var root = document.RootElement;
        if (root.GetProperty("protocol").GetInt32() != 1 || root.GetProperty("backendId").GetString() != backendId ||
            root.GetProperty("through").GetInt64() != id || root.GetProperty("nextAfter").ValueKind != JsonValueKind.Null ||
            root.GetProperty("items").GetArrayLength() != 1) throw new JsonException("The hosted submission is missing or its backend changed.");
        var row = root.GetProperty("items")[0];
        var review = row.GetProperty("review");
        var submission = row.GetProperty("submission");
        if (row.GetProperty("id").GetString() != change.Id || review.GetProperty("submissionSequence").GetInt64() != id ||
            review.GetProperty("formId").GetString() != change.Payload.GetProperty("formId").GetString() ||
            review.GetProperty("clientId").GetString() != change.Payload.GetProperty("clientId").GetString() ||
            submission.GetProperty("formId").GetString() != review.GetProperty("formId").GetString() ||
            submission.GetProperty("id").GetString() != review.GetProperty("clientId").GetString() ||
            submission.GetProperty("values").ValueKind != JsonValueKind.Object)
            throw new JsonException("The hosted submission identities do not match the queued review.");
        return submission.Clone();
    }

    public async Task<AdminSyncCurrent> FetchCurrentAsync(string backendId, AdminSyncChange expected, CancellationToken ct = default)
    {
        if (!Guid.TryParse(backendId, out _) || expected.Kind is not ("scorecard" or "entry_review") || string.IsNullOrEmpty(expected.Id))
            throw new InvalidOperationException("A saved backend and review identity are required.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri,
            $"admin/sync-current.php?backendId={Uri.EscapeDataString(backendId)}&kind={expected.Kind}&id={Uri.EscapeDataString(expected.Id)}"));
        request.Headers.Authorization = _authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType != "application/json") throw new JsonException("Current comparison did not return JSON.");
        await response.Content.LoadIntoBufferAsync(4 * 1024 * 1024, timeout.Token);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        var root = document.RootElement;
        var item = root.GetProperty("item");
        var current = new AdminSyncChange(item.GetProperty("sequence").GetInt64(), item.GetProperty("kind").GetString()!,
            item.GetProperty("id").GetString()!, item.GetProperty("revision").GetInt64(), item.GetProperty("seasonId").GetString(),
            item.GetProperty("source").GetString()!, item.GetProperty("payload").Clone());
        if (root.GetProperty("protocol").GetInt32() != 1 || root.GetProperty("backendId").GetString() != backendId ||
            current.Kind != expected.Kind || current.Id != expected.Id || current.SeasonId != expected.SeasonId ||
            current.Revision < expected.Revision || current.Sequence < expected.Sequence || current.Payload.ValueKind != JsonValueKind.Object ||
            current.Source is not ("web" or "desktop") ||
            (current.Revision == expected.Revision && (current.Sequence != expected.Sequence || current.Source != expected.Source || !JsonElement.DeepEquals(current.Payload, expected.Payload))) ||
            (current.Revision > expected.Revision && current.Sequence <= expected.Sequence))
            throw new JsonException("Current comparison does not match the saved review identity or journal history.");
        if (current.Kind == "entry_review")
            foreach (var key in new[] { "formId", "clientId", "submissionSequence" })
                if (!JsonElement.DeepEquals(current.Payload.GetProperty(key), expected.Payload.GetProperty(key)))
                    throw new JsonException("The submission identity changed.");
        var matches = root.GetProperty("liveMatchesJournal").GetBoolean();
        var live = root.GetProperty("live");
        if (live.ValueKind is not (JsonValueKind.Null or JsonValueKind.Object) ||
            (current.Kind == "entry_review" && (!matches || live.ValueKind != JsonValueKind.Null)) ||
            (current.Kind == "scorecard" && matches && live.ValueKind != JsonValueKind.Object))
            throw new JsonException("Invalid live comparison state.");
        return new(current, matches, live.ValueKind == JsonValueKind.Null ? null : live.Clone());
    }

    public void Dispose() => _http.Dispose();
}
