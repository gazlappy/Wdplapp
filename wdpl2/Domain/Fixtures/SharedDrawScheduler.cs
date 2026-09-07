using Wdpl2.Models;

namespace Wdpl2.Services;

internal static class SharedDrawScheduler
{
    internal static List<Fixture> Generate(LeagueData data, Guid seasonId, IReadOnlyList<DateTime> dates, int legs, TimeSpan kickoff)
    {
        var teams = data.Teams.Where(t => t.SeasonId == seasonId).ToList();
        var divisions = teams.GroupBy(t => t.DivisionId!.Value).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).ToList();
        int maximum = divisions.Max(g => g.Count());
        int size = maximum + maximum % 2;
        int rounds = size - 1;
        int required = checked(rounds * legs);
        if (dates.Count < required)
            throw new InvalidOperationException($"The shared draw needs {required} match nights, but only {dates.Count} are available. Extend the season or review blackout dates. Existing fixtures have not been changed.");

        var expanded = NumberedFixtureDraw.Create(size, legs)
            .Select(p => (night: p.Round, home: p.Home - 1, away: p.Away - 1)).ToList();
        var placements = divisions.ToDictionary(g => g.Key, _ => new Team?[size]);
        var assigned = new HashSet<Guid>();
        var tableCounts = teams.GroupBy(t => (t.VenueId, t.TableId)).ToDictionary(g => g.Key, g => g.Count());
        // Each team must host at least floor(legs / 2) matches per opponent.
        foreach (var table in teams.GroupBy(t => (t.VenueId, t.TableId)))
        {
            if (table.Count() > 2)
                throw new InvalidOperationException($"The odd/even table-partner rule supports at most two teams on one table: {string.Join(", ", table.Select(t => t.Name))}. Review home-table assignments. Existing fixtures have not been changed.");
            int minimum = table.Sum(t => (divisions.Single(g => g.Key == t.DivisionId).Count() - 1) * (legs / 2));
            if (minimum > required)
                throw new InvalidOperationException($"Home table capacity is insufficient for {string.Join(", ", table.Select(t => t.Name))}: at least {minimum} home matches need {required} nights. Existing fixtures have not been changed.");
        }

        bool HasClash()
        {
            var bookings = new HashSet<(int night, Guid? venue, Guid? table)>();
            foreach (var division in placements.Values)
                foreach (var p in expanded)
                {
                    var home = division[p.home];
                    var away = division[p.away];
                    if (home != null && away != null && !bookings.Add((p.night, home.VenueId, home.TableId))) return true;
                }
            return false;
        }
        int attempts = 0;
        bool exhausted = false;
        var assignedSlots = new Dictionary<Guid, int>();
        bool Search()
        {
            if (++attempts > 250000) { exhausted = true; return false; }
            if (assigned.Count == teams.Count) return true;
            Team? next = null;
            List<int>? candidates = null;
            foreach (var team in teams.Where(t => !assigned.Contains(t.Id))
                .OrderByDescending(t => tableCounts[(t.VenueId, t.TableId)])
                .ThenByDescending(t => teams.Count(a => assigned.Contains(a.Id) && a.VenueId == t.VenueId && a.TableId == t.TableId))
                .ThenBy(t => t.Name).ThenBy(t => t.Id))
            {
                var slots = placements[team.DivisionId!.Value];
                var available = new List<int>();
                // An odd-sized largest division's final slot is a BYE in every division.
                for (int slot = 0; slot < maximum; slot++)
                {
                    if (slots[slot] != null) continue;
                    if (teams.Any(partner => partner.Id != team.Id && partner.VenueId == team.VenueId && partner.TableId == team.TableId
                        && assignedSlots.TryGetValue(partner.Id, out int other) && !NumberedFixtureDraw.AreTablePartners(slot + 1, other + 1))) continue;
                    slots[slot] = team;
                    if (!HasClash()) available.Add(slot);
                    slots[slot] = null;
                }
                if (available.Count == 0) return false;
                if (candidates == null || available.Count < candidates.Count)
                {
                    next = team;
                    candidates = available;
                }
            }
            var target = placements[next!.DivisionId!.Value];
            assigned.Add(next.Id);
            foreach (int slot in candidates!)
            {
                target[slot] = next;
                assignedSlots[next.Id] = slot;
                if (Search()) return true;
                assignedSlots.Remove(next.Id);
                target[slot] = null;
                if (exhausted) break;
            }
            assigned.Remove(next.Id);
            return false;
        }
        if (!Search())
            throw new InvalidOperationException(exhausted
                ? "The shared-draw placement search reached its limit. A safe schedule could not be established; this does not prove the setup is impossible. Existing fixtures have not been changed."
                : "No clash-free team placement was found for the shared draw. Review the assigned home tables. No matches were moved to other nights or venues, and existing fixtures have not been changed.");

        var fixtures = new List<Fixture>();
        foreach (var (divisionId, slots) in placements)
            foreach (var p in expanded)
            {
                var home = slots[p.home];
                var away = slots[p.away];
                if (home == null || away == null) continue;
                fixtures.Add(new Fixture
                {
                    SeasonId = seasonId, DivisionId = divisionId, Date = dates[p.night].Add(kickoff),
                    HomeTeamId = home.Id, AwayTeamId = away.Id, VenueId = home.VenueId, TableId = home.TableId
                });
            }
        return fixtures.OrderBy(f => f.Date).ThenBy(f => f.DivisionId).ToList();
    }
}
