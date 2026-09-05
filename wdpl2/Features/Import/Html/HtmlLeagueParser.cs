using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Wdpl2.Services;

/// <summary>
/// Specialized parser for WDPL HTML webpages containing league data
/// Extracts tables, standings, results, and player information
/// </summary>
public static partial class HtmlLeagueParser
{
    /// <summary>
    /// Result of parsing an HTML file
    /// </summary>
    public class HtmlParseResult
    {
        public bool Success { get; set; }
        public string FileName { get; set; } = "";
        public string PageTitle { get; set; } = "";
        public string PageHeading { get; set; } = "";
        public string? DetectedDivision { get; set; }
        public PageType DetectedPageType { get; set; } = PageType.Unknown;
        public List<HtmlTable> Tables { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        
        // Detected data types
        public bool HasLeagueTable { get; set; }
        public bool HasResults { get; set; }
        public bool HasPlayerStats { get; set; }
        public bool HasFixtures { get; set; }
        public bool HasCompetitions { get; set; }
        public bool HasPlayerProfile { get; set; }
        
        // Extracted data
        public List<ExtractedTeam> Teams { get; set; } = new();
        public List<ExtractedPlayer> Players { get; set; } = new();
        public List<ExtractedResult> Results { get; set; } = new();
        public List<DetectedCompetition> DetectedCompetitions { get; set; } = new();
        public List<ExtractedDoublesEntry> DoublesEntries { get; set; } = new();
        public List<ExtractedPlayerListEntry> PlayerListEntries { get; set; } = new();
        public ExtractedPlayerProfile? PlayerProfile { get; set; }
    }

    public enum PageType
    {
        Unknown,
        LeagueTable,        // tableRed.htm, tableYellow.htm
        Results,            // results.htm
        PlayerRatings,      // singleRed.htm, singleYellow.htm
        DoublesRatings,     // doubleRed.htm
        PlayerProfile,      // player100.htm
        PlayerList,         // players.htm
        Fixtures
    }

    /// <summary>
    /// Represents a detected competition from HTML
    /// </summary>
    public class DetectedCompetition
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "Singles";
        public string? WinnerName { get; set; }
        public string? RunnerUpName { get; set; }
        public string? WinnerTeam { get; set; }
        public string? Score { get; set; }
        public DateTime? Date { get; set; }
    }

    /// <summary>
    /// Extracted team from league table
    /// </summary>
    public class ExtractedTeam
    {
        public int Position { get; set; }
        public string Name { get; set; } = "";
        public string Division { get; set; } = "";
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public int FramesFor { get; set; }
        public int FramesAgainst { get; set; }
        public int PointsDeducted { get; set; }
        public int Points { get; set; }
    }

    /// <summary>
    /// Extracted doubles pair from doubles ratings table
    /// </summary>
    public class ExtractedDoublesEntry
    {
        public int Position { get; set; }
        public string Player1Name { get; set; } = "";
        public string Player2Name { get; set; } = "";
        public string TeamName { get; set; } = "";
        public string Division { get; set; } = "";
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public int BestRating { get; set; }
        public DateTime? BestRatingDate { get; set; }
        public int CurrentRating { get; set; }
        public string? Player1ProfileLink { get; set; }
        public string? Player2ProfileLink { get; set; }
    }

    /// <summary>
    /// Extracted player name from player list page
    /// </summary>
    public class ExtractedPlayerListEntry
    {
        public string Name { get; set; } = "";
        public string? ProfileLink { get; set; }
    }

    /// <summary>
    /// Extracted player from ratings table
    /// </summary>
    public class ExtractedPlayer
    {
        public int Position { get; set; }
        public string Name { get; set; } = "";
        public string TeamName { get; set; } = "";
        public string Division { get; set; } = "";
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public int EightBalls { get; set; }
        public int BestRating { get; set; }
        public int CurrentRating { get; set; }
        public string? ProfileLink { get; set; }
    }

    /// <summary>
    /// Extracted match result
    /// </summary>
    public class ExtractedResult
    {
        public DateTime Date { get; set; }
        public string Division { get; set; } = "";
        public string HomeTeam { get; set; } = "";
        public int HomeScore { get; set; }
        public string AwayTeam { get; set; } = "";
        public int AwayScore { get; set; }
    }

    /// <summary>
    /// Extracted player profile
    /// </summary>
    public class ExtractedPlayerProfile
    {
        public string PlayerName { get; set; } = "";
        public string TeamName { get; set; } = "";
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public int EightBalls { get; set; }
        public int BestRating { get; set; }
        public DateTime? BestRatingDate { get; set; }
        public int CurrentRating { get; set; }
        public List<PlayerMatchRecord> MatchHistory { get; set; } = new();
    }

    /// <summary>
    /// Individual match record from player profile
    /// </summary>
    public class PlayerMatchRecord
    {
        public DateTime Date { get; set; }
        public string OpponentName { get; set; } = "";
        public string OpponentTeam { get; set; } = "";
        public string Result { get; set; } = ""; // "Won" or "Lost"
        public int RatingAttained { get; set; }
        public int Weighting { get; set; }
        public string? OpponentProfileLink { get; set; }
    }

    /// <summary>
    /// Represents a table extracted from HTML
    /// </summary>
    public class HtmlTable
    {
        public string Caption { get; set; } = "";
        public List<List<string>> Rows { get; set; } = new();
        public bool HasHeaders { get; set; }
        public TableType DetectedType { get; set; } = TableType.Unknown;
        
        public int RowCount => Rows.Count;
        public int ColumnCount => Rows.FirstOrDefault()?.Count ?? 0;
    }

    public enum TableType
    {
        Unknown,
        LeagueStandings,
        MatchResults,
        PlayerStatistics,
        PlayerProfile,
        PlayerMatchHistory,
        Fixtures,
        TopScorers,
        Awards,
        Competitions
    }

    /// <summary>
    /// Parse HTML file and extract all league-related data
    /// </summary>
    public static async Task<HtmlParseResult> ParseHtmlFileAsync(string filePath)
    {
        var result = new HtmlParseResult
        {
            FileName = System.IO.Path.GetFileName(filePath)
        };

        try
        {
            var html = await System.IO.File.ReadAllTextAsync(filePath);
            
            // Extract page title
            result.PageTitle = ExtractPageTitle(html);
            
            // Extract page heading (the main <FONT SIZE="6"> heading)
            result.PageHeading = ExtractPageHeading(html);
            
            // Detect page type from heading
            result.DetectedPageType = DetectPageType(result.PageHeading, result.FileName);
            
            // Detect division from heading
            result.DetectedDivision = ExtractDivision(result.PageHeading);
            
            // Extract all tables
            result.Tables = ExtractTables(html);
            
            // Process based on page type
            switch (result.DetectedPageType)
            {
                case PageType.LeagueTable:
                    ProcessLeagueTable(result);
                    break;

                case PageType.Results:
                    ProcessResults(result);
                    break;

                case PageType.PlayerRatings:
                    ProcessPlayerRatings(result, html);
                    break;

                case PageType.DoublesRatings:
                    ProcessDoublesRatings(result, html);
                    break;

                case PageType.PlayerProfile:
                    ProcessPlayerProfile(result, html);
                    break;

                case PageType.PlayerList:
                    ProcessPlayerList(result, html);
                    break;

                case PageType.Fixtures:
                    ProcessFixtures(result);
                    break;
            }

            // Set flags based on what was extracted
            result.HasLeagueTable = result.Teams.Count != 0;
            result.HasResults = result.Results.Count != 0;
            result.HasPlayerStats = result.Players.Count != 0 || result.DoublesEntries.Count != 0;
            result.HasPlayerProfile = result.PlayerProfile != null;
            
            // Validate
            if (result.Tables.Count == 0)
            {
                result.Warnings.Add("No tables found in HTML file");
            }
            
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Parse error: {ex.Message}");
            result.Success = false;
        }

        return result;
    }

    /// <summary>
    /// Detect page type from heading text
    /// </summary>
    private static PageType DetectPageType(string heading, string fileName)
    {
        var lowerHeading = heading.ToLower();
        var lowerFileName = fileName.ToLower();

        // Player list page (must check before player profile)
        if (lowerHeading.Contains("list of players") || lowerFileName == "players.htm")
            return PageType.PlayerList;

        // Player profile pages
        if (lowerHeading.Contains("record of") || lowerFileName.StartsWith("player"))
            return PageType.PlayerProfile;

        // League table pages
        if (lowerHeading.Contains("division table") || lowerHeading.EndsWith(" table") || lowerFileName.StartsWith("table"))
            return PageType.LeagueTable;

        // Results pages
        if (lowerHeading.Contains("results") || lowerFileName == "results.htm")
            return PageType.Results;

        // Player ratings pages
        if (lowerHeading.Contains("player ratings") || lowerFileName.StartsWith("single"))
            return PageType.PlayerRatings;

        // Doubles ratings pages
        if (lowerHeading.Contains("doubles ratings") || lowerFileName.StartsWith("double"))
            return PageType.DoublesRatings;

        // Fixtures pages
        if (lowerHeading.Contains("fixture"))
            return PageType.Fixtures;

        return PageType.Unknown;
    }

    /// <summary>
    /// Extract division name from heading
    /// </summary>
    private static string? ExtractDivision(string heading)
    {
        // Look for division patterns like "Red Division", "Yellow Division"
        var match = MyRegex().Match(heading);
        if (match.Success)
        {
            return match.Groups[1].Value + " Division";
        }

        // Handle headings like "First Table", "Red Doubles Ratings", "Red Player Ratings"
        // Strip known suffixes to find the division word
        var h = heading.Trim();
        var suffixes = new[] { " Table", " Doubles Ratings", " Player Ratings", " Ratings" };
        foreach (var suffix in suffixes)
        {
            if (h.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var core = h[..^suffix.Length].Trim();
                if (!string.IsNullOrWhiteSpace(core) && core.Split(' ').Length <= 2)
                {
                    return core + " Division";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Process league table page
    /// </summary>
    private static void ProcessLeagueTable(HtmlParseResult result)
    {
        if (result.Tables.Count == 0) return;
        var table = FindEntityTable(result, 1, "team", 2);
        if (table == null) return;
        var division = result.DetectedDivision ?? "Unknown Division";

        // Skip header row
        var dataRows = table.Rows.Skip(1);

        foreach (var row in dataRows)
        {
            if (row.Count < 9) continue;

            var position = ParseInt(row[0]);
            var name = CleanText(row[1]);
            var played = ParseInt(row[2]);
            var won = ParseInt(row[3]);
            var lost = ParseInt(row[4]);
            var framesFor = ParseInt(row[5]);
            var framesAgainst = ParseInt(row[6]);
            var pointsDeducted = ParseInt(row[7]);
            var points = ParseInt(row[8]);

            // Validate: position must be > 0 (confirms this is a real data row)
            if (position <= 0) continue;

            // Validate: name must exist and not be purely numeric
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (int.TryParse(name, out _)) continue;

            // Validate: at least one stat column has data (played, won, or points > 0)
            // This filters out misidentified tables
            if (played <= 0 && won <= 0 && points <= 0 && framesFor <= 0) continue;

            var team = new ExtractedTeam
            {
                Division = division,
                Position = position,
                Name = name,
                Played = played,
                Won = won,
                Lost = lost,
                FramesFor = framesFor,
                FramesAgainst = framesAgainst,
                PointsDeducted = pointsDeducted,
                Points = points
            };

            result.Teams.Add(team);
        }
    }

    /// <summary>
    /// Process results page
    /// </summary>
    private static void ProcessResults(HtmlParseResult result)
    {
        if (result.Tables.Count == 0) return;
        
        var table = result.Tables.First();
        
        // Skip header row
        var dataRows = table.Rows.Skip(1);
        
        foreach (var row in dataRows)
        {
            if (row.Count < 6) continue;
            
            // Parse date (DD/MM/YYYY format)
            if (!TryParseDate(row[0], out var date))
                continue;
                
            var matchResult = new ExtractedResult
            {
                Date = date,
                Division = CleanText(row[1]),
                HomeTeam = CleanText(row[2]),
                HomeScore = ParseInt(row[3]),
                AwayTeam = CleanText(row[4]),
                AwayScore = ParseInt(row[5])
            };
            
            if (!string.IsNullOrWhiteSpace(matchResult.HomeTeam) && 
                !string.IsNullOrWhiteSpace(matchResult.AwayTeam))
            {
                result.Results.Add(matchResult);
            }
        }
    }

    /// <summary>
    /// Process player ratings page — also extracts profile links from &lt;A&gt; tags
    /// </summary>
    private static void ProcessPlayerRatings(HtmlParseResult result, string html)
    {
        if (result.Tables.Count == 0) return;
        var table = FindEntityTable(result, 1, "player", 3);
        if (table == null || !IsHeader(table.Rows[0][2], "team")) return;
        var division = result.DetectedDivision ?? "Unknown Division";

        // Pre-extract profile links from raw HTML so we can match them to players
        var profileLinks = ExtractProfileLinksFromHtml(html);

        // Skip header row
        var dataRows = table.Rows.Skip(1);

        foreach (var row in dataRows)
        {
            if (row.Count < 9) continue;

            var position = ParseInt(row[0]);
            var name = CleanText(row[1]);
            var teamName = CleanText(row[2]);
            var played = ParseInt(row[3]);

            // Validate: position must be > 0
            if (position <= 0) continue;

            // Validate: name must exist and not be purely numeric
            if (!IsValidPlayerName(name)) continue;

            // Validate: must have a team name (not a summary row)
            if (string.IsNullOrWhiteSpace(teamName)) continue;
            if (int.TryParse(teamName, out _)) continue;

            var player = new ExtractedPlayer
            {
                Division = division,
                Position = position,
                Name = name,
                TeamName = teamName,
                Played = played,
                Won = ParseInt(row[4]),
                Lost = ParseInt(row[5]),
                EightBalls = ParseInt(row[6]),
                BestRating = ParseInt(row[7]),
                CurrentRating = ParseInt(row[8])
            };

            // Try to find profile link for this player
            if (profileLinks.TryGetValue(name, out var link))
                player.ProfileLink = link;

            result.Players.Add(player);
        }
    }

    /// <summary>
    /// Process player profile page — extracts stats, match history with Weighting, and opponent links
    /// </summary>
    private static void ProcessPlayerProfile(HtmlParseResult result, string html)
    {
        // Extract player name and team from heading
        // Format: "Record of Chris Cannon (Nice Parking)"
        var headingMatch = Regex.Match(result.PageHeading, @"Record of\s+(.+?)\s*\((.+?)\)", RegexOptions.IgnoreCase);
        if (!headingMatch.Success) return;

        var profile = new ExtractedPlayerProfile
        {
            PlayerName = headingMatch.Groups[1].Value.Trim(),
            TeamName = headingMatch.Groups[2].Value.Trim()
        };
        if (!IsValidPlayerName(profile.PlayerName)) return;

        if (result.Tables.Count >= 1)
        {
            // First table has summary stats
            var summaryTable = result.Tables[0];
            if (summaryTable.Rows.Count >= 2)
            {
                var statsRow = summaryTable.Rows[1]; // Second row has the data
                if (statsRow.Count >= 7)
                {
                    profile.Played = ParseInt(statsRow[0]);
                    profile.Won = ParseInt(statsRow[1]);
                    profile.Lost = ParseInt(statsRow[2]);
                    profile.EightBalls = ParseInt(statsRow[3]);
                    profile.BestRating = ParseInt(statsRow[4]);
                    TryParseDate(statsRow[5], out var bestDate);
                    profile.BestRatingDate = bestDate;
                    profile.CurrentRating = ParseInt(statsRow[6]);
                }
            }
        }

        // Pre-extract opponent profile links from raw HTML
        var profileLinks = ExtractProfileLinksFromHtml(html);

        if (result.Tables.Count >= 2)
        {
            // Second table has match history
            var historyTable = result.Tables[1];

            // Skip header row
            var dataRows = historyTable.Rows.Skip(1);

            foreach (var row in dataRows)
            {
                if (row.Count < 5) continue;

                // Skip totals row (has "Totals" in one of the cells)
                if (row.Any(c => c.ToLower().Contains("total")))
                    continue;

                // Skip empty rows
                if (row.All(c => string.IsNullOrWhiteSpace(c)))
                    continue;

                if (!TryParseDate(row[0], out var matchDate))
                    continue;

                var opponentName = CleanText(row[1]);

                var matchRecord = new PlayerMatchRecord
                {
                    Date = matchDate,
                    OpponentName = opponentName,
                    OpponentTeam = CleanText(row[2]),
                    Result = CleanText(row[3])
                };

                if (row.Count > 4)
                {
                    matchRecord.RatingAttained = ParseInt(row[4]);
                }

                // Extract Weighting (column 5)
                if (row.Count > 5)
                {
                    matchRecord.Weighting = ParseInt(row[5]);
                }

                // Try to find opponent profile link
                if (profileLinks.TryGetValue(opponentName, out var link))
                    matchRecord.OpponentProfileLink = link;

                if (IsValidPlayerName(matchRecord.OpponentName))
                {
                    profile.MatchHistory.Add(matchRecord);
                }
            }
        }

        result.PlayerProfile = profile;
    }

    /// <summary>
    /// Process doubles ratings page — 12 columns:
    /// Pos, Player1 No, Player1 Name, Player2 No, Player2 Name, Team, Played, Won, Lost, Best Rating, Attained On, Current Rating
    /// </summary>
    private static void ProcessDoublesRatings(HtmlParseResult result, string html)
    {
        if (result.Tables.Count == 0) return;
        var table = FindEntityTable(result, 5, "team", 6);
        if (table == null) return;
        var division = result.DetectedDivision ?? "Unknown Division";

        // Pre-extract profile links from raw HTML
        var profileLinks = ExtractProfileLinksFromHtml(html);

        var dataRows = table.Rows.Skip(1);

        foreach (var row in dataRows)
        {
            if (row.Count < 12) continue;

            var position = ParseInt(row[0]);
            // row[1] = Player1 No (numeric ID, skip)
            var player1Name = CleanText(row[2]);
            // row[3] = Player2 No (numeric ID, skip)
            var player2Name = CleanText(row[4]);
            var teamName = CleanText(row[5]);
            var played = ParseInt(row[6]);
            var won = ParseInt(row[7]);
            var lost = ParseInt(row[8]);
            var bestRating = ParseInt(row[9]);
            TryParseDate(row[10], out var bestDate);
            var currentRating = ParseInt(row[11]);

            if (position <= 0) continue;
            if (!IsValidPlayerName(player1Name) || !IsValidPlayerName(player2Name)) continue;

            var entry = new ExtractedDoublesEntry
            {
                Division = division,
                Position = position,
                Player1Name = player1Name,
                Player2Name = player2Name,
                TeamName = teamName,
                Played = played,
                Won = won,
                Lost = lost,
                BestRating = bestRating,
                BestRatingDate = bestDate,
                CurrentRating = currentRating
            };

            // Try to find profile links for both players
            if (profileLinks.TryGetValue(player1Name, out var link1))
                entry.Player1ProfileLink = link1;
            if (profileLinks.TryGetValue(player2Name, out var link2))
                entry.Player2ProfileLink = link2;

            result.DoublesEntries.Add(entry);

            // Also add both players to the Players list so they get imported
            AddDoublePlayerAsExtracted(result, player1Name, teamName, division, profileLinks);
            AddDoublePlayerAsExtracted(result, player2Name, teamName, division, profileLinks);
        }
    }

    /// <summary>
    /// Helper: add a doubles player to the Players list if not already present
    /// </summary>
    private static void AddDoublePlayerAsExtracted(HtmlParseResult result, string playerName, string teamName, string division, Dictionary<string, string> profileLinks)
    {
        if (!IsValidPlayerName(playerName)) return;

        // Check if player already exists in the list
        if (result.Players.Any(p => string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase)))
            return;

        var player = new ExtractedPlayer
        {
            Name = playerName,
            TeamName = teamName,
            Division = division
        };

        if (profileLinks.TryGetValue(playerName, out var link))
            player.ProfileLink = link;

        result.Players.Add(player);
    }

    /// <summary>
    /// Process player list page — extracts player names and profile links from &lt;A&gt; tags
    /// Format: &lt;A HREF="player100.htm"&gt;Chris Cannon&lt;/A&gt;
    /// </summary>
    private static void ProcessPlayerList(HtmlParseResult result, string html)
    {
        var linkMatches = Regex.Matches(html, @"<A\s+HREF\s*=\s*""(player\d+\.htm)""[^>]*>(.*?)</A>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in linkMatches)
        {
            var profileLink = match.Groups[1].Value;
            var name = CleanText(StripHtmlTags(match.Groups[2].Value));
            if (!IsValidPlayerName(name)) continue;

            result.PlayerListEntries.Add(new ExtractedPlayerListEntry
            {
                Name = name,
                ProfileLink = profileLink
            });

            // Also add as an ExtractedPlayer (with no team/division — those come from other pages)
            if (!result.Players.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                result.Players.Add(new ExtractedPlayer
                {
                    Name = name,
                    ProfileLink = profileLink
                });
            }
        }
    }

    /// <summary>
    /// Process fixtures page — similar format to results but may include unplayed matches (score = 0-0)
    /// </summary>
    private static void ProcessFixtures(HtmlParseResult result)
    {
        if (result.Tables.Count == 0) return;

        var table = result.Tables.First();
        var dataRows = table.Rows.Skip(1);

        foreach (var row in dataRows)
        {
            if (row.Count < 6) continue;

            if (!TryParseDate(row[0], out var date))
                continue;

            var matchResult = new ExtractedResult
            {
                Date = date,
                Division = CleanText(row[1]),
                HomeTeam = CleanText(row[2]),
                HomeScore = ParseInt(row[3]),
                AwayTeam = CleanText(row[4]),
                AwayScore = ParseInt(row[5])
            };

            if (!string.IsNullOrWhiteSpace(matchResult.HomeTeam) && 
                !string.IsNullOrWhiteSpace(matchResult.AwayTeam))
            {
                result.Results.Add(matchResult);
                result.HasFixtures = true;
            }
        }
    }

    /// <summary>
    /// Extract profile links from raw HTML before tags are stripped.
    /// Returns dictionary of player name → profile filename (e.g. "player100.htm").
    /// </summary>
    private static Dictionary<string, string> ExtractProfileLinksFromHtml(string html)
    {
        var links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var linkMatches = Regex.Matches(html, @"<A\s+HREF\s*=\s*""(player\d+\.htm)""[^>]*>(.*?)</A>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in linkMatches)
        {
            var href = match.Groups[1].Value;
            var name = CleanText(StripHtmlTags(match.Groups[2].Value));
            if (!string.IsNullOrWhiteSpace(name))
            {
                links.TryAdd(name, href);
            }
        }

        return links;
    }

    /// <summary>
    /// Extract page title from HTML
    /// </summary>
    public static bool IsValidPlayerName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        name = CleanText(name);
        if (name.Count(char.IsLetter) < 2) return false;
        return !Regex.IsMatch(name, @"^(?:bye|byes|unknown|player|players|name|total|totals|home|next|previous|index|a\s*[-–]\s*z)$", RegexOptions.IgnoreCase);
    }

    private static bool IsHeader(string value, string entity) =>
        Regex.IsMatch(value, $@"^{entity}(?:\s+name)?$", RegexOptions.IgnoreCase);

    private static HtmlTable? FindEntityTable(HtmlParseResult result, int nameColumn, string entity, int playedColumn)
    {
        return result.Tables.FirstOrDefault(t => t.Rows.Count > 1 &&
            t.Rows[0].Count > Math.Max(nameColumn, playedColumn) &&
            IsHeader(t.Rows[0][nameColumn], entity) &&
            Regex.IsMatch(t.Rows[0][playedColumn], @"^(played|p|pld)$", RegexOptions.IgnoreCase));
    }

    private static string ExtractPageTitle(string html)
    {
        var titleMatch = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (titleMatch.Success)
        {
            return CleanText(titleMatch.Groups[1].Value);
        }
        return "Untitled Page";
    }

    /// <summary>
    /// Extract main page heading (SIZE="6" font)
    /// </summary>
    private static string ExtractPageHeading(string html)
    {
        // Look for <FONT SIZE="6"> heading
        var headingMatch = Regex.Match(html, @"<FONT[^>]*SIZE\s*=\s*[""']?6[""']?[^>]*>(.*?)</FONT>", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (headingMatch.Success)
        {
            return CleanText(headingMatch.Groups[1].Value);
        }
        
        // Fall back to <h1>
        var h1Match = Regex.Match(html, @"<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (h1Match.Success)
        {
            return CleanText(h1Match.Groups[1].Value);
        }
        
        return "";
    }

    /// <summary>
    /// Extract all tables from HTML
    /// </summary>
    private static List<HtmlTable> ExtractTables(string html)
    {
        var tables = new List<HtmlTable>();
        
        var tableMatches = Regex.Matches(html, @"<table[^>]*>.*?</table>", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match tableMatch in tableMatches)
        {
            var tableHtml = tableMatch.Value;
            var table = new HtmlTable();

            // Extract rows
            var rowMatches = Regex.Matches(tableHtml, @"<tr[^>]*>(.*?)</tr>", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            bool isFirstRow = true;
            foreach (Match rowMatch in rowMatches)
            {
                var rowHtml = rowMatch.Groups[1].Value;
                var cells = new List<string>();

                // Extract cells
                var cellMatches = Regex.Matches(rowHtml, @"<t[dh]\b[^>]*>(.*?)</t[dh]>", 
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match cellMatch in cellMatches)
                {
                    var cellContent = cellMatch.Groups[1].Value;
                    
                    // Strip all HTML tags including FONT, P, A, etc.
                    cellContent = StripHtmlTags(cellContent);
                    cellContent = CleanText(cellContent);
                    
                    cells.Add(cellContent);
                }

                if (cells.Count != 0)
                {
                    // First row is usually header
                    if (isFirstRow)
                    {
                        table.HasHeaders = true;
                        isFirstRow = false;
                    }
                    table.Rows.Add(cells);
                }
            }

            if (table.Rows.Count > 0)
            {
                tables.Add(table);
            }
        }

        return tables;
    }

    /// <summary>
    /// Strip all HTML tags
    /// </summary>
    private static string StripHtmlTags(string html)
    {
        // Remove all HTML tags
        html = Regex.Replace(html, @"<[^>]+>", " ", RegexOptions.Singleline);
        return html;
    }

    /// <summary>
    /// Clean text - decode entities and normalize whitespace
    /// </summary>
    private static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Decode HTML entities
        text = text.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'")
                   .Replace("&apos;", "'");

        // Normalize whitespace
        text = Regex.Replace(text, @"\s+", " ");
        
        return text.Trim();
    }

    /// <summary>
    /// Parse integer from string, returning 0 if invalid
    /// </summary>
    private static int ParseInt(string text)
    {
        text = CleanText(text);
        if (int.TryParse(text, out var result))
            return result;
        return 0;
    }

    /// <summary>
    /// Try to parse date in DD/MM/YYYY format
    /// </summary>
    private static bool TryParseDate(string text, out DateTime date)
    {
        text = CleanText(text);
        
        // Try UK date format (DD/MM/YYYY)
        if (DateTime.TryParseExact(text, "dd/MM/yyyy", CultureInfo.InvariantCulture, 
            DateTimeStyles.None, out date))
        {
            return true;
        }
        
        // Try other common formats
        if (DateTime.TryParse(text, out date))
        {
            return true;
        }
        
        date = default;
        return false;
    }

    // ===== Legacy methods for compatibility =====

    /// <summary>
    /// Extract structured league standings data (legacy compatibility)
    /// </summary>
    public static List<LeagueStandingRow> ParseLeagueStandings(HtmlTable table)
    {
        var standings = new List<LeagueStandingRow>();

        if (table.Rows.Count < 2)
            return standings;

        var dataRows = table.HasHeaders ? table.Rows.Skip(1) : table.Rows;

        foreach (var row in dataRows)
        {
            if (row.Count < 4) continue;
            
            var standing = new LeagueStandingRow();
            
            // Try to identify columns
            int col = 0;
            foreach (var cell in row)
            {
                var cleanCell = CleanText(cell);
                
                if (int.TryParse(cleanCell, out var num))
                {
                    // Numeric column
                    if (standing.Position == 0 && col == 0)
                        standing.Position = num;
                    else if (standing.Played == 0)
                        standing.Played = num;
                    else if (standing.Won == 0)
                        standing.Won = num;
                    else if (standing.Lost == 0)
                        standing.Lost = num;
                    else if (standing.FramesFor == 0)
                        standing.FramesFor = num;
                    else if (standing.FramesAgainst == 0)
                        standing.FramesAgainst = num;
                    else if (standing.Points == 0)
                        standing.Points = num;
                }
                else if (string.IsNullOrWhiteSpace(standing.TeamName) && !string.IsNullOrWhiteSpace(cleanCell))
                {
                    standing.TeamName = cleanCell;
                }
                
                col++;
            }

            if (!string.IsNullOrWhiteSpace(standing.TeamName))
            {
                standings.Add(standing);
            }
        }

        return standings;
    }

    /// <summary>
    /// Extract match results (legacy compatibility)
    /// </summary>
    public static List<MatchResultRow> ParseMatchResults(HtmlTable table)
    {
        var results = new List<MatchResultRow>();

        if (table.Rows.Count < 2)
            return results;

        var dataRows = table.HasHeaders ? table.Rows.Skip(1) : table.Rows;

        foreach (var row in dataRows)
        {
            if (row.Count < 3) continue;

            var result = new MatchResultRow();
            
            foreach (var cell in row)
            {
                var cleanCell = CleanText(cell);
                
                // Try to parse as date
                if (TryParseDate(cleanCell, out var date) && result.Date == null)
                {
                    result.Date = date;
                    continue;
                }
                
                // Try to parse as score
                if (int.TryParse(cleanCell, out var score))
                {
                    if (result.HomeScore == 0 && !string.IsNullOrWhiteSpace(result.HomeTeam))
                        result.HomeScore = score;
                    else if (result.AwayScore == 0 && !string.IsNullOrWhiteSpace(result.AwayTeam))
                        result.AwayScore = score;
                    continue;
                }
                
                // Assume team name
                if (string.IsNullOrWhiteSpace(result.HomeTeam))
                    result.HomeTeam = cleanCell;
                else if (string.IsNullOrWhiteSpace(result.AwayTeam))
                    result.AwayTeam = cleanCell;
            }

            if (!string.IsNullOrWhiteSpace(result.HomeTeam) && 
                !string.IsNullOrWhiteSpace(result.AwayTeam))
            {
                results.Add(result);
            }
        }

        return results;
    }

    [GeneratedRegex(@"(\w+)\s+Division", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex MyRegex();
}

/// <summary>
/// Represents a row in a league standings table
/// </summary>
public class LeagueStandingRow
{
    public int Position { get; set; }
    public string TeamName { get; set; } = "";
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int FramesFor { get; set; }
    public int FramesAgainst { get; set; }
    public int FrameDifference => FramesFor - FramesAgainst;
    public int Points { get; set; }
}

/// <summary>
/// Represents a match result row
/// </summary>
public class MatchResultRow
{
    public DateTime? Date { get; set; }
    public string HomeTeam { get; set; } = "";
    public string AwayTeam { get; set; } = "";
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
}
