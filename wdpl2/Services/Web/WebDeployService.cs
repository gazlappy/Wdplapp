using System.Text;
using Microsoft.Maui.Storage;
using Wdpl2.Models;

namespace Wdpl2.Services.Web;

public sealed class WebDeployResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public int FilesUploaded { get; init; }
    public List<string> Problems { get; init; } = new();
}

/// <summary>
/// Uploads the bundled backend and writes its configuration sidecar.
/// </summary>
/// <remarks>
/// Reuses <see cref="FtpUploadService"/> and the website's existing FTP settings
/// rather than carrying a second upload implementation.
/// </remarks>
public sealed class WebDeployService
{
    private readonly WebModuleRegistry _registry;

    public WebDeployService(WebModuleRegistry registry) => _registry = registry;

    /// <summary>
    /// Reads every registered module's server files out of the app package.
    /// A file that will not load is a packaging fault, so it is reported rather
    /// than skipped - a partially uploaded backend is a broken backend.
    /// </summary>
    public async Task<(Dictionary<string, string> files, List<string> missing)> LoadBundledAsync()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();

        foreach (var file in _registry.DeployMap())
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("backend/" + file.Source);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                files[file.Destination] = await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                missing.Add($"{file} ({ex.GetType().Name})");
            }
        }

        return (files, missing);
    }

    /// <summary>
    /// Builds <c>api/config.php</c>. Written separately from the code deploy so a
    /// later code-only redeploy cannot overwrite working credentials.
    /// </summary>
    public static string BuildConfigPhp(WebsiteSettings settings, WebConnection connection, string adminPasswordHash, string pepper)
    {
        static string Php(string? value) =>
            "'" + (value ?? "").Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        var origin = "";
        if (Uri.TryCreate(settings.WebsiteUrl?.Trim() ?? "", UriKind.Absolute, out var website))
            origin = website.GetLeftPart(UriPartial.Authority);

        var builder = new StringBuilder();
        builder.AppendLine("<?php");
        builder.AppendLine("// Written by WDPL -> Web Control -> Connection & Deploy.");
        builder.AppendLine("// Contains credentials. Do not commit or share.");
        builder.AppendLine("return [");
        builder.AppendLine($"    'db_host' => {Php(settings.BackendDbHost)},");
        builder.AppendLine($"    'db_name' => {Php(settings.BackendDbName)},");
        builder.AppendLine($"    'db_user' => {Php(settings.BackendDbUser)},");
        builder.AppendLine($"    'db_password' => {Php(settings.BackendDbPassword)},");
        builder.AppendLine($"    'admin_user' => {Php(connection.AdminUser)},");
        builder.AppendLine($"    'admin_password_hash' => {Php(adminPasswordHash)},");
        builder.AppendLine($"    'pepper' => {Php(pepper)},");
        builder.AppendLine($"    'allowed_origin' => {Php(origin)},");
        builder.AppendLine("    'behind_tls_proxy' => false,");
        builder.AppendLine("    'allow_insecure' => false,");
        builder.AppendLine("    'max_body_bytes' => 262144,");
        builder.AppendLine("];");
        return builder.ToString();
    }

    /// <summary>
    /// Uploads the backend. Pass <paramref name="configPhp"/> only when
    /// deploying configuration too; otherwise the server keeps the config it has.
    /// </summary>
    public async Task<WebDeployResult> DeployAsync(
        WebsiteSettings settings,
        string? configPhp = null,
        IProgress<UploadProgress>? progress = null)
    {
        var (files, missing) = await LoadBundledAsync();

        if (missing.Count > 0)
            return new WebDeployResult
            {
                Success = false,
                Message = "The backend package is incomplete, so nothing was uploaded. Rebuild the app and try again.",
                Problems = missing,
            };

        if (files.Count == 0)
            return new WebDeployResult { Success = false, Message = "No backend files were found in the app package." };

        if (string.IsNullOrWhiteSpace(settings.FtpHost) || string.IsNullOrWhiteSpace(settings.FtpUsername))
            return new WebDeployResult
            {
                Success = false,
                Message = "Set the FTP host and username under Website → Deployment first.",
            };

        if (configPhp is not null)
            files["api/config.php"] = configPhp;

        var uploader = new FtpUploadService(BackendUploadTarget(settings));
        var (success, message) = await uploader.UploadWebsiteAsync(files, progress);

        return new WebDeployResult
        {
            Success = success,
            Message = message,
            FilesUploaded = success ? files.Count : 0,
        };
    }

    /// <summary>
    /// <see cref="FtpUploadService"/> uploads to <c>RemotePath</c>, which for the
    /// website is the site folder. The backend goes somewhere else, so give the
    /// uploader its own target carrying only the fields it reads - safer than
    /// mutating the live settings object for the duration of an upload.
    /// </summary>
    private static WebsiteSettings BackendUploadTarget(WebsiteSettings settings) => new()
    {
        FtpHost     = settings.FtpHost,
        FtpPort     = settings.FtpPort,
        FtpUsername = settings.FtpUsername,
        FtpPassword = settings.FtpPassword,
        RemotePath  = settings.GetEffectiveBackendRemotePath(),
    };
}
