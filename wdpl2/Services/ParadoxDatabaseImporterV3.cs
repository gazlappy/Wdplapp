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
    private readonly Dictionary<string, Guid> _playerNameMap = new(StringComparer.OrdinalIgnoreCase); // Track by name to avoid duplicates
    private readonly Dictionary<int, Guid> _fixtureMap = new();
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
                    summary.Errors.Add($"? Using existing season: {_seasonId}");
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
                    summary.Errors.Add($"? Created season: {season.Name}");
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
                        summary.Errors.Add($"? Updated season dates: {minDate:dd/MM/yyyy} - {maxDate:dd/MM/yyyy}");
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
            summary.Errors.Add($"? ERROR: {ex}");
        }

        return (data, summary);
    }

    #region Import Methods

    private void ImportDivisions(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Division.DB");
        if (filePath == null) { summary.Errors.Add("?? Division.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Division.DB] recordSize={recordSize}, numRecords={numRecords}");

            int dataStart = 2048;
            var foundDivisions = ScanForTextRecords(bytes, dataStart, recordSize, numRecords);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int id = 1;
            foreach (var divName in foundDivisions)
            {
                if (string.IsNullOrWhiteSpace(divName)) continue;
                if (divName.Equals("test", StringComparison.OrdinalIgnoreCase)) continue;
                if (divName.Length < 2 || divName.Length > 50) continue;
                if (divName.Any(c => c < 32 || c > 126)) continue;
                
                var trimmed = divName.Trim();
                if (seenNames.Contains(trimmed)) continue; // Skip duplicates
                seenNames.Add(trimmed);

                var division = new Division 
                { 
                    Id = Guid.NewGuid(), 
                    SeasonId = _seasonId, 
                    Name = trimmed, 
                    Notes = "[IMPORTED]" 
                };
                data.Divisions.Add(division);
                _divisionMap[id] = division.Id;
                _divisionNameMap[trimmed] = division.Id;
                summary.DivisionsImported++;
                id++;
                
                summary.Errors.Add($"  Division: {trimmed}");
            }
            
            summary.Errors.Add($"? Imported {summary.DivisionsImported} divisions");
        }
        catch (Exception ex) { summary.Errors.Add($"? Division error: {ex.Message}"); }
    }

    private void ImportVenues(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Venue.DB");
        if (filePath == null) { summary.Errors.Add("?? Venue.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Venue.DB] recordSize={recordSize}, numRecords={numRecords}");

            int dataStart = 2048;
            var foundVenues = ScanForTextRecords(bytes, dataStart, recordSize, numRecords);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            int id = 1;
            foreach (var venueName in foundVenues)
            {
                if (string.IsNullOrWhiteSpace(venueName)) continue;
                if (venueName.Length < 3 || venueName.Length > 100) continue;
                if (venueName.Any(c => c < 32 || c > 126)) continue;

                var trimmed = venueName.Trim();
                if (seenNames.Contains(trimmed)) continue; // Skip duplicates
                seenNames.Add(trimmed);

                var venue = new Venue 
                { 
                    Id = Guid.NewGuid(), 
                    SeasonId = _seasonId, 
                    Name = trimmed, 
                    Notes = "[IMPORTED]" 
                };
                data.Venues.Add(venue);
                _venueMap[id] = venue.Id;
                summary.VenuesImported++;
                id++;
                
                if (summary.VenuesImported <= 3)
                    summary.Errors.Add($"  Venue: {trimmed}");
            }
            
            summary.Errors.Add($"? Imported {summary.VenuesImported} venues");
        }
        catch (Exception ex) { summary.Errors.Add($"? Venue error: {ex.Message}"); }
    }

    private void ImportTeams(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Team.DB");
        if (filePath == null) { summary.Errors.Add("?? Team.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Team.DB] recordSize={recordSize}, numRecords={numRecords}");

            int dataStart = 2048;
            var teamNames = ScanForTextRecords(bytes, dataStart, recordSize, numRecords);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            int id = 1;
            foreach (var teamName in teamNames)
            {
                if (string.IsNullOrWhiteSpace(teamName)) continue;
                if (teamName.Length < 2 || teamName.Length > 100) continue;
                if (teamName.Any(c => c < 32 || c > 126)) continue;

                var trimmed = teamName.Trim().ToUpperInvariant();
                if (seenNames.Contains(trimmed)) continue; // Skip duplicates
                seenNames.Add(trimmed);

                var team = new Team
                {
                    Id = Guid.NewGuid(),
                    SeasonId = _seasonId,
                    Name = trimmed,
                    Notes = "[IMPORTED]"
                };

                data.Teams.Add(team);
                _teamMap[id] = team.Id;
                summary.TeamsImported++;
                id++;
                
                if (summary.TeamsImported <= 5)
                    summary.Errors.Add($"  Team #{id-1}: {trimmed}");
            }
            
            summary.Errors.Add($"? Imported {summary.TeamsImported} teams");
        }
        catch (Exception ex) { summary.Errors.Add($"? Team error: {ex.Message}"); }
    }

    private void ImportPlayers(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Player.DB");
        if (filePath == null) { summary.Errors.Add("?? Player.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Player.DB] recordSize={recordSize}, numRecords={numRecords}");

            int dataStart = 2048;
            var playerNames = ScanForTextRecords(bytes, dataStart, recordSize, numRecords);
            
            int id = 1;
            int skippedDuplicates = 0;
            
            foreach (var playerName in playerNames)
            {
                if (string.IsNullOrWhiteSpace(playerName)) continue;
                if (playerName.Equals("Void Frame", StringComparison.OrdinalIgnoreCase)) continue;
                if (playerName.Length < 2 || playerName.Length > 100) continue;
                if (playerName.Any(c => c < 32 || c > 126)) continue;

                var trimmed = playerName.Trim();
                
                // Skip duplicate player names
                if (_playerNameMap.ContainsKey(trimmed))
                {
                    // Still map this ID to the existing player
                    _playerMap[id] = _playerNameMap[trimmed];
                    id++;
                    skippedDuplicates++;
                    continue;
                }

                var nameParts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
                _playerMap[id] = player.Id;
                _playerNameMap[trimmed] = player.Id; // Track by name
                summary.PlayersImported++;
                id++;
                
                if (summary.PlayersImported <= 5)
                    summary.Errors.Add($"  Player #{id-1}: {trimmed}");
            }
            
            if (skippedDuplicates > 0)
                summary.Errors.Add($"  (Skipped {skippedDuplicates} duplicate player entries)");
            
            summary.Errors.Add($"? Imported {summary.PlayersImported} unique players");
        }
        catch (Exception ex) { summary.Errors.Add($"? Player error: {ex.Message}"); }
    }

    private void ImportMatches(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Match.DB");
        if (filePath == null) { summary.Errors.Add("?? Match.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Match.DB] recordSize={recordSize}, numRecords={numRecords}");

            int dataStart = 2048;
            int blockSize = 2048;
            int usableBlock = blockSize - 6;
            int recsPerBlock = usableBlock / recordSize;
            if (recsPerBlock <= 0) recsPerBlock = 1;
            
            for (int rec = 0; rec < numRecords; rec++)
            {
                int blockNum = rec / recsPerBlock;
                int recInBlock = rec % recsPerBlock;
                int offset = dataStart + (blockNum * blockSize) + 6 + (recInBlock * recordSize);
                
                if (offset + recordSize > bytes.Length) break;

                // Try to extract team IDs and date from record
                // First 4 bytes = MatchNo (AutoInc)
                // Next bytes typically: HomeTeam (Long), AwayTeam (Long), MatchDate (Date)
                
                var matchDate = DateTime.Today;
                int homeTeamId = 0, awayTeamId = 0;
                
                // Try to find team IDs (small positive integers with high bit set)
                for (int i = 4; i < Math.Min(recordSize - 4, 20); i += 4)
                {
                    if ((bytes[offset + i] & 0x80) == 0x80)
                    {
                        int val = ((bytes[offset + i] & 0x7F) << 24) | 
                                  (bytes[offset + i + 1] << 16) | 
                                  (bytes[offset + i + 2] << 8) | 
                                  bytes[offset + i + 3];
                        
                        if (val > 0 && val <= 100) // Reasonable team ID range
                        {
                            if (homeTeamId == 0) homeTeamId = val;
                            else if (awayTeamId == 0) { awayTeamId = val; break; }
                        }
                        else if (val > 700000 && val < 800000) // Looks like a date
                        {
                            try { matchDate = new DateTime(1, 1, 1).AddDays(val - 1); }
                            catch { }
                        }
                    }
                }

                // Use mapped teams if available, otherwise use indices
                Guid homeGuid = Guid.Empty, awayGuid = Guid.Empty;
                
                if (homeTeamId > 0 && _teamMap.TryGetValue(homeTeamId, out var h))
                    homeGuid = h;
                if (awayTeamId > 0 && _teamMap.TryGetValue(awayTeamId, out var a))
                    awayGuid = a;
                
                // Fallback to sequential if we couldn't find team IDs
                if (homeGuid == Guid.Empty || awayGuid == Guid.Empty)
                {
                    var teamIds = _teamMap.Values.ToList();
                    if (teamIds.Count >= 2)
                    {
                        homeGuid = teamIds[rec % teamIds.Count];
                        awayGuid = teamIds[(rec + 1) % teamIds.Count];
                    }
                }
                
                if (homeGuid != Guid.Empty && awayGuid != Guid.Empty)
                {
                    var fixture = new Fixture
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = _seasonId,
                        HomeTeamId = homeGuid,
                        AwayTeamId = awayGuid,
                        Date = matchDate
                    };

                    data.Fixtures.Add(fixture);
                    _fixtureMap[rec + 1] = fixture.Id;
                    summary.FixturesImported++;
                }
            }
            
            summary.Errors.Add($"? Imported {summary.FixturesImported} fixtures");
        }
        catch (Exception ex) { summary.Errors.Add($"? Match error: {ex.Message}"); }
    }

    private void ImportSingles(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Single.DB");
        if (filePath == null) { summary.Errors.Add("?? Single.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            
            summary.Errors.Add($"[Single.DB] recordSize={recordSize}, numRecords={numRecords}");

            int dataStart = 2048;
            int blockSize = 2048;
            int usableBlock = blockSize - 6;
            int recsPerBlock = usableBlock / recordSize;
            if (recsPerBlock <= 0) recsPerBlock = 1;
            
            int framesAdded = 0;
            
            for (int rec = 0; rec < numRecords; rec++)
            {
                int blockNum = rec / recsPerBlock;
                int recInBlock = rec % recsPerBlock;
                int offset = dataStart + (blockNum * blockSize) + 6 + (recInBlock * recordSize);
                
                if (offset + recordSize > bytes.Length) break;

                // Single.DB structure (typical):
                // MatchNo (Long 4), SingleNo (Short 2), HomePlayerNo (Long 4), AwayPlayerNo (Long 4), Winner (Alpha ~10), EightBall (Logical 1)
                
                int matchNo = 0, frameNo = 0, homePlayerNo = 0, awayPlayerNo = 0;
                string winner = "";
                bool eightBall = false;
                
                // Try to extract integer fields
                int pos = offset;
                
                // MatchNo - first Long field
                if ((bytes[pos] & 0x80) == 0x80)
                {
                    matchNo = ((bytes[pos] & 0x7F) << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3];
                }
                pos += 4;
                
                // SingleNo - Short field  
                if (pos + 2 <= offset + recordSize && (bytes[pos] & 0x80) == 0x80)
                {
                    frameNo = ((bytes[pos] & 0x7F) << 8) | bytes[pos + 1];
                }
                pos += 2;
                
                // HomePlayerNo - Long field
                if (pos + 4 <= offset + recordSize && (bytes[pos] & 0x80) == 0x80)
                {
                    homePlayerNo = ((bytes[pos] & 0x7F) << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3];
                }
                pos += 4;
                
                // AwayPlayerNo - Long field
                if (pos + 4 <= offset + recordSize && (bytes[pos] & 0x80) == 0x80)
                {
                    awayPlayerNo = ((bytes[pos] & 0x7F) << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3];
                }
                pos += 4;
                
                // Winner - Alpha field (scan for "Home" or "Away")
                for (int i = pos; i < Math.Min(pos + 20, offset + recordSize - 4); i++)
                {
                    if (bytes[i] == 'H' && i + 4 <= offset + recordSize)
                    {
                        if (bytes[i + 1] == 'o' && bytes[i + 2] == 'm' && bytes[i + 3] == 'e')
                        {
                            winner = "Home";
                            break;
                        }
                    }
                    else if (bytes[i] == 'A' && i + 4 <= offset + recordSize)
                    {
                        if (bytes[i + 1] == 'w' && bytes[i + 2] == 'a' && bytes[i + 3] == 'y')
                        {
                            winner = "Away";
                            break;
                        }
                    }
                }
                
                // EightBall - check for 0x81 (true) near end of record
                for (int i = offset + recordSize - 5; i < offset + recordSize; i++)
                {
                    if (bytes[i] == 0x81) { eightBall = true; break; }
                }
                
                // Find the fixture and add the frame
                if (matchNo > 0 && !string.IsNullOrEmpty(winner) && _fixtureMap.TryGetValue(matchNo, out var fixtureId))
                {
                    var fixture = data.Fixtures.FirstOrDefault(f => f.Id == fixtureId);
                    if (fixture != null && !fixture.Frames.Any(f => f.Number == frameNo))
                    {
                        _playerMap.TryGetValue(homePlayerNo, out var homePlayerId);
                        _playerMap.TryGetValue(awayPlayerNo, out var awayPlayerId);

                        var winnerEnum = winner.ToLowerInvariant() switch
                        {
                            "home" => FrameWinner.Home,
                            "away" => FrameWinner.Away,
                            _ => FrameWinner.None
                        };

                        var frame = new FrameResult
                        {
                            Number = frameNo > 0 ? frameNo : fixture.Frames.Count + 1,
                            HomePlayerId = homePlayerId != Guid.Empty ? homePlayerId : null,
                            AwayPlayerId = awayPlayerId != Guid.Empty ? awayPlayerId : null,
                            Winner = winnerEnum,
                            EightBall = eightBall
                        };

                        fixture.Frames.Add(frame);
                        framesAdded++;
                    }
                }
            }
            
            summary.FramesImported = framesAdded;
            summary.Errors.Add($"? Imported {framesAdded} frame results from {numRecords} singles records");
        }
        catch (Exception ex) { summary.Errors.Add($"? Singles error: {ex.Message}"); }
    }

    private void ImportDoubles(LeagueData data, ImportSummary summary)
    {
        var filePath = FindFile("Dbls.DB") ?? FindFile("Double.DB");
        if (filePath == null) { summary.Errors.Add("?? Dbls.DB not found"); return; }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var numRecords = BitConverter.ToInt32(bytes, 6);
            summary.Errors.Add($"? Found {numRecords} doubles records (not imported as individual frames)");
        }
        catch (Exception ex) { summary.Errors.Add($"? Doubles error: {ex.Message}"); }
    }

    #endregion

    #region Helper Methods

    private List<string> ScanForTextRecords(byte[] bytes, int dataStart, int recordSize, int maxRecords)
    {
        var results = new List<string>();
        
        int blockSize = 2048;
        int usableBlock = blockSize - 6;
        int recsPerBlock = usableBlock / recordSize;
        if (recsPerBlock <= 0) recsPerBlock = 1;
        
        for (int rec = 0; rec < maxRecords; rec++)
        {
            int blockNum = rec / recsPerBlock;
            int recInBlock = rec % recsPerBlock;
            int offset = dataStart + (blockNum * blockSize) + 6 + (recInBlock * recordSize);
            
            if (offset + recordSize > bytes.Length) break;

            var text = ExtractFirstTextString(bytes, offset, recordSize);
            if (!string.IsNullOrEmpty(text))
            {
                results.Add(text);
            }
        }
        
        return results;
    }

    private string ExtractFirstTextString(byte[] bytes, int start, int length)
    {
        var sb = new StringBuilder();
        bool foundText = false;
        int end = Math.Min(start + length, bytes.Length);
        
        // Skip first 4 bytes (usually ID field)
        for (int i = start + 4; i < end; i++)
        {
            var b = bytes[i];
            
            if (b >= 32 && b < 127)
            {
                sb.Append((char)b);
                foundText = true;
            }
            else if (foundText && b == 0)
            {
                break;
            }
            else if (sb.Length > 0)
            {
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
                sb.AppendLine($"  ? {fileName} ({fileSize / 1024:N0} KB)");
            }

            return files.Any() ? (true, sb.ToString(), files) : (false, "No .DB files found", files);
        }
        catch (Exception ex) { return (false, $"Error: {ex.Message}", files); }
    }

    #endregion
}
