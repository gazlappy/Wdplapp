using System;
using System.IO;

namespace Wdpl2.Services;

/// <summary>
/// Where the league is kept on disk.
/// </summary>
/// <remarks>
/// Normally the app's own data folder. Setting the LEAGUE_DATA_DIR environment
/// variable points the app at another folder instead - an empty one to see the
/// app as a new customer does, or a copy of a real league for the smoke test,
/// without either touching the league everyone else is using.
/// </remarks>
public static class AppPaths
{
    public const string OverrideVariable = "LEAGUE_DATA_DIR";

    private static readonly Lazy<string> _data = new(() =>
    {
        var chosen = Environment.GetEnvironmentVariable(OverrideVariable);
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            Directory.CreateDirectory(chosen);
            return chosen;
        }
        return FileSystem.AppDataDirectory;
    });

    /// <summary>The folder holding the database, the league file and the backups.</summary>
    public static string Data => _data.Value;

    public static string Database => Path.Combine(Data, "league.db");

    public static string LeagueFile => Path.Combine(Data, "wdpl2", "data.json");
}
