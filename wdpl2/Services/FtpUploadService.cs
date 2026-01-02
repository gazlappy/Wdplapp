using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wdpl2.Models;

#pragma warning disable SYSLIB0014 // WebRequest is obsolete - FTP requires it until FluentFTP or similar is added

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
    /// Handles FTP/SFTP upload of website files
    /// </summary>
    public sealed class FtpUploadService
    {
        private readonly WebsiteSettings _settings;
        private bool? _usePassiveMode; // null = not determined yet
        
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
                
                // Determine which mode works (passive or active)
                if (!_usePassiveMode.HasValue)
                {
                    _usePassiveMode = await DetermineConnectionModeAsync();
                }
                
                // Try to ensure the directory exists
                progress?.Report(new UploadProgress
                {
                    Status = $"Checking remote directory: {remotePath}",
                    TotalFiles = totalFiles,
                    TotalBytes = totalBytes
                });
                
                await EnsureDirectoryExistsAsync();
                
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
                    
                    var (uploaded, errorMsg) = await UploadFileAsync(fileName, content);
                    
                    if (!uploaded)
                    {
                        return (false, $"Failed to upload {fileName}: {errorMsg}");
                    }
                    
                    uploadedFiles.Add(fileName);
                    filesCompleted++;
                    bytesUploaded += Encoding.UTF8.GetByteCount(content);
                    
                    progress?.Report(new UploadProgress
                    {
                        CurrentFile = fileName,
                        FilesCompleted = filesCompleted,
                        TotalFiles = totalFiles,
                        BytesUploaded = bytesUploaded,
                        TotalBytes = totalBytes,
                        Status = $"? Uploaded {fileName}"
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
                    sb.AppendLine("?? Warning: index.html was not in the file list!");
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
                var host = _settings.FtpHost.Trim();
                var remotePath = NormalizePath(_settings.RemotePath);
                var ftpUrl = $"ftp://{host}:{_settings.FtpPort}{remotePath}";
                
                var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(_settings.FtpUsername, _settings.FtpPassword);
                request.UsePassive = _usePassiveMode ?? true;
                request.KeepAlive = false;
                request.Timeout = 15000;
                
                using var response = (FtpWebResponse)await request.GetResponseAsync();
                using var reader = new StreamReader(response.GetResponseStream());
                
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var fileName = line.Trim();
                        if (fileName != "." && fileName != "..")
                        {
                            foundFiles.Add(fileName);
                        }
                    }
                }
                
                var hasIndex = foundFiles.Any(f => f.Equals("index.html", StringComparison.OrdinalIgnoreCase));
                var hasStyle = foundFiles.Any(f => f.Equals("style.css", StringComparison.OrdinalIgnoreCase));
                
                var sb = new StringBuilder();
                sb.AppendLine($"Found {foundFiles.Count} file(s) at {remotePath}:");
                sb.AppendLine();
                
                foreach (var file in foundFiles.OrderBy(f => f))
                {
                    var icon = file.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? "??" :
                               file.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? "??" : "??";
                    sb.AppendLine($"  {icon} {file}");
                }
                
                sb.AppendLine();
                
                if (hasIndex)
                {
                    sb.AppendLine("? index.html found - your homepage should be accessible!");
                }
                else
                {
                    sb.AppendLine("? index.html NOT FOUND - your site will show 'Not Found'");
                    sb.AppendLine();
                    sb.AppendLine("Possible issues:");
                    sb.AppendLine("  • Files uploaded to wrong directory");
                    sb.AppendLine("  • Try changing Remote Path in settings");
                }
                
                return (hasIndex, foundFiles, sb.ToString());
            }
            catch (Exception ex)
            {
                return (false, foundFiles, $"Could not verify upload: {ex.Message}");
            }
        }
        
        private async Task<bool> DetermineConnectionModeAsync()
        {
            // Try passive first
            var passiveWorks = await TestModeAsync(usePassive: true);
            if (passiveWorks) return true;
            
            // Try active
            var activeWorks = await TestModeAsync(usePassive: false);
            return activeWorks ? false : true; // Default to passive if both fail
        }
        
        private async Task<bool> TestModeAsync(bool usePassive)
        {
            try
            {
                var host = _settings.FtpHost.Trim();
                var remotePath = NormalizePath(_settings.RemotePath);
                var ftpUrl = $"ftp://{host}:{_settings.FtpPort}{remotePath}";
                
                var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(_settings.FtpUsername, _settings.FtpPassword);
                request.UsePassive = usePassive;
                request.KeepAlive = false;
                request.Timeout = 10000;
                
                using var response = (FtpWebResponse)await request.GetResponseAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task EnsureDirectoryExistsAsync()
        {
            try
            {
                var host = _settings.FtpHost.Trim();
                var remotePath = NormalizePath(_settings.RemotePath);
                
                // Try to create directory (will fail silently if it exists)
                var ftpUrl = $"ftp://{host}:{_settings.FtpPort}{remotePath}";
                
                var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.MakeDirectory;
                request.Credentials = new NetworkCredential(_settings.FtpUsername, _settings.FtpPassword);
                request.UsePassive = _usePassiveMode ?? true;
                request.KeepAlive = false;
                request.Timeout = 10000;
                
                using var response = (FtpWebResponse)await request.GetResponseAsync();
            }
            catch (WebException ex) when (ex.Response is FtpWebResponse ftpResp && 
                                          (ftpResp.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable ||
                                           ftpResp.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailableOrBusy))
            {
                // Directory already exists - this is fine
            }
            catch
            {
                // Ignore errors - directory might already exist or we don't have permission to create
            }
        }
        
        private async Task<(bool success, string? error)> UploadFileAsync(string fileName, string content)
        {
            try
            {
                // Build FTP URL - ensure proper path formatting
                var host = _settings.FtpHost.Trim();
                var remotePath = NormalizePath(_settings.RemotePath);
                var ftpUrl = $"ftp://{host}:{_settings.FtpPort}{remotePath}{fileName}";
                
                System.Diagnostics.Debug.WriteLine($"Uploading to: {ftpUrl}");
                System.Diagnostics.Debug.WriteLine($"Using passive mode: {_usePassiveMode ?? true}");
                
                // Create FTP request
                var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(_settings.FtpUsername, _settings.FtpPassword);
                request.UsePassive = _usePassiveMode ?? true;
                request.UseBinary = true;
                request.KeepAlive = false;
                request.EnableSsl = false;
                request.Timeout = 60000; // 60 seconds for upload
                
                // Convert content to bytes
                var fileContents = Encoding.UTF8.GetBytes(content);
                request.ContentLength = fileContents.Length;
                
                // Upload file
                using (var requestStream = await request.GetRequestStreamAsync())
                {
                    await requestStream.WriteAsync(fileContents, 0, fileContents.Length);
                }
                
                // Get response
                using (var response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    System.Diagnostics.Debug.WriteLine($"Upload status: {response.StatusCode} - {response.StatusDescription}");
                    
                    if (response.StatusCode == FtpStatusCode.ClosingData || 
                        response.StatusCode == FtpStatusCode.FileActionOK)
                    {
                        return (true, null);
                    }
                    else
                    {
                        return (false, $"Status {(int)response.StatusCode}: {response.StatusDescription}");
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is FtpWebResponse ftpResp)
                {
                    var statusCode = (int)ftpResp.StatusCode;
                    var statusDesc = ftpResp.StatusDescription?.Trim() ?? "";
                    
                    System.Diagnostics.Debug.WriteLine($"FTP error: {statusCode} - {statusDesc}");
                    
                    // Provide user-friendly error messages
                    return statusCode switch
                    {
                        550 => (false, "Permission denied or path not found. Check remote path."),
                        553 => (false, "File name not allowed. Check file name."),
                        452 => (false, "Disk full or quota exceeded."),
                        _ => (false, $"FTP error {statusCode}: {statusDesc}")
                    };
                }
                
                System.Diagnostics.Debug.WriteLine($"Upload error: {ex.Message}");
                return (false, ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Upload file error ({fileName}): {ex.Message}");
                return (false, ex.Message);
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
            
            // Build the URL for debugging
            var host = _settings.FtpHost.Trim();
            var remotePath = NormalizePath(_settings.RemotePath);
            var ftpUrl = $"ftp://{host}:{_settings.FtpPort}{remotePath}";
            
            System.Diagnostics.Debug.WriteLine($"Testing FTP connection to: {ftpUrl}");
            System.Diagnostics.Debug.WriteLine($"Username: {_settings.FtpUsername}");
            
            // Try passive mode first, then active mode
            var passiveResult = await TryConnectAsync(ftpUrl, usePassive: true);
            if (passiveResult.success)
            {
                _usePassiveMode = true;
                return (true, $"Connected (passive mode)! {passiveResult.message}");
            }
            
            System.Diagnostics.Debug.WriteLine($"Passive mode failed: {passiveResult.message}, trying active mode...");
            
            var activeResult = await TryConnectAsync(ftpUrl, usePassive: false);
            if (activeResult.success)
            {
                _usePassiveMode = false;
                return (true, $"Connected (active mode)! {activeResult.message}");
            }
            
            // Both failed - try to discover available paths
            var discoveredPaths = await DiscoverAvailablePathsAsync(host);
            
            // Build helpful error message
            var errorDetails = new StringBuilder();
            errorDetails.AppendLine("Connection failed with both passive and active modes.");
            errorDetails.AppendLine();
            errorDetails.AppendLine($"Host: {host}");
            errorDetails.AppendLine($"Port: {_settings.FtpPort}");
            errorDetails.AppendLine($"Path: {remotePath}");
            errorDetails.AppendLine($"User: {_settings.FtpUsername}");
            errorDetails.AppendLine();
            errorDetails.AppendLine($"Passive error: {passiveResult.message}");
            errorDetails.AppendLine($"Active error: {activeResult.message}");
            
            if (discoveredPaths.Any())
            {
                errorDetails.AppendLine();
                errorDetails.AppendLine("?? Available paths found at root:");
                foreach (var pathItem in discoveredPaths.Take(15))
                {
                    errorDetails.AppendLine($"  • {pathItem}");
                }
                if (discoveredPaths.Count > 15)
                    errorDetails.AppendLine($"  ... and {discoveredPaths.Count - 15} more");
            }
            
            errorDetails.AppendLine();
            errorDetails.AppendLine("Tips:");
            errorDetails.AppendLine("• Check the FTP host - try with/without 'ftp.' prefix");
            errorDetails.AppendLine("• Verify username includes domain (e.g., user@domain.com)");
            errorDetails.AppendLine("• Check password is correct");
            errorDetails.AppendLine("• Try path '/' first to see what's available");
            
            return (false, errorDetails.ToString());
        }
        
        /// <summary>
        /// Discover available directories at the FTP root
        /// </summary>
        private async Task<List<string>> DiscoverAvailablePathsAsync(string host)
        {
            var paths = new List<string>();
            
            try
            {
                // Try to list root directory
                var rootUrl = $"ftp://{host}:{_settings.FtpPort}/";
                
                var request = (FtpWebRequest)WebRequest.Create(rootUrl);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(_settings.FtpUsername, _settings.FtpPassword);
                request.UsePassive = true;
                request.KeepAlive = false;
                request.EnableSsl = false;
                request.Timeout = 10000;
                
                using var response = (FtpWebResponse)await request.GetResponseAsync();
                using var reader = new StreamReader(response.GetResponseStream());
                
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        paths.Add("/" + line.Trim());
                    }
                }
            }
            catch
            {
                // If root listing fails, try common paths
                var commonPaths = new[] 
                { 
                    "/public_html/", 
                    "/www/", 
                    "/htdocs/",
                    "/domains/",
                    "/"
                };
                
                foreach (var testPath in commonPaths)
                {
                    try
                    {
                        var testUrl = $"ftp://{host}:{_settings.FtpPort}{testPath}";
                        var request = (FtpWebRequest)WebRequest.Create(testUrl);
                        request.Method = WebRequestMethods.Ftp.ListDirectory;
                        request.Credentials = new NetworkCredential(_settings.FtpUsername, _settings.FtpPassword);
                        request.UsePassive = true;
                        request.KeepAlive = false;
                        request.Timeout = 5000;
                        
                        using var response = (FtpWebResponse)await request.GetResponseAsync();
                        paths.Add($"{testPath} ? (accessible)");
                    }
                    catch
                    {
                        // Path not accessible
                    }
                }
            }
            
            return paths;
        }

        private async Task<(bool success, string message)> TryConnectAsync(string ftpUrl, bool usePassive)
        {
            try
            {
                var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(_settings.FtpUsername, _settings.FtpPassword);
                request.UsePassive = usePassive;
                request.KeepAlive = false;
                request.EnableSsl = false;
                request.Timeout = 15000; // 15 seconds
                
                using var response = (FtpWebResponse)await request.GetResponseAsync();
                
                // Get names of items in directory
                var items = new List<string>();
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            var item = line.Trim();
                            // Filter out . and .. entries
                            if (item != "." && item != "..")
                            {
                                items.Add(item);
                            }
                        }
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
                        var icon = item.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? "??" :
                                   item.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? "??" : "??";
                        message.AppendLine($"  {icon} /{item}");
                    }
                    if (items.Count > 15)
                    {
                        message.AppendLine($"  ... and {items.Count - 15} more");
                    }
                    
                    if (hasIndex)
                    {
                        message.AppendLine();
                        message.AppendLine("? index.html already exists in this directory!");
                        message.AppendLine("   Uploading will overwrite existing files.");
                    }
                    else
                    {
                        // Suggest what to try
                        message.AppendLine();
                        message.AppendLine("?? This folder has no index.html yet.");
                        message.AppendLine("   Upload your website here to make it live!");
                    }
                }
                else
                {
                    message.AppendLine();
                    message.AppendLine("This folder appears empty. You can upload directly here.");
                    message.AppendLine("?? Keep Remote Path as current setting to upload to this location.");
                }
                
                return (true, message.ToString());
            }
            catch (WebException ex)
            {
                if (ex.Response is FtpWebResponse ftpResponse)
                {
                    var statusCode = (int)ftpResponse.StatusCode;
                    var statusDesc = ftpResponse.StatusDescription?.Trim() ?? "";
                    
                    // Provide user-friendly error messages
                    return statusCode switch
                    {
                        530 => (false, "Login failed - check username/password"),
                        550 => (false, "Path not found - check remote path exists"),
                        421 => (false, "Server unavailable - try again later"),
                        _ => (false, $"FTP error {statusCode}: {statusDesc}")
                    };
                }
                
                // Network-level error
                if (ex.Status == WebExceptionStatus.NameResolutionFailure)
                    return (false, "Host not found - check FTP hostname");
                if (ex.Status == WebExceptionStatus.ConnectFailure)
                    return (false, "Connection refused - check host and port");
                if (ex.Status == WebExceptionStatus.Timeout)
                    return (false, "Connection timed out - server may be slow or blocked");
                
                return (false, ex.Message);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
