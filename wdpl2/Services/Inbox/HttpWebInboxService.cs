using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Wdpl2.Services.Inbox;

/// <summary>
/// Talks to the PHP admin endpoints on wdpl.uk using HTTP Basic auth.
/// </summary>
public sealed class HttpWebInboxService : IWebInboxService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<IReadOnlyList<WebSubmission>> GetPendingAsync(CancellationToken ct = default)
    {
        var settings = await WebInboxSettings.LoadAsync();
        using var req = new HttpRequestMessage(HttpMethod.Get, BuildUrl(settings, "admin/pending.php"));
        AddAuth(req, settings);

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        await EnsureOkAsync(resp).ConfigureAwait(false);

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var parsed = await JsonSerializer.DeserializeAsync<PendingResponse>(stream, cancellationToken: ct)
                       .ConfigureAwait(false);
        return parsed?.Items ?? new List<WebSubmission>();
    }

    public async Task MarkProcessedAsync(IEnumerable<long> ids, string? by = null, string? notes = null, CancellationToken ct = default)
    {
        var idList = ids?.ToList() ?? new List<long>();
        if (idList.Count == 0) return;

        var settings = await WebInboxSettings.LoadAsync();
        var body = JsonSerializer.Serialize(new
        {
            ids   = idList,
            by    = by    ?? settings.AdminUser,
            notes = notes ?? ""
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUrl(settings, "admin/mark-processed.php"))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        AddAuth(req, settings);

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        await EnsureOkAsync(resp).ConfigureAwait(false);
    }

    private static Uri BuildUrl(WebInboxSettings s, string relative)
    {
        var baseUrl = string.IsNullOrWhiteSpace(s.BaseUrl) ? WebInboxSettings.DefaultBaseUrl : s.BaseUrl;
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        return new Uri(new Uri(baseUrl), relative);
    }

    private static void AddAuth(HttpRequestMessage req, WebInboxSettings s)
    {
        if (string.IsNullOrEmpty(s.AdminUser)) return;
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{s.AdminUser}:{s.AdminPassword}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private static async Task EnsureOkAsync(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = "";
        try { body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false); } catch { }
        throw new HttpRequestException($"{(int)resp.StatusCode} {resp.ReasonPhrase}: {body}");
    }
}
