using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
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
    /// Handles FTP/SFTP upload of website files
    /// </summary>
    public sealed class FtpUploadService
    {
        private readonly WebsiteSettings _settings;
        
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
                
                // Calculate total bytes
                foreach (var file in files.Values)
                {
                    totalBytes += Encoding.UTF8.GetByteCount(file);
                }
                
                progress?.Report(new UploadProgress
                {
                    Status = "Connecting to FTP server...",
                    TotalFiles = totalFiles,
                    TotalBytes = totalBytes
                });
                
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
                        Status = $"Uploading {fileName}..."
                    });
                    
                    var uploaded = await UploadFileAsync(fileName, content);
                    
                    if (!uploaded)
                    {
                        return (false, $"Failed to upload {fileName}");
                    }
                    
                    filesCompleted++;
                    bytesUploaded += Encoding.UTF8.GetByteCount(content);
                    
                    progress?.Report(new UploadProgress
                    {
                        CurrentFile = fileName,
                        FilesCompleted = filesCompleted,
                        TotalFiles = totalFiles,
                        BytesUploaded = bytesUploaded,
                        TotalBytes = totalBytes,
                        Status = $"Uploaded {fileName}"
                    });
                }
                
                progress?.Report(new UploadProgress
                {
                    FilesCompleted = totalFiles,
                    TotalFiles = totalFiles,
                    BytesUploaded = totalBytes,
                    TotalBytes = totalBytes,
                    Status = "Upload complete!"
                });
                
                return (true, $"Successfully uploaded {totalFiles} file(s)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FTP Upload error: {ex.Message}");
                return (false, $"Upload failed: {ex.Message}");
            }
        }
        
        private async Task<bool> UploadFileAsync(string fileName, string content)
        {
            try
            {
                // Build FTP URL
                var remotePath = _settings.RemotePath.TrimEnd('/');
                var ftpUrl = $"ftp://{_settings.FtpHost}:{_settings.FtpPort}{remotePath}/{fileName}";
                
                System.Diagnostics.Debug.WriteLine($"Uploading to: {ftpUrl}");
                
                // Create FTP request
                var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(_settings.FtpUsername, _settings.FtpPassword);
                request.UsePassive = true;
                request.UseBinary = true;
                request.KeepAlive = false;
                
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
                    System.Diagnostics.Debug.WriteLine($"Upload status: {response.StatusDescription}");
                    return response.StatusCode == FtpStatusCode.ClosingData || 
                           response.StatusCode == FtpStatusCode.FileActionOK;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Upload file error ({fileName}): {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Test FTP connection
        /// </summary>
        public async Task<(bool success, string message)> TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(_settings.FtpHost))
            {
                return (false, "FTP host is not configured");
            }
            
            try
            {
                var ftpUrl = $"ftp://{_settings.FtpHost}:{_settings.FtpPort}{_settings.RemotePath}";
                
                var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(_settings.FtpUsername, _settings.FtpPassword);
                request.UsePassive = true;
                request.KeepAlive = false;
                request.Timeout = 10000; // 10 seconds
                
                using (var response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    return (true, $"Connected successfully! Status: {response.StatusDescription}");
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as FtpWebResponse;
                var message = response != null 
                    ? $"Connection failed: {response.StatusDescription}" 
                    : $"Connection failed: {ex.Message}";
                return (false, message);
            }
            catch (Exception ex)
            {
                return (false, $"Connection failed: {ex.Message}");
            }
        }
    }
}
