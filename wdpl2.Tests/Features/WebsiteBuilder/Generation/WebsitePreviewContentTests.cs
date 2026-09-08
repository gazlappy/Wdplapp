using System.Text.Json;
using System.Text.RegularExpressions;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

public class WebsitePreviewContentTests
{
    [Theory]
    [InlineData("players-data.json", false)]
    [InlineData("players-data.json", true)]
    [InlineData("teams-data.json", false)]
    [InlineData("teams-data.json", true)]
    public void Prepare_InlinesPlainAndCacheBustedData(string fileName, bool cacheBusted)
    {
        var fetch = cacheBusted ? $"fetch('{fileName}?v=' + cacheBuster)" : $"fetch('{fileName}')";
        var html = $"<script>{fetch}\r\n .then(function(r) {{ return r.json(); }})</script>";
        var json = "[{\"name\":\"O'Brien\"}]";
        var files = new Dictionary<string, string> { [fileName] = json };
        var result = WebsitePreviewContent.Prepare(html, files);
        Assert.Equal($"<script>Promise.resolve(JSON.parse({JsonSerializer.Serialize(json)}))</script>", result);
        Assert.Equal(json, files[fileName]);
    }

    [Theory]
    [InlineData("style.css")]
    [InlineData("style.css?v=123")]
    public void Prepare_InlinesGeneratedStylesheet(string url)
    {
        var files = new Dictionary<string, string> { ["style.css"] = "body { color: red; }" };
        Assert.Equal("<style>body { color: red; }</style>",
            WebsitePreviewContent.Prepare($"<link rel=\"stylesheet\" href=\"{url}\">", files));
    }

    [Fact]
    public void Prepare_PreservesScriptLikeDataAndEscapesScriptDelimiters()
    {
        var json = "{\n\"name\":\"</script><script>alert('x')</script> & new URLSearchParams(window.location.search)\"\n}";
        var html = "<script>fetch('players-data.json').then(function(r) { return r.json(); })</script>";
        var files = new Dictionary<string, string> { ["players-data.json"] = json };
        var result = WebsitePreviewContent.Prepare(html, files, "?id=example");
        var literal = Regex.Match(result, @"JSON\.parse\((.*)\)\)</script>").Groups[1].Value;
        Assert.Equal(json, JsonSerializer.Deserialize<string>(literal));
        Assert.DoesNotContain("</script><script>", result);
        Assert.Single(Regex.Matches(result, "</script>"));
    }

    [Fact]
    public void Prepare_UsesLocalQueryWithoutReplacingGlobalUrlSearchParams()
    {
        var query = "?id=O'Brien&note=</script><script>oops</script>\n";
        var html = "<script>var p = new URLSearchParams(window.location.search); var other = new URLSearchParams('x=1');</script>";
        var result = WebsitePreviewContent.Prepare(html, new Dictionary<string, string>(), query);
        Assert.Contains($"new URLSearchParams({JsonSerializer.Serialize(query)})", result);
        Assert.Contains("new URLSearchParams('x=1')", result);
        Assert.DoesNotContain("URLSearchParams=function", result);
        Assert.DoesNotContain("_editorQS", result);
        Assert.DoesNotContain("</script><script>", result);
    }

    [Fact]
    public void Prepare_LeavesMissingAssetsAndAbsentQueryUntouched()
    {
        const string html = "<script>fetch('players-data.json').then(function(r) { return r.json(); }); new URLSearchParams(window.location.search); fetch('other.json');</script>";
        Assert.Equal(html, WebsitePreviewContent.Prepare(html, new Dictionary<string, string>()));
    }

    [Theory]
    [InlineData("https://example.test/live-form")]
    [InlineData("http://example.test/")]
    [InlineData("ftp://example.test/")]
    [InlineData("mailto:secretary@example.test")]
    [InlineData("tel:12345")]
    [InlineData("app://command")]
    [InlineData("//example.test/live-form")]
    [InlineData("file://server/share/form.html")]
    public void Navigation_BlocksExternalDestinations(string url) =>
        Assert.True(WebsitePreviewContent.ShouldBlockNavigation(url));

    [Theory]
    [InlineData("about:blank")]
    [InlineData("about:blank#form-1")]
    [InlineData("data:text/html,<h1>Preview</h1>")]
    [InlineData("file:///C:/Temp/preview_123.html")]
    [InlineData("file:///tmp/preview_123.html")]
    [InlineData("entry-forms.html")]
    [InlineData("#form-1")]
    public void Navigation_AllowsLocalSourcesAndAnchors(string url) =>
        Assert.False(WebsitePreviewContent.ShouldBlockNavigation(url));

    [Fact]
    public void Prepare_HandlesActualGeneratedTemplatesWithoutMutatingPublishableFiles()
    {
        var league = new LeagueData();
        league.Seasons.Add(new Season { Name = "Summer 2026", IsActive = true });
        var settings = new WebsiteSettings
        {
            ShowPlayerStats = true, ShowDivisions = true, ShowEntryForms = true,
            FormServiceUrl = "https://example.test/entry", EntryForms = [EntryForm.CreateTeamEntryForm()]
        };
        var generator = new WebsiteGenerator(league, settings);
        var files = generator.GenerateWebsite(previewEntryForms: true);
        var before = JsonSerializer.Serialize(files);
        foreach (var page in new[] { "player.html", "team.html" })
        {
            var preview = WebsitePreviewContent.Prepare(files[page], files, "?id=example");
            Assert.Contains("Promise.resolve(JSON.parse(", preview);
            Assert.DoesNotContain("fetch('players-data.json", preview);
            Assert.DoesNotContain("fetch('teams-data.json", preview);
            Assert.DoesNotContain("new URLSearchParams(window.location.search)", preview);
            Assert.DoesNotContain("href=\"style.css?v=", preview);
        }
        var formPreview = WebsitePreviewContent.Prepare(files["entry-forms.html"], files);
        Assert.Contains("data-preview=\"true\"", formPreview);
        Assert.DoesNotContain(settings.FormServiceUrl, formPreview);
        Assert.Equal(before, JsonSerializer.Serialize(files));
        var published = generator.GenerateWebsite();
        Assert.Contains("data-preview=\"false\"", published["entry-forms.html"]);
        Assert.Contains(settings.FormServiceUrl, published["entry-forms.html"]);
        Assert.Contains("fetch('players-data.json?v=' + cacheBuster)", published["player.html"]);
    }
}
