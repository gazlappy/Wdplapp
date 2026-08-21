using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for the live scores page produced by WebsiteGenerator.
/// </summary>
public class WebsiteGeneratorLiveScoresTests
{
    private static LeagueData CreateLeague()
    {
        var league = new LeagueData();
        league.Seasons.Add(new Season { Id = Guid.NewGuid(), Name = "2024", IsActive = true });
        return league;
    }

    private static WebsiteSettings CreateSettings()
    {
        return new WebsiteSettings
        {
            LeagueName = "Test League",
            SelectedTemplate = "modern",
            ShowLiveScores = true
        };
    }

    [Fact]
    public void GenerateWebsite_LiveScoresDisabled_DoesNotEmitLivePage()
    {
        var settings = CreateSettings();
        settings.ShowLiveScores = false;

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.False(files.ContainsKey("live.html"));
    }

    [Fact]
    public void GenerateWebsite_LiveScoresEnabled_EmitsLivePage()
    {
        var files = new WebsiteGenerator(CreateLeague(), CreateSettings()).GenerateWebsite();

        Assert.True(files.ContainsKey("live.html"));
        Assert.Contains("live-board", files["live.html"]);
    }

    [Fact]
    public void GenerateWebsite_LiveScoresEnabled_AddsNavigationLink()
    {
        var settings = CreateSettings();
        settings.LiveScoresNavLabel = "Live Now";

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.Contains("live.html", files["live.html"]);
        Assert.Contains("Live Now", files["live.html"]);
    }

    [Fact]
    public void GenerateWebsite_LiveScoresEnabled_AddsLiveStyles()
    {
        var files = new WebsiteGenerator(CreateLeague(), CreateSettings()).GenerateWebsite();

        Assert.Contains(".live-card", files["style.css"]);
        Assert.Contains("livePulse", files["style.css"]);
    }

    [Fact]
    public void GenerateWebsite_BlankApiUrl_LivePageAutoDetectsEndpoint()
    {
        var settings = CreateSettings();
        settings.LiveScoresApiBaseUrl = "";

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.Contains("api/public/live.php", files["live.html"]);
    }

    [Fact]
    public void GenerateWebsite_ExplicitApiUrl_IsUsedVerbatim()
    {
        var settings = CreateSettings();
        settings.LiveScoresApiBaseUrl = "https://example.com/api/public/live.php";

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.Contains("https://example.com/api/public/live.php", files["live.html"]);
    }

    [Theory]
    [InlineData(1, 5000)]
    [InlineData(20, 20000)]
    [InlineData(9999, 300000)]
    public void GenerateWebsite_RefreshSeconds_ClampedIntoPollInterval(int seconds, int expectedMs)
    {
        var settings = CreateSettings();
        settings.LiveScoresRefreshSeconds = seconds;

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.Contains($"var intervalMs = {expectedMs};", files["live.html"]);
    }

    [Fact]
    public void GenerateWebsite_FrameDetailDisabled_DoesNotRenderFrames()
    {
        var settings = CreateSettings();
        settings.LiveScoresShowFrameDetail = false;

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.Contains("var showFrames = false;", files["live.html"]);
    }

    [Fact]
    public void GenerateWebsite_EmptyMessageWithApostrophe_IsEscapedForJavaScript()
    {
        var settings = CreateSettings();
        settings.LiveScoresEmptyMessage = "It's quiet tonight";

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.Contains(@"It\'s quiet tonight", files["live.html"]);
    }

    [Fact]
    public void GenerateWebsite_ShowOnHomeEnabled_HomePageIncludesLiveStrip()
    {
        var settings = CreateSettings();
        settings.LiveScoresShowOnHome = true;

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.Contains("live-strip", files["home.html"]);
    }

    [Fact]
    public void GenerateWebsite_ShowOnHomeDisabled_HomePageOmitsLiveStrip()
    {
        var settings = CreateSettings();
        settings.LiveScoresShowOnHome = false;

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.DoesNotContain("live-strip", files["home.html"]);
    }
}
