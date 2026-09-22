using System;
using System.IO;
using System.IO.Compression;

namespace Wdpl2.Services;

/// <summary>
/// The file work behind restoring a backup, kept free of MAUI so it can be
/// tested against plain folders.
/// </summary>
/// <remarks>
/// A restore cannot be done while the league is open. The database file is
/// held by the app, and the league is also held in memory - so the next save
/// after an in-place restore would write the old league straight back over the
/// one just restored. Instead a restore is <see cref="Stage">staged</see>: the
/// backup is checked and copied aside, the app closes, and on the next start
/// <see cref="ApplyPending"/> puts it in place before anything opens the league.
/// </remarks>
internal static class BackupFiles
{
    public const string DatabaseEntry = "league.db";
    public const string LeagueEntry = "data.json";
    public const string PendingName = "restore-pending.zip";

    /// <summary>Checks a backup and queues it to be put in place at the next start.</summary>
    /// <exception cref="InvalidDataException">The file is not a backup this app made.</exception>
    public static void Stage(string backupZip, string appDataDir)
    {
        using (var archive = ZipFile.OpenRead(backupZip))
            Validate(archive);

        Directory.CreateDirectory(appDataDir);
        File.Copy(backupZip, PendingPath(appDataDir), overwrite: true);
    }

    public static bool HasPending(string appDataDir) => File.Exists(PendingPath(appDataDir));

    /// <summary>Forgets a queued restore without applying it.</summary>
    public static void CancelPending(string appDataDir)
    {
        var pending = PendingPath(appDataDir);
        if (File.Exists(pending)) File.Delete(pending);
    }

    /// <summary>
    /// Puts a queued backup in place. Nothing may have the league open.
    /// </summary>
    /// <returns>False when nothing was queued.</returns>
    /// <remarks>
    /// All or nothing, as far as two files can be: both are written out beside
    /// their targets first, and only once both are complete are they moved over
    /// the originals. A backup that turns out to be unreadable leaves the league
    /// exactly as it was.
    /// </remarks>
    public static bool ApplyPending(string appDataDir, string databasePath, string leaguePath)
    {
        var pending = PendingPath(appDataDir);
        if (!File.Exists(pending)) return false;

        var staged = new System.Collections.Generic.List<(string Temp, string Target)>();
        try
        {
            using (var archive = ZipFile.OpenRead(pending))
            {
                Validate(archive);
                foreach (var (entryName, target) in new[] { (DatabaseEntry, databasePath), (LeagueEntry, leaguePath) })
                {
                    var entry = archive.GetEntry(entryName);
                    if (entry is null) continue;

                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    var temp = target + ".restoring";
                    entry.ExtractToFile(temp, overwrite: true);
                    staged.Add((temp, target));
                }
            }

            foreach (var (temp, target) in staged)
                File.Move(temp, target, overwrite: true);
        }
        finally
        {
            foreach (var (temp, _) in staged)
                if (File.Exists(temp)) File.Delete(temp);
        }

        File.Delete(pending);
        return true;
    }

    private static string PendingPath(string appDataDir) => Path.Combine(appDataDir, PendingName);

    private static void Validate(ZipArchive archive)
    {
        if (archive.GetEntry(DatabaseEntry) is null && archive.GetEntry(LeagueEntry) is null)
            throw new InvalidDataException("That file is not a backup from this app - it holds no league.");
    }
}
