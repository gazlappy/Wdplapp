using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Inbox;

namespace Wdpl2.Views.Inbox;

public sealed class AdminReviewPage : ContentPage
{
    private readonly AdminSyncReviewStore _queue = new(Path.Combine(FileSystem.AppDataDirectory, "admin-sync", "reviews.json"));
    private readonly Label _status = new() { Text = "Loading saved review queue…" };
    private readonly Label _identity = new();
    private readonly Editor _local = new() { IsReadOnly = true, HeightRequest = 260, AutoSize = EditorAutoSizeOption.Disabled };
    private readonly Editor _website = new() { IsReadOnly = true, HeightRequest = 260, AutoSize = EditorAutoSizeOption.Disabled };
    private readonly Button _download = new() { Text = "Download website changes" };
    private readonly Button _server = new() { Text = "Use website", IsEnabled = false };
    private readonly Button _keepLocal = new() { Text = "Keep local", IsEnabled = false };
    private readonly CheckBox _confirmed = new();
    private readonly Button _loadLinks = new() { Text = "Compare entries for explicit linking", IsVisible = false };
    private readonly Picker _linkCandidates = new() { Title = "Select the exact local entry", IsVisible = false };
    private readonly Button _saveLink = new() { Text = "Link selected entry (do not apply review)", IsVisible = false };
    private readonly Button _compareCurrent = new() { Text = "Compare current local data" };
    private readonly Button _compareWebsite = new() { Text = "Compare current website revision" };
    private readonly Editor _currentWebsite = new() { IsReadOnly = true, HeightRequest = 300, IsVisible = false };
    private readonly Editor _currentLocal = new() { IsReadOnly = true, HeightRequest = 260, IsVisible = false };
    private readonly Button _useCurrent = new() { Text = "Use this local preview (do not apply review)", IsVisible = false };
    private JsonElement? _currentSnapshot;
    private List<EntryFormSubmission> _candidates = [];
    private JsonElement? _linkSnapshot;
    private Guid? _linkLocalId;
    private AdminSyncReviewItem? _item;
    private string? _backend;
    private bool _busy;
    private bool _active;
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public AdminReviewPage()
    {
        Title = "Website change review";
        _download.Clicked += async (_, _) => await RunAsync(DownloadAsync);
        _server.Clicked += async (_, _) => await RunAsync(() => ResolveAsync(false));
        _keepLocal.Clicked += async (_, _) => await RunAsync(() => ResolveAsync(true));
        _confirmed.CheckedChanged += (_, _) => UpdateControls();
        _loadLinks.Clicked += async (_, _) => await RunAsync(LoadLinksAsync);
        _saveLink.Clicked += async (_, _) => await RunAsync(SaveLinkAsync);
        _compareCurrent.Clicked += async (_, _) => await RunAsync(CompareCurrentAsync);
        _compareWebsite.Clicked += async (_, _) => await RunAsync(CompareWebsiteAsync);
        _useCurrent.Clicked += async (_, _) => await RunAsync(UseCurrentAsync);
        _linkCandidates.SelectedIndexChanged += (_, _) =>
        {
            var index = _linkCandidates.SelectedIndex;
            _linkSnapshot = index >= 0 && index < _candidates.Count ? JsonSerializer.SerializeToElement(_candidates[index]) : null;
            _linkLocalId = index >= 0 && index < _candidates.Count ? _candidates[index].Id : null;
            _local.Text = _linkSnapshot is { } snapshot ? JsonSerializer.Serialize(snapshot, Pretty) : "Select a local entry to compare.";
            UpdateControls();
        };
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16, Spacing = 12,
                Children =
                {
                    new Label { Text = "Review website changes", FontSize = 22, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Uses saved Web Inbox settings and valid HTTPS certificates. Downloading only queues changes. Review the first item before continuing; original entry answers are never replaced." },
                    _download, _status, _identity,
                    _loadLinks, _linkCandidates,
                    new Label { Text = "Saved local review snapshot", FontAttributes = FontAttributes.Bold }, _local,
                    _compareCurrent, _currentLocal, _useCurrent,
                    new Label { Text = "Website revision", FontAttributes = FontAttributes.Bold }, _website,
                    _compareWebsite, _currentWebsite,
                    _saveLink,
                    new HorizontalStackLayout { Spacing = 8, Children = { _confirmed, new Label { Text = "I have compared both versions.", VerticalOptions = LayoutOptions.Center } } },
                    _server, _keepLocal,
                    new Label { Text = "Unmapped entries and unknown fixtures remain blocked. A started decision must be retried with the same choice. Stale revisions require reconciliation; do not delete the queue or reset its cursor." }
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _active = true;
        await RunAsync(ReloadAsync);
    }

    protected override void OnDisappearing()
    {
        _active = false;
        _confirmed.IsChecked = false;
        base.OnDisappearing();
    }

    private void UpdateControls()
    {
        _download.IsEnabled = !_busy;
        var ready = _active && !_busy && _confirmed.IsChecked && _item?.LocalSnapshot != null;
        _server.IsEnabled = ready && _item!.ResolutionMode is null or "server";
        _keepLocal.IsEnabled = ready && _item!.ResolutionMode is null or "local";
        _confirmed.IsEnabled = !_busy && _item?.LocalSnapshot != null;
        _loadLinks.IsVisible = _item is { Change.Kind: "entry_review", LocalSnapshot: null, ResolutionMode: null };
        _loadLinks.IsEnabled = _active && !_busy;
        _linkCandidates.IsEnabled = _active && !_busy;
        _saveLink.IsEnabled = _active && !_busy && _linkSnapshot != null && _linkLocalId != null;
        _compareCurrent.IsEnabled = _active && !_busy && _item is { ResolutionMode: null, ResolutionPayload: null };
        _useCurrent.IsEnabled = _compareCurrent.IsEnabled && _currentSnapshot != null;
        _compareWebsite.IsEnabled = _active && !_busy && _item != null;
    }

    private async Task RunAsync(Func<Task> work)
    {
        if (_busy) return;
        _busy = true; UpdateControls();
        try { await work(); }
        catch (Exception ex) { _status.Text = "Review stopped: " + ex.Message + " No queue reset was performed."; }
        finally { _busy = false; UpdateControls(); }
    }

    private async Task ReloadAsync()
    {
        var state = await _queue.LoadAsync();
        _item = state.Pending.FirstOrDefault();
        _backend = state.BackendId;
        _currentWebsite.Text = "";
        _currentWebsite.IsVisible = false;
        _currentSnapshot = null;
        _currentLocal.Text = "";
        _currentLocal.IsVisible = false;
        _useCurrent.IsVisible = false;
        _linkCandidates.IsVisible = false;
        _saveLink.IsVisible = false;
        _linkCandidates.SelectedIndex = -1;
        _candidates = [];
        _linkSnapshot = null;
        _linkLocalId = null;
        _confirmed.IsChecked = false;
        _status.Text = $"{state.Pending.Count} pending; downloaded through {state.DownloadedThrough}; applied through {state.AppliedThrough}.";
        _identity.Text = _item == null ? "No queued changes." :
            $"{_item.Change.Kind} · {_item.Change.Id}\nBackend: {state.BackendId}\nAPI: {state.ApiBaseUri}\nSequence {_item.Change.Sequence} · Revision {_item.Change.Revision} · Source {_item.Change.Source}\n" +
            (_item.ResolutionMode == null ? "No decision started." : $"Saved choice: {_item.ResolutionMode}. Retry only this choice.");
        _local.Text = _item?.LocalSnapshot is { } local ? JsonSerializer.Serialize(local, Pretty) : "No mapped local snapshot. This item cannot be applied.";
        _website.Text = _item == null ? "" : JsonSerializer.Serialize(_item.Change.Payload, Pretty);
    }

    private async Task CompareWebsiteAsync()
    {
        if (!_active || _item is not { } item || _backend == null) return;
        _currentWebsite.Text = "";
        _currentWebsite.IsVisible = false;
        _confirmed.IsChecked = false;
        using var client = new AdminSyncService(await WebInboxSettings.LoadAsync());
        var state = await _queue.LoadAsync();
        if (state.ApiBaseUri != client.BaseUri.AbsoluteUri || state.BackendId != _backend ||
            state.Pending.FirstOrDefault()?.ResolutionRequestId != item.ResolutionRequestId)
            throw new InvalidOperationException("The backend or queue changed. Reopen review before comparing.");
        var current = await client.FetchCurrentAsync(_backend, item.Change);
        if (!_active) return;
        _currentWebsite.Text = $"Current journal revision {current.Change.Revision}, sequence {current.Change.Sequence}\n" +
            JsonSerializer.Serialize(current.Change.Payload, Pretty) +
            (current.LiveMatchesJournal ? "\nLive state matches the journal at comparison time." :
                "\nWARNING: Live scorecard differs from the journal or is missing. This comparison is not a resolution.\n" + JsonSerializer.Serialize(current.Live, Pretty));
        _currentWebsite.IsVisible = true;
        _status.Text = "Read-only website comparison loaded. Queued revision, request, local data and cursors are unchanged. " +
            (current.Change.Revision != item.Change.Revision || !current.LiveMatchesJournal
                ? "Server conflict recovery is still required; do not reset a started decision."
                : "The server may change again before a resolution request.");
    }

    private async Task DownloadAsync()
    {
        using var client = new AdminSyncService(await WebInboxSettings.LoadAsync());
        var state = await _queue.LoadAsync();
        if (state.ApiBaseUri.Length > 0 && state.ApiBaseUri != client.BaseUri.AbsoluteUri)
            throw new InvalidOperationException("Saved settings point to a different backend. Reconcile the existing queue before switching.");
        _status.Text = "Downloading changes; no local records are being applied…";
        var batch = await client.FetchAsync(string.IsNullOrEmpty(state.BackendId) ? null : state.BackendId, state.DownloadedThrough);
        await using var context = new LeagueContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var league = new LeagueData
        {
            Seasons = await context.Seasons.AsNoTracking().ToListAsync(),
            Teams = await context.Teams.AsNoTracking().ToListAsync(),
            Fixtures = await context.Fixtures.AsNoTracking().ToListAsync(),
            WebsiteSettings = DataStore.Data.WebsiteSettings
        };
        await transaction.CommitAsync();
        await _queue.StageAsync(client.BaseUri, batch, change =>
        {
            try
            {
                return change.Kind == "scorecard" && Guid.TryParse(change.Id, out var id)
                    ? AdminScorecardMapper.Capture(league, id)
                    : change.Kind == "entry_review"
                        ? JsonSerializer.SerializeToElement(AdminEntryReviewMapper.FindMapped(league, batch.BackendId, change))
                        : null;
            }
            catch (InvalidOperationException) { return null; }
        });
        await ReloadAsync();
    }

    private async Task<JsonElement> CaptureCurrentAsync(AdminSyncReviewItem item)
    {
        using var client = new AdminSyncService(await WebInboxSettings.LoadAsync());
        var state = await _queue.LoadAsync();
        if (state.ApiBaseUri != client.BaseUri.AbsoluteUri || state.BackendId != _backend ||
            state.Pending.FirstOrDefault()?.ResolutionRequestId != item.ResolutionRequestId)
            throw new InvalidOperationException("The backend or queue changed. Reopen review before refreshing a snapshot.");
        await using var context = new LeagueContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var league = new LeagueData
        {
            Seasons = await context.Seasons.AsNoTracking().ToListAsync(),
            Teams = await context.Teams.AsNoTracking().ToListAsync(),
            Fixtures = await context.Fixtures.AsNoTracking().ToListAsync(),
            WebsiteSettings = DataStore.Data.WebsiteSettings
        };
        var snapshot = AdminReviewSnapshot.Capture(league, state.BackendId, item);
        await transaction.CommitAsync();
        return snapshot;
    }

    private async Task CompareCurrentAsync()
    {
        if (!_active || _item is not { ResolutionMode: null, ResolutionPayload: null } item) return;
        _currentSnapshot = null;
        _useCurrent.IsVisible = false;
        _confirmed.IsChecked = false;
        var snapshot = await CaptureCurrentAsync(item);
        if (!_active) return;
        _currentSnapshot = snapshot;
        _currentLocal.Text = JsonSerializer.Serialize(snapshot, Pretty);
        _currentLocal.IsVisible = true;
        _useCurrent.IsVisible = true;
        _status.Text = "Current local data is shown separately below the saved snapshot. Compare it before replacing the preview. Website data and cursors will not change.";
    }

    private async Task UseCurrentAsync()
    {
        if (!_active || _item is not { ResolutionMode: null, ResolutionPayload: null } item ||
            _backend == null || _currentSnapshot is not { } snapshot) return;
        if (!await DisplayAlert("Replace local review preview",
            "Have you compared the saved and current local data? Only the review snapshot will change. No domain data, website revision or cursor will be updated.", "Replace preview", "Cancel")) return;
        if (!_active) return;
        var current = await CaptureCurrentAsync(item);
        if (!JsonElement.DeepEquals(current, snapshot))
        {
            _currentSnapshot = null;
            _useCurrent.IsVisible = false;
            throw new InvalidOperationException("Local data changed during comparison. Compare current local data again.");
        }
        if (!_active) return;
        await _queue.RefreshLocalSnapshotAsync(_backend, item, snapshot);
        await ReloadAsync();
        _status.Text = "Local review preview refreshed. Compare it with the website revision before choosing a resolution. Nothing was applied or acknowledged.";
    }

    private async Task LoadLinksAsync()
    {
        if (_item is not { Change.Kind: "entry_review", LocalSnapshot: null, ResolutionMode: null } item || _backend == null) return;
        using var client = new AdminSyncService(await WebInboxSettings.LoadAsync());
        var state = await _queue.LoadAsync();
        if (state.ApiBaseUri != client.BaseUri.AbsoluteUri || state.BackendId != _backend ||
            state.Pending.FirstOrDefault()?.ResolutionRequestId != item.ResolutionRequestId)
            throw new InvalidOperationException("The backend or queue changed. Reopen review before linking.");
        var submission = await client.FetchEntryForLinkAsync(_backend, item.Change);
        if (!_active) return;
        var form = AdminEntryLinkMapper.SourceForm(DataStore.Data, item.Change);
        _candidates = JsonSerializer.Deserialize<List<EntryFormSubmission>>(JsonSerializer.Serialize(form.Submissions))!;
        _linkCandidates.ItemsSource = _candidates.Select(s => $"{s.EntryName} · {s.Id} · {s.SubmittedDate:g}").ToList();
        _linkCandidates.SelectedIndex = -1;
        _linkSnapshot = null;
        _linkLocalId = null;
        _linkCandidates.IsVisible = true;
        _saveLink.IsVisible = true;
        _local.Text = "Select an existing local entry from this form. No entry is selected automatically.";
        _website.Text = "Original website submission (read-only):\n" + JsonSerializer.Serialize(submission, Pretty) +
            "\n\nQueued review metadata:\n" + JsonSerializer.Serialize(item.Change.Payload, Pretty);
        _status.Text = _candidates.Count == 0 ? "This form has no local entries to link. Import or restore the entry first." :
            "Compare the original answers and identities. Linking preserves both versions and does not apply status or notes.";
    }

    private async Task SaveLinkAsync()
    {
        if (!_active || _item is not { Change.Kind: "entry_review", LocalSnapshot: null, ResolutionMode: null } item ||
            _backend == null || _linkSnapshot is not { } expected || _linkLocalId is not { } localId) return;
        if (!await DisplayAlert("Confirm explicit entry link",
            $"Have you compared both sets of answers? Link local entry {localId} to website submission {item.Change.Id}? Answers, status and notes will not change.", "Link this entry", "Cancel")) return;
        if (!_active) return;
        using var client = new AdminSyncService(await WebInboxSettings.LoadAsync());
        var state = await _queue.LoadAsync();
        var first = state.Pending.FirstOrDefault();
        if (state.BackendId != _backend || state.ApiBaseUri != client.BaseUri.AbsoluteUri || first?.ResolutionRequestId != item.ResolutionRequestId ||
            first.ResolutionMode != null || first.LocalSnapshot != null)
            throw new InvalidOperationException("The queue changed. Reload before linking.");
        await using var context = new LeagueContext();
        await RefreshEntryIdentitiesAsync(context);
        var snapshot = DataStore.LinkEntryReview(_backend, item.Change, localId, expected);
        // Mapping is saved first. An interrupted checkpoint can be recovered by selecting the same mapped entry.
        await _queue.AttachLinkedEntryAsync(_backend, item.ResolutionRequestId, snapshot);
        await ReloadAsync();
        _status.Text = "Entry linked. Compare the queued review again and explicitly choose Use website or Keep local.";
    }

    private static async Task RefreshEntryIdentitiesAsync(LeagueContext context)
    {
        var seasons = await context.Seasons.AsNoTracking().ToListAsync();
        var teams = await context.Teams.AsNoTracking().ToListAsync();
        foreach (var season in seasons)
        {
            var previous = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == season.Id);
            if (previous == null) continue;
            season.Settings = previous.Settings;
            season.BlackoutDateTitles = previous.BlackoutDateTitles;
        }
        DataStore.Data.Seasons = seasons;
        DataStore.Data.Teams = teams;
    }

    private async Task ResolveAsync(bool keepLocal)
    {
        var item = _item;
        if (!_active || !_confirmed.IsChecked || item?.LocalSnapshot == null || _backend == null) return;
        var choice = keepLocal ? "Keep local" : "Use website";
        if (!await DisplayAlert(choice, $"Apply this choice for {item.Change.Kind} {item.Change.Id}? This can change local data or website review data. It will not create teams or players.", choice, "Cancel")) return;
        if (!_active) return;
        using var client = new AdminSyncService(await WebInboxSettings.LoadAsync());
        await using var context = new LeagueContext();
        var state = await _queue.LoadAsync();
        if (state.BackendId != _backend || state.Pending.FirstOrDefault()?.ResolutionRequestId != item.ResolutionRequestId)
            throw new InvalidOperationException("The review queue changed. Reopen this page and compare the current item.");
        // Refresh authoritative season locks before applying JSON entry metadata.
        await RefreshEntryIdentitiesAsync(context);
        var coordinator = new AdminSyncCoordinator(_queue, client, new AdminScorecardPersistence(context), SqliteDataStore.RefreshAfterAdminSync);
        try
        {
            if (item.Change.Kind == "entry_review") await coordinator.ResolveEntryReviewAsync(item.Change.Sequence, keepLocal);
            else if (item.Change.Kind == "scorecard" && keepLocal) await coordinator.KeepLocalScorecardAsync(item.Change.Sequence);
            else if (item.Change.Kind == "scorecard") await coordinator.AcceptServerScorecardAsync(item.Change.Sequence);
            else throw new InvalidOperationException("Unsupported review type.");
        }
        finally { await ReloadAsync(); }
    }
}
