using System;
using System.IO;
using System.IO.Compression;
using Wdpl2.Services;
using Xunit;

namespace Wdpl2.Tests.Services;

/// <summary>
/// Restoring a backup, on throwaway folders: a restore that goes wrong must
/// leave the league exactly as it was.
/// </summary>
public sealed class BackupFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "backup-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _appData;
    private readonly string _database;
    private readonly string _league;

    public BackupFilesTests()
    {
        _appData = Path.Combine(_root, "Data");
        _database = Path.Combine(_appData, "league.db");
        _league = Path.Combine(_appData, "wdpl2", "data.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_league)!);
        File.WriteAllText(_database, "current db");
        File.WriteAllText(_league, "current league");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private string Zip(params (string Name, string Text)[] entries)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, text) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(text);
        }
        return path;
    }

    [Fact]
    public void A_staged_backup_replaces_both_files_at_the_next_start()
    {
        BackupFiles.Stage(Zip(("league.db", "old db"), ("data.json", "old league")), _appData);

        Assert.True(BackupFiles.HasPending(_appData));
        Assert.Equal("current league", File.ReadAllText(_league));

        Assert.True(BackupFiles.ApplyPending(_appData, _database, _league));

        Assert.Equal("old db", File.ReadAllText(_database));
        Assert.Equal("old league", File.ReadAllText(_league));
        Assert.False(BackupFiles.HasPending(_appData));
        Assert.Empty(Directory.GetFiles(_appData, "*.restoring", SearchOption.AllDirectories));
    }

    [Fact]
    public void Nothing_queued_changes_nothing()
    {
        Assert.False(BackupFiles.ApplyPending(_appData, _database, _league));
        Assert.Equal("current db", File.ReadAllText(_database));
    }

    [Fact]
    public void A_zip_without_a_league_is_refused_when_chosen()
    {
        var notABackup = Zip(("holiday.jpg", "..."));

        Assert.Throws<InvalidDataException>(() => BackupFiles.Stage(notABackup, _appData));
        Assert.False(BackupFiles.HasPending(_appData));
    }

    [Fact]
    public void A_file_that_is_not_a_zip_is_refused_when_chosen()
    {
        var text = Path.Combine(_root, "notes.zip");
        File.WriteAllText(text, "not a zip at all");

        Assert.ThrowsAny<InvalidDataException>(() => BackupFiles.Stage(text, _appData));
        Assert.False(BackupFiles.HasPending(_appData));
    }

    [Fact]
    public void A_pending_restore_that_turns_out_unreadable_leaves_the_league_alone()
    {
        BackupFiles.Stage(Zip(("league.db", "old db"), ("data.json", "old league")), _appData);

        // Damaged between choosing it and the next start.
        File.WriteAllText(Path.Combine(_appData, BackupFiles.PendingName), "damaged");

        Assert.ThrowsAny<Exception>(() => BackupFiles.ApplyPending(_appData, _database, _league));

        Assert.Equal("current db", File.ReadAllText(_database));
        Assert.Equal("current league", File.ReadAllText(_league));
        Assert.Empty(Directory.GetFiles(_appData, "*.restoring", SearchOption.AllDirectories));
    }

    [Fact]
    public void A_backup_holding_only_the_league_file_leaves_the_database_as_it_is()
    {
        BackupFiles.Stage(Zip(("data.json", "old league")), _appData);

        BackupFiles.ApplyPending(_appData, _database, _league);

        Assert.Equal("current db", File.ReadAllText(_database));
        Assert.Equal("old league", File.ReadAllText(_league));
    }

    [Fact]
    public void A_cancelled_restore_is_forgotten()
    {
        BackupFiles.Stage(Zip(("league.db", "old db")), _appData);
        BackupFiles.CancelPending(_appData);

        Assert.False(BackupFiles.ApplyPending(_appData, _database, _league));
        Assert.Equal("current db", File.ReadAllText(_database));
    }

    [Theory]
    [InlineData("auto_backup_20260922_101500.zip", BackupKind.Automatic)]
    [InlineData("manual_backup_20260922_101500.zip", BackupKind.Manual)]
    [InlineData("before-restore_20260922_101500.zip", BackupKind.BeforeRestore)]
    [InlineData("wdpl2_backup_20250101_120000.zip", BackupKind.OlderAutomatic)]
    [InlineData("league_backup_20250101_120000.zip", BackupKind.OlderAutomatic)]
    [InlineData("my league copy.zip", BackupKind.Manual)]
    public void Each_backup_is_known_by_its_name(string file, BackupKind kind) =>
        Assert.Equal(kind, BackupService.KindOf(file));
}
