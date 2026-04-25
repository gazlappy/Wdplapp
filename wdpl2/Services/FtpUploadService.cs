using System.Text;
using FluentFTP;
using FluentFTP.Exceptions;
using Wdpl2.Helpers;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// Upload progress information
    /// </summary>
    public sealed class UploadProgress
    {
        public string CurrentFile { get; set; } = "";
        public int FilesCompleted { get; set; }
        public int TotalFiles { get; set; }
        public long BytesUploaded { get; set; }
        public long TotalBytes { get; set; }
        public int PercentComplete => TotalFiles > 0 ? (FilesCompleted * 100 / TotalFiles) : 0;
        public string Status { get; set; } = "";
    }

    /// <summary>
    /// Handles FTP upload of website files using FluentFTP.
    /// </summary>
    public sealed class FtpUploadService
    {
        private readonly WebsiteSettings _settings;
        private FtpProfile? _connectionProfile; // null = not determined yet

        public FtpUploadService(WebsiteSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Upload generated website files to FTP server
        /// </summary>
        public async Task<(bool success, string message)> UploadWebsiteAsync(
            Dictionary<string, string> files,
            IProgress<UploadProgress>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(_settings.FtpHost))
            {
                return (false, "FTP host is not configured");
            }

            if (string.IsNullOrWhiteSpace(_settings.FtpUsername))
            {
                return (false, "FTP username is not configured");
            }

            try
            {
                var totalFiles = files.Count;
                var filesCompleted = 0;
                var totalBytes = 0L;
                var bytesUploaded = 0L;
                var uploadedFiles = new List<string>();

                // Calculate total bytes
                foreach (var file in files.Values)
                {
                    totalBytes += Encoding.UTF8.GetByteCount(file);
                }

                var remotePath = NormalizePath(_settings.RemotePath);

                progress?.Report(new UploadProgress
                {
                    Status = $"Connecting to {_settings.FtpHost}...",
                    TotalFiles = totalFiles,
                    TotalBytes = totalBytes
                });

                // Determine best connection settings (encryption + data mode)
                await using var client = CreateClient();
                await ConnectAsync(client);

                // Try to ensure the directory exists
                progress?.Report(new UploadProgress
                {
                    Status = $"Checking remote directory: {remotePath}",
                    TotalFiles = totalFiles,
                    TotalBytes = totalBytes
                });

                // CreateDirectory is a no-op if it already exists
                if (!await client.DirectoryExists(remotePath))
                {
                    await client.CreateDirectory(remotePath);
                }

                // Upload each file
                foreach (var file in files)
                {
                    var fileName = file.Key;
                    var content = file.Value;

                    progress?.Report(new UploadProgress
                    {
                        CurrentFile = fileName,
                        FilesCompleted = filesCompleted,
                        TotalFiles = totalFiles,
                        BytesUploaded = bytesUploaded,
                        TotalBytes = totalBytes,
                        Status = $"Uploading {fileName} to {remotePath}..."
                    });

                    var fileBytes = Encoding.UTF8.GetBytes(content);
                    var fullPath = remotePath + fileName;

                    System.Diagnostics.Debug.WriteLine($"Uploading to: {fullPath}");

                    using var ms = new MemoryStream(fileBytes);
                    var status = await client.UploadStream(ms, fullPath, FtpRemoteExists.Overwrite, true);

                    if (status != FtpStatus.Success)
                    {
                        return (false, $"Failed to upload {fileName}: server returned {status}");
                    }

                    uploadedFiles.Add(fileName);
                    filesCompleted++;
                    bytesUploaded += fileBytes.Length;

                    progress?.Report(new UploadProgress
                    {
                        CurrentFile = fileName,
                        FilesCompleted = filesCompleted,
                        TotalFiles = totalFiles,
                        BytesUploaded = bytesUploaded,
                        TotalBytes = totalBytes,
                        Status = $"✅ Uploaded {fileName}"
                    });
                }

                // Verify index.html was uploaded
                var hasIndexHtml = uploadedFiles.Contains("index.html");

                progress?.Report(new UploadProgress
                {
                    FilesCompleted = totalFiles,
                    TotalFiles = totalFiles,
                    BytesUploaded = totalBytes,
                    TotalBytes = totalBytes,
                    Status = "Upload complete!"
                });

                // Build success message
                var sb = new StringBuilder();
                sb.AppendLine($"Successfully uploaded {totalFiles} file(s)");
                sb.AppendLine();
                sb.AppendLine($"Upload location: {_settings.FtpHost}{remotePath}");
                sb.AppendLine();
                sb.AppendLine("Files uploaded:");
                foreach (var f in uploadedFiles)
                {
                    sb.AppendLine($"  ? {f}");
                }

                if (!hasIndexHtml)
                {
                    sb.AppendLine();
                    sb.AppendLine("ℹ️ Warning: index.html was not in the file list!");
                }

                return (true, sb.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FTP Upload error: {ex.Message}");
                return (false, $"Upload failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Verify uploaded files exist on the server
        /// </summary>
        public async Task<(bool success, List<string> foundFiles, string message)> VerifyUploadAsync()
        {
            var foundFiles = new List<string>();

            try
            {
                var remotePath = NormalizePath(_settings.RemotePath);

                await using var client = CreateClient();
                await ConnectAsync(client);

                var items = await client.GetNameListing(remotePath);

                foreach (var item in items)
                {
                    var name = Path.GetFileName(item);
                    if (!string.IsNullOrWhiteSpace(name) && name != "." && name != "..")
                    {
                        foundFiles.Add(name);
                    }
                }

                var hasIndex = foundFiles.Any(f => f.Equals("index.html", StringComparison.OrdinalIgnoreCase));

                var sb = new StringBuilder();
                sb.AppendLine($"Found {foundFiles.Count} file(s) at {remotePath}:");
                sb.AppendLine();

                foreach (var file in foundFiles.OrderBy(f => f))
                {
                    var icon = file.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? Emojis.Document :
                               file.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? Emojis.Edit : Emojis.Document;
                    sb.AppendLine($"  {icon} {file}");
                }

                sb.AppendLine();

                if (hasIndex)
                {
                    sb.AppendLine("✅ index.html found - your homepage should be accessible!");
                }
                else
                {
                    sb.AppendLine("❌ index.html NOT FOUND - your site will show 'Not Found'");
                    sb.AppendLine();
                    sb.AppendLine("Possible issues:");
                    sb.AppendLine("  � Files uploaded to wrong directory");
                    sb.AppendLine("  � Try changing Remote Path in settings");
                }

                return (hasIndex, foundFiles, sb.ToString());
            }
            catch (Exception ex)
            {
                return (false, foundFiles, $"Could not verify upload: {ex.Message}");
            }
        }

        /// <summary>
        /// Test FTP connection with detailed error reporting
        /// </summary>
        public async Task<(bool success, string message)> TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(_settings.FtpHost))
            {
                return (false, "FTP host is not configured");
            }

            if (string.IsNullOrWhiteSpace(_settings.FtpUsername))
            {
                return (false, "FTP username is not configured");
            }

            var host = _settings.FtpHost.Trim();
            var remotePath = NormalizePath(_settings.RemotePath);

            System.Diagnostics.Debug.WriteLine($"Testing FTP connection to: {host}:{_settings.FtpPort}{remotePath}");
            System.Diagnostics.Debug.WriteLine($"Username: {_settings.FtpUsername}");

            try
            {
                await using var client = CreateClient();
                _connectionProfile = await client.AutoConnect();

                var modeDesc = _connectionProfile?.Encryption switch
                {
                    FtpEncryptionMode.Explicit => "FTPS explicit",
                    FtpEncryptionMode.Implicit => "FTPS implicit",
                    _ => "plain FTP"
                };

                // Get names of items in directory
                var rawItems = await client.GetNameListing(remotePath);
                var items = new List<string>();
                foreach (var raw in rawItems)
                {
                    var item = Path.GetFileName(raw);
                    if (!string.IsNullOrWhiteSpace(item) && item != "." && item != "..")
                    {
                        items.Add(item);
                    }
                }

                // Check if index.html already exists
                var hasIndex = items.Any(i => i.Equals("index.html", StringComparison.OrdinalIgnoreCase));

                // Build message showing what was found
                var message = new StringBuilder();
                message.Append($"Found {items.Count} item(s)");

                if (items.Count > 0)
                {
                    message.AppendLine(":");
                    foreach (var item in items.Take(15))
                    {
                        var icon = item.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? Emojis.Document :
                                   item.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? Emojis.Edit : Emojis.Document;
                        message.AppendLine($"  {icon} /{item}");
                    }
                    if (items.Count > 15)
                    {
                        message.AppendLine($"  ... and {items.Count - 15} more");
                    }

                    if (hasIndex)
                    {
                        message.AppendLine();
                        message.AppendLine("✅ index.html already exists in this directory!");
                        message.AppendLine("   Uploading will overwrite existing files.");
                    }
                    else
                    {
                        message.AppendLine();
                        message.AppendLine("ℹ️ This folder has no index.html yet.");
                        message.AppendLine("   Upload your website here to make it live!");
                    }
                }
                else
                {
                    message.AppendLine();
                    message.AppendLine("This folder appears empty. You can upload directly here.");
                    message.AppendLine("ℹ️ Keep Remote Path as current setting to upload to this location.");
                }

                return (true, $"Connected ({modeDesc})! {message}");
            }
            catch (FtpAuthenticationException)
            {
                return (false, "Login failed - check username/password");
            }
            catch (FtpCommandException ex)
            {
                return ex.CompletionCode switch
                {
                    "550" => (false, "Path not found - check remote path exists"),
                    "421" => (false, "Server unavailable - try again later"),
                    _ => (false, $"FTP error {ex.CompletionCode}: {ex.Message}")
                };
            }
            catch (FtpException ex) when (ex.InnerException is System.Net.Sockets.SocketException sock)
            {
                return sock.SocketErrorCode switch
                {
                    System.Net.Sockets.SocketError.HostNotFound => (false, "Host not found - check FTP hostname"),
                    System.Net.Sockets.SocketError.ConnectionRefused => (false, "Connection refused - check host and port"),
                    System.Net.Sockets.SocketError.TimedOut => (false, "Connection timed out - server may be slow or blocked"),
                    _ => (false, ex.Message)
                };
            }
            catch (TimeoutException)
            {
                return (false, "Connection timed out - server may be slow or blocked");
            }
            catch (Exception ex)
            {
                var errorDetails = new StringBuilder();
                errorDetails.AppendLine($"Connection failed: {ex.Message}");
                errorDetails.AppendLine();
                errorDetails.AppendLine($"Host: {host}");
                errorDetails.AppendLine($"Port: {_settings.FtpPort}");
                errorDetails.AppendLine($"Path: {remotePath}");
                errorDetails.AppendLine($"User: {_settings.FtpUsername}");
                errorDetails.AppendLine();
                errorDetails.AppendLine("Tips:");
                errorDetails.AppendLine("• Check the FTP host - try with/without 'ftp.' prefix");
                errorDetails.AppendLine("• Verify username includes domain (e.g., user@domain.com)");
                errorDetails.AppendLine("• Check password is correct");
                errorDetails.AppendLine("• Try path '/' first to see what's available");

                return (false, errorDetails.ToString());
            }
        }

        // ====== PRIVATE HELPERS ======

        private AsyncFtpClient CreateClient()
        {
            var host = _settings.FtpHost.Trim();
            var port = _settings.FtpPort;

            var client = new AsyncFtpClient(host, _settings.FtpUsername, _settings.FtpPassword, port)
            {
                Config =
                {
                    ConnectTimeout = 15000,
                    ReadTimeout = 15000,
                    DataConnectionConnectTimeout = 15000,
                    DataConnectionReadTimeout = 60000,
                    ValidateAnyCertificate = true,
                }
            };

            // Apply previously discovered profile settings for faster reconnect
            if (_connectionProfile != null)
            {
                client.Config.EncryptionMode = _connectionProfile.Encryption;
                client.Config.DataConnectionType = _connectionProfile.DataConnection;
            }

            return client;
        }

        /// <summary>
        /// Connect using a previously discovered profile, or auto-detect the best settings.
        /// AutoConnect tries multiple encryption modes (Explicit TLS, Implicit TLS, None)
        /// and data connection types (Passive, Active) to find what works.
        /// </summary>
        private async Task ConnectAsync(AsyncFtpClient client)
        {
            if (_connectionProfile != null)
            {
                // Fast path: reuse previously discovered settings
                await client.Connect();
            }
            else
            {
                // First connection: auto-detect the best encryption + data mode
                _connectionProfile = await client.AutoConnect();
            }
        }

        private string NormalizePath(string? path)
        {
            var remotePath = path?.Trim() ?? "/";

            // Ensure path starts with /
            if (!remotePath.StartsWith("/"))
                remotePath = "/" + remotePath;

            // Ensure path ends with /
            if (!remotePath.EndsWith("/"))
                remotePath += "/";

            return remotePath;
        }

        /// <summary>
        /// Compute SHA256 hash of file content for change detection.
        /// </summary>
        public static string ComputeHash(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = System.Security.Cryptography.SHA256.HashData(bytes);
            return Convert.ToHexStringLower(hash);
        }

        /// <summary>
        /// Returns only the files whose content has changed since the last upload.
        /// </summary>
        public static Dictionary<string, string> GetChangedFiles(
            Dictionary<string, string> allFiles,
            Dictionary<string, string> previousHashes)
        {
            var changed = new Dictionary<string, string>();
            foreach (var kvp in allFiles)
            {
                var hash = ComputeHash(kvp.Value);
                if (!previousHashes.TryGetValue(kvp.Key, out var prev) || prev != hash)
                    changed[kvp.Key] = kvp.Value;
            }
            return changed;
        }

        /// <summary>
        /// Builds a hash dictionary for all files (call after successful upload to store).
        /// </summary>
        public static Dictionary<string, string> BuildHashDictionary(Dictionary<string, string> files)
        {
            var hashes = new Dictionary<string, string>(files.Count);
            foreach (var kvp in files)
                hashes[kvp.Key] = ComputeHash(kvp.Value);
            return hashes;
        }
    }
}
