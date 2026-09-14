using Wdpl2.Services.Cloud;

namespace wdpl2.Tests;

public class BackendDeploymentManifestTests
{
    [Fact]
    public void DeploymentManifest_ContainsAllRuntimeBackendFilesAndNoMissingPaths()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "wdpl2.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var root = Path.Combine(directory.FullName, "wdpl2", "web-backend");
        var expected = new[] { "api", "captain" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*", SearchOption.AllDirectories))
            .Where(path => new[] { ".php", ".html", ".js", ".webmanifest" }.Contains(Path.GetExtension(path)) || Path.GetFileName(path) == ".htaccess")
            .Where(path => Path.GetFileName(path) != "_db.config.php")
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var actual = BackendDeployService.BundledFiles.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
        Assert.Contains("api/_scorecard_journal.php", actual);
    }
}
