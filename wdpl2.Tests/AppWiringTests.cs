using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Wdpl2;
using Wdpl2.Services;
using Wdpl2.ViewModels;
using Wdpl2.Views;

namespace wdpl2.Tests;

/// <summary>
/// Every tab, and every page the app fetches from the container, must open.
/// </summary>
/// <remarks>
/// A MAUI page cannot be constructed in this test runner, so these do the next
/// best thing: build the same service collection the app builds, and check the
/// wiring the compiler cannot see. A tab pointing at a type that does not exist
/// fails the build already; a page that needs a service nobody registered, or a
/// page fetched with <c>GetService</c> after its registration was deleted, only
/// fails when someone clicks it. Those are what these catch.
/// </remarks>
public class AppWiringTests
{
    private static readonly Assembly AppAssembly = typeof(Product).Assembly;

    /// <summary>What <c>MauiProgram.CreateMauiApp</c> registers, minus the MAUI host itself.</summary>
    private static ServiceCollection AppServices()
    {
        var services = new ServiceCollection();
        services.AddPersistence();
        services.AddCoreAppServices();
        services.AddNotifications();
        services.AddViewModels();
        services.AddPages();
        return services;
    }

    [Fact]
    public void EveryRegistration_CanBeResolved()
    {
        // ValidateOnBuild walks every registration's constructor and throws if
        // any dependency is missing - without creating a single page.
        var error = Record.Exception(() => AppServices().BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true }));

        Assert.True(error is null, "A registration needs something that is not registered:\n" + error?.Message);
    }

    [Fact]
    public void EveryTab_OpensAPageThatCanBeBuilt()
    {
        var registered = AppServices().Select(d => d.ServiceType).ToHashSet();
        var tabs = TabPages();

        Assert.NotEmpty(tabs);
        foreach (var (title, type) in tabs)
        {
            Assert.True(typeof(Page).IsAssignableFrom(type), $"The {title} tab points at {type.Name}, which is not a page.");

            // Shell builds a tab from the container when the page is registered
            // there, and with a parameterless constructor when it is not.
            var buildable = registered.Contains(type) || type.GetConstructor(Type.EmptyTypes) != null;
            Assert.True(buildable,
                $"The {title} tab points at {type.Name}, which is not registered and has no parameterless constructor.");
        }
    }

    [Fact]
    public void EveryPageFetchedAtRuntime_IsRegistered()
    {
        var registered = AppServices().Select(d => d.ServiceType.Name).ToHashSet();

        // GetService<T> compiles whether or not T is registered, and hands back
        // null when it is not - so a deleted registration only shows up as a
        // crash on the button that fetches the page.
        var fetched = SourceFiles()
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"Get(?:Required)?Service<(\w+)>\(\)").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(fetched);
        var missing = fetched.Where(name => !registered.Contains(name)).OrderBy(n => n).ToList();
        Assert.True(missing.Count == 0, "Fetched with GetService but never registered: " + string.Join(", ", missing));
    }

    /// <summary>
    /// No tab's page may swap itself out for another page.
    /// </summary>
    /// <remarks>
    /// Shell reuses a tab's page. The Website tab used to point at a page that
    /// pushed the real builder once and removed itself, guarded by a flag that
    /// was never reset - so coming back quickly landed on the same page with
    /// the flag set, and a spinner that never stopped.
    /// </remarks>
    [Fact]
    public void NoTabPage_RemovesItselfFromNavigation()
    {
        foreach (var (title, type) in TabPages())
        {
            var code = SourceFiles().FirstOrDefault(f => Path.GetFileName(f) == type.Name + ".xaml.cs"
                                                      || Path.GetFileName(f) == type.Name + ".cs");
            if (code is null) continue;

            Assert.False(File.ReadAllText(code).Contains("RemovePage(this)"),
                $"The {title} tab's page ({type.Name}) removes itself from navigation. Point the tab at the page it redirects to.");
        }
    }

    /// <summary>The tabs in AppShell.xaml, as (title, page type).</summary>
    private static List<(string Title, Type Type)> TabPages()
    {
        var shell = XDocument.Load(Path.Combine(RepoRoot(), "wdpl2", "AppShell.xaml"));

        // xmlns:prefix="clr-namespace:Some.Namespace" -> prefix => namespace
        var namespaces = shell.Root!.Attributes()
            .Where(a => a.IsNamespaceDeclaration && a.Value.StartsWith("clr-namespace:"))
            .ToDictionary(a => a.Name.LocalName, a => a.Value["clr-namespace:".Length..].Split(';')[0]);

        var tabs = new List<(string, Type)>();
        foreach (var content in shell.Descendants().Where(e => e.Name.LocalName == "ShellContent"))
        {
            var template = (string?)content.Attribute("ContentTemplate") ?? "";
            var match = Regex.Match(template, @"\{DataTemplate\s+(\w+):(\w+)\}");
            Assert.True(match.Success, $"Could not read the page from ContentTemplate=\"{template}\"");

            var fullName = namespaces[match.Groups[1].Value] + "." + match.Groups[2].Value;
            var type = AppAssembly.GetType(fullName);
            Assert.True(type != null, $"No type {fullName}");
            tabs.Add(((string?)content.Attribute("Title") ?? fullName, type!));
        }
        return tabs;
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "wdpl2"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "wdpl2.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
