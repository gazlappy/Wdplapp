using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Wdpl2.Services;

/// <summary>
/// Parser for Paradox database files (.DB format)
/// Used to import legacy pool league data from old Paradox-based systems (Delphi application)
/// 
/// Based on deep analysis of actual Paradox 7.x files:
/// - Header in block 0 (2048 bytes)
/// - Field types at offset 78
/// - Field sizes at offset 78 + numFields  
/// - Field names as null-terminated strings after offset ~200
/// - Data starts at block 1 (offset 2048)
/// - Each block has 6-byte header before records
/// </summary>
public static class ParadoxDatabaseParser
{
    private const int BLOCK_SIZE = 2048;
    private const int DATA_START = 2048; // Block 1
    private const int BLOCK_HEADER_SIZE = 6;

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
        public List<ParadoxDouble> Doubles { get; set; } = new();
        public List<ParadoxVenue> Venues { get; set; } = new();
        public List<ParadoxDateRate> DateRates { get; set; } = new();
        
        // Raw diagnostic data
        public string DiagnosticReport { get; set; } = "";
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
        public bool IsComplete { get; set; }
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

    public class ParadoxDouble
    {
        public int MatchNo { get; set; }
        public int DoubleNo { get; set; }
        public int HomePlayer1No { get; set; }
        public int HomePlayer2No { get; set; }
        public int AwayPlayer1No { get; set; }
        public int AwayPlayer2No { get; set; }
        public string Winner { get; set; } = ""; // "Home" or "Away"
        public bool EightBall1 { get; set; }
        public bool EightBall2 { get; set; }
    }

    public class ParadoxVenue
    {
        public int ItemId { get; set; }
        public string VenueName { get; set; } = "";
        public string Address { get; set; } = "";
        public string AddressLine1 { get; set; } = "";
        public string AddressLine2 { get; set; } = "";
        public string AddressLine3 { get; set; } = "";
        public string AddressLine4 { get; set; } = "";
    }

    /// <summary>
    /// Per-frame rating data from the VBA DateRate table.
    /// Each row represents one player's rating contribution for one frame.
    /// </summary>
    public class ParadoxDateRate
    {
        public int DateRateKey { get; set; }
        public int PlayerNo { get; set; }
        public bool Won { get; set; }
        public int AgainstPlayerNo { get; set; }
        public int Rating { get; set; }
        public DateTime RatingDate { get; set; }
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

            // Generate diagnostic report
            result.DiagnosticReport = ParadoxDeepDive.AnalyzeAll(folderPath);

            // FIRST: Try to load from CSV files if they exist (more reliable)
            var csvLoaded = TryLoadFromCsvFiles(folderPath, result);
            
            if (csvLoaded)
            {
                result.Success = true;
                result.Warnings.Insert(0, "? Loaded from CSV files (preferred method)");
                return result;
            }

            // Parse binary .DB files
            result.Warnings.Add("Parsing binary .DB files...");

            // Parse Division.DB
            var divisionPath = FindFile(folderPath, "Division.DB");
            if (divisionPath != null)
            {
                result.Divisions = ParseDivisionDb(divisionPath, result);
                result.Warnings.Add($"? Divisions: {result.Divisions.Count} parsed");
            }

            // Parse Venue.DB (need before Team.DB)
            var venuePath = FindFile(folderPath, "Venue.DB");
            if (venuePath != null)
            {
                result.Venues = ParseVenueDb(venuePath, result);
                result.Warnings.Add($"? Venues: {result.Venues.Count} parsed");
            }

            // Parse Team.DB
            var teamPath = FindFile(folderPath, "Team.DB");
            if (teamPath != null)
            {
                result.Teams = ParseTeamDb(teamPath, result);
                result.Warnings.Add($"? Teams: {result.Teams.Count} parsed");
            }

            // Parse Player.DB
            var playerPath = FindFile(folderPath, "Player.DB");
            if (playerPath != null)
            {
                result.Players = ParsePlayerDb(playerPath, result);
                result.Warnings.Add($"? Players: {result.Players.Count} parsed");
            }

            // Parse Match.DB
            var matchPath = FindFile(folderPath, "Match.DB");
            if (matchPath != null)
            {
                result.Matches = ParseMatchDb(matchPath, result);
                result.Warnings.Add($"? Matches: {result.Matches.Count} parsed");
            }

            // Parse Single.DB
            var singlePath = FindFile(folderPath, "Single.DB");
            if (singlePath != null)
            {
                result.Singles = ParseSingleDb(singlePath, result);
                result.Warnings.Add($"? Singles: {result.Singles.Count} frames parsed");
            }

            // Parse Dbls.DB
            var dblsPath = FindFile(folderPath, "Dbls.DB");
            if (dblsPath != null)
            {
                result.Doubles = ParseDoublesDb(dblsPath, result);
                result.Warnings.Add($"? Doubles: {result.Doubles.Count} frames parsed");
            }

            // Parse Daterate.DB (per-frame rating data from VBA)
            var dateRatePath = FindFile(folderPath, "Daterate.DB");
            if (dateRatePath != null)
            {
                result.DateRates = ParseDateRateDb(dateRatePath, result);
                result.Warnings.Add($"? DateRates: {result.DateRates.Count} rating entries parsed");
            }

            result.Success = result.Divisions.Count > 0 || result.Teams.Count > 0 || 
                           result.Players.Count > 0 || result.Matches.Count > 0;
                          
            if (!result.Success)
            {
                result.Errors.Add("No data could be parsed from the .DB files.");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Paradox database: {ex.Message}");
        }

        return result;
    }

    #region Binary Parsing

    /// <summary>
    /// Read header info from a Paradox file
    /// </summary>
    private static (int recordSize, int numRecords, int numFields, List<byte> fieldTypes, List<byte> fieldSizes, List<string> fieldNames) 
        ReadParadoxHeader(byte[] bytes)
    {
        var recordSize = BitConverter.ToInt16(bytes, 0);
        var numRecords = BitConverter.ToInt32(bytes, 6);
        var numFields = bytes[33];

        var fieldTypes = new List<byte>();
        var fieldSizes = new List<byte>();
        var fieldNames = new List<string>();

        // Read field types (at offset 78)
        int typeOffset = 78;

        for (int i = 0; i < numFields; i++)
        {
            if (typeOffset + i < bytes.Length)
                fieldTypes.Add(bytes[typeOffset + i]);
        }

        // Field sizes follow types
        for (int i = 0; i < numFields; i++)
        {
            if (typeOffset + numFields + i < bytes.Length)
                fieldSizes.Add(bytes[typeOffset + numFields + i]);
        }

        // Field names start after types+sizes arrays, preceded by the table name.
        // Correct offset: 78 + numFields * 2 (types array + sizes array)
        int nameStart = 78 + numFields * 2;

        // Skip the table name (null-terminated string, e.g. "Player.DB\0")
        while (nameStart < bytes.Length && bytes[nameStart] != 0)
            nameStart++;
        nameStart++; // skip the null terminator

        // Calculate header end using header size and block info
        var headerSize = bytes.Length >= 4 ? BitConverter.ToInt16(bytes, 2) : 1;
        var maxTableSize = bytes.Length > 5 ? bytes[5] : 4;
        int blockSize = maxTableSize * 0x400;
        if (blockSize == 0) blockSize = BLOCK_SIZE;
        int headerEnd = headerSize * blockSize;
        if (headerEnd == 0 || headerEnd > bytes.Length) headerEnd = Math.Min(bytes.Length, DATA_START);

        // Read null-terminated field names
        var currentName = new StringBuilder();
        for (int i = nameStart; i < headerEnd && fieldNames.Count < numFields; i++)
        {
            if (bytes[i] == 0)
            {
                if (currentName.Length > 0)
                {
                    fieldNames.Add(currentName.ToString());
                    currentName.Clear();
                }
            }
            else if (bytes[i] >= 32 && bytes[i] < 127)
            {
                currentName.Append((char)bytes[i]);
            }
        }
        // Capture last name if file ended without a null terminator
        if (currentName.Length > 0 && fieldNames.Count < numFields)
            fieldNames.Add(currentName.ToString());

        return (recordSize, numRecords, numFields, fieldTypes, fieldSizes, fieldNames);
    }

    /// <summary>
    /// Read records from a Paradox file
    /// </summary>
    private static List<Dictionary<string, object?>> ReadRecords(byte[] bytes, int recordSize, int numRecords, 
        List<byte> fieldTypes, List<byte> fieldSizes, List<string> fieldNames)
    {
        var records = new List<Dictionary<string, object?>>();
        
        if (recordSize <= 0 || numRecords <= 0)
            return records;

        // Data starts at block 1 (offset 2048)
        // Each block has a 6-byte header
        int blockSize = BLOCK_SIZE;

        // Calculate records per block
        int recordsPerBlock = (blockSize - BLOCK_HEADER_SIZE) / recordSize;
        if (recordsPerBlock <= 0) recordsPerBlock = 1;

        for (int rec = 0; rec < numRecords; rec++)
        {
            // Calculate which block this record is in
            int blockNum = rec / recordsPerBlock;
            int recInBlock = rec % recordsPerBlock;
            
            // Calculate offset
            int blockStart = DATA_START + (blockNum * blockSize);
            int recOffset = blockStart + BLOCK_HEADER_SIZE + (recInBlock * recordSize);
            
            if (recOffset + recordSize > bytes.Length)
                break;

            var record = new Dictionary<string, object?>();
            int fieldOffset = recOffset;

            for (int f = 0; f < fieldTypes.Count && f < fieldSizes.Count; f++)
            {
                var fType = fieldTypes[f];
                var fSize = fieldSizes[f];
                var fName = f < fieldNames.Count ? fieldNames[f] : $"Field{f + 1}";

                if (fieldOffset + fSize > bytes.Length)
                    break;

                var fieldBytes = new byte[fSize];
                Array.Copy(bytes, fieldOffset, fieldBytes, 0, fSize);

                record[fName] = DecodeField(fieldBytes, fType);
                fieldOffset += fSize;
            }

            records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Decode a field value based on Paradox type
    /// </summary>
    private static object? DecodeField(byte[] bytes, byte fieldType)
    {
        if (bytes.All(b => b == 0))
            return null;

        switch (fieldType)
        {
            case 0x01: // Alpha (string)
                int len = Array.IndexOf(bytes, (byte)0);
                if (len < 0) len = bytes.Length;
                return Encoding.ASCII.GetString(bytes, 0, len).Trim();

            case 0x02: // Date
                if ((bytes[0] & 0x80) == 0x80)
                {
                    int days = ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                    if (days > 0 && days < 3000000)
                    {
                        try { return new DateTime(1, 1, 1).AddDays(days - 1); }
                        catch { return null; }
                    }
                }
                return null;

            case 0x03: // Short
                if ((bytes[0] & 0x80) == 0x80)
                    return (short)(((bytes[0] & 0x7F) << 8) | bytes[1]);
                else if (bytes[0] != 0 || bytes[1] != 0)
                    return (short)-(((bytes[0] ^ 0x7F) << 8) | (bytes[1] ^ 0xFF));
                return (short)0;

            case 0x04: // Long
            case 0x16: // AutoInc
                if ((bytes[0] & 0x80) == 0x80)
                    return ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                else if (bytes.Any(b => b != 0))
                    return -(((bytes[0] ^ 0x7F) << 24) | ((bytes[1] ^ 0xFF) << 16) | ((bytes[2] ^ 0xFF) << 8) | (bytes[3] ^ 0xFF));
                return 0;

            case 0x06: // Number (double)
                var modBytes = new byte[8];
                if ((bytes[0] & 0x80) == 0x80)
                {
                    modBytes[0] = (byte)(bytes[0] ^ 0x80);
                    Array.Copy(bytes, 1, modBytes, 1, 7);
                }
                else
                {
                    for (int i = 0; i < 8; i++)
                        modBytes[i] = (byte)(bytes[i] ^ 0xFF);
                }
                Array.Reverse(modBytes);
                return BitConverter.ToDouble(modBytes, 0);

            case 0x09: // Logical
                return bytes[0] == 0x81;

            default:
                // Try as string
                int strLen = Array.IndexOf(bytes, (byte)0);
                if (strLen < 0) strLen = bytes.Length;
                var str = Encoding.ASCII.GetString(bytes, 0, strLen).Trim();
                return string.IsNullOrEmpty(str) ? null : str;
        }
    }

    private static List<ParadoxDivision> ParseDivisionDb(string filePath, ParadoxParseResult result)
    {
        var divisions = new List<ParadoxDivision>();
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var (recordSize, numRecords, numFields, fieldTypes, fieldSizes, fieldNames) = ReadParadoxHeader(bytes);
            var records = ReadRecords(bytes, recordSize, numRecords, fieldTypes, fieldSizes, fieldNames);

            foreach (var rec in records)
            {
                var div = new ParadoxDivision
                {
                    ItemId = GetInt(rec, "Item_id", "ItemId") ?? divisions.Count + 1,
                    Abbreviated = GetString(rec, "Abbreviated", "Abbrev") ?? "",
                    FullDivisionName = GetString(rec, "FullDivisionName", "DivisionName", "Name") ?? ""
                };

                if (string.IsNullOrWhiteSpace(div.FullDivisionName) && !string.IsNullOrWhiteSpace(div.Abbreviated))
                    div.FullDivisionName = div.Abbreviated;

                // Filter out placeholder entries
                if (!string.IsNullOrWhiteSpace(div.FullDivisionName) && 
                    !div.FullDivisionName.Equals("test", StringComparison.OrdinalIgnoreCase))
                    divisions.Add(div);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"? Error parsing Division.DB: {ex.Message}");
        }
        return divisions;
    }

    private static List<ParadoxVenue> ParseVenueDb(string filePath, ParadoxParseResult result)
    {
        var venues = new List<ParadoxVenue>();
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var (recordSize, numRecords, numFields, fieldTypes, fieldSizes, fieldNames) = ReadParadoxHeader(bytes);
            var records = ReadRecords(bytes, recordSize, numRecords, fieldTypes, fieldSizes, fieldNames);

            foreach (var rec in records)
            {
                var venue = new ParadoxVenue
                {
                    ItemId = GetInt(rec, "Item_id", "ItemId") ?? venues.Count + 1,
                    VenueName = GetString(rec, "Venue", "VenueName", "Name") ?? "",
                    AddressLine1 = GetString(rec, "AddressLine1", "Address1") ?? "",
                    AddressLine2 = GetString(rec, "AddressLine2", "Address2") ?? "",
                    AddressLine3 = GetString(rec, "AddressLine3", "Address3") ?? "",
                    AddressLine4 = GetString(rec, "AddressLine4", "Address4") ?? ""
                };

                // Combine address
                var addressParts = new[] { venue.AddressLine1, venue.AddressLine2, venue.AddressLine3, venue.AddressLine4 }
                    .Where(a => !string.IsNullOrWhiteSpace(a));
                venue.Address = string.Join(", ", addressParts);

                if (!string.IsNullOrWhiteSpace(venue.VenueName))
                    venues.Add(venue);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"? Error parsing Venue.DB: {ex.Message}");
        }
        return venues;
    }

    private static List<ParadoxTeam> ParseTeamDb(string filePath, ParadoxParseResult result)
    {
        var teams = new List<ParadoxTeam>();
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var (recordSize, numRecords, numFields, fieldTypes, fieldSizes, fieldNames) = ReadParadoxHeader(bytes);
            var records = ReadRecords(bytes, recordSize, numRecords, fieldTypes, fieldSizes, fieldNames);

            foreach (var rec in records)
            {
                var team = new ParadoxTeam
                {
                    ItemId = GetInt(rec, "Item_id", "ItemId") ?? teams.Count + 1,
                    TeamName = GetString(rec, "TeamName", "Name") ?? "",
                    VenueId = GetInt(rec, "Venue", "VenueId"),
                    DivisionId = GetInt(rec, "Division", "DivisionId"),
                    Contact = GetString(rec, "Contact") ?? "",
                    ContactAddress1 = GetString(rec, "ContactAddress1") ?? "",
                    ContactAddress2 = GetString(rec, "ContactAddress2") ?? "",
                    ContactAddress3 = GetString(rec, "ContactAddress3") ?? "",
                    ContactAddress4 = GetString(rec, "ContactAddress4") ?? "",
                    Wins = GetInt(rec, "Wins") ?? 0,
                    Losses = GetInt(rec, "Loses", "Losses") ?? 0,
                    Draws = GetInt(rec, "Draws") ?? 0,
                    SinglesWins = GetInt(rec, "SWins", "SinglesWins") ?? 0,
                    SinglesLosses = GetInt(rec, "SLosses", "SinglesLosses") ?? 0,
                    DoublesWins = GetInt(rec, "DWins", "DoublesWins") ?? 0,
                    DoublesLosses = GetInt(rec, "DLosses", "DoublesLosses") ?? 0,
                    Points = GetInt(rec, "Points") ?? 0,
                    Played = GetInt(rec, "Played") ?? 0,
                    Withdrawn = GetBool(rec, "Withdrawn"),
                    RemoveResults = GetBool(rec, "RemoveResults")
                };

                if (!string.IsNullOrWhiteSpace(team.TeamName))
                    teams.Add(team);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"? Error parsing Team.DB: {ex.Message}");
        }
        return teams;
    }

    private static List<ParadoxPlayer> ParsePlayerDb(string filePath, ParadoxParseResult result)
    {
        var players = new List<ParadoxPlayer>();
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var (recordSize, numRecords, numFields, fieldTypes, fieldSizes, fieldNames) = ReadParadoxHeader(bytes);
            var records = ReadRecords(bytes, recordSize, numRecords, fieldTypes, fieldSizes, fieldNames);

            foreach (var rec in records)
            {
                var name = GetString(rec, "PlayerName", "Name") ?? "";
                
                // Skip void/placeholder entries
                if (string.IsNullOrWhiteSpace(name) || 
                    name.Equals("Void Frame", StringComparison.OrdinalIgnoreCase))
                    continue;

                var nameParts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                var player = new ParadoxPlayer
                {
                    PlayerNo = GetInt(rec, "PlayerNo", "Id") ?? players.Count + 1,
                    PlayerName = name,
                    FirstName = nameParts.FirstOrDefault() ?? "",
                    LastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "",
                    PlayerTeam = GetInt(rec, "PlayerTeam", "Team", "TeamId"),
                    Played = GetInt(rec, "Played") ?? 0,
                    Wins = GetInt(rec, "Wins") ?? 0,
                    Losses = GetInt(rec, "Losses") ?? 0,
                    CurrentRating = GetInt(rec, "CurrentRating", "Rating"),
                    BestRating = GetInt(rec, "BestRating"),
                    BestRatingDate = GetDate(rec, "BestRatingDate"),
                    EightBalls = GetInt(rec, "EightBalls", "8Balls") ?? 0
                };

                players.Add(player);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"? Error parsing Player.DB: {ex.Message}");
        }
        return players;
    }

    private static List<ParadoxMatch> ParseMatchDb(string filePath, ParadoxParseResult result)
    {
        var matches = new List<ParadoxMatch>();
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var (recordSize, numRecords, numFields, fieldTypes, fieldSizes, fieldNames) = ReadParadoxHeader(bytes);
            var records = ReadRecords(bytes, recordSize, numRecords, fieldTypes, fieldSizes, fieldNames);

            foreach (var rec in records)
            {
                var homeTeam = GetInt(rec, "HomeTeam", "Home") ?? 0;
                var awayTeam = GetInt(rec, "AwayTeam", "Away") ?? 0;
                
                if (homeTeam == 0 || awayTeam == 0)
                    continue;

                var match = new ParadoxMatch
                {
                    MatchNo = GetInt(rec, "MatchNo", "Id") ?? matches.Count + 1,
                    HomeTeam = homeTeam,
                    AwayTeam = awayTeam,
                    MatchDate = GetDate(rec, "MatchDate", "Date") ?? DateTime.MinValue,
                    HomeSinglesWins = GetInt(rec, "HSWins", "HomeSinglesWins") ?? 0,
                    AwaySinglesWins = GetInt(rec, "ASWins", "AwaySinglesWins") ?? 0,
                    HomeDoublesWins = GetInt(rec, "HDWins", "HomeDoublesWins") ?? 0,
                    AwayDoublesWins = GetInt(rec, "ADWins", "AwayDoublesWins") ?? 0,
                    DivisionName = GetString(rec, "DivName", "Division") ?? ""
                };

                match.IsComplete = match.HomeSinglesWins > 0 || match.AwaySinglesWins > 0;
                matches.Add(match);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"? Error parsing Match.DB: {ex.Message}");
        }
        return matches;
    }

    private static List<ParadoxSingle> ParseSingleDb(string filePath, ParadoxParseResult result)
    {
        var singles = new List<ParadoxSingle>();
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var (recordSize, numRecords, numFields, fieldTypes, fieldSizes, fieldNames) = ReadParadoxHeader(bytes);
            var records = ReadRecords(bytes, recordSize, numRecords, fieldTypes, fieldSizes, fieldNames);

            foreach (var rec in records)
            {
                var matchNo = GetInt(rec, "MatchNo", "Match") ?? 0;
                var winner = GetString(rec, "Winner") ?? "";
                
                if (matchNo == 0 || string.IsNullOrWhiteSpace(winner))
                    continue;

                var single = new ParadoxSingle
                {
                    MatchNo = matchNo,
                    SingleNo = GetInt(rec, "SingleNo", "FrameNo", "Frame") ?? singles.Count + 1,
                    HomePlayerNo = GetInt(rec, "HomePlayerNo", "HomePlayer") ?? 0,
                    AwayPlayerNo = GetInt(rec, "AwayPlayerNo", "AwayPlayer") ?? 0,
                    Winner = winner,
                    EightBall = GetBool(rec, "EightBall", "8Ball")
                };

                singles.Add(single);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"? Error parsing Single.DB: {ex.Message}");
        }
        return singles;
    }

    private static List<ParadoxDouble> ParseDoublesDb(string filePath, ParadoxParseResult result)
    {
        var doubles = new List<ParadoxDouble>();
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var (recordSize, numRecords, numFields, fieldTypes, fieldSizes, fieldNames) = ReadParadoxHeader(bytes);
            var records = ReadRecords(bytes, recordSize, numRecords, fieldTypes, fieldSizes, fieldNames);

            foreach (var rec in records)
            {
                var matchNo = GetInt(rec, "MatchNo", "Match") ?? 0;
                var winner = GetString(rec, "Winner") ?? "";
                
                if (matchNo == 0 || string.IsNullOrWhiteSpace(winner))
                    continue;

                var dbl = new ParadoxDouble
                {
                    MatchNo = matchNo,
                    DoubleNo = GetInt(rec, "DblNo", "DoubleNo", "FrameNo") ?? doubles.Count + 1,
                    HomePlayer1No = GetInt(rec, "HomePlayer1No", "HP1") ?? 0,
                    HomePlayer2No = GetInt(rec, "HomePlayer2No", "HP2") ?? 0,
                    AwayPlayer1No = GetInt(rec, "AwayPlayer1No", "AP1") ?? 0,
                    AwayPlayer2No = GetInt(rec, "AwayPlayer2No", "AP2") ?? 0,
                    Winner = winner,
                    EightBall1 = GetBool(rec, "EightBall1", "8Ball1"),
                    EightBall2 = GetBool(rec, "EightBall2", "8Ball2")
                };

                doubles.Add(dbl);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"? Error parsing Dbls.DB: {ex.Message}");
        }
        return doubles;
    }

    private static List<ParadoxDateRate> ParseDateRateDb(string filePath, ParadoxParseResult result)
    {
        var dateRates = new List<ParadoxDateRate>();
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var (recordSize, numRecords, numFields, fieldTypes, fieldSizes, fieldNames) = ReadParadoxHeader(bytes);
            var records = ReadRecords(bytes, recordSize, numRecords, fieldTypes, fieldSizes, fieldNames);

            foreach (var rec in records)
            {
                var playerNo = GetInt(rec, "PlayerNo") ?? 0;
                if (playerNo == 0) continue;

                var dateRate = new ParadoxDateRate
                {
                    DateRateKey = GetInt(rec, "DateRateKey", "Key", "Id") ?? dateRates.Count + 1,
                    PlayerNo = playerNo,
                    Won = GetBool(rec, "Won"),
                    AgainstPlayerNo = GetInt(rec, "Against") ?? 0,
                    Rating = GetInt(rec, "Rating") ?? 0,
                    RatingDate = GetDate(rec, "RatingDate", "Date") ?? DateTime.MinValue
                };

                if (dateRate.Rating > 0)
                    dateRates.Add(dateRate);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"? Error parsing Daterate.DB: {ex.Message}");
        }
        return dateRates;
    }

    #endregion

    #region Helper Methods

    private static int? GetInt(Dictionary<string, object?> rec, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = rec.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match != null && rec[match] != null)
            {
                var val = rec[match];
                if (val is int i) return i;
                if (val is short s) return s;
                if (val is long l) return (int)l;
                if (val is double d) return (int)d;
                if (int.TryParse(val?.ToString(), out var parsed)) return parsed;
            }
        }
        return null;
    }

    private static string? GetString(Dictionary<string, object?> rec, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = rec.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match != null && rec[match] != null)
            {
                var str = rec[match]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(str))
                    return str;
            }
        }
        return null;
    }

    private static DateTime? GetDate(Dictionary<string, object?> rec, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = rec.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match != null && rec[match] != null)
            {
                if (rec[match] is DateTime dt) return dt;
                if (DateTime.TryParse(rec[match]?.ToString(), out var parsed)) return parsed;
            }
        }
        return null;
    }

    private static bool GetBool(Dictionary<string, object?> rec, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = rec.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match != null && rec[match] != null)
            {
                if (rec[match] is bool b) return b;
                var str = rec[match]?.ToString()?.Trim().ToLowerInvariant();
                if (str == "1" || str == "true" || str == "yes") return true;
            }
        }
        return false;
    }

    private static string? FindFile(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path)) return path;

        var files = Directory.GetFiles(folderPath, "*.DB", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            if (Path.GetFileName(file).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return file;
        }

        return null;
    }

    #endregion

    #region CSV Loading

    private static bool TryLoadFromCsvFiles(string folderPath, ParadoxParseResult result)
    {
        bool foundAny = false;

        var csvFiles = Directory.GetFiles(folderPath, "*.csv", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(folderPath, "*.CSV", SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (csvFiles.Count == 0)
            return false;

        foreach (var csvFile in csvFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(csvFile).ToUpperInvariant();
            
            try
            {
                if (fileName.Contains("DIVISION"))
                {
                    result.Divisions = LoadDivisionsFromCsv(csvFile);
                    if (result.Divisions.Count != 0) foundAny = true;
                }
                else if (fileName.Contains("TEAM") && !fileName.Contains("PLAYER"))
                {
                    result.Teams = LoadTeamsFromCsv(csvFile);
                    if (result.Teams.Count != 0) foundAny = true;
                }
                else if (fileName.Contains("PLAYER"))
                {
                    result.Players = LoadPlayersFromCsv(csvFile);
                    if (result.Players.Count != 0) foundAny = true;
                }
                else if (fileName.Contains("MATCH"))
                {
                    result.Matches = LoadMatchesFromCsv(csvFile);
                    if (result.Matches.Count != 0) foundAny = true;
                }
                else if (fileName.Contains("SINGLE"))
                {
                    result.Singles = LoadSinglesFromCsv(csvFile);
                    if (result.Singles.Count != 0) foundAny = true;
                }
                else if (fileName.Contains("VENUE"))
                {
                    result.Venues = LoadVenuesFromCsv(csvFile);
                    if (result.Venues.Count != 0) foundAny = true;
                }
                else if (fileName.Contains("DATERATE") || fileName.Contains("DATE_RATE"))
                {
                    result.DateRates = LoadDateRatesFromCsv(csvFile);
                    if (result.DateRates.Count != 0) foundAny = true;
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"? Error loading CSV {Path.GetFileName(csvFile)}: {ex.Message}");
            }
        }

        return foundAny;
    }

    private static List<ParadoxDivision> LoadDivisionsFromCsv(string filePath)
    {
        var divisions = new List<ParadoxDivision>();
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return divisions;

        for (int i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count >= 2)
            {
                divisions.Add(new ParadoxDivision
                {
                    ItemId = i,
                    Abbreviated = fields.Count > 1 ? fields[1] : "",
                    FullDivisionName = fields.Count > 2 ? fields[2] : fields[1]
                });
            }
        }
        return divisions;
    }

    private static List<ParadoxTeam> LoadTeamsFromCsv(string filePath)
    {
        var teams = new List<ParadoxTeam>();
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return teams;

        // Parse header to find columns by name
        var header = ParseCsvLine(lines[0]);
        int IdxOf(params string[] names) {
            for (int c = 0; c < header.Count; c++)
                foreach (var n in names)
                    if (header[c].Equals(n, StringComparison.OrdinalIgnoreCase)) return c;
            return -1;
        }
        string Col(List<string> f, int idx) => idx >= 0 && idx < f.Count ? f[idx] : "";
        int? IntCol(List<string> f, int idx) => idx >= 0 && idx < f.Count && int.TryParse(f[idx], out var v) ? v : null;

        var idCol = IdxOf("Id", "Item_id", "ItemId");
        var nameCol = IdxOf("TeamName", "Name");
        var venueCol = IdxOf("VenueId", "Venue");
        var divCol = IdxOf("DivisionId", "Division");
        var contactCol = IdxOf("Contact", "Captain");
        var winsCol = IdxOf("Wins");
        var lossesCol = IdxOf("Losses", "Loses");
        var pointsCol = IdxOf("Points");

        // Fallback: if no header match, use positional (Id, TeamName)
        if (nameCol < 0) nameCol = header.Count > 1 ? 1 : 0;
        if (idCol < 0) idCol = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count < 2) continue;

            var name = Col(fields, nameCol);
            if (string.IsNullOrWhiteSpace(name)) continue;

            teams.Add(new ParadoxTeam
            {
                ItemId = IntCol(fields, idCol) ?? i,
                TeamName = name,
                VenueId = IntCol(fields, venueCol),
                DivisionId = IntCol(fields, divCol),
                Contact = Col(fields, contactCol),
                Wins = IntCol(fields, winsCol) ?? 0,
                Losses = IntCol(fields, lossesCol) ?? 0,
                Points = IntCol(fields, pointsCol) ?? 0
            });
        }
        return teams;
    }

    private static List<ParadoxPlayer> LoadPlayersFromCsv(string filePath)
    {
        var players = new List<ParadoxPlayer>();
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return players;

        var header = ParseCsvLine(lines[0]);
        int IdxOf(params string[] names) {
            for (int c = 0; c < header.Count; c++)
                foreach (var n in names)
                    if (header[c].Equals(n, StringComparison.OrdinalIgnoreCase)) return c;
            return -1;
        }
        int? IntCol(List<string> f, int idx) => idx >= 0 && idx < f.Count && int.TryParse(f[idx], out var v) ? v : null;
        string Col(List<string> f, int idx) => idx >= 0 && idx < f.Count ? f[idx] : "";

        var idCol = IdxOf("Id", "PlayerNo");
        var nameCol = IdxOf("PlayerName", "Name");
        var teamCol = IdxOf("TeamId", "Team", "PlayerTeam");
        var winsCol = IdxOf("Wins");
        var lossesCol = IdxOf("Losses");
        var ratingCol = IdxOf("Rating", "CurrentRating");

        if (nameCol < 0) nameCol = header.Count > 1 ? 1 : 0;
        if (idCol < 0) idCol = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count < 2) continue;

            var name = Col(fields, nameCol);
            if (string.IsNullOrWhiteSpace(name)) continue;

            var nameParts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            players.Add(new ParadoxPlayer
            {
                PlayerNo = IntCol(fields, idCol) ?? i,
                PlayerName = name,
                FirstName = nameParts.FirstOrDefault() ?? "",
                LastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "",
                PlayerTeam = IntCol(fields, teamCol),
                Wins = IntCol(fields, winsCol) ?? 0,
                Losses = IntCol(fields, lossesCol) ?? 0,
                CurrentRating = IntCol(fields, ratingCol)
            });
        }
        return players;
    }

    private static List<ParadoxMatch> LoadMatchesFromCsv(string filePath)
    {
        var matches = new List<ParadoxMatch>();
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return matches;

        var header = ParseCsvLine(lines[0]);
        int IdxOf(params string[] names) {
            for (int c = 0; c < header.Count; c++)
                foreach (var n in names)
                    if (header[c].Equals(n, StringComparison.OrdinalIgnoreCase)) return c;
            return -1;
        }
        int? IntCol(List<string> f, int idx) => idx >= 0 && idx < f.Count && int.TryParse(f[idx], out var v) ? v : null;
        string Col(List<string> f, int idx) => idx >= 0 && idx < f.Count ? f[idx] : "";

        var idCol = IdxOf("Id", "MatchNo");
        var homeCol = IdxOf("HomeTeam", "Home");
        var awayCol = IdxOf("AwayTeam", "Away");
        var dateCol = IdxOf("Date", "MatchDate");
        var hsCol = IdxOf("HSWins", "HomeSinglesWins");
        var asCol = IdxOf("ASWins", "AwaySinglesWins");
        var hdCol = IdxOf("HDWins", "HomeDoublesWins");
        var adCol = IdxOf("ADWins", "AwayDoublesWins");
        var divCol = IdxOf("Division", "DivName");

        // Fallback positional: Id, HomeTeam, AwayTeam, Date
        if (idCol < 0) idCol = 0;
        if (homeCol < 0) homeCol = 1;
        if (awayCol < 0) awayCol = 2;
        if (dateCol < 0) dateCol = 3;

        for (int i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count < 4) continue;

            var home = IntCol(fields, homeCol) ?? 0;
            var away = IntCol(fields, awayCol) ?? 0;
            if (home == 0 || away == 0) continue;

            matches.Add(new ParadoxMatch
            {
                MatchNo = IntCol(fields, idCol) ?? i,
                HomeTeam = home,
                AwayTeam = away,
                MatchDate = DateTime.TryParse(Col(fields, dateCol), out var d) ? d : DateTime.MinValue,
                HomeSinglesWins = IntCol(fields, hsCol) ?? 0,
                AwaySinglesWins = IntCol(fields, asCol) ?? 0,
                HomeDoublesWins = IntCol(fields, hdCol) ?? 0,
                AwayDoublesWins = IntCol(fields, adCol) ?? 0,
                DivisionName = Col(fields, divCol)
            });
        }
        return matches;
    }

    private static List<ParadoxSingle> LoadSinglesFromCsv(string filePath)
    {
        var singles = new List<ParadoxSingle>();
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return singles;

        for (int i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count >= 5)
            {
                singles.Add(new ParadoxSingle
                {
                    MatchNo = int.TryParse(fields[0], out var m) ? m : 0,
                    SingleNo = int.TryParse(fields[1], out var s) ? s : i,
                    HomePlayerNo = int.TryParse(fields[2], out var h) ? h : 0,
                    AwayPlayerNo = int.TryParse(fields[3], out var a) ? a : 0,
                    Winner = fields[4]
                });
            }
        }
        return singles;
    }

    private static List<ParadoxVenue> LoadVenuesFromCsv(string filePath)
    {
        var venues = new List<ParadoxVenue>();
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return venues;

        for (int i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count >= 2)
            {
                venues.Add(new ParadoxVenue
                {
                    ItemId = int.TryParse(fields[0], out var id) ? id : i,
                    VenueName = fields.Count > 1 ? fields[1] : ""
                });
            }
        }
        return venues;
    }

    private static List<ParadoxDateRate> LoadDateRatesFromCsv(string filePath)
    {
        var dateRates = new List<ParadoxDateRate>();
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return dateRates;

        var header = ParseCsvLine(lines[0]);
        int IdxOf(params string[] names) {
            for (int c = 0; c < header.Count; c++)
                foreach (var n in names)
                    if (header[c].Equals(n, StringComparison.OrdinalIgnoreCase)) return c;
            return -1;
        }
        int? IntCol(List<string> f, int idx) => idx >= 0 && idx < f.Count && int.TryParse(f[idx], out var v) ? v : null;
        string Col(List<string> f, int idx) => idx >= 0 && idx < f.Count ? f[idx] : "";

        var keyCol = IdxOf("DateRateKey", "Key", "Id");
        var playerCol = IdxOf("PlayerNo", "Player");
        var wonCol = IdxOf("Won");
        var againstCol = IdxOf("Against", "AgainstPlayerNo");
        var ratingCol = IdxOf("Rating");
        var dateCol = IdxOf("RatingDate", "Date");

        if (playerCol < 0) playerCol = 1;
        if (ratingCol < 0) ratingCol = 4;

        for (int i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count < 3) continue;

            var playerNo = IntCol(fields, playerCol) ?? 0;
            var rating = IntCol(fields, ratingCol) ?? 0;
            if (playerNo == 0 || rating == 0) continue;

            var wonStr = Col(fields, wonCol).ToLowerInvariant();

            dateRates.Add(new ParadoxDateRate
            {
                DateRateKey = IntCol(fields, keyCol) ?? i,
                PlayerNo = playerNo,
                Won = wonStr == "1" || wonStr == "true" || wonStr == "yes",
                AgainstPlayerNo = IntCol(fields, againstCol) ?? 0,
                Rating = rating,
                RatingDate = DateTime.TryParse(Col(fields, dateCol), out var d) ? d : DateTime.MinValue
            });
        }
        return dateRates;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes) { fields.Add(current.ToString().Trim()); current.Clear(); }
            else current.Append(c);
        }
        fields.Add(current.ToString().Trim());
        return fields;
    }

    #endregion

    #region CSV Export

    public static string ExportToCsv(ParadoxParseResult parseResult, string outputFolder)
    {
        var exportedFiles = new List<string>();
        Directory.CreateDirectory(outputFolder);

        if (parseResult.Divisions.Count != 0)
        {
            var file = Path.Combine(outputFolder, "Division_Export.csv");
            var sb = new StringBuilder("Id,Abbreviated,FullDivisionName\n");
            foreach (var d in parseResult.Divisions)
                sb.AppendLine($"{d.ItemId},{Esc(d.Abbreviated)},{Esc(d.FullDivisionName)}");
            File.WriteAllText(file, sb.ToString());
            exportedFiles.Add("Division_Export.csv");
        }

        if (parseResult.Teams.Count != 0)
        {
            var file = Path.Combine(outputFolder, "Team_Export.csv");
            var sb = new StringBuilder("Id,TeamName,VenueId,DivisionId,Contact,Wins,Losses,Points\n");
            foreach (var t in parseResult.Teams)
                sb.AppendLine($"{t.ItemId},{Esc(t.TeamName)},{t.VenueId},{t.DivisionId},{Esc(t.Contact)},{t.Wins},{t.Losses},{t.Points}");
            File.WriteAllText(file, sb.ToString());
            exportedFiles.Add("Team_Export.csv");
        }

        if (parseResult.Players.Count != 0)
        {
            var file = Path.Combine(outputFolder, "Player_Export.csv");
            var sb = new StringBuilder("Id,PlayerName,TeamId,Wins,Losses,Rating\n");
            foreach (var p in parseResult.Players)
                sb.AppendLine($"{p.PlayerNo},{Esc(p.PlayerName)},{p.PlayerTeam},{p.Wins},{p.Losses},{p.CurrentRating}");
            File.WriteAllText(file, sb.ToString());
            exportedFiles.Add("Player_Export.csv");
        }

        if (parseResult.Matches.Count != 0)
        {
            var file = Path.Combine(outputFolder, "Match_Export.csv");
            var sb = new StringBuilder("Id,HomeTeam,AwayTeam,Date,HSWins,ASWins,HDWins,ADWins,Division\n");
            foreach (var m in parseResult.Matches)
                sb.AppendLine($"{m.MatchNo},{m.HomeTeam},{m.AwayTeam},{m.MatchDate:yyyy-MM-dd},{m.HomeSinglesWins},{m.AwaySinglesWins},{m.HomeDoublesWins},{m.AwayDoublesWins},{Esc(m.DivisionName)}");
            File.WriteAllText(file, sb.ToString());
            exportedFiles.Add("Match_Export.csv");
        }

        if (parseResult.Singles.Count != 0)
        {
            var file = Path.Combine(outputFolder, "Single_Export.csv");
            var sb = new StringBuilder("MatchNo,FrameNo,HomePlayer,AwayPlayer,Winner,EightBall\n");
            foreach (var s in parseResult.Singles)
                sb.AppendLine($"{s.MatchNo},{s.SingleNo},{s.HomePlayerNo},{s.AwayPlayerNo},{s.Winner},{(s.EightBall ? 1 : 0)}");
            File.WriteAllText(file, sb.ToString());
            exportedFiles.Add("Single_Export.csv");
        }

        if (parseResult.Venues.Count != 0)
        {
            var file = Path.Combine(outputFolder, "Venue_Export.csv");
            var sb = new StringBuilder("Id,VenueName,Address\n");
            foreach (var v in parseResult.Venues)
                sb.AppendLine($"{v.ItemId},{Esc(v.VenueName)},{Esc(v.Address)}");
            File.WriteAllText(file, sb.ToString());
            exportedFiles.Add("Venue_Export.csv");
        }

        return exportedFiles.Count != 0
            ? $"Exported {exportedFiles.Count} files:\n• {string.Join("\n• ", exportedFiles)}"
            : "No data to export";
    }

    private static string Esc(string? s) => 
        string.IsNullOrEmpty(s) ? "" : 
        s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

    #endregion
}
