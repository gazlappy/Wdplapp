using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Imports historic honours (Roll of Honour) data from Excel (.xlsx) files.
/// Expects tables per season/year with columns: Competition Title, Winners, Runners Up.
/// </summary>
public static class HonoursExcelImporter
{
    public sealed class ImportResult
    {
        public bool Success { get; set; }
        public int HonoursImported { get; set; }
        public List<string> Warnings { get; set; } = [];
        public string? Error { get; set; }
    }

    /// <summary>
    /// Import honours from an Excel (.xlsx) file. Each table block is detected as a season
    /// with a header row containing the year/season name, a column-headers row (Winners / Runners Up),
    /// then data rows with competition title, winner, and runner-up.
    /// </summary>
    public static async Task<ImportResult> ImportAsync(string filePath)
    {
        var result = new ImportResult();

        try
        {
            var (isValid, fileError) = DataStore.ValidateImportFile(filePath);
            if (!isValid)
            {
                result.Error = fileError ?? "Invalid file";
                return result;
            }

            var rows = await ReadExcelRowsAsync(filePath);
            if (rows.Count == 0)
            {
                result.Error = "No data found in file";
                return result;
            }

            var honours = ParseHonours(rows, result.Warnings);
            
            var settings = DataStore.Data.WebsiteSettings;
            settings.HistoricHonours.AddRange(honours);
            result.HonoursImported = honours.Count;
            result.Success = honours.Count > 0;

            return result;
        }
        catch (Exception ex)
        {
            result.Error = $"Import error: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Parse honour records from raw rows. Detects season header rows (single merged-like cell with a year)
    /// and column header rows (containing "Winner" / "Runner"), then reads data rows.
    /// </summary>
    internal static List<HistoricHonour> ParseHonours(List<string[]> rows, List<string> warnings)
    {
        var honours = new List<HistoricHonour>();
        string currentSeason = "";
        bool expectData = false;
        int winnerCol = -1;
        int runnerUpCol = -1;
        int titleCol = 0;
        int sortOrder = 0;

        foreach (var row in rows)
        {
            if (row.All(string.IsNullOrWhiteSpace))
                continue;

            var nonEmpty = row.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

            // Detect season header: a row where the first non-empty cell looks like a year/range
            // Allow 1-2 non-empty cells (Excel may have formatting artefacts in other columns)
            if (nonEmpty.Count <= 2)
            {
                var candidate = nonEmpty[0];
                if (IsSeasonHeader(candidate))
                {
                    currentSeason = candidate.Trim();
                    // Keep winnerCol/runnerUpCol/titleCol from the previous section
                    // since columns are typically the same throughout the file.
                    // Only reset expectData if we haven't seen columns yet.
                    if (winnerCol >= 0) expectData = true;
                    else expectData = false;
                    sortOrder = 0;
                    continue;
                }
            }

            // Detect column headers row: contains "Winner" and/or "Runner"
            if (HasColumnHeaders(row))
            {
                winnerCol = Array.FindIndex(row, c => c != null && c.Contains("Winner", StringComparison.OrdinalIgnoreCase));
                runnerUpCol = Array.FindIndex(row, c => c != null && (c.Contains("Runner", StringComparison.OrdinalIgnoreCase)));
                // Title column is the first column that has data and isn't winner/runner-up.
                // Column 0 is often empty (Excel formatting) so scan for the real title column.
                titleCol = -1;
                for (int ci = 0; ci < row.Length; ci++)
                {
                    if (ci != winnerCol && ci != runnerUpCol)
                    {
                        titleCol = ci;
                        break;
                    }
                }
                if (titleCol < 0) titleCol = 0;
                expectData = true;
                continue;
            }

            // Data row
            if (expectData && !string.IsNullOrWhiteSpace(currentSeason))
            {
                var title = titleCol < row.Length ? row[titleCol]?.Trim() ?? "" : "";
                var winner = winnerCol >= 0 && winnerCol < row.Length ? row[winnerCol]?.Trim() ?? "" : "";
                var runnerUp = runnerUpCol >= 0 && runnerUpCol < row.Length ? row[runnerUpCol]?.Trim() ?? "" : "";

                // If title column is empty, try the next non-winner/runner column
                if (string.IsNullOrWhiteSpace(title))
                {
                    for (int ci = titleCol + 1; ci < row.Length; ci++)
                    {
                        if (ci != winnerCol && ci != runnerUpCol && !string.IsNullOrWhiteSpace(row[ci]))
                        {
                            title = row[ci].Trim();
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(winner) && string.IsNullOrWhiteSpace(runnerUp))
                    continue;

                if (!string.IsNullOrWhiteSpace(title))
                {
                    honours.Add(new HistoricHonour
                    {
                        Season = currentSeason,
                        Title = title,
                        Winner = winner,
                        RunnerUp = runnerUp,
                        SortOrder = sortOrder++
                    });
                }
            }
            else if (!expectData && nonEmpty.Count >= 2 && !string.IsNullOrWhiteSpace(currentSeason))
            {
                // Fallback: if we never found column headers, use first 3 non-empty columns
                var title = nonEmpty.Count > 0 ? nonEmpty[0].Trim() : "";
                var winner = nonEmpty.Count > 1 ? nonEmpty[1].Trim() : "";
                var runnerUp = nonEmpty.Count > 2 ? nonEmpty[2].Trim() : "";

                if (!string.IsNullOrWhiteSpace(title) &&
                    !title.Contains("Winner", StringComparison.OrdinalIgnoreCase) &&
                    !title.Contains("Runner", StringComparison.OrdinalIgnoreCase))
                {
                    honours.Add(new HistoricHonour
                    {
                        Season = currentSeason,
                        Title = title,
                        Winner = winner,
                        RunnerUp = runnerUp,
                        SortOrder = sortOrder++
                    });
                }
            }
        }

        if (honours.Count == 0)
            warnings.Add("Could not detect any honours data. Ensure the file has season headers (e.g. '1994') followed by competition rows.");

        return honours;
    }

    private static bool IsSeasonHeader(string value)
    {
        var trimmed = value.Trim();
        // Match patterns like "1994", "1988 - 1991", "2023/24", "Season 2024"
        return Regex.IsMatch(trimmed, @"^\d{4}(\s*[-/]\s*\d{2,4})?$") ||
               Regex.IsMatch(trimmed, @"^(Season\s+)?\d{4}", RegexOptions.IgnoreCase);
    }

    private static bool HasColumnHeaders(string[] row)
    {
        return row.Any(c => c != null && c.Contains("Winner", StringComparison.OrdinalIgnoreCase)) ||
               row.Any(c => c != null && c.Contains("Runner", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Read all rows from ALL worksheets of an .xlsx file using raw ZIP/XML parsing.
    /// Sheets are read in order, separated by an empty row so the parser sees season boundaries.
    /// </summary>
    private static async Task<List<string[]>> ReadExcelRowsAsync(string filePath)
    {
        var rows = new List<string[]>();

        using var archive = ZipFile.OpenRead(filePath);

        // Load shared strings
        var sharedStrings = new List<string>();
        var ssEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (ssEntry != null)
        {
            using var stream = ssEntry.Open();
            using var reader = new StreamReader(stream);
            var xml = await reader.ReadToEndAsync();
            sharedStrings = ParseSharedStrings(xml);
        }

        // Read ALL worksheets (data may span multiple sheets)
        var sheetEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("xl/worksheets/sheet") && e.FullName.EndsWith(".xml"))
            .OrderBy(e => e.FullName)
            .ToList();

        foreach (var sheetEntry in sheetEntries)
        {
            // Add a blank row between sheets so the parser sees a boundary
            if (rows.Count > 0)
                rows.Add(new string[] { "" });

            using var sheetStream = sheetEntry.Open();
            using var sheetReader = new StreamReader(sheetStream);
            var sheetXml = await sheetReader.ReadToEndAsync();

            // Also read merged cell ranges so we know which cells are merged season headers
            var mergedRanges = new HashSet<string>();
            var mergeMatches = Regex.Matches(sheetXml, @"<mergeCell\s+ref=""([^""]+)""", RegexOptions.Singleline);
            foreach (Match mm in mergeMatches)
                mergedRanges.Add(mm.Groups[1].Value);

            // Extract row data
            var rowMatches = Regex.Matches(sheetXml, @"<row\b[^>]*>(.*?)</row>", RegexOptions.Singleline);
            foreach (Match rowMatch in rowMatches)
            {
                var cells = new Dictionary<int, string>();
                // Match both regular <c ...>...</c> and self-closing <c .../> cells
                var cellMatches = Regex.Matches(rowMatch.Groups[1].Value,
                    @"<c\b([^>]*?)(?:>(.*?)</c>|/>)", RegexOptions.Singleline);

                foreach (Match cellMatch in cellMatches)
                {
                    var attrs = cellMatch.Groups[1].Value;
                    var inner = cellMatch.Groups[2].Value; // empty for self-closing tags

                    var rMatch = Regex.Match(attrs, @"r=""([A-Z]+)(\d+)""");
                    if (!rMatch.Success) continue;
                    var colRef = rMatch.Groups[1].Value;

                    var tMatch = Regex.Match(attrs, @"t=""([^""]*)""");
                    var type = tMatch.Success ? tMatch.Groups[1].Value : "";

                    var vMatch = Regex.Match(inner, @"<v>(.*?)</v>", RegexOptions.Singleline);
                    var value = vMatch.Success ? vMatch.Groups[1].Value : "";

                    var colIndex = ColumnRefToIndex(colRef);
                    string cellValue;

                    if (type == "s" && int.TryParse(value, out var ssIndex) && ssIndex < sharedStrings.Count)
                        cellValue = sharedStrings[ssIndex];
                    else if (type == "inlineStr")
                    {
                        var isMatch = Regex.Match(inner, @"<is>.*?<t[^>]*>(.*?)</t>.*?</is>", RegexOptions.Singleline);
                        cellValue = isMatch.Success ? isMatch.Groups[1].Value : value;
                    }
                    else
                        cellValue = value;

                    if (!string.IsNullOrEmpty(cellValue))
                        cells[colIndex] = System.Net.WebUtility.HtmlDecode(cellValue);
                }

                if (cells.Count > 0)
                {
                    var maxCol = cells.Keys.Max();
                    var row = new string[maxCol + 1];
                    foreach (var (col, val) in cells)
                        row[col] = val;
                    rows.Add(row);
                }
            }
        }

        return rows;
    }

    private static List<string> ParseSharedStrings(string xml)
    {
        var strings = new List<string>();
        var matches = Regex.Matches(xml, @"<si>(.*?)</si>", RegexOptions.Singleline);
        foreach (Match match in matches)
        {
            // Concatenate all <t> elements within the <si> element
            var tMatches = Regex.Matches(match.Groups[1].Value, @"<t[^>]*>(.*?)</t>", RegexOptions.Singleline);
            var text = string.Concat(tMatches.Cast<Match>().Select(m => m.Groups[1].Value));
            strings.Add(System.Net.WebUtility.HtmlDecode(text));
        }
        return strings;
    }

    private static int ColumnRefToIndex(string colRef)
    {
        int index = 0;
        foreach (char c in colRef)
            index = index * 26 + (c - 'A' + 1);
        return index - 1;
    }
}
