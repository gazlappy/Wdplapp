using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Exports a snapshot of a season's scheduling data to a JSON file (for offline
/// analysis / hand-crafted rescheduling) and applies a corresponding plan back
/// onto the league's unplayed fixtures.
///
/// Played and cancelled fixtures are NEVER modified by Apply.
/// </summary>
public static class ScheduleSnapshotService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public sealed class Snapshot
    {
        public SeasonInfo Season { get; set; } = new();
        public List<VenueInfo> Venues { get; set; } = new();
        public List<TeamInfo> Teams { get; set; } = new();
        public List<string> MatchNights { get; set; } = new();    // yyyy-MM-dd
        public List<string> BlackoutDates { get; set; } = new();  // yyyy-MM-dd
        public List<PlayedFixtureInfo> PlayedFixtures { get; set; } = new();
        public List<UnplayedFixtureInfo> UnplayedFixtures { get; set; } = new();
        public List<string> SharedHomeTableWarnings { get; set; } = new();
    }

    public sealed class SeasonInfo
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? MatchDayOfWeek { get; set; }
        public string? Kickoff { get; set; }   // HH:mm
        public string? StartDate { get; set; } // yyyy-MM-dd
        public string? EndDate { get; set; }   // yyyy-MM-dd
    }

    public sealed class VenueInfo
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public List<TableInfo> Tables { get; set; } = new();
    }

    public sealed class TableInfo
    {
        public Guid Id { get; set; }
        public string? Label { get; set; }
    }

    public sealed class TeamInfo
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public Guid? DivisionId { get; set; }
        public string? DivisionName { get; set; }
        public Guid? VenueId { get; set; }
        public string? VenueName { get; set; }
        public Guid? TableId { get; set; }
        public string? TableLabel { get; set; }
    }

    public sealed class PlayedFixtureInfo
    {
        public Guid Id { get; set; }
        public string? Date { get; set; }       // yyyy-MM-dd
        public Guid HomeTeamId { get; set; }
        public string? HomeTeam { get; set; }
        public Guid AwayTeamId { get; set; }
        public string? AwayTeam { get; set; }
        public Guid? VenueId { get; set; }
        public string? Venue { get; set; }
        public Guid? TableId { get; set; }
        public string? Table { get; set; }
        public bool Cancelled { get; set; }
    }

    public sealed class UnplayedFixtureInfo
    {
        public Guid Id { get; set; }
        public string? CurrentDate { get; set; } // yyyy-MM-dd
        public Guid HomeTeamId { get; set; }
        public string? HomeTeam { get; set; }
        public Guid AwayTeamId { get; set; }
        public string? AwayTeam { get; set; }
        public Guid? CurrentVenueId { get; set; }
        public string? CurrentVenue { get; set; }
        public Guid? CurrentTableId { get; set; }
        public string? CurrentTable { get; set; }
    }

    /// <summary>The shape the AI / human-edited "plan" file must follow.</summary>
    public sealed class Plan
    {
        public List<Assignment> Assignments { get; set; } = new();
    }

    public sealed class Assignment
    {
        public Guid FixtureId { get; set; }
        /// <summary>yyyy-MM-dd OR yyyy-MM-ddTHH:mm. Time defaults to existing fixture's time.</summary>
        public string? Date { get; set; }
        public Guid? VenueId { get; set; }
        public Guid? TableId { get; set; }
    }

    public sealed class ApplyResult
    {
        public int Applied { get; set; }
        public int Skipped { get; set; }
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public static Snapshot BuildSnapshot(LeagueData data, Guid seasonId)
    {
        var season = data.Seasons.FirstOrDefault(s => s.Id == seasonId)
                     ?? throw new InvalidOperationException("Season not found.");

        var teams     = data.Teams   .Where(t => t.SeasonId == seasonId).ToList();
        var fixtures  = data.Fixtures.Where(f => f.SeasonId == seasonId).ToList();
        var divisions = data.Divisions.Where(d => d.SeasonId == seasonId).ToList();
        // Venues are global; include all because teams may reference any venue.
        var venues    = data.Venues.ToList();

        var divisionLookup = divisions.ToDictionary(d => d.Id, d => d.Name ?? "");
        var venueLookup    = venues   .ToDictionary(v => v.Id);
        var teamLookup     = teams    .ToDictionary(t => t.Id);

        string? VenueName(Guid? id) =>
            id.HasValue && venueLookup.TryGetValue(id.Value, out var v) ? v.Name : null;

        string? TableLabel(Guid? venueId, Guid? tableId)
        {
            if (!venueId.HasValue || !tableId.HasValue) return null;
            if (!venueLookup.TryGetValue(venueId.Value, out var v)) return null;
            return v.Tables?.FirstOrDefault(t => t.Id == tableId.Value)?.Label;
        }

        string? TeamName(Guid id) =>
            teamLookup.TryGetValue(id, out var t) ? t.Name : null;

        // Pick most common kickoff time from existing fixtures, else season default.
        TimeSpan kickoff;
        var kickoffs = fixtures.Select(f => f.Date.TimeOfDay).Where(t => t > TimeSpan.Zero).ToList();
        kickoff = kickoffs.Count > 0
            ? kickoffs.GroupBy(t => t).OrderByDescending(g => g.Count()).First().Key
            : season.MatchStartTime;

        var snap = new Snapshot
        {
            Season = new SeasonInfo
            {
                Id             = season.Id,
                Name           = season.Name,
                MatchDayOfWeek = season.MatchDayOfWeek.ToString(),
                Kickoff        = kickoff.ToString(@"hh\:mm"),
                StartDate      = season.StartDate.ToString("yyyy-MM-dd"),
                EndDate        = season.EndDate.ToString("yyyy-MM-dd"),
            },
            Venues = venues.Select(v => new VenueInfo
            {
                Id     = v.Id,
                Name   = v.Name,
                Tables = (v.Tables ?? new List<VenueTable>())
                            .Select(t => new TableInfo { Id = t.Id, Label = t.Label }).ToList()
            }).ToList(),
            Teams = teams.Select(t => new TeamInfo
            {
                Id           = t.Id,
                Name         = t.Name,
                DivisionId   = t.DivisionId,
                DivisionName = t.DivisionId.HasValue && divisionLookup.TryGetValue(t.DivisionId.Value, out var dn) ? dn : null,
                VenueId      = t.VenueId,
                VenueName    = VenueName(t.VenueId),
                TableId      = t.TableId,
                TableLabel   = TableLabel(t.VenueId, t.TableId),
            }).OrderBy(t => t.Name).ToList(),
            MatchNights = fixtures.Select(f => f.Date.Date)
                                  .Distinct()
                                  .OrderBy(d => d)
                                  .Select(d => d.ToString("yyyy-MM-dd"))
                                  .ToList(),
            BlackoutDates = (season.BlackoutDates ?? new List<DateTime>())
                                  .Select(d => d.Date)
                                  .OrderBy(d => d)
                                  .Select(d => d.ToString("yyyy-MM-dd"))
                                  .ToList(),
        };

        foreach (var f in fixtures.OrderBy(f => f.Date))
        {
            bool played = f.Frames.Count > 0 || f.CancelledByTeam != FrameWinner.None;
            if (played)
            {
                snap.PlayedFixtures.Add(new PlayedFixtureInfo
                {
                    Id          = f.Id,
                    Date        = f.Date.ToString("yyyy-MM-dd"),
                    HomeTeamId  = f.HomeTeamId,
                    HomeTeam    = TeamName(f.HomeTeamId),
                    AwayTeamId  = f.AwayTeamId,
                    AwayTeam    = TeamName(f.AwayTeamId),
                    VenueId     = f.VenueId,
                    Venue       = VenueName(f.VenueId),
                    TableId     = f.TableId,
                    Table       = TableLabel(f.VenueId, f.TableId),
                    Cancelled   = f.CancelledByTeam != FrameWinner.None,
                });
            }
            else
            {
                snap.UnplayedFixtures.Add(new UnplayedFixtureInfo
                {
                    Id              = f.Id,
                    CurrentDate     = f.Date.ToString("yyyy-MM-dd"),
                    HomeTeamId      = f.HomeTeamId,
                    HomeTeam        = TeamName(f.HomeTeamId),
                    AwayTeamId      = f.AwayTeamId,
                    AwayTeam        = TeamName(f.AwayTeamId),
                    CurrentVenueId  = f.VenueId,
                    CurrentVenue    = VenueName(f.VenueId),
                    CurrentTableId  = f.TableId,
                    CurrentTable    = TableLabel(f.VenueId, f.TableId),
                });
            }
        }

        // Helpful info: which teams legitimately share a registered home venue+table.
        var sharedGroups = teams
            .Where(t => t.VenueId.HasValue)
            .GroupBy(t => (t.VenueId!.Value, t.TableId))
            .Where(g => g.Count() > 1);
        foreach (var g in sharedGroups)
        {
            var venueName = VenueName(g.Key.Value) ?? g.Key.Value.ToString();
            var tableLbl  = g.Key.TableId.HasValue
                ? (TableLabel(g.Key.Value, g.Key.TableId) ?? g.Key.TableId.ToString())
                : "(unspecified table)";
            var names     = string.Join(", ", g.Select(t => t.Name));
            snap.SharedHomeTableWarnings.Add($"{venueName} / {tableLbl}: {names}");
        }

        return snap;
    }

    public static async Task<string> ExportAsync(LeagueData data, Guid seasonId, string outputFolder)
    {
        if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

        var snap   = BuildSnapshot(data, seasonId);
        var json   = JsonSerializer.Serialize(snap, JsonOpts);
        var stamp  = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var safe   = MakeSafeFileName(snap.Season.Name ?? "season");
        var file   = Path.Combine(outputFolder, $"schedule-snapshot-{safe}-{stamp}.json");
        await File.WriteAllTextAsync(file, json);
        return file;
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '-');
        return name.Replace(' ', '_');
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Apply a plan (deserialized or raw JSON) onto the league's unplayed fixtures.
    /// Played/cancelled fixtures are skipped silently (they are NEVER modified).
    /// </summary>
    public static ApplyResult Apply(LeagueData data, string planJson)
    {
        var result = new ApplyResult();

        Plan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<Plan>(planJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Could not parse plan JSON: {ex.Message}");
            return result;
        }

        if (plan?.Assignments == null || plan.Assignments.Count == 0)
        {
            result.Errors.Add("Plan contained no assignments.");
            return result;
        }

        var fixturesById = data.Fixtures.ToDictionary(f => f.Id);
        var teamLookup   = data.Teams.ToDictionary(t => t.Id);
        var venueLookup  = data.Venues.ToDictionary(v => v.Id);

        // Validation pass: detect team / home-table clashes inside the plan itself
        // (combined with played fixtures, which stay put).
        var nightTeams  = new Dictionary<DateTime, HashSet<Guid>>();
        var nightTables = new Dictionary<DateTime, HashSet<(Guid, Guid?)>>();

        // Seed with played fixtures
        foreach (var f in data.Fixtures)
        {
            bool played = f.Frames.Count > 0 || f.CancelledByTeam != FrameWinner.None;
            if (!played) continue;
            var d = f.Date.Date;
            if (!nightTeams.ContainsKey(d))  nightTeams[d]  = new HashSet<Guid>();
            if (!nightTables.ContainsKey(d)) nightTables[d] = new HashSet<(Guid, Guid?)>();
            nightTeams[d].Add(f.HomeTeamId);
            nightTeams[d].Add(f.AwayTeamId);
            if (teamLookup.TryGetValue(f.HomeTeamId, out var ht) && ht.VenueId.HasValue)
                nightTables[d].Add((ht.VenueId.Value, ht.TableId));
        }

        // First pass: pre-validate the assignments
        var parsedAssignments = new List<(Assignment a, Fixture fx, DateTime newDate)>();
        foreach (var a in plan.Assignments)
        {
            if (!fixturesById.TryGetValue(a.FixtureId, out var fx))
            {
                result.Warnings.Add($"Fixture {a.FixtureId} not found — skipped.");
                result.Skipped++;
                continue;
            }
            bool played = fx.Frames.Count > 0 || fx.CancelledByTeam != FrameWinner.None;
            if (played)
            {
                result.Warnings.Add(
                    $"Fixture {fx.Id} is played/cancelled — skipped (played fixtures are never modified).");
                result.Skipped++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(a.Date))
            {
                result.Errors.Add($"Fixture {fx.Id}: missing date.");
                result.Skipped++;
                continue;
            }

            if (!TryParseDate(a.Date, fx.Date.TimeOfDay, out var newDateTime))
            {
                result.Errors.Add($"Fixture {fx.Id}: invalid date '{a.Date}'.");
                result.Skipped++;
                continue;
            }

            parsedAssignments.Add((a, fx, newDateTime));
        }

        // Add the proposed assignments to the night maps for clash checking
        foreach (var (a, fx, newDateTime) in parsedAssignments)
        {
            var d = newDateTime.Date;
            if (!nightTeams.ContainsKey(d))  nightTeams[d]  = new HashSet<Guid>();
            if (!nightTables.ContainsKey(d)) nightTables[d] = new HashSet<(Guid, Guid?)>();

            if (!nightTeams[d].Add(fx.HomeTeamId))
                result.Warnings.Add($"Plan clash: team {NameOf(teamLookup, fx.HomeTeamId)} already booked on {d:ddd dd MMM} (fixture {fx.Id}).");
            if (!nightTeams[d].Add(fx.AwayTeamId))
                result.Warnings.Add($"Plan clash: team {NameOf(teamLookup, fx.AwayTeamId)} already booked on {d:ddd dd MMM} (fixture {fx.Id}).");

            // Use the assigned home venue/table if provided, otherwise the home team's registered one.
            Guid? venueId = a.VenueId;
            Guid? tableId = a.TableId;
            if (!venueId.HasValue && teamLookup.TryGetValue(fx.HomeTeamId, out var ht))
            {
                venueId = ht.VenueId;
                tableId = ht.TableId;
            }
            if (venueId.HasValue)
            {
                var key = (venueId.Value, tableId);
                if (!nightTables[d].Add(key))
                {
                    var venueName = venueLookup.TryGetValue(venueId.Value, out var v) ? v.Name : venueId.ToString();
                    result.Warnings.Add(
                        $"Plan clash: home table {venueName} already booked on {d:ddd dd MMM} (fixture {fx.Id}).");
                }
            }
        }

        // Second pass: apply
        foreach (var (a, fx, newDateTime) in parsedAssignments)
        {
            fx.Date = newDateTime;
            if (a.VenueId.HasValue) fx.VenueId = a.VenueId;
            if (a.TableId.HasValue) fx.TableId = a.TableId;
            fx.ModifiedDate = DateTime.UtcNow;
            result.Applied++;
        }

        return result;
    }

    private static string NameOf(Dictionary<Guid, Team> lookup, Guid id) =>
        lookup.TryGetValue(id, out var t) ? (t.Name ?? id.ToString()) : id.ToString();

    private static bool TryParseDate(string s, TimeSpan defaultTime, out DateTime result)
    {
        s = s.Trim();
        if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal, out result))
        {
            // If only a date was given, attach the default time.
            if (result.TimeOfDay == TimeSpan.Zero && !s.Contains('T') && !s.Contains(':'))
                result = result.Date.Add(defaultTime);
            return true;
        }
        return false;
    }
}
