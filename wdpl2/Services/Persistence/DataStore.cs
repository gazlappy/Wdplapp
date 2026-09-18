using System.Text;
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

    /// <summary>Where the league is stored, for the few callers that need the file itself.</summary>
    public static string DataFilePath => DataPath;

    /// <summary>
    /// Writes a file so that it is either wholly the old one or wholly the new.
    /// </summary>
    /// <remarks>
    /// Written beside the real file and then renamed over it. A rename cannot
    /// happen by halves, so whatever stops the app - Android killing it for
    /// taking too long to start, a laptop lid closing, a power cut - the file
    /// on disk is one complete version or the other, never twenty megabytes
    /// that stop in the middle of a fixture.
    /// <para>
    /// This is not hypothetical: testing on Android left a backup of the league
    /// truncated at 3.9 MB of 21.8 MB, killed partway through the copy. It was
    /// the backup that time. The next moment it would have been the league.
    /// </para>
    /// </remarks>
    private static void WriteAtomic(string path, string contents)
    {
        var temp = path + ".writing";

        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write(contents);
            writer.Flush();

            // On disk, not merely handed to the operating system: a rename that
            // beats the data to the platter would leave an empty file behind.
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Copies a file the same way, so a half-written copy is never left.</summary>
    private static void CopyAtomic(string from, string to)
    {
        var temp = to + ".writing";

        File.Copy(from, temp, overwrite: true);
        File.Move(temp, to, overwrite: true);
    }
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
                CopyAtomic(DataPath, BackupPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataStore.Save] Backup snapshot failed: {ex.Message}");
        }

        var json = JsonSerializer.Serialize(Data, JsonOpts);
        WriteAtomic(DataPath, json);

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
        WriteAtomic(DataPath, json);
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

            CopyAtomic(BackupPath, DataPath);

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
            WriteAtomic(ImportSnapshotPath, json);
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
            WriteAtomic(DataPath, json);

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

            // The database is about to overwrite what was just read, so if it
            // has fallen behind the file this is the only moment the file's
            // version still exists to put it right from. A failed save leaves
            // exactly that state, and without this the next load would copy the
            // stale database over the good file and the work would be gone.
            RepairDatabaseIfBehind();

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
    /// <summary>
    /// Why the database copy was last left behind, or null if it is current.
    /// </summary>
    /// <remarks>
    /// This used to be swallowed, and it mattered: one competition round with a
    /// null draw order made Entity Framework refuse the whole transaction, and
    /// because the transaction carries every table, every save after that wrote
    /// the JSON file and silently left the database on the previous data. The
    /// app reads the database for players, fixtures and competitions, so the
    /// screen stopped agreeing with the file and nothing said why.
    /// </remarks>
    public static string? LastSyncError { get; private set; }

    /// <summary>
    /// Brings the database copy level with the file if it has fallen behind.
    /// </summary>
    /// <remarks>
    /// The JSON file is the record; the database is a copy of it that the app
    /// reads for players, fixtures and competitions. A sync that failed - and
    /// they failed silently for a long while - leaves the two disagreeing, and
    /// nothing else ever notices, because every page reads only one of them.
    /// <para>
    /// Compared on counts rather than contents so the check costs a handful of
    /// COUNT queries: the row counts of each table, plus how many players carry
    /// a cross-season link, which is the field a failed sync was last seen to
    /// lose. Worth running on the way in, off the UI thread, because the cost
    /// of being wrong is every career statistic in the app being stale.
    /// </para>
    /// </remarks>
    public static void RepairDatabaseIfBehind()
    {
        if (_services == null) return;

        try
        {
            string? behind = null;

            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<LeagueContext>();

                void Check(string what, int inDatabase, int inFile)
                {
                    if (behind is null && inDatabase != inFile)
                        behind = $"{what}: database {inDatabase}, file {inFile}";
                }

                Check("seasons", context.Seasons.Count(), Data.Seasons.Count);
                Check("divisions", context.Divisions.Count(), Data.Divisions.Count);
                Check("venues", context.Venues.Count(), Data.Venues.Count);
                Check("teams", context.Teams.Count(), Data.Teams.Count);
                Check("players", context.Players.Count(), Data.Players.Count);
                Check("fixtures", context.Fixtures.Count(), Data.Fixtures.Count);
                Check("competitions", context.Competitions.Count(), Data.Competitions.Count);

                // The field a failed sync was last seen to lose, and the one
                // nothing else would notice the absence of.
                Check("linked players",
                    context.Players.Count(p => p.GlobalPlayerId != null),
                    Data.Players.Count(p => p.GlobalPlayerId != null));
            }

            if (behind is null) return;

            System.Diagnostics.Debug.WriteLine($"[DataStore] Database is behind the file ({behind}). Rebuilding.");
            SyncEntitiesToDatabase();
        }
        catch (Exception ex)
        {
            LastSyncError = ex.Message;
            System.Diagnostics.Debug.WriteLine($"RepairDatabaseIfBehind failed: {ex.Message}");
        }
    }

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

            LastSyncError = null;
        }
        catch (Exception ex)
        {
            // Kept rather than rethrown: the JSON file is written by now, so the
            // work is not lost, and throwing here would turn a stale screen into
            // a crash. But it is no longer a secret.
            LastSyncError = ex.InnerException?.Message ?? ex.Message;
            System.Diagnostics.Debug.WriteLine($"SyncEntitiesToDatabase failed: {LastSyncError}");
        }
    }
}
