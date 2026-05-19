using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wdpl2.Services;
using Wdpl2.Services.Inbox;

namespace Wdpl2.ViewModels.Inbox;

/// <summary>
/// Drives the Web Inbox page — pulls pending submissions from wdpl.uk and
/// lets the admin mark them as processed.
/// </summary>
public partial class InboxViewModel : BaseViewModel
{
    private readonly IWebInboxService _service;

    public InboxViewModel(ISeasonService seasonService, IWebInboxService service)
        : base(seasonService)
    {
        _service = service;
    }

    public ObservableCollection<WebSubmission> Items { get; } = new();

    [ObservableProperty]
    private string _baseUrl = WebInboxSettings.DefaultBaseUrl;

    [ObservableProperty]
    private string _adminUser = "";

    [ObservableProperty]
    private string _adminPassword = "";

    [ObservableProperty]
    private bool _hasItems;

    public async Task InitializeAsync()
    {
        var s = await WebInboxSettings.LoadAsync();
        BaseUrl = s.BaseUrl;
        AdminUser = s.AdminUser;
        AdminPassword = s.AdminPassword;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var s = new WebInboxSettings
        {
            BaseUrl = BaseUrl,
            AdminUser = AdminUser,
            AdminPassword = AdminPassword
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
            SetStatus($"Loaded {Items.Count} pending submission(s).");
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
