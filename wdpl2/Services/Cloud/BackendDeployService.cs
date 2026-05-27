using System.Text;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Services.Cloud;

/// <summary>
/// Pushes the PHP/HTML backend bundled under <c>web-backend/</c> (shipped as MAUI
/// assets with <c>LogicalName="backend/..."</c>) up to the same FTP target the
/// Website Builder uses. Lets the admin redeploy the captain portal / admin pages
/// without touching cPanel.
/// </summary>
public sealed class BackendDeployService
{
    /// <summary>
    /// Static list of backend files bundled into the app. Keep in sync with the
    /// <c>web-backend/</c> folder contents — the build also packs them as
    /// <c>backend/&lt;relative&gt;</c> MAUI assets.
    /// </summary>
    public static readonly IReadOnlyList<string> BundledFiles = new[]
    {
        "api/_captain.php",
        "api/_admin.php",
        "api/_db.php",
        "api/submit.php",
        "api/admin/.htaccess",
        "api/admin/captains.html",
        "api/admin/captains.php",
        "api/admin/diag.php",
        "api/admin/login.php",
        "api/admin/logout.php",
        "api/admin/mark-processed.php",
        "api/admin/pending.php",
        "api/admin/publish-league.php",
        "api/admin/reopen-fixture.php",
        "api/admin/scorecards.php",
        "api/admin/teams.php",
        "api/admin/users.php",
        "api/admin/whoami.php",
        "api/admin/index.html",
        "api/captain/login.php",
        "api/captain/logout.php",
        "api/captain/me.php",
        "api/captain/submit-result.php",
        "api/captain/history.php",
        "api/captain/scorecard.php",
        "api/captain/finalize.php",
        "api/captain/roster.php",
        "api/captain/availability.php",
        "api/captain/fixtures.php",
        "api/captain/account.php",
        "api/captain/messages.php",
        "captain/index.html",
        "captain/manifest.webmanifest",
        "captain/sw.js",
    };

    public sealed class DeployResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
        public int FilesUploaded { get; init; }
        public int FilesSkipped  { get; init; }
        public List<string> Failures { get; init; } = new();
    }

    /// <summary>
    /// Reads the bundled backend files and returns them as a
    /// dictionary keyed by their target relative path (e.g. "api/captain/login.php").
    /// Also records which files were missing in <paramref name="missing"/>.
    /// </summary>
    public async Task<Dictionary<string, string>> LoadBundledAsync(List<string>? missing = null)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in BundledFiles)
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("backend/" + rel);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                map[rel] = await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                missing?.Add($"{rel} ({ex.GetType().Name}: {ex.Message})");
            }
        }
        return map;
    }

    /// <summary>
    /// Pushes all bundled backend files to the website's FTP host, placing them
    /// under <c>{RemotePath}/{relative path}</c>. Returns a summary string.
    /// </summary>
    public async Task<DeployResult> DeployAsync(
        WebsiteSettings settings,
        IProgress<UploadProgress>? progress = null,
        IEnumerable<string>? onlyFiles = null)
    {
        var missing = new List<string>();
        var files = await LoadBundledAsync(missing);

        // Inject the DB credential sidecar (api/_db.config.php) so the deploy
        // configures the server without needing _db.php itself to carry secrets.
        // Only emitted when the user has actually filled in DB credentials.
        if (!string.IsNullOrWhiteSpace(settings.BackendDbName) &&
            !string.IsNullOrWhiteSpace(settings.BackendDbUser))
        {
            files["api/_db.config.php"] = BuildDbConfigPhp(settings);
        }

        if (onlyFiles is not null)
        {
            var filter = new HashSet<string>(onlyFiles, StringComparer.OrdinalIgnoreCase);
            files = files.Where(kv => filter.Contains(kv.Key))
                         .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        if (files.Count == 0)
            return new DeployResult { Success = false, Message = "No bundled backend files were found in the app package." };

        if (string.IsNullOrWhiteSpace(settings.FtpHost) ||
            string.IsNullOrWhiteSpace(settings.FtpUsername))
        {
            return new DeployResult
            {
                Success = false,
                Message = "FTP host/username are not configured. Set them in Website Builder → Deployment first."
            };
        }

        var ftp = new FtpUploadService(settings);

        // Backend deploys to the site root (or whatever BackendRemotePath says),
        // NOT to WebsiteSettings.RemotePath which usually points at a sub-folder
        // for the generated league site (e.g. /public_html/NewPool).
        var backendRoot = settings.GetEffectiveBackendRemotePath();

        int uploaded = 0;
        var failures = new List<string>();
        int idx = 0;

        foreach (var kv in files)
        {
            idx++;
            var rel = kv.Key;
            progress?.Report(new UploadProgress
            {
                CurrentFile    = rel,
                FilesCompleted = idx - 1,
                TotalFiles     = files.Count,
                Status         = $"Uploading {rel}..."
            });

            // FtpUploadService.UploadWebsiteAsync writes everything into the
            // remote root - here we want to preserve sub-paths, so feed one file
            // at a time with its sub-path appended.
            var subDir  = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
            var name    = Path.GetFileName(rel);
            var single  = new Dictionary<string, string> { [name] = kv.Value };

            // Temporarily widen the settings remote path for this file.
            var originalRemote = settings.RemotePath;
            try
            {
                var combined = backendRoot.TrimEnd('/') + "/" + subDir;
                settings.RemotePath = combined.Replace("//", "/").TrimEnd('/');
                if (string.IsNullOrEmpty(settings.RemotePath)) settings.RemotePath = "/";
                var (ok, msg) = await ftp.UploadWebsiteAsync(single);
                if (ok) uploaded++;
                else    failures.Add($"{rel}: {msg}");
            }
            catch (Exception ex)
            {
                failures.Add($"{rel}: {ex.Message}");
            }
            finally
            {
                settings.RemotePath = originalRemote;
            }
        }

        // NOTE: do NOT post a final progress.Report here. Progress<T> callbacks
        // are dispatched asynchronously via the sync context, so a "final"
        // report would arrive AFTER the caller's SetStatus(result.Message)
        // and silently overwrite the diagnostic output (size+hash+missing).

        var sb = new StringBuilder();
        sb.AppendLine($"Backend deploy: {uploaded}/{files.Count} file(s) uploaded to {settings.FtpHost}{backendRoot}");

        // Spot-check: report size + hash of captain/index.html so you can tell
        // whether the bundled HTML is the new redesign or an old build.
        if (files.TryGetValue("captain/index.html", out var capHtml))
        {
            var bytes = Encoding.UTF8.GetByteCount(capHtml);
            var hash  = FtpUploadService.ComputeHash(capHtml);
            sb.AppendLine($"  captain/index.html: {bytes:N0} bytes, sha256 {hash[..12]}...");
        }

        if (missing.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Missing from bundle ({missing.Count}) — REBUILD the app to pick up edits:");
            foreach (var m in missing) sb.AppendLine("  - " + m);
        }

        if (failures.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Failures:");
            foreach (var f in failures) sb.AppendLine("  - " + f);
        }

        return new DeployResult
        {
            Success       = failures.Count == 0,
            Message       = sb.ToString(),
            FilesUploaded = uploaded,
            FilesSkipped  = files.Count - uploaded - failures.Count,
            Failures      = failures,
        };
    }

    /// <summary>
    /// Build the contents of <c>api/_db.config.php</c> — a sidecar file that
    /// <c>_db.php</c> includes to obtain DB credentials. Lets backend deploys
    /// configure DB access without keeping passwords in the bundled source.
    /// </summary>
    private static string BuildDbConfigPhp(WebsiteSettings s)
    {
        static string Esc(string v) => (v ?? "").Replace("\\", "\\\\").Replace("'", "\\'");
        var sb = new StringBuilder();
        sb.AppendLine("<?php");
        sb.AppendLine("// AUTO-GENERATED by the WDPL admin app on backend deploy.");
        sb.AppendLine("// Edits here are overwritten on the next deploy.");
        sb.AppendLine($"define('DB_HOST', '{Esc(s.BackendDbHost)}');");
        sb.AppendLine($"define('DB_NAME', '{Esc(s.BackendDbName)}');");
        sb.AppendLine($"define('DB_USER', '{Esc(s.BackendDbUser)}');");
        sb.AppendLine($"define('DB_PASS', '{Esc(s.BackendDbPassword)}');");
        return sb.ToString();
    }
}
