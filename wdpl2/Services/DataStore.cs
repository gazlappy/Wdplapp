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
        EnsureDataDirectory();
        var json = JsonSerializer.Serialize(Data, JsonOpts);
        File.WriteAllText(DataPath, json);
    }

    public static void Load()
    {
        // Make Load resilient: ensure directory exists and file exists before reading
        EnsureDataDirectory();

        if (!File.Exists(DataPath))
        {
            Data = new LeagueData();
            return;
        }

        var json = File.ReadAllText(DataPath);
        Data = JsonSerializer.Deserialize<LeagueData>(json, JsonOpts) ?? new LeagueData();
    }
}
