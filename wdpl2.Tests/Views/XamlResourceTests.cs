using System.Text.RegularExpressions;

namespace wdpl2.Tests;

/// <summary>
/// Every StaticResource a page asks for has to exist.
/// </summary>
/// <remarks>
/// A missing key is not a warning and not a build error - it throws when the
/// page is parsed, which is when somebody taps the thing that opens it. So it
/// looks like a dead button rather than a fault, and the page can sit broken
/// for as long as nobody happens to go there.
/// <para>
/// Four analytics pages were in exactly that state, all four asking for one
/// style that had been removed or renamed: the season awards, the what-if
/// simulator, the match day dashboard and a player's profile.
/// </para>
/// </remarks>
public class XamlResourceTests
{
    private static readonly Regex Key = new(@"x:Key=""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex Reference =
        new(@"\{\s*StaticResource\s+([A-Za-z0-9_.]+)\s*\}", RegexOptions.Compiled);

    /// <summary>Dictionaries merged into the app, so their keys are in scope everywhere.</summary>
    private static readonly string[] GlobalDictionaries =
    {
        "App.xaml",
        Path.Combine("Resources", "Styles", "Styles.xaml"),
        Path.Combine("Resources", "Styles", "SharedStyles.xaml"),
        Path.Combine("Resources", "Styles", "Colors.xaml"),
    };

    [Fact]
    public void EveryStaticResourceUsedByAPageIsDefined()
    {
        var app = AppFolder();

        var global = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relative in GlobalDictionaries)
        {
            var path = Path.Combine(app.FullName, relative);
            Assert.True(File.Exists(path), $"Expected a resource dictionary at {relative}");

            foreach (Match match in Key.Matches(File.ReadAllText(path)))
                global.Add(match.Groups[1].Value);
        }

        Assert.True(global.Count > 50, $"Only found {global.Count} global resource keys - the sweep is not reading them.");

        var missing = new List<string>();

        foreach (var file in Xaml(app))
        {
            var text = File.ReadAllText(file.FullName);

            // A page may define its own, which are in scope for itself.
            var own = Key.Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

            foreach (Match match in Reference.Matches(text))
            {
                var key = match.Groups[1].Value;
                if (global.Contains(key) || own.Contains(key)) continue;

                var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                missing.Add($"{Path.GetRelativePath(app.FullName, file.FullName)}:{line}  {key}");
            }
        }

        Assert.True(missing.Count == 0,
            "These pages ask for a resource that is not defined anywhere, so they throw when opened:\n  "
            + string.Join("\n  ", missing.Distinct()));
    }

    private static IEnumerable<FileInfo> Xaml(DirectoryInfo app) =>
        app.EnumerateFiles("*.xaml", SearchOption.AllDirectories)
           .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    && !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    /// <summary>Walks up from the test binary to the app project.</summary>
    private static DirectoryInfo AppFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "wdpl2.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);

        var app = new DirectoryInfo(Path.Combine(dir!.FullName, "wdpl2"));
        Assert.True(app.Exists, $"App project not found at {app.FullName}");
        return app;
    }
}
