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
                    new Label { Text = "Local snapshot at download", FontAttributes = FontAttributes.Bold }, _local,
                    new Label { Text = "Website revision", FontAttributes = FontAttributes.Bold }, _website,
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
        _confirmed.IsChecked = false;
        _status.Text = $"{state.Pending.Count} pending; downloaded through {state.DownloadedThrough}; applied through {state.AppliedThrough}.";
        _identity.Text = _item == null ? "No queued changes." :
            $"{_item.Change.Kind} · {_item.Change.Id}\nBackend: {state.BackendId}\nAPI: {state.ApiBaseUri}\nSequence {_item.Change.Sequence} · Revision {_item.Change.Revision} · Source {_item.Change.Source}\n" +
            (_item.ResolutionMode == null ? "No decision started." : $"Saved choice: {_item.ResolutionMode}. Retry only this choice.");
        _local.Text = _item?.LocalSnapshot is { } local ? JsonSerializer.Serialize(local, Pretty) : "No mapped local snapshot. This item cannot be applied.";
        _website.Text = _item == null ? "" : JsonSerializer.Serialize(_item.Change.Payload, Pretty);
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
        var seasons = await context.Seasons.AsNoTracking().ToListAsync();
        foreach (var season in seasons)
        {
            var previous = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == season.Id);
            if (previous == null) continue;
            season.Settings = previous.Settings;
            season.BlackoutDateTitles = previous.BlackoutDateTitles;
        }
        DataStore.Data.Seasons = seasons;
        DataStore.Data.Teams = await context.Teams.AsNoTracking().ToListAsync();
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
