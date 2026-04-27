using System.IO.Compression;
using System.Text;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for LocalExportService — local file and ZIP export operations.
/// </summary>
public class LocalExportServiceTests : IDisposable
{
    private readonly LocalExportService _service;
    private readonly string _tempDir;

    public LocalExportServiceTests()
    {
        _service = new LocalExportService();
        _tempDir = Path.Combine(Path.GetTempPath(), $"LocalExportServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task ExportToFolderAsync_CreatesDirectory_Success()
    {
        // Arrange
        var outputFolder = Path.Combine(_tempDir, "output");
        var files = new Dictionary<string, string>
        {
            ["test.txt"] = "test content"
        };

        // Act
        var result = await _service.ExportToFolderAsync(files, outputFolder);

        // Assert
        Assert.True(result.success);
        Assert.Contains("Successfully exported", result.message);
        Assert.Equal(outputFolder, result.outputPath);
        Assert.True(Directory.Exists(outputFolder));
        Assert.True(File.Exists(Path.Combine(outputFolder, "test.txt")));
    }

    [Fact]
    public async Task ExportToFolderAsync_ExistingDirectory_Success()
    {
        // Arrange
        var outputFolder = Path.Combine(_tempDir, "existing");
        Directory.CreateDirectory(outputFolder);
        var files = new Dictionary<string, string>
        {
            ["file.html"] = "<html></html>"
        };

        // Act
        var result = await _service.ExportToFolderAsync(files, outputFolder);

        // Assert
        Assert.True(result.success);
        Assert.Equal(outputFolder, result.outputPath);
    }

    [Fact]
    public async Task ExportToFolderAsync_CreatesSubdirectories_Success()
    {
        // Arrange
        var outputFolder = Path.Combine(_tempDir, "withsubs");
        var files = new Dictionary<string, string>
        {
            ["css/style.css"] = "body {}",
            ["js/script.js"] = "console.log();",
            ["index.html"] = "<html></html>"
        };

        // Act
        var result = await _service.ExportToFolderAsync(files, outputFolder);

        // Assert
        Assert.True(result.success);
        Assert.Contains("3 files", result.message);
        Assert.True(File.Exists(Path.Combine(outputFolder, "css", "style.css")));
        Assert.True(File.Exists(Path.Combine(outputFolder, "js", "script.js")));
        Assert.True(File.Exists(Path.Combine(outputFolder, "index.html")));
    }

    [Fact]
    public async Task ExportToFolderAsync_MultipleFiles_WritesAllFiles()
    {
        // Arrange
        var outputFolder = Path.Combine(_tempDir, "multiple");
        var files = new Dictionary<string, string>
        {
            ["file1.txt"] = "content1",
            ["file2.txt"] = "content2",
            ["file3.txt"] = "content3"
        };

        // Act
        var result = await _service.ExportToFolderAsync(files, outputFolder);

        // Assert
        Assert.True(result.success);
        Assert.Equal("content1", await File.ReadAllTextAsync(Path.Combine(outputFolder, "file1.txt")));
        Assert.Equal("content2", await File.ReadAllTextAsync(Path.Combine(outputFolder, "file2.txt")));
        Assert.Equal("content3", await File.ReadAllTextAsync(Path.Combine(outputFolder, "file3.txt")));
    }

    [Fact]
    public async Task ExportToFolderAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var outputFolder = Path.Combine(_tempDir, "progress");
        var files = new Dictionary<string, string>
        {
            ["test.txt"] = "content"
        };
        var progressReports = new List<string>();
        var progress = new Progress<string>(msg => progressReports.Add(msg));

        // Act
        var result = await _service.ExportToFolderAsync(files, outputFolder, progress);

        // Assert
        Assert.True(result.success);
        Assert.Contains("Preparing export folder...", progressReports);
        Assert.Contains("Exporting test.txt (1/1)...", progressReports);
        Assert.Contains("Export complete!", progressReports);
    }

    [Fact]
    public async Task ExportToFolderAsync_InvalidPath_ReturnsFailure()
    {
        // Arrange
        var invalidPath = "Z:\\invalid\\path\\that\\does\\not\\exist\\and\\cannot\\be\\created\\due\\to\\invalid\\drive";
        var files = new Dictionary<string, string>
        {
            ["test.txt"] = "content"
        };

        // Act
        var result = await _service.ExportToFolderAsync(files, invalidPath);

        // Assert
        Assert.False(result.success);
        Assert.Contains("Export failed:", result.message);
        Assert.Null(result.outputPath);
    }

    [Fact]
    public async Task ExportAsZipAsync_CreatesZipFile_Success()
    {
        // Arrange
        var zipPath = Path.Combine(_tempDir, "test.zip");
        var files = new Dictionary<string, string>
        {
            ["test.txt"] = "test content"
        };

        // Act
        var result = await _service.ExportAsZipAsync(files, zipPath);

        // Assert
        Assert.True(result.success);
        Assert.Contains("Successfully created ZIP", result.message);
        Assert.Equal(zipPath, result.zipPath);
        Assert.True(File.Exists(zipPath));
    }

    [Fact]
    public async Task ExportAsZipAsync_CreatesDirectory_Success()
    {
        // Arrange
        var zipDir = Path.Combine(_tempDir, "zipdir");
        var zipPath = Path.Combine(zipDir, "archive.zip");
        var files = new Dictionary<string, string>
        {
            ["file.txt"] = "content"
        };

        // Act
        var result = await _service.ExportAsZipAsync(files, zipPath);

        // Assert
        Assert.True(result.success);
        Assert.True(Directory.Exists(zipDir));
        Assert.True(File.Exists(zipPath));
    }

    [Fact]
    public async Task ExportAsZipAsync_OverwritesExistingFile_Success()
    {
        // Arrange
        var zipPath = Path.Combine(_tempDir, "overwrite.zip");
        await File.WriteAllTextAsync(zipPath, "old content");
        var files = new Dictionary<string, string>
        {
            ["new.txt"] = "new content"
        };

        // Act
        var result = await _service.ExportAsZipAsync(files, zipPath);

        // Assert
        Assert.True(result.success);
        using var archive = ZipFile.OpenRead(zipPath);
        Assert.Single(archive.Entries);
        Assert.Equal("new.txt", archive.Entries[0].FullName);
    }

    [Fact]
    public async Task ExportAsZipAsync_MultipleFiles_IncludesAllFiles()
    {
        // Arrange
        var zipPath = Path.Combine(_tempDir, "multiple.zip");
        var files = new Dictionary<string, string>
        {
            ["file1.txt"] = "content1",
            ["file2.html"] = "<html></html>",
            ["css/style.css"] = "body {}"
        };

        // Act
        var result = await _service.ExportAsZipAsync(files, zipPath);

        // Assert
        Assert.True(result.success);
        Assert.Contains("3 files", result.message);
        using var archive = ZipFile.OpenRead(zipPath);
        Assert.Equal(3, archive.Entries.Count);
    }

    [Fact]
    public async Task ExportAsZipAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var zipPath = Path.Combine(_tempDir, "progress.zip");
        var files = new Dictionary<string, string>
        {
            ["test.txt"] = "content"
        };
        var progressReports = new List<string>();
        var progress = new Progress<string>(msg => progressReports.Add(msg));

        // Act
        var result = await _service.ExportAsZipAsync(files, zipPath, progress);

        // Assert
        Assert.True(result.success);
        Assert.Contains("Creating ZIP archive...", progressReports);
        Assert.Contains("Adding test.txt (1/1)...", progressReports);
        Assert.Contains("ZIP created successfully!", progressReports);
    }

    [Fact]
    public async Task ExportAsZipAsync_VerifiesContent_Success()
    {
        // Arrange
        var zipPath = Path.Combine(_tempDir, "verify.zip");
        var files = new Dictionary<string, string>
        {
            ["test.txt"] = "Hello World"
        };

        // Act
        var result = await _service.ExportAsZipAsync(files, zipPath);

        // Assert
        Assert.True(result.success);
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries[0];
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("Hello World", content);
    }

    [Fact]
    public async Task ExportAsZipAsync_InvalidPath_ReturnsFailure()
    {
        // Arrange
        var invalidPath = "Z:\\invalid\\path\\archive.zip";
        var files = new Dictionary<string, string>
        {
            ["test.txt"] = "content"
        };

        // Act
        var result = await _service.ExportAsZipAsync(files, invalidPath);

        // Assert
        Assert.False(result.success);
        Assert.Contains("ZIP export failed:", result.message);
        Assert.Null(result.zipPath);
    }

    [Fact]
    public async Task ExportToMemoryStreamAsync_CreatesZipInMemory_Success()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["test.txt"] = "test content"
        };

        // Act
        var result = await _service.ExportToMemoryStreamAsync(files);

        // Assert
        Assert.True(result.success);
        Assert.Contains("Created ZIP", result.message);
        Assert.NotNull(result.zipStream);
        Assert.True(result.zipStream.Length > 0);
        result.zipStream.Dispose();
    }

    [Fact]
    public async Task ExportToMemoryStreamAsync_MultipleFiles_IncludesAllFiles()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["file1.txt"] = "content1",
            ["file2.txt"] = "content2",
            ["file3.txt"] = "content3"
        };

        // Act
        var result = await _service.ExportToMemoryStreamAsync(files);

        // Assert
        Assert.True(result.success);
        Assert.Contains("3 files", result.message);
        Assert.NotNull(result.zipStream);
        using var archive = new ZipArchive(result.zipStream, ZipArchiveMode.Read, false);
        Assert.Equal(3, archive.Entries.Count);
    }

    [Fact]
    public async Task ExportToMemoryStreamAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["test.txt"] = "content"
        };
        var progressReports = new List<string>();
        var progress = new Progress<string>(msg => progressReports.Add(msg));

        // Act
        var result = await _service.ExportToMemoryStreamAsync(files, progress);

        // Assert
        Assert.True(result.success);
        Assert.Contains("Creating ZIP in memory...", progressReports);
        Assert.Contains("Adding test.txt (1/1)...", progressReports);
        Assert.Contains("ZIP created successfully!", progressReports);
        result.zipStream?.Dispose();
    }

    [Fact]
    public async Task ExportToMemoryStreamAsync_StreamPositionReset_Success()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["test.txt"] = "content"
        };

        // Act
        var result = await _service.ExportToMemoryStreamAsync(files);

        // Assert
        Assert.True(result.success);
        Assert.NotNull(result.zipStream);
        Assert.Equal(0, result.zipStream.Position);
        result.zipStream.Dispose();
    }

    [Fact]
    public async Task ExportToMemoryStreamAsync_VerifiesContent_Success()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["test.txt"] = "Hello Memory"
        };

        // Act
        var result = await _service.ExportToMemoryStreamAsync(files);

        // Assert
        Assert.True(result.success);
        Assert.NotNull(result.zipStream);
        using var archive = new ZipArchive(result.zipStream, ZipArchiveMode.Read, false);
        var entry = archive.Entries[0];
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("Hello Memory", content);
    }

    [Fact]
    public void GetDefaultExportFolder_ReturnsValidPath_Success()
    {
        // Act
        var result = LocalExportService.GetDefaultExportFolder();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("WDPL", result);
        Assert.Contains("Website", result);
    }

    [Fact]
    public void GetDefaultExportFolder_ContainsDocumentsPath_Success()
    {
        // Arrange
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // Act
        var result = LocalExportService.GetDefaultExportFolder();

        // Assert
        Assert.StartsWith(documentsPath, result);
        Assert.EndsWith(Path.Combine("WDPL", "Website"), result);
    }

    [Fact]
    public void GetDefaultZipPath_ReturnsValidPath_Success()
    {
        // Arrange
        var leagueName = "TestLeague";

        // Act
        var result = LocalExportService.GetDefaultZipPath(leagueName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("WDPL", result);
        Assert.Contains("TestLeague", result);
        Assert.EndsWith(".zip", result);
    }

    [Fact]
    public void GetDefaultZipPath_ContainsDocumentsPath_Success()
    {
        // Arrange
        var leagueName = "MyLeague";
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // Act
        var result = LocalExportService.GetDefaultZipPath(leagueName);

        // Assert
        Assert.StartsWith(documentsPath, result);
    }

    [Fact]
    public void GetDefaultZipPath_ContainsTimestamp_Success()
    {
        // Arrange
        var leagueName = "League";

        // Act
        var result = LocalExportService.GetDefaultZipPath(leagueName);

        // Assert
        Assert.Contains("Website_", result);
        Assert.Matches(@"\d{8}_\d{6}\.zip$", result);
    }

    [Fact]
    public void GetDefaultZipPath_SafeFileName_Success()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = LocalExportService.GetDefaultZipPath(leagueName);

        // Assert
        Assert.Contains("Test_League", result);
    }

    [Fact]
    public async Task ExportToFolderAsync_EmptyFiles_Success()
    {
        // Arrange
        var outputFolder = Path.Combine(_tempDir, "empty");
        var files = new Dictionary<string, string>();

        // Act
        var result = await _service.ExportToFolderAsync(files, outputFolder);

        // Assert
        Assert.True(result.success);
        Assert.Contains("0 files", result.message);
        Assert.Equal(outputFolder, result.outputPath);
    }

    [Fact]
    public async Task ExportAsZipAsync_EmptyFiles_Success()
    {
        // Arrange
        var zipPath = Path.Combine(_tempDir, "empty.zip");
        var files = new Dictionary<string, string>();

        // Act
        var result = await _service.ExportAsZipAsync(files, zipPath);

        // Assert
        Assert.True(result.success);
        Assert.Contains("0 files", result.message);
        Assert.True(File.Exists(zipPath));
    }

    [Fact]
    public async Task ExportToMemoryStreamAsync_EmptyFiles_Success()
    {
        // Arrange
        var files = new Dictionary<string, string>();

        // Act
        var result = await _service.ExportToMemoryStreamAsync(files);

        // Assert
        Assert.True(result.success);
        Assert.Contains("0 files", result.message);
        Assert.NotNull(result.zipStream);
        result.zipStream.Dispose();
    }

    [Fact]
    public async Task ExportToFolderAsync_EmptyContent_Success()
    {
        // Arrange
        var outputFolder = Path.Combine(_tempDir, "emptycontent");
        var files = new Dictionary<string, string>
        {
            ["empty.txt"] = ""
        };

        // Act
        var result = await _service.ExportToFolderAsync(files, outputFolder);

        // Assert
        Assert.True(result.success);
        var content = await File.ReadAllTextAsync(Path.Combine(outputFolder, "empty.txt"));
        Assert.Equal("", content);
    }

    [Fact]
    public async Task ExportAsZipAsync_EmptyContent_Success()
    {
        // Arrange
        var zipPath = Path.Combine(_tempDir, "emptycontent.zip");
        var files = new Dictionary<string, string>
        {
            ["empty.txt"] = ""
        };

        // Act
        var result = await _service.ExportAsZipAsync(files, zipPath);

        // Assert
        Assert.True(result.success);
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries[0];
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("", content);
    }

    [Fact]
    public async Task ExportToMemoryStreamAsync_EmptyContent_Success()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["empty.txt"] = ""
        };

        // Act
        var result = await _service.ExportToMemoryStreamAsync(files);

        // Assert
        Assert.True(result.success);
        Assert.NotNull(result.zipStream);
        using var archive = new ZipArchive(result.zipStream, ZipArchiveMode.Read, false);
        Assert.Single(archive.Entries);
        var entry = archive.Entries[0];
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("", content);
    }
}
