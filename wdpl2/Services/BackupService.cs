using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Wdpl2.Data;

namespace Wdpl2.Services;

/// <summary>
/// Service for backing up and restoring app data (SQLite database + JSON data).
/// </summary>
public class BackupService
{
    /// <summary>
    /// Create a backup ZIP containing the SQLite database and JSON data file.
    /// </summary>
    /// <param name="outputPath">Full path for the output ZIP file. If null, uses default backup folder.</param>
    /// <returns>The path of the created backup file, or null on failure.</returns>
    public async Task<(bool success, string message, string? backupPath)> CreateBackupAsync(string? outputPath = null)
    {
        try
        {
            var backupDir = Path.Combine(FileSystem.AppDataDirectory, "backups");
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zipPath = outputPath ?? Path.Combine(backupDir, $"wdpl2_backup_{timestamp}.zip");

            // Ensure parent directory exists
            var zipDir = Path.GetDirectoryName(zipPath);
            if (!string.IsNullOrEmpty(zipDir) && !Directory.Exists(zipDir))
                Directory.CreateDirectory(zipDir);

            // Delete existing file if present
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using var zipStream = new FileStream(zipPath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true);

            // Backup SQLite database
            var dbPath = LeagueContext.GetDatabasePath();
            if (File.Exists(dbPath))
            {
                await AddFileToArchiveAsync(archive, dbPath, "league.db");
            }

            // Backup JSON data file
            var jsonPath = Path.Combine(FileSystem.AppDataDirectory, "wdpl2", "data.json");
            if (File.Exists(jsonPath))
            {
                await AddFileToArchiveAsync(archive, jsonPath, "data.json");
            }

            // Add metadata
            var metadata = $"Backup created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                           $"App version: {AppInfo.VersionString}\n" +
                           $"Platform: {DeviceInfo.Platform}\n";
            var metaEntry = archive.CreateEntry("backup_info.txt");
            using (var writer = new StreamWriter(metaEntry.Open()))
            {
                await writer.WriteAsync(metadata);
            }

            return (true, $"Backup created successfully at {zipPath}", zipPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Backup failed: {ex}");
            return (false, $"Backup failed: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Restore data from a backup ZIP file.
    /// </summary>
    /// <param name="backupZipPath">Full path to the backup ZIP file.</param>
    /// <returns>Success status and message.</returns>
    public async Task<(bool success, string message)> RestoreBackupAsync(string backupZipPath)
    {
        try
        {
            if (!File.Exists(backupZipPath))
                return (false, "Backup file not found.");

            using var zipStream = new FileStream(backupZipPath, FileMode.Open, FileAccess.Read);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            // Verify it's a valid backup
            var hasDb = archive.GetEntry("league.db") != null;
            var hasJson = archive.GetEntry("data.json") != null;

            if (!hasDb && !hasJson)
                return (false, "Invalid backup file — no data found.");

            // Restore SQLite database
            if (hasDb)
            {
                var dbPath = LeagueContext.GetDatabasePath();
                var entry = archive.GetEntry("league.db")!;
                await ExtractEntryAsync(entry, dbPath);
            }

            // Restore JSON data
            if (hasJson)
            {
                var jsonPath = Path.Combine(FileSystem.AppDataDirectory, "wdpl2", "data.json");
                var jsonDir = Path.GetDirectoryName(jsonPath);
                if (!string.IsNullOrEmpty(jsonDir) && !Directory.Exists(jsonDir))
                    Directory.CreateDirectory(jsonDir);

                var entry = archive.GetEntry("data.json")!;
                await ExtractEntryAsync(entry, jsonPath);
            }

            var restored = new System.Collections.Generic.List<string>();
            if (hasDb) restored.Add("database");
            if (hasJson) restored.Add("settings");

            return (true, $"Restored {string.Join(" and ", restored)} from backup. Please restart the app.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Restore failed: {ex}");
            return (false, $"Restore failed: {ex.Message}");
        }
    }

    /// <summary>
    /// List available backup files in the default backup directory.
    /// </summary>
    public System.Collections.Generic.List<BackupInfo> GetAvailableBackups()
    {
        var backups = new System.Collections.Generic.List<BackupInfo>();
        var backupDir = Path.Combine(FileSystem.AppDataDirectory, "backups");

        if (!Directory.Exists(backupDir))
            return backups;

        foreach (var file in Directory.GetFiles(backupDir, "*.zip"))
        {
            var info = new FileInfo(file);
            backups.Add(new BackupInfo
            {
                FilePath = file,
                FileName = info.Name,
                CreatedDate = info.CreationTime,
                SizeBytes = info.Length
            });
        }

        backups.Sort((a, b) => b.CreatedDate.CompareTo(a.CreatedDate));
        return backups;
    }

    /// <summary>
    /// Delete a specific backup file.
    /// </summary>
    public bool DeleteBackup(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task AddFileToArchiveAsync(ZipArchive archive, string filePath, string entryName)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await fileStream.CopyToAsync(entryStream);
    }

    private static async Task ExtractEntryAsync(ZipArchiveEntry entry, string outputPath)
    {
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        await entryStream.CopyToAsync(fileStream);
    }
}

/// <summary>
/// Information about an available backup file.
/// </summary>
public class BackupInfo
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public long SizeBytes { get; set; }
    public string SizeDisplay => SizeBytes < 1024 * 1024
        ? $"{SizeBytes / 1024.0:F1} KB"
        : $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
}
