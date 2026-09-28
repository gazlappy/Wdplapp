using System.Text.RegularExpressions;

namespace wdpl2.Tests;

/// <summary>
/// Every element id on the website's hand-written pages is used once.
/// </summary>
/// <remarks>
/// The pages find their parts with <c>getElementById</c>, which answers with
/// the first match and says nothing about a second. Adding a match chooser
/// to the captains' page gave it a list called <c>pickList</c> - the name the
/// player picker already had - so the players went into the hidden chooser
/// and the picker on screen stayed empty. On both copies of the page, on a
/// match night, with nothing in the console to say why.
/// </remarks>
public class WebPageIdTests
{
    public static TheoryData<string> Pages() => new()
    {
        "captain/index.html",
        "comp/index.html",
        "admin/index.html",
    };

    [Theory]
    [MemberData(nameof(Pages))]
    public void No_id_is_used_twice(string page)
    {
        var html = File.ReadAllText(Path.Combine(RepoRoot().FullName, "wdpl2", "web-backend", page));

        var repeated = Regex.Matches(html, @"\sid=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ×{g.Count()}")
            .ToList();

        Assert.True(repeated.Count == 0, $"{page} uses these ids more than once: {string.Join(", ", repeated)}");
    }

    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "wdpl2.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!;
    }
}
