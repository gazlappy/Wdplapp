using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Detects and resolves home-table clashes among unplayed fixtures.
///
/// A clash occurs when two or more unplayed fixtures on the same date have home teams
/// that share the same registered home venue+table (team.VenueId + team.TableId).
/// This is the real constraint: a physical pool table can only host one match per night.
///
/// The fixture's own VenueId/TableId is NOT used for detection because the generator
/// can hand out different table IDs to fixtures even when the home teams both claim
/// the same physical table — masking the true conflict.
///
/// Played and cancelled fixtures are never touched.
/// </summary>
public static class FixtureClashResolverService
{
    public sealed class ClashResolution
    {
        public Fixture Fixture { get; init; } = null!;
        public string Description { get; init; } = "";
    }

    public sealed class ResolveResult
    {
        public List<string> Unresolved { get; } = new();
        public List<ClashResolution> Resolved { get; } = new();
    }

    // ── Full reschedule ────────────────────────────────────────────────────────

    public sealed class FullRescheduleResult
    {
        /// <summary>Number of fixtures that were successfully scheduled.</summary>
        public int ScheduledCount { get; set; }

        /// <summary>Summary of every date change made (for the preview dialog).</summary>
        public List<string> Changes { get; } = new();

        /// <summary>Matchups that could not be placed (not enough nights).</summary>
        public List<string> Unschedulable { get; } = new();

        /// <summary>Round-by-round breakdown for diagnosis.</summary>
        public List<string> RoundDiagnostics { get; } = new();
    }

    /// <summary>
    /// Re-distributes unplayed fixtures so every team plays exactly once per night,
    /// with the second half mirroring the first half.
    ///
    /// Season structure:
    ///   firstHalfSeasonNights[i]  ↔  secondHalfSeasonNights[i]
    ///   - Played first-half nights: their return legs (orphaned mirrors) go on the
    ///     corresponding second-half night.
    ///   - Unplayed first-half nights: schedule remaining first-half fixtures using
    ///     greedy edge-colouring (one match per team, no table clash), then put the
    ///     return leg of each scheduled fixture on the corresponding second-half night.
    ///
    /// Played/cancelled fixtures are never touched.
    /// </summary>
    public static FullRescheduleResult RescheduleRemaining(
        List<Fixture> allFixtures,
        List<Team> teams)
    {
        var result     = new FullRescheduleResult();
        var teamLookup = teams.ToDictionary(t => t.Id);

        var played   = allFixtures.Where(f => !IsUnplayed(f)).ToList();
        var unplayed = allFixtures.Where(IsUnplayed).ToList();
        if (unplayed.Count == 0) return result;

        var kickoff = unplayed
            .GroupBy(f => f.Date.TimeOfDay)
            .OrderByDescending(g => g.Count())
            .First().Key;

        // All distinct season nights (played + unplayed), split at midpoint
        var allSeasonNights = allFixtures
            .Select(f => f.Date.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        int mid = allSeasonNights.Count / 2;
        var firstHalfSeasonNights  = allSeasonNights.Take(mid).ToList();
        var secondHalfSeasonNights = allSeasonNights.Skip(mid).ToList();

        // Quick lookup: is a night in first or second half?
        var firstHalfNightSet  = new HashSet<DateTime>(firstHalfSeasonNights);
        var secondHalfNightSet = new HashSet<DateTime>(secondHalfSeasonNights);

        // Played fixtures indexed by date for quick lookup
        var playedByNight = played
            .GroupBy(f => f.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // All unplayed fixtures by exact matchup direction
        var byMatchup = new Dictionary<(Guid, Guid), Fixture>();
        foreach (var fx in unplayed)
            byMatchup[(fx.HomeTeamId, fx.AwayTeamId)] = fx;

        var playedPairs = new HashSet<(Guid, Guid)>(played.Select(f => (f.HomeTeamId, f.AwayTeamId)));

        var unplayedFirstHalfNights = firstHalfSeasonNights
            .Where(n => !playedByNight.ContainsKey(n))
            .ToList();

        // ── Classify every unplayed fixture into exactly one of: first-half or mirror ──
        // For each canonical pair {A,B}:
        //   Both unplayed  → ONE goes to first half, the OTHER is its paired mirror (2nd half)
        //                     Prefer to keep the one already on a first-half night as the 1H leg.
        //   Only one unplayed, reverse was played → orphaned mirror (return leg) → 2nd half only
        //   Only one unplayed, no played reverse  → treat as first-half standalone
        var toScheduleFirstHalf = new List<Fixture>();
        var orphanedMirrors     = new List<Fixture>();
        // pairedMirrorOf[firstHalfFx.Id] = its second-half mirror fixture
        var pairedMirrorOf = new Dictionary<Guid, Fixture>();

        var seenPairs = new HashSet<(Guid, Guid)>();
        foreach (var fx in unplayed)
        {
            var cp = CanonicalPair(fx.HomeTeamId, fx.AwayTeamId);
            if (!seenPairs.Add(cp)) continue;

            var (a, b) = cp;
            byMatchup.TryGetValue((a, b), out var fxAB);
            byMatchup.TryGetValue((b, a), out var fxBA);
            bool abPlayed = playedPairs.Contains((a, b));
            bool baPlayed = playedPairs.Contains((b, a));

            if (fxAB != null && fxBA != null)
            {
                // Both directions unplayed — assign one to 1H, other to 2H (mirror)
                // Prefer to keep the one currently on a first-half night as the 1H fixture
                Fixture firstHalfFx, mirrorFx;
                if (firstHalfNightSet.Contains(fxAB.Date.Date) || !firstHalfNightSet.Contains(fxBA.Date.Date))
                {
                    firstHalfFx = fxAB;
                    mirrorFx    = fxBA;
                }
                else
                {
                    firstHalfFx = fxBA;
                    mirrorFx    = fxAB;
                }
                toScheduleFirstHalf.Add(firstHalfFx);
                pairedMirrorOf[firstHalfFx.Id] = mirrorFx;
            }
            else if (fxAB != null && baPlayed)
            {
                orphanedMirrors.Add(fxAB); // return leg of a played fixture
            }
            else if (fxBA != null && abPlayed)
            {
                orphanedMirrors.Add(fxBA); // return leg of a played fixture
            }
            else if (fxAB != null)
            {
                toScheduleFirstHalf.Add(fxAB);
            }
            else if (fxBA != null)
            {
                toScheduleFirstHalf.Add(fxBA);
            }
        }

        result.RoundDiagnostics.Add(
            $"Total season nights: {allSeasonNights.Count}  Midpoint: {(secondHalfSeasonNights.Count > 0 ? secondHalfSeasonNights[0].ToString("ddd dd MMM") : "?")}");
        result.RoundDiagnostics.Add(
            $"1H season nights: {firstHalfSeasonNights.Count}  2H season nights: {secondHalfSeasonNights.Count}");
        result.RoundDiagnostics.Add(
            $"Unplayed 1H nights: {unplayedFirstHalfNights.Count}  Orphaned mirrors: {orphanedMirrors.Count}  1H fixtures to schedule: {toScheduleFirstHalf.Count}  Paired mirrors: {pairedMirrorOf.Count}");

        // ── Schedule first-half fixtures onto unplayed first-half nights ──
        // For each night, greedily pick fixtures (team appears at most once, no table clash)
        var remaining      = new List<Fixture>(toScheduleFirstHalf);
        // nightPairings[firstHalfNight] = list of fixtures scheduled that night
        var nightPairings  = new Dictionary<DateTime, List<Fixture>>();

        foreach (var night in unplayedFirstHalfNights)
        {
            var scheduled    = new List<Fixture>();
            var teamsThisNight  = new HashSet<Guid>();
            var tablesThisNight = new HashSet<(Guid, Guid?)>();

            foreach (var fx in remaining.ToList())
            {
                if (teamsThisNight.Contains(fx.HomeTeamId) ||
                    teamsThisNight.Contains(fx.AwayTeamId))
                    continue;

                var homeTeam = teamLookup.TryGetValue(fx.HomeTeamId, out var ht) ? ht : null;
                if (homeTeam?.VenueId != null)
                {
                    var tKey = (homeTeam.VenueId.Value, homeTeam.TableId);
                    if (tablesThisNight.Contains(tKey)) continue;
                    tablesThisNight.Add(tKey);
                }

                scheduled.Add(fx);
                teamsThisNight.Add(fx.HomeTeamId);
                teamsThisNight.Add(fx.AwayTeamId);
                remaining.Remove(fx);
            }

            nightPairings[night] = scheduled;

            var diagMatches = string.Join(", ", scheduled.Select(fx =>
            {
                var hn = teamLookup.TryGetValue(fx.HomeTeamId, out var htt) ? htt.Name ?? "?" : "?";
                var an = teamLookup.TryGetValue(fx.AwayTeamId, out var att) ? att.Name ?? "?" : "?";
                return $"{hn} v {an}";
            }));
            result.RoundDiagnostics.Add($"1H [{night:ddd dd MMM}]: {diagMatches}");
        }

        // Unschedulable first-half fixtures
        foreach (var fx in remaining)
        {
            var hn = teamLookup.TryGetValue(fx.HomeTeamId, out var ht) ? ht.Name ?? "?" : "?";
            var an = teamLookup.TryGetValue(fx.AwayTeamId, out var at) ? at.Name ?? "?" : "?";
            result.Unschedulable.Add($"{hn} vs {an}");
        }

        // ── Build second-half schedule ──
        // For each first-half season night[i], its mirror night is secondHalfSeasonNights[i].
        // Fixtures on mirror night = reverse of what played/scheduled on first-half night[i].
        //
        // Mirror placement is clash-aware: a mirror is placed on its preferred night
        // only if no team is already booked that night AND no other home team that night
        // shares the same registered (VenueId, TableId). If the preferred night clashes,
        // the mirror is moved to the nearest other second-half night that has room.
        result.RoundDiagnostics.Add("── Second half ──");

        // Per-second-half-night occupancy, seeded from any already-played fixtures that
        // happen to fall on a second-half night.
        var secondHalfTeams  = new Dictionary<DateTime, HashSet<Guid>>();
        var secondHalfTables = new Dictionary<DateTime, HashSet<(Guid, Guid?)>>();
        foreach (var night in secondHalfSeasonNights)
        {
            secondHalfTeams[night]  = new HashSet<Guid>();
            secondHalfTables[night] = new HashSet<(Guid, Guid?)>();

            if (playedByNight.TryGetValue(night, out var pfx))
            {
                foreach (var pf in pfx)
                {
                    secondHalfTeams[night].Add(pf.HomeTeamId);
                    secondHalfTeams[night].Add(pf.AwayTeamId);
                    if (teamLookup.TryGetValue(pf.HomeTeamId, out var pht) && pht.VenueId.HasValue)
                        secondHalfTables[night].Add((pht.VenueId.Value, pht.TableId));
                }
            }
        }

        // Collect every mirror with its preferred (ideal) second-half night.
        var mirrorsToPlace = new List<(Fixture mirrorFx, DateTime preferredNight, DateTime firstNight)>();
        for (int i = 0; i < firstHalfSeasonNights.Count && i < secondHalfSeasonNights.Count; i++)
        {
            var firstNight  = firstHalfSeasonNights[i];
            var secondNight = secondHalfSeasonNights[i];

            List<Fixture>? firstNightFixtures = null;
            if (playedByNight.TryGetValue(firstNight, out var pf))
                firstNightFixtures = pf;
            else
                nightPairings.TryGetValue(firstNight, out firstNightFixtures);

            if (firstNightFixtures == null || firstNightFixtures.Count == 0)
            {
                result.RoundDiagnostics.Add($"2H [{secondNight:ddd dd MMM}] (mirror of {firstNight:ddd dd MMM}): no first-half fixtures found");
                continue;
            }

            foreach (var firstFx in firstNightFixtures)
            {
                Fixture? mirrorFx = null;
                if (!pairedMirrorOf.TryGetValue(firstFx.Id, out mirrorFx))
                    byMatchup.TryGetValue((firstFx.AwayTeamId, firstFx.HomeTeamId), out mirrorFx);

                if (mirrorFx == null)
                {
                    result.RoundDiagnostics.Add(
                        $"  no mirror found for {teamLookup.GetValueOrDefault(firstFx.HomeTeamId)?.Name} v {teamLookup.GetValueOrDefault(firstFx.AwayTeamId)?.Name}");
                    continue;
                }

                mirrorsToPlace.Add((mirrorFx, secondNight, firstNight));
            }
        }

        // Try to place each mirror at its preferred night, falling back to the nearest
        // other second-half night if there's a team-clash or a home-table clash.
        var mirrorPlacements   = secondHalfSeasonNights.ToDictionary(n => n, _ => new List<Fixture>());
        var mirrorMovedFrom    = new Dictionary<Guid, DateTime>(); // mirrorFx.Id → preferred night (only if displaced)

        bool TryPlaceMirror(Fixture mirrorFx, DateTime night)
        {
            if (!secondHalfTeams.TryGetValue(night, out var teamsBooked)) return false;
            var tablesBooked = secondHalfTables[night];

            if (teamsBooked.Contains(mirrorFx.HomeTeamId) ||
                teamsBooked.Contains(mirrorFx.AwayTeamId))
                return false;

            (Guid, Guid?)? tableKey = null;
            if (teamLookup.TryGetValue(mirrorFx.HomeTeamId, out var mh) && mh.VenueId.HasValue)
            {
                tableKey = (mh.VenueId.Value, mh.TableId);
                if (tablesBooked.Contains(tableKey.Value)) return false;
            }

            teamsBooked.Add(mirrorFx.HomeTeamId);
            teamsBooked.Add(mirrorFx.AwayTeamId);
            if (tableKey.HasValue) tablesBooked.Add(tableKey.Value);
            mirrorPlacements[night].Add(mirrorFx);
            return true;
        }

        foreach (var (mirrorFx, preferredNight, _) in mirrorsToPlace)
        {
            if (TryPlaceMirror(mirrorFx, preferredNight)) continue;

            var alternatives = secondHalfSeasonNights
                .Where(n => n != preferredNight)
                .OrderBy(n => Math.Abs((n - preferredNight).TotalDays))
                .ToList();

            bool placed = false;
            foreach (var alt in alternatives)
            {
                if (TryPlaceMirror(mirrorFx, alt))
                {
                    mirrorMovedFrom[mirrorFx.Id] = preferredNight;
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                var hn = teamLookup.TryGetValue(mirrorFx.HomeTeamId, out var ht2) ? ht2.Name ?? "?" : "?";
                var an = teamLookup.TryGetValue(mirrorFx.AwayTeamId, out var at2) ? at2.Name ?? "?" : "?";
                result.Unschedulable.Add($"{hn} vs {an} (2nd half)");
            }
        }

        // Apply the mirror placements + diagnostics, in second-half night order.
        foreach (var night in secondHalfSeasonNights)
        {
            var fixtures = mirrorPlacements[night];
            if (fixtures.Count == 0)
            {
                // Already logged "no first-half fixtures found" above when applicable.
                continue;
            }

            foreach (var fx in fixtures)
            {
                var mHome = teamLookup.TryGetValue(fx.HomeTeamId, out var mht) ? mht : null;
                var note  = mirrorMovedFrom.TryGetValue(fx.Id, out var pref)
                    ? $" (2nd half — moved from mirror night {pref:ddd dd MMM} to avoid clash)"
                    : " (2nd half)";
                AssignFixture(fx, night, kickoff, mHome, result, teamLookup, note);
            }

            var diagMatches = string.Join(", ", fixtures.Select(fx =>
            {
                var hn = teamLookup.TryGetValue(fx.HomeTeamId, out var htt) ? htt.Name ?? "?" : "?";
                var an = teamLookup.TryGetValue(fx.AwayTeamId, out var att) ? att.Name ?? "?" : "?";
                return $"{hn} v {an}";
            }));
            result.RoundDiagnostics.Add($"2H [{night:ddd dd MMM}]: {diagMatches}");
        }

        // ── Assign first-half fixtures to their nights ──
        foreach (var (night, fixtures) in nightPairings)
            foreach (var fx in fixtures)
            {
                var homeTeam = teamLookup.TryGetValue(fx.HomeTeamId, out var ht) ? ht : null;
                AssignFixture(fx, night, kickoff, homeTeam, result, teamLookup, "");
            }

        return result;
    }

    private static void AssignFixture(
        Fixture fx, DateTime night, TimeSpan kickoff,
        Team? homeTeam, FullRescheduleResult result,
        Dictionary<Guid, Team> teamLookup, string suffix)
    {
        var oldDate  = fx.Date;
        var homeName = homeTeam?.Name
                       ?? (teamLookup.TryGetValue(fx.HomeTeamId, out var ht) ? ht.Name ?? "?" : "?");
        var awayName = teamLookup.TryGetValue(fx.AwayTeamId, out var at) ? at.Name ?? "?" : "?";

        fx.Date = night.Add(kickoff);
        if (homeTeam?.VenueId != null) fx.VenueId = homeTeam.VenueId;
        if (homeTeam?.TableId != null) fx.TableId = homeTeam.TableId;
        fx.ModifiedDate = DateTime.UtcNow;

        result.ScheduledCount++;
        if (oldDate.Date != night)
            result.Changes.Add(
                $"{oldDate:ddd dd MMM} → {night:ddd dd MMM}:  {homeName} vs {awayName}{suffix}");
    }

    /// <summary>Returns a canonical (smaller, larger) Guid pair regardless of argument order.</summary>
    private static (Guid, Guid) CanonicalPair(Guid a, Guid b)
        => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Detects all home-table clashes among unplayed fixtures.
    /// Uses the HOME TEAM's registered venue+table, not the fixture's assigned venue/table.
    /// </summary>
    public static (List<string> clashes, string diagnostics) DetectClashes(
        List<Fixture> allFixtures, List<Venue> venues, List<Team> teams)
    {
        var messages = new List<string>();
        var unplayed = allFixtures.Where(IsUnplayed).ToList();
        int played   = allFixtures.Count - unplayed.Count;

        var teamLookup = teams.ToDictionary(t => t.Id);
        var diagLines  = new System.Text.StringBuilder();

        diagLines.AppendLine(
            $"Total: {allFixtures.Count}  Unplayed: {unplayed.Count}  " +
            $"Played/cancelled: {played}");

        int noHomeVenue = 0, multiCount = 0;

        // Key = (date, homeTeamVenueId, homeTeamTableId)
        // Two fixtures with home teams sharing the same venue+table on the same date = clash
        var groups = unplayed
            .Select(f =>
            {
                teamLookup.TryGetValue(f.HomeTeamId, out var homeTeam);
                return (fixture: f, homeTeam);
            })
            .Where(x =>
            {
                if (x.homeTeam?.VenueId == null) { noHomeVenue++; return false; }
                return true;
            })
            .GroupBy(x => (
                Date:    x.fixture.Date.Date,
                VenueId: x.homeTeam!.VenueId!.Value,
                TableId: x.homeTeam.TableId   // may be null — null = whole venue
            ))
            .OrderBy(g => g.Key.Date)
            .ThenBy(g => venues.FirstOrDefault(v => v.Id == g.Key.VenueId)?.Name);

        foreach (var g in groups)
        {
            if (g.Count() < 2) continue;

            multiCount++;
            var venue     = venues.FirstOrDefault(v => v.Id == g.Key.VenueId);
            var venueName = venue?.Name ?? $"(id={g.Key.VenueId})";
            var tableLabel = g.Key.TableId.HasValue
                ? (venue?.Tables?.FirstOrDefault(t => t.Id == g.Key.TableId)?.Label ?? g.Key.TableId.ToString()![..8])
                : "BAR/unspecified";

            var teamNames = string.Join(", ", g.Select(x => x.homeTeam?.Name ?? "?"));
            var msg = $"{g.Key.Date:ddd dd MMM} — {venueName} / {tableLabel}: " +
                      $"{g.Count()} home teams share this table ({teamNames})";

            messages.Add(msg);
            diagLines.AppendLine($"  CLASH: {msg}");
        }

        diagLines.AppendLine($"Home teams with no registered venue: {noHomeVenue} (cannot check)");
        diagLines.AppendLine($"Clash groups: {multiCount}");

        return (messages, diagLines.ToString().TrimEnd());
    }

    /// <summary>
    /// Resolves home-table clashes by moving the later-created fixture(s) to another
    /// date that already exists in the season schedule — never inventing new dates.
    ///
    /// For each clashing fixture the resolver looks through every existing match date
    /// (ordered by proximity to the original date) and picks the first one where:
    ///   1. The home team's registered table is free.
    ///   2. Neither team is already playing that night.
    /// </summary>
    public static ResolveResult Resolve(
        List<Fixture> allFixtures,
        List<Venue> venues,
        List<Team> teams,
        DayOfWeek matchDayOfWeek,   // kept for signature compat, no longer used
        int maxWeeksAhead = 26)     // kept for signature compat, no longer used
    {
        var result       = new ResolveResult();
        var teamLookup   = teams.ToDictionary(t => t.Id);
        var teamBookings = BuildTeamBookings(allFixtures);

        var unplayed = allFixtures.Where(IsUnplayed).ToList();

        // All distinct match dates already in the season (unplayed only — we don't
        // move things onto nights that are already fully played out)
        var existingDates = unplayed
            .Select(f => f.Date.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        // Build home-table bookings keyed on home team's registered venue+table
        var homeTableBookings = BuildHomeTableBookings(unplayed, teamLookup);

        // Find all clashing groups (home teams sharing same table on same night)
        var clashGroups = homeTableBookings
            .Where(kvp => kvp.Value.Count > 1)
            .ToList();

        foreach (var kvp in clashGroups)
        {
            var (date, venueId, tableId) = kvp.Key;
            var venue      = venues.FirstOrDefault(v => v.Id == venueId);
            var venueName  = venue?.Name ?? "venue";
            var tableLabel = tableId.HasValue
                ? (venue?.Tables?.FirstOrDefault(t => t.Id == tableId)?.Label ?? "table")
                : "unspecified table";

            // Keep the earliest-created fixture; try to move the rest
            var ordered = kvp.Value.OrderBy(f => f.CreatedDate).ThenBy(f => f.Id).ToList();

            for (int i = 1; i < ordered.Count; i++)
            {
                var fx       = ordered[i];
                var homeTeam = teamLookup.TryGetValue(fx.HomeTeamId, out var ht) ? ht : null;
                var homeKey  = (Date: fx.Date.Date,
                                VenueId: homeTeam?.VenueId ?? venueId,
                                TableId: homeTeam?.TableId ?? tableId);

                // Candidate dates: every existing match night except the current clashing one,
                // sorted by distance from the original date so we prefer nearby dates first
                var candidates = existingDates
                    .Where(d => d != fx.Date.Date)
                    .OrderBy(d => Math.Abs((d - fx.Date.Date).TotalDays))
                    .ToList();

                bool moved = false;
                foreach (var candidate in candidates)
                {
                    // Both teams must be free that night
                    if (IsTeamBooked(teamBookings, candidate, fx.HomeTeamId) ||
                        IsTeamBooked(teamBookings, candidate, fx.AwayTeamId))
                        continue;

                    // Home team's registered table must be free that night
                    var candidateHomeKey = (candidate, homeTeam?.VenueId ?? venueId, homeTeam?.TableId ?? tableId);
                    if (homeTableBookings.TryGetValue(candidateHomeKey, out var occupants) && occupants.Count > 0)
                        continue;

                    var oldDate = fx.Date;
                    RemoveTeamBookings(teamBookings, fx);
                    RemoveHomeTableBooking(homeTableBookings, fx, teamLookup);

                    fx.Date = candidate.Add(fx.Date.TimeOfDay);
                    if (homeTeam?.VenueId != null) fx.VenueId = homeTeam.VenueId;
                    if (homeTeam?.TableId != null) fx.TableId = homeTeam.TableId;
                    fx.ModifiedDate = DateTime.UtcNow;

                    AddTeamBookings(teamBookings, fx);
                    AddHomeTableBooking(homeTableBookings, fx, teamLookup);

                    result.Resolved.Add(new ClashResolution
                    {
                        Fixture = fx,
                        Description =
                            $"Moved from {oldDate:ddd dd MMM} → {fx.Date:ddd dd MMM} " +
                            $"({homeTeam?.Name ?? "?"} home at {venueName}/{tableLabel})"
                    });
                    moved = true;
                    break;
                }

                if (!moved)
                {
                    result.Unresolved.Add(
                        $"{fx.Date:ddd dd MMM} — {venueName}/{tableLabel} " +
                        $"({homeTeam?.Name ?? "?"}): no existing match night has a free slot — reschedule manually");
                }
            }
        }

        return result;
    }
                        // ── Helpers ────────────────────────────────────────────────────────────────

                        private static bool IsUnplayed(Fixture f)
        => f.Frames.Count == 0 && f.CancelledByTeam == FrameWinner.None;

    private static DateTime NextMatchNight(DateTime fromDate, DayOfWeek matchDay, int weeksAhead)
    {
        int diff = ((int)matchDay - (int)fromDate.DayOfWeek + 7) % 7;
        if (diff == 0) diff = 7;
        return fromDate.AddDays(diff + (weeksAhead - 1) * 7);
    }

    // Home-table bookings: (date, homeTeamVenueId, homeTeamTableId) → list of fixtures
    private static Dictionary<(DateTime Date, Guid VenueId, Guid? TableId), List<Fixture>>
        BuildHomeTableBookings(List<Fixture> fixtures, Dictionary<Guid, Team> teamLookup)
    {
        var d = new Dictionary<(DateTime, Guid, Guid?), List<Fixture>>();
        foreach (var f in fixtures)
        {
            if (!teamLookup.TryGetValue(f.HomeTeamId, out var homeTeam)) continue;
            if (homeTeam.VenueId == null) continue;
            var key = (f.Date.Date, homeTeam.VenueId.Value, homeTeam.TableId);
            if (!d.ContainsKey(key)) d[key] = new List<Fixture>();
            d[key].Add(f);
        }
        return d;
    }

    private static void RemoveHomeTableBooking(
        Dictionary<(DateTime, Guid, Guid?), List<Fixture>> d,
        Fixture fx, Dictionary<Guid, Team> teamLookup)
    {
        if (!teamLookup.TryGetValue(fx.HomeTeamId, out var ht) || ht.VenueId == null) return;
        var key = (fx.Date.Date, ht.VenueId.Value, ht.TableId);
        if (d.TryGetValue(key, out var list)) list.Remove(fx);
    }

    private static void AddHomeTableBooking(
        Dictionary<(DateTime, Guid, Guid?), List<Fixture>> d,
        Fixture fx, Dictionary<Guid, Team> teamLookup)
    {
        if (!teamLookup.TryGetValue(fx.HomeTeamId, out var ht) || ht.VenueId == null) return;
        var key = (fx.Date.Date, ht.VenueId.Value, ht.TableId);
        if (!d.ContainsKey(key)) d[key] = new List<Fixture>();
        d[key].Add(fx);
    }

    private static Dictionary<(DateTime, Guid), HashSet<Guid>> BuildTeamBookings(List<Fixture> fixtures)
    {
        var d = new Dictionary<(DateTime, Guid), HashSet<Guid>>();
        foreach (var f in fixtures)
        {
            foreach (var teamId in new[] { f.HomeTeamId, f.AwayTeamId })
            {
                var key = (f.Date.Date, teamId);
                if (!d.ContainsKey(key)) d[key] = new HashSet<Guid>();
                d[key].Add(f.Id);
            }
        }
        return d;
    }

    private static bool IsTeamBooked(Dictionary<(DateTime, Guid), HashSet<Guid>> d, DateTime date, Guid teamId)
        => d.TryGetValue((date, teamId), out var ids) && ids.Count > 0;

    private static void RemoveTeamBookings(Dictionary<(DateTime, Guid), HashSet<Guid>> d, Fixture fx)
    {
        foreach (var id in new[] { fx.HomeTeamId, fx.AwayTeamId })
            if (d.TryGetValue((fx.Date.Date, id), out var s)) s.Remove(fx.Id);
    }

    private static void AddTeamBookings(Dictionary<(DateTime, Guid), HashSet<Guid>> d, Fixture fx)
    {
        foreach (var id in new[] { fx.HomeTeamId, fx.AwayTeamId })
        {
            var key = (fx.Date.Date, id);
            if (!d.ContainsKey(key)) d[key] = new HashSet<Guid>();
            d[key].Add(fx.Id);
        }
    }

    /// <summary>
    /// Returns a canonical pair key for two teams regardless of which is home/away,
    /// so that A-vs-B and B-vs-A map to the same key.
    /// </summary>
    private static (DateTime date, Guid minId, Guid maxId) MakePairKey(DateTime date, Guid a, Guid b)
        => a.CompareTo(b) <= 0 ? (date, a, b) : (date, b, a);

    // Kept for signature compatibility — no longer used internally
    private static string? GetClashMessage(
        List<Fixture> group, DateTime date, Guid venueId, List<Venue> venues) => null;
}
