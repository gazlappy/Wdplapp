using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Wdpl2.Services;

/// <summary>
/// Parser for Paradox database files (.DB format)
/// Used to import legacy pool league data from old Paradox-based systems
/// </summary>
public static class ParadoxDatabaseParser
{
    /// <summary>
    /// Result of parsing a Paradox database folder
    /// </summary>
    public class ParadoxParseResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        
        // Parsed data
        public List<ParadoxDivision> Divisions { get; set; } = new();
        public List<ParadoxTeam> Teams { get; set; } = new();
        public List<ParadoxPlayer> Players { get; set; } = new();
        public List<ParadoxMatch> Matches { get; set; } = new();
        public List<ParadoxSingle> Singles { get; set; } = new();
        public List<ParadoxVenue> Venues { get; set; } = new();
    }

    public class ParadoxDivision
    {
        public int ItemId { get; set; }
        public string Abbreviated { get; set; } = "";
        public string FullDivisionName { get; set; } = "";
    }

    public class ParadoxTeam
    {
        public int ItemId { get; set; }
        public string TeamName { get; set; } = "";
        public int? VenueId { get; set; }
        public int? DivisionId { get; set; }
        public string Contact { get; set; } = "";
        public string ContactAddress1 { get; set; } = "";
        public string ContactAddress2 { get; set; } = "";
        public string ContactAddress3 { get; set; } = "";
        public string ContactAddress4 { get; set; } = "";
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public int SinglesWins { get; set; }
        public int SinglesLosses { get; set; }
        public int DoublesWins { get; set; }
        public int DoublesLosses { get; set; }
        public int Points { get; set; }
        public int Played { get; set; }
        public bool Withdrawn { get; set; }
        public bool RemoveResults { get; set; }
    }

    public class ParadoxPlayer
    {
        public int PlayerNo { get; set; }
        public string PlayerName { get; set; } = "";
        public int? PlayerTeam { get; set; }
        public int Played { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int? CurrentRating { get; set; }
        public int? BestRating { get; set; }
        public DateTime? BestRatingDate { get; set; }
        public int EightBalls { get; set; }
        
        // Parsed name parts
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
    }

    public class ParadoxMatch
    {
        public int MatchNo { get; set; }
        public int HomeTeam { get; set; }
        public int AwayTeam { get; set; }
        public DateTime MatchDate { get; set; }
        public int HomeSinglesWins { get; set; }
        public int AwaySinglesWins { get; set; }
        public int HomeDoublesWins { get; set; }
        public int AwayDoublesWins { get; set; }
        public string DivisionName { get; set; } = "";
    }

    public class ParadoxSingle
    {
        public int MatchNo { get; set; }
        public int SingleNo { get; set; }
        public int HomePlayerNo { get; set; }
        public int AwayPlayerNo { get; set; }
        public string Winner { get; set; } = ""; // "Home" or "Away"
        public bool EightBall { get; set; }
    }

    public class ParadoxVenue
    {
        public int ItemId { get; set; }
        public string VenueName { get; set; } = "";
        public string Address { get; set; } = "";
    }

    /// <summary>
    /// Parse all Paradox database files in a folder
    /// </summary>
    public static ParadoxParseResult ParseFolder(string folderPath)
    {
        var result = new ParadoxParseResult();

        try
        {
            if (!Directory.Exists(folderPath))
            {
                result.Errors.Add($"Folder not found: {folderPath}");
                return result;
            }

            // Look for the main .DB files
            var files = Directory.GetFiles(folderPath, "*.DB", SearchOption.TopDirectoryOnly)
                .Select(f => Path.GetFileName(f).ToUpperInvariant())
                .ToHashSet();

            // Parse Division.DB
            var divisionPath = FindFile(folderPath, "Division.DB");
            if (divisionPath != null)
            {
                result.Divisions = ParseDivisionDb(divisionPath, result);
                System.Diagnostics.Debug.WriteLine($"Parsed {result.Divisions.Count} divisions");
            }
            else
            {
                result.Warnings.Add("Division.DB not found");
            }

            // Parse Team.DB
            var teamPath = FindFile(folderPath, "Team.DB");
            if (teamPath != null)
            {
                result.Teams = ParseTeamDb(teamPath, result);
                System.Diagnostics.Debug.WriteLine($"Parsed {result.Teams.Count} teams");
            }
            else
            {
                result.Warnings.Add("Team.DB not found");
            }

            // Parse Player.DB
            var playerPath = FindFile(folderPath, "Player.DB");
            if (playerPath != null)
            {
                result.Players = ParsePlayerDb(playerPath, result);
                System.Diagnostics.Debug.WriteLine($"Parsed {result.Players.Count} players");
            }
            else
            {
                result.Warnings.Add("Player.DB not found");
            }

            // Parse Match.DB
            var matchPath = FindFile(folderPath, "Match.DB");
            if (matchPath != null)
            {
                result.Matches = ParseMatchDb(matchPath, result);
                System.Diagnostics.Debug.WriteLine($"Parsed {result.Matches.Count} matches");
            }
            else
            {
                result.Warnings.Add("Match.DB not found");
            }

            // Parse Single.DB (individual frame results)
            var singlePath = FindFile(folderPath, "Single.DB");
            if (singlePath != null)
            {
                result.Singles = ParseSingleDb(singlePath, result);
                System.Diagnostics.Debug.WriteLine($"Parsed {result.Singles.Count} singles frames");
            }
            else
            {
                result.Warnings.Add("Single.DB not found");
            }

            // Parse Venue.DB if exists
            var venuePath = FindFile(folderPath, "Venue.DB");
            if (venuePath != null)
            {
                result.Venues = ParseVenueDb(venuePath, result);
                System.Diagnostics.Debug.WriteLine($"Parsed {result.Venues.Count} venues");
            }

            result.Success = result.Divisions.Count > 0 || result.Teams.Count > 0 || 
                           result.Players.Count > 0 || result.Matches.Count > 0;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Paradox database: {ex.Message}");
        }

        return result;
    }

    private static string? FindFile(string folderPath, string fileName)
    {
        // Try exact case first
        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path)) return path;

        // Try case-insensitive search
        var files = Directory.GetFiles(folderPath, "*.DB", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            if (Path.GetFileName(file).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return file;
        }

        // Also check for a_ prefixed versions (archive copies)
        var aPath = Path.Combine(folderPath, "a_" + fileName);
        if (File.Exists(aPath)) return aPath;

        return null;
    }

    /// <summary>
    /// Parse Division.DB file
    /// Schema: Item_id, Abbreviated, FullDivisionName
    /// </summary>
    private static List<ParadoxDivision> ParseDivisionDb(string filePath, ParadoxParseResult result)
    {
        var divisions = new List<ParadoxDivision>();

        try
        {
            var content = File.ReadAllBytes(filePath);
            var records = ExtractParadoxRecords(content);

            foreach (var record in records)
            {
                // Parse record fields - Division has simple structure
                var fields = ParseRecordFields(record, new[] { "int", "string", "string" });
                if (fields.Count >= 3)
                {
                    var div = new ParadoxDivision
                    {
                        ItemId = Convert.ToInt32(fields[0]),
                        Abbreviated = CleanString(fields[1]?.ToString() ?? ""),
                        FullDivisionName = CleanString(fields[2]?.ToString() ?? "")
                    };
                    
                    if (!string.IsNullOrWhiteSpace(div.FullDivisionName) && 
                        !div.FullDivisionName.StartsWith("test", StringComparison.OrdinalIgnoreCase))
                    {
                        divisions.Add(div);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Error parsing Division.DB: {ex.Message}");
        }

        return divisions;
    }

    /// <summary>
    /// Parse Team.DB file
    /// Schema: Item_id, TeamName, Venue, Division, Contact, ContactAddress1-4, stats...
    /// Team names are typically ALL CAPS, contact names are Title Case, addresses have location words
    /// </summary>
    private static List<ParadoxTeam> ParseTeamDb(string filePath, ParadoxParseResult result)
    {
        var teams = new List<ParadoxTeam>();

        try
        {
            var content = File.ReadAllBytes(filePath);
            // Extract text strings that look like team names
            var textSegments = ExtractTextSegments(content, 4, 50);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Common address words to filter out
            var addressWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "road", "rd", "street", "st", "lane", "ln", "close", "cl", "way", "drive", "dr",
                "avenue", "ave", "court", "ct", "place", "pl", "terrace", "ter", "gardens", "gdns",
                "crescent", "cres", "park", "grove", "hill", "view", "farm", "house", "cottage",
                "somerset", "devon", "wellington", "taunton", "bristol", "exeter", "england",
                "nr", "north", "south", "east", "west", "lower", "upper", "milverton", "rockwell",
                "tonedale", "clifford", "quantock", "burrough", "foxmoor", "shillingford", "bampton",
                "lawne", "wood", "pyles", "thorne", "stedham", "quartley"
            };

            // Words that indicate this is likely a person name (Title Case pattern)
            bool LooksLikePersonName(string text)
            {
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || parts.Length > 3) return false;
                
                // Check if it's Title Case (first letter capital, rest lower)
                foreach (var part in parts)
                {
                    if (part.Length < 2) continue;
                    if (char.IsUpper(part[0]) && part.Skip(1).All(c => char.IsLower(c) || c == '\''))
                    {
                        // Looks like Title Case - could be a person name
                        return true;
                    }
                }
                return false;
            }

            // Check if text looks like an address
            bool LooksLikeAddress(string text)
            {
                var lower = text.ToLowerInvariant();
                var parts = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                // Contains address words
                if (parts.Any(p => addressWords.Contains(p)))
                    return true;
                
                // Starts with a number (house number)
                if (char.IsDigit(text.FirstOrDefault()))
                    return true;
                
                // Contains postcode pattern
                if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\b[A-Z]{1,2}\d{1,2}\s?\d[A-Z]{2}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    return true;
                
                return false;
            }

            // Check if text looks like a valid team name
            bool LooksLikeTeamName(string text)
            {
                // Team names are typically:
                // 1. ALL CAPS or mostly uppercase
                // 2. May contain apostrophes (e.g., "ROW'S REBELS")
                // 3. Often have fun/creative names
                // 4. Don't look like person names or addresses
                
                if (string.IsNullOrWhiteSpace(text)) return false;
                if (text.Length < 4) return false;
                
                // Filter out addresses
                if (LooksLikeAddress(text)) return false;
                
                // Filter out person names (Title Case with 2-3 parts)
                if (LooksLikePersonName(text)) return false;
                
                // Count uppercase vs lowercase letters
                int upperCount = text.Count(c => char.IsUpper(c));
                int lowerCount = text.Count(c => char.IsLower(c));
                int letterCount = upperCount + lowerCount;
                
                if (letterCount == 0) return false;
                
                // If mostly uppercase (>70%), likely a team name
                double upperRatio = (double)upperCount / letterCount;
                if (upperRatio >= 0.7)
                    return true;
                
                // If it's Title Case with creative words, might be a team name
                // but filter out common person name patterns
                var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                // Team names often have words like: Rebels, Rockets, Bears, Stars, Crew, etc.
                var teamWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "rebels", "rockets", "bears", "stars", "crew", "army", "blacks", "nutts",
                    "champions", "legends", "jesters", "snipers", "giants", "squad", "tribe",
                    "premier", "pool", "cc", "fc", "inn", "arms", "bar", "pub", "club",
                    "annihalation", "annihilation", "operation", "opperation", "all", "cued", "up"
                };
                
                if (words.Any(w => teamWords.Contains(w.ToLowerInvariant())))
                    return true;
                
                return false;
            }

            foreach (var segment in textSegments)
            {
                var name = CleanString(segment);
                
                // Basic filters
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (name.Length < 4) continue;
                if (name.StartsWith("Team", StringComparison.OrdinalIgnoreCase) && name.Contains(".")) continue;
                if (name.Contains("resttemp")) continue;
                if (name.Equals("ascii", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.All(c => char.IsDigit(c) || char.IsWhiteSpace(c))) continue;
                
                // Apply team name detection
                if (!LooksLikeTeamName(name)) continue;
                
                // Normalize to uppercase
                var upperName = name.ToUpperInvariant();
                if (!seenNames.Contains(upperName))
                {
                    seenNames.Add(upperName);
                    teams.Add(new ParadoxTeam
                    {
                        ItemId = teams.Count + 1,
                        TeamName = upperName
                    });
                    System.Diagnostics.Debug.WriteLine($"Found team: {upperName}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Error parsing Team.DB: {ex.Message}");
        }

        return teams;
    }

    /// <summary>
    /// Parse Player.DB file
    /// Schema: PlayerNo, PlayerName, PlayerTeam, Played, Wins, Losses, CurrentRating, BestRating, BestRatingDate, EightBalls
    /// Player names are typically "FirstName LastName" in various cases
    /// </summary>
    private static List<ParadoxPlayer> ParsePlayerDb(string filePath, ParadoxParseResult result)
    {
        var players = new List<ParadoxPlayer>();

        try
        {
            var content = File.ReadAllBytes(filePath);
            // Extract text strings that look like player names
            var textSegments = ExtractTextSegments(content, 3, 40);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int playerNo = 1;

            // Common non-name words to filter out
            var excludePatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "void frame", "resttemp", "ascii", "player", "team", "rating", "played",
                "wins", "losses", "eight", "balls", "best", "current", "date"
            };

            // Words that suggest this is an address, not a name
            var addressWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "road", "rd", "street", "st", "lane", "close", "way", "drive", "avenue",
                "somerset", "devon", "wellington", "taunton", "bristol"
            };

            bool LooksLikePlayerName(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return false;
                
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                // Should have 2-3 parts (First Last or First Middle Last)
                if (parts.Length < 2 || parts.Length > 4) return false;
                
                // Should not contain address words
                if (parts.Any(p => addressWords.Contains(p))) return false;
                
                // Each part should start with a letter
                if (!parts.All(p => p.Length > 0 && char.IsLetter(p[0]))) return false;
                
                // First name should be reasonable length (2-15 chars)
                if (parts[0].Length < 2 || parts[0].Length > 15) return false;
                
                // Last name should be reasonable length (2-20 chars)
                var lastName = string.Join(" ", parts.Skip(1));
                if (lastName.Length < 2 || lastName.Length > 25) return false;
                
                // Should not be all the same letter repeated
                if (parts[0].Distinct().Count() == 1) return false;
                
                return true;
            }

            foreach (var segment in textSegments)
            {
                var name = CleanString(segment);
                
                // Basic filters
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (name.Length < 4) continue;
                if (excludePatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase))) continue;
                if (name.All(c => char.IsDigit(c) || char.IsWhiteSpace(c))) continue;
                
                // Apply player name detection
                if (!LooksLikePlayerName(name)) continue;
                
                // Normalize
                var normalizedName = NormalizeNameCasing(name);
                var upperName = normalizedName.ToUpperInvariant();
                
                if (!seenNames.Contains(upperName))
                {
                    seenNames.Add(upperName);
                    
                    var parts = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var player = new ParadoxPlayer
                    {
                        PlayerNo = playerNo++,
                        PlayerName = upperName,
                        FirstName = parts[0].ToUpperInvariant(),
                        LastName = string.Join(" ", parts.Skip(1)).ToUpperInvariant()
                    };
                    
                    players.Add(player);
                    System.Diagnostics.Debug.WriteLine($"Found player: {player.FirstName} {player.LastName}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Error parsing Player.DB: {ex.Message}");
        }

        return players;
    }

    /// <summary>
    /// Normalize name casing (Title Case)
    /// </summary>
    private static string NormalizeNameCasing(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                parts[i] = char.ToUpperInvariant(parts[i][0]) + 
                          (parts[i].Length > 1 ? parts[i].Substring(1).ToLowerInvariant() : "");
            }
        }
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Parse Match.DB file
    /// Schema: MatchNo, HomeTeam, AwayTeam, MatchDate, HSWins, ASWins, HDWins, ADWins, DivName
    /// Record structure: The date appears to be stored as 2 bytes (days since epoch), team IDs as bytes
    /// </summary>
    private static List<ParadoxMatch> ParseMatchDb(string filePath, ParadoxParseResult result)
    {
        var matches = new List<ParadoxMatch>();

        try
        {
            var content = File.ReadAllBytes(filePath);
            
            // Find all occurrences of division names which mark the end of records
            var divisionMarkers = new[] { "Premier", "One", "Two" };
            var recordPositions = new List<(int position, string division)>();
            
            foreach (var div in divisionMarkers)
            {
                var divBytes = Encoding.ASCII.GetBytes(div);
                for (int i = 0; i <= content.Length - divBytes.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < divBytes.Length; j++)
                    {
                        if (content[i + j] != divBytes[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        recordPositions.Add((i, div));
                    }
                }
            }
            
            // Sort by position
            recordPositions = recordPositions.OrderBy(r => r.position).ToList();
            
            // Parse each record - look backwards from division name for match data
            // Record structure appears to be: MatchNo(2), HomeTeam(2), AwayTeam(2), Date(4), HSWins(2), ASWins(2), HDWins(2), ADWins(2), DivName
            int matchNo = 1;
            var seenMatches = new HashSet<string>();
            
            foreach (var (pos, div) in recordPositions)
            {
                try
                {
                    // Look back from division name position to find the record data
                    // Skip any padding/whitespace
                    int dataStart = pos - 30; // Approximate record size before division name
                    if (dataStart < 0) continue;
                    
                    // Try to extract team IDs and scores from the bytes before the division name
                    // The structure varies, so we'll look for valid byte patterns
                    
                    // Look for the score pattern (small integers 0-15 typically)
                    int scoreOffset = pos - 20;
                    if (scoreOffset < 10) continue;
                    
                    // Extract what appears to be date (4 bytes before scores area)
                    int dateOffset = pos - 28;
                    if (dateOffset < 0) continue;
                    
                    // Read potential date value (Paradox dates are days since Dec 31, 1899)
                    int dateValue = 0;
                    if (dateOffset + 3 < content.Length)
                    {
                        // Try reading as little-endian int32
                        dateValue = content[dateOffset] | 
                                   (content[dateOffset + 1] << 8) |
                                   (content[dateOffset + 2] << 16) |
                                   (content[dateOffset + 3] << 24);
                    }
                    
                    // Convert Paradox date to DateTime (days since Dec 31, 1899)
                    DateTime matchDate = DateTime.MinValue;
                    if (dateValue > 30000 && dateValue < 50000) // Reasonable date range (1982-2036)
                    {
                        try
                        {
                            matchDate = new DateTime(1899, 12, 31).AddDays(dateValue);
                        }
                        catch
                        {
                            // Invalid date, skip
                            continue;
                        }
                    }
                    else
                    {
                        // Try reading as 2-byte value
                        if (dateOffset + 1 < content.Length)
                        {
                            int dateValue2 = content[dateOffset] | (content[dateOffset + 1] << 8);
                            if (dateValue2 > 30000 && dateValue2 < 50000)
                            {
                                try
                                {
                                    matchDate = new DateTime(1899, 12, 31).AddDays(dateValue2);
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                        }
                    }
                    
                    // Skip if we couldn't parse a valid date
                    if (matchDate == DateTime.MinValue || matchDate.Year < 2000 || matchDate.Year > 2030)
                        continue;
                    
                    // Try to extract team IDs (bytes further back)
                    int teamOffset = dateOffset - 6;
                    if (teamOffset < 0) continue;
                    
                    int homeTeam = content[teamOffset];
                    int awayTeam = content[teamOffset + 2];
                    
                    // Validate team IDs (should be small positive integers, 1-50 typically)
                    if (homeTeam < 1 || homeTeam > 50 || awayTeam < 1 || awayTeam > 50)
                        continue;
                    if (homeTeam == awayTeam)
                        continue;
                    
                    // Try to extract scores
                    int homeWins = 0, awayWins = 0;
                    int scorePos = pos - 12;
                    if (scorePos > 0 && scorePos + 4 < content.Length)
                    {
                        homeWins = content[scorePos];
                        awayWins = content[scorePos + 2];
                        
                        // Validate scores (0-15 range typically)
                        if (homeWins > 15) homeWins = 0;
                        if (awayWins > 15) awayWins = 0;
                    }
                    
                    // Create unique key for deduplication
                    var matchKey = $"{matchDate:yyyyMMdd}|{homeTeam}|{awayTeam}";
                    if (seenMatches.Contains(matchKey))
                        continue;
                    seenMatches.Add(matchKey);
                    
                    var paradoxMatch = new ParadoxMatch
                    {
                        MatchNo = matchNo++,
                        HomeTeam = homeTeam,
                        AwayTeam = awayTeam,
                        MatchDate = matchDate,
                        HomeSinglesWins = homeWins,
                        AwaySinglesWins = awayWins,
                        DivisionName = div
                    };
                    
                    matches.Add(paradoxMatch);
                    System.Diagnostics.Debug.WriteLine($"Found match: {matchDate:dd/MM/yyyy} Team {homeTeam} vs Team {awayTeam} ({div})");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing match record at pos {pos}: {ex.Message}");
                }
            }
            
            result.Warnings.Add($"Parsed {matches.Count} matches from Match.DB");
            
            // If we couldn't parse structured data, at least report what we found
            if (matches.Count == 0)
            {
                result.Warnings.Add($"Found {recordPositions.Count} division markers but couldn't parse match records");
                result.Warnings.Add($"Division breakdown: Premier={recordPositions.Count(r => r.division == "Premier")}, " +
                                   $"One={recordPositions.Count(r => r.division == "One")}, " +
                                   $"Two={recordPositions.Count(r => r.division == "Two")}");
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Error parsing Match.DB: {ex.Message}");
        }

        return matches;
    }

    /// <summary>
    /// Parse Single.DB file (individual frame results)
    /// Schema: MatchNo, SingleNo, HomePlayerNo, AwayPlayerNo, Winner, EightBall
    /// </summary>
    private static List<ParadoxSingle> ParseSingleDb(string filePath, ParadoxParseResult result)
    {
        var singles = new List<ParadoxSingle>();

        try
        {
            var content = File.ReadAllBytes(filePath);
            
            // Count "Home" and "Away" occurrences to estimate frame count
            var homeCount = CountOccurrences(content, "Home");
            var awayCount = CountOccurrences(content, "Away");
            
            result.Warnings.Add($"Single.DB contains approximately {homeCount + awayCount} frame results (Home: {homeCount}, Away: {awayCount})");
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Error parsing Single.DB: {ex.Message}");
        }

        return singles;
    }

    /// <summary>
    /// Parse Venue.DB file
    /// Venues are pubs/clubs - they typically start with "The" or end with common venue words
    /// </summary>
    private static List<ParadoxVenue> ParseVenueDb(string filePath, ParadoxParseResult result)
    {
        var venues = new List<ParadoxVenue>();

        try
        {
            var content = File.ReadAllBytes(filePath);
            var textSegments = ExtractTextSegments(content, 5, 50);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Common venue name patterns (pubs, clubs, etc.)
            var venueWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "arms", "inn", "hotel", "club", "tavern", "bar", "pub", "house",
                "mow", "ball", "bell", "wheel", "hart", "lion", "bear", "globe",
                "dolphin", "ship", "cottage", "vintage", "victoria", "prince",
                "weavers", "sportsmans", "sanford", "ayshford", "cups"
            };

            // Address indicators to exclude
            var addressWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "road", "rd", "street", "st", "lane", "ln", "close", "way", "drive",
                "avenue", "ave", "terrace", "moor", "somerset", "devon", "wellington",
                "taunton", "bristol", "exeter", "nr", "ta21", "ta4"
            };

            bool LooksLikeVenueName(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return false;
                if (text.Length < 5) return false;
                
                var lower = text.ToLowerInvariant();
                var words = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                // Exclude if it looks like an address
                if (words.Any(w => addressWords.Contains(w))) return false;
                if (char.IsDigit(text[0])) return false;
                
                // Must start with "The" or "R" (R.G.W.M) or contain venue words
                if (text.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
                    return true;
                
                if (text.StartsWith("R.") || text.StartsWith("R "))
                    return true;
                
                // Contains common venue words
                if (words.Any(w => venueWords.Contains(w)))
                    return true;
                
                // Ends with common venue suffixes
                if (lower.EndsWith(" cc") || lower.EndsWith(" c.c") || 
                    lower.EndsWith(" club") || lower.EndsWith(" inn") ||
                    lower.EndsWith(" arms") || lower.EndsWith(" mow"))
                    return true;
                
                return false;
            }

            foreach (var segment in textSegments)
            {
                var name = CleanString(segment);
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (name.Contains("resttemp")) continue;
                if (name.Contains("a_Venue")) continue;
                if (name.Contains("ascii")) continue;
                if (name.Contains("Item_id")) continue;
                if (name.Contains("AddressLine")) continue;
                if (name.Contains("UniRef")) continue;
                
                if (!LooksLikeVenueName(name)) continue;
                
                // Normalize venue name
                var normalizedName = NormalizeNameCasing(name);
                
                if (!seenNames.Contains(normalizedName))
                {
                    seenNames.Add(normalizedName);
                    venues.Add(new ParadoxVenue
                    {
                        ItemId = venues.Count + 1,
                        VenueName = normalizedName
                    });
                    System.Diagnostics.Debug.WriteLine($"Found venue: {normalizedName}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Error parsing Venue.DB: {ex.Message}");
        }

        return venues;
    }

    /// <summary>
    /// Extract text segments from binary Paradox data
    /// </summary>
    private static List<string> ExtractTextSegments(byte[] data, int minLength, int maxLength)
    {
        var segments = new List<string>();
        var currentSegment = new StringBuilder();

        foreach (var b in data)
        {
            // Printable ASCII range
            if (b >= 32 && b < 127)
            {
                currentSegment.Append((char)b);
            }
            else
            {
                if (currentSegment.Length >= minLength && currentSegment.Length <= maxLength)
                {
                    segments.Add(currentSegment.ToString());
                }
                currentSegment.Clear();
            }
        }

        // Don't forget the last segment
        if (currentSegment.Length >= minLength && currentSegment.Length <= maxLength)
        {
            segments.Add(currentSegment.ToString());
        }

        return segments;
    }

    /// <summary>
    /// Count occurrences of a string in binary data
    /// </summary>
    private static int CountOccurrences(byte[] data, string search)
    {
        var searchBytes = Encoding.ASCII.GetBytes(search);
        int count = 0;
        
        for (int i = 0; i <= data.Length - searchBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < searchBytes.Length; j++)
            {
                if (data[i + j] != searchBytes[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) count++;
        }

        return count;
    }

    /// <summary>
    /// Extract Paradox records from binary data (simplified)
    /// </summary>
    private static List<byte[]> ExtractParadoxRecords(byte[] data)
    {
        // Simplified record extraction - Paradox files have a header followed by fixed-size records
        var records = new List<byte[]>();
        // This is a simplified implementation - full Paradox parsing would require understanding the complete file format
        return records;
    }

    /// <summary>
    /// Parse fields from a record
    /// </summary>
    private static List<object?> ParseRecordFields(byte[] record, string[] fieldTypes)
    {
        var fields = new List<object?>();
        // Simplified field parsing
        return fields;
    }

    /// <summary>
    /// Clean a string extracted from binary data
    /// </summary>
    private static string CleanString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        
        // Remove null bytes and trim
        s = s.Replace("\0", "").Trim();
        
        // Remove any non-printable characters
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (c >= 32 && c < 127)
                sb.Append(c);
        }
        
        return sb.ToString().Trim();
    }
}
