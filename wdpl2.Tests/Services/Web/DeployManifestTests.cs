using Wdpl2.Services.Web;
using System.Text.RegularExpressions;

namespace wdpl2.Tests;

/// <summary>
/// Guards the deploy manifest against the gap that actually bit us.
/// </summary>
/// <remarks>
/// <c>core/Passwords.php</c> was added to the backend but never listed in any
/// module's <see cref="IWebModule.ServerFiles"/>, so it was never uploaded. The
/// deployed backend then died on <c>require</c> at the first request.
/// <para>
/// Nothing caught it: local testing copied the whole folder rather than going
/// through the manifest, and <c>WebDeployService</c> can only verify that files
/// it was told about exist - it cannot know about one it was never told about.
/// These tests close that loop by comparing the manifest against the tree.
/// </para>
/// </remarks>
public class DeployManifestTests
{
    /// <summary>All modules this build would deploy.</summary>
    private static IReadOnlyList<IWebModule> Modules() => new IWebModule[]
    {
        new SystemWebModule(),
        new LeagueWebModule(),
        new CaptainsWebModule(),
        new ScorecardsWebModule(),
        new CompsWebModule(),
    };

    /// <summary>
    /// The app's declared schema version must match the PHP module's.
    /// </summary>
    /// <remarks>
    /// Nothing reads the C# number today, which is exactly how it drifted to 1
    /// while three PHP modules moved to 3. A number that is never checked and
    /// never right is worse than no number, so it is checked here.
    /// </remarks>
    [Fact]
    public void SchemaVersions_MatchTheServerModules()
    {
        var root = RepoRoot();

        foreach (var module in Modules())
        {
            var php = Path.Combine(
                root.FullName, "wdpl2", "web-backend", "api", "modules", module.Id, "Module.php");

            Assert.True(File.Exists(php), $"{module.Id}: no Module.php at {php}");

            var match = Regex.Match(
                File.ReadAllText(php),
                @"function\s+schemaVersion\s*\(\s*\)\s*:\s*int\s*\{\s*return\s+(\d+)\s*;");

            Assert.True(match.Success, $"{module.Id}: could not read schemaVersion() from Module.php");

            Assert.True(
                int.Parse(match.Groups[1].Value) == module.SchemaVersion,
                $"{module.Id}: Module.php declares schema {match.Groups[1].Value}, "
                + $"but {module.GetType().Name} says {module.SchemaVersion}.");
        }
    }

    /// <summary>Walks up from the test binary to the repository root.</summary>
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "wdpl2.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!;
    }

    private static DirectoryInfo BackendRoot() =>
        new(Path.Combine(RepoRoot().FullName, "wdpl2", "web-backend"));

    /// <summary>Backend files that must reach the server, as repo-relative paths.</summary>
    private static List<string> FilesOnDisk()
    {
        var root = BackendRoot();
        Assert.True(root.Exists, $"Backend folder not found at {root.FullName}");

        return root.EnumerateFiles("*", SearchOption.AllDirectories)
            // config.php is the credential sidecar: generated at deploy time,
            // never committed, never shipped as an asset.
            .Where(f => !f.Name.Equals("config.php", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Name.Equals("config.sample.php", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Name.Equals("README.md", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(root.FullName, f.FullName).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Every source file some module deploys.
    /// </summary>
    /// <remarks>
    /// An entry may serve one file at a second address (<see cref="ServerFile"/>),
    /// so the source is what is compared against the tree - the alias adds an
    /// address, not a file.
    /// </remarks>
    private static List<string> FilesDeclared() =>
        Modules()
            .SelectMany(m => m.ServerFiles)
            .Select(e => ServerFile.Parse(e).Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Two modules must not deploy different files to the same address.
    /// </summary>
    /// <remarks>
    /// The deploy is a dictionary keyed by destination, so a clash would not
    /// fail - one file would silently win, and which one would depend on the
    /// order the modules happen to be registered in.
    /// </remarks>
    [Fact]
    public void NoTwoFilesClaimTheSameAddress()
    {
        var clashes = Modules()
            .SelectMany(m => m.ServerFiles)
            .Select(ServerFile.Parse)
            .GroupBy(f => f.Destination, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(f => f.Source).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(g => $"{g.Key} <- {string.Join(", ", g.Select(f => f.Source))}")
            .ToList();

        Assert.True(clashes.Count == 0,
            "These addresses are claimed by more than one file:\n  " + string.Join("\n  ", clashes));
    }

    /// <summary>A file served at a second address is uploaded to both.</summary>
    [Fact]
    public void AliasedFilesAreDeployedToBothAddresses()
    {
        var aliases = Modules()
            .SelectMany(m => m.ServerFiles)
            .Select(ServerFile.Parse)
            .Where(f => f.IsAlias)
            .ToList();

        // The cup tie card is the one that exists, and the reason the mechanism
        // exists at all. If it goes, this test should be reconsidered, not deleted.
        Assert.Contains(aliases, f => f.Destination == "comp/tie.html"
                                      && f.Source == "captain/index.html");

        foreach (var alias in aliases)
        {
            Assert.Contains(alias.Source, FilesDeclared());
        }
    }

    [Fact]
    public void EveryBackendFileIsDeployed()
    {
        var missing = FilesOnDisk()
            .Except(FilesDeclared(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(missing.Count == 0,
            "These backend files exist but no module deploys them, so the server would be missing them:\n  " +
            string.Join("\n  ", missing));
    }

    [Fact]
    public void EveryDeployedFileExists()
    {
        var root = BackendRoot();

        var absent = FilesDeclared()
            .Where(rel => !File.Exists(Path.Combine(root.FullName, rel.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();

        Assert.True(absent.Count == 0,
            "These files are declared for deployment but do not exist:\n  " + string.Join("\n  ", absent));
    }

    [Fact]
    public void TheCredentialSidecarIsNeverDeployedAsCode()
    {
        // config.php holds the database password and the admin hash. It is
        // written separately at deploy time; shipping it as an asset would put
        // credentials in the app package and let a code redeploy clobber them.
        //
        // Matched as an exact path, not EndsWith: case-insensitively,
        // "api/core/Config.php" ends with "config.php" and would trip this.
        // That is the same trap that once made an MSBuild glob exclude the
        // wrong file from the app package.
        Assert.DoesNotContain(FilesDeclared(),
            p => p.Equals("api/config.php", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ModuleIdsAreUniqueAndMatchTheirServerFolder()
    {
        var modules = Modules();

        Assert.Equal(modules.Count, modules.Select(m => m.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var module in modules)
        {
            // The PHP side discovers modules by folder name, so a mismatch means
            // the app and the server disagree about what is installed.
            var expected = $"api/modules/{module.Id}/Module.php";
            if (module.Id == "system") continue; // core files, covered above

            Assert.Contains(expected, module.ServerFiles);
        }
    }

    [Fact]
    public void TheFrontControllerAndHtaccessAreAlwaysDeployed()
    {
        var declared = FilesDeclared();

        // Without index.php there is no API at all; without .htaccess the core
        // and the credential sidecar become publicly fetchable.
        Assert.Contains("api/index.php", declared);
        Assert.Contains("api/.htaccess", declared);
    }
}
