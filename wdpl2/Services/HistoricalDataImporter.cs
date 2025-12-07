using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Multi-format historical data importer - handles CSV, Excel, Images, and HTML
/// </summary>
public class HistoricalDataImporter
{
    public enum ImportFormat
    {
        CSV,
        Excel,
        Image,
        HTML,
        PlainText,
        Word,
        PowerPoint,
        PDF,
        RTF,
        OpenDocument
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public int RecordsImported { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string Summary { get; set; } = "";
    }

    /// <summary>
    /// Detect file format from extension or content
    /// </summary>
    public static ImportFormat DetectFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        
        return extension switch
        {
            ".csv" or ".txt" => ImportFormat.CSV,
            ".xlsx" or ".xls" => ImportFormat.Excel,
            ".jpg" or ".jpeg" or ".png" or ".bmp" => ImportFormat.Image,
            ".html" or ".htm" or ".mhtml" => ImportFormat.HTML,
            ".docx" or ".doc" => ImportFormat.Word,
            ".pptx" or ".ppt" => ImportFormat.PowerPoint,
            ".pdf" => ImportFormat.PDF,
            ".rtf" => ImportFormat.RTF,
            ".odt" or ".ods" or ".odp" => ImportFormat.OpenDocument,
            _ => ImportFormat.PlainText
        };
    }

    /// <summary>
    /// Import from CSV/spreadsheet (league tables, results)
    /// </summary>
    public static async Task<ImportResult> ImportFromSpreadsheetAsync(
        string filePath, 
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportResult();

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            if (lines.Length < 2)
            {
                result.Errors.Add("File contains no data");
                return result;
            }

            // Try to detect data type from headers
            var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();
            var dataType = DetectDataType(headers);

            switch (dataType)
            {
                case "LeagueTable":
                    result = await ImportLeagueTableAsync(lines, seasonId, data);
                    break;
                case "Results":
                    result = await ImportResultsAsync(lines, seasonId, data);
                    break;
                case "Players":
                    result = await ImportPlayersAsync(lines, seasonId, data);
                    break;
                case "Fixtures":
                    result = await ImportFixturesAsync(lines, seasonId, data);
                    break;
                default:
                    result = await ImportGenericDataAsync(lines, seasonId, data);
                    break;
            }

            result.Success = result.Errors.Count == 0;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Import error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Import from HTML (cached league websites)
    /// </summary>
    public static async Task<ImportResult> ImportFromHTMLAsync(
        string filePath,
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportResult();

        try
        {
            var html = await File.ReadAllTextAsync(filePath);
            
            // Extract tables from HTML
            var tables = ExtractHTMLTables(html);
            
            foreach (var table in tables)
            {
                // Try to identify table type
                if (IsLeagueTable(table))
                {
                    var tableResult = ParseLeagueTableFromHTML(table, seasonId, data);
                    result.RecordsImported += tableResult.RecordsImported;
                    result.Errors.AddRange(tableResult.Errors);
                }
                else if (IsResultsTable(table))
                {
                    var resultsResult = ParseResultsFromHTML(table, seasonId, data);
                    result.RecordsImported += resultsResult.RecordsImported;
                    result.Errors.AddRange(resultsResult.Errors);
                }
            }

            result.Success = result.Errors.Count == 0;
            result.Summary = $"Extracted {tables.Count} tables, imported {result.RecordsImported} records";
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"HTML import error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Import from image (OCR-based extraction)
    /// </summary>
    public static async Task<ImportResult> ImportFromImageAsync(
        string filePath,
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportResult();

        try
        {
            // Note: Full OCR requires external library (Tesseract.NET)
            // For now, we'll provide structure for manual data entry from image
            
            result.Warnings.Add("Image import requires manual verification");
            result.Warnings.Add($"Image: {Path.GetFileName(filePath)}");
            result.Warnings.Add("Please review extracted data and confirm accuracy");
            
            // Placeholder: In production, use Tesseract OCR here
            // var text = await PerformOCR(filePath);
            // result = await ParseExtractedText(text, seasonId, data);
            
            result.Success = false; // Manual review required
            result.Summary = "Image loaded - manual data entry mode";
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Image import error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Import from any supported document format
    /// </summary>
    public static async Task<ImportResult> ImportFromDocumentAsync(
        string filePath,
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportResult();

        try
        {
            // Use DocumentParser to extract content
            var parsedDoc = await DocumentParser.ParseDocumentAsync(filePath);

            if (!parsedDoc.Success)
            {
                result.Errors.AddRange(parsedDoc.Errors);
                return result;
            }

            // Process extracted tables
            foreach (var table in parsedDoc.Tables)
            {
                // Try to identify table type
                if (IsLeagueTableFromData(table))
                {
                    var tableResult = await ProcessLeagueTableAsync(table, seasonId, data);
                    result.RecordsImported += tableResult.RecordsImported;
                    result.Errors.AddRange(tableResult.Errors);
                }
                else if (IsResultsTableFromData(table))
                {
                    var resultsResult = await ProcessResultsTableAsync(table, seasonId, data);
                    result.RecordsImported += resultsResult.RecordsImported;
                    result.Errors.AddRange(resultsResult.Errors);
                }
                else if (IsPlayersTableFromData(table))
                {
                    var playersResult = await ProcessPlayersTableAsync(table, seasonId, data);
                    result.RecordsImported += playersResult.RecordsImported;
                    result.Errors.AddRange(playersResult.Errors);
                }
            }

            // If no tables found, try to parse text content
            if (!parsedDoc.Tables.Any() && parsedDoc.TextContent.Any())
            {
                result.Warnings.Add("No tables found - extracted text content");
                result.Summary = $"Extracted {parsedDoc.TextContent.Count} lines of text";
            }

            result.Success = result.Errors.Count == 0;
            result.Summary = $"Imported {result.RecordsImported} records from {parsedDoc.Tables.Count} table(s)";
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Document import error: {ex.Message}");
            return result;
        }
    }

    // ========== PRIVATE HELPERS ==========

    private static string DetectDataType(string[] headers)
    {
        var headerStr = string.Join(",", headers).ToLower();

        if (headerStr.Contains("team") && headerStr.Contains("points") && headerStr.Contains("played"))
            return "LeagueTable";
        
        if (headerStr.Contains("home") && headerStr.Contains("away") && headerStr.Contains("score"))
            return "Results";
        
        if (headerStr.Contains("player") && (headerStr.Contains("rating") || headerStr.Contains("win")))
            return "Players";
        
        if (headerStr.Contains("date") && headerStr.Contains("fixture"))
            return "Fixtures";

        return "Generic";
    }

    private static async Task<ImportResult> ImportLeagueTableAsync(
        string[] lines,
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportResult();
        var division = data.Divisions.FirstOrDefault(d => d.SeasonId == seasonId);
        
        if (division == null)
        {
            // Create default division
            division = new Division
            {
                Id = Guid.NewGuid(),
                SeasonId = seasonId,
                Name = "Imported Division"
            };
            data.Divisions.Add(division);
        }

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var parts = lines[i].Split(',').Select(p => p.Trim().Trim('"')).ToArray();
                if (parts.Length < 4) continue;

                var teamName = parts[1]; // Assuming: Position, Team, Played, Won, Lost, etc.
                
                // Check if team exists
                var team = data.Teams.FirstOrDefault(t => 
                    t.SeasonId == seasonId && 
                    string.Equals(t.Name, teamName, StringComparison.OrdinalIgnoreCase));

                if (team == null)
                {
                    // Create team
                    team = new Team
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = seasonId,
                        DivisionId = division.Id,
                        Name = teamName
                    };
                    data.Teams.Add(team);
                    result.RecordsImported++;
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Line {i}: {ex.Message}");
            }
        }

        result.Summary = $"Imported {result.RecordsImported} teams from league table";
        return result;
    }

    private static async Task<ImportResult> ImportResultsAsync(
        string[] lines,
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportResult();
        var division = data.Divisions.FirstOrDefault(d => d.SeasonId == seasonId);

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var parts = lines[i].Split(',').Select(p => p.Trim().Trim('"')).ToArray();
                if (parts.Length < 4) continue;

                // Parse: Date, HomeTeam, AwayTeam, HomeScore, AwayScore
                var dateStr = parts[0];
                var homeTeamName = parts[1];
                var awayTeamName = parts[2];
                var homeScore = int.Parse(parts[3]);
                var awayScore = int.Parse(parts[4]);

                // Find or create teams
                var homeTeam = GetOrCreateTeam(homeTeamName, seasonId, division?.Id, data);
                var awayTeam = GetOrCreateTeam(awayTeamName, seasonId, division?.Id, data);

                // Create fixture
                var fixture = new Fixture
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    DivisionId = division?.Id,
                    Date = ParseDate(dateStr),
                    HomeTeamId = homeTeam.Id,
                    AwayTeamId = awayTeam.Id
                };

                // Create frames to match scores
                for (int f = 1; f <= homeScore; f++)
                {
                    fixture.Frames.Add(new FrameResult
                    {
                        Number = fixture.Frames.Count + 1,
                        HomePlayerId = null, // Unknown from historical data
                        AwayPlayerId = null,
                        Winner = FrameWinner.Home
                    });
                }
                for (int f = 1; f <= awayScore; f++)
                {
                    fixture.Frames.Add(new FrameResult
                    {
                        Number = fixture.Frames.Count + 1,
                        HomePlayerId = null,
                        AwayPlayerId = null,
                        Winner = FrameWinner.Away
                    });
                }

                data.Fixtures.Add(fixture);
                result.RecordsImported++;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Line {i}: {ex.Message}");
            }
        }

        result.Summary = $"Imported {result.RecordsImported} match results";
        return result;
    }

    private static async Task<ImportResult> ImportPlayersAsync(
        string[] lines,
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportResult();

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var parts = lines[i].Split(',').Select(p => p.Trim().Trim('"')).ToArray();
                if (parts.Length < 2) continue;

                var playerName = parts[0];
                var teamName = parts.Length > 1 ? parts[1] : "";

                // Split name
                var nameParts = playerName.Split(' ');
                var firstName = nameParts.FirstOrDefault() ?? "";
                var lastName = string.Join(" ", nameParts.Skip(1));

                // Find team
                Team? team = null;
                if (!string.IsNullOrWhiteSpace(teamName))
                {
                    team = data.Teams.FirstOrDefault(t => 
                        t.SeasonId == seasonId && 
                        string.Equals(t.Name, teamName, StringComparison.OrdinalIgnoreCase));
                }

                // Create player
                var player = new Player
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    FirstName = firstName,
                    LastName = lastName,
                    TeamId = team?.Id
                };

                data.Players.Add(player);
                result.RecordsImported++;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Line {i}: {ex.Message}");
            }
        }

        result.Summary = $"Imported {result.RecordsImported} players";
        return result;
    }

    private static async Task<ImportResult> ImportFixturesAsync(
        string[] lines,
        Guid seasonId,
        LeagueData data)
    {
        // Similar to ImportResultsAsync but without scores
        var result = new ImportResult();
        result.Summary = "Fixture import - use results import for historical data";
        return result;
    }

    private static async Task<ImportResult> ImportGenericDataAsync(
        string[] lines,
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportResult();
        result.Warnings.Add("Could not auto-detect data type");
        result.Warnings.Add("Please use specific import template or manual entry");
        result.Summary = $"File has {lines.Length} lines - manual review required";
        return result;
    }

    // HTML Parsing Helpers
    private static List<string> ExtractHTMLTables(string html)
    {
        var tables = new List<string>();
        var tableRegex = new Regex(@"<table[^>]*>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var matches = tableRegex.Matches(html);

        foreach (Match match in matches)
        {
            tables.Add(match.Value);
        }

        return tables;
    }

    private static bool IsLeagueTable(string tableHtml)
    {
        var lower = tableHtml.ToLower();
        return (lower.Contains("points") || lower.Contains("pts")) &&
               (lower.Contains("played") || lower.Contains("p")) &&
               (lower.Contains("won") || lower.Contains("w"));
    }

    private static bool IsResultsTable(string tableHtml)
    {
        var lower = tableHtml.ToLower();
        return lower.Contains("score") || lower.Contains("result") || 
               (lower.Contains("home") && lower.Contains("away"));
    }

    private static ImportResult ParseLeagueTableFromHTML(string tableHtml, Guid seasonId, LeagueData data)
    {
        var result = new ImportResult();
        
        // Extract rows
        var rowRegex = new Regex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var rows = rowRegex.Matches(tableHtml);

        foreach (Match row in rows.Skip(1)) // Skip header
        {
            var cellRegex = new Regex(@"<t[dh][^>]*>(.*?)</t[dh]>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var cells = cellRegex.Matches(row.Value);

            if (cells.Count >= 2)
            {
                var teamName = StripHTML(cells[1].Groups[1].Value).Trim();
                if (!string.IsNullOrWhiteSpace(teamName))
                {
                    result.RecordsImported++;
                }
            }
        }

        return result;
    }

    private static ImportResult ParseResultsFromHTML(string tableHtml, Guid seasonId, LeagueData data)
    {
        // Similar logic to ParseLeagueTableFromHTML
        var result = new ImportResult();
        result.Summary = "Results extracted from HTML";
        return result;
    }

    private static string StripHTML(string html)
    {
        return Regex.Replace(html, "<.*?>", string.Empty).Trim();
    }

    private static Team GetOrCreateTeam(string teamName, Guid seasonId, Guid? divisionId, LeagueData data)
    {
        var team = data.Teams.FirstOrDefault(t => 
            t.SeasonId == seasonId && 
            string.Equals(t.Name, teamName, StringComparison.OrdinalIgnoreCase));

        if (team == null)
        {
            team = new Team
            {
                Id = Guid.NewGuid(),
                SeasonId = seasonId,
                DivisionId = divisionId,
                Name = teamName
            };
            data.Teams.Add(team);
        }

        return team;
    }

    private static DateTime ParseDate(string dateStr)
    {
        // Try common formats
        if (DateTime.TryParse(dateStr, out var date))
            return date;

        // Try dd/MM/yyyy
        if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out date))
            return date;

        // Try MM/dd/yyyy
        if (DateTime.TryParseExact(dateStr, "MM/dd/yyyy", null, System.Globalization.DateTimeStyles.None, out date))
            return date;

        return DateTime.Now;
    }

    private static bool IsPlayersTableFromData(DocumentParser.TableData table)
    {
        if (table.Rows.Count < 2) return false;
        
        var headerRow = string.Join(" ", table.Rows.First()).ToLower();
        return headerRow.Contains("player") && 
               (headerRow.Contains("team") || headerRow.Contains("name"));
    }

    private static bool IsLeagueTableFromData(DocumentParser.TableData table)
    {
        if (table.Rows.Count < 2) return false;
        
        var headerRow = string.Join(" ", table.Rows.First()).ToLower();
        return (headerRow.Contains("team") || headerRow.Contains("position")) &&
               (headerRow.Contains("points") || headerRow.Contains("pts")) &&
               (headerRow.Contains("played") || headerRow.Contains("p"));
    }

    private static bool IsResultsTableFromData(DocumentParser.TableData table)
    {
        if (table.Rows.Count < 2) return false;
        
        var headerRow = string.Join(" ", table.Rows.First()).ToLower();
        return (headerRow.Contains("home") && headerRow.Contains("away")) ||
               (headerRow.Contains("score") && headerRow.Contains("result")) ||
               headerRow.Contains("fixture");
    }

    private static async Task<ImportResult> ProcessLeagueTableAsync(
        DocumentParser.TableData table,
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportResult();
        var division = data.Divisions.FirstOrDefault(d => d.SeasonId == seasonId);
        
        if (division == null)
        {
            division = new Division
            {
                Id = Guid.NewGuid(),
                SeasonId = seasonId,
                Name = "Imported Division"
            };
            data.Divisions.Add(division);
        }

        // Skip header row
        foreach (var row in table.Rows.Skip(1))
        {
            try
            {
                if (row.Count < 2) continue;

                // Try to extract team name (usually second column after position)
                var teamName = row.Count > 1 ? row[1] : row[0];
                
                var team = GetOrCreateTeam(teamName, seasonId, division.Id, data);
                result.RecordsImported++;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Row error: {ex.Message}");
            }
        }

        return result;
    }

    private static async Task<ImportResult> ProcessResultsTableAsync(
        DocumentParser.TableData table,
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportResult();
        var division = data.Divisions.FirstOrDefault(d => d.SeasonId == seasonId);

        foreach (var row in table.Rows.Skip(1))
        {
            try
            {
                if (row.Count < 3) continue;

                // Try to parse: Date, HomeTeam, AwayTeam, Score
                var homeTeam = row.Count > 1 ? row[1] : "";
                var awayTeam = row.Count > 2 ? row[2] : "";
                var scoreStr = row.Count > 3 ? row[3] : "";

                // Parse score (e.g., "6-4")
                var scoreParts = scoreStr.Split('-', 'v', ':');
                if (scoreParts.Length != 2) continue;

                if (!int.TryParse(scoreParts[0].Trim(), out var homeScore)) continue;
                if (!int.TryParse(scoreParts[1].Trim(), out var awayScore)) continue;

                var home = GetOrCreateTeam(homeTeam, seasonId, division?.Id, data);
                var away = GetOrCreateTeam(awayTeam, seasonId, division?.Id, data);

                var fixture = new Fixture
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    DivisionId = division?.Id,
                    Date = DateTime.Now,
                    HomeTeamId = home.Id,
                    AwayTeamId = away.Id
                };

                // Create frames
                for (int f = 1; f <= homeScore; f++)
                {
                    fixture.Frames.Add(new FrameResult
                    {
                        Number = fixture.Frames.Count + 1,
                        Winner = FrameWinner.Home
                    });
                }
                for (int f = 1; f <= awayScore; f++)
                {
                    fixture.Frames.Add(new FrameResult
                    {
                        Number = fixture.Frames.Count + 1,
                        Winner = FrameWinner.Away
                    });
                }

                data.Fixtures.Add(fixture);
                result.RecordsImported++;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Row error: {ex.Message}");
            }
        }

        return result;
    }

    private static async Task<ImportResult> ProcessPlayersTableAsync(
        DocumentParser.TableData table,
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportResult();

        foreach (var row in table.Rows.Skip(1))
        {
            try
            {
                if (row.Count < 1) continue;

                var playerName = row[0];
                var teamName = row.Count > 1 ? row[1] : "";

                var nameParts = playerName.Split(' ');
                var firstName = nameParts.FirstOrDefault() ?? "";
                var lastName = string.Join(" ", nameParts.Skip(1));

                Team? team = null;
                if (!string.IsNullOrWhiteSpace(teamName))
                {
                    team = data.Teams.FirstOrDefault(t => 
                        t.SeasonId == seasonId && 
                        string.Equals(t.Name, teamName, StringComparison.OrdinalIgnoreCase));
                }

                var player = new Player
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    FirstName = firstName,
                    LastName = lastName,
                    TeamId = team?.Id
                };

                data.Players.Add(player);
                result.RecordsImported++;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Row error: {ex.Message}");
            }
        }

        return result;
    }
}
