using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Wdpl2.Data;

namespace Wdpl2.Services;

/// <summary>What made a backup, told apart by the start of its file name.</summary>
public enum BackupKind
{
    /// <summary>Taken every few saves. Only the newest <see cref="BackupService.AutomaticKept"/> are kept.</summary>
    Automatic,

    /// <summary>Asked for in Data Tools. Never deleted by the app.</summary>
    Manual,

    /// <summary>Taken of the league a restore was about to replace. Never deleted by the app.</summary>
    BeforeRestore,

    /// <summary>Automatic backups from before they were named apart. Never deleted by the app.</summary>
    OlderAutomatic,
}

/// <summary>
/// Backs up the league (the SQLite database and the JSON file, zipped together)
/// and restores a backup at the next start.
/// </summary>
public class BackupService
{
    /// <summary>How many automatic backups are kept; older ones are deleted as new ones are made.</summary>
    public const int AutomaticKept = 20;

    private static readonly (BackupKind Kind, string Prefix)[] Prefixes =
    [
        (BackupKind.Automatic, "auto_backup_"),
        (BackupKind.Manual, "manual_backup_"),
        (BackupKind.BeforeRestore, "before-restore_"),
        // Every save used to take one of these, under either name, and nothing
        // ever deleted them: one league had built up 280.
        (BackupKind.OlderAutomatic, "wdpl2_backup_"),
        (BackupKind.OlderAutomatic, "league_backup_"),
    ];

    public static string BackupFolder => Path.Combine(FileSystem.AppDataDirectory, "backups");

    private static string LeaguePath => Path.Combine(FileSystem.AppDataDirectory, "wdpl2", "data.json");

    /// <summary>
    /// Zips the database and the league file into the backups folder.
    /// </summary>
    public async Task<(bool success, string message, string? backupPath)> CreateBackupAsync(BackupKind kind = BackupKind.Manual)
    {
        try
        {
            Directory.CreateDirectory(BackupFolder);

            var prefix = Prefixes.First(p => p.Kind == kind).Prefix;
            var zipPath = Path.Combine(BackupFolder, $"{prefix}{DateTime.Now:yyyyMMdd_HHmmss}.zip");

            // Two backups in the same second (a quick run of saves) would
            // otherwise overwrite one another.
            for (var n = 2; File.Exists(zipPath); n++)
                zipPath = Path.Combine(BackupFolder, $"{prefix}{DateTime.Now:yyyyMMdd_HHmmss}_{n}.zip");

            using (var zipStream = new FileStream(zipPath, FileMode.CreateNew))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var dbPath = LeagueContext.GetDatabasePath();
                if (File.Exists(dbPath))
                    await AddFileToArchiveAsync(archive, dbPath, BackupFiles.DatabaseEntry);

                if (File.Exists(LeaguePath))
                    await AddFileToArchiveAsync(archive, LeaguePath, BackupFiles.LeagueEntry);

                var metaEntry = archive.CreateEntry("backup_info.txt");
                using var writer = new StreamWriter(metaEntry.Open());
                await writer.WriteAsync(
                    $"Backup created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"Kind: {kind}\n" +
                    $"App version: {AppInfo.VersionString}\n" +
                    $"Platform: {DeviceInfo.Platform}\n");
            }

            if (kind == BackupKind.Automatic)
                PruneAutomatic();

            return (true, $"Backup saved as {Path.GetFileName(zipPath)}.", zipPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Backup failed: {ex}");
            return (false, $"Backup failed: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Checks a backup and queues it to replace the league at the next start.
    /// </summary>
    /// <remarks>See <see cref="BackupFiles"/> for why a restore cannot happen while the app is open.</remarks>
    public static void StageRestore(string backupZip) =>
        BackupFiles.Stage(backupZip, FileSystem.AppDataDirectory);

    /// <summary>
    /// Puts a queued backup in place, first taking a backup of the league it
    /// replaces. Called at start-up before anything opens the league.
    /// </summary>
    /// <returns>What happened, for the person; null when nothing was queued.</returns>
    public static async Task<string?> ApplyPendingRestoreAsync()
    {
        var appData = FileSystem.AppDataDirectory;
        if (!BackupFiles.HasPending(appData))
            return null;

        // The league about to be replaced is backed up first, so a restore of
        // the wrong file is itself one restore away from being undone.
        var safety = await new BackupService().CreateBackupAsync(BackupKind.BeforeRestore);
        if (!safety.success)
        {
            BackupFiles.CancelPending(appData);
            return "The backup was not restored: the current league could not be backed up first, "
                 + "so it was left as it was. " + safety.message;
        }

        try
        {
            BackupFiles.ApplyPending(appData, LeagueContext.GetDatabasePath(), LeaguePath);
            return "Your backup has been restored. The league it replaced was saved first, as "
                 + Path.GetFileName(safety.backupPath) + ".";
        }
        catch (Exception ex)
        {
            BackupFiles.CancelPending(appData);
            System.Diagnostics.Debug.WriteLine($"Restore failed: {ex}");
            return "The backup could not be restored, and your league was left as it was. " + ex.Message;
        }
    }

    /// <summary>Every backup in the folder, newest first.</summary>
    public List<BackupInfo> GetAvailableBackups()
    {
        var backups = new List<BackupInfo>();
        if (!Directory.Exists(BackupFolder))
            return backups;

        foreach (var file in Directory.GetFiles(BackupFolder, "*.zip"))
        {
            var info = new FileInfo(file);
            backups.Add(new BackupInfo
            {
                FilePath = file,
                FileName = info.Name,
                CreatedDate = info.LastWriteTime,
                SizeBytes = info.Length,
                Kind = KindOf(info.Name),
            });
        }

        backups.Sort((a, b) => b.CreatedDate.CompareTo(a.CreatedDate));
        return backups;
    }

    public bool DeleteBackup(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;
            File.Delete(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Deletes every backup of one kind. Returns how many went.</summary>
    public int DeleteAll(BackupKind kind) =>
        GetAvailableBackups().Where(b => b.Kind == kind).Count(b => DeleteBackup(b.FilePath));

    public static BackupKind KindOf(string fileName)
    {
        foreach (var (kind, prefix) in Prefixes)
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return kind;
        return BackupKind.Manual;
    }

    /// <summary>Keeps the newest automatic backups and deletes the rest.</summary>
    private void PruneAutomatic()
    {
        foreach (var old in GetAvailableBackups().Where(b => b.Kind == BackupKind.Automatic).Skip(AutomaticKept))
            DeleteBackup(old.FilePath);
    }

    private static async Task AddFileToArchiveAsync(ZipArchive archive, string filePath, string entryName)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await fileStream.CopyToAsync(entryStream);
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
    public BackupKind Kind { get; set; }

    public string SizeDisplay => SizeBytes < 1024 * 1024
        ? $"{SizeBytes / 1024.0:F1} KB"
        : $"{SizeBytes / (1024.0 * 1024.0):F1} MB";

    public string KindDisplay => Kind switch
    {
        BackupKind.Automatic => "Automatic",
        BackupKind.Manual => "Made by you",
        BackupKind.BeforeRestore => "Before a restore",
        _ => "Older automatic",
    };
}
