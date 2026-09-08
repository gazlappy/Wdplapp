using System.Text.Json;
using System.Text.RegularExpressions;

namespace Wdpl2.Services;

/// <summary>Prepares generated pages for local WebView previews without changing publishable files.</summary>
public static class WebsitePreviewContent
{
    public static string Prepare(string html, IReadOnlyDictionary<string, string> files, string? queryString = null)
    {
        if (files.TryGetValue("style.css", out var css))
            html = Regex.Replace(html, @"<link rel=""stylesheet"" href=""style\.css(?:\?v=[^""]*)?"">", _ => $"<style>{css}</style>");

        if (!string.IsNullOrEmpty(queryString))
        {
            var literal = JsonSerializer.Serialize(queryString);
            html = Regex.Replace(html, @"new\s+URLSearchParams\(\s*window\.location\.search\s*\)",
                _ => $"new URLSearchParams({literal})");
        }

        foreach (var fileName in new[] { "players-data.json", "teams-data.json" })
        {
            if (!files.TryGetValue(fileName, out var json)) continue;
            var escapedFile = Regex.Escape(fileName);
            var pattern = @"fetch\(\s*(?:'" + escapedFile + @"'|'" + escapedFile
                + @"\?v='\s*\+\s*cacheBuster)\s*\)\s*\.then\(function\(r\)\s*\{\s*return\s+r\.json\(\);\s*\}\)";
            // Default JSON escaping protects script delimiters as well as quotes and newlines.
            var literal = JsonSerializer.Serialize(json);
            html = Regex.Replace(html, pattern, _ => $"Promise.resolve(JSON.parse({literal}))");
        }

        return html;
    }

    /// <summary>Local page/anchor navigation is allowed; app bridge commands are handled by the caller first.</summary>
    public static bool ShouldBlockNavigation(string url)
    {
        if (url.StartsWith("//", StringComparison.Ordinal) || url.StartsWith(@"\\", StringComparison.Ordinal)) return true;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return uri.IsUnc || (!uri.IsFile && uri.Scheme is not ("about" or "data"));
    }
}
