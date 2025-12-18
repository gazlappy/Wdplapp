using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

/// <summary>
/// Imports Division data from Paradox Division.DB file.
/// 
/// Delphi schema (from datamodule.pas):
/// - Item_id: Integer (PK, auto-increment)
/// - Abbreviated: String (short name like "Prem", "One")
/// - FullDivisionName: String (full name like "Premier Division")
/// </summary>
public static class ParadoxDivisionImporter
{
    public class DivisionImportResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<ImportedDivision> Divisions { get; set; } = new();
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public class ImportedDivision
    {
        public int ParadoxId { get; set; }
        public string Abbreviated { get; set; } = "";
        public string FullName { get; set; } = "";
        public Guid? MappedId { get; set; }
    }

    /// <summary>
    /// Parse Division.DB file and return raw division data
    /// </summary>
    public static DivisionImportResult ParseDivisionDb(string filePath)
    {
        var result = new DivisionImportResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"Division.DB not found: {filePath}");
                return result;
            }

            var bytes = File.ReadAllBytes(filePath);
            var header = ParadoxBinaryReader.ReadHeader(bytes);

            if (header.RecordCount == 0)
            {
                result.Warnings.Add("Division.DB contains no records");
                result.Success = true;
                return result;
            }

            var records = ParadoxBinaryReader.ReadRecords(bytes, header);

            foreach (var rec in records)
            {
                var div = new ImportedDivision
                {
                    ParadoxId = GetInt(rec, "Item_id") ?? result.Divisions.Count + 1,
                    Abbreviated = GetString(rec, "Abbreviated", "Abbrev") ?? "",
                    FullName = GetString(rec, "FullDivisionName", "DivisionName", "Name") ?? ""
                };

                // Use abbreviated as full name if full name is empty
                if (string.IsNullOrWhiteSpace(div.FullName) && !string.IsNullOrWhiteSpace(div.Abbreviated))
                {
                    div.FullName = div.Abbreviated;
                }

                // Skip placeholder entries
                if (!string.IsNullOrWhiteSpace(div.FullName) &&
                    !div.FullName.Equals("test", StringComparison.OrdinalIgnoreCase))
                {
                    result.Divisions.Add(div);
                }
            }

            result.Success = true;
            result.Warnings.Add($"Parsed {result.Divisions.Count} divisions from Division.DB");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Division.DB: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Import parsed divisions into the app database
    /// </summary>
    public static DivisionImportResult ImportToSeason(
        List<ImportedDivision> divisions,
        Guid seasonId,
        Dictionary<int, Guid>? existingDivisionMap = null,
        Dictionary<string, Guid>? divisionNameMap = null)
    {
        var result = new DivisionImportResult();
        existingDivisionMap ??= new Dictionary<int, Guid>();
        divisionNameMap ??= new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var div in divisions)
            {
                // Check if division already exists (by name)
                var existing = DataStore.Data.Divisions.FirstOrDefault(d =>
                    d.SeasonId == seasonId &&
                    !string.IsNullOrWhiteSpace(d.Name) &&
                    d.Name.Equals(div.FullName, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    div.MappedId = existing.Id;
                    existingDivisionMap[div.ParadoxId] = existing.Id;
                    divisionNameMap[div.FullName] = existing.Id;
                    if (!string.IsNullOrWhiteSpace(div.Abbreviated))
                        divisionNameMap[div.Abbreviated] = existing.Id;
                    result.SkippedCount++;
                    continue;
                }

                // Create new division
                var newDiv = new Division
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    Name = div.FullName
                };

                DataStore.Data.Divisions.Add(newDiv);
                div.MappedId = newDiv.Id;
                existingDivisionMap[div.ParadoxId] = newDiv.Id;
                divisionNameMap[div.FullName] = newDiv.Id;
                if (!string.IsNullOrWhiteSpace(div.Abbreviated))
                    divisionNameMap[div.Abbreviated] = newDiv.Id;
                result.ImportedCount++;
            }

            result.Divisions = divisions;
            result.Success = true;
            DataStore.Save();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error importing divisions: {ex.Message}");
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

    #endregion
}
