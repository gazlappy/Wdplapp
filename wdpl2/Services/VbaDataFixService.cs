using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Service to fix fixture dates based on VBA database exports.
/// Uses tblmatchdates and tblmatchheader to correct dates.
/// </summary>
public static class VbaDataFixService
{
    /// <summary>
    /// Parsed match date from VBA tblmatchdates
    /// </summary>
    public class VbaMatchDate
    {
        public int MatchDatesID { get; set; }
        public int Week { get; set; }
        public DateTime MatchDate { get; set; }
    }

    /// <summary>
    /// Parsed match header from VBA tblmatchheader
    /// </summary>
    public class VbaMatchHeader
    {
        public int MatchHeaderID { get; set; }
        public int MatchNo { get; set; }
        public int WeekNo { get; set; }
        public int Division { get; set; }
        public int TeamHome { get; set; }
        public int TeamAway { get; set; }
    }

    /// <summary>
    /// Result of the fix operation
    /// </summary>
    public class FixResult
    {
        public bool Success { get; set; }
        public int FixturesUpdated { get; set; }
        public int FixturesNotFound { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Log { get; set; } = new();
        public string Summary => $"Updated: {FixturesUpdated}, Not Found: {FixturesNotFound}, Errors: {Errors.Count}";
    }

    // VBA Team ID to Name mapping (from tblteams.txt)
    private static readonly Dictionary<int, string> VbaTeamNames = new()
    {
        { 1, "HERE 4 THE BEER" },
        { 2, "TWO TEES" },
        { 3, "RATTLED BLACKS" },
        { 4, "THE JETS" },
        { 5, "MILO'S MINIONS" },
        { 6, "POCKET ROCKETS" },
        { 7, "LEGENDS" },
        { 8, "S.S.SQUAD" },
        { 9, "SNIPERS" },
        { 10, "MUTTS NUTTS" },
        { 11, "ROW REBELS" },
        { 12, "OLD BLUES" },
        { 13, "CHANGING LANES" },
        { 14, "BYE" }
    };

    /// <summary>
    /// Fix fixture dates based on VBA data files in the VBA_Data folder.
    /// </summary>
    public static FixResult FixFixtureDates(
        List<Fixture> fixtures,
        List<Team> teams,
        string vbaDataFolder)
    {
        var result = new FixResult { Success = true };

        try
        {
            // Parse VBA data files
            var matchDatesPath = Path.Combine(vbaDataFolder, "tblmatchdates.txt");
            var matchHeaderPath = Path.Combine(vbaDataFolder, "tblmatchheader.txt");

            if (!File.Exists(matchDatesPath))
            {
                result.Success = false;
                result.Errors.Add($"Match dates file not found: {matchDatesPath}");
                return result;
            }

            if (!File.Exists(matchHeaderPath))
            {
                result.Success = false;
                result.Errors.Add($"Match header file not found: {matchHeaderPath}");
                return result;
            }

            // Parse match dates (week -> date mapping)
            var matchDates = ParseMatchDates(File.ReadAllText(matchDatesPath));
            result.Log.Add($"Parsed {matchDates.Count} match dates from VBA");
            
            if (!matchDates.Any())
            {
                result.Success = false;
                result.Errors.Add("No match dates parsed from file");
                return result;
            }

            // Parse match headers (match -> week + teams)
            var matchHeaders = ParseMatchHeaders(File.ReadAllText(matchHeaderPath));
            result.Log.Add($"Parsed {matchHeaders.Count} match headers from VBA");
            
            if (!matchHeaders.Any())
            {
                result.Success = false;
                result.Errors.Add("No match headers parsed from file");
                return result;
            }

            // Create week -> date lookup
            var weekToDate = matchDates.ToDictionary(m => m.Week, m => m.MatchDate);
            result.Log.Add($"Week dates: {string.Join(", ", weekToDate.Take(5).Select(kvp => $"W{kvp.Key}={kvp.Value:dd/MM}"))}...");

            // Build mapping from app Team Guid to VBA Team ID
            var teamGuidToVbaId = BuildTeamGuidToVbaIdMapping(teams, result);
            result.Log.Add($"Mapped {teamGuidToVbaId.Count} teams to VBA IDs");

            // Log the app teams for debugging
            result.Log.Add("=== APP TEAMS ===");
            foreach (var team in teams.OrderBy(t => t.Name))
            {
                var vbaId = teamGuidToVbaId.TryGetValue(team.Id, out var id) ? id.ToString() : "NOT MAPPED";
                result.Log.Add($"  {team.Name} -> VBA ID: {vbaId}");
            }

            // Process each fixture
            int processed = 0;
            foreach (var fixture in fixtures)
            {
                processed++;
                
                // Find the teams
                var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);

                if (homeTeam == null || awayTeam == null)
                {
                    result.Warnings.Add($"Fixture #{processed}: Teams not found (Home:{fixture.HomeTeamId}, Away:{fixture.AwayTeamId})");
                    result.FixturesNotFound++;
                    continue;
                }

                // Get VBA team IDs
                if (!teamGuidToVbaId.TryGetValue(fixture.HomeTeamId, out var vbaHomeId))
                {
                    result.Warnings.Add($"Fixture #{processed}: VBA ID not found for home team '{homeTeam.Name}'");
                    result.FixturesNotFound++;
                    continue;
                }

                if (!teamGuidToVbaId.TryGetValue(fixture.AwayTeamId, out var vbaAwayId))
                {
                    result.Warnings.Add($"Fixture #{processed}: VBA ID not found for away team '{awayTeam.Name}'");
                    result.FixturesNotFound++;
                    continue;
                }

                // Find matching VBA match header
                var matchHeader = matchHeaders.FirstOrDefault(mh =>
                    mh.TeamHome == vbaHomeId && mh.TeamAway == vbaAwayId);

                if (matchHeader == null)
                {
                    result.Warnings.Add($"Fixture #{processed}: No VBA match found for {homeTeam.Name} (VBA:{vbaHomeId}) vs {awayTeam.Name} (VBA:{vbaAwayId})");
                    result.FixturesNotFound++;
                    continue;
                }

                // Get the correct date from week number
                if (!weekToDate.TryGetValue(matchHeader.WeekNo, out var correctDate))
                {
                    result.Warnings.Add($"Fixture #{processed}: Week {matchHeader.WeekNo} not found in match dates");
                    result.FixturesNotFound++;
                    continue;
                }

                // Update the fixture date
                var newDate = correctDate.Date + new TimeSpan(19, 30, 0); // 7:30 PM

                if (fixture.Date.Date != correctDate.Date)
                {
                    result.Log.Add($"FIX: {homeTeam.Name} vs {awayTeam.Name} - Week {matchHeader.WeekNo}: {fixture.Date:dd/MM/yyyy} -> {correctDate:dd/MM/yyyy}");
                    fixture.Date = newDate;
                    result.FixturesUpdated++;
                }
            }

            result.Log.Add($"=== SUMMARY ===");
            result.Log.Add($"Processed: {processed}, Updated: {result.FixturesUpdated}, Not Found: {result.FixturesNotFound}");
            result.Success = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Exception: {ex.Message}");
            result.Log.Add($"EXCEPTION: {ex}");
        }

        return result;
    }

    /// <summary>
    /// Build mapping from app Team Guid to VBA Team ID using fuzzy name matching.
    /// </summary>
    private static Dictionary<Guid, int> BuildTeamGuidToVbaIdMapping(List<Team> teams, FixResult result)
    {
        var mapping = new Dictionary<Guid, int>();

        foreach (var team in teams)
        {
            var teamName = team.Name?.ToUpperInvariant().Trim() ?? "";
            int? vbaId = null;

            // Try exact match first
            foreach (var kvp in VbaTeamNames)
            {
                if (kvp.Value.Equals(teamName, StringComparison.OrdinalIgnoreCase))
                {
                    vbaId = kvp.Key;
                    break;
                }
            }

            // Try fuzzy matching if exact match failed
            if (!vbaId.HasValue)
            {
                vbaId = FuzzyMatchTeamName(teamName);
            }

            if (vbaId.HasValue)
            {
                mapping[team.Id] = vbaId.Value;
            }
            else
            {
                result.Warnings.Add($"Could not map team '{team.Name}' to VBA team ID");
            }
        }

        return mapping;
    }

    /// <summary>
    /// Fuzzy match a team name to a VBA team ID.
    /// </summary>
    private static int? FuzzyMatchTeamName(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
            return null;

        var normalized = teamName.ToUpperInvariant()
            .Replace("'", "")
            .Replace("'", "")
            .Replace(".", "")
            .Replace(" ", "")
            .Trim();

        // Check against normalized VBA names
        var vbaNameVariants = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // Team 1 - HERE 4 THE BEER
            { "HERE4THEBEER", 1 },
            { "HEREFORTHEBEER", 1 },
            { "HERE FOR THE BEER", 1 },
            
            // Team 2 - TWO TEES
            { "TWOTEES", 2 },
            { "2TEES", 2 },
            
            // Team 3 - RATTLED BLACKS
            { "RATTLEDBLACKS", 3 },
            
            // Team 4 - THE JETS
            { "THEJETS", 4 },
            { "JETS", 4 },
            
            // Team 5 - MILO'S MINIONS
            { "MILOSMINIONS", 5 },
            { "MILOS MINIONS", 5 },
            
            // Team 6 - POCKET ROCKETS
            { "POCKETROCKETS", 6 },
            
            // Team 7 - LEGENDS
            { "LEGENDS", 7 },
            
            // Team 8 - S.S.SQUAD
            { "SSSQUAD", 8 },
            { "SSQUAD", 8 },
            { "SS SQUAD", 8 },
            
            // Team 9 - SNIPERS
            { "SNIPERS", 9 },
            
            // Team 10 - MUTTS NUTTS
            { "MUTTSNUTTS", 10 },
            { "MUTT'S NUTTS", 10 },
            { "MUTTS NUTTS", 10 },
            
            // Team 11 - ROW REBELS
            { "ROWREBELS", 11 },
            { "ROWS REBELS", 11 },
            
            // Team 12 - OLD BLUES
            { "OLDBLUES", 12 },
            
            // Team 13 - CHANGING LANES
            { "CHANGINGLANES", 13 },
            
            // Team 14 - BYE
            { "BYE", 14 },
        };

        // Try normalized match
        if (vbaNameVariants.TryGetValue(normalized, out var id))
            return id;

        // Try contains match
        foreach (var kvp in VbaTeamNames)
        {
            var vbaNormalized = kvp.Value.Replace("'", "").Replace(".", "").Replace(" ", "").ToUpperInvariant();
            
            // Check if either contains the other
            if (normalized.Contains(vbaNormalized) || vbaNormalized.Contains(normalized))
                return kvp.Key;
        }

        // Try partial word match
        foreach (var kvp in VbaTeamNames)
        {
            var words = kvp.Value.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // If the main word matches (e.g., "LEGENDS", "SNIPERS")
            foreach (var word in words)
            {
                if (word.Length >= 4 && normalized.Contains(word))
                    return kvp.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Parse tblmatchdates.txt content
    /// </summary>
    private static List<VbaMatchDate> ParseMatchDates(string content)
    {
        var results = new List<VbaMatchDate>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Skip comments and headers
            if (line.TrimStart().StartsWith("#") || 
                line.Contains("MatchDatesID") ||
                string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split('\t');
            if (parts.Length >= 4)
            {
                try
                {
                    // Format: MatchDatesID, League, week, MatchDate, Complete
                    var matchDate = new VbaMatchDate
                    {
                        MatchDatesID = int.Parse(parts[0].Trim()),
                        Week = int.Parse(parts[2].Trim()),
                    };

                    // Parse date (dd/MM/yyyy format)
                    if (DateTime.TryParseExact(parts[3].Trim(), "dd/MM/yyyy", 
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        matchDate.MatchDate = date;
                        results.Add(matchDate);
                    }
                }
                catch
                {
                    // Skip malformed lines
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Parse tblmatchheader.txt content
    /// </summary>
    private static List<VbaMatchHeader> ParseMatchHeaders(string content)
    {
        var results = new List<VbaMatchHeader>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Skip comments and headers
            if (line.TrimStart().StartsWith("#") || 
                line.Contains("MatchHeaderID") ||
                string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split('\t');
            if (parts.Length >= 7)
            {
                try
                {
                    // Format: MatchHeaderID, MatchNo, WeekNo, Division, TeamHome, TeamAway, Locked
                    results.Add(new VbaMatchHeader
                    {
                        MatchHeaderID = int.Parse(parts[0].Trim()),
                        MatchNo = int.Parse(parts[1].Trim()),
                        WeekNo = int.Parse(parts[2].Trim()),
                        Division = int.Parse(parts[3].Trim()),
                        TeamHome = int.Parse(parts[4].Trim()),
                        TeamAway = int.Parse(parts[5].Trim())
                    });
                }
                catch
                {
                    // Skip malformed lines
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Convenience method to fix dates using default VBA_Data folder location.
    /// </summary>
    public static FixResult FixFixtureDatesFromVbaData(Guid seasonId)
    {
        var data = DataStore.Data;
        var fixtures = data.Fixtures.Where(f => f.SeasonId == seasonId).ToList();
        var teams = data.Teams.Where(t => t.SeasonId == seasonId).ToList();

        // Try to find VBA_Data folder
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "VBA_Data"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VBA_Data"),
            @"C:\Users\bobgc\source\repos\gazlappy\Wdplapp\wdpl2\VBA_Data"
        };

        string? vbaDataFolder = possiblePaths.FirstOrDefault(Directory.Exists);

        if (vbaDataFolder == null)
        {
            return new FixResult
            {
                Success = false,
                Errors = { "VBA_Data folder not found. Please ensure VBA data files are available." }
            };
        }

        return FixFixtureDates(fixtures, teams, vbaDataFolder);
    }

    /// <summary>
    /// Also update the season start date to match VBA data.
    /// </summary>
    public static void FixSeasonStartDate(Guid seasonId, string vbaDataFolder)
    {
        var matchDatesPath = Path.Combine(vbaDataFolder, "tblmatchdates.txt");
        if (!File.Exists(matchDatesPath)) return;

        var matchDates = ParseMatchDates(File.ReadAllText(matchDatesPath));
        var week1Date = matchDates.FirstOrDefault(m => m.Week == 1);

        if (week1Date != null)
        {
            var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == seasonId);
            if (season != null && season.StartDate != week1Date.MatchDate)
            {
                System.Diagnostics.Debug.WriteLine($"Fixing season start date: {season.StartDate:dd/MM/yyyy} -> {week1Date.MatchDate:dd/MM/yyyy}");
                season.StartDate = week1Date.MatchDate;
            }
        }
    }

    /// <summary>
    /// Get a diagnostic report of team mappings without making changes.
    /// </summary>
    public static string GetTeamMappingDiagnostics(Guid seasonId, string vbaDataFolder)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== VBA DATA FIX DIAGNOSTICS ===\n");

        try
        {
            var teams = DataStore.Data.Teams.Where(t => t.SeasonId == seasonId).OrderBy(t => t.Name).ToList();
            var fixtures = DataStore.Data.Fixtures.Where(f => f.SeasonId == seasonId).ToList();

            sb.AppendLine($"Season has {teams.Count} teams and {fixtures.Count} fixtures\n");

            // Parse VBA data
            var matchDatesPath = Path.Combine(vbaDataFolder, "tblmatchdates.txt");
            var matchHeaderPath = Path.Combine(vbaDataFolder, "tblmatchheader.txt");

            if (!File.Exists(matchDatesPath) || !File.Exists(matchHeaderPath))
            {
                sb.AppendLine("? VBA data files not found!");
                return sb.ToString();
            }

            var matchDates = ParseMatchDates(File.ReadAllText(matchDatesPath));
            var matchHeaders = ParseMatchHeaders(File.ReadAllText(matchHeaderPath));

            sb.AppendLine($"VBA has {matchDates.Count} match dates and {matchHeaders.Count} match headers\n");

            // Show week -> date mapping
            sb.AppendLine("Week -> Date Mapping:");
            foreach (var md in matchDates.OrderBy(m => m.Week).Take(15))
            {
                sb.AppendLine($"  Week {md.Week,2}: {md.MatchDate:ddd dd/MM/yyyy}");
            }
            sb.AppendLine("  ...\n");

            // Build and show team mapping
            var result = new FixResult();
            var mapping = BuildTeamGuidToVbaIdMapping(teams, result);

            sb.AppendLine("Team Mapping (App -> VBA):");
            foreach (var team in teams)
            {
                var status = mapping.TryGetValue(team.Id, out var vbaId)
                    ? $"? VBA ID {vbaId,2} ({VbaTeamNames.GetValueOrDefault(vbaId, "?")})"
                    : "? NOT MAPPED";
                sb.AppendLine($"  {team.Name,-20} -> {status}");
            }

            // Show fixture dates by week
            sb.AppendLine("\nCurrent Fixture Dates:");
            var fixturesByDate = fixtures.GroupBy(f => f.Date.Date).OrderBy(g => g.Key).Take(15);
            foreach (var group in fixturesByDate)
            {
                sb.AppendLine($"  {group.Key:ddd dd/MM/yyyy}: {group.Count()} fixtures");
            }

            // Show warnings
            if (result.Warnings.Any())
            {
                sb.AppendLine("\nWarnings:");
                foreach (var w in result.Warnings)
                    sb.AppendLine($"  ?? {w}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"\n? Error: {ex.Message}");
        }

        return sb.ToString();
    }
}
