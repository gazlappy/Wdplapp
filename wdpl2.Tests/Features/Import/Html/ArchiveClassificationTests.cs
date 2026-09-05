using Wdpl2.Services;

namespace Wdpl2.Tests;

public class ArchiveClassificationTests
{
    [Theory]
    [InlineData("Winter 2000", "Winter 2000")]
    [InlineData("2000 Summer", "Summer 2000")]
    [InlineData("Winter 1999/2000", "Winter 1999-00")]
    [InlineData("Summer 2000", "Summer 2000")]
    [InlineData("Unknown (2000)", "Unknown (2000)")]
    [InlineData("2000/2001", "2000-01")]
    public void NormalizeSeason_PreservesIdentity(string input, string expected)
    {
        Assert.Equal(expected, LeagueFileDiscoveryService.NormalizeSeasonKey(input));
    }

    [Fact]
    public void GroupBySeason_DoesNotMergeOverlappingYears()
    {
        var files = new[] { "Summer 2000", "Winter 2000", "2000", "2000-01" }
            .Select(s => new LeagueFileDiscoveryService.DiscoveredFile { DetectedSeason = s }).ToList();
        var groups = new LeagueFileDiscoveryService().GroupBySeason(files);
        Assert.Equal(4, groups.Count);
        Assert.All(groups.Where(g => !LeagueFileDiscoveryService.HasSeasonTerm(g.SeasonKey)), g =>
        {
            Assert.True(g.RequiresReview);
            Assert.False(g.IsSelected);
            Assert.Null(g.ExistingSeasonId);
        });
    }

    [Theory]
    [InlineData("Summer", "Red Division", "Summer 2000")]
    [InlineData("Winter", "1st Division", "Winter 2000")]
    [InlineData("", "Second Division", "Winter 2000")]
    public void RefineSeason_UsesContentEvidence(string title, string division, string expected)
    {
        var file = new LeagueFileDiscoveryService.DiscoveredFile { DetectedSeason = "2000" };
        LeagueFileDiscoveryService.RefineSeason(file, new HtmlLeagueParser.HtmlParseResult
        {
            PageTitle = title,
            DetectedDivision = division
        });
        Assert.Equal(expected, file.DetectedSeason);
        Assert.Null(file.SeasonReviewReason);
    }

    [Fact]
    public void RefineSeason_ConflictingDivisionsRequireReview()
    {
        var file = new LeagueFileDiscoveryService.DiscoveredFile { DetectedSeason = "2000" };
        LeagueFileDiscoveryService.RefineSeason(file, new HtmlLeagueParser.HtmlParseResult
        {
            PageTitle = "Summer",
            DetectedDivision = "First Division"
        });
        var group = Assert.Single(new LeagueFileDiscoveryService().GroupBySeason(new() { file }));
        Assert.True(group.RequiresReview);
        Assert.False(group.IsSelected);
    }

    [Theory]
    [InlineData("A", false)]
    [InlineData("b.", false)]
    [InlineData("123", false)]
    [InlineData("A-Z", false)]
    [InlineData("Next", false)]
    [InlineData("J. Smith", true)]
    [InlineData("O'Neill", true)]
    [InlineData("Li", true)]
    public void PlayerNames_RejectNavigationNotRealNames(string name, bool expected)
    {
        Assert.Equal(expected, HtmlLeagueParser.IsValidPlayerName(name));
    }

    [Fact]
    public async Task PlayerList_RejectsAlphabetProfileLinks()
    {
        var result = await ParseTemporary("<h1>List of players</h1><a href=\"player1.htm\">A</a><a href=\"player2.htm\">B</a><a href=\"player3.htm\">J. Smith</a>");
        Assert.Equal("J. Smith", Assert.Single(result.Players).Name);
        Assert.Single(result.PlayerListEntries);
    }

    [Theory]
    [InlineData("Venue", 0)]
    [InlineData("Team Name", 1)]
    public async Task Standings_RequireTeamColumn(string header, int expected)
    {
        var result = await ParseTemporary($"<h1>Red Division Table</h1><table><tr><th>Pos</th><th>{header}</th><th>Played</th><th>Won</th><th>Lost</th><th>Singles Won</th><th>Singles Lost</th><th>Deducted</th><th>Points</th></tr><tr><td>1</td><td>New Inn</td><td>10</td><td>8</td><td>2</td><td>80</td><td>20</td><td>0</td><td>80</td></tr></table>");
        Assert.Equal(expected, result.Teams.Count);
    }

    [Fact]
    public async Task AnalyzeGroups_SplitsSummerAndWinterForSameYear()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(folder);
        try
        {
            var files = new List<LeagueFileDiscoveryService.DiscoveredFile>();
            foreach (var term in new[] { "Summer", "Winter" })
            {
                var path = Path.Combine(folder, term + ".htm");
                await File.WriteAllTextAsync(path, $"<title>{term}</title><h1>List of players</h1><a href=\"player1.htm\">J. Smith</a>");
                files.Add(new() { FilePath = path, FileType = "HTML", DetectedSeason = "2000" });
            }
            var groups = new LeagueFileDiscoveryService().GroupBySeason(files);
            await LeagueFileDiscoveryService.AnalyzeGroupsAsync(groups);
            Assert.Equal(2, groups.Count);
            Assert.Contains(groups, g => g.SeasonKey == "Summer 2000");
            Assert.Contains(groups, g => g.SeasonKey == "Winter 2000");
            Assert.All(groups, g => { Assert.False(g.RequiresReview); Assert.Equal(1, g.AnalyzedPlayers); });
        }
        finally { Directory.Delete(folder, true); }
    }

    private static async Task<HtmlLeagueParser.HtmlParseResult> ParseTemporary(string html)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".htm");
        try
        {
            await File.WriteAllTextAsync(path, html);
            var result = await HtmlLeagueParser.ParseHtmlFileAsync(path);
            Assert.True(result.Success);
            return result;
        }
        finally { File.Delete(path); }
    }
}
