using Wdpl2.Services;

namespace Wdpl2.Tests;

/// <summary>
/// Tests for HtmlLeagueParser — validates parsing of each HTML page type.
/// Uses the sample HTML files in wdpl2\html data sample\
/// </summary>
public class HtmlLeagueParserTests
{
    /// <summary>
    /// Resolves the path to a sample HTML file relative to the test assembly location.
    /// The sample files live under wdpl2\html data sample\ in the repo root.
    /// </summary>
    private static string GetSampleFilePath(string fileName)
    {
        // Walk up from the test output directory to find the repo root
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "wdpl2", "html data sample")))
            dir = Directory.GetParent(dir)?.FullName;

        Assert.NotNull(dir); // Ensure we found the repo root
        var path = Path.Combine(dir!, "wdpl2", "html data sample", fileName);
        Assert.True(File.Exists(path), $"Sample file not found: {path}");
        return path;
    }

    // ─── League Table (tableRed.htm) ───────────────────────────────

    [Fact]
    public async Task ParseLeagueTable_DetectsCorrectPageType()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("tableRed.htm"));

        Assert.True(result.Success);
        Assert.Equal(HtmlLeagueParser.PageType.LeagueTable, result.DetectedPageType);
    }

    [Fact]
    public async Task ParseLeagueTable_ExtractsDivision()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("tableRed.htm"));

        Assert.True(result.Success);
        Assert.Equal("Red Division", result.DetectedDivision);
    }

    [Fact]
    public async Task ParseLeagueTable_ExtractsTeams()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("tableRed.htm"));

        Assert.True(result.Success);
        Assert.True(result.HasLeagueTable);
        Assert.True(result.Teams.Count > 0);

        // First team should be position 1
        var first = result.Teams[0];
        Assert.Equal(1, first.Position);
        Assert.Equal("Pot Blacks", first.Name);
        Assert.Equal(16, first.Played);
        Assert.Equal(15, first.Won);
        Assert.Equal(1, first.Lost);
        Assert.Equal(214, first.Points);
    }

    [Fact]
    public async Task ParseLeagueTable_AllTeamsHaveDivision()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("tableRed.htm"));

        Assert.True(result.Success);
        Assert.All(result.Teams, t => Assert.Equal("Red Division", t.Division));
    }

    // ─── League Table with ordinal heading (tableFirst.htm) ────────

    [Fact]
    public async Task ParseLeagueTable_FirstTable_DetectsAsLeagueTable()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("tableFirst.htm"));

        Assert.True(result.Success);
        Assert.Equal(HtmlLeagueParser.PageType.LeagueTable, result.DetectedPageType);
    }

    [Fact]
    public async Task ParseLeagueTable_FirstTable_ExtractsDivision()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("tableFirst.htm"));

        Assert.True(result.Success);
        // "First Table" → strip " Table" → "First" → "First Division"
        Assert.Equal("First Division", result.DetectedDivision);
    }

    [Fact]
    public async Task ParseLeagueTable_FirstTable_ExtractsTeams()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("tableFirst.htm"));

        Assert.True(result.Success);
        Assert.True(result.Teams.Count > 0);

        var first = result.Teams[0];
        Assert.Equal(1, first.Position);
        Assert.Equal("NEW INN", first.Name);
        Assert.Equal(22, first.Played);
    }

    // ─── Results (results.htm) ─────────────────────────────────────

    [Fact]
    public async Task ParseResults_DetectsCorrectPageType()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("results.htm"));

        Assert.True(result.Success);
        Assert.Equal(HtmlLeagueParser.PageType.Results, result.DetectedPageType);
    }

    [Fact]
    public async Task ParseResults_ExtractsFixtures()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("results.htm"));

        Assert.True(result.Success);
        Assert.True(result.HasResults);
        Assert.True(result.Results.Count > 0);

        // First result: 01/05/2014, Red Division, Mutts Nutts 7 - 8 Pro's & Con's
        var first = result.Results[0];
        Assert.Equal(new DateTime(2014, 5, 1), first.Date);
        Assert.Equal("Red Division", first.Division);
        Assert.Equal("Mutts Nutts", first.HomeTeam);
        Assert.Equal(7, first.HomeScore);
        Assert.Contains("Con", first.AwayTeam); // "Pro's & Con's"
        Assert.Equal(8, first.AwayScore);
    }

    [Fact]
    public async Task ParseResults_ContainsMultipleDivisions()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("results.htm"));

        Assert.True(result.Success);
        var divisions = result.Results.Select(r => r.Division).Distinct().ToList();
        Assert.True(divisions.Count >= 2, "Results should contain at least Red and Yellow division");
    }

    // ─── Player Ratings (singleRed.htm) ────────────────────────────

    [Fact]
    public async Task ParsePlayerRatings_DetectsCorrectPageType()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("singleRed.htm"));

        Assert.True(result.Success);
        Assert.Equal(HtmlLeagueParser.PageType.PlayerRatings, result.DetectedPageType);
    }

    [Fact]
    public async Task ParsePlayerRatings_ExtractsDivision()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("singleRed.htm"));

        Assert.True(result.Success);
        Assert.Equal("Red Division", result.DetectedDivision);
    }

    [Fact]
    public async Task ParsePlayerRatings_ExtractsPlayers()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("singleRed.htm"));

        Assert.True(result.Success);
        Assert.True(result.HasPlayerStats);
        Assert.True(result.Players.Count > 0);

        // First player: Andy Bowrah, Pot Blacks, 32 played, 26 won, 6 lost
        var first = result.Players[0];
        Assert.Equal(1, first.Position);
        Assert.Equal("Andy Bowrah", first.Name);
        Assert.Equal("Pot Blacks", first.TeamName);
        Assert.Equal(32, first.Played);
        Assert.Equal(26, first.Won);
        Assert.Equal(6, first.Lost);
        Assert.Equal(1189, first.BestRating);
        Assert.Equal(1175, first.CurrentRating);
    }

    [Fact]
    public async Task ParsePlayerRatings_ExtractsProfileLinks()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("singleRed.htm"));

        Assert.True(result.Success);
        Assert.True(result.Players.Count > 0);

        // Andy Bowrah should have a profile link
        var andy = result.Players.First(p => p.Name == "Andy Bowrah");
        Assert.Equal("player72.htm", andy.ProfileLink);
    }

    [Fact]
    public async Task ParsePlayerRatings_AllPlayersHaveTeamAndDivision()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("singleRed.htm"));

        Assert.True(result.Success);
        Assert.All(result.Players, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.TeamName));
            Assert.Equal("Red Division", p.Division);
        });
    }

    // ─── Player Profile (player100.htm) ────────────────────────────

    [Fact]
    public async Task ParsePlayerProfile_DetectsCorrectPageType()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("player100.htm"));

        Assert.True(result.Success);
        Assert.Equal(HtmlLeagueParser.PageType.PlayerProfile, result.DetectedPageType);
    }

    [Fact]
    public async Task ParsePlayerProfile_ExtractsPlayerNameAndTeam()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("player100.htm"));

        Assert.True(result.Success);
        Assert.True(result.HasPlayerProfile);
        Assert.NotNull(result.PlayerProfile);
        Assert.Equal("Chris Cannon", result.PlayerProfile!.PlayerName);
        Assert.Equal("Nice Parking", result.PlayerProfile.TeamName);
    }

    [Fact]
    public async Task ParsePlayerProfile_ExtractsSummaryStats()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("player100.htm"));

        Assert.NotNull(result.PlayerProfile);
        Assert.Equal(11, result.PlayerProfile!.Played);
        Assert.Equal(5, result.PlayerProfile.Won);
        Assert.Equal(6, result.PlayerProfile.Lost);
        Assert.Equal(975, result.PlayerProfile.CurrentRating);
    }

    [Fact]
    public async Task ParsePlayerProfile_ExtractsMatchHistory()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("player100.htm"));

        Assert.NotNull(result.PlayerProfile);
        Assert.True(result.PlayerProfile!.MatchHistory.Count > 0);

        // First match: 28/08/2014, Will Morley, Milverton Splitter's, Won, Rating=1261, Weighting=157
        var firstMatch = result.PlayerProfile.MatchHistory[0];
        Assert.Equal(new DateTime(2014, 8, 28), firstMatch.Date);
        Assert.Equal("Will Morley", firstMatch.OpponentName);
        Assert.Equal("Milverton Splitter's", firstMatch.OpponentTeam);
        Assert.Equal("Won", firstMatch.Result);
        Assert.Equal(1261, firstMatch.RatingAttained);
        Assert.Equal(157, firstMatch.Weighting);
    }

    [Fact]
    public async Task ParsePlayerProfile_ExtractsOpponentProfileLinks()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("player100.htm"));

        Assert.NotNull(result.PlayerProfile);
        var firstMatch = result.PlayerProfile!.MatchHistory[0];
        Assert.Equal("player120.htm", firstMatch.OpponentProfileLink);
    }

    [Fact]
    public async Task ParsePlayerProfile_ExtractsWeightingForAllMatches()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("player100.htm"));

        Assert.NotNull(result.PlayerProfile);
        // All matches should have non-zero Weighting values
        Assert.All(result.PlayerProfile!.MatchHistory, m =>
        {
            Assert.True(m.Weighting > 0, $"Weighting should be > 0 for match on {m.Date:dd/MM/yyyy} vs {m.OpponentName}");
        });
    }

    // ─── Doubles Ratings (doubleRed.htm) ───────────────────────────

    [Fact]
    public async Task ParseDoublesRatings_DetectsCorrectPageType()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("doubleRed.htm"));

        Assert.True(result.Success);
        Assert.Equal(HtmlLeagueParser.PageType.DoublesRatings, result.DetectedPageType);
    }

    [Fact]
    public async Task ParseDoublesRatings_ExtractsDivision()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("doubleRed.htm"));

        Assert.True(result.Success);
        Assert.Equal("Red Division", result.DetectedDivision);
    }

    [Fact]
    public async Task ParseDoublesRatings_EmptyTable_NoEntries()
    {
        // The sample doubleRed.htm has only headers, no data rows
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("doubleRed.htm"));

        Assert.True(result.Success);
        Assert.Empty(result.DoublesEntries);
    }

    // ─── Player List (players.htm) ─────────────────────────────────

    [Fact]
    public async Task ParsePlayerList_DetectsCorrectPageType()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("players.htm"));

        Assert.True(result.Success);
        Assert.Equal(HtmlLeagueParser.PageType.PlayerList, result.DetectedPageType);
    }

    [Fact]
    public async Task ParsePlayerList_ExtractsPlayerNames()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("players.htm"));

        Assert.True(result.Success);
        Assert.True(result.PlayerListEntries.Count > 10);

        // Check a known player
        var chris = result.PlayerListEntries.FirstOrDefault(p => p.Name == "Chris Cannon");
        Assert.NotNull(chris);
        Assert.Equal("player100.htm", chris!.ProfileLink);
    }

    [Fact]
    public async Task ParsePlayerList_AddsToPlayersCollection()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("players.htm"));

        Assert.True(result.Success);
        // Players should also be in the general Players list
        Assert.True(result.Players.Count > 0);
        Assert.Contains(result.Players, p => p.Name == "Chris Cannon");
    }

    // ─── Cross-cutting concerns ────────────────────────────────────

    [Fact]
    public async Task ParseLeagueTable_StripsHtmlEntities()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("tableRed.htm"));

        Assert.True(result.Success);
        // "Good, Bad & Curly" has &amp; in the HTML — should be decoded
        var team = result.Teams.FirstOrDefault(t => t.Name.Contains("Curly"));
        Assert.NotNull(team);
        Assert.Contains("&", team!.Name);
    }

    [Fact]
    public async Task ParseResults_HtmlEntitiesDecoded()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("results.htm"));

        Assert.True(result.Success);
        // "Pro's & Con's" should have decoded &amp;
        var match = result.Results.FirstOrDefault(r => r.AwayTeam.Contains("Con"));
        Assert.NotNull(match);
        Assert.Contains("&", match!.AwayTeam);
    }

    [Fact]
    public async Task ParsePlayerRatings_YellowDivision_ExtractsDifferentDivision()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("singleYellow.htm"));

        Assert.True(result.Success);
        Assert.Equal("Yellow Division", result.DetectedDivision);
        Assert.True(result.Players.Count > 0);
    }

    [Fact]
    public async Task ParseLeagueTable_YellowDivision_ExtractsDifferentDivision()
    {
        var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath("tableYellow.htm"));

        Assert.True(result.Success);
        Assert.Equal("Yellow Division", result.DetectedDivision);
        Assert.True(result.Teams.Count > 0);
    }

    [Fact]
    public async Task AllPageTypes_ReturnSuccess()
    {
        var files = new[]
        {
            "tableRed.htm", "tableYellow.htm", "tableFirst.htm",
            "singleRed.htm", "singleYellow.htm",
            "doubleRed.htm", "doubleYellow.htm",
            "results.htm", "players.htm", "player100.htm"
        };

        foreach (var file in files)
        {
            var result = await HtmlLeagueParser.ParseHtmlFileAsync(GetSampleFilePath(file));
            Assert.True(result.Success, $"{file} should parse successfully");
        }
    }
}
