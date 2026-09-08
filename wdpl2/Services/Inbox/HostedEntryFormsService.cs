using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services.Inbox;

/// <summary>Authenticated entry-form operations against the configured Web Inbox backend.</summary>
public sealed class HostedEntryFormsService : IDisposable
{
    private readonly HttpClient _http;
    private readonly AuthenticationHeaderValue _authorization;
    public Uri BaseUri { get; }
    public Uri SubmissionUri => new(BaseUri, "entry-forms/submit.php");
    public Uri CollectionUri => new(BaseUri, "admin/entry-form-submissions.php");

    public HostedEntryFormsService(WebInboxSettings settings, HttpMessageHandler? handler = null)
    {
        BaseUri = ValidateBaseUri(settings.BaseUrl);
        if (settings.IgnoreSslErrors)
            throw new InvalidOperationException("Disable Ignore SSL errors in Web Inbox settings. Hosted entry forms require a valid HTTPS certificate.");
        if (string.IsNullOrWhiteSpace(settings.AdminUser) || string.IsNullOrEmpty(settings.AdminPassword) || settings.AdminUser.Contains(':'))
            throw new InvalidOperationException("Configure an existing admin username and password in Web Inbox settings first.");
        _authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.AdminUser}:{settings.AdminPassword}")));
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public static Uri ValidateBaseUri(string value) => ValidateHttpsUri(value,
        "Set a valid HTTPS API base URL without credentials, a query or fragment in Web Inbox settings.");

    public static Uri ValidateWebsiteUri(string value) => ValidateHttpsUri(value,
        "Set the public Website URL in Online Forms delivery settings (or website settings). Use the HTTPS address visitors open, without credentials, a query or fragment; this is not the Web Inbox API URL.");

    private static Uri ValidateHttpsUri(string value, string error)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException(error);
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }

    public static string BuildDefinitionPayload(WebsiteSettings settings)
    {
        var website = ValidateWebsiteUri(settings.WebsiteUrl);
        var forms = settings.ShowEntryForms ? settings.EntryForms.Where(f => f.IsPublished).ToList() : [];
        if (forms.Count > 100 || forms.Select(f => f.Id).Distinct().Count() != forms.Count)
            throw new InvalidOperationException("Publish at most 100 forms with unique identities.");
        foreach (var form in forms)
        {
            var errors = EntryFormRules.Validate(form);
            if (errors.Count > 0) throw new InvalidOperationException($"{form.Title}: {string.Join(" ", errors)}");
            if (form.Title.Length > 200 || form.Fields.Count > 100 || form.Fields.Any(f => f.Label.Length > 200))
                throw new InvalidOperationException("Hosted forms allow titles/labels up to 200 characters and at most 100 fields per form.");
            foreach (var field in form.Fields.Where(f => f.FieldType == "select"))
            {
                var options = field.Options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (options.Length is 0 or > 100 || options.Any(o => o.Length > 500) || options.Distinct(StringComparer.Ordinal).Count() != options.Length)
                    throw new InvalidOperationException($"{form.Title}: dropdowns need 1–100 distinct options, each at most 500 characters.");
            }
        }
        var payload = JsonSerializer.Serialize(new
        {
            protocol = 1,
            origin = website.GetLeftPart(UriPartial.Authority),
            timeZone = "Europe/London",
            forms = forms.Select(form => new
            {
                id = $"form-{form.Id:N}", title = form.Title, closed = form.IsClosed,
                closingDate = form.ClosingDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                fields = form.Fields.OrderBy(f => f.SortOrder).Select(field => new
                {
                    label = field.Label, type = field.FieldType, required = field.IsRequired,
                    options = field.FieldType == "select"
                        ? field.Options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : Array.Empty<string>()
                })
            })
        });
        if (Encoding.UTF8.GetByteCount(payload) > 1_048_576)
            throw new InvalidOperationException("The published form definitions exceed the 1 MB limit.");
        return payload;
    }

    public async Task PublishAsync(string payload, CancellationToken ct = default)
    {
        using var definition = JsonDocument.Parse(payload);
        var expected = definition.RootElement.GetProperty("forms").GetArrayLength();
        using var response = await SendAsync(HttpMethod.Post, new Uri(BaseUri, "admin/entry-form-definitions.php"), payload, ct);
        var root = response.RootElement;
        if (!root.TryGetProperty("protocol", out var protocol) || protocol.GetInt32() != 1 ||
            !root.TryGetProperty("published", out var published) || published.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("count", out var count) || count.GetInt32() != expected)
            throw new InvalidOperationException("The backend did not acknowledge the complete form definition set. Publishing is unconfirmed; retry before enabling online delivery.");
    }

    public async Task<string> FetchAsync(WebsiteSettings settings, CancellationToken ct = default)
    {
        if (!EntryFormRules.IsHostedDeliveryReady(settings))
            throw new InvalidOperationException("Own-hosting preference is saved, but delivery is not confirmed. Publish saved forms to our hosting before fetching submissions.");
        // Never attach admin credentials to an arbitrary external collection URL.
        if (!string.Equals(settings.FormServiceFetchUrl, CollectionUri.AbsoluteUri, StringComparison.Ordinal) ||
            !string.Equals(settings.FormServiceUrl, SubmissionUri.AbsoluteUri, StringComparison.Ordinal))
            throw new InvalidOperationException("Hosted URLs no longer match Web Inbox settings. Publish the saved forms to the intended backend again before fetching.");
        var entries = new List<JsonElement>();
        long after = 0;
        long? through = null;
        long bytes = 0;
        while (true)
        {
            var url = new Uri(CollectionUri.AbsoluteUri + $"?after={after}" + (through.HasValue ? $"&through={through.Value}" : ""));
            using var response = await SendAsync(HttpMethod.Get, url, null, ct);
            var root = response.RootElement;
            if (root.GetProperty("protocol").GetInt32() != 1) throw new JsonException("Unsupported collection protocol.");
            var upper = root.GetProperty("through").GetInt64();
            if (upper < after || (through.HasValue && upper != through.Value)) throw new JsonException("The collection snapshot changed; no entries were imported.");
            through = upper;
            var page = root.GetProperty("submissions");
            if (page.ValueKind != JsonValueKind.Array || page.GetArrayLength() > 100) throw new JsonException("Invalid collection page.");
            foreach (var entry in page.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid entry in collection.");
                bytes += Encoding.UTF8.GetByteCount(entry.GetRawText());
                if (entries.Count >= 50_000 || bytes > 67_108_864) throw new InvalidOperationException("Collection exceeds the review limit. Arrange a smaller private export; no partial import was performed.");
                entries.Add(entry.Clone());
            }
            var next = root.GetProperty("nextAfter");
            if (next.ValueKind == JsonValueKind.Null) break;
            var cursor = next.GetInt64();
            if (page.GetArrayLength() == 0 || cursor <= after || cursor > upper) throw new JsonException("Invalid or repeated collection cursor; no entries were imported.");
            after = cursor;
        }
        return JsonSerializer.Serialize(entries);
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, Uri uri, string? payload, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = _authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (payload != null) request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Hosted forms returned HTTP {(int)response.StatusCode}. Check backend deployment, admin access and hosting configuration; redirects are not followed.");
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            throw new JsonException("The backend returned a non-JSON response. Verify the PHP endpoint, not a login or HTML page.");
        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var buffer = new MemoryStream();
        var chunk = new byte[32768];
        int read;
        while ((read = await stream.ReadAsync(chunk, timeout.Token)) != 0)
        {
            if (buffer.Length + read > 8_388_608) throw new InvalidOperationException("The backend response exceeds the 8 MB page limit.");
            buffer.Write(chunk, 0, read);
        }
        return JsonDocument.Parse(buffer.ToArray());
    }

    public void Dispose() => _http.Dispose();
}
