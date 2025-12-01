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
        try
        {
            EnsureDataDirectory();
            
            System.Diagnostics.Debug.WriteLine($"=== DATASTORE SAVE DEBUG ===");
            System.Diagnostics.Debug.WriteLine($"Save path: {DataPath}");
            System.Diagnostics.Debug.WriteLine($"Divisions count: {Data.Divisions?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Teams count: {Data.Teams?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Players count: {Data.Players?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Seasons count: {Data.Seasons?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Fixtures count: {Data.Fixtures?.Count ?? 0}");
            
            var json = JsonSerializer.Serialize(Data, JsonOpts);
            System.Diagnostics.Debug.WriteLine($"JSON length: {json.Length} characters");
            
            File.WriteAllText(DataPath, json);
            
            System.Diagnostics.Debug.WriteLine($"File written successfully");
            System.Diagnostics.Debug.WriteLine($"File exists after write: {File.Exists(DataPath)}");
            System.Diagnostics.Debug.WriteLine($"File size: {new FileInfo(DataPath).Length} bytes");
            System.Diagnostics.Debug.WriteLine("=== END SAVE ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"!!! SAVE ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public static void Load()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== DATASTORE LOAD DEBUG ===");
            System.Diagnostics.Debug.WriteLine($"Load path: {DataPath}");
            
            // Make Load resilient: ensure directory exists and file exists before reading
            EnsureDataDirectory();

            if (!File.Exists(DataPath))
            {
                System.Diagnostics.Debug.WriteLine($"File does not exist - creating new LeagueData");
                Data = new LeagueData();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"File exists - reading...");
            System.Diagnostics.Debug.WriteLine($"File size: {new FileInfo(DataPath).Length} bytes");
            
            var json = File.ReadAllText(DataPath);
            System.Diagnostics.Debug.WriteLine($"Read {json.Length} characters");
            
            Data = JsonSerializer.Deserialize<LeagueData>(json, JsonOpts) ?? new LeagueData();
            
            System.Diagnostics.Debug.WriteLine($"After load:");
            System.Diagnostics.Debug.WriteLine($"  Divisions: {Data.Divisions?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"  Teams: {Data.Teams?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"  Players: {Data.Players?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"  Seasons: {Data.Seasons?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"  Fixtures: {Data.Fixtures?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine("=== END LOAD ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"!!! LOAD ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            Data = new LeagueData();
        }
    }
}
