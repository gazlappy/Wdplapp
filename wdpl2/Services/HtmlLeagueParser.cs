using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Wdpl2.Services;

/// <summary>
/// Specialized parser for HTML webpages containing league data
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
        public List<HtmlTable> Tables { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        
        // Detected data types
        public bool HasLeagueTable { get; set; }
        public bool HasResults { get; set; }
        public bool HasPlayerStats { get; set; }
        public bool HasFixtures { get; set; }
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
        Fixtures,
        TopScorers,
        Awards
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
            
            // Extract all tables
            result.Tables = ExtractTables(html);
            
            // Detect table types
            foreach (var table in result.Tables)
            {
                table.DetectedType = DetectTableType(table);
                
                // Set flags based on detected types
                switch (table.DetectedType)
                {
                    case TableType.LeagueStandings:
                        result.HasLeagueTable = true;
                        break;
                    case TableType.MatchResults:
                        result.HasResults = true;
                        break;
                    case TableType.PlayerStatistics:
                    case TableType.TopScorers:
                        result.HasPlayerStats = true;
                        break;
                    case TableType.Fixtures:
                        result.HasFixtures = true;
                        break;
                }
            }
            
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
    /// Extract page title from HTML
    /// </summary>
    private static string ExtractPageTitle(string html)
    {
        // Try <title> tag first
        var titleMatch = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (titleMatch.Success)
        {
            return CleanText(titleMatch.Groups[1].Value);
        }

        // Try <h1> tag
        var h1Match = Regex.Match(html, @"<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (h1Match.Success)
        {
            return CleanText(h1Match.Groups[1].Value);
        }

        return "Untitled Page";
    }

    /// <summary>
    /// Extract all tables from HTML
    /// </summary>
    private static List<HtmlTable> ExtractTables(string html)
    {
        var tables = new List<HtmlTable>();
        
        // Find all <table> elements
        var tableMatches = Regex.Matches(html, @"<table[^>]*>.*?</table>", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match tableMatch in tableMatches)
        {
            var tableHtml = tableMatch.Value;
            var table = new HtmlTable();

            // Extract caption if present
            var captionMatch = Regex.Match(tableHtml, @"<caption[^>]*>(.*?)</caption>", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (captionMatch.Success)
            {
                table.Caption = CleanText(captionMatch.Groups[1].Value);
            }

            // Check for header row (<thead> or first row with <th>)
            var hasTheadOrTh = Regex.IsMatch(tableHtml, @"<thead|<th", RegexOptions.IgnoreCase);
            table.HasHeaders = hasTheadOrTh;

            // Extract all rows
            var rowMatches = Regex.Matches(tableHtml, @"<tr[^>]*>.*?</tr>", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match rowMatch in rowMatches)
            {
                var rowHtml = rowMatch.Value;
                var cells = new List<string>();

                // Extract cells (both <td> and <th>)
                var cellMatches = Regex.Matches(rowHtml, @"<t[dh][^>]*>(.*?)</t[dh]>", 
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match cellMatch in cellMatches)
                {
                    var cellContent = cellMatch.Groups[1].Value;
                    
                    // Handle nested tags (links, spans, etc.)
                    cellContent = Regex.Replace(cellContent, @"<[^>]+>", " ");
                    cellContent = CleanText(cellContent);
                    
                    cells.Add(cellContent);
                }

                if (cells.Any(c => !string.IsNullOrWhiteSpace(c)))
                {
                    table.Rows.Add(cells);
                }
            }

            // Only add tables with content
            if (table.Rows.Count > 0)
            {
                tables.Add(table);
            }
        }

        return tables;
    }

    /// <summary>
    /// Detect what type of league table this is
    /// </summary>
    private static TableType DetectTableType(HtmlTable table)
    {
        if (table.Rows.Count < 2)
            return TableType.Unknown;

        // Get header row (first row or look for th elements)
        var headerRow = table.HasHeaders && table.Rows.Any() 
            ? string.Join(" ", table.Rows.First()).ToLower()
            : "";

        // If no clear headers, check first few rows
        if (string.IsNullOrWhiteSpace(headerRow))
        {
            headerRow = string.Join(" ", table.Rows.Take(3).SelectMany(r => r)).ToLower();
        }

        // Detect league standings table
        if ((headerRow.Contains("team") || headerRow.Contains("pos") || headerRow.Contains("position")) &&
            (headerRow.Contains("points") || headerRow.Contains("pts")) &&
            (headerRow.Contains("played") || headerRow.Contains("p") || headerRow.Contains("pld")))
        {
            return TableType.LeagueStandings;
        }

        // Detect match results
        if ((headerRow.Contains("home") && headerRow.Contains("away")) ||
            (headerRow.Contains("result") && headerRow.Contains("score")) ||
            headerRow.Contains("fixture"))
        {
            return TableType.MatchResults;
        }

        // Detect fixtures (upcoming matches)
        if ((headerRow.Contains("fixture") || headerRow.Contains("upcoming")) &&
            (headerRow.Contains("date") || headerRow.Contains("venue")))
        {
            return TableType.Fixtures;
        }

        // Detect player statistics
        if (headerRow.Contains("player") && 
            (headerRow.Contains("goals") || headerRow.Contains("frames") || 
             headerRow.Contains("wins") || headerRow.Contains("rating")))
        {
            return TableType.PlayerStatistics;
        }

        // Detect top scorers
        if (headerRow.Contains("player") && 
            (headerRow.Contains("goals") || headerRow.Contains("frames won")))
        {
            return TableType.TopScorers;
        }

        // Detect awards/trophies
        if (headerRow.Contains("award") || headerRow.Contains("trophy") || 
            headerRow.Contains("winner"))
        {
            return TableType.Awards;
        }

        return TableType.Unknown;
    }

    /// <summary>
    /// Clean HTML entities and extra whitespace
    /// </summary>
    private static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Decode common HTML entities
        text = text.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'")
                   .Replace("&apos;", "'");

        // Remove extra whitespace
        text = Regex.Replace(text, @"\s+", " ");
        
        return text.Trim();
    }

    /// <summary>
    /// Extract structured league standings data
    /// </summary>
    public static List<LeagueStandingRow> ParseLeagueStandings(HtmlTable table)
    {
        var standings = new List<LeagueStandingRow>();

        if (table.DetectedType != TableType.LeagueStandings || table.Rows.Count < 2)
            return standings;

        // Skip header row if present
        var dataRows = table.HasHeaders ? table.Rows.Skip(1) : table.Rows;

        foreach (var row in dataRows)
        {
            // Try to parse common league table formats
            var standing = TryParseStandingRow(row);
            if (standing != null)
            {
                standings.Add(standing);
            }
        }

        return standings;
    }

    private static LeagueStandingRow? TryParseStandingRow(List<string> cells)
    {
        if (cells.Count < 4)
            return null;

        var standing = new LeagueStandingRow();

        // Try to find team name (usually the only non-numeric cell)
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            
            // Skip empty cells
            if (string.IsNullOrWhiteSpace(cell))
                continue;

            // Try to parse as number
            if (int.TryParse(cell, out var number))
            {
                // Could be position, played, won, etc.
                if (standing.Position == 0 && i < 3)
                    standing.Position = number;
                else if (standing.Played == 0)
                    standing.Played = number;
                else if (standing.Won == 0)
                    standing.Won = number;
                else if (standing.Drawn == 0)
                    standing.Drawn = number;
                else if (standing.Lost == 0)
                    standing.Lost = number;
                else if (standing.FramesFor == 0)
                    standing.FramesFor = number;
                else if (standing.FramesAgainst == 0)
                    standing.FramesAgainst = number;
                else if (standing.Points == 0)
                    standing.Points = number;
            }
            else if (string.IsNullOrWhiteSpace(standing.TeamName))
            {
                // Assume this is the team name
                standing.TeamName = cell;
            }
        }

        // Validate we got at least a team name and some data
        return !string.IsNullOrWhiteSpace(standing.TeamName) && standing.Points > 0 
            ? standing 
            : null;
    }

    /// <summary>
    /// Extract match results
    /// </summary>
    public static List<MatchResultRow> ParseMatchResults(HtmlTable table)
    {
        var results = new List<MatchResultRow>();

        if (table.DetectedType != TableType.MatchResults || table.Rows.Count < 2)
            return results;

        var dataRows = table.HasHeaders ? table.Rows.Skip(1) : table.Rows;

        foreach (var row in dataRows)
        {
            var result = TryParseResultRow(row);
            if (result != null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    private static MatchResultRow? TryParseResultRow(List<string> cells)
    {
        if (cells.Count < 3)
            return null;

        var result = new MatchResultRow();

        // Look for team names and scores
        // Common formats:
        // - Home Team | Score | Away Team
        // - Date | Home Team | Away Team | Score
        // - Home Team vs Away Team | Score

        foreach (var cell in cells)
        {
            // Try to parse as score (e.g., "5-3", "6-4")
            var scoreMatch = Regex.Match(cell, @"(\d+)\s*[-:]\s*(\d+)");
            if (scoreMatch.Success)
            {
                result.HomeScore = int.Parse(scoreMatch.Groups[1].Value);
                result.AwayScore = int.Parse(scoreMatch.Groups[2].Value);
                continue;
            }

            // Try to parse as date
            if (DateTime.TryParse(cell, out var date))
            {
                result.Date = date;
                continue;
            }

            // Assume it's a team name
            if (string.IsNullOrWhiteSpace(result.HomeTeam))
            {
                result.HomeTeam = cell;
            }
            else if (string.IsNullOrWhiteSpace(result.AwayTeam))
            {
                result.AwayTeam = cell;
            }
        }

        // Validate we got both teams
        return !string.IsNullOrWhiteSpace(result.HomeTeam) && 
               !string.IsNullOrWhiteSpace(result.AwayTeam)
            ? result
            : null;
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
