using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

/// <summary>
/// Imports Doubles frame results from Paradox Double.DB (or Dbls.DB) file.
/// 
/// Delphi schema (from datamodule.pas):
/// - MatchNo: Float (FK to Match.MatchNo)
/// - DoubleNo: Float (frame number within match, 1-based, continuation from singles)
/// - HomePlayerNo1: Float (FK to Player.PlayerNo)
/// - HomePlayerNo2: Float (FK to Player.PlayerNo)
/// - AwayPlayerNo1: Float (FK to Player.PlayerNo)
/// - AwayPlayerNo2: Float (FK to Player.PlayerNo)
/// - Winner: String ("Home" or "Away")
/// - EightBall1: Boolean (8-ball by first pair player)
/// - EightBall2: Boolean (8-ball by second pair player)
/// 
/// Note: In doubles, both home players play as a pair against both away players.
/// The lookup fields HPN1, HPN2, APN1, APN2 exist for display.
/// </summary>
public static class ParadoxDoubleImporter
{
    public class DoubleImportResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<ImportedDouble> Doubles { get; set; } = new();
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public class ImportedDouble
    {
        public int MatchNo { get; set; }
        public int FrameNumber { get; set; }
        public int HomePlayer1No { get; set; }
        public int HomePlayer2No { get; set; }
        public int AwayPlayer1No { get; set; }
        public int AwayPlayer2No { get; set; }
        public string Winner { get; set; } = ""; // "Home" or "Away"
        public bool EightBall1 { get; set; }
        public bool EightBall2 { get; set; }
        
        public Guid? MappedFixtureId { get; set; }
        public Guid? MappedHomePlayer1Id { get; set; }
        public Guid? MappedHomePlayer2Id { get; set; }
        public Guid? MappedAwayPlayer1Id { get; set; }
        public Guid? MappedAwayPlayer2Id { get; set; }
    }

    /// <summary>
    /// Parse Dbls.DB (or Double.DB) file and return raw doubles frame data
    /// </summary>
    public static DoubleImportResult ParseDoublesDb(string filePath)
    {
        var result = new DoubleImportResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"Doubles DB file not found: {filePath}");
                return result;
            }

            var bytes = File.ReadAllBytes(filePath);
            var header = ParadoxBinaryReader.ReadHeader(bytes);

            if (header.RecordCount == 0)
            {
                result.Warnings.Add("Doubles DB contains no records");
                result.Success = true;
                return result;
            }

            var records = ParadoxBinaryReader.ReadRecords(bytes, header);

            foreach (var rec in records)
            {
                var matchNo = GetInt(rec, "MatchNo", "Match") ?? 0;
                var winner = GetString(rec, "Winner") ?? "";

                // Skip invalid entries (must have match and winner)
                if (matchNo == 0 || string.IsNullOrWhiteSpace(winner))
                    continue;

                var dbl = new ImportedDouble
                {
                    MatchNo = matchNo,
                    FrameNumber = GetInt(rec, "DblNo", "DoubleNo", "FrameNo") ?? result.Doubles.Count + 1,
                    HomePlayer1No = GetInt(rec, "HomePlayerNo1", "HP1", "HomePlayer1") ?? 0,
                    HomePlayer2No = GetInt(rec, "HomePlayerNo2", "HP2", "HomePlayer2") ?? 0,
                    AwayPlayer1No = GetInt(rec, "AwayPlayerNo1", "AP1", "AwayPlayer1") ?? 0,
                    AwayPlayer2No = GetInt(rec, "AwayPlayerNo2", "AP2", "AwayPlayer2") ?? 0,
                    Winner = winner,
                    EightBall1 = GetBool(rec, "EightBall1", "8Ball1"),
                    EightBall2 = GetBool(rec, "EightBall2", "8Ball2")
                };

                result.Doubles.Add(dbl);
            }

            result.Success = true;
            result.Warnings.Add($"Parsed {result.Doubles.Count} doubles frames from Dbls.DB");
            
            // Count unique matches
            var uniqueMatches = result.Doubles.Select(d => d.MatchNo).Distinct().Count();
            result.Warnings.Add($"Frames span {uniqueMatches} matches");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Dbls.DB: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Import parsed doubles frames into fixtures.
    /// 
    /// Note: The current Fixture model (FrameResult) only supports singles-style frames 
    /// with one home player and one away player. To properly import doubles, we would need
    /// a different model. For now, this imports doubles as notes or metadata on the fixture.
    /// </summary>
    public static DoubleImportResult ImportToFixtures(
        List<ImportedDouble> doubles,
        Dictionary<int, Guid> matchMap,
        Dictionary<int, Guid> playerMap,
        int singlesFrameCount = 8) // Used to offset frame numbers
    {
        var result = new DoubleImportResult();

        try
        {
            // Group by match for efficiency
            var doublesByMatch = doubles.GroupBy(d => d.MatchNo);

            foreach (var matchGroup in doublesByMatch)
            {
                if (!matchMap.TryGetValue(matchGroup.Key, out var fixtureId))
                {
                    result.Warnings.Add($"Match {matchGroup.Key}: Fixture not found, skipping {matchGroup.Count()} doubles frames");
                    result.SkippedCount += matchGroup.Count();
                    continue;
                }

                var fixture = DataStore.Data.Fixtures.FirstOrDefault(f => f.Id == fixtureId);
                if (fixture == null)
                {
                    result.Warnings.Add($"Match {matchGroup.Key}: Fixture GUID {fixtureId} not in database");
                    result.SkippedCount += matchGroup.Count();
                    continue;
                }

                foreach (var dbl in matchGroup)
                {
                    dbl.MappedFixtureId = fixtureId;

                    // Offset frame number to come after singles
                    var frameNumber = singlesFrameCount + dbl.FrameNumber;

                    // Check if frame already exists
                    if (fixture.Frames.Any(f => f.Number == frameNumber))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Map players (use first player from each pair for simplified storage)
                    Guid? homePlayerId = null;
                    Guid? awayPlayerId = null;

                    if (dbl.HomePlayer1No > 0 && playerMap.TryGetValue(dbl.HomePlayer1No, out var hp1Id))
                    {
                        homePlayerId = hp1Id;
                        dbl.MappedHomePlayer1Id = hp1Id;
                    }
                    if (dbl.HomePlayer2No > 0 && playerMap.TryGetValue(dbl.HomePlayer2No, out var hp2Id))
                    {
                        dbl.MappedHomePlayer2Id = hp2Id;
                    }
                    if (dbl.AwayPlayer1No > 0 && playerMap.TryGetValue(dbl.AwayPlayer1No, out var ap1Id))
                    {
                        awayPlayerId = ap1Id;
                        dbl.MappedAwayPlayer1Id = ap1Id;
                    }
                    if (dbl.AwayPlayer2No > 0 && playerMap.TryGetValue(dbl.AwayPlayer2No, out var ap2Id))
                    {
                        dbl.MappedAwayPlayer2Id = ap2Id;
                    }

                    // Determine winner
                    var winnerEnum = dbl.Winner.ToLowerInvariant() switch
                    {
                        "home" => FrameWinner.Home,
                        "away" => FrameWinner.Away,
                        _ => FrameWinner.None
                    };

                    // Create frame result (simplified - only stores first player from each pair)
                    // In future, consider extending FrameResult to support doubles pairs
                    var frame = new FrameResult
                    {
                        Number = frameNumber,
                        HomePlayerId = homePlayerId,
                        AwayPlayerId = awayPlayerId,
                        Winner = winnerEnum,
                        EightBall = dbl.EightBall1 || dbl.EightBall2,
                        IsDoubles = true // Mark as doubles frame
                    };

                    fixture.Frames.Add(frame);
                    result.ImportedCount++;
                }
            }

            result.Doubles = doubles;
            result.Success = true;
            DataStore.Save();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error importing doubles: {ex.Message}");
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
