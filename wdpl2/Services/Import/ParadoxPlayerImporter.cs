using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

/// <summary>
/// Imports Player data from Paradox Player.DB file.
/// 
/// Delphi schema (from datamodule.pas):
/// - PlayerNo: Float (used as Integer PK)
/// - PlayerName: String
/// - PlayerTeam: Integer (FK to Team.Item_id)
/// - Played: Integer
/// - Wins: Integer
/// - Losses: Integer
/// - CurrentRating: Integer
/// - BestRating: Integer
/// - BestRatingDate: Date
/// - EightBalls: Integer
/// 
/// Note: The Delphi code creates a "Void Frame" player for each team
/// to represent forfeited/void frames. These should be skipped during import.
/// </summary>
public static class ParadoxPlayerImporter
{
    public class PlayerImportResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<ImportedPlayer> Players { get; set; } = new();
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public int VoidFrameSkipped { get; set; }
    }

    public class ImportedPlayer
    {
        public int ParadoxId { get; set; }
        public string FullName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public int? TeamId { get; set; }
        
        // Stats (for verification/display, usually recalculated from results)
        public int Played { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int? CurrentRating { get; set; }
        public int? BestRating { get; set; }
        public DateTime? BestRatingDate { get; set; }
        public int EightBalls { get; set; }
        
        public Guid? MappedId { get; set; }
        public Guid? MappedTeamId { get; set; }
    }

    /// <summary>
    /// Parse Player.DB file and return raw player data
    /// </summary>
    public static PlayerImportResult ParsePlayerDb(string filePath)
    {
        var result = new PlayerImportResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"Player.DB not found: {filePath}");
                return result;
            }

            var bytes = File.ReadAllBytes(filePath);
            var header = ParadoxBinaryReader.ReadHeader(bytes);

            if (header.RecordCount == 0)
            {
                result.Warnings.Add("Player.DB contains no records");
                result.Success = true;
                return result;
            }

            var records = ParadoxBinaryReader.ReadRecords(bytes, header);

            foreach (var rec in records)
            {
                var fullName = GetString(rec, "PlayerName", "Name") ?? "";

                // Skip void/placeholder entries
                if (string.IsNullOrWhiteSpace(fullName) ||
                    fullName.Equals("Void Frame", StringComparison.OrdinalIgnoreCase) ||
                    fullName.Equals("VoidFrame", StringComparison.OrdinalIgnoreCase))
                {
                    result.VoidFrameSkipped++;
                    continue;
                }

                // Parse name into first/last
                var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var firstName = nameParts.FirstOrDefault()?.ToUpperInvariant() ?? "";
                var lastName = nameParts.Length > 1 
                    ? string.Join(" ", nameParts.Skip(1)).ToUpperInvariant() 
                    : "";

                var player = new ImportedPlayer
                {
                    ParadoxId = GetInt(rec, "PlayerNo", "Id") ?? result.Players.Count + 1,
                    FullName = fullName.ToUpperInvariant(),
                    FirstName = firstName,
                    LastName = lastName,
                    TeamId = GetInt(rec, "PlayerTeam", "Team", "TeamId"),
                    Played = GetInt(rec, "Played") ?? 0,
                    Wins = GetInt(rec, "Wins") ?? 0,
                    Losses = GetInt(rec, "Losses") ?? 0,
                    CurrentRating = GetInt(rec, "CurrentRating", "Rating"),
                    BestRating = GetInt(rec, "BestRating"),
                    BestRatingDate = GetDate(rec, "BestRatingDate"),
                    EightBalls = GetInt(rec, "EightBalls", "8Balls") ?? 0
                };

                result.Players.Add(player);
            }

            result.Success = true;
            result.Warnings.Add($"Parsed {result.Players.Count} players from Player.DB");
            if (result.VoidFrameSkipped > 0)
            {
                result.Warnings.Add($"Skipped {result.VoidFrameSkipped} 'Void Frame' placeholder entries");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Player.DB: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Import parsed players into the app database
    /// </summary>
    public static PlayerImportResult ImportToSeason(
        List<ImportedPlayer> players,
        Guid seasonId,
        Dictionary<int, Guid> teamMap,
        Dictionary<int, Guid>? existingPlayerMap = null)
    {
        var result = new PlayerImportResult();
        existingPlayerMap ??= new Dictionary<int, Guid>();

        try
        {
            foreach (var player in players)
            {
                // Check if player already exists (by name)
                var existing = DataStore.Data.Players.FirstOrDefault(p =>
                    p.SeasonId == seasonId &&
                    p.FirstName?.Equals(player.FirstName, StringComparison.OrdinalIgnoreCase) == true &&
                    p.LastName?.Equals(player.LastName, StringComparison.OrdinalIgnoreCase) == true);

                if (existing != null)
                {
                    player.MappedId = existing.Id;
                    existingPlayerMap[player.ParadoxId] = existing.Id;
                    result.SkippedCount++;
                    continue;
                }

                // Map team
                Guid? mappedTeamId = null;
                if (player.TeamId.HasValue && teamMap.TryGetValue(player.TeamId.Value, out var tId))
                {
                    mappedTeamId = tId;
                    player.MappedTeamId = tId;
                }

                // Create new player
                var newPlayer = new Player
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    FirstName = player.FirstName,
                    LastName = player.LastName,
                    TeamId = mappedTeamId
                    // Note: Don't import stats - they will be recalculated from fixtures
                };

                DataStore.Data.Players.Add(newPlayer);
                player.MappedId = newPlayer.Id;
                existingPlayerMap[player.ParadoxId] = newPlayer.Id;
                result.ImportedCount++;
            }

            result.Players = players;
            result.Success = true;
            DataStore.Save();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error importing players: {ex.Message}");
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
