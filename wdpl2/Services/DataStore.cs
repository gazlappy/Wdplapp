using System.Text.Json;
using Microsoft.Maui.Storage;
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

    private static int _saveCount;
    private const int AutoBackupInterval = 5;

    public static LeagueData Data { get; private set; } = new();

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
            Load();
            return true;
        }
        catch
        {
            return false;
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
                return;
            }

            var json = File.ReadAllText(DataPath);
            Data = JsonSerializer.Deserialize<LeagueData>(json, JsonOpts) ?? new LeagueData();
        }
        catch
        {
            Data = new LeagueData();
        }
    }
}
