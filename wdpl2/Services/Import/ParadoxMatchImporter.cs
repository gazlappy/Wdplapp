using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

/// <summary>
/// Imports Match/Fixture data from Paradox Match.DB file.
/// Uses proven binary parsing logic from ParadoxDeepDive.
/// 
/// Delphi schema (from datamodule.pas):
/// - MatchNo: Float (used as Integer PK)
/// - HomeTeam: Integer (FK to Team.Item_id)
/// - AwayTeam: Integer (FK to Team.Item_id)
/// - MatchDate: Date
/// - HSWins: Integer (Home Singles Wins)
/// - ASWins: Integer (Away Singles Wins)
/// - HDWins: Integer (Home Doubles Wins)
/// - ADWins: Integer (Away Doubles Wins)
/// - DivName: String (lookup from Division table)
/// </summary>
public static class ParadoxMatchImporter
{
    public class MatchImportResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<ImportedMatch> Matches { get; set; } = new();
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public DateTime? MinDate { get; set; }
        public DateTime? MaxDate { get; set; }
    }

    public class ImportedMatch
    {
        public int ParadoxId { get; set; }
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public DateTime MatchDate { get; set; }
        public string DivisionName { get; set; } = "";
        
        // Results summary
        public int HomeSinglesWins { get; set; }
        public int AwaySinglesWins { get; set; }
        public int HomeDoublesWins { get; set; }
        public int AwayDoublesWins { get; set; }
        
        public bool IsComplete => HomeSinglesWins > 0 || AwaySinglesWins > 0 ||
                                  HomeDoublesWins > 0 || AwayDoublesWins > 0;
        
        public Guid? MappedId { get; set; }
        public Guid? MappedHomeTeamId { get; set; }
        public Guid? MappedAwayTeamId { get; set; }
        public Guid? MappedDivisionId { get; set; }
        public Guid? MappedVenueId { get; set; }
    }

    /// <summary>
    /// Parse Match.DB file and return raw match data
    /// Uses proven binary parsing logic from ParadoxDeepDive
    /// </summary>
    public static MatchImportResult ParseMatchDb(string filePath)
    {
        var result = new MatchImportResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"Match.DB not found: {filePath}");
                return result;
            }

            var bytes = File.ReadAllBytes(filePath);
            
            if (bytes.Length < 88)
            {
                result.Errors.Add("File too small for Paradox header");
                return result;
            }

            // Read header - using exact same method as ParadoxDeepDive
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var headerSize = BitConverter.ToInt16(bytes, 2);
            var maxTableSize = bytes[5];
            var numRecords = BitConverter.ToInt32(bytes, 6);
            var numFields = bytes[33];
            
            result.Warnings.Add($"Match.DB header: {numRecords} records, {numFields} fields, record size {recordSize}");

            if (numRecords == 0)
            {
                result.Warnings.Add("Match.DB contains no records");
                result.Success = true;
                return result;
            }

            // Field types at offset 78
            var fieldTypes = new List<byte>();
            for (int i = 0; i < numFields; i++)
            {
                if (78 + i < bytes.Length)
                    fieldTypes.Add(bytes[78 + i]);
            }
            
            // Field sizes immediately follow types
            var fieldSizes = new List<byte>();
            for (int i = 0; i < numFields; i++)
            {
                if (78 + numFields + i < bytes.Length)
                    fieldSizes.Add(bytes[78 + numFields + i]);
            }

            // Field names - using exact same method as ParadoxDeepDive
            var fieldNames = new List<string>();
            int nameStart = 78 + numFields * 2;
            
            // Skip table name first
            while (nameStart < bytes.Length && bytes[nameStart] != 0)
                nameStart++;
            nameStart++; // Skip null
            
            // Calculate header end
            int blockSize = maxTableSize * 0x400;
            if (blockSize == 0) blockSize = 2048;
            int headerEnd = headerSize * blockSize;
            if (headerEnd == 0) headerEnd = blockSize;
            
            // Read field names
            var currentName = new StringBuilder();
            for (int i = nameStart; i < Math.Min(bytes.Length, headerEnd) && fieldNames.Count < numFields; i++)
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

            result.Warnings.Add($"Field names found: {string.Join(", ", fieldNames)}");

            // Calculate data start
            int dataStart = headerEnd;
            if (dataStart == 0) dataStart = blockSize;

            result.Warnings.Add($"Data starts at offset {dataStart}, block size {blockSize}");

            // Read records - using exact same block calculation as ParadoxDeepDive
            int recordsRead = 0;
            for (int rec = 0; rec < numRecords; rec++)
            {
                // Calculate position accounting for block headers
                int blockNum = (rec * recordSize) / (blockSize - 6);
                int posInBlock = (rec * recordSize) % (blockSize - 6);
                int actualOffset = dataStart + (blockNum * blockSize) + 6 + posInBlock;
                
                if (actualOffset + recordSize > bytes.Length)
                {
                    result.Warnings.Add($"Record {rec} beyond file end at offset {actualOffset}");
                    break;
                }

                var record = new Dictionary<string, object?>();
                int fieldOffset = actualOffset;
                
                for (int f = 0; f < numFields && f < fieldTypes.Count && f < fieldSizes.Count; f++)
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

                // Extract match data
                var homeTeam = GetInt(record, "HomeTeam", "Home") ?? 0;
                var awayTeam = GetInt(record, "AwayTeam", "Away") ?? 0;

                // Skip invalid matches
                if (homeTeam == 0 || awayTeam == 0)
                    continue;

                var matchDate = GetDate(record, "MatchDate", "Date") ?? DateTime.MinValue;

                var match = new ImportedMatch
                {
                    ParadoxId = GetInt(record, "MatchNo", "Id") ?? result.Matches.Count + 1,
                    HomeTeamId = homeTeam,
                    AwayTeamId = awayTeam,
                    MatchDate = matchDate,
                    HomeSinglesWins = GetInt(record, "HSWins", "HomeSinglesWins") ?? 0,
                    AwaySinglesWins = GetInt(record, "ASWins", "AwaySinglesWins") ?? 0,
                    HomeDoublesWins = GetInt(record, "HDWins", "HomeDoublesWins") ?? 0,
                    AwayDoublesWins = GetInt(record, "ADWins", "AwayDoublesWins") ?? 0,
                    DivisionName = GetString(record, "DivName", "Division") ?? ""
                };

                result.Matches.Add(match);
                recordsRead++;

                // Track date range
                if (matchDate > DateTime.MinValue)
                {
                    if (!result.MinDate.HasValue || matchDate < result.MinDate)
                        result.MinDate = matchDate;
                    if (!result.MaxDate.HasValue || matchDate > result.MaxDate)
                        result.MaxDate = matchDate;
                }

                // Log first few records for debugging
                if (recordsRead <= 3)
                {
                    result.Warnings.Add($"  Match {recordsRead}: Home={homeTeam}, Away={awayTeam}, Date={matchDate:yyyy-MM-dd}");
                }
            }

            result.Success = true;
            result.Warnings.Add($"? Parsed {result.Matches.Count} valid matches from {numRecords} records");
            if (result.MinDate.HasValue && result.MaxDate.HasValue)
            {
                result.Warnings.Add($"Date range: {result.MinDate.Value:dd/MM/yyyy} to {result.MaxDate.Value:dd/MM/yyyy}");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Match.DB: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Import parsed matches as fixtures into the app database
    /// </summary>
    public static MatchImportResult ImportToSeason(
        List<ImportedMatch> matches,
        Guid seasonId,
        Dictionary<int, Guid> teamMap,
        Dictionary<string, Guid> divisionNameMap,
        Dictionary<int, Guid>? existingMatchMap = null)
    {
        var result = new MatchImportResult();
        existingMatchMap ??= new Dictionary<int, Guid>();

        try
        {
            foreach (var match in matches)
            {
                // Map teams
                if (!teamMap.TryGetValue(match.HomeTeamId, out var homeTeamGuid))
                {
                    result.Warnings.Add($"Match {match.ParadoxId}: Home team ID {match.HomeTeamId} not found");
                    continue;
                }
                if (!teamMap.TryGetValue(match.AwayTeamId, out var awayTeamGuid))
                {
                    result.Warnings.Add($"Match {match.ParadoxId}: Away team ID {match.AwayTeamId} not found");
                    continue;
                }

                match.MappedHomeTeamId = homeTeamGuid;
                match.MappedAwayTeamId = awayTeamGuid;

                // Check if fixture already exists
                var existing = DataStore.Data.Fixtures.FirstOrDefault(f =>
                    f.SeasonId == seasonId &&
                    f.Date.Date == match.MatchDate.Date &&
                    f.HomeTeamId == homeTeamGuid &&
                    f.AwayTeamId == awayTeamGuid);

                if (existing != null)
                {
                    match.MappedId = existing.Id;
                    existingMatchMap[match.ParadoxId] = existing.Id;
                    result.SkippedCount++;
                    continue;
                }

                // Map division by name
                Guid? divisionId = null;
                if (!string.IsNullOrWhiteSpace(match.DivisionName) &&
                    divisionNameMap.TryGetValue(match.DivisionName, out var dId))
                {
                    divisionId = dId;
                    match.MappedDivisionId = dId;
                }

                // Get venue from home team
                Guid? venueId = null;
                var homeTeam = DataStore.Data.Teams.FirstOrDefault(t => t.Id == homeTeamGuid);
                if (homeTeam?.VenueId.HasValue == true)
                {
                    venueId = homeTeam.VenueId;
                    match.MappedVenueId = venueId;
                }

                // Create fixture
                var fixture = new Fixture
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    DivisionId = divisionId,
                    Date = match.MatchDate,
                    HomeTeamId = homeTeamGuid,
                    AwayTeamId = awayTeamGuid,
                    VenueId = venueId
                };

                DataStore.Data.Fixtures.Add(fixture);
                match.MappedId = fixture.Id;
                existingMatchMap[match.ParadoxId] = fixture.Id;
                result.ImportedCount++;

                // Track date range
                if (match.MatchDate > DateTime.MinValue)
                {
                    if (!result.MinDate.HasValue || match.MatchDate < result.MinDate)
                        result.MinDate = match.MatchDate;
                    if (!result.MaxDate.HasValue || match.MatchDate > result.MaxDate)
                        result.MaxDate = match.MatchDate;
                }
            }

            result.Matches = matches;
            result.Success = true;
            DataStore.Save();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error importing matches: {ex.Message}");
        }

        return result;
    }

    #region Paradox Field Decoding (from ParadoxDeepDive)

    private static object? DecodeField(byte[] bytes, byte fieldType)
    {
        if (bytes.All(b => b == 0))
            return null;

        try
        {
            switch (fieldType)
            {
                case 0x01: // Alpha (string)
                    int len = Array.IndexOf(bytes, (byte)0);
                    if (len < 0) len = bytes.Length;
                    return Encoding.ASCII.GetString(bytes, 0, len).Trim();

                case 0x02: // Date
                    if (bytes.Length >= 4 && (bytes[0] & 0x80) == 0x80)
                    {
                        int days = ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                        if (days > 0 && days < 3000000)
                        {
                            try { return new DateTime(1, 1, 1).AddDays(days - 1); }
                            catch { return null; }
                        }
                    }
                    return null;

                case 0x03: // Short (2 bytes)
                    if (bytes.Length >= 2)
                    {
                        if ((bytes[0] & 0x80) == 0x80)
                            return (short)(((bytes[0] & 0x7F) << 8) | bytes[1]);
                        else if (bytes[0] != 0 || bytes[1] != 0)
                            return (short)-(((bytes[0] ^ 0x7F) << 8) | (bytes[1] ^ 0xFF));
                        return (short)0;
                    }
                    return null;

                case 0x04: // Long (4 bytes)
                case 0x16: // AutoInc
                    if (bytes.Length >= 4)
                    {
                        if ((bytes[0] & 0x80) == 0x80)
                            return ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                        else if (bytes.Any(b => b != 0))
                            return -(((bytes[0] ^ 0x7F) << 24) | ((bytes[1] ^ 0xFF) << 16) | ((bytes[2] ^ 0xFF) << 8) | (bytes[3] ^ 0xFF));
                        return 0;
                    }
                    return null;

                case 0x06: // Number (double, 8 bytes)
                    if (bytes.Length >= 8)
                    {
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
                    }
                    return null;

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
        catch
        {
            return null;
        }
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

    #endregion
}
