using System.Text.RegularExpressions;
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

        // The auto-detected endpoint is the league module's live action on the
        // single front controller, not the removed per-file endpoint.
        Assert.Contains("index.php?m=league&a=live", files["live.html"]);
        Assert.DoesNotContain("api/public/live.php", files["live.html"]);
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

    [Fact]
    public void GenerateWebsite_FrameDetailEnabled_LivePageAsksTheBackendForFrames()
    {
        var settings = CreateSettings();
        settings.LiveScoresShowFrameDetail = true;

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.Contains("var wantFrames = true;", files["live.html"]);
    }

    /// <summary>
    /// The frames are the bulk of the payload and the board would throw them
    /// away, so a league that has the detail switched off must not be paying
    /// for it on every poll.
    /// </summary>
    [Fact]
    public void GenerateWebsite_FrameDetailDisabled_LivePageDoesNotAskForFrames()
    {
        var settings = CreateSettings();
        settings.LiveScoresShowFrameDetail = false;

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.Contains("var wantFrames = false;", files["live.html"]);
    }

    /// <summary>
    /// The home page strip only ever draws a scoreline, and it is polled by
    /// every visitor rather than only by the people watching the board.
    /// </summary>
    [Fact]
    public void GenerateWebsite_HomeStrip_NeverAsksForFrames()
    {
        var settings = CreateSettings();
        settings.LiveScoresShowOnHome = true;
        settings.LiveScoresShowFrameDetail = true;

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.Contains("var wantFrames = false;", files["home.html"]);
        Assert.DoesNotContain("var wantFrames = true;", files["home.html"]);
    }

    /// <summary>
    /// A frame row is classed by who won it. That class used to be the same
    /// name as the span holding the home player, which only worked by accident.
    /// </summary>
    [Fact]
    public void GenerateWebsite_FrameRows_AreClassedByWinnerNotBySide()
    {
        var files = new WebsiteGenerator(CreateLeague(), CreateSettings()).GenerateWebsite();

        Assert.Contains("live-frame-won-", files["live.html"]);
        Assert.Contains(".live-frame-won-home .live-frame-home", files["style.css"]);
        Assert.Contains(".live-frame-won-away .live-frame-away", files["style.css"]);
    }

    /// <summary>
    /// A cup tie and a league match look identical on the board otherwise, and
    /// the backend has always said which one it is.
    /// </summary>
    [Fact]
    public void GenerateWebsite_LiveBoard_MarksCupTies()
    {
        var files = new WebsiteGenerator(CreateLeague(), CreateSettings()).GenerateWebsite();

        Assert.Contains("match.kind === 'cup'", files["live.html"]);
        Assert.Contains(".live-kind", files["style.css"]);
    }

    /// <summary>
    /// Every field the board reads off a match must be one the backend puts
    /// there.
    /// </summary>
    /// <remarks>
    /// This is the failure that prompted the test: the page was written against
    /// <c>status</c>, <c>division_name</c> and <c>frames</c> while
    /// <c>league.live</c> sent none of them, so the frame detail setting was a
    /// switch wired to nothing and every card carried a broken CSS class. The
    /// two sides are in different languages and neither build checks the other,
    /// so the only thing that notices is a test that reads both.
    /// </remarks>
    [Fact]
    public void GenerateWebsite_LiveBoard_ReadsOnlyFieldsTheBackendSends()
    {
        var files = new WebsiteGenerator(CreateLeague(), CreateSettings()).GenerateWebsite();
        var sent = LivePayloadFields();

        // Only the board is scanned. It reads every field name the home page
        // strip does, and "match" is its own name for a row - scanning the home
        // page for a bare "m." would trip over any other script that used it.
        var read = Regex.Matches(files["live.html"], @"\bmatch\.([a-z_][a-z0-9_]*)\b")
            .Select(hit => hit.Groups[1].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(read);

        foreach (var field in read)
        {
            Assert.True(sent.Contains(field),
                $"The live board reads match.{field}, which league.live does not send. "
                + $"It sends: {string.Join(", ", sent.OrderBy(f => f))}.");
        }
    }

    /// <summary>
    /// The crawl along the foot of the screen - the same component the captains
    /// see under their own card.
    /// </summary>
    [Fact]
    public void GenerateWebsite_LivePage_CarriesTheScoreCrawl()
    {
        var files = new WebsiteGenerator(CreateLeague(), CreateSettings()).GenerateWebsite();

        Assert.Contains("id=\"tickTrack\"", files["live.html"]);
        Assert.Contains(".tick-track", files["style.css"]);
        Assert.Contains("tickRun", files["style.css"]);
    }

    /// <summary>
    /// The crawl belongs to the live page. The home page has its own compact
    /// strip and must not also get a bar fixed across every visitor's screen.
    /// </summary>
    [Fact]
    public void GenerateWebsite_HomePage_HasNoScoreCrawl()
    {
        var settings = CreateSettings();
        settings.LiveScoresShowOnHome = true;

        var files = new WebsiteGenerator(CreateLeague(), settings).GenerateWebsite();

        Assert.DoesNotContain("id=\"tickTrack\"", files["home.html"]);
    }

    /// <summary>
    /// The board and the crawl both read the same poll.
    /// </summary>
    /// <remarks>
    /// They subscribe rather than each starting a loop of their own. An earlier
    /// shape had start() clear the existing timer, so a second caller silently
    /// stopped the first from ever updating again - the board would have drawn
    /// once and then frozen while the crawl kept moving.
    /// </remarks>
    [Fact]
    public void GenerateWebsite_LivePage_BoardAndCrawlShareOnePoll()
    {
        var files = new WebsiteGenerator(CreateLeague(), CreateSettings()).GenerateWebsite();
        var page = files["live.html"];

        Assert.Equal(2, Regex.Matches(page, @"wdplLive\.start\(").Count);
        Assert.Contains("subscribers.push(sub)", page);
        Assert.DoesNotContain("clearInterval", page);
    }

    /// <summary>
    /// Player results per match: who played each frame and who took it.
    /// </summary>
    [Fact]
    public void GenerateWebsite_FrameDetailEnabled_BoardRendersPlayerResults()
    {
        var files = new WebsiteGenerator(CreateLeague(), CreateSettings()).GenerateWebsite();

        Assert.Contains("live-results", files["live.html"]);
        Assert.Contains("f.home_player", files["live.html"]);
        Assert.Contains("f.away_player", files["live.html"]);
        // A doubles frame names both players and is marked D rather than F.
        Assert.Contains("f.doubles ? 'D' : 'F'", files["live.html"]);
    }

    /// <summary>
    /// The keys <c>League::live()</c> builds each board item from, read out of
    /// the PHP itself so the two cannot drift apart unnoticed.
    /// </summary>
    private static HashSet<string> LivePayloadFields()
    {
        var php = Path.Combine(
            RepoRoot().FullName, "wdpl2", "web-backend", "api", "modules", "league", "Module.php");

        Assert.True(File.Exists(php), $"No league module at {php}");

        var source = File.ReadAllText(php);

        // The match itself, and then the frames it carries, are two separate
        // array literals in the PHP. The board reads fields off both.
        var fields = new HashSet<string>(StringComparer.Ordinal);
        // The frames are keyed by a subscript that itself contains brackets,
        // so that key is matched as "the rest of the line", not as a token.
        foreach (var block in new[] { @"\$board\[\]\s*=\s*\[(.*?)\n\s*\];", @"\$byFixture\[[^\n]*\]\[\]\s*=\s*\[(.*?)\n\s*\];" })
        {
            var match = Regex.Match(source, block, RegexOptions.Singleline);
            Assert.True(match.Success, $"Could not find the payload literal /{block}/ in league/Module.php");

            foreach (Match key in Regex.Matches(match.Groups[1].Value, @"'([a-z_][a-z0-9_]*)'\s*=>"))
                fields.Add(key.Groups[1].Value);
        }

        return fields;
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
