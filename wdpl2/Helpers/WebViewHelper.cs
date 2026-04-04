namespace Wdpl2.Helpers;

/// <summary>
/// Helper for safely loading HTML into a WebView, avoiding the WebView2
/// NavigateToString size limit (~2 MB) by falling back to a temp file.
/// </summary>
internal static class WebViewHelper
{
    // WebView2 NavigateToString fails above ~2 MB; use a safe threshold.
    private const int MaxInlineLength = 1_500_000;

    /// <summary>
    /// Sets the <see cref="WebView.Source"/> to the given HTML string.
    /// For small strings the content is inlined; for large strings it is
    /// written to a temp file and loaded via a file URL.
    /// </summary>
    public static void LoadHtml(WebView webView, string html)
    {
        if (html.Length <= MaxInlineLength)
        {
            webView.Source = new HtmlWebViewSource { Html = html };
            return;
        }

        // Write to a temp file and navigate to it
        var tempDir = Path.Combine(FileSystem.CacheDirectory, "webview_preview");
        Directory.CreateDirectory(tempDir);

        var filePath = Path.Combine(tempDir, $"preview_{Guid.NewGuid():N}.html");
        File.WriteAllText(filePath, html);

        webView.Source = new UrlWebViewSource { Url = filePath };

        // Clean up old temp files (keep only the most recent)
        CleanupOldFiles(tempDir, filePath);
    }

    private static void CleanupOldFiles(string directory, string keepPath)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "preview_*.html"))
            {
                if (!string.Equals(file, keepPath, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch { }
    }
}
