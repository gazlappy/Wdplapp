using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services.Import;

namespace Wdpl2;

public static partial class DataStore
{
    internal static void WriteImportMetadata(LeagueData baseline, LeagueData imported)
    {
        var next = ImportWorkspace.Clone(Data);
        if (!ImportWorkspace.Equal(baseline.DoublesPairings, imported.DoublesPairings))
            next.DoublesPairings = ImportWorkspace.Clone(imported.DoublesPairings);
        foreach (var season in imported.Seasons)
        {
            var old = baseline.Seasons.FirstOrDefault(s => s.Id == season.Id);
            if (old != null && ImportWorkspace.Equal(old.Settings, season.Settings) &&
                ImportWorkspace.Equal(old.BlackoutDateTitles, season.BlackoutDateTitles)) continue;
            var target = next.Seasons.FirstOrDefault(s => s.Id == season.Id);
            if (target == null)
            {
                target = ImportWorkspace.Clone(season);
                next.Seasons.Add(target);
            }
            target.Settings = ImportWorkspace.Clone(season.Settings);
            target.BlackoutDateTitles = ImportWorkspace.Clone(season.BlackoutDateTitles);
        }
        EnsureDataDirectory();
        var temporary = DataPath + ".import.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(next, JsonOpts));
            File.Move(temporary, DataPath, true);
            Data = next;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
