using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Puts the players who were already here into capitals, once.
/// </summary>
/// <remarks>
/// A player's name is stored in capitals from the moment it is set - see
/// <see cref="Player"/> - so anybody arriving from now on needs nothing doing
/// to them. This is only for the ones written before that was true.
/// <para>
/// Everything here reads the stored file rather than what is in memory,
/// because loading has already put the names into capitals: by the time the
/// app can be asked, it can no longer tell which ones were not.
/// </para>
/// </remarks>
public static class PlayerNameCase
{
    /// <summary>How many stored players are written in something other than capitals.</summary>
    public static int PendingInStoredFile()
    {
        try
        {
            return StoredNames().Count(n => n != n.ToUpperInvariant());
        }
        catch
        {
            // A file that cannot be read is not a count of zero, but there is
            // nothing useful to say here and the caller only offers the work.
            return 0;
        }
    }

    /// <summary>
    /// Takes a dated copy of the data file, then saves the names in capitals.
    /// </summary>
    /// <returns>Where the previous data was put.</returns>
    /// <remarks>
    /// The copy is the whole point. Flattening the case throws the original
    /// away - nothing can tell afterwards whether MCLOUGHLIN was McLoughlin or
    /// Mcloughlin - so the only way back is a file that still has it.
    /// </remarks>
    public static string BackUpAndApply()
    {
        var path = DataStore.DataFilePath;
        var backup = path + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".before-capitals";

        if (File.Exists(path))
        {
            // Through a temporary name, so a copy that is interrupted leaves no
            // backup at all rather than a truncated one that looks like a backup.
            var writing = backup + ".writing";
            File.Copy(path, writing, overwrite: true);
            File.Move(writing, backup, overwrite: true);
        }

        // The names in memory are already in capitals, because loading them put
        // them there. Saving is the whole of the work.
        DataStore.Save();

        return backup;
    }

    private static List<string> StoredNames()
    {
        var path = DataStore.DataFilePath;
        if (!File.Exists(path)) return new List<string>();

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("Players", out var players)
            || players.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        var names = new List<string>();

        foreach (var player in players.EnumerateArray())
        {
            foreach (var field in new[] { "FirstName", "LastName" })
            {
                if (player.TryGetProperty(field, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && value.GetString() is { Length: > 0 } text)
                {
                    names.Add(text);
                }
            }
        }

        return names;
    }
}
