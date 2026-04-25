using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

/// <summary>
/// Imports Team data from Paradox Team.DB file.
/// 
/// Delphi schema (from datamodule.pas):
/// - Item_id: Integer (PK, auto-increment)
/// - TeamName: String
/// - Venue: Integer (FK to Venue.Item_id)
/// - Division: Integer (FK to Division.Item_id)
/// - Contact: String (captain name)
/// - ContactAddress1-4: String
/// - Wins: Integer
/// - Loses: Integer
/// - Draws: Integer
/// - SWins: Integer (singles wins)
/// - SLosses: Integer (singles losses)
/// - DWins: Integer (doubles wins)
/// - DLosses: Integer (doubles losses)
/// - Points: Integer
/// - Played: Integer
/// - Withdrawn: Boolean
/// - RemoveResults: Boolean
/// - Deduction: Integer (from Team_1 secondary table)
/// - AmtFined, FinesPaid, FinesDue: Currency (from Team_1)
/// </summary>
public static class ParadoxTeamImporter
{
    public class TeamImportResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<ImportedTeam> Teams { get; set; } = new();
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public class ImportedTeam
    {
        public int ParadoxId { get; set; }
        public string Name { get; set; } = "";
        public int? VenueId { get; set; }
        public int? DivisionId { get; set; }
        public string Captain { get; set; } = "";
        public string ContactAddress1 { get; set; } = "";
        public string ContactAddress2 { get; set; } = "";
        public string ContactAddress3 { get; set; } = "";
        public string ContactAddress4 { get; set; } = "";
        
        // Stats (for verification/display, usually recalculated from results)
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
        public int PointsDeduction { get; set; }
        
        public Guid? MappedId { get; set; }
        public Guid? MappedVenueId { get; set; }
        public Guid? MappedDivisionId { get; set; }
    }

    /// <summary>
    /// Parse Team.DB file and return raw team data
    /// </summary>
    public static TeamImportResult ParseTeamDb(string filePath)
    {
        var result = new TeamImportResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"Team.DB not found: {filePath}");
                return result;
            }

            var bytes = File.ReadAllBytes(filePath);
            var header = ParadoxBinaryReader.ReadHeader(bytes);

            if (header.RecordCount == 0)
            {
                result.Warnings.Add("Team.DB contains no records");
                result.Success = true;
                return result;
            }

            var records = ParadoxBinaryReader.ReadRecords(bytes, header);

            foreach (var rec in records)
            {
                var team = new ImportedTeam
                {
                    ParadoxId = GetInt(rec, "Item_id", "ItemId") ?? result.Teams.Count + 1,
                    Name = (GetString(rec, "TeamName", "Name") ?? "").ToUpperInvariant(),
                    VenueId = GetInt(rec, "Venue", "VenueId"),
                    DivisionId = GetInt(rec, "Division", "DivisionId"),
                    Captain = GetString(rec, "Contact", "Captain") ?? "",
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
                    RemoveResults = GetBool(rec, "RemoveResults"),
                    PointsDeduction = GetInt(rec, "Deduction") ?? 0
                };

                if (!string.IsNullOrWhiteSpace(team.Name))
                {
                    result.Teams.Add(team);
                }
            }

            result.Success = true;
            result.Warnings.Add($"Parsed {result.Teams.Count} teams from Team.DB");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Team.DB: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Import parsed teams into the app database
    /// </summary>
    public static TeamImportResult ImportToSeason(
        List<ImportedTeam> teams,
        Guid seasonId,
        Dictionary<int, Guid> venueMap,
        Dictionary<int, Guid> divisionMap,
        Dictionary<int, Guid>? existingTeamMap = null)
    {
        var result = new TeamImportResult();
        existingTeamMap ??= new Dictionary<int, Guid>();

        try
        {
            foreach (var team in teams)
            {
                var normalizedName = team.Name.ToUpperInvariant();

                // Check if team already exists (by name)
                var existing = DataStore.Data.Teams.FirstOrDefault(t =>
                    t.SeasonId == seasonId &&
                    !string.IsNullOrWhiteSpace(t.Name) &&
                    t.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    team.MappedId = existing.Id;
                    existingTeamMap[team.ParadoxId] = existing.Id;
                    result.SkippedCount++;
                    continue;
                }

                // Map venue
                Guid? mappedVenueId = null;
                if (team.VenueId.HasValue && venueMap.TryGetValue(team.VenueId.Value, out var vId))
                {
                    mappedVenueId = vId;
                    team.MappedVenueId = vId;
                }

                // Map division
                Guid? mappedDivisionId = null;
                if (team.DivisionId.HasValue && divisionMap.TryGetValue(team.DivisionId.Value, out var dId))
                {
                    mappedDivisionId = dId;
                    team.MappedDivisionId = dId;
                }

                // Create new team
                var newTeam = new Team
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    Name = normalizedName,
                    DivisionId = mappedDivisionId,
                    VenueId = mappedVenueId,
                    Captain = team.Captain
                    // Note: Don't import stats - they will be recalculated from fixtures
                };

                DataStore.Data.Teams.Add(newTeam);
                team.MappedId = newTeam.Id;
                existingTeamMap[team.ParadoxId] = newTeam.Id;
                result.ImportedCount++;
            }

            result.Teams = teams;
            result.Success = true;
            DataStore.Save();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error importing teams: {ex.Message}");
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

    #endregion
}
