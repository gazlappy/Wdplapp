using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace Wdpl2.Services;

/// <summary>
/// Service for exporting website files locally
/// </summary>
public sealed class LocalExportService
{
    /// <summary>
    /// Export website files to a folder
    /// </summary>
    public async Task<(bool success, string message, string? outputPath)> ExportToFolderAsync(
        Dictionary<string, string> files,
        string outputFolder,
        IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Preparing export folder...");
            
            // Create output folder if it doesn't exist
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            
            var totalFiles = files.Count;
            var processedFiles = 0;
            
            foreach (var file in files)
            {
                processedFiles++;
                progress?.Report($"Exporting {file.Key} ({processedFiles}/{totalFiles})...");
                
                var filePath = Path.Combine(outputFolder, file.Key);
                
                // Create subdirectories if needed
                var fileDir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(fileDir) && !Directory.Exists(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }
                
                // Write file
                await File.WriteAllTextAsync(filePath, file.Value, Encoding.UTF8);
            }
            
            progress?.Report("Export complete!");
            
            return (true, $"Successfully exported {files.Count} files to folder.", outputFolder);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Local export error: {ex}");
            return (false, $"Export failed: {ex.Message}", null);
        }
    }
    
    /// <summary>
    /// Export website files as a ZIP archive
    /// </summary>
    public async Task<(bool success, string message, string? zipPath)> ExportAsZipAsync(
        Dictionary<string, string> files,
        string zipFilePath,
        IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Creating ZIP archive...");
            
            // Ensure directory exists
            var zipDir = Path.GetDirectoryName(zipFilePath);
            if (!string.IsNullOrEmpty(zipDir) && !Directory.Exists(zipDir))
            {
                Directory.CreateDirectory(zipDir);
            }
            
            // Delete existing file if present
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }
            
            using var zipStream = new FileStream(zipFilePath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true);
            
            var totalFiles = files.Count;
            var processedFiles = 0;
            
            foreach (var file in files)
            {
                processedFiles++;
                progress?.Report($"Adding {file.Key} ({processedFiles}/{totalFiles})...");
                
                var entry = archive.CreateEntry(file.Key, CompressionLevel.Optimal);
                
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                await writer.WriteAsync(file.Value);
            }
            
            progress?.Report("ZIP created successfully!");
            
            var fileInfo = new FileInfo(zipFilePath);
            var sizeKb = fileInfo.Length / 1024.0;
            
            return (true, $"Successfully created ZIP ({sizeKb:N1} KB) with {files.Count} files.", zipFilePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ZIP export error: {ex}");
            return (false, $"ZIP export failed: {ex.Message}", null);
        }
    }
    
    /// <summary>
    /// Export website files to a memory stream (for sharing)
    /// </summary>
    public async Task<(bool success, string message, MemoryStream? zipStream)> ExportToMemoryStreamAsync(
        Dictionary<string, string> files,
        IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Creating ZIP in memory...");
            
            var memoryStream = new MemoryStream();
            
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                var totalFiles = files.Count;
                var processedFiles = 0;
                
                foreach (var file in files)
                {
                    processedFiles++;
                    progress?.Report($"Adding {file.Key} ({processedFiles}/{totalFiles})...");
                    
                    var entry = archive.CreateEntry(file.Key, CompressionLevel.Optimal);
                    
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                    await writer.WriteAsync(file.Value);
                }
            }
            
            // Reset position for reading
            memoryStream.Position = 0;
            
            progress?.Report("ZIP created successfully!");
            
            var sizeKb = memoryStream.Length / 1024.0;
            return (true, $"Created ZIP ({sizeKb:N1} KB) with {files.Count} files.", memoryStream);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Memory stream export error: {ex}");
            return (false, $"Export failed: {ex.Message}", null);
        }
    }
    
    /// <summary>
    /// Get default export folder path
    /// </summary>
    public static string GetDefaultExportFolder()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "WDPL", "Website");
    }
    
    /// <summary>
    /// Get default ZIP file path
    /// </summary>
    public static string GetDefaultZipPath(string leagueName)
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var safeName = MakeSafeFileName(leagueName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(documentsPath, "WDPL", $"{safeName}_Website_{timestamp}.zip");
    }
    
    private static string MakeSafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "PoolLeague";
        
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new StringBuilder();
        
        foreach (var c in name)
        {
            if (Array.IndexOf(invalidChars, c) < 0 && c != ' ')
                safeName.Append(c);
            else if (c == ' ')
                safeName.Append('_');
        }
        
        var result = safeName.ToString();
        return string.IsNullOrEmpty(result) ? "PoolLeague" : result;
    }
}
