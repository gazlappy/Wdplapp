using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using Wdpl2.Data;
using Wdpl2.Models;

namespace Wdpl2;

public static partial class DataStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string DataPath =
        Path.Combine(FileSystem.AppDataDirectory, "wdpl2", "data.json");

    private static readonly string BackupPath =
        Path.Combine(FileSystem.AppDataDirectory, "wdpl2", "data.json.bak");

    private static readonly string ImportSnapshotPath =
        Path.Combine(FileSystem.AppDataDirectory, "wdpl2", "data.json.pre-import");

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

    public static void Save()
    {
        EnsureDataDirectory();

        // Create undo snapshot before overwriting
        try
        {
            if (File.Exists(DataPath))
                File.Copy(DataPath, BackupPath, overwrite: true);
        }
        catch { /* non-critical */ }

        var json = JsonSerializer.Serialize(Data, JsonOpts);
        File.WriteAllText(DataPath, json);

        // Push entity changes to EF Core so both stores stay in sync
        SyncEntitiesToDatabase();

        // Auto-backup every N saves
        _saveCount++;
        if (_saveCount % AutoBackupInterval == 0)
        {
            try
            {
                var backupService = new Wdpl2.Services.BackupService();
                _ = backupService.CreateBackupAsync();
            }
            catch { /* non-critical */ }
        }
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
        catch { /* non-critical */ }
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
        catch
        {
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

            Data.Seasons = context.Seasons.AsNoTracking().ToList();
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
