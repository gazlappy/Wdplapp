using System.Net;
using System.Text;
using System.Text.Json;

namespace Wdpl2.Services;

/// <summary>
/// Lightweight local HTTP listener that receives form submissions from the
/// generated website via fetch() POST. Runs on a background thread.
/// </summary>
public sealed class FormSubmissionListener : IDisposable
{
    public const int Port = 19532;
    private static readonly string Prefix = $"http://localhost:{Port}/";

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Raised on a background thread when a valid submission arrives.
    /// </summary>
    public event Action<SubmissionReceivedArgs>? SubmissionReceived;

    public bool IsListening => _listener?.IsListening == true;

    public void Start()
    {
        if (_disposed || _listener?.IsListening == true) return;

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add(Prefix);

        try
        {
            _listener.Start();
            _ = ListenLoopAsync(_cts.Token);
        }
        catch
        {
            // Port in use or permission issue — silently degrade
            _listener = null;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        _listener = null;
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var context = await _listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleRequest(context), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            catch { /* ignore transient errors */ }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var response = context.Response;

        // CORS headers for all responses
        response.Headers.Set("Access-Control-Allow-Origin", "*");
        response.Headers.Set("Access-Control-Allow-Methods", "POST, OPTIONS");
        response.Headers.Set("Access-Control-Allow-Headers", "Content-Type");

        try
        {
            // Handle CORS preflight
            if (context.Request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            if (context.Request.HttpMethod != "POST" ||
                !context.Request.Url!.AbsolutePath.Equals("/api/entry-submit", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 404;
                response.Close();
                return;
            }

            // Read body
            string body;
            using (var reader = new System.IO.StreamReader(context.Request.InputStream, Encoding.UTF8))
                body = reader.ReadToEnd();

            if (string.IsNullOrWhiteSpace(body))
            {
                response.StatusCode = 400;
                response.Close();
                return;
            }

            // Parse JSON
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var formId = root.TryGetProperty("formId", out var fid) ? fid.GetString() ?? "" : "";
            var entryName = root.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";

            var fieldValues = new Dictionary<string, string>();
            if (root.TryGetProperty("values", out var vp) && vp.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in vp.EnumerateObject())
                    fieldValues[prop.Name] = prop.Value.GetString() ?? "";
            }

            SubmissionReceived?.Invoke(new SubmissionReceivedArgs(formId, entryName, fieldValues));

            // Respond OK
            response.StatusCode = 200;
            response.ContentType = "application/json";
            var responseBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
            response.ContentLength64 = responseBytes.Length;
            response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
        }
        catch
        {
            response.StatusCode = 500;
        }
        finally
        {
            try { response.Close(); } catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts?.Dispose();
    }
}

public sealed class SubmissionReceivedArgs(string formId, string entryName, Dictionary<string, string> fieldValues)
{
    /// <summary>Form ID from the website (format: "form-{guid:N}")</summary>
    public string FormId { get; } = formId;
    public string EntryName { get; } = entryName;
    public Dictionary<string, string> FieldValues { get; } = fieldValues;
}
