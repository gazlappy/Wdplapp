using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

/// <summary>
/// Imports Singles frame results from Paradox Single.DB file.
/// 
/// Delphi schema (from datamodule.pas):
/// - MatchNo: Float (FK to Match.MatchNo)
/// - SingleNo: Float (frame number within match, 1-based)
/// - HomePlayerNo: Float (FK to Player.PlayerNo)
/// - AwayPlayerNo: Float (FK to Player.PlayerNo)
/// - Winner: String ("Home" or "Away")
/// - EightBall: Boolean (true if frame won on the 8-ball)
/// 
/// Note: Lookup fields HPN and APN exist for display but actual data is HomePlayerNo/AwayPlayerNo
/// </summary>
public static class ParadoxSingleImporter
{
    public class SingleImportResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<ImportedSingle> Singles { get; set; } = new();
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public class ImportedSingle
    {
        public int MatchNo { get; set; }
        public int FrameNumber { get; set; }
        public int HomePlayerNo { get; set; }
        public int AwayPlayerNo { get; set; }
        public string Winner { get; set; } = ""; // "Home" or "Away"
        public bool EightBall { get; set; }
        
        public Guid? MappedFixtureId { get; set; }
        public Guid? MappedHomePlayerId { get; set; }
        public Guid? MappedAwayPlayerId { get; set; }
    }

    /// <summary>
    /// Parse Single.DB file and return raw singles frame data
    /// </summary>
    public static SingleImportResult ParseSingleDb(string filePath)
    {
        var result = new SingleImportResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"Single.DB not found: {filePath}");
                return result;
            }

            var bytes = File.ReadAllBytes(filePath);
            var header = ParadoxBinaryReader.ReadHeader(bytes);

            if (header.RecordCount == 0)
            {
                result.Warnings.Add("Single.DB contains no records");
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

                var single = new ImportedSingle
                {
                    MatchNo = matchNo,
                    FrameNumber = GetInt(rec, "SingleNo", "FrameNo", "Frame") ?? result.Singles.Count + 1,
                    HomePlayerNo = GetInt(rec, "HomePlayerNo", "HomePlayer") ?? 0,
                    AwayPlayerNo = GetInt(rec, "AwayPlayerNo", "AwayPlayer") ?? 0,
                    Winner = winner,
                    EightBall = GetBool(rec, "EightBall", "8Ball")
                };

                result.Singles.Add(single);
            }

            result.Success = true;
            result.Warnings.Add($"Parsed {result.Singles.Count} singles frames from Single.DB");
            
            // Count unique matches
            var uniqueMatches = result.Singles.Select(s => s.MatchNo).Distinct().Count();
            result.Warnings.Add($"Frames span {uniqueMatches} matches");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Single.DB: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Import parsed singles frames into fixtures
    /// </summary>
    public static SingleImportResult ImportToFixtures(
        List<ImportedSingle> singles,
        Dictionary<int, Guid> matchMap,
        Dictionary<int, Guid> playerMap)
    {
        var result = new SingleImportResult();

        try
        {
            // Group by match for efficiency
            var singlesByMatch = singles.GroupBy(s => s.MatchNo);

            foreach (var matchGroup in singlesByMatch)
            {
                if (!matchMap.TryGetValue(matchGroup.Key, out var fixtureId))
                {
                    result.Warnings.Add($"Match {matchGroup.Key}: Fixture not found, skipping {matchGroup.Count()} frames");
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

                foreach (var single in matchGroup)
                {
                    single.MappedFixtureId = fixtureId;

                    // Check if frame already exists
                    if (fixture.Frames.Any(f => f.Number == single.FrameNumber))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Map players
                    Guid? homePlayerId = null;
                    Guid? awayPlayerId = null;

                    if (single.HomePlayerNo > 0 && playerMap.TryGetValue(single.HomePlayerNo, out var hpId))
                    {
                        homePlayerId = hpId;
                        single.MappedHomePlayerId = hpId;
                    }
                    if (single.AwayPlayerNo > 0 && playerMap.TryGetValue(single.AwayPlayerNo, out var apId))
                    {
                        awayPlayerId = apId;
                        single.MappedAwayPlayerId = apId;
                    }

                    // Determine winner
                    var winnerEnum = single.Winner.ToLowerInvariant() switch
                    {
                        "home" => FrameWinner.Home,
                        "away" => FrameWinner.Away,
                        _ => FrameWinner.None
                    };

                    // Create frame result
                    var frame = new FrameResult
                    {
                        Number = single.FrameNumber,
                        HomePlayerId = homePlayerId,
                        AwayPlayerId = awayPlayerId,
                        Winner = winnerEnum,
                        EightBall = single.EightBall
                    };

                    fixture.Frames.Add(frame);
                    result.ImportedCount++;
                }
            }

            result.Singles = singles;
            result.Success = true;
            DataStore.Save();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error importing singles: {ex.Message}");
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
