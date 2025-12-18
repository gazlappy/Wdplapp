using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

/// <summary>
/// Orchestrates the complete Paradox database import process.
/// Coordinates the separate importer services in the correct order
/// to maintain foreign key relationships.
/// 
/// Import order (respecting dependencies):
/// 1. Divisions (no dependencies)
/// 2. Venues (no dependencies)  
/// 3. Teams (depends on Divisions, Venues)
/// 4. Players (depends on Teams)
/// 5. Matches/Fixtures (depends on Teams, Divisions)
/// 6. Singles frames (depends on Matches, Players)
/// 7. Doubles frames (depends on Matches, Players)
/// </summary>
public class ParadoxImportOrchestrator
{
    public class ImportProgress
    {
        public string CurrentStep { get; set; } = "";
        public int TotalSteps { get; set; } = 7;
        public int CurrentStepNumber { get; set; }
        public int ItemsProcessed { get; set; }
        public int TotalItems { get; set; }
    }

    public class ImportSummary
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        
        public int DivisionsImported { get; set; }
        public int VenuesImported { get; set; }
        public int TeamsImported { get; set; }
        public int PlayersImported { get; set; }
        public int FixturesImported { get; set; }
        public int SinglesImported { get; set; }
        public int DoublesImported { get; set; }
        
        public int DivisionsSkipped { get; set; }
        public int VenuesSkipped { get; set; }
        public int TeamsSkipped { get; set; }
        public int PlayersSkipped { get; set; }
        public int FixturesSkipped { get; set; }
        public int SinglesSkipped { get; set; }
        public int DoublesSkipped { get; set; }
        
        public DateTime? SeasonStartDate { get; set; }
        public DateTime? SeasonEndDate { get; set; }
        
        public string GetSummaryText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Import Summary:");
            sb.AppendLine($"  • Divisions: {DivisionsImported} imported, {DivisionsSkipped} skipped");
            sb.AppendLine($"  • Venues: {VenuesImported} imported, {VenuesSkipped} skipped");
            sb.AppendLine($"  • Teams: {TeamsImported} imported, {TeamsSkipped} skipped");
            sb.AppendLine($"  • Players: {PlayersImported} imported, {PlayersSkipped} skipped");
            sb.AppendLine($"  • Fixtures: {FixturesImported} imported, {FixturesSkipped} skipped");
            sb.AppendLine($"  • Singles Frames: {SinglesImported} imported, {SinglesSkipped} skipped");
            sb.AppendLine($"  • Doubles Frames: {DoublesImported} imported, {DoublesSkipped} skipped");
            
            if (SeasonStartDate.HasValue && SeasonEndDate.HasValue)
            {
                sb.AppendLine($"\nSeason dates: {SeasonStartDate.Value:dd/MM/yyyy} - {SeasonEndDate.Value:dd/MM/yyyy}");
            }
            
            return sb.ToString();
        }
    }

    private readonly string _folderPath;
    private Guid _seasonId;
    
    // ID mapping dictionaries (Paradox ID -> App GUID)
    private readonly Dictionary<int, Guid> _divisionMap = new();
    private readonly Dictionary<string, Guid> _divisionNameMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Guid> _venueMap = new();
    private readonly Dictionary<int, Guid> _teamMap = new();
    private readonly Dictionary<int, Guid> _playerMap = new();
    private readonly Dictionary<int, Guid> _matchMap = new();

    public event Action<ImportProgress>? ProgressChanged;

    public ParadoxImportOrchestrator(string folderPath)
    {
        _folderPath = folderPath;
    }

    /// <summary>
    /// Run the complete import process
    /// </summary>
    public async Task<ImportSummary> ImportAsync(Guid seasonId)
    {
        _seasonId = seasonId;
        var summary = new ImportSummary();

        try
        {
            // Step 1: Import Divisions
            ReportProgress("Importing Divisions...", 1);
            var divisionResult = await ImportDivisionsAsync();
            summary.DivisionsImported = divisionResult.ImportedCount;
            summary.DivisionsSkipped = divisionResult.SkippedCount;
            summary.Warnings.AddRange(divisionResult.Warnings);
            summary.Errors.AddRange(divisionResult.Errors);

            // Step 2: Import Venues
            ReportProgress("Importing Venues...", 2);
            var venueResult = await ImportVenuesAsync();
            summary.VenuesImported = venueResult.ImportedCount;
            summary.VenuesSkipped = venueResult.SkippedCount;
            summary.Warnings.AddRange(venueResult.Warnings);
            summary.Errors.AddRange(venueResult.Errors);

            // Step 3: Import Teams
            ReportProgress("Importing Teams...", 3);
            var teamResult = await ImportTeamsAsync();
            summary.TeamsImported = teamResult.ImportedCount;
            summary.TeamsSkipped = teamResult.SkippedCount;
            summary.Warnings.AddRange(teamResult.Warnings);
            summary.Errors.AddRange(teamResult.Errors);

            // Step 4: Import Players
            ReportProgress("Importing Players...", 4);
            var playerResult = await ImportPlayersAsync();
            summary.PlayersImported = playerResult.ImportedCount;
            summary.PlayersSkipped = playerResult.SkippedCount;
            summary.Warnings.AddRange(playerResult.Warnings);
            summary.Errors.AddRange(playerResult.Errors);

            // Step 5: Import Matches/Fixtures
            ReportProgress("Importing Fixtures...", 5);
            var matchResult = await ImportMatchesAsync();
            summary.FixturesImported = matchResult.ImportedCount;
            summary.FixturesSkipped = matchResult.SkippedCount;
            summary.SeasonStartDate = matchResult.MinDate;
            summary.SeasonEndDate = matchResult.MaxDate;
            summary.Warnings.AddRange(matchResult.Warnings);
            summary.Errors.AddRange(matchResult.Errors);

            // Step 6: Import Singles Frames
            ReportProgress("Importing Singles Frames...", 6);
            var singlesResult = await ImportSinglesAsync();
            summary.SinglesImported = singlesResult.ImportedCount;
            summary.SinglesSkipped = singlesResult.SkippedCount;
            summary.Warnings.AddRange(singlesResult.Warnings);
            summary.Errors.AddRange(singlesResult.Errors);

            // Step 7: Import Doubles Frames
            ReportProgress("Importing Doubles Frames...", 7);
            var doublesResult = await ImportDoublesAsync();
            summary.DoublesImported = doublesResult.ImportedCount;
            summary.DoublesSkipped = doublesResult.SkippedCount;
            summary.Warnings.AddRange(doublesResult.Warnings);
            summary.Errors.AddRange(doublesResult.Errors);

            // Update season dates if we imported fixtures
            if (summary.SeasonStartDate.HasValue || summary.SeasonEndDate.HasValue)
            {
                UpdateSeasonDates(summary.SeasonStartDate, summary.SeasonEndDate);
            }

            summary.Success = !summary.Errors.Any();
        }
        catch (Exception ex)
        {
            summary.Errors.Add($"Import failed: {ex.Message}");
            summary.Success = false;
        }

        return summary;
    }

    private void ReportProgress(string step, int stepNumber)
    {
        ProgressChanged?.Invoke(new ImportProgress
        {
            CurrentStep = step,
            CurrentStepNumber = stepNumber
        });
    }

    private Task<ParadoxDivisionImporter.DivisionImportResult> ImportDivisionsAsync()
    {
        return Task.Run(() =>
        {
            var filePath = FindFile("Division.DB");
            if (filePath == null)
            {
                return new ParadoxDivisionImporter.DivisionImportResult
                {
                    Success = true,
                    Warnings = { "Division.DB not found - skipping division import" }
                };
            }

            var parseResult = ParadoxDivisionImporter.ParseDivisionDb(filePath);
            if (!parseResult.Success || !parseResult.Divisions.Any())
                return parseResult;

            return ParadoxDivisionImporter.ImportToSeason(
                parseResult.Divisions, _seasonId, _divisionMap, _divisionNameMap);
        });
    }

    private Task<ParadoxVenueImporter.VenueImportResult> ImportVenuesAsync()
    {
        return Task.Run(() =>
        {
            var filePath = FindFile("Venue.DB");
            if (filePath == null)
            {
                return new ParadoxVenueImporter.VenueImportResult
                {
                    Success = true,
                    Warnings = { "Venue.DB not found - skipping venue import" }
                };
            }

            var parseResult = ParadoxVenueImporter.ParseVenueDb(filePath);
            if (!parseResult.Success || !parseResult.Venues.Any())
                return parseResult;

            return ParadoxVenueImporter.ImportToSeason(
                parseResult.Venues, _seasonId, _venueMap);
        });
    }

    private Task<ParadoxTeamImporter.TeamImportResult> ImportTeamsAsync()
    {
        return Task.Run(() =>
        {
            var filePath = FindFile("Team.DB");
            if (filePath == null)
            {
                return new ParadoxTeamImporter.TeamImportResult
                {
                    Success = true,
                    Warnings = { "Team.DB not found - skipping team import" }
                };
            }

            var parseResult = ParadoxTeamImporter.ParseTeamDb(filePath);
            if (!parseResult.Success || !parseResult.Teams.Any())
                return parseResult;

            return ParadoxTeamImporter.ImportToSeason(
                parseResult.Teams, _seasonId, _venueMap, _divisionMap, _teamMap);
        });
    }

    private Task<ParadoxPlayerImporter.PlayerImportResult> ImportPlayersAsync()
    {
        return Task.Run(() =>
        {
            var filePath = FindFile("Player.DB");
            if (filePath == null)
            {
                return new ParadoxPlayerImporter.PlayerImportResult
                {
                    Success = true,
                    Warnings = { "Player.DB not found - skipping player import" }
                };
            }

            var parseResult = ParadoxPlayerImporter.ParsePlayerDb(filePath);
            if (!parseResult.Success || !parseResult.Players.Any())
                return parseResult;

            return ParadoxPlayerImporter.ImportToSeason(
                parseResult.Players, _seasonId, _teamMap, _playerMap);
        });
    }

    private Task<ParadoxMatchImporter.MatchImportResult> ImportMatchesAsync()
    {
        return Task.Run(() =>
        {
            var filePath = FindFile("Match.DB");
            if (filePath == null)
            {
                return new ParadoxMatchImporter.MatchImportResult
                {
                    Success = true,
                    Warnings = { "Match.DB not found - skipping fixture import" }
                };
            }

            var parseResult = ParadoxMatchImporter.ParseMatchDb(filePath);
            if (!parseResult.Success || !parseResult.Matches.Any())
                return parseResult;

            return ParadoxMatchImporter.ImportToSeason(
                parseResult.Matches, _seasonId, _teamMap, _divisionNameMap, _matchMap);
        });
    }

    private Task<ParadoxSingleImporter.SingleImportResult> ImportSinglesAsync()
    {
        return Task.Run(() =>
        {
            var filePath = FindFile("Single.DB");
            if (filePath == null)
            {
                return new ParadoxSingleImporter.SingleImportResult
                {
                    Success = true,
                    Warnings = { "Single.DB not found - skipping singles frame import" }
                };
            }

            var parseResult = ParadoxSingleImporter.ParseSingleDb(filePath);
            if (!parseResult.Success || !parseResult.Singles.Any())
                return parseResult;

            return ParadoxSingleImporter.ImportToFixtures(
                parseResult.Singles, _matchMap, _playerMap);
        });
    }

    private Task<ParadoxDoubleImporter.DoubleImportResult> ImportDoublesAsync()
    {
        return Task.Run(() =>
        {
            // Try both file names
            var filePath = FindFile("Dbls.DB") ?? FindFile("Double.DB");
            if (filePath == null)
            {
                return new ParadoxDoubleImporter.DoubleImportResult
                {
                    Success = true,
                    Warnings = { "Dbls.DB not found - skipping doubles frame import" }
                };
            }

            var parseResult = ParadoxDoubleImporter.ParseDoublesDb(filePath);
            if (!parseResult.Success || !parseResult.Doubles.Any())
                return parseResult;

            // Default to 8 singles frames (common in pool leagues)
            return ParadoxDoubleImporter.ImportToFixtures(
                parseResult.Doubles, _matchMap, _playerMap, singlesFrameCount: 8);
        });
    }

    private void UpdateSeasonDates(DateTime? startDate, DateTime? endDate)
    {
        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _seasonId);
        if (season != null)
        {
            if (startDate.HasValue && startDate.Value < season.StartDate)
                season.StartDate = startDate.Value;
            if (endDate.HasValue && endDate.Value > season.EndDate)
                season.EndDate = endDate.Value;
            DataStore.Save();
        }
    }

    private string? FindFile(string fileName)
    {
        var path = Path.Combine(_folderPath, fileName);
        if (File.Exists(path))
            return path;

        // Case-insensitive search
        var files = Directory.GetFiles(_folderPath, "*.DB", SearchOption.TopDirectoryOnly);
        return files.FirstOrDefault(f => 
            Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    #region Static Helper Methods

    /// <summary>
    /// Quick scan of a folder to see what Paradox files are available
    /// </summary>
    public static FolderScanResult ScanFolder(string folderPath)
    {
        var result = new FolderScanResult();

        try
        {
            if (!Directory.Exists(folderPath))
            {
                result.Errors.Add($"Folder not found: {folderPath}");
                return result;
            }

            var dbFiles = Directory.GetFiles(folderPath, "*.DB", SearchOption.TopDirectoryOnly);
            
            foreach (var file in dbFiles)
            {
                var fileName = Path.GetFileName(file).ToUpperInvariant();
                var fileSize = new FileInfo(file).Length;

                switch (fileName)
                {
                    case "DIVISION.DB":
                        result.HasDivisions = true;
                        result.DivisionFileSize = fileSize;
                        break;
                    case "VENUE.DB":
                        result.HasVenues = true;
                        result.VenueFileSize = fileSize;
                        break;
                    case "TEAM.DB":
                        result.HasTeams = true;
                        result.TeamFileSize = fileSize;
                        break;
                    case "PLAYER.DB":
                        result.HasPlayers = true;
                        result.PlayerFileSize = fileSize;
                        break;
                    case "MATCH.DB":
                        result.HasMatches = true;
                        result.MatchFileSize = fileSize;
                        break;
                    case "SINGLE.DB":
                        result.HasSingles = true;
                        result.SingleFileSize = fileSize;
                        break;
                    case "DBLS.DB":
                    case "DOUBLE.DB":
                        result.HasDoubles = true;
                        result.DoubleFileSize = fileSize;
                        break;
                }
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error scanning folder: {ex.Message}");
        }

        return result;
    }

    public class FolderScanResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        
        public bool HasDivisions { get; set; }
        public bool HasVenues { get; set; }
        public bool HasTeams { get; set; }
        public bool HasPlayers { get; set; }
        public bool HasMatches { get; set; }
        public bool HasSingles { get; set; }
        public bool HasDoubles { get; set; }
        
        public long DivisionFileSize { get; set; }
        public long VenueFileSize { get; set; }
        public long TeamFileSize { get; set; }
        public long PlayerFileSize { get; set; }
        public long MatchFileSize { get; set; }
        public long SingleFileSize { get; set; }
        public long DoubleFileSize { get; set; }

        public bool HasAnyData => HasDivisions || HasVenues || HasTeams || 
                                  HasPlayers || HasMatches || HasSingles || HasDoubles;

        public string GetSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Paradox files found:");
            if (HasDivisions) sb.AppendLine($"  ? Division.DB ({DivisionFileSize / 1024:N0} KB)");
            if (HasVenues) sb.AppendLine($"  ? Venue.DB ({VenueFileSize / 1024:N0} KB)");
            if (HasTeams) sb.AppendLine($"  ? Team.DB ({TeamFileSize / 1024:N0} KB)");
            if (HasPlayers) sb.AppendLine($"  ? Player.DB ({PlayerFileSize / 1024:N0} KB)");
            if (HasMatches) sb.AppendLine($"  ? Match.DB ({MatchFileSize / 1024:N0} KB)");
            if (HasSingles) sb.AppendLine($"  ? Single.DB ({SingleFileSize / 1024:N0} KB)");
            if (HasDoubles) sb.AppendLine($"  ? Dbls.DB ({DoubleFileSize / 1024:N0} KB)");
            return sb.ToString();
        }
    }

    #endregion
}
