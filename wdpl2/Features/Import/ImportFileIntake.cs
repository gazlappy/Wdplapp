using System.IO.Compression;
using System.Security.Cryptography;

namespace Wdpl2.Services.Import;

public enum ImportFileKind { Access, Paradox, Word, Spreadsheet, Html, Pdf, Sql }

public sealed record PreparedImportFile(string FileName, string FilePath, ImportFileKind Kind, long Length, string Hash);

public sealed class ImportFileIntake : IDisposable
{
    public const long MaxFileBytes = 100L * 1024 * 1024;
    public const long MaxBatchBytes = 500L * 1024 * 1024;
    public const int MaxFiles = 200;
    private readonly string _directory;
    private readonly List<PreparedImportFile> _files = [];
    public IReadOnlyList<PreparedImportFile> Files => _files;

    public ImportFileIntake(string cacheDirectory)
    {
        _directory = Path.Combine(cacheDirectory, "imports", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public static ImportFileKind DetectKind(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".mdb" or ".accdb" => ImportFileKind.Access,
        ".db" => ImportFileKind.Paradox,
        ".docx" or ".doc" => ImportFileKind.Word,
        ".xlsx" or ".csv" => ImportFileKind.Spreadsheet,
        ".html" or ".htm" => ImportFileKind.Html,
        ".pdf" => ImportFileKind.Pdf,
        ".sql" => ImportFileKind.Sql,
        ".xls" => throw new InvalidDataException("Legacy Excel .xls files must be saved as .xlsx or .csv first."),
        _ => throw new InvalidDataException("Unsupported file type. Choose Access, Paradox, Word, XLSX, CSV, HTML, PDF, or SQL files.")
    };

    public async Task<PreparedImportFile> AddAsync(string fileName, Stream source, CancellationToken ct = default)
    {
        var kind = DetectKind(fileName);
        if (_files.Count >= MaxFiles) throw new InvalidDataException($"Select no more than {MaxFiles} files at a time.");
        if (kind == ImportFileKind.Paradox)
            throw new InvalidDataException("Paradox databases need their companion tables. Choose the Paradox folder option instead of individual .DB files.");
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        var folder = Path.Combine(_directory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, safeName);
        try
        {
            long length = 0;
            var remaining = MaxBatchBytes - _files.Sum(f => f.Length);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                int count;
                while ((count = await source.ReadAsync(buffer.AsMemory(), ct)) != 0)
                {
                    length += count;
                    if (length > MaxFileBytes || length > remaining)
                        throw new InvalidDataException("Import limit exceeded: 100 MB per file and 500 MB per selection.");
                    hash.AppendData(buffer, 0, count);
                    await destination.WriteAsync(buffer.AsMemory(0, count), ct);
                }
            }
            if (length == 0) throw new InvalidDataException("The selected file is empty.");
            var digest = Convert.ToHexString(hash.GetHashAndReset());
            if (_files.Any(f => f.Hash == digest)) throw new InvalidDataException("This file's contents are already selected.");
            ValidateContainer(path, kind);
            var file = new PreparedImportFile(safeName, path, kind, length, digest);
            _files.Add(file);
            return file;
        }
        catch
        {
            Directory.Delete(folder, true);
            throw;
        }
    }

    private static void ValidateContainer(string path, ImportFileKind kind)
    {
        if (Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(path);
            if (zip.Entries.Count > 10000 || zip.Entries.Sum(e => (decimal)e.Length) > MaxBatchBytes)
                throw new InvalidDataException("The document expands beyond the safe import limit.");
            var required = kind == ImportFileKind.Word ? "word/document.xml" : "xl/workbook.xml";
            if (zip.GetEntry(required) == null) throw new InvalidDataException("The document contents do not match its file extension.");
        }
        if (kind == ImportFileKind.Pdf)
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[5];
            if (stream.Read(header) != 5 || !header.SequenceEqual("%PDF-"u8))
                throw new InvalidDataException("The file is not a valid PDF document.");
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, true); }
        catch (IOException ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        catch (UnauthorizedAccessException ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        _files.Clear();
    }
}
