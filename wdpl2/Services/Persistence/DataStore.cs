using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2;

public static partial class DataStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // Paths are lazily resolved so unit tests (which can't init MAUI FileSystem)
    // can touch DataStore.Data without triggering the static cctor MAUI lookup.
    private static readonly Lazy<string> _appDataDir = new(() =>
    {
        try { return FileSystem.AppDataDirectory; }
        catch { return Path.Combine(Path.GetTempPath(), "wdpl2-test"); }
    });

    private static string DataPath => Path.Combine(_appDataDir.Value, "wdpl2", "data.json");
    private static string BackupPath => Path.Combine(_appDataDir.Value, "wdpl2", "data.json.bak");
    private static string ImportSnapshotPath => Path.Combine(_appDataDir.Value, "wdpl2", "data.json.pre-import");

    private static int _saveCount;
    private const int AutoBackupInterval = 5;

    private static IServiceProvider? _services;

    public static LeagueData Data { get; private set; } = new();

    /// <summary>
    /// Set the DI service provider so entity data can be synchronised with EF Core.
    /// Call this during app startup before <see cref="Load"/>.
    /// </summary>
    public static void SetServiceProvider(IServiceProvider services)
    {
        _services = services;
    }

    private static void EnsureDataDirectory()
    {
        var dir = Path.GetDirectoryName(DataPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public static void Initialize()
    {
        EnsureDataDirectory();

        if (File.Exists(DataPath))
            Load();
        else
        {
            Data = new LeagueData();
            Save();
        }
    }

    public static void Save() => SaveCore(syncEntities: true, pushToCloud: true);

    /// <summary>
    /// Save only the JSON file without syncing entities to the database.
    /// Use when only non-entity data (e.g. CalendarEvents, CalendarSettings) has changed
    /// and entity tables (competitions, fixtures, etc.) should not be overwritten.
    /// </summary>
    public static void SaveJsonOnly() => SaveCore(syncEntities: false, pushToCloud: false);

    private static void SaveCore(bool syncEntities, bool pushToCloud)
    {
        EnsureDataDirectory();

        // Create undo snapshot before overwriting
        try
        {
            if (File.Exists(DataPath))
                File.Copy(DataPath, BackupPath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataStore.Save] Backup snapshot failed: {ex.Message}");
        }

        var json = JsonSerializer.Serialize(Data, JsonOpts);
        File.WriteAllText(DataPath, json);

        if (syncEntities)
        {
            // Push entity changes to EF Core so both stores stay in sync
            SyncEntitiesToDatabase();
        }

        // Auto-backup every N saves
        var count = System.Threading.Interlocked.Increment(ref _saveCount);
        if (count % AutoBackupInterval == 0)
        {
            try
            {
                var backupService = new Wdpl2.Services.BackupService();
                _ = backupService.CreateBackupAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataStore.Save] Auto-backup failed: {ex.Message}");
            }
        }

        if (pushToCloud)
        {
            // Push to cloud if enabled (fire-and-forget)
            PushToCloudIfEnabled();
        }
    }

    /// <summary>
    /// Push the current data to the cloud (GitHub repo) if cloud sync is enabled.
    /// Runs as fire-and-forget so Save() remains synchronous.
    /// </summary>
    private static void PushToCloudIfEnabled()
    {
        try
        {
            var settings = Data.WebsiteSettings;
            if (!settings.EnableCloudSync) return;
            if (string.IsNullOrWhiteSpace(settings.GitHubToken) ||
                string.IsNullOrWhiteSpace(settings.GitHubUsername) ||
                string.IsNullOrWhiteSpace(settings.GitHubRepoName))
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    using var sync = new CloudSyncService(
                        settings.GitHubToken, settings.GitHubUsername, settings.GitHubRepoName);
                    var (success, message) = await sync.PushAsync(Data);
                    if (success)
                    {
                        settings.LastCloudSyncUtc = DateTime.UtcNow;
                        System.Diagnostics.Debug.WriteLine($"[CloudSync] Auto-push succeeded");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[CloudSync] Auto-push failed: {message}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CloudSync] Auto-push error: {ex}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CloudSync] Auto-push setup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Manually pull league data from the cloud and replace local data.
    /// Returns (success, message).
    /// </summary>
    public static async Task<(bool success, string message)> PullFromCloudAsync(IProgress<string>? progress = null)
    {
        var settings = Data.WebsiteSettings;
        if (string.IsNullOrWhiteSpace(settings.GitHubToken) ||
            string.IsNullOrWhiteSpace(settings.GitHubUsername) ||
            string.IsNullOrWhiteSpace(settings.GitHubRepoName))
        {
            return (false, "GitHub credentials not configured. Set them in Deployment Settings.");
        }

        // Create a backup before pulling
        CreatePreImportSnapshot();

        using var sync = new CloudSyncService(
            settings.GitHubToken, settings.GitHubUsername, settings.GitHubRepoName);

        var (success, message, data) = await sync.PullAsync(progress);
        if (!success || data == null)
        {
            ClearPreImportSnapshot();
            return (false, message);
        }

        // Preserve local credentials (the cloud copy has them stripped out)
        data.WebsiteSettings.GitHubToken = settings.GitHubToken;
        data.WebsiteSettings.GitHubUsername = settings.GitHubUsername;
        data.WebsiteSettings.GitHubRepoName = settings.GitHubRepoName;
        data.WebsiteSettings.EnableCloudSync = settings.EnableCloudSync;
        data.WebsiteSettings.LastCloudSyncUtc = DateTime.UtcNow;
        data.WebsiteSettings.FtpHost = settings.FtpHost;
        data.WebsiteSettings.FtpUsername = settings.FtpUsername;
        data.WebsiteSettings.FtpPassword = settings.FtpPassword;
        data.WebsiteSettings.FormServiceApiToken = settings.FormServiceApiToken;
        data.WebsiteSettings.FormServiceUrl = settings.FormServiceUrl;
        data.WebsiteSettings.FormServiceFetchUrl = settings.FormServiceFetchUrl;

        // Replace local data
        Data = data;

        // Persist locally
        EnsureDataDirectory();
        var json = JsonSerializer.Serialize(Data, JsonOpts);
        File.WriteAllText(DataPath, json);
        SyncEntitiesToDatabase();

        ClearPreImportSnapshot();
        return (true, "League data pulled from cloud and loaded successfully.");
    }

    /// <summary>
    /// Revert to the state before the last Save() call.
    /// Returns true if undo was successful.
    /// </summary>
    public static bool UndoLastSave()
    {
        try
        {
            if (!File.Exists(BackupPath)) return false;

            File.Copy(BackupPath, DataPath, overwrite: true);

            // Load settings from the restored JSON
            var json = File.ReadAllText(DataPath);
            Data = JsonSerializer.Deserialize<LeagueData>(json, JsonOpts) ?? new LeagueData();

            // Push the restored entity state to EF Core, then refresh from it
            SyncEntitiesToDatabase();
            RefreshEntitiesFromDatabase();

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Create a snapshot of the current data before an import operation.
    /// Call this before any import to enable rollback on failure.
    /// Returns true if the snapshot was created successfully.
    /// </summary>
    public static bool CreatePreImportSnapshot()
    {
        try
        {
            EnsureDataDirectory();
            var json = JsonSerializer.Serialize(Data, JsonOpts);
            File.WriteAllText(ImportSnapshotPath, json);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create pre-import snapshot: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restore data from the pre-import snapshot, undoing all changes made during a failed import.
    /// Returns true if the restore was successful.
    /// </summary>
    public static bool RestorePreImportSnapshot()
    {
        try
        {
            if (!File.Exists(ImportSnapshotPath)) return false;

            var json = File.ReadAllText(ImportSnapshotPath);
            Data = JsonSerializer.Deserialize<LeagueData>(json, JsonOpts) ?? new LeagueData();

            // Also restore the persisted file so a restart doesn't load partial import data
            File.WriteAllText(DataPath, json);

            // Push restored entities to EF Core
            SyncEntitiesToDatabase();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore pre-import snapshot: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Clean up the pre-import snapshot after a successful import.
    /// </summary>
    public static void ClearPreImportSnapshot()
    {
        try
        {
            if (File.Exists(ImportSnapshotPath))
                File.Delete(ImportSnapshotPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataStore.ClearPreImportSnapshot] {ex.Message}");
        }
    }

    /// <summary>
    /// Validate that a file is suitable for import (exists, readable, not too large).
    /// Returns (isValid, errorMessage).
    /// </summary>
    public static (bool isValid, string? error) ValidateImportFile(string filePath, long maxSizeBytes = 100 * 1024 * 1024)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return (false, "No file path specified.");

        if (!File.Exists(filePath))
            return (false, $"File not found: {Path.GetFileName(filePath)}");

        try
        {
            var fileInfo = new FileInfo(filePath);

            if (fileInfo.Length == 0)
                return (false, $"File is empty: {Path.GetFileName(filePath)}");

            if (fileInfo.Length > maxSizeBytes)
                return (false, $"File is too large ({fileInfo.Length / (1024 * 1024)} MB). Maximum supported size is {maxSizeBytes / (1024 * 1024)} MB.");

            // Verify we can read the file
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (true, null);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, $"Access denied: {Path.GetFileName(filePath)}. Check file permissions.");
        }
        catch (IOException ex)
        {
            return (false, $"Cannot read file: {ex.Message}");
        }
    }

    public static void Load()
    {
        try
        {
            EnsureDataDirectory();

            if (!File.Exists(DataPath))
            {
                Data = new LeagueData();
            }
            else
            {
                var json = File.ReadAllText(DataPath);
                Data = JsonSerializer.Deserialize<LeagueData>(json, JsonOpts) ?? new LeagueData();
            }

            // Overlay entity collections from EF Core (source of truth after migration)
            RefreshEntitiesFromDatabase();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataStore.Load] Primary load failed: {ex.Message}. Attempting backup recovery.");

            // Attempt to recover from the last good backup snapshot before giving up
            try
            {
                if (File.Exists(BackupPath))
                {
                    var backupJson = File.ReadAllText(BackupPath);
                    Data = JsonSerializer.Deserialize<LeagueData>(backupJson, JsonOpts) ?? new LeagueData();
                    System.Diagnostics.Debug.WriteLine("[DataStore.Load] Restored from BackupPath snapshot.");
                    RefreshEntitiesFromDatabase();
                    return;
                }
            }
            catch (Exception backupEx)
            {
                System.Diagnostics.Debug.WriteLine($"[DataStore.Load] Backup recovery also failed: {backupEx.Message}");
            }

            Data = new LeagueData();
        }
    }

    /// <summary>
    /// Reload entity collections from the EF Core database into <see cref="Data"/>.
    /// Settings, WebsiteSettings, and other non-entity data are preserved.
    /// </summary>
    public static void RefreshEntitiesFromDatabase()
    {
        if (_services == null) return;

        try
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LeagueContext>();

            // Preserve JSON-only Season properties that EF Core ignores
            var titlesBySeasonId = Data.Seasons.ToDictionary(s => s.Id, s => s.BlackoutDateTitles);
            var settingsBySeasonId = Data.Seasons
                .Where(s => s.Settings != null)
                .ToDictionary(s => s.Id, s => s.Settings);

            Data.Seasons = context.Seasons.AsNoTracking().ToList();

            // Restore JSON-only properties lost during EF Core load
            foreach (var season in Data.Seasons)
            {
                if (titlesBySeasonId.TryGetValue(season.Id, out var titles))
                    season.BlackoutDateTitles = titles;
                if (settingsBySeasonId.TryGetValue(season.Id, out var settings))
                    season.Settings = settings;
            }

            Data.Divisions = context.Divisions.AsNoTracking().ToList();
            Data.Teams = context.Teams.AsNoTracking().ToList();
            Data.Players = context.Players.AsNoTracking().ToList();
            Data.Venues = context.Venues.AsNoTracking().ToList();
            Data.Fixtures = context.Fixtures.AsNoTracking().ToList();
            Data.Competitions = context.Competitions.AsNoTracking().ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshEntitiesFromDatabase failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Push the current entity collections from <see cref="Data"/> into the EF Core database.
    /// Uses delete-all + re-insert within a transaction to avoid complex diff logic.
    /// </summary>
    private static void SyncEntitiesToDatabase()
    {
        if (_services == null) return;

        try
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LeagueContext>();

            using var transaction = context.Database.BeginTransaction();

            // Delete in child-first order to respect FK constraints
            context.Database.ExecuteSqlRaw("DELETE FROM Competitions");
            context.Database.ExecuteSqlRaw("DELETE FROM Fixtures");
            context.Database.ExecuteSqlRaw("DELETE FROM Players");
            context.Database.ExecuteSqlRaw("DELETE FROM Teams");
            context.Database.ExecuteSqlRaw("DELETE FROM Venues");
            context.Database.ExecuteSqlRaw("DELETE FROM Divisions");
            context.Database.ExecuteSqlRaw("DELETE FROM Seasons");

            // Clear the tracker so Add doesn't conflict with stale entries
            context.ChangeTracker.Clear();

            // Re-insert in parent-first order
            context.Seasons.AddRange(Data.Seasons);
            context.Divisions.AddRange(Data.Divisions);
            context.Venues.AddRange(Data.Venues);
            context.Teams.AddRange(Data.Teams);
            context.Players.AddRange(Data.Players);
            context.Fixtures.AddRange(Data.Fixtures);
            context.Competitions.AddRange(Data.Competitions);

            context.SaveChanges();
            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SyncEntitiesToDatabase failed: {ex.Message}");
        }
    }
}
