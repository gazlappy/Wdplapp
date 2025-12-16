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
public static class HtmlLeagueParser
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
                    ProcessPlayerRatings(result);
                    break;
                    
                case PageType.PlayerProfile:
                    ProcessPlayerProfile(result, html);
                    break;
            }
            
            // Set flags based on what was extracted
            result.HasLeagueTable = result.Teams.Any();
            result.HasResults = result.Results.Any();
            result.HasPlayerStats = result.Players.Any();
            result.HasPlayerProfile = result.PlayerProfile != null;
            
            // Validate
            if (!result.Tables.Any())
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
        
        // Player profile pages
        if (lowerHeading.Contains("record of") || lowerFileName.StartsWith("player"))
            return PageType.PlayerProfile;
            
        // League table pages
        if (lowerHeading.Contains("division table") || lowerFileName.StartsWith("table"))
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
        var match = Regex.Match(heading, @"(\w+)\s+Division", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value + " Division";
        }
        return null;
    }

    /// <summary>
    /// Process league table page
    /// </summary>
    private static void ProcessLeagueTable(HtmlParseResult result)
    {
        if (!result.Tables.Any()) return;
        
        var table = result.Tables.First();
        var division = result.DetectedDivision ?? "Unknown Division";
        
        // Skip header row
        var dataRows = table.Rows.Skip(1);
        
        foreach (var row in dataRows)
        {
            if (row.Count < 9) continue;
            
            var team = new ExtractedTeam
            {
                Division = division,
                Position = ParseInt(row[0]),
                Name = CleanText(row[1]),
                Played = ParseInt(row[2]),
                Won = ParseInt(row[3]),
                Lost = ParseInt(row[4]),
                FramesFor = ParseInt(row[5]),
                FramesAgainst = ParseInt(row[6]),
                PointsDeducted = ParseInt(row[7]),
                Points = ParseInt(row[8])
            };
            
            if (!string.IsNullOrWhiteSpace(team.Name))
            {
                result.Teams.Add(team);
            }
        }
    }

    /// <summary>
    /// Process results page
    /// </summary>
    private static void ProcessResults(HtmlParseResult result)
    {
        if (!result.Tables.Any()) return;
        
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
    /// Process player ratings page
    /// </summary>
    private static void ProcessPlayerRatings(HtmlParseResult result)
    {
        if (!result.Tables.Any()) return;
        
        var table = result.Tables.First();
        var division = result.DetectedDivision ?? "Unknown Division";
        
        // Skip header row
        var dataRows = table.Rows.Skip(1);
        
        foreach (var row in dataRows)
        {
            if (row.Count < 9) continue;
            
            var player = new ExtractedPlayer
            {
                Division = division,
                Position = ParseInt(row[0]),
                Name = CleanText(row[1]),
                TeamName = CleanText(row[2]),
                Played = ParseInt(row[3]),
                Won = ParseInt(row[4]),
                Lost = ParseInt(row[5]),
                EightBalls = ParseInt(row[6]),
                BestRating = ParseInt(row[7]),
                CurrentRating = ParseInt(row[8])
            };
            
            // Extract profile link if present
            // The raw HTML for player name contains: <A HREF="player123.htm">Name</A>
            // We need to extract the link separately from the original HTML
            
            if (!string.IsNullOrWhiteSpace(player.Name))
            {
                result.Players.Add(player);
            }
        }
    }

    /// <summary>
    /// Process player profile page
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
                    
                var matchRecord = new PlayerMatchRecord
                {
                    Date = matchDate,
                    OpponentName = CleanText(row[1]),
                    OpponentTeam = CleanText(row[2]),
                    Result = CleanText(row[3])
                };
                
                if (row.Count > 4)
                {
                    matchRecord.RatingAttained = ParseInt(row[4]);
                }
                
                if (!string.IsNullOrWhiteSpace(matchRecord.OpponentName))
                {
                    profile.MatchHistory.Add(matchRecord);
                }
            }
        }
        
        result.PlayerProfile = profile;
    }

    /// <summary>
    /// Extract page title from HTML
    /// </summary>
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
                var cellMatches = Regex.Matches(rowHtml, @"<td[^>]*>(.*?)</td>", 
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match cellMatch in cellMatches)
                {
                    var cellContent = cellMatch.Groups[1].Value;
                    
                    // Strip all HTML tags including FONT, P, A, etc.
                    cellContent = StripHtmlTags(cellContent);
                    cellContent = CleanText(cellContent);
                    
                    cells.Add(cellContent);
                }

                if (cells.Any())
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
