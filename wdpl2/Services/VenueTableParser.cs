using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Shared helper for parsing venue names that may contain table identifiers.
/// Used by all importers to ensure consistent venue/table parsing.
/// </summary>
public static class VenueTableParser
{
    /// <summary>
    /// Default table label for venues without explicit table suffixes
    /// </summary>
    public const string DefaultTableLabel = "Table 1";
    
    /// <summary>
    /// Result of parsing a venue name
    /// </summary>
    public class ParseResult
    {
        /// <summary>Base venue name (e.g., "R.G.W.M.C" from "R.G.W.M.C T1")</summary>
        public string BaseName { get; set; } = "";
        
        /// <summary>Table label if detected (e.g., "T1", "TB1", "BAR")</summary>
        public string? TableLabel { get; set; }
        
        /// <summary>Original full name before parsing</summary>
        public string OriginalName { get; set; } = "";
        
        /// <summary>Whether a table was detected in the name</summary>
        public bool HasTable => !string.IsNullOrEmpty(TableLabel);
    }

    // Regex patterns to detect table suffixes in venue names
    // Order matters - more specific patterns first
    private static readonly (Regex pattern, int baseGroup, int tableGroup)[] TablePatterns = 
    {
        // "VENUE T1", "VENUE T2" - table number pattern
        (new Regex(@"^(.+?)\s+(T\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1, 2),
        
        // "VENUE TB1", "VENUE TB2" - table with TB prefix
        (new Regex(@"^(.+?)\s+(TB\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1, 2),
        
        // "VENUE TABLE 1", "VENUE TABLE1" - explicit table word
        (new Regex(@"^(.+?)\s+(TABLE\s*\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1, 2),
        
        // "VENUE BAR" - BAR as a location within venue
        (new Regex(@"^(.+?)\s+(BAR)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1, 2),
        
        // "VENUE LOUNGE" - LOUNGE as a location within venue
        (new Regex(@"^(.+?)\s+(LOUNGE)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1, 2),
        
        // "VENUE CLUB" - CLUB as a location (but only if preceded by venue name)
        // Be careful - "CON CLUB" should be a venue name, not venue "CON" + table "CLUB"
        // So we only match if there's already a longer name before CLUB
        (new Regex(@"^(.{5,}?)\s+(CLUB\s*\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1, 2),
    };

    /// <summary>
    /// Parse a venue name to extract the base venue name and optional table label.
    /// </summary>
    /// <param name="fullVenueName">The complete venue name (e.g., "R.G.W.M.C T1")</param>
    /// <returns>Parse result with base name and optional table label</returns>
    public static ParseResult Parse(string? fullVenueName)
    {
        var result = new ParseResult
        {
            OriginalName = fullVenueName ?? "",
            BaseName = fullVenueName?.Trim() ?? ""
        };

        if (string.IsNullOrWhiteSpace(fullVenueName))
            return result;

        var trimmedName = fullVenueName.Trim();
        result.OriginalName = trimmedName;
        result.BaseName = trimmedName;

        // Try each pattern to find a table suffix
        foreach (var (pattern, baseGroup, tableGroup) in TablePatterns)
        {
            var match = pattern.Match(trimmedName);
            if (match.Success)
            {
                var baseName = match.Groups[baseGroup].Value.Trim();
                var tableLabel = match.Groups[tableGroup].Value.Trim().ToUpperInvariant();
                
                // Validate base name is not too short (prevents false positives)
                if (baseName.Length >= 2)
                {
                    result.BaseName = baseName;
                    result.TableLabel = tableLabel;
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Process a list of venue names and group them into venues with tables.
    /// </summary>
    /// <param name="venueNames">Raw venue names from import source</param>
    /// <param name="seasonId">Season ID for created venues</param>
    /// <param name="createDefaultTable">If true, creates a default table for venues without explicit tables</param>
    /// <returns>Consolidated list of venues with their tables</returns>
    public static (List<Venue> venues, Dictionary<string, (Guid venueId, Guid? tableId)> rawNameMapping) 
        ConsolidateVenues(IEnumerable<string> venueNames, Guid seasonId, bool createDefaultTable = true)
    {
        var venuesByBaseName = new Dictionary<string, Venue>(StringComparer.OrdinalIgnoreCase);
        var rawNameMapping = new Dictionary<string, (Guid venueId, Guid? tableId)>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawName in venueNames)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            var parsed = Parse(rawName);
            
            // Skip invalid names
            if (string.IsNullOrWhiteSpace(parsed.BaseName) || parsed.BaseName.Length < 2)
                continue;

            // Get or create venue
            if (!venuesByBaseName.TryGetValue(parsed.BaseName, out var venue))
            {
                venue = new Venue
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    Name = parsed.BaseName.ToUpperInvariant(),
                    Notes = "[IMPORTED]",
                    Tables = new List<VenueTable>()
                };
                venuesByBaseName[parsed.BaseName] = venue;
            }

            // Add table if detected
            Guid? tableId = null;
            if (parsed.HasTable)
            {
                var existingTable = venue.Tables.Find(t => 
                    t.Label.Equals(parsed.TableLabel, StringComparison.OrdinalIgnoreCase));
                
                if (existingTable == null)
                {
                    var newTable = new VenueTable
                    {
                        Id = Guid.NewGuid(),
                        Label = parsed.TableLabel!,
                        MaxTeams = 2
                    };
                    venue.Tables.Add(newTable);
                    tableId = newTable.Id;
                }
                else
                {
                    tableId = existingTable.Id;
                }
            }

            // Map the raw name to the venue (and optionally table)
            if (!rawNameMapping.ContainsKey(rawName))
            {
                rawNameMapping[rawName] = (venue.Id, tableId);
            }
        }

        // After processing all venues, add default table to venues that don't have any tables
        if (createDefaultTable)
        {
            foreach (var venue in venuesByBaseName.Values)
            {
                if (!venue.Tables.Any())
                {
                    var defaultTable = new VenueTable
                    {
                        Id = Guid.NewGuid(),
                        Label = DefaultTableLabel,
                        MaxTeams = 2
                    };
                    venue.Tables.Add(defaultTable);
                }
            }
        }

        return (venuesByBaseName.Values.ToList(), rawNameMapping);
    }

    /// <summary>
    /// Add a table to a venue if it doesn't already exist.
    /// </summary>
    public static VenueTable GetOrAddTable(Venue venue, string tableLabel)
    {
        var existing = venue.Tables.Find(t => 
            t.Label.Equals(tableLabel, StringComparison.OrdinalIgnoreCase));
        
        if (existing != null)
            return existing;

        var newTable = new VenueTable
        {
            Id = Guid.NewGuid(),
            Label = tableLabel.ToUpperInvariant(),
            MaxTeams = 2
        };
        venue.Tables.Add(newTable);
        return newTable;
    }
    
    /// <summary>
    /// Ensure a venue has at least one table. Creates default if none exist.
    /// </summary>
    public static void EnsureHasTable(Venue venue)
    {
        if (!venue.Tables.Any())
        {
            venue.Tables.Add(new VenueTable
            {
                Id = Guid.NewGuid(),
                Label = DefaultTableLabel,
                MaxTeams = 2
            });
        }
    }
}
