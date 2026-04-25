using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wdpl2.Helpers;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

/// <summary>
/// Orchestrates the complete Paradox database import process.
/// Uses the proven ParadoxDatabaseParser for file parsing, then imports
/// entities in the correct order to maintain foreign key relationships.
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
        public int TotalSteps { get; set; } = 8;
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
    /// Run the complete import process using the proven ParadoxDatabaseParser
    /// </summary>
    public async Task<ImportSummary> ImportAsync(Guid seasonId)
    {
        _seasonId = seasonId;
        var summary = new ImportSummary();

        // Create snapshot so we can roll back on failure
        DataStore.CreatePreImportSnapshot();

        try
        {
            // First, parse all files using the working parser
            ReportProgress("Parsing Paradox files...", 0);
            var parseResult = await Task.Run(() => ParadoxDatabaseParser.ParseFolder(_folderPath));

            if (!parseResult.Success && parseResult.Divisions.Count == 0 && parseResult.Teams.Count == 0 && 
                parseResult.Players.Count == 0 && parseResult.Matches.Count == 0)
            {
                summary.Errors.AddRange(parseResult.Errors);
                summary.Warnings.AddRange(parseResult.Warnings);
                DataStore.ClearPreImportSnapshot();
                return summary;
            }

            summary.Warnings.AddRange(parseResult.Warnings);

            // Step 1: Import Divisions
            ReportProgress("Importing Divisions...", 1);
            await Task.Run(() => ImportDivisions(parseResult.Divisions, summary));

            // Step 2: Import Venues
            ReportProgress("Importing Venues...", 2);
            await Task.Run(() => ImportVenues(parseResult.Venues, summary));

            // Step 3: Import Teams
            ReportProgress("Importing Teams...", 3);
            await Task.Run(() => ImportTeams(parseResult.Teams, summary));

            // Step 4: Import Players
            ReportProgress("Importing Players...", 4);
            await Task.Run(() => ImportPlayers(parseResult.Players, summary));

            // Step 5: Import Matches/Fixtures
            ReportProgress("Importing Fixtures...", 5);
            await Task.Run(() => ImportMatches(parseResult.Matches, summary));

            // Step 6: Import Singles Frames (with rating data from DateRate)
            ReportProgress("Importing Singles Frames...", 6);
            await Task.Run(() => ImportSingles(parseResult.Singles, summary));

            // Step 7: Import Doubles Frames
            ReportProgress("Importing Doubles Frames...", 7);
            await Task.Run(() => ImportDoubles(parseResult.Doubles, summary));

            // Step 8: Apply VBA rating data from DateRate table to imported frames
            if (parseResult.DateRates.Count > 0)
            {
                ReportProgress("Applying VBA rating data...", 8);
                await Task.Run(() => ApplyDateRateData(parseResult, summary));
            }

            // Update season dates if we imported fixtures
            if (summary.SeasonStartDate.HasValue || summary.SeasonEndDate.HasValue)
            {
                UpdateSeasonDates(summary.SeasonStartDate, summary.SeasonEndDate);
            }

            // Save all changes
            DataStore.Save();

            summary.Success = summary.Errors.Count == 0;
            DataStore.ClearPreImportSnapshot();
        }
        catch (Exception ex)
        {
            summary.Errors.Add($"Import failed: {ex.Message}");
            summary.Success = false;

            // Roll back all changes on failure
            DataStore.RestorePreImportSnapshot();
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

    #region Import Methods

    private static string NormalizeDivisionName(string name) => DivisionHelper.NormalizeDivisionNameForMatching(name);

    private static string StripDivisionSuffix(string name) => DivisionHelper.StripDivisionSuffix(name);

    private void ImportDivisions(List<ParadoxDatabaseParser.ParadoxDivision> divisions, ImportSummary summary)
    {
        foreach (var div in divisions)
        {
            // Try exact match first
            var existing = DataStore.Data.Divisions.FirstOrDefault(d =>
                d.SeasonId == _seasonId &&
                !string.IsNullOrWhiteSpace(d.Name) &&
                d.Name.Equals(div.FullDivisionName, StringComparison.OrdinalIgnoreCase));

            // Fuzzy match: normalize and compare (handles "Red" vs "Red Division" etc.)
            if (existing == null)
            {
                var normalizedInput = NormalizeDivisionName(div.FullDivisionName);
                existing = DataStore.Data.Divisions.FirstOrDefault(d =>
                    d.SeasonId == _seasonId &&
                    !string.IsNullOrWhiteSpace(d.Name) &&
                    NormalizeDivisionName(d.Name).Equals(normalizedInput, StringComparison.OrdinalIgnoreCase));
            }

            // Also try abbreviated name matching
            if (existing == null && !string.IsNullOrWhiteSpace(div.Abbreviated))
            {
                var abbrevLower = div.Abbreviated.Trim().ToLower();
                existing = DataStore.Data.Divisions.FirstOrDefault(d =>
                    d.SeasonId == _seasonId &&
                    !string.IsNullOrWhiteSpace(d.Name) &&
                    StripDivisionSuffix(d.Name).ToLower().StartsWith(abbrevLower));
            }

            if (existing != null)
            {
                _divisionMap[div.ItemId] = existing.Id;
                _divisionNameMap[div.FullDivisionName] = existing.Id;
                if (!string.IsNullOrWhiteSpace(div.Abbreviated))
                    _divisionNameMap[div.Abbreviated] = existing.Id;
                summary.DivisionsSkipped++;
            }
            else
            {
                var newDiv = new Division
                {
                    Id = Guid.NewGuid(),
                    SeasonId = _seasonId,
                    Name = div.FullDivisionName
                };
                DataStore.Data.Divisions.Add(newDiv);
                _divisionMap[div.ItemId] = newDiv.Id;
                _divisionNameMap[div.FullDivisionName] = newDiv.Id;
                if (!string.IsNullOrWhiteSpace(div.Abbreviated))
                    _divisionNameMap[div.Abbreviated] = newDiv.Id;
                summary.DivisionsImported++;
            }
        }

        // Also add common division name mappings
        var commonDivNames = new[] { "Premier", "One", "Two", "Three" };
        foreach (var divName in commonDivNames)
        {
            if (!_divisionNameMap.ContainsKey(divName))
            {
                var existing = DataStore.Data.Divisions.FirstOrDefault(d =>
                    d.SeasonId == _seasonId &&
                    d.Name != null &&
                    d.Name.Contains(divName, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    _divisionNameMap[divName] = existing.Id;
                }
            }
        }
    }

    private void ImportVenues(List<ParadoxDatabaseParser.ParadoxVenue> venues, ImportSummary summary)
    {
        foreach (var venue in venues)
        {
            var existing = DataStore.Data.Venues.FirstOrDefault(v =>
                v.SeasonId == _seasonId &&
                !string.IsNullOrWhiteSpace(v.Name) &&
                v.Name.Equals(venue.VenueName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                _venueMap[venue.ItemId] = existing.Id;
                summary.VenuesSkipped++;
            }
            else
            {
                var newVenue = new Venue
                {
                    Id = Guid.NewGuid(),
                    SeasonId = _seasonId,
                    Name = venue.VenueName,
                    Address = venue.Address
                };
                DataStore.Data.Venues.Add(newVenue);
                _venueMap[venue.ItemId] = newVenue.Id;
                summary.VenuesImported++;
            }
        }
    }

    private void ImportTeams(List<ParadoxDatabaseParser.ParadoxTeam> teams, ImportSummary summary)
    {
        foreach (var team in teams)
        {
            var existing = DataStore.Data.Teams.FirstOrDefault(t =>
                t.SeasonId == _seasonId &&
                !string.IsNullOrWhiteSpace(t.Name) &&
                t.Name.Equals(team.TeamName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                _teamMap[team.ItemId] = existing.Id;
                summary.TeamsSkipped++;
            }
            else
            {
                Guid? divisionId = null;
                if (team.DivisionId.HasValue && _divisionMap.TryGetValue(team.DivisionId.Value, out var divId))
                {
                    divisionId = divId;
                }

                Guid? venueId = null;
                if (team.VenueId.HasValue && _venueMap.TryGetValue(team.VenueId.Value, out var vId))
                {
                    venueId = vId;
                }

                var newTeam = new Team
                {
                    Id = Guid.NewGuid(),
                    SeasonId = _seasonId,
                    Name = team.TeamName,
                    DivisionId = divisionId,
                    VenueId = venueId,
                    Captain = team.Contact
                };
                DataStore.Data.Teams.Add(newTeam);
                _teamMap[team.ItemId] = newTeam.Id;
                summary.TeamsImported++;
            }
        }
    }

    private void ImportPlayers(List<ParadoxDatabaseParser.ParadoxPlayer> players, ImportSummary summary)
    {
        foreach (var player in players)
        {
            var existing = DataStore.Data.Players.FirstOrDefault(p =>
                p.SeasonId == _seasonId &&
                p.FirstName?.Equals(player.FirstName, StringComparison.OrdinalIgnoreCase) == true &&
                p.LastName?.Equals(player.LastName, StringComparison.OrdinalIgnoreCase) == true);

            if (existing != null)
            {
                _playerMap[player.PlayerNo] = existing.Id;
                summary.PlayersSkipped++;
            }
            else
            {
                Guid? teamId = null;
                if (player.PlayerTeam.HasValue && _teamMap.TryGetValue(player.PlayerTeam.Value, out var tId))
                {
                    teamId = tId;
                }

                var newPlayer = new Player
                {
                    Id = Guid.NewGuid(),
                    SeasonId = _seasonId,
                    FirstName = player.FirstName,
                    LastName = player.LastName,
                    TeamId = teamId
                };
                DataStore.Data.Players.Add(newPlayer);
                _playerMap[player.PlayerNo] = newPlayer.Id;
                summary.PlayersImported++;
            }
        }
    }

    private void ImportMatches(List<ParadoxDatabaseParser.ParadoxMatch> matches, ImportSummary summary)
    {
        DateTime? minDate = null;
        DateTime? maxDate = null;

        foreach (var match in matches)
        {
            _teamMap.TryGetValue(match.HomeTeam, out var homeTeamId);
            _teamMap.TryGetValue(match.AwayTeam, out var awayTeamId);

            if (homeTeamId == Guid.Empty || awayTeamId == Guid.Empty)
            {
                summary.FixturesSkipped++;
                continue;
            }

            // Resolve division: try match's DivisionName first
            Guid? divisionId = null;
            if (!string.IsNullOrWhiteSpace(match.DivisionName) &&
                _divisionNameMap.TryGetValue(match.DivisionName, out var divId))
            {
                divisionId = divId;
            }

            // Fallback: use home team's division if match doesn't specify one
            if (!divisionId.HasValue)
            {
                var homeTeam = DataStore.Data.Teams.FirstOrDefault(t => t.Id == homeTeamId);
                if (homeTeam?.DivisionId.HasValue == true)
                {
                    divisionId = homeTeam.DivisionId;
                }
            }

            // Find the home team's venue
            Guid? venueId = null;
            var homeTeamForVenue = DataStore.Data.Teams.FirstOrDefault(t => t.Id == homeTeamId);
            if (homeTeamForVenue?.VenueId.HasValue == true)
            {
                venueId = homeTeamForVenue.VenueId;
            }

            // Only dedup if the match has a real date (not MinValue)
            if (match.MatchDate > DateTime.MinValue)
            {
                var existingFixture = DataStore.Data.Fixtures.FirstOrDefault(f =>
                    f.SeasonId == _seasonId &&
                    f.Date.Date == match.MatchDate.Date &&
                    f.HomeTeamId == homeTeamId &&
                    f.AwayTeamId == awayTeamId);

                if (existingFixture != null)
                {
                    _matchMap[match.MatchNo] = existingFixture.Id;
                    summary.FixturesSkipped++;
                    continue;
                }
            }

            var fixtureId = Guid.NewGuid();
            var fixture = new Fixture
            {
                Id = fixtureId,
                SeasonId = _seasonId,
                DivisionId = divisionId,
                Date = match.MatchDate,
                HomeTeamId = homeTeamId,
                AwayTeamId = awayTeamId,
                VenueId = venueId
            };

            DataStore.Data.Fixtures.Add(fixture);
            _matchMap[match.MatchNo] = fixtureId;
            summary.FixturesImported++;

            if (match.MatchDate > DateTime.MinValue)
            {
                if (!minDate.HasValue || match.MatchDate < minDate) minDate = match.MatchDate;
                if (!maxDate.HasValue || match.MatchDate > maxDate) maxDate = match.MatchDate;
            }
        }

        summary.SeasonStartDate = minDate;
        summary.SeasonEndDate = maxDate;
    }

    private void ImportSingles(List<ParadoxDatabaseParser.ParadoxSingle> singles, ImportSummary summary)
    {
        foreach (var single in singles)
        {
            if (!_matchMap.TryGetValue(single.MatchNo, out var fixtureId))
            {
                summary.SinglesSkipped++;
                continue;
            }

            var fixture = DataStore.Data.Fixtures.FirstOrDefault(f => f.Id == fixtureId);
            if (fixture == null)
            {
                summary.SinglesSkipped++;
                continue;
            }

            // Check if frame already exists
            if (fixture.Frames.Any(f => f.Number == single.SingleNo))
            {
                summary.SinglesSkipped++;
                continue;
            }

            // Get player GUIDs
            Guid? homePlayerId = null;
            Guid? awayPlayerId = null;

            if (single.HomePlayerNo > 0 && _playerMap.TryGetValue(single.HomePlayerNo, out var hpId))
                homePlayerId = hpId;
            if (single.AwayPlayerNo > 0 && _playerMap.TryGetValue(single.AwayPlayerNo, out var apId))
                awayPlayerId = apId;

            // Determine winner
            var winner = (single.Winner ?? "").ToLowerInvariant() switch
            {
                "home" => FrameWinner.Home,
                "away" => FrameWinner.Away,
                _ => FrameWinner.None
            };

            var frame = new FrameResult
            {
                Number = single.SingleNo,
                HomePlayerId = homePlayerId,
                AwayPlayerId = awayPlayerId,
                Winner = winner,
                EightBall = single.EightBall,
                WeekNo = GetWeekNumber(fixture.Date)
            };

            fixture.Frames.Add(frame);
            summary.SinglesImported++;
        }
    }

    private void ImportDoubles(List<ParadoxDatabaseParser.ParadoxDouble> doubles, ImportSummary summary)
    {
        foreach (var dbl in doubles)
        {
            if (!_matchMap.TryGetValue(dbl.MatchNo, out var fixtureId))
            {
                summary.DoublesSkipped++;
                continue;
            }

            var fixture = DataStore.Data.Fixtures.FirstOrDefault(f => f.Id == fixtureId);
            if (fixture == null)
            {
                summary.DoublesSkipped++;
                continue;
            }

            // Offset frame number to come after singles (typically 8 singles frames)
            var singlesCount = fixture.Frames.Count(f => !f.IsDoubles);
            var frameNumber = singlesCount + dbl.DoubleNo;

            // Check if frame already exists
            if (fixture.Frames.Any(f => f.Number == frameNumber))
            {
                summary.DoublesSkipped++;
                continue;
            }

            // Map players (use first player from each pair)
            Guid? homePlayerId = null;
            Guid? awayPlayerId = null;

            if (dbl.HomePlayer1No > 0 && _playerMap.TryGetValue(dbl.HomePlayer1No, out var hp1Id))
                homePlayerId = hp1Id;
            if (dbl.AwayPlayer1No > 0 && _playerMap.TryGetValue(dbl.AwayPlayer1No, out var ap1Id))
                awayPlayerId = ap1Id;

            // Determine winner
            var winner = (dbl.Winner ?? "").ToLowerInvariant() switch
            {
                "home" => FrameWinner.Home,
                "away" => FrameWinner.Away,
                _ => FrameWinner.None
            };

            var frame = new FrameResult
            {
                Number = frameNumber,
                HomePlayerId = homePlayerId,
                AwayPlayerId = awayPlayerId,
                Winner = winner,
                EightBall = dbl.EightBall1 || dbl.EightBall2,
                IsDoubles = true,
                WeekNo = GetWeekNumber(fixture.Date)
            };

            fixture.Frames.Add(frame);
            summary.DoublesImported++;
        }
    }

    #endregion

    /// <summary>
    /// Apply VBA DateRate rating data to imported frames.
    /// The DateRate table stores per-player per-frame rating contributions that
    /// were calculated by the Delphi app during its "Update" process.
    /// By mapping these back onto the imported FrameResult objects, the
    /// RatingCalculator can reproduce the original VBA ratings exactly.
    /// </summary>
    private void ApplyDateRateData(
        ParadoxDatabaseParser.ParadoxParseResult parseResult,
        ImportSummary summary)
    {
        if (parseResult.DateRates.Count == 0) return;

        // Build a lookup: (PlayerNo, OpponentNo, RatingDate) -> Rating
        // DateRate records come in pairs (home + away for each frame), ordered by DateRateKey.
        // We group by PlayerNo and date to match against frames.
        var ratingsByPlayerDate = parseResult.DateRates
            .GroupBy(dr => (dr.PlayerNo, dr.RatingDate.Date))
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(dr => dr.DateRateKey).ToList());

        int ratingsApplied = 0;

        // Build reverse player map: GUID -> Paradox PlayerNo
        var reversePlayerMap = _playerMap.ToDictionary(kv => kv.Value, kv => kv.Key);

        foreach (var fixture in DataStore.Data.Fixtures.Where(f => f.SeasonId == _seasonId))
        {
            var matchDate = fixture.Date.Date;

            foreach (var frame in fixture.Frames)
            {
                if (frame.HomePlayerId == null || frame.AwayPlayerId == null)
                    continue;

                // Get Paradox player numbers
                if (!reversePlayerMap.TryGetValue(frame.HomePlayerId.Value, out var homePlayerNo))
                    continue;
                if (!reversePlayerMap.TryGetValue(frame.AwayPlayerId.Value, out var awayPlayerNo))
                    continue;

                // Look up home player's rating for this date
                if (ratingsByPlayerDate.TryGetValue((homePlayerNo, matchDate), out var homeRatings))
                {
                    // Find the matching entry (against the away player)
                    var homeEntry = homeRatings.FirstOrDefault(dr => dr.AgainstPlayerNo == awayPlayerNo);
                    if (homeEntry != null)
                    {
                        frame.HomePlayerRating = homeEntry.Rating;
                        homeRatings.Remove(homeEntry); // consume so next frame finds the next entry
                        ratingsApplied++;
                    }
                }

                // Look up away player's rating for this date
                if (ratingsByPlayerDate.TryGetValue((awayPlayerNo, matchDate), out var awayRatings))
                {
                    var awayEntry = awayRatings.FirstOrDefault(dr => dr.AgainstPlayerNo == homePlayerNo);
                    if (awayEntry != null)
                    {
                        frame.AwayPlayerRating = awayEntry.Rating;
                        awayRatings.Remove(awayEntry);
                        ratingsApplied++;
                    }
                }
            }
        }

        summary.Warnings.Add($"Applied {ratingsApplied} VBA rating values to imported frames");
    }

    /// <summary>
    /// Calculate week number from match date relative to the season start.
    /// </summary>
    private int GetWeekNumber(DateTime matchDate)
    {
        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _seasonId);
        if (season == null) return 1;

        var daysSinceStart = (matchDate.Date - season.StartDate.Date).Days;
        if (daysSinceStart < 0) return 1;
        return (daysSinceStart / 7) + 1;
    }

    private void UpdateSeasonDates(DateTime? startDate, DateTime? endDate)
    {
        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _seasonId);
        if (season == null) return;

        // For seasons that still have default dates (Today / Today+3months),
        // set the dates directly from the imported data
        var isDefaultDates = season.StartDate.Date == DateTime.Today ||
                             (season.StartDate.Date >= DateTime.Today.AddDays(-7) && 
                              season.EndDate.Date <= DateTime.Today.AddMonths(4));

        if (isDefaultDates && startDate.HasValue && endDate.HasValue)
        {
            season.StartDate = startDate.Value;
            season.EndDate = endDate.Value;
        }
        else
        {
            // Expand existing range if needed
            if (startDate.HasValue && startDate.Value < season.StartDate)
                season.StartDate = startDate.Value;
            if (endDate.HasValue && endDate.Value > season.EndDate)
                season.EndDate = endDate.Value;
        }
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
                    case "DATERATE.DB":
                        result.HasDateRates = true;
                        result.DateRateFileSize = fileSize;
                        break;
                }
            }

            // Also check for CSV files (preferred import method)
            var csvFiles = Directory.GetFiles(folderPath, "*.csv", SearchOption.TopDirectoryOnly);
            result.HasCsvFiles = csvFiles.Any();

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
        public bool HasDateRates { get; set; }
        public bool HasCsvFiles { get; set; }

        public long DivisionFileSize { get; set; }
        public long VenueFileSize { get; set; }
        public long TeamFileSize { get; set; }
        public long PlayerFileSize { get; set; }
        public long MatchFileSize { get; set; }
        public long SingleFileSize { get; set; }
        public long DoubleFileSize { get; set; }
        public long DateRateFileSize { get; set; }

        public bool HasAnyData => HasDivisions || HasVenues || HasTeams || 
                                  HasPlayers || HasMatches || HasSingles || HasDoubles ||
                                  HasDateRates || HasCsvFiles;

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
            if (HasDateRates) sb.AppendLine($"  ? Daterate.DB ({DateRateFileSize / 1024:N0} KB) - VBA ratings");
            if (HasCsvFiles) sb.AppendLine($"  ? CSV files found (preferred)");
            return sb.ToString();
        }
    }

    #endregion
}
