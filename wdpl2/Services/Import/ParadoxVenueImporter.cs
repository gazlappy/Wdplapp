using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

/// <summary>
/// Imports Venue data from Paradox Venue.DB file.
/// 
/// Delphi schema (from datamodule.pas):
/// - Item_id: Integer (PK)
/// - Venue: String (name)
/// - AddressLine1: String
/// - AddressLine2: String
/// - AddressLine3: String
/// - AddressLine4: String
/// </summary>
public static class ParadoxVenueImporter
{
    public class VenueImportResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<ImportedVenue> Venues { get; set; } = new();
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public class ImportedVenue
    {
        public int ParadoxId { get; set; }
        public string Name { get; set; } = "";
        public string AddressLine1 { get; set; } = "";
        public string AddressLine2 { get; set; } = "";
        public string AddressLine3 { get; set; } = "";
        public string AddressLine4 { get; set; } = "";
        public string FullAddress => string.Join(", ", 
            new[] { AddressLine1, AddressLine2, AddressLine3, AddressLine4 }
            .Where(a => !string.IsNullOrWhiteSpace(a)));
        
        public Guid? MappedId { get; set; }
    }

    /// <summary>
    /// Parse Venue.DB file and return raw venue data
    /// </summary>
    public static VenueImportResult ParseVenueDb(string filePath)
    {
        var result = new VenueImportResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"Venue.DB not found: {filePath}");
                return result;
            }

            var bytes = File.ReadAllBytes(filePath);
            var header = ParadoxBinaryReader.ReadHeader(bytes);

            if (header.RecordCount == 0)
            {
                result.Warnings.Add("Venue.DB contains no records");
                result.Success = true;
                return result;
            }

            var records = ParadoxBinaryReader.ReadRecords(bytes, header);

            foreach (var rec in records)
            {
                var venue = new ImportedVenue
                {
                    ParadoxId = GetInt(rec, "Item_id") ?? result.Venues.Count + 1,
                    Name = GetString(rec, "Venue", "VenueName", "Name") ?? "",
                    AddressLine1 = GetString(rec, "AddressLine1", "Address1") ?? "",
                    AddressLine2 = GetString(rec, "AddressLine2", "Address2") ?? "",
                    AddressLine3 = GetString(rec, "AddressLine3", "Address3") ?? "",
                    AddressLine4 = GetString(rec, "AddressLine4", "Address4") ?? ""
                };

                if (!string.IsNullOrWhiteSpace(venue.Name))
                {
                    result.Venues.Add(venue);
                }
            }

            result.Success = true;
            result.Warnings.Add($"Parsed {result.Venues.Count} venues from Venue.DB");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Venue.DB: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Import parsed venues into the app database
    /// </summary>
    public static VenueImportResult ImportToSeason(
        List<ImportedVenue> venues, 
        Guid seasonId,
        Dictionary<int, Guid>? existingVenueMap = null)
    {
        var result = new VenueImportResult();
        existingVenueMap ??= new Dictionary<int, Guid>();

        try
        {
            foreach (var venue in venues)
            {
                // Check if venue already exists (by name)
                var existing = DataStore.Data.Venues.FirstOrDefault(v =>
                    v.SeasonId == seasonId &&
                    !string.IsNullOrWhiteSpace(v.Name) &&
                    v.Name.Equals(venue.Name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    venue.MappedId = existing.Id;
                    existingVenueMap[venue.ParadoxId] = existing.Id;
                    result.SkippedCount++;
                    continue;
                }

                // Create new venue
                var newVenue = new Venue
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    Name = venue.Name,
                    Address = venue.FullAddress
                };

                DataStore.Data.Venues.Add(newVenue);
                venue.MappedId = newVenue.Id;
                existingVenueMap[venue.ParadoxId] = newVenue.Id;
                result.ImportedCount++;
            }

            result.Venues = venues;
            result.Success = true;
            DataStore.Save();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error importing venues: {ex.Message}");
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
