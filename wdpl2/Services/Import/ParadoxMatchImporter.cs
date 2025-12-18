using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

/// <summary>
/// Imports Match/Fixture data from Paradox Match.DB file.
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
/// - DivName: String (lookup from Division table, but stored as calculated field)
/// 
/// The Delphi code also calculates points based on league settings:
/// - HomeTeamPoints = (HSWins * SinglesBonus) + (HDWins * DoublesBonus) + WinBonus/DrawBonus/LossBonus
/// - AwayTeamPoints = (ASWins * SinglesBonus) + (ADWins * DoublesBonus) + WinBonus/DrawBonus/LossBonus
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
            var header = ParadoxBinaryReader.ReadHeader(bytes);

            if (header.RecordCount == 0)
            {
                result.Warnings.Add("Match.DB contains no records");
                result.Success = true;
                return result;
            }

            var records = ParadoxBinaryReader.ReadRecords(bytes, header);

            foreach (var rec in records)
            {
                var homeTeam = GetInt(rec, "HomeTeam", "Home") ?? 0;
                var awayTeam = GetInt(rec, "AwayTeam", "Away") ?? 0;

                // Skip invalid matches
                if (homeTeam == 0 || awayTeam == 0)
                    continue;

                var matchDate = GetDate(rec, "MatchDate", "Date") ?? DateTime.MinValue;

                var match = new ImportedMatch
                {
                    ParadoxId = GetInt(rec, "MatchNo", "Id") ?? result.Matches.Count + 1,
                    HomeTeamId = homeTeam,
                    AwayTeamId = awayTeam,
                    MatchDate = matchDate,
                    HomeSinglesWins = GetInt(rec, "HSWins", "HomeSinglesWins") ?? 0,
                    AwaySinglesWins = GetInt(rec, "ASWins", "AwaySinglesWins") ?? 0,
                    HomeDoublesWins = GetInt(rec, "HDWins", "HomeDoublesWins") ?? 0,
                    AwayDoublesWins = GetInt(rec, "ADWins", "AwayDoublesWins") ?? 0,
                    DivisionName = GetString(rec, "DivName", "Division") ?? ""
                };

                result.Matches.Add(match);

                // Track date range
                if (matchDate > DateTime.MinValue)
                {
                    if (!result.MinDate.HasValue || matchDate < result.MinDate)
                        result.MinDate = matchDate;
                    if (!result.MaxDate.HasValue || matchDate > result.MaxDate)
                        result.MaxDate = matchDate;
                }
            }

            result.Success = true;
            result.Warnings.Add($"Parsed {result.Matches.Count} matches from Match.DB");
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
