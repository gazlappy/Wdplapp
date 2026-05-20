using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wdpl2.Services;
using Wdpl2.Services.Cloud;
using Wdpl2.Services.Inbox;

namespace Wdpl2.ViewModels.Inbox;

/// <summary>
/// Drives the Web Inbox page — pulls pending submissions from wdpl.uk and
/// lets the admin mark them as processed.
/// </summary>
public partial class InboxViewModel : BaseViewModel
{
    private readonly IWebInboxService _service;
    private readonly IWebPublishService _publish;
    private readonly IMatchResultImporter _importer;
    private readonly BackendDeployService _backend;

    public InboxViewModel(
        ISeasonService seasonService,
        IWebInboxService service,
        IWebPublishService publish,
        IMatchResultImporter importer,
        BackendDeployService backend)
        : base(seasonService)
    {
        _service = service;
        _publish = publish;
        _importer = importer;
        _backend = backend;
    }

    public ObservableCollection<WebSubmission> Items { get; } = new();
    public ObservableCollection<FixtureSubmissionGroup> ResultGroups { get; } = new();
    public ObservableCollection<WebSubmission> OtherItems { get; } = new();

    [ObservableProperty]
    private string _baseUrl = WebInboxSettings.DefaultBaseUrl;

    [ObservableProperty]
    private string _adminUser = "";

    [ObservableProperty]
    private string _adminPassword = "";

    [ObservableProperty]
    private bool _ignoreSslErrors;

    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private bool _hasResultGroups;

    [ObservableProperty]
    private bool _hasOtherItems;

    public bool IsEmpty => !HasItems;
    partial void OnHasItemsChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    public async Task InitializeAsync()
    {
        var s = await WebInboxSettings.LoadAsync();
        BaseUrl = s.BaseUrl;
        AdminUser = s.AdminUser;
        AdminPassword = s.AdminPassword;
        IgnoreSslErrors = s.IgnoreSslErrors;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var s = new WebInboxSettings
        {
            BaseUrl = BaseUrl,
            AdminUser = AdminUser,
            AdminPassword = AdminPassword,
            IgnoreSslErrors = IgnoreSslErrors
        };
        await s.SaveAsync();
        SetStatus("Settings saved.");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var ct = ResetLoadToken();
            var list = await _service.GetPendingAsync(ct);
            Items.Clear();
            foreach (var item in list)
                Items.Add(item);
            HasItems = Items.Count > 0;

            ResultGroups.Clear();
            OtherItems.Clear();
            var groups = SubmissionGrouper.Group(list, out var others);
            foreach (var g in groups) ResultGroups.Add(g);
            foreach (var o in others) OtherItems.Add(o);
            HasResultGroups = ResultGroups.Count > 0;
            HasOtherItems   = OtherItems.Count > 0;

            SetStatus($"Loaded {Items.Count} pending submission(s) - {ResultGroups.Count} fixture group(s).");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetStatus($"Refresh failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ImportGroupAsync(FixtureSubmissionGroup? group)
    {
        if (group is null) return;
        if (group.IsDisputed)
        {
            SetStatus($"Cannot import - {group.DisputeCount} frame(s) disputed. Use Force home/away or Reject.");
            return;
        }
        await DoImportAsync(group, ImportSidePreference.Auto);
    }

    [RelayCommand]
    private async Task ForceImportHomeAsync(FixtureSubmissionGroup? group)
    {
        if (group is null || group.HomePayload is null) return;
        if (!await ConfirmAsync("Force-import HOME card",
            $"Use the HOME captain's card as the authoritative result for {group.Summary}?"))
            return;
        await DoImportAsync(group, ImportSidePreference.Home);
    }

    [RelayCommand]
    private async Task ForceImportAwayAsync(FixtureSubmissionGroup? group)
    {
        if (group is null || group.AwayPayload is null) return;
        if (!await ConfirmAsync("Force-import AWAY card",
            $"Use the AWAY captain's card as the authoritative result for {group.Summary}?"))
            return;
        await DoImportAsync(group, ImportSidePreference.Away);
    }

    private async Task DoImportAsync(FixtureSubmissionGroup group, ImportSidePreference prefer)
    {
        IsLoading = true;
        try
        {
            var result = await _importer.ImportAsync(group, prefer);
            if (!result.Success)
            {
                SetStatus($"Import failed: {result.Message}");
                return;
            }

            var ids = new List<long>();
            if (group.HomeCard != null) ids.Add(group.HomeCard.Id);
            if (group.AwayCard != null) ids.Add(group.AwayCard.Id);
            ids.AddRange(group.Extras.Select(e => e.Id));
            if (ids.Count > 0)
            {
                try { await _service.MarkProcessedAsync(ids, AdminUser); } catch { /* surface below */ }
            }

            var src = prefer switch
            {
                ImportSidePreference.Home => " (home card)",
                ImportSidePreference.Away => " (away card)",
                _ => ""
            };
            SetStatus($"{group.Summary}{src}: {result.Message}");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Import failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DiscardGroupAsync(FixtureSubmissionGroup? group)
    {
        if (group is null) return;
        if (!await ConfirmAsync("Discard cards",
            $"Discard all submissions for {group.Summary}? They will be marked processed with no result imported."))
            return;
        await MarkGroupProcessedAsync(group, "discarded");
    }

    [RelayCommand]
    private async Task RejectGroupAsync(FixtureSubmissionGroup? group)
    {
        if (group is null) return;
        var note = await PromptAsync("Reject with note",
            "Reason (will be recorded against the submissions):", "needs clarification");
        if (string.IsNullOrWhiteSpace(note)) return;
        await MarkGroupProcessedAsync(group, "rejected: " + note.Trim());
    }

    private async Task MarkGroupProcessedAsync(FixtureSubmissionGroup group, string note)
    {
        var ids = new List<long>();
        if (group.HomeCard != null) ids.Add(group.HomeCard.Id);
        if (group.AwayCard != null) ids.Add(group.AwayCard.Id);
        ids.AddRange(group.Extras.Select(e => e.Id));
        if (ids.Count == 0) return;

        try
        {
            await _service.MarkProcessedAsync(ids, AdminUser, note);
            SetStatus($"{note}: {ids.Count} card(s) for {group.Summary}.");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Action failed: {ex.Message}");
        }
    }

    private static async Task<bool> ConfirmAsync(string title, string message)
    {
        var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
        if (page is null) return true;
        return await page.DisplayAlert(title, message, "Yes", "Cancel");
    }

    private static async Task<string?> PromptAsync(string title, string message, string? initial = null)
    {
        var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
        if (page is null) return initial;
        return await page.DisplayPromptAsync(title, message, "OK", "Cancel", initialValue: initial);
    }

    [RelayCommand]
    private async Task MarkProcessedAsync(WebSubmission? submission)
    {
        if (submission is null) return;
        try
        {
            await _service.MarkProcessedAsync(new[] { submission.Id }, AdminUser);
            Items.Remove(submission);
            HasItems = Items.Count > 0;
            SetStatus($"Marked #{submission.Id} as processed.");
        }
        catch (Exception ex)
        {
            SetStatus($"Mark-processed failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PublishLeagueAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var (t, p, f) = await _publish.PublishLeagueAsync();
            SetStatus($"Published {t} team(s), {p} player(s), {f} fixture(s) to wdpl.uk.");
        }
        catch (Exception ex)
        {
            SetStatus($"Publish failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Returns the site root (everything before "/api/") for the configured Base URL.
    /// e.g. "https://wdpl.uk/api/" -> "https://wdpl.uk".
    /// </summary>
    private string SiteRoot()
    {
        var b = (BaseUrl ?? "").Trim();
        if (string.IsNullOrEmpty(b)) return "https://wdpl.uk";
        var idx = b.IndexOf("/api", StringComparison.OrdinalIgnoreCase);
        if (idx > 0) return b.Substring(0, idx).TrimEnd('/');
        return b.TrimEnd('/');
    }

    [RelayCommand]
    private async Task OpenLinkAsync(string? relative)
    {
        try
        {
            var url = (relative ?? "").StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? relative!
                : $"{SiteRoot()}/{(relative ?? "").TrimStart('/')}";
            await Launcher.Default.OpenAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            SetStatus($"Open link failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeployBackendAsync()
    {
        if (IsLoading) return;
        if (!await ConfirmAsync("Deploy backend",
            "Upload the bundled PHP/HTML backend (captain portal, admin pages, API endpoints) to the website's FTP host? Existing files will be overwritten."))
            return;

        IsLoading = true;
        try
        {
            var settings = Wdpl2.DataStore.Data.WebsiteSettings;
            var progress = new Progress<UploadProgress>(p =>
                SetStatus($"{p.Status} ({p.FilesCompleted}/{p.TotalFiles})"));

            var result = await _backend.DeployAsync(settings, progress);
            SetStatus(result.Message.Replace(Environment.NewLine, " | "));
        }
        catch (Exception ex)
        {
            SetStatus($"Backend deploy failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task MarkAllProcessedAsync()
    {
        if (Items.Count == 0) return;
        var ids = Items.Select(i => i.Id).ToList();
        try
        {
            await _service.MarkProcessedAsync(ids, AdminUser);
            Items.Clear();
            HasItems = false;
            SetStatus($"Marked {ids.Count} submission(s) as processed.");
        }
        catch (Exception ex)
        {
            SetStatus($"Bulk mark-processed failed: {ex.Message}");
        }
    }
}
