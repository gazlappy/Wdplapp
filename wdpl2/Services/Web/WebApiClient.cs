using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Wdpl2.Services.Web;

/// <summary>A backend failure the caller is expected to show the user.</summary>
public sealed class WebApiException : Exception
{
    public WebApiException(string code, string message, HttpStatusCode? status = null)
        : base(message)
    {
        Code = code;
        Status = status;
    }

    /// <summary>Machine-readable code from the backend, e.g. <c>not_configured</c>.</summary>
    public string Code { get; }

    public HttpStatusCode? Status { get; }
}

/// <summary>
/// The single way this app talks to the backend.
/// </summary>
/// <remarks>
/// Every call goes to <c>index.php?m={module}&amp;a={action}</c> and unwraps the
/// one response envelope the front controller produces, so error handling is
/// identical everywhere instead of being reinvented per endpoint.
/// </remarks>
public sealed class WebApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly WebConnection _connection;
    private readonly Uri _endpoint;

    public WebApiClient(WebConnection connection, HttpMessageHandler? handler = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _endpoint = connection.ResolveEndpoint();

        _http = handler is null
            ? new HttpClient(new HttpClientHandler
              {
                  // A redirect would re-send the Authorization header to whatever
                  // host the server names. Refuse and surface it instead.
                  AllowAutoRedirect = false,
              })
            : new HttpClient(handler);

        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Calls an action that needs no administrator credentials.</summary>
    public Task<JsonElement> PublicAsync(string module, string action, object? body = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(module, action, body, authenticate: false, cancellationToken);

    /// <summary>Calls an action as the administrator.</summary>
    public Task<JsonElement> AdminAsync(string module, string action, object? body = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(module, action, body, authenticate: true, cancellationToken);

    private async Task<JsonElement> SendAsync(string module, string action, object? body,
        bool authenticate, CancellationToken cancellationToken)
    {
        var uri = new Uri(_endpoint + $"?m={Uri.EscapeDataString(module)}&a={Uri.EscapeDataString(action)}");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);

        request.Content = new StringContent(
            body is null ? "{}" : JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8,
            "application/json");

        if (authenticate)
        {
            if (string.IsNullOrWhiteSpace(_connection.AdminUser) || string.IsNullOrEmpty(_connection.AdminPassword))
                throw new WebApiException("no_credentials", "Enter the backend administrator username and password first.");

            var raw = Encoding.UTF8.GetBytes($"{_connection.AdminUser}:{_connection.AdminPassword}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WebApiException("timeout", "The backend did not respond within 30 seconds.");
        }
        catch (HttpRequestException ex)
        {
            throw new WebApiException("unreachable", $"Could not reach the backend: {ex.Message}");
        }

        using (response)
        {
            if ((int)response.StatusCode is >= 300 and < 400)
                throw new WebApiException("redirected",
                    "The backend redirected the request. Check the API URL points directly at the api/ folder.", response.StatusCode);

            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return Unwrap(text, response.StatusCode);
        }
    }

    /// <summary>
    /// Unwraps <c>{ok:true,data:...}</c> / <c>{ok:false,error:{...}}</c>.
    /// Anything that is not that envelope means we did not reach the front
    /// controller - usually a login page or an error page from the host.
    /// </summary>
    private static JsonElement Unwrap(string text, HttpStatusCode status)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            throw new WebApiException("not_json",
                $"The backend returned {(int)status} but not JSON. Check the API URL and that PHP is executing.", status);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("ok", out var ok))
                throw new WebApiException("bad_envelope", "The backend returned an unexpected response shape.", status);

            if (ok.ValueKind == JsonValueKind.False)
            {
                var code = "server_error";
                var message = "The backend reported an error.";
                if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                {
                    if (error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                        code = c.GetString() ?? code;
                    if (error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                        message = m.GetString() ?? message;
                }
                throw new WebApiException(code, message, status);
            }

            // Clone so the value outlives the JsonDocument.
            return root.TryGetProperty("data", out var data) ? data.Clone() : default;
        }
    }

    public void Dispose() => _http.Dispose();
}
