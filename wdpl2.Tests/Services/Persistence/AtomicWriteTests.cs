using System.Text;

namespace wdpl2.Tests;

/// <summary>
/// The league file is either wholly the old one or wholly the new.
/// </summary>
/// <remarks>
/// Testing on Android killed the app partway through copying the league and
/// left a backup truncated at 3.9 MB of 21.8 MB. It was the backup that time;
/// the next moment it would have been the league itself. Being killed is
/// routine on a phone, so the write has to be arranged so that being killed
/// cannot leave a half a file behind.
/// <para>
/// These exercise the arrangement rather than the private method: write
/// through a temporary name, then rename over the real one. A rename cannot
/// happen by halves.
/// </para>
/// </remarks>
public class AtomicWriteTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "wdpl-atomic-" + Guid.NewGuid().ToString("N")[..8]);

    public AtomicWriteTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    /// <summary>The arrangement under test, as DataStore performs it.</summary>
    private static void WriteAtomic(string path, string contents)
    {
        var temp = path + ".writing";

        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write(contents);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }

    [Fact]
    public void ANewFileIsWrittenWhole()
    {
        var path = Path.Combine(_folder, "data.json");

        WriteAtomic(path, "{\"Players\":[]}");

        Assert.Equal("{\"Players\":[]}", File.ReadAllText(path));
    }

    [Fact]
    public void WritingOverAFileReplacesItCompletely()
    {
        var path = Path.Combine(_folder, "data.json");
        var big = new string('x', 2_000_000);

        WriteAtomic(path, big);
        WriteAtomic(path, "small");

        // Not the new content followed by the tail of the old, which is what a
        // plain overwrite of a shorter payload risks on some filesystems.
        Assert.Equal("small", File.ReadAllText(path));
    }

    /// <summary>
    /// Killed partway through, the old file is still entirely there.
    /// </summary>
    /// <remarks>
    /// The kill is simulated by doing what the write does and then stopping
    /// before the rename - which is the only window a crash could land in.
    /// </remarks>
    [Fact]
    public void InterruptedPartwayThrough_TheOldFileIsUntouched()
    {
        var path = Path.Combine(_folder, "data.json");
        var original = new string('o', 500_000);

        WriteAtomic(path, original);

        // Everything the save does, up to but not including the rename.
        var temp = path + ".writing";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(new string('n', 250_000));
            writer.Flush();
        }

        // Stopped here. The league is exactly as it was.
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Equal(500_000, new FileInfo(path).Length);
    }

    /// <summary>
    /// A leftover part-written file from a previous kill is not mistaken for data.
    /// </summary>
    [Fact]
    public void ALeftoverTemporaryFileIsOverwrittenNotAppendedTo()
    {
        var path = Path.Combine(_folder, "data.json");
        var temp = path + ".writing";

        File.WriteAllText(temp, new string('j', 900_000));
        WriteAtomic(path, "clean");

        Assert.Equal("clean", File.ReadAllText(path));
        Assert.False(File.Exists(temp), "the temporary file should have become the real one");
    }

    [Fact]
    public void TheFileIsWrittenWithoutAByteOrderMark()
    {
        var path = Path.Combine(_folder, "data.json");

        WriteAtomic(path, "{}");

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { (byte)'{', (byte)'}' }, bytes);
    }
}
