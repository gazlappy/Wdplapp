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
        "api/_db.php",
        "api/submit.php",
        "api/admin/.htaccess",
        "api/admin/captains.html",
        "api/admin/captains.php",
        "api/admin/diag.php",
        "api/admin/mark-processed.php",
        "api/admin/pending.php",
        "api/admin/publish-league.php",
        "api/captain/login.php",
        "api/captain/logout.php",
        "api/captain/me.php",
        "api/captain/submit-result.php",
        "captain/index.html",
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
    /// </summary>
    public async Task<Dictionary<string, string>> LoadBundledAsync()
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
            catch
            {
                // File not packaged on this target - skip silently; report via Failures later.
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
        var files = await LoadBundledAsync();
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
                var combined = originalRemote.TrimEnd('/') + "/" + subDir;
                settings.RemotePath = combined.Replace("//", "/");
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

        progress?.Report(new UploadProgress
        {
            FilesCompleted = files.Count,
            TotalFiles     = files.Count,
            Status         = failures.Count == 0 ? "Backend deployed." : $"Completed with {failures.Count} failure(s)."
        });

        var sb = new StringBuilder();
        sb.AppendLine($"Backend deploy: {uploaded}/{files.Count} file(s) uploaded to {settings.FtpHost}{settings.RemotePath}");
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
}
