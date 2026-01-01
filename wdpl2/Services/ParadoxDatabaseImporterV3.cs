using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Paradox database importer V3 - with raw hex scanning for older Paradox versions
/// </summary>
public class ParadoxDatabaseImporterV3
{
    private readonly string _folderPath;
    private readonly Dictionary<int, Guid> _divisionMap = new();
    private readonly Dictionary<string, Guid> _divisionNameMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Guid> _venueMap = new();
    private readonly Dictionary<int, Guid> _teamMap = new();
    private readonly Dictionary<int, Guid> _playerMap = new();
    private readonly Dictionary<int, Guid> _fixtureMap = new();
    
    // Track player names to avoid duplicates
    private readonly HashSet<string> _importedPlayerNames = new(StringComparer.OrdinalIgnoreCase);
    
    // Track imported fixtures to avoid duplicates (key: "HomeTeamId|AwayTeamId|Date")
    private readonly HashSet<string> _importedFixtureKeys = new(StringComparer.OrdinalIgnoreCase);
    
    private Guid _seasonId;

    public ParadoxDatabaseImporterV3(string folderPath)
    {
        _folderPath = folderPath;
    }

    public async Task<(LeagueData data, ImportSummary summary)> ImportAllAsync(Guid? existingSeasonId = null)
    {
        var data = new LeagueData();
        var summary = new ImportSummary();

        try
        {
            await Task.Run(() =>
            {
                if (existingSeasonId.HasValue)
                {
                    _seasonId = existingSeasonId.Value;
                    summary.Errors.Add($"✓ Using existing season: {_seasonId}");
                }
                else
                {
                    _seasonId = Guid.NewGuid();
                    var season = new Season
                    {
                        Id = _seasonId,
                        Name = $"[IMPORTED] Paradox Data {DateTime.Now:yyyy-MM-dd}",
                        StartDate = DateTime.Today.AddMonths(-6),
                        EndDate = DateTime.Today,
                        IsActive = false
                    };
                    data.Seasons.Add(season);
                    summary.SeasonsImported = 1;
                    summary.Errors.Add($"✓ Created season: {season.Name}");
                }

                ImportDivisions(data, summary);
                ImportVenues(data, summary);
                ImportTeams(data, summary);
                ImportPlayers(data, summary);
                ImportMatches(data, summary);
                ImportSingles(data, summary);
                ImportDoubles(data, summary);

                if (data.Fixtures.Any())
                {
                    var minDate = data.Fixtures.Min(f => f.Date);
                    var maxDate = data.Fixtures.Max(f => f.Date);
                    var season = data.Seasons.FirstOrDefault(s => s.Id == _seasonId);
                    if (season != null)
                    {
                        season.StartDate = minDate;
                        season.EndDate = maxDate;
                        summary.Errors.Add($"✓ Updated season dates: {minDate:dd/MM/yyyy} - {maxDate:dd/MM/yyyy}");
                    }
                }
            });

            summary.Success = true;
            summary.Message = "Import completed successfully!";
        }
        catch (Exception ex)
        {
            summary.Success = false;
            summary.Message = $"Import failed: {ex.Message}";
            summary.Errors.Add($"❌ ERROR: {ex}");
        }

        return (data, summary);
    }

    #region Import Methods

    private void ImportDivisions(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Division.DB");
        if (filePath == null) { summary.Errors.Add("⚠️ Division.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Division.DB] recordSize={recordSize}, numRecords={numRecords}");

            int dataStart = 2048;
            var foundDivisions = ScanForTextRecords(bytes, dataStart, recordSize, numRecords);
            
            summary.Errors.Add($"[Division.DB] Found {foundDivisions.Count} text records");

            int id = 1;
            foreach (var divName in foundDivisions)
            {
                if (string.IsNullOrWhiteSpace(divName)) continue;
                if (divName.Equals("test", StringComparison.OrdinalIgnoreCase)) continue;
                if (divName.Length < 2 || divName.Length > 50) continue;
                if (divName.Any(c => c < 32 || c > 126)) continue;

                var division = new Division 
                { 
                    Id = Guid.NewGuid(), 
                    SeasonId = _seasonId, 
                    Name = divName.Trim(), 
                    Notes = "[IMPORTED]" 
                };
                data.Divisions.Add(division);
                _divisionMap[id] = division.Id;
                _divisionNameMap[divName.Trim()] = division.Id;
                summary.DivisionsImported++;
                id++;
                
                summary.Errors.Add($"  Division: {divName}");
            }
            
            summary.Errors.Add($"✓ Imported {summary.DivisionsImported} divisions");
        }
        catch (Exception ex) { summary.Errors.Add($"❌ Division error: {ex.Message}"); }
    }

    private void ImportVenues(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Venue.DB");
        if (filePath == null) { summary.Errors.Add("⚠️ Venue.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Venue.DB] recordSize={recordSize}, numRecords={numRecords}");

            int dataStart = 2048;
            var foundVenues = ScanForTextRecords(bytes, dataStart, recordSize, numRecords);
            
            int id = 1;
            foreach (var venueName in foundVenues)
            {
                if (string.IsNullOrWhiteSpace(venueName)) continue;
                if (venueName.Length < 3 || venueName.Length > 100) continue;
                if (venueName.Any(c => c < 32 || c > 126)) continue;

                var venue = new Venue 
                { 
                    Id = Guid.NewGuid(), 
                    SeasonId = _seasonId, 
                    Name = venueName.Trim(), 
                    Notes = "[IMPORTED]" 
                };
                data.Venues.Add(venue);
                _venueMap[id] = venue.Id;
                summary.VenuesImported++;
                id++;
                
                if (summary.VenuesImported <= 3)
                    summary.Errors.Add($"  Venue: {venueName}");
            }
            
            summary.Errors.Add($"✓ Imported {summary.VenuesImported} venues");
        }
        catch (Exception ex) { summary.Errors.Add($"❌ Venue error: {ex.Message}"); }
    }

    private void ImportTeams(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Team.DB");
        if (filePath == null) { summary.Errors.Add("⚠️ Team.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Team.DB] recordSize={recordSize}, numRecords={numRecords}");

            int dataStart = 2048;
            
            // Debug: show first few record structures to understand division link
            for (int debugRec = 0; debugRec < Math.Min(3, numRecords); debugRec++)
            {
                int blockSize = 2048;
                int usableBlock = blockSize - 6;
                int recsPerBlock = usableBlock / recordSize;
                if (recsPerBlock <= 0) recsPerBlock = 1;
                
                int blockNum = debugRec / recsPerBlock;
                int recInBlock = debugRec % recsPerBlock;
                int debugOffset = dataStart + (blockNum * blockSize) + 6 + (recInBlock * recordSize);
                
                if (debugOffset + Math.Min((int)recordSize, 50) <= bytes.Length)
                {
                    var rawBytes = new byte[Math.Min((int)recordSize, 50)];
                    Array.Copy(bytes, debugOffset, rawBytes, 0, rawBytes.Length);
                    summary.Errors.Add($"  [Team #{debugRec+1}] Raw: {BitConverter.ToString(rawBytes).Replace("-", " ")}");
                }
            }
            
            // Track team names to detect duplicates
            var teamNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            for (int rec = 0; rec < numRecords; rec++)
            {
                int blockSize = 2048;
                int usableBlock = blockSize - 6;
                int recsPerBlock = usableBlock / recordSize;
                if (recsPerBlock <= 0) recsPerBlock = 1;
                
                int blockNum = rec / recsPerBlock;
                int recInBlock = rec % recsPerBlock;
                int offset = dataStart + (blockNum * blockSize) + 6 + (recInBlock * recordSize);
                
                if (offset + recordSize > bytes.Length) break;

                // Team.DB structure based on raw data analysis:
                // Offset 0-3: TeamId (4-byte Paradox int)
                // Offset 4-33: TeamName (Alpha, ~30 chars)
                // Offset 34-37: VenueId (4-byte Paradox int) 
                // Offset 38-41: DivisionId (4-byte Paradox int)
                
                // Parse team ID
                int teamId = 0;
                if (offset + 4 <= bytes.Length && (bytes[offset] & 0x80) == 0x80)
                {
                    teamId = ((bytes[offset] & 0x7F) << 24) | (bytes[offset + 1] << 16) | 
                              (bytes[offset + 2] << 8) | bytes[offset + 3];
                }
                
                if (teamId <= 0 || teamId > 1000)
                {
                    teamId = rec + 1;
                }

                // Extract team name - skip first 4 bytes for ID
                var teamName = ExtractTextAfterOffset(bytes, offset + 4, 30);
                
                // Parse VenueId at offset 34 and DivisionId at offset 38
                int venueId = 0;
                int divisionId = 0;
                
                // VenueId at offset 34
                int venueOffset = offset + 34;
                if (venueOffset + 4 <= bytes.Length && (bytes[venueOffset] & 0x80) == 0x80)
                {
                    venueId = ((bytes[venueOffset] & 0x7F) << 24) | (bytes[venueOffset + 1] << 16) | 
                              (bytes[venueOffset + 2] << 8) | bytes[venueOffset + 3];
                }
                
                // DivisionId at offset 38 (right after VenueId)
                int divOffset = offset + 38;
                if (divOffset + 4 <= bytes.Length && (bytes[divOffset] & 0x80) == 0x80)
                {
                    divisionId = ((bytes[divOffset] & 0x7F) << 24) | (bytes[divOffset + 1] << 16) | 
                                  (bytes[divOffset + 2] << 8) | bytes[divOffset + 3];
                }
                
                if (string.IsNullOrWhiteSpace(teamName)) 
                {
                    summary.Errors.Add($"  [Skip] Team record #{rec+1} (ID={teamId}): empty name - deleted/placeholder record");
                    continue;
                }
                if (teamName.Length < 2 || teamName.Length > 100) continue;
                if (teamName.Any(c => c < 32 || c > 126)) continue;

                // Check for duplicate team name
                var normalizedName = teamName.Trim().ToUpperInvariant();
                if (teamNameToId.TryGetValue(normalizedName, out var existingId))
                {
                    // Duplicate team name - map this ID to the existing team
                    if (_teamMap.TryGetValue(existingId, out var existingGuid))
                    {
                        _teamMap[teamId] = existingGuid;
                        summary.Errors.Add($"  [Duplicate] Team #{teamId} '{teamName}' -> mapped to existing #{existingId}");
                    }
                    continue;
                }

                // Find the division GUID
                Guid? divisionGuid = null;
                if (divisionId > 0 && _divisionMap.TryGetValue(divisionId, out var dg))
                {
                    divisionGuid = dg;
                }
                
                // Find venue GUID
                Guid? venueGuid = null;
                if (venueId > 0 && _venueMap.TryGetValue(venueId, out var vg))
                {
                    venueGuid = vg;
                }

                var team = new Team
                {
                    Id = Guid.NewGuid(),
                    SeasonId = _seasonId,
                    Name = teamName.Trim().ToUpperInvariant(),
                    DivisionId = divisionGuid,
                    VenueId = venueGuid,
                    Notes = "[IMPORTED]"
                };

                data.Teams.Add(team);
                _teamMap[teamId] = team.Id;
                teamNameToId[normalizedName] = teamId;
                summary.TeamsImported++;
                
                if (summary.TeamsImported <= 8)
                    summary.Errors.Add($"  Team #{teamId}: {teamName} (Venue={venueId}, Div={divisionId})");
            }
            
            // Log the team map with ALL IDs
            var teamIds = _teamMap.Keys.OrderBy(k => k).ToList();
            summary.Errors.Add($"  Team IDs in map: {string.Join(", ", teamIds)}");
            summary.Errors.Add($"  Team ID range: {teamIds.FirstOrDefault()} - {teamIds.LastOrDefault()}");
            
            // Log division distribution
            var divCounts = data.Teams
                .GroupBy(t => t.DivisionId)
                .Select(g => $"{(g.Key.HasValue ? data.Divisions.FirstOrDefault(d => d.Id == g.Key)?.Name ?? "?" : "None")}={g.Count()}");
            summary.Errors.Add($"  Division distribution: {string.Join(", ", divCounts)}");
            
            summary.Errors.Add($"✓ Imported {summary.TeamsImported} teams");
        }
        catch (Exception ex) { summary.Errors.Add($"❌ Team error: {ex.Message}"); }
    }

    private void ImportPlayers(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Player.DB");
        if (filePath == null) { summary.Errors.Add("⚠️ Player.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Player.DB] recordSize={recordSize}, numRecords={numRecords}");

            // Debug: show first record structure
            int dataStart = 2048;
            if (numRecords > 0)
            {
                int debugOffset = dataStart + 6;
                if (debugOffset + Math.Min((int)recordSize, 40) <= bytes.Length)
                {
                    var rawBytes = new byte[Math.Min((int)recordSize, 40)];
                    Array.Copy(bytes, debugOffset, rawBytes, 0, rawBytes.Length);
                    summary.Errors.Add($"  [Player #1] Raw: {BitConverter.ToString(rawBytes).Replace("-", " ")}");
                }
            }
            
            // Parse players with their record IDs
            int skippedDuplicates = 0;
            int skippedVoid = 0;
            
            for (int rec = 0; rec < numRecords; rec++)
            {
                int blockSize = 2048;
                int usableBlock = blockSize - 6;
                int recsPerBlock = usableBlock / recordSize;
                if (recsPerBlock <= 0) recsPerBlock = 1;
                
                int blockNum = rec / recsPerBlock;
                int recInBlock = rec % recsPerBlock;
                int offset = dataStart + (blockNum * blockSize) + 6 + (recInBlock * recordSize);
                
                if (offset + recordSize > bytes.Length) break;

                // Player.DB structure - try parsing PlayerNo as a double first
                int recordId = ParseParadoxDouble(bytes, offset);
                if (recordId <= 0 || recordId > 10000)
                {
                    // Fall back to sequential (1-based)
                    recordId = rec + 1;
                }

                // Extract player name - skip first 8 bytes (PlayerNo double)
                var playerName = ExtractTextAfterOffset(bytes, offset + 8, recordSize - 8);
                
                // Debug first few
                if (rec < 3)
                {
                    summary.Errors.Add($"  Raw Player #{rec+1}: ID={recordId}, Name='{playerName}'");
                }
                
                if (string.IsNullOrWhiteSpace(playerName)) continue;
                
                // Skip "Void Frame" but still map it to null (so we know it exists)
                if (playerName.Equals("Void Frame", StringComparison.OrdinalIgnoreCase))
                {
                    skippedVoid++;
                    // Don't add to player map - these are placeholder entries
                    continue;
                }
                
                if (playerName.Length < 2 || playerName.Length > 100) continue;
                if (playerName.Any(c => c < 32 || c > 126)) continue;

                // Check for duplicate player name
                var normalizedName = playerName.Trim().ToUpperInvariant();
                if (_importedPlayerNames.Contains(normalizedName))
                {
                    // Map duplicate record ID to existing player
                    var existingPlayer = data.Players.FirstOrDefault(p => 
                        $"{p.FirstName} {p.LastName}".Trim().Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
                    if (existingPlayer != null)
                    {
                        _playerMap[recordId] = existingPlayer.Id;
                    }
                    skippedDuplicates++;
                    continue;
                }

                var nameParts = playerName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var firstName = nameParts.FirstOrDefault() ?? "";
                var lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";

                var player = new Player
                {
                    Id = Guid.NewGuid(),
                    SeasonId = _seasonId,
                    FirstName = firstName.ToUpperInvariant(),
                    LastName = lastName.ToUpperInvariant(),
                    Notes = "[IMPORTED]"
                };

                data.Players.Add(player);
                _playerMap[recordId] = player.Id;
                _importedPlayerNames.Add(normalizedName);
                summary.PlayersImported++;
                
                if (summary.PlayersImported <= 5)
                    summary.Errors.Add($"  Player #{recordId}: {playerName}");
            }
            
            // Log the player map for debugging
            summary.Errors.Add($"  Player map has {_playerMap.Count} entries");
            var allIds = _playerMap.Keys.OrderBy(k => k).ToList();
            summary.Errors.Add($"  Player ID range: {allIds.FirstOrDefault()} - {allIds.LastOrDefault()}");
            
            if (skippedVoid > 0)
                summary.Errors.Add($"  (Skipped {skippedVoid} 'Void Frame' entries)");
            if (skippedDuplicates > 0)
                summary.Errors.Add($"  (Skipped {skippedDuplicates} duplicate names)");
            
            summary.Errors.Add($"✓ Imported {summary.PlayersImported} players");
        }
        catch (Exception ex) { summary.Errors.Add($"❌ Player error: {ex.Message}"); }
    }

    private void ImportMatches(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Match.DB");
        if (filePath == null) { summary.Errors.Add("⚠️ Match.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Match.DB] recordSize={recordSize}, numRecords={numRecords}");
            summary.Errors.Add($"  Team map has {_teamMap.Count} entries (IDs: {string.Join(", ", _teamMap.Keys.OrderBy(k => k).Take(10))})");

            int dataStart = 2048;
            int skippedNoTeam = 0;
            int skippedDuplicate = 0;
            
            // Build a map of team ID -> Division ID for setting fixture division
            var teamToDivision = new Dictionary<Guid, Guid?>();
            foreach (var team in data.Teams)
            {
                teamToDivision[team.Id] = team.DivisionId;
            }
            
            // Debug: show first record structure with full bytes
            if (numRecords > 0)
            {
                int debugOffset = dataStart + 6;
                if (debugOffset + recordSize <= bytes.Length)
                {
                    var rawBytes = new byte[recordSize];
                    Array.Copy(bytes, debugOffset, rawBytes, 0, recordSize);
                    summary.Errors.Add($"  [Match #1] Raw ({recordSize} bytes): {BitConverter.ToString(rawBytes).Replace("-", " ")}");
                }
            }
            
            for (int rec = 0; rec < numRecords; rec++)
            {
                int blockSize = 2048;
                int usableBlock = blockSize - 6;
                int recsPerBlock = usableBlock / recordSize;
                if (recsPerBlock <= 0) recsPerBlock = 1;
                
                int blockNum = rec / recsPerBlock;
                int recInBlock = rec % recsPerBlock;
                int offset = dataStart + (blockNum * blockSize) + 6 + (recInBlock * recordSize);
                
                if (offset + recordSize > bytes.Length) break;

                // Parse MatchNo - try as double first (8 bytes)
                int matchNo = ParseParadoxDouble(bytes, offset);
                if (matchNo <= 0 || matchNo > 10000)
                {
                    matchNo = rec + 1;
                }

                // Parse HomeTeam and AwayTeam as 4-byte Paradox integers at offsets 8 and 12
                int homeTeamId = 0;
                int awayTeamId = 0;
                
                if (offset + 12 <= bytes.Length && (bytes[offset + 8] & 0x80) == 0x80)
                {
                    homeTeamId = ((bytes[offset + 8] & 0x7F) << 24) | (bytes[offset + 9] << 16) | 
                                  (bytes[offset + 10] << 8) | bytes[offset + 11];
                }
                
                if (offset + 16 <= bytes.Length && (bytes[offset + 12] & 0x80) == 0x80)
                {
                    awayTeamId = ((bytes[offset + 12] & 0x7F) << 24) | (bytes[offset + 13] << 16) | 
                                  (bytes[offset + 14] << 8) | bytes[offset + 15];
                }
                
                // Parse Date as 4-byte Paradox date at offset 16
                var matchDate = DateTime.Today;
                if (offset + 20 <= bytes.Length && (bytes[offset + 16] & 0x80) == 0x80)
                {
                    int dateVal = ((bytes[offset + 16] & 0x7F) << 24) | (bytes[offset + 17] << 16) | 
                                   (bytes[offset + 18] << 8) | bytes[offset + 19];
                    if (dateVal > 700000 && dateVal < 800000)
                    {
                        try { matchDate = new DateTime(1, 1, 1).AddDays(dateVal - 1); }
                        catch { }
                    }
                }

                // Debug first few matches
                if (rec < 3)
                {
                    summary.Errors.Add($"  Match #{rec+1}: MatchNo={matchNo}, Home={homeTeamId}, Away={awayTeamId}, Date={matchDate:yyyy-MM-dd}");
                }

                // Create fixture - need valid team IDs
                Guid homeGuid = Guid.Empty;
                Guid awayGuid = Guid.Empty;
                
                if (homeTeamId > 0 && _teamMap.TryGetValue(homeTeamId, out var hg))
                    homeGuid = hg;
                if (awayTeamId > 0 && _teamMap.TryGetValue(awayTeamId, out var ag))
                    awayGuid = ag;
                
                // Only create fixture if we have both teams
                if (homeGuid != Guid.Empty && awayGuid != Guid.Empty)
                {
                    // Create a unique key for this fixture to detect duplicates
                    var fixtureKey = $"{homeGuid}|{awayGuid}|{matchDate:yyyy-MM-dd}";
                    
                    if (_importedFixtureKeys.Contains(fixtureKey))
                    {
                        // This is a duplicate fixture - just map the matchNo to the existing fixture
                        skippedDuplicate++;
                        if (skippedDuplicate <= 3)
                        {
                            summary.Errors.Add($"  [Skip Duplicate] Match #{matchNo}: {homeTeamId} vs {awayTeamId} on {matchDate:yyyy-MM-dd}");
                        }
                        continue;
                    }
                    
                    // Set division from home team's division
                    Guid? divisionId = null;
                    if (teamToDivision.TryGetValue(homeGuid, out var div))
                        divisionId = div;
                    
                    var fixture = new Fixture
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = _seasonId,
                        HomeTeamId = homeGuid,
                        AwayTeamId = awayGuid,
                        DivisionId = divisionId,
                        Date = matchDate
                    };

                    data.Fixtures.Add(fixture);
                    _fixtureMap[matchNo] = fixture.Id;
                    _importedFixtureKeys.Add(fixtureKey);
                    summary.FixturesImported++;
                }
                else
                {
                    skippedNoTeam++;
                    if (skippedNoTeam <= 3)
                    {
                        summary.Errors.Add($"  [Skip] Match #{matchNo}: HomeTeam={homeTeamId} (found={homeGuid != Guid.Empty}), AwayTeam={awayTeamId} (found={awayGuid != Guid.Empty})");
                    }
                }
            }
            
            // Log the fixture map for debugging
            summary.Errors.Add($"  Fixture map has {_fixtureMap.Count} entries (from {numRecords} records)");
            if (skippedNoTeam > 0)
                summary.Errors.Add($"  ({skippedNoTeam} matches skipped - team not found)");
            if (skippedDuplicate > 0)
                summary.Errors.Add($"  ({skippedDuplicate} duplicate fixtures skipped)");
            var matchIds = _fixtureMap.Keys.OrderBy(k => k).ToList();
            summary.Errors.Add($"  Match ID range: {matchIds.FirstOrDefault()} - {matchIds.LastOrDefault()}");
            
            summary.Errors.Add($"✓ Imported {summary.FixturesImported} fixtures");
        }
        catch (Exception ex) { summary.Errors.Add($"❌ Match error: {ex.Message}"); }
    }

    private void ImportSingles(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Single.DB");
        if (filePath == null) { summary.Errors.Add("⚠️ Single.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Single.DB] recordSize={recordSize}, numRecords={numRecords}");

            int dataStart = 2048;
            int framesImported = 0;
            int framesSkipped = 0;
            int framesNoMatch = 0;
            int framesNoPlayers = 0;
            int framesPartialPlayers = 0;
            int eightBallCount = 0;
            
            // Track unmapped player IDs for debugging
            var unmappedPlayerIds = new HashSet<int>();
            
            // Debug: show first few raw records with full length
            for (int debugRec = 0; debugRec < Math.Min(3, numRecords); debugRec++)
            {
                int blockSize = 2048;
                int usableBlock = blockSize - 6;
                int recsPerBlock = usableBlock / recordSize;
                if (recsPerBlock <= 0) recsPerBlock = 1;
                
                int blockNum = debugRec / recsPerBlock;
                int recInBlock = debugRec % recsPerBlock;
                int debugOffset = dataStart + (blockNum * blockSize) + 6 + (recInBlock * recordSize);
                
                if (debugOffset + recordSize <= bytes.Length)
                {
                    var rawBytes = new byte[recordSize];
                    Array.Copy(bytes, debugOffset, rawBytes, 0, recordSize);
                    summary.Errors.Add($"  [Single #{debugRec+1}] Raw ({recordSize} bytes): {BitConverter.ToString(rawBytes).Replace("-", " ")}");
                }
            }
            
            for (int rec = 0; rec < numRecords; rec++)
            {
                int blockSize = 2048;
                int usableBlock = blockSize - 6;
                int recsPerBlock = usableBlock / recordSize;
                if (recsPerBlock <= 0) recsPerBlock = 1;
                
                int blockNum = rec / recsPerBlock;
                int recInBlock = rec % recsPerBlock;
                int offset = dataStart + (blockNum * blockSize) + 6 + (recInBlock * recordSize);
                
                if (offset + recordSize > bytes.Length) break;

                // Single.DB structure for Paradox 7 with Number (Double) fields:
                // Record size is 37 bytes:
                // Offset 0-7: MatchNo (Number/Double, 8 bytes) - FK to Match.DB
                // Offset 8-15: SingleNo (Number/Double, 8 bytes) - Frame number 1-10
                // Offset 16-23: HomePlayerNo (Number/Double, 8 bytes) - FK to Player.DB
                // Offset 24-31: AwayPlayerNo (Number/Double, 8 bytes) - FK to Player.DB
                // Offset 32-35: Winner (Alpha, 4 bytes) - "Home" or "Away"
                // Offset 36: EightBall (Logical, 1 byte) - 0x80=false, 0x81=true
                
                // Parse as Paradox Number (IEEE 754 double with modified sign bit)
                int matchNo = ParseParadoxDouble(bytes, offset);
                int frameNo = ParseParadoxDouble(bytes, offset + 8);
                int homePlayerNo = ParseParadoxDouble(bytes, offset + 16);
                int awayPlayerNo = ParseParadoxDouble(bytes, offset + 24);
                
                // Find Winner text (4 bytes at offset 32)
                var winner = FrameWinner.None;
                if (offset + 36 <= bytes.Length)
                {
                    var winnerBytes = new byte[4];
                    Array.Copy(bytes, offset + 32, winnerBytes, 0, 4);
                    var winnerText = Encoding.ASCII.GetString(winnerBytes).ToLower().Trim('\0');
                    
                    if (winnerText.StartsWith("home"))
                        winner = FrameWinner.Home;
                    else if (winnerText.StartsWith("away"))
                        winner = FrameWinner.Away;
                }
                
                // Check for 8-ball (1 byte at offset 36)
                // Paradox Logical: 0x80 = false, 0x81 = true
                bool eightBall = false;
                if (offset + 37 <= bytes.Length)
                {
                    // Only 0x81 means true; 0x80 means false
                    eightBall = bytes[offset + 36] == 0x81;
                    if (eightBall) eightBallCount++;
                }
                
                // Debug first few frames with 8-ball info
                if (rec < 5)
                {
                    var ebByte = offset + 36 < bytes.Length ? $"0x{bytes[offset + 36]:X2}" : "N/A";
                    summary.Errors.Add($"  Frame {rec+1}: Match={matchNo}, Frame={frameNo}, Home={homePlayerNo}, Away={awayPlayerNo}, Winner={winner}, 8Ball={eightBall} (byte={ebByte})");
                }
                
                // Find fixture
                if (matchNo <= 0)
                {
                    framesNoMatch++;
                    continue;
                }
                
                if (!_fixtureMap.TryGetValue(matchNo, out var fixtureId))
                {
                    framesNoMatch++;
                    continue;
                }
                
                var fixture = data.Fixtures.FirstOrDefault(f => f.Id == fixtureId);
                if (fixture == null)
                {
                    framesNoMatch++;
                    continue;
                }
                
                // Get player GUIDs
                Guid? homePlayerId = null;
                Guid? awayPlayerId = null;
                
                if (homePlayerNo > 0 && _playerMap.TryGetValue(homePlayerNo, out var hpg))
                    homePlayerId = hpg;
                else if (homePlayerNo > 0)
                    unmappedPlayerIds.Add(homePlayerNo);
                    
                if (awayPlayerNo > 0 && _playerMap.TryGetValue(awayPlayerNo, out var apg))
                    awayPlayerId = apg;
                else if (awayPlayerNo > 0)
                    unmappedPlayerIds.Add(awayPlayerNo);
                
                // Track partial player mappings (one player found, one not)
                bool isPartial = (homePlayerId == null && awayPlayerId != null) || 
                                 (homePlayerId != null && awayPlayerId == null);
                if (isPartial)
                {
                    framesPartialPlayers++;
                }
                
                // Skip only if BOTH players are missing (void frames)
                // Import partial frames - we still have the winner and one player
                if (homePlayerId == null && awayPlayerId == null)
                {
                    framesNoPlayers++;
                    continue;
                }
                
                // Determine frame number (use parsed value or sequential)
                int actualFrameNo = frameNo > 0 && frameNo <= 15 ? frameNo : fixture.Frames.Count + 1;
                
                // Check if frame already exists
                if (fixture.Frames.Any(f => f.Number == actualFrameNo))
                {
                    framesSkipped++;
                    continue;
                }
                
                var frame = new FrameResult
                {
                    Number = actualFrameNo,
                    HomePlayerId = homePlayerId,
                    AwayPlayerId = awayPlayerId,
                    Winner = winner,
                    EightBall = eightBall
                };
                
                fixture.Frames.Add(frame);
                framesImported++;
            }
            
            summary.FramesImported = framesImported;
            summary.Errors.Add($"✓ Imported {framesImported} frame results ({eightBallCount} with 8-ball wins)");
            if (framesSkipped > 0) summary.Errors.Add($"  ({framesSkipped} duplicates skipped)");
            if (framesNoMatch > 0) summary.Errors.Add($"  ({framesNoMatch} no matching fixture - orphan data in source DB)");
            if (framesNoPlayers > 0) summary.Errors.Add($"  ({framesNoPlayers} void/no players)");
            if (framesPartialPlayers > 0) summary.Errors.Add($"  ({framesPartialPlayers} partial - one player from 'Void Frame' range, still imported)");
            
            // Log unmapped player IDs (condensed)
            if (unmappedPlayerIds.Any())
            {
                var sortedIds = unmappedPlayerIds.OrderBy(x => x).ToList();
                var voidRangeCount = sortedIds.Count(id => id >= 1 && id <= 16);
                var otherCount = sortedIds.Count - voidRangeCount;
                
                if (voidRangeCount > 0)
                    summary.Errors.Add($"  ℹ️ {voidRangeCount} player refs in 'Void Frame' range (1-16) - these are placeholder entries in source DB");
                if (otherCount > 0)
                    summary.Errors.Add($"  ⚠️ {otherCount} player refs outside known range: {string.Join(", ", sortedIds.Where(id => id < 1 || id > 16).Take(10))}{(otherCount > 10 ? "..." : "")}");
            }
        }
        catch (Exception ex) { summary.Errors.Add($"❌ Singles error: {ex.Message}"); }
    }

    /// <summary>
    /// Parse a Paradox Number field (IEEE 754 double with modified sign bit) and return as int
    /// </summary>
    private int ParseParadoxDouble(byte[] bytes, int offset)
    {
        if (offset + 8 > bytes.Length) return 0;
        
        // Check if the value is empty (all zeros)
        bool allZero = true;
        for (int i = 0; i < 8; i++)
        {
            if (bytes[offset + i] != 0)
            {
                allZero = false;
                break;
            }
        }
        if (allZero) return 0;
        
        // Paradox stores doubles in big-endian with a modified sign bit:
        // If bit 7 of first byte is set, the number is positive and bit 7 should be cleared
        // If bit 7 is not set, the number is negative and all bits should be inverted
        var modBytes = new byte[8];
        
        if ((bytes[offset] & 0x80) == 0x80)
        {
            // Positive number - clear the sign bit
            modBytes[0] = (byte)(bytes[offset] ^ 0x80);
            for (int i = 1; i < 8; i++)
                modBytes[i] = bytes[offset + i];
        }
        else
        {
            // Negative number - invert all bits
            for (int i = 0; i < 8; i++)
                modBytes[i] = (byte)(bytes[offset + i] ^ 0xFF);
        }
        
        // Reverse to little-endian for BitConverter
        Array.Reverse(modBytes);
        
        try
        {
            double value = BitConverter.ToDouble(modBytes, 0);
            return (int)Math.Round(value);
        }
        catch
        {
            return 0;
        }
    }

    private void ImportDoubles(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Dbls.DB") ?? FindFile("Double.DB");
        if (filePath == null) { summary.Errors.Add("⚠️ Dbls.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            summary.Errors.Add($"✓ Found {numRecords} doubles records (not imported as individual frames)");
        }
        catch (Exception ex) { summary.Errors.Add($"❌ Doubles error: {ex.Message}"); }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Extract text string starting at a specific offset
    /// </summary>
    private string ExtractTextAfterOffset(byte[] bytes, int start, int maxLength)
    {
        var sb = new StringBuilder();
        int end = Math.Min(start + maxLength, bytes.Length);
        
        for (int i = start; i < end; i++)
        {
            var b = bytes[i];
            
            if (b >= 32 && b < 127) // Printable ASCII
            {
                sb.Append((char)b);
            }
            else if (sb.Length > 0 && b == 0)
            {
                // End of string
                break;
            }
            else if (sb.Length > 0)
            {
                // Non-printable after text - end
                break;
            }
        }
        
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Scan raw bytes for readable text strings in records
    /// </summary>
    private List<string> ScanForTextRecords(byte[] bytes, int dataStart, int recordSize, int maxRecords)
    {
        var results = new List<string>();
        
        for (int rec = 0; rec < maxRecords; rec++)
        {
            int blockSize = 2048;
            int usableBlock = blockSize - 6;
            int recsPerBlock = usableBlock / recordSize;
            if (recsPerBlock <= 0) recsPerBlock = 1;
            
            int blockNum = rec / recsPerBlock;
            int recInBlock = rec % recsPerBlock;
            int offset = dataStart + (blockNum * blockSize) + 6 + (recInBlock * recordSize);
            
            if (offset + recordSize > bytes.Length) break;

            // Find the first readable text string in this record
            var text = ExtractFirstTextString(bytes, offset, recordSize);
            if (!string.IsNullOrEmpty(text))
            {
                results.Add(text);
            }
        }
        
        return results;
    }

    /// <summary>
    /// Extract the first readable text string from a byte range
    /// </summary>
    private string ExtractFirstTextString(byte[] bytes, int start, int length)
    {
        var sb = new StringBuilder();
        bool foundText = false;
        int end = Math.Min(start + length, bytes.Length);
        
        // Skip first 4 bytes (usually ID field)
        for (int i = start + 4; i < end; i++)
        {
            var b = bytes[i];
            
            if (b >= 32 && b < 127) // Printable ASCII
            {
                sb.Append((char)b);
                foundText = true;
            }
            else if (foundText && b == 0)
            {
                // End of string
                break;
            }
            else if (sb.Length > 0)
            {
                // Non-printable after some text - might be end
                break;
            }
        }
        
        var result = sb.ToString().Trim();
        
        // Validate - should look like a name, not random chars
        if (result.Length >= 2 && result.Length <= 60 && !result.All(char.IsDigit))
        {
            return result;
        }
        
        return "";
    }

    private string? FindFile(string fileName)
    {
        var path = Path.Combine(_folderPath, fileName);
        if (File.Exists(path)) return path;
        return Directory.GetFiles(_folderPath, "*.DB", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    public static (bool hasData, string summary, List<string> files) ScanFolder(string folderPath)
    {
        var files = new List<string>();
        var sb = new StringBuilder();

        try
        {
            if (!Directory.Exists(folderPath)) return (false, "Folder not found", files);

            var dbFiles = Directory.GetFiles(folderPath, "*.DB", SearchOption.TopDirectoryOnly);
            foreach (var file in dbFiles)
            {
                var fileName = Path.GetFileName(file).ToUpperInvariant();
                var fileSize = new FileInfo(file).Length;
                files.Add(fileName);
                sb.AppendLine($"  ✓ {fileName} ({fileSize / 1024:N0} KB)");
            }

            return files.Any() ? (true, sb.ToString(), files) : (false, "No .DB files found", files);
        }
        catch (Exception ex) { return (false, $"Error: {ex.Message}", files); }
    }

    #endregion
}
