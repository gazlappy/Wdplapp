using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class EntryFormsSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private readonly ObservableCollection<FormDisplayItem> _forms = new();
    private readonly ObservableCollection<EntryDisplayItem> _entries = new();
    private readonly ObservableCollection<CrossRefItem> _crossRefItems = new();
    private Guid? _selectedFormId;
    private Guid? _selectedEntryId;
    private EntryForm? _draft;
    private bool _isNewDraft;
    private bool _changingSelection;
    private bool _showRecords;
    private bool _initializing = true;
    private Dictionary<string, string> _entryValues = new();
    private static readonly HttpClient _httpClient = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };

    public EntryFormsSettingsPage()
    {
        InitializeComponent();
        FormsCollection.ItemsSource = _forms;
        EntriesCollection.ItemsSource = _entries;
        CrossRefList.ItemsSource = _crossRefItems;
        ShowEntryFormsSwitch.IsToggled = League.WebsiteSettings.ShowEntryForms;
        FormServiceUrlEntry.Text = League.WebsiteSettings.FormServiceUrl;
        FormServiceTokenEntry.Text = League.WebsiteSettings.FormServiceApiToken;
        FormServiceFetchUrlEntry.Text = League.WebsiteSettings.FormServiceFetchUrl;
        HostedWebsiteUrlEntry.Text = League.WebsiteSettings.WebsiteUrl;
        UseHostedFormsSwitch.IsToggled = League.WebsiteSettings.UseHostedEntryForms;
        UpdateHostedModeUi();
        EntryFilterPicker.SelectedIndex = 0;
        LoadForms();
        UpdateRecordsHeader();
        UpdateDeliveryStatus();
        _initializing = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    private void OnWorkspaceSizeChanged(object? sender, EventArgs e) => UpdateWorkspaceLayout();
    private void OnDesignTabClicked(object? sender, EventArgs e) { _showRecords = false; UpdateWorkspaceLayout(); }
    private void OnRecordsTabClicked(object? sender, EventArgs e) { _showRecords = true; UpdateWorkspaceLayout(); }

    private void UpdateWorkspaceLayout()
    {
        if (WorkspaceGrid == null || Width <= 0) return;
        var compact = Width < 1100;
        WorkspaceTabs.IsVisible = compact;
        WorkspaceDivider.IsVisible = !compact;
        DesignPane.IsVisible = !compact || !_showRecords;
        RecordsPane.IsVisible = !compact || _showRecords;
        WorkspaceGrid.ColumnDefinitions[0].Width = GridLength.Star;
        WorkspaceGrid.ColumnDefinitions[1].Width = compact ? new GridLength(0) : GridLength.Auto;
        WorkspaceGrid.ColumnDefinitions[2].Width = compact ? new GridLength(0) : GridLength.Star;
        Grid.SetColumn(RecordsPane, compact ? 0 : 2);
    }

    private void OnToggleServiceClicked(object? sender, EventArgs e) => ServiceSettingsPanel.IsVisible = !ServiceSettingsPanel.IsVisible;

    private void UpdateDeliveryStatus()
    {
        DeliveryStatusLabel.Text = League.WebsiteSettings.UseHostedEntryForms
            ? EntryFormRules.IsHostedDeliveryReady(League.WebsiteSettings)
                ? "Own-hosting delivery confirmed — republish saved form changes, then regenerate and deploy the website."
                : "Own-hosting preference saved — setup/publishing still required. Online delivery is not enabled yet."
            : EntryFormRules.PublicEndpoint(League.WebsiteSettings.FormServiceUrl) != null
            ? "Online POST endpoint configured — verify delivery before publishing."
            : "Download and send mode — no safe online delivery endpoint configured.";
    }

    private void OnHostedModeToggled(object? sender, ToggledEventArgs e)
    {
        UpdateHostedModeUi();
        if (_initializing) return;
        EntryFormRules.SetHostedMode(League.WebsiteSettings, e.Value);
        DataStore.SaveJsonOnly();
        UpdateDeliveryStatus();
        FetchStatusLabel.IsVisible = false;
    }

    private static void ValidateWebsiteUri(string value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException(
                "Set the public Website URL in Online Forms delivery settings (or website settings). " +
                "Use the HTTPS address visitors open, without credentials, a query or fragment.");
    }

    private void SaveHostedSettings()
    {
        var websiteUrl = HostedWebsiteUrlEntry.Text?.Trim() ?? "";
        ValidateWebsiteUri(websiteUrl);
        EntryFormRules.SetHostedMode(League.WebsiteSettings, true);
        if (!string.Equals(League.WebsiteSettings.WebsiteUrl, websiteUrl, StringComparison.Ordinal))
            League.WebsiteSettings.HostedEntryFormsPublished = false;
        League.WebsiteSettings.WebsiteUrl = websiteUrl;
        DataStore.SaveJsonOnly();
        UpdateDeliveryStatus();
    }

    private void OnSaveHostedSettingsClicked(object? sender, EventArgs e)
    {
        try
        {
            SaveHostedSettings();
            FetchStatusLabel.Text = "Hosting settings saved locally. Publish saved forms to confirm backend delivery; saving alone does not publish.";
            FetchStatusLabel.TextColor = Color.FromArgb("#10B981");
        }
        catch (Exception ex)
        {
            FetchStatusLabel.Text = ex.Message;
            FetchStatusLabel.TextColor = Color.FromArgb("#EF4444");
        }
        FetchStatusLabel.IsVisible = true;
    }

    private void UpdateHostedModeUi()
    {
        if (HostedServicePanel == null || ExternalServicePanel == null) return;
        HostedServicePanel.IsVisible = UseHostedFormsSwitch.IsToggled;
        ExternalServicePanel.IsVisible = !UseHostedFormsSwitch.IsToggled;
    }

    private async void OnPublishHostedFormsClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Hosted delivery unavailable",
            "The bundled PHP backend has been removed and hosted form delivery is being rebuilt. " +
            "Forms stay in download-and-send mode, or use an external HTTPS endpoint.",
            "OK");
    }

    private void OnHasClosingDateToggled(object? sender, ToggledEventArgs e)
    {
        if (ClosingDatePicker != null) ClosingDatePicker.IsEnabled = e.Value;
    }

    private void CaptureDraft()
    {
        if (_draft == null) return;
        _draft.Title = TitleEntry.Text?.Trim() ?? "";
        _draft.Description = DescriptionEditor.Text?.Trim() ?? "";
        _draft.SubmitButtonText = string.IsNullOrWhiteSpace(SubmitButtonEntry.Text) ? "Submit entry" : SubmitButtonEntry.Text.Trim();
        _draft.ConfirmationMessage = ConfirmationEditor.Text?.Trim() ?? "";
        _draft.IsPublished = IsPublishedSwitch.IsToggled;
        _draft.IsClosed = IsClosedSwitch.IsToggled;
        _draft.ClosingDate = HasClosingDateSwitch.IsToggled ? ClosingDatePicker.Date.Date : null;
    }

    private async Task<bool> CanDiscardDraftAsync()
    {
        if (_draft == null) return true;
        CaptureDraft();
        var saved = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _draft.Id);
        if (!_isNewDraft && saved != null && JsonSerializer.Serialize(_draft) == JsonSerializer.Serialize(EntryFormRules.CreateDraft(saved))) return true;
        return await DisplayAlert("Unsaved form changes", "Discard changes to this form? Saved forms and entry records will be left unchanged.", "Discard changes", "Keep editing");
    }

    private bool ValidateDraft()
    {
        if (_draft == null) return false;
        var errors = EntryFormRules.Validate(_draft);
        FormValidationLabel.Text = string.Join("\n", errors);
        FormValidationLabel.TextColor = Color.FromArgb("#B91C1C");
        FormValidationLabel.IsVisible = errors.Count > 0;
        return errors.Count == 0;
    }

    private async void OnPreviewFormClicked(object? sender, EventArgs e)
    {
        CaptureDraft();
        if (_draft == null || !ValidateDraft()) return;
        var preview = new WebView { Source = new HtmlWebViewSource { Html = new WebsiteGenerator(League, League.WebsiteSettings).GenerateEntryFormPreview(_draft) } };
        // Preview cannot launch external navigation or contact links.
        preview.Navigating += (_, args) =>
        {
            if (Uri.TryCreate(args.Url, UriKind.Absolute, out var uri) && uri.Scheme is not ("about" or "data")) args.Cancel = true;
        };
        var close = new Button { Text = "Close preview", BackgroundColor = Color.FromArgb("#334155"), TextColor = Colors.White, CornerRadius = 10 };
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();
        var desktop = new Button { Text = "Desktop", CornerRadius = 10 };
        var mobile = new Button { Text = "Mobile (390px)", CornerRadius = 10 };
        bool mobileMode = false;
        var canvas = new Grid { Padding = 12, BackgroundColor = Color.FromArgb("#E2E8F0") };
        preview.HorizontalOptions = LayoutOptions.Center;
        canvas.Add(preview);
        void ResizePreview()
        {
            if (canvas.Width <= 24) return;
            preview.WidthRequest = Math.Min(mobileMode ? 390 : 1100, canvas.Width - 24);
            desktop.BackgroundColor = Color.FromArgb(mobileMode ? "#334155" : "#2563EB");
            mobile.BackgroundColor = Color.FromArgb(mobileMode ? "#2563EB" : "#334155");
            desktop.TextColor = mobile.TextColor = Colors.White;
        }
        desktop.Clicked += (_, _) => { mobileMode = false; ResizePreview(); };
        mobile.Clicked += (_, _) => { mobileMode = true; ResizePreview(); };
        canvas.SizeChanged += (_, _) => ResizePreview();
        var toolbar = new VerticalStackLayout
        {
            Padding = 16, Spacing = 8, BackgroundColor = Color.FromArgb("#0F172A"),
            Children =
            {
                new Label { Text = _draft.Title, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                new Label { Text = "Interactive preview • Complete, review and test. Nothing is sent, downloaded or saved. Save and publish separately.", TextColor = Color.FromArgb("#CBD5E1") },
                new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { desktop, mobile, close } }
            }
        };
        var layout = new Grid { RowDefinitions = new RowDefinitionCollection { new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(toolbar, 0, 0);
        layout.Add(canvas, 0, 1);
        await Navigation.PushModalAsync(new ContentPage { Title = "Form preview", Content = layout });
    }

    private void OnEntryFilterChanged(object? sender, EventArgs e)
    {
        if (_initializing || !_selectedFormId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form != null) RefreshEntries(form);
    }

    private async void OnSaveServiceSettingsClicked(object sender, EventArgs e)
    {
        var publicUrl = FormServiceUrlEntry.Text?.Trim() ?? "";
        var fetchUrl = FormServiceFetchUrlEntry.Text?.Trim() ?? "";
        if (publicUrl.Length > 0 && EntryFormRules.PublicEndpoint(publicUrl) == null)
        {
            await DisplayAlert("Public endpoint", "Use a public HTTPS POST endpoint without credentials. Move a legacy JSONBin URL to Private collection URL; leave the public endpoint blank for download-and-send entries.", "OK");
            return;
        }
        if (fetchUrl.Length > 0 && (!Uri.TryCreate(fetchUrl, UriKind.Absolute, out var fetchUri) ||
            fetchUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(fetchUri.UserInfo)))
        {
            await DisplayAlert("Private collection", "Use an HTTPS collection URL without embedded credentials.", "OK");
            return;
        }
        League.WebsiteSettings.FormServiceUrl = publicUrl;
        League.WebsiteSettings.FormServiceFetchUrl = fetchUrl;
        League.WebsiteSettings.FormServiceApiToken = FormServiceTokenEntry.Text?.Trim() ?? "";
        League.WebsiteSettings.UseHostedEntryForms = false;
        League.WebsiteSettings.HostedEntryFormsPublished = false;
        DataStore.SaveJsonOnly();
        UpdateDeliveryStatus();
        FetchStatusLabel.Text = "\u2705 Settings saved";
        FetchStatusLabel.TextColor = Color.FromArgb("#10B981");
        FetchStatusLabel.IsVisible = true;
    }

    private async void OnCreateBinClicked(object sender, EventArgs e)
    {
        var apiKey = FormServiceTokenEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await DisplayAlert("API Key Required",
                "Enter your jsonbin.io X-Master-Key first.\n\nSign up free at jsonbin.io, then copy the key from the dashboard.", "OK");
            return;
        }

        CreateBinBtn.IsEnabled = false;
        FetchStatusLabel.Text = "Creating bin...";
        FetchStatusLabel.TextColor = Color.FromArgb("#3B82F6");
        FetchStatusLabel.IsVisible = true;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.jsonbin.io/v3/b");
            request.Headers.TryAddWithoutValidation("X-Master-Key", apiKey);
            request.Content = new StringContent("{\"_init\":true}", System.Text.Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Extract error message from jsonbin.io response body
                var errorMsg = $"{(int)response.StatusCode} {response.ReasonPhrase}";
                try
                {
                    using var errDoc = JsonDocument.Parse(json);
                    if (errDoc.RootElement.TryGetProperty("message", out var msg))
                        errorMsg = msg.GetString() ?? errorMsg;
                }
                catch { /* use status code message */ }

                if (!apiKey.StartsWith("$2a$", StringComparison.Ordinal))
                    errorMsg += "\n\nHint: jsonbin.io keys start with '$2a$'. You may have an old key from another service. Sign up at jsonbin.io and copy your X-Master-Key from the dashboard.";

                FetchStatusLabel.Text = $"\u26A0 {errorMsg}";
                FetchStatusLabel.TextColor = Color.FromArgb("#EF4444");
                return;
            }

            using var doc = JsonDocument.Parse(json);
            var binId = doc.RootElement.GetProperty("metadata").GetProperty("id").GetString();

            var binUrl = $"https://api.jsonbin.io/v3/b/{binId}";
            FormServiceFetchUrlEntry.Text = binUrl;
            League.WebsiteSettings.FormServiceFetchUrl = binUrl;
            League.WebsiteSettings.FormServiceApiToken = apiKey;
            League.WebsiteSettings.UseHostedEntryForms = false;
            League.WebsiteSettings.HostedEntryFormsPublished = false;
            DataStore.SaveJsonOnly();
            UpdateDeliveryStatus();

            FetchStatusLabel.Text = "\u2705 Bin created and saved!";
            FetchStatusLabel.TextColor = Color.FromArgb("#10B981");
        }
        catch (Exception ex)
        {
            FetchStatusLabel.Text = $"\u26A0 Error: {ex.Message}";
            FetchStatusLabel.TextColor = Color.FromArgb("#EF4444");
        }
        finally
        {
            CreateBinBtn.IsEnabled = true;
        }
    }

    private async void OnFetchSubmissionsClicked(object sender, EventArgs e)
    {
        var serviceUrl = League.WebsiteSettings.FormServiceFetchUrl?.Trim() ?? "";
        // Keep access to existing JSONBin records without publishing or silently migrating credentials.
        if (string.IsNullOrWhiteSpace(serviceUrl) && IsJsonBinUrl(League.WebsiteSettings.FormServiceUrl))
            serviceUrl = League.WebsiteSettings.FormServiceUrl.Trim();
        var apiToken = League.WebsiteSettings.FormServiceApiToken?.Trim() ?? "";

        if (!League.WebsiteSettings.UseHostedEntryForms &&
            (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var fetchUri) || fetchUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(fetchUri.UserInfo)))
        {
            await DisplayAlert("Not Configured", "Save a private HTTPS collection URL first, or import an entry JSON file.", "OK");
            return;
        }

        FetchSubmissionsBtn.IsEnabled = false;
        FetchStatusLabel.Text = "Fetching submissions...";
        FetchStatusLabel.TextColor = Color.FromArgb("#3B82F6");
        FetchStatusLabel.IsVisible = true;

        try
        {
            string json;
            {
                var baseUrl = BuildApiUrl(serviceUrl);
                using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                if (!string.IsNullOrWhiteSpace(apiToken))
                {
                    if (IsJsonBinUrl(baseUrl))
                        request.Headers.TryAddWithoutValidation("X-Master-Key", apiToken);
                    else
                        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiToken}");
                }
                using var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
            }
            var (allSubmissions, _) = ParseServiceResponse(json);

            if (allSubmissions.Count == 0)
            {
                FetchStatusLabel.Text = "No submissions found";
                FetchStatusLabel.TextColor = Color.FromArgb("#94A3B8");
                return;
            }

            var imported = await ReviewAndImportSubmissionsAsync(allSubmissions);

            if (imported > 0)
            {
                FetchStatusLabel.Text = $"\u2705 Imported {imported} new submission{(imported != 1 ? "s" : "")} (of {allSubmissions.Count} total)";
                FetchStatusLabel.TextColor = Color.FromArgb("#10B981");
            }
            else if (!_selectedFormId.HasValue)
            {
                FetchStatusLabel.Text = $"\u26A0 Found {allSubmissions.Count} submission{(allSubmissions.Count != 1 ? "s" : "")} but no form is selected";
                FetchStatusLabel.TextColor = Color.FromArgb("#F59E0B");
            }
            else
            {
                FetchStatusLabel.Text = "No entries added. Review was canceled, or entries were duplicates/unmatched.";
                FetchStatusLabel.TextColor = Color.FromArgb("#94A3B8");
            }
        }
        catch (Exception ex)
        {
            FetchStatusLabel.Text = $"\u26A0 Error: {ex.Message}";
            FetchStatusLabel.TextColor = Color.FromArgb("#EF4444");
        }
        finally
        {
            FetchSubmissionsBtn.IsEnabled = true;
        }
    }

    private static string BuildApiUrl(string serviceUrl)
    {
        // jsonbin.io: ensure /latest suffix for reading
        if (IsJsonBinUrl(serviceUrl))
        {
            var url = serviceUrl.TrimEnd('/');
            if (!url.EndsWith("/latest", StringComparison.OrdinalIgnoreCase))
                url += "/latest";
            return url;
        }

        return serviceUrl;
    }

    private static bool IsJsonBinUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Host.Equals("jsonbin.io", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".jsonbin.io", StringComparison.OrdinalIgnoreCase));

    private static string AppendQuery(string url, string queryParams)
    {
        var separator = url.Contains('?') ? '&' : '?';
        return $"{url}{separator}{queryParams}";
    }

    private static (List<WebsiteSubmissionDto> submissions, int lastPage) ParseServiceResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var lastPage = 0;
        if (root.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            throw new JsonException("Expected an entries array or collection response.");

        // Try common response shapes: array, { data: { submissions: [] } }, { submissions: [] }, { data: [] }
        JsonElement array;
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
        }
        else if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
                array = data;
            else if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("submissions", out var subs) && subs.ValueKind == JsonValueKind.Array)
            {
                array = subs;
                // Forminit pagination: { data: { pagination: { lastPage: N } } }
                if (data.TryGetProperty("pagination", out var pag) && pag.TryGetProperty("lastPage", out var lp))
                    lastPage = lp.GetInt32();
            }
            else
                return ([], 0);
        }
        else if (root.TryGetProperty("submissions", out var subs2) && subs2.ValueKind == JsonValueKind.Array)
        {
            array = subs2;
        }
        else if (root.TryGetProperty("record", out var rec) && rec.ValueKind == JsonValueKind.Array)
        {
            array = rec;
        }
        else
        {
            return ([], 0);
        }

        var result = new List<WebsiteSubmissionDto>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) throw new JsonException("Each entry must be an object.");
            var dto = new WebsiteSubmissionDto { Values = new Dictionary<string, string>() };

            // Extract known meta fields
            dto.ExternalId = TryGetString(item, "_id") ?? TryGetString(item, "id")
                ?? TryGetString(item, "hashId") ?? "";
            dto.FormId = TryGetString(item, "_formId") ?? TryGetString(item, "formId") ?? "";
            dto.Name = TryGetString(item, "_entryName") ?? TryGetString(item, "name") ?? "";

            var dateStr = TryGetString(item, "submissionDate") ?? TryGetString(item, "date")
                ?? TryGetString(item, "created_at") ?? TryGetString(item, "submittedAt") ?? "";
            if (DateTime.TryParse(dateStr, out var dt))
                dto.SubmittedAt = dt;

            // Check for nested "values" object (jsonbin.io / localStorage format)
            if (item.TryGetProperty("values", out var valuesObj) && valuesObj.ValueKind == JsonValueKind.Object)
            {
                foreach (var vp in valuesObj.EnumerateObject())
                {
                    dto.Values[vp.Name] = vp.Value.ValueKind == JsonValueKind.String
                        ? (vp.Value.GetString() ?? "")
                        : vp.Value.ToString();
                }
            }
            else
            {
                // Flat format: collect field values from top-level properties, skipping meta keys
                var metaKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "_id", "id", "hashId", "_formId", "formId", "_entryName", "name", "created_at", "submittedAt",
                      "submissionDate", "date", "status", "submissionStatus", "sender", "tracking",
                      "submissionInfo", "files", "values" };

                foreach (var prop in item.EnumerateObject())
                {
                    if (prop.Name.StartsWith("_") || metaKeys.Contains(prop.Name))
                        continue;
                    if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        continue;
                    dto.Values[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? (prop.Value.GetString() ?? "")
                        : prop.Value.ToString();
                }
            }

            result.Add(dto);
        }

        return (result, lastPage);
    }

    private static string? TryGetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Reads a property value as a string regardless of JSON type (string, bool, number).</summary>
    private static string? TryGetAnyString(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString()
            : v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null
            : v.ToString();
    }

    private async Task<int> ReviewAndImportSubmissionsAsync(List<WebsiteSubmissionDto> submissions)
    {
        var candidates = new List<(EntryForm Form, WebsiteSubmissionDto Submission, string Key)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unmatched = 0;
        var duplicates = 0;
        foreach (var sub in submissions)
        {
            var rawId = sub.FormId ?? "";
            if (rawId.StartsWith("form-", StringComparison.OrdinalIgnoreCase)) rawId = rawId[5..];
            var form = Guid.TryParse(rawId, out var id) ? League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == id) : null;
            if (form == null) { unmatched++; continue; }
            var key = EntryFormImportIdentity.GetKey(sub.ExternalId, sub.FormId, sub.Name, sub.SubmittedAt, sub.Values);
            if (form.ImportedExternalIds.Contains(key) || !seen.Add($"{form.Id}:{key}")) { duplicates++; continue; }
            candidates.Add((form, sub, key));
        }
        var summary = string.Join("\n", candidates.GroupBy(c => c.Form.Id).Select(g => $"{g.First().Form.Title}: {g.Count()} new entries"));
        if (candidates.Count == 0)
        {
            await DisplayAlert("Entry review", $"No new entries to add.\n{duplicates} duplicates; {unmatched} missing or unknown form identities.\nUnknown forms are never assigned to the selected form automatically.", "OK");
            return 0;
        }
        if (!await DisplayAlert("Review entry import", $"{summary}\n\n{duplicates} duplicates skipped; {unmatched} unmatched entries excluded.\n\nAdd {candidates.Count} entries as pending? No teams or players will be created or linked.", "Add pending entries", "Cancel")) return 0;
        var imported = 0;

        foreach (var candidate in candidates)
        {
            var (form, sub, key) = candidate;
            if (!League.WebsiteSettings.EntryForms.Contains(form) || form.ImportedExternalIds.Contains(key)) continue;
            var submission = new EntryFormSubmission
            {
                EntryName = sub.Name ?? "",
                Notes = "Imported after review",
                SubmittedDate = sub.SubmittedAt ?? DateTime.Now,
                FieldValues = sub.Values != null ? new Dictionary<string, string>(sub.Values) : new(),
            };
            form.Submissions.Add(submission);
            form.ImportedExternalIds.Add(key);
            imported++;
        }

        if (imported > 0)
        {
            DataStore.SaveJsonOnly();

            if (_selectedFormId.HasValue)
            {
                var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId.Value);
                if (form != null)
                {
                    RefreshEntries(form);
                    RefreshCrossReference(form);
                }
            }

            UpdateRecordsHeader();
        }

        return imported;
    }

    private async void OnImportSubmissionsClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select submissions JSON file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } }
                })
            });

            if (result == null) return;

            var json = await File.ReadAllTextAsync(result.FullPath);

            // Try service format first, then localStorage format
            List<WebsiteSubmissionDto> submissions;
            try
            {
                var (parsed, _) = ParseServiceResponse(json);
                submissions = parsed;
            }
            catch
            {
                submissions = JsonSerializer.Deserialize<List<WebsiteSubmissionDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? [];
            }

            if (submissions.Count == 0)
            {
                await DisplayAlert("No Submissions", "The file contained no submissions.", "OK");
                return;
            }

            var imported = await ReviewAndImportSubmissionsAsync(submissions);

            await DisplayAlert("Import Complete",
                $"Imported {imported} of {submissions.Count} submission{(submissions.Count != 1 ? "s" : "")}.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import Error", $"Failed to import: {ex.Message}", "OK");
        }
    }

    /// <summary>DTO for submissions from both API and file imports.</summary>
    private sealed class WebsiteSubmissionDto
    {
        public string? ExternalId { get; set; }
        public string? FormId { get; set; }
        public string? Name { get; set; }
        public Dictionary<string, string>? Values { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }

    // ── Display items ───────────────────────────────────────────────────

    private sealed class FormDisplayItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string FormType { get; set; } = "";
        public string TypeLabel => FormType switch
        {
            "team-entry" => "Team",
            "competition-entry" => "Comp",
            _ => "Custom"
        };
        public Color TypeBadgeColor => FormType switch
        {
            "team-entry" => Color.FromArgb("#10B981"),
            "competition-entry" => Color.FromArgb("#8B5CF6"),
            _ => Color.FromArgb("#3B82F6")
        };
        public bool IsPublished { get; set; }
        public bool IsClosed { get; set; }
        public string StatusIcon => !IsPublished ? "Draft" : IsClosed ? "Closed" : "Open";
        public string DateLabel { get; set; } = "";
        public string FieldCountLabel { get; set; } = "";

        public static FormDisplayItem FromModel(EntryForm form) => new()
        {
            Id = form.Id,
            Title = form.Title,
            FormType = form.FormType,
            IsPublished = form.IsPublished,
            IsClosed = EntryFormRules.IsClosed(form, DateTime.Today),
            DateLabel = $"Created {form.DateCreated:dd MMM yyyy}",
            FieldCountLabel = $"{form.Fields.Count} field{(form.Fields.Count == 1 ? "" : "s")}"
        };
    }

    private sealed class EntryDisplayItem
    {
        public Guid Id { get; set; }
        public string EntryName { get; set; } = "";
        public string Status { get; set; } = "pending";
        public string StatusLabel => Status switch
        {
            "confirmed" => "Confirmed",
            "rejected" => "Rejected",
            _ => "Pending"
        };
        public Color StatusColor => Status switch
        {
            "confirmed" => Color.FromArgb("#10B981"),
            "rejected" => Color.FromArgb("#EF4444"),
            _ => Color.FromArgb("#F59E0B")
        };
        public string DateLabel { get; set; } = "";
        public string LinkedTeamLabel { get; set; } = "";
    }

    private sealed class CrossRefItem
    {
        public Guid TeamId { get; set; }
        public string TeamName { get; set; } = "";
        public bool HasEntry { get; set; }
        public string StatusIcon => HasEntry ? "\u2705" : "\u274C";
        public string EntryLabel { get; set; } = "";
        public Color EntryLabelColor => HasEntry ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
    }

    // ── Loading ─────────────────────────────────────────────────────────

    private void LoadForms()
    {
        _changingSelection = true;
        _forms.Clear();
        foreach (var form in League.WebsiteSettings.EntryForms.OrderBy(f => f.SortOrder).ThenByDescending(f => f.DateCreated))
            _forms.Add(FormDisplayItem.FromModel(form));
        _changingSelection = false;
        UpdateWorkspaceSummary();
    }

    private void UpdateWorkspaceSummary()
    {
        var forms = League.WebsiteSettings.EntryForms;
        WorkspaceSummaryLabel.Text = $"{forms.Count} saved forms · {forms.Count(f => f.IsPublished && !EntryFormRules.IsClosed(f, DateTime.Today))} open · {forms.Sum(f => f.Submissions.Count(s => s.Status == "pending"))} pending entries";
    }

    // ── Form list events ────────────────────────────────────────────────

    private void OnShowToggled(object? sender, ToggledEventArgs e)
    {
        if (_initializing) return;
        League.WebsiteSettings.ShowEntryForms = e.Value;
        DataStore.SaveJsonOnly();
    }

    private void OnAddTeamEntryClicked(object? sender, EventArgs e)
        => AddFormAndSelect(EntryForm.CreateTeamEntryForm());

    private void OnAddCompEntryClicked(object? sender, EventArgs e)
        => AddFormAndSelect(EntryForm.CreateCompetitionEntryForm());

    private void OnAddCustomClicked(object? sender, EventArgs e)
    {
        AddFormAndSelect(new EntryForm
        {
            Title = "New Entry Form",
            Description = "Please complete the form below.",
        });
    }

    private async void AddFormAndSelect(EntryForm form)
    {
        if (!await CanDiscardDraftAsync()) return;
        form.SortOrder = League.WebsiteSettings.EntryForms.Count;
        form.IsPublished = false;
        LoadForms();
        var item = FormDisplayItem.FromModel(form);
        _forms.Add(item);
        _changingSelection = true;
        FormsCollection.SelectedItem = item;
        _changingSelection = false;
        SelectForm(form.Id, form);
    }

    private async void OnFormSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_changingSelection || e.CurrentSelection.FirstOrDefault() is not FormDisplayItem item || item.Id == _selectedFormId) return;
        _changingSelection = true;
        if (!await CanDiscardDraftAsync())
        {
            FormsCollection.SelectedItem = _forms.FirstOrDefault(f => f.Id == _selectedFormId);
            _changingSelection = false;
            return;
        }
        var oldNewId = _isNewDraft ? _selectedFormId : null;
        SelectForm(item.Id);
        if (oldNewId.HasValue && _forms.FirstOrDefault(f => f.Id == oldNewId) is { } unsaved) _forms.Remove(unsaved);
        _changingSelection = false;
    }

    private async void OnDeleteFormClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        var form = _draft;
        if (form == null) return;

        var saved = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == form.Id);
        if (!await DisplayAlert("Delete Form", $"Delete '{form.Title}'? This will also delete all {saved?.Submissions.Count ?? 0} logged entries.", "Delete", "Cancel"))
            return;

        if (saved != null) League.WebsiteSettings.EntryForms.Remove(saved);
        DataStore.SaveJsonOnly();
        _draft = null;
        _isNewDraft = false;
        _selectedFormId = null;
        _selectedEntryId = null;
        LoadForms();
        EditorForm.IsVisible = false;
        EditorPlaceholder.IsVisible = true;
        DeleteFormBtn.IsEnabled = false;
        DuplicateFormBtn.IsEnabled = false;
        RecordsPanel.IsVisible = false;
        RecordsPlaceholder.IsVisible = true;
        AddEntryBtn.IsEnabled = false;
        RefreshEntriesBtn.IsEnabled = false;
        UpdateRecordsHeader();
    }

    private void OnDuplicateFormClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        CaptureDraft();
        var original = _draft;
        if (original == null) return;

        var copy = new EntryForm
        {
            Title = $"{original.Title} (Copy)",
            Description = original.Description,
            FormType = original.FormType,
            SubmitButtonText = original.SubmitButtonText,
            ConfirmationMessage = original.ConfirmationMessage,
            LogoImageData = original.LogoImageData != null ? (byte[])original.LogoImageData.Clone() : null,
            Fields = original.Fields.Select(f => new EntryFormField
            {
                Label = f.Label,
                FieldType = f.FieldType,
                IsRequired = f.IsRequired,
                Placeholder = f.Placeholder,
                Options = f.Options,
                SortOrder = f.SortOrder,
            }).ToList(),
        };
        AddFormAndSelect(copy);
    }

    // ── Form editor ─────────────────────────────────────────────────────

    private void SelectForm(Guid formId, EntryForm? newDraft = null)
    {
        _selectedFormId = formId;
        _selectedEntryId = null;
        var source = newDraft ?? League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == formId);
        var form = source == null ? null : EntryFormRules.CreateDraft(source);
        if (form == null) return;
        _draft = form;
        _isNewDraft = newDraft != null;
        FormValidationLabel.IsVisible = false;

        TitleEntry.Text = form.Title;
        DescriptionEditor.Text = form.Description;
        SubmitButtonEntry.Text = form.SubmitButtonText;
        ConfirmationEditor.Text = form.ConfirmationMessage;
        IsPublishedSwitch.IsToggled = form.IsPublished;
        IsClosedSwitch.IsToggled = form.IsClosed;
        HasClosingDateSwitch.IsToggled = form.ClosingDate.HasValue;
        ClosingDatePicker.IsEnabled = form.ClosingDate.HasValue;

        if (form.ClosingDate.HasValue)
            ClosingDatePicker.Date = form.ClosingDate.Value;
        else
            ClosingDatePicker.Date = DateTime.Now.AddDays(30);

        UpdateFormLogoPreview(form);

        // Left side: show editor
        EditorPlaceholder.IsVisible = false;
        EditorForm.IsVisible = true;
        DeleteFormBtn.IsEnabled = true;
        DuplicateFormBtn.IsEnabled = true;

        // Right side: show records
        RecordsPlaceholder.IsVisible = false;
        RecordsPanel.IsVisible = true;
        AddEntryBtn.IsEnabled = !_isNewDraft;
        RefreshEntriesBtn.IsEnabled = !_isNewDraft;

        RebuildFieldsPanel(form);
        RefreshEntries(source!);
        RefreshCrossReference(source!);
    }

    private void RebuildFieldsPanel(EntryForm form)
    {
        FieldsPanel.Children.Clear();
        NoFieldsLabel.IsVisible = form.Fields.Count == 0;

        var orderedFields = form.Fields.OrderBy(f => f.SortOrder).ToList();

        for (int idx = 0; idx < orderedFields.Count; idx++)
        {
            var field = orderedFields[idx];
            var capturedField = field;
            var capturedIdx = idx;

            var card = new Border
            {
                Stroke = Color.FromArgb("#E2E8F0"),
                Padding = new Thickness(12),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                BackgroundColor = Colors.White,
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                },
                ColumnSpacing = 8,
                RowDefinitions = new RowDefinitionCollection
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Auto),
                },
                RowSpacing = 6,
            };

            // Up/down reorder buttons
            var moveStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            var upBtn = new Button
            {
                Text = "\u25B2",
                BackgroundColor = Colors.Transparent,
                TextColor = capturedIdx == 0 ? Color.FromArgb("#CBD5E1") : Color.FromArgb("#3B82F6"),
                FontSize = 10,
                Padding = new Thickness(2),
                WidthRequest = 28,
                HeightRequest = 24,
                IsEnabled = capturedIdx > 0,
            };
            upBtn.Clicked += (_, _) =>
            {
                SwapFieldOrder(form, capturedField, -1);
                RebuildFieldsPanel(form);
            };
            var downBtn = new Button
            {
                Text = "\u25BC",
                BackgroundColor = Colors.Transparent,
                TextColor = capturedIdx == orderedFields.Count - 1 ? Color.FromArgb("#CBD5E1") : Color.FromArgb("#3B82F6"),
                FontSize = 10,
                Padding = new Thickness(2),
                WidthRequest = 28,
                HeightRequest = 24,
                IsEnabled = capturedIdx < orderedFields.Count - 1,
            };
            downBtn.Clicked += (_, _) =>
            {
                SwapFieldOrder(form, capturedField, 1);
                RebuildFieldsPanel(form);
            };
            moveStack.Add(upBtn);
            moveStack.Add(downBtn);
            grid.Add(moveStack, 0, 0);

            var labelEntry = new Entry
            {
                Text = field.Label,
                Placeholder = "Field label",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
            };
            labelEntry.TextChanged += (_, args) => capturedField.Label = args.NewTextValue;
            grid.Add(labelEntry, 1, 0);

            var typePicker = new Picker
            {
                FontSize = 12,
                HorizontalOptions = LayoutOptions.Fill,
                ItemsSource = new[] { "text", "email", "phone", "number", "date", "textarea", "select", "checkbox" },
                SelectedItem = field.FieldType,
            };
            typePicker.SelectedIndexChanged += (_, _) =>
            {
                if (typePicker.SelectedItem is string t)
                    capturedField.FieldType = t;
            };

            var requiredSwitch = new Switch { IsToggled = field.IsRequired };
            requiredSwitch.Toggled += (_, args) => capturedField.IsRequired = args.Value;
            var requiredStack = new HorizontalStackLayout { Spacing = 4 };
            requiredStack.Add(new Label { Text = "Req", FontSize = 11, TextColor = Color.FromArgb("#94A3B8"), VerticalOptions = LayoutOptions.Center });
            requiredStack.Add(requiredSwitch);
            var fieldSettings = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
            fieldSettings.Add(typePicker, 0, 0);
            fieldSettings.Add(requiredStack, 1, 0);
            Grid.SetColumnSpan(fieldSettings, 3);
            grid.Add(fieldSettings, 0, 1);

            var deleteBtn = new Button
            {
                Text = "\u2716",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#EF4444"),
                FontSize = 14,
                Padding = new Thickness(4),
                WidthRequest = 36,
                HeightRequest = 36,
            };
            deleteBtn.Clicked += (_, _) =>
            {
                form.Fields.Remove(capturedField);
                RebuildFieldsPanel(form);
            };
            SemanticProperties.SetDescription(deleteBtn, $"Remove field {idx + 1}");
            SemanticProperties.SetDescription(upBtn, $"Move field {idx + 1} up");
            SemanticProperties.SetDescription(downBtn, $"Move field {idx + 1} down");
            grid.Add(deleteBtn, 2, 0);

            var row1 = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 8 };
            var placeholderEntry = new Entry
            {
                Text = field.Placeholder,
                Placeholder = "Placeholder text",
                FontSize = 12,
            };
            placeholderEntry.TextChanged += (_, args) => capturedField.Placeholder = args.NewTextValue;
            row1.Add(placeholderEntry, 0, 0);

            var optionsEntry = new Entry
            {
                Text = field.Options,
                Placeholder = "Options (comma separated)",
                FontSize = 12,
                IsEnabled = field.FieldType == "select",
            };
            optionsEntry.TextChanged += (_, args) => capturedField.Options = args.NewTextValue;
            typePicker.SelectedIndexChanged += (_, _) =>
            {
                optionsEntry.IsEnabled = capturedField.FieldType == "select";
            };
            row1.Add(optionsEntry, 1, 0);

            Grid.SetColumnSpan(row1, 3);
            grid.Add(row1, 0, 2);

            card.Content = grid;
            FieldsPanel.Children.Add(card);
        }
    }

    private static void SwapFieldOrder(EntryForm form, EntryFormField field, int direction)
    {
        var ordered = form.Fields.OrderBy(f => f.SortOrder).ToList();
        for (var index = 0; index < ordered.Count; index++) ordered[index].SortOrder = index;
        var idx = ordered.IndexOf(field);
        var targetIdx = idx + direction;
        if (targetIdx < 0 || targetIdx >= ordered.Count) return;

        var other = ordered[targetIdx];
        (field.SortOrder, other.SortOrder) = (other.SortOrder, field.SortOrder);
    }

    private void UpdateFormLogoPreview(EntryForm form)
    {
        if (form.LogoImageData != null && form.LogoImageData.Length > 0)
        {
            FormLogoPreview.Source = ImageSource.FromStream(() => new MemoryStream(form.LogoImageData));
            FormLogoPreview.IsVisible = true;
            NoLogoLabel.IsVisible = false;
            ClearFormLogoBtn.IsVisible = true;
        }
        else
        {
            FormLogoPreview.Source = null;
            FormLogoPreview.IsVisible = false;
            NoLogoLabel.IsVisible = true;
            ClearFormLogoBtn.IsVisible = false;
        }
    }

    private async void OnUploadFormLogoClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        var form = _draft;
        if (form == null) return;

        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Form Logo",
                FileTypes = FilePickerFileType.Images
            });

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                if (_draft != form) return;
                form.LogoImageData = ms.ToArray();
                UpdateFormLogoPreview(form);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to upload logo: {ex.Message}", "OK");
        }
    }

    private void OnClearFormLogoClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        var form = _draft;
        if (form == null) return;

        form.LogoImageData = null;
        UpdateFormLogoPreview(form);
    }

    private void OnAddFieldClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        var form = _draft;
        if (form == null) return;

        form.Fields.Add(new EntryFormField
        {
            Label = "",
            FieldType = "text",
            IsRequired = false,
            SortOrder = form.Fields.Count == 0 ? 0 : form.Fields.Max(f => f.SortOrder) + 1,
        });
        RebuildFieldsPanel(form);
    }

    private void OnClearClosingDate(object? sender, EventArgs e)
    {
        HasClosingDateSwitch.IsToggled = false;
        ClosingDatePicker.IsEnabled = false;
    }

    private void OnSaveFormClicked(object? sender, EventArgs e)
    {
        CaptureDraft();
        if (_draft == null || !ValidateDraft()) return;
        var form = EntryFormRules.CreateDraft(_draft);
        foreach (var field in form.Fields) field.Label = field.Label.Trim();
        var saved = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == form.Id);
        if (saved != null)
        {
            form.Submissions = saved.Submissions;
            form.ImportedExternalIds = saved.ImportedExternalIds;
            League.WebsiteSettings.EntryForms[League.WebsiteSettings.EntryForms.IndexOf(saved)] = form;
        }
        else if (_isNewDraft) League.WebsiteSettings.EntryForms.Add(form);
        else return;
        DataStore.SaveJsonOnly();
        LoadForms();
        _changingSelection = true;
        var item = _forms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (item != null)
            FormsCollection.SelectedItem = item;
        SelectForm(form.Id);
        _changingSelection = false;
        FormValidationLabel.Text = "Saved locally. Regenerate and publish the website when ready.";
        FormValidationLabel.TextColor = Color.FromArgb("#15803D");
        FormValidationLabel.IsVisible = true;
    }

    // ── Entry (submission) management ───────────────────────────────────

    private void RefreshEntries(EntryForm form)
    {
        _entries.Clear();
        _selectedEntryId = null;
        EntryDetailPanel.IsVisible = false;
        DeleteEntryBtn.IsEnabled = false;

        var teamLookup = GetSeasonTeamLookup();
        var search = EntrySearchBar.Text?.Trim() ?? "";
        var status = EntryFilterPicker.SelectedIndex switch { 1 => "pending", 2 => "confirmed", 3 => "rejected", _ => null };

        foreach (var sub in form.Submissions
            .Where(s => status == null || s.Status == status)
            .Where(s => search.Length == 0 || s.EntryName.Contains(search, StringComparison.OrdinalIgnoreCase) || s.FieldValues.Values.Any(v => v.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(s => s.SubmittedDate))
        {
            var linkedLabel = "";
            if (sub.LinkedTeamId.HasValue && teamLookup.TryGetValue(sub.LinkedTeamId.Value, out var teamName))
                linkedLabel = $"\U0001F517 {teamName}";

            _entries.Add(new EntryDisplayItem
            {
                Id = sub.Id,
                EntryName = string.IsNullOrWhiteSpace(sub.EntryName) ? "(unnamed)" : sub.EntryName,
                Status = sub.Status,
                DateLabel = sub.SubmittedDate.ToString("dd MMM yyyy HH:mm"),
                LinkedTeamLabel = linkedLabel,
            });
        }

        UpdateRecordsHeader();
    }

    private void UpdateRecordsHeader()
    {
        UpdateWorkspaceSummary();
        if (_selectedFormId.HasValue)
        {
            var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
            var count = form?.Submissions.Count ?? 0;
            EntriesHeaderLabel.Text = $"ENTRIES ({_entries.Count}/{count})";
        }
        else
        {
            EntriesHeaderLabel.Text = "RECORDS (0)";
        }
    }

    private void OnRefreshEntriesClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;

        RefreshEntries(form);
        RefreshCrossReference(form);
    }

    private void OnAddEntryClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;

        var submission = new EntryFormSubmission();

        // Pre-populate field values from form fields
        foreach (var field in form.Fields.OrderBy(f => f.SortOrder))
            submission.FieldValues[field.Label] = "";

        form.Submissions.Add(submission);
        DataStore.SaveJsonOnly();
        RefreshEntries(form);
        RefreshCrossReference(form);

        // Select the new entry
        var item = _entries.FirstOrDefault(e => e.Id == submission.Id);
        if (item != null)
            EntriesCollection.SelectedItem = item;
        LoadEntryDetail(form, submission);
    }

    private void OnEntrySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not EntryDisplayItem item) return;
        if (!_selectedFormId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;

        var sub = form.Submissions.FirstOrDefault(s => s.Id == item.Id);
        if (sub == null) return;

        LoadEntryDetail(form, sub);
    }

    private void LoadEntryDetail(EntryForm form, EntryFormSubmission sub)
    {
        _selectedEntryId = sub.Id;
        DeleteEntryBtn.IsEnabled = true;
        EntryDetailPanel.IsVisible = true;

        EntryNameEntry.Text = sub.EntryName;
        EntryNotesEditor.Text = sub.Notes;
        _entryValues = new Dictionary<string, string>(sub.FieldValues);

        // Status picker
        EntryStatusPicker.SelectedItem = sub.Status;

        // Team picker
        var seasonId = SeasonService.Current.CurrentSeasonId;
        var teams = League.Teams?
            .Where(t => t != null && seasonId.HasValue && t.SeasonId == seasonId)
            .OrderBy(t => t.Name ?? "")
            .ToList() ?? [];

        EntryTeamPicker.ItemsSource = teams;
        EntryTeamPicker.ItemDisplayBinding = new Binding("Name");
        EntryTeamPicker.SelectedItem = sub.LinkedTeamId.HasValue
            ? teams.FirstOrDefault(t => t.Id == sub.LinkedTeamId)
            : null;

        // Dynamic field values
        RebuildEntryFieldsPanel(form, sub);
    }

    private void RebuildEntryFieldsPanel(EntryForm form, EntryFormSubmission sub)
    {
        EntryFieldsPanel.Children.Clear();

        EntryFieldsPanel.Children.Add(new Label
        {
            Text = "FIELD VALUES",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#94A3B8"),
            CharacterSpacing = 1.5,
        });

        // Preserve and display values from older versions of a form after field renames/removals.
        var fields = form.Fields.OrderBy(f => f.SortOrder).ToList();
        fields.AddRange(sub.FieldValues.Keys.Where(key => fields.All(f => f.Label != key))
            .Select(key => new EntryFormField { Label = key, Placeholder = "Historical field value" }));
        foreach (var field in fields)
        {
            var capturedLabel = field.Label;
            var currentValue = sub.FieldValues.TryGetValue(capturedLabel, out var val) ? val : "";

            var stack = new VerticalStackLayout { Spacing = 3 };
            stack.Add(new Label
            {
                Text = field.Label,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#475569"),
            });

            var entry = new Entry
            {
                Text = currentValue,
                Placeholder = field.Placeholder,
                FontSize = 12,
            };
            entry.TextChanged += (_, args) => _entryValues[capturedLabel] = args.NewTextValue ?? "";
            stack.Add(entry);
            EntryFieldsPanel.Children.Add(stack);
        }
    }

    private void OnSaveEntryClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue || !_selectedEntryId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;
        var sub = form.Submissions.FirstOrDefault(s => s.Id == _selectedEntryId);
        if (sub == null) return;

        sub.EntryName = EntryNameEntry.Text?.Trim() ?? "";
        sub.Status = EntryStatusPicker.SelectedItem as string ?? "pending";
        sub.Notes = EntryNotesEditor.Text?.Trim() ?? "";
        sub.LinkedTeamId = (EntryTeamPicker.SelectedItem as Team)?.Id;
        sub.FieldValues = new Dictionary<string, string>(_entryValues);

        var selectedId = sub.Id;
        DataStore.SaveJsonOnly();
        RefreshEntries(form);
        RefreshCrossReference(form);

        // Re-select
        var item = _entries.FirstOrDefault(e => e.Id == selectedId);
        if (item != null)
            EntriesCollection.SelectedItem = item;
    }

    private async void OnDeleteEntryClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue || !_selectedEntryId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;
        var sub = form.Submissions.FirstOrDefault(s => s.Id == _selectedEntryId);
        if (sub == null) return;

        var name = string.IsNullOrWhiteSpace(sub.EntryName) ? "this entry" : $"'{sub.EntryName}'";
        if (!await DisplayAlert("Delete Entry", $"Delete {name}?", "Delete", "Cancel"))
            return;

        form.Submissions.Remove(sub);
        _selectedEntryId = null;
        DataStore.SaveJsonOnly();
        RefreshEntries(form);
        RefreshCrossReference(form);
    }

    // ── Team cross-reference ────────────────────────────────────────────

    private void RefreshCrossReference(EntryForm form)
    {
        _crossRefItems.Clear();

        var seasonId = SeasonService.Current.CurrentSeasonId;
        var teams = League.Teams?
            .Where(t => t != null && seasonId.HasValue && t.SeasonId == seasonId)
            .OrderBy(t => t.Name ?? "")
            .ToList() ?? [];

        if (teams.Count == 0)
        {
            CrossRefSummaryLabel.Text = "No teams in current season";
            return;
        }

        // Build set of team IDs that have a linked submission
        var linkedTeamIds = form.Submissions
            .Where(s => s.LinkedTeamId.HasValue && s.Status != "rejected")
            .Select(s => s.LinkedTeamId!.Value)
            .ToHashSet();

        // Only explicit record links count as registrations; names are not identity.
        var entryNamesByTeam = new Dictionary<Guid, string>();
        foreach (var team in teams)
        {
            if (linkedTeamIds.Contains(team.Id))
            {
                var sub = form.Submissions.First(s => s.LinkedTeamId == team.Id && s.Status != "rejected");
                entryNamesByTeam[team.Id] = sub.Status == "confirmed" ? "Confirmed" : "Pending";
            }
        }

        int matched = 0;
        foreach (var team in teams)
        {
            var hasEntry = entryNamesByTeam.ContainsKey(team.Id);
            if (hasEntry) matched++;

            _crossRefItems.Add(new CrossRefItem
            {
                TeamId = team.Id,
                TeamName = team.Name ?? "Unknown",
                HasEntry = hasEntry,
                EntryLabel = hasEntry ? entryNamesByTeam[team.Id] : "No entry",
            });
        }

        CrossRefSummaryLabel.Text = $"{matched} of {teams.Count} teams have entries";
    }

    private Dictionary<Guid, string> GetSeasonTeamLookup()
    {
        var seasonId = SeasonService.Current.CurrentSeasonId;
        return League.Teams?
            .Where(t => t != null && seasonId.HasValue && t.SeasonId == seasonId)
            .ToDictionary(t => t.Id, t => t.Name ?? "Unknown") ?? new Dictionary<Guid, string>();
    }
}
