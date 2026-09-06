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

        var template = new List<(int round, int home, int away)>();
        var rotation = Enumerable.Range(0, size).ToList();
        for (int round = 0; round < rounds; round++)
        {
            // Opponent edges and fixed complementary slot pairs form even cycles.
            // Opposite home roles let table-sharing teams use complementary slots.
            var opponents = new int[size];
            for (int i = 0; i < size / 2; i++)
            {
                int first = rotation[i], second = rotation[size - 1 - i];
                opponents[first] = second;
                opponents[second] = first;
            }
            var roles = new bool?[size];
            for (int root = 0; root < size; root++)
            {
                if (roles[root].HasValue) continue;
                roles[root] = round % 2 == 0;
                var pending = new Queue<int>();
                pending.Enqueue(root);
                while (pending.TryDequeue(out int slot))
                    foreach (int other in new[] { opponents[slot], slot ^ 1 })
                    {
                        if (roles[other].HasValue) continue;
                        roles[other] = !roles[slot]!.Value;
                        pending.Enqueue(other);
                    }
            }
            for (int i = 0; i < size / 2; i++)
            {
                int first = rotation[i], second = rotation[size - 1 - i];
                template.Add((round, roles[first]!.Value ? first : second, roles[first]!.Value ? second : first));
            }
            int last = rotation[^1];
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, last);
        }
        var expanded = Enumerable.Range(0, legs).SelectMany(leg => template.Select(p =>
            (night: p.round + leg * rounds, home: leg % 2 == 0 ? p.home : p.away, away: leg % 2 == 0 ? p.away : p.home))).ToList();
        var placements = divisions.ToDictionary(g => g.Key, _ => new Team?[size]);
        var assigned = new HashSet<Guid>();
        var tableCounts = teams.GroupBy(t => (t.VenueId, t.TableId)).ToDictionary(g => g.Key, g => g.Count());
        // Each team must host at least floor(legs / 2) matches per opponent.
        foreach (var table in teams.GroupBy(t => (t.VenueId, t.TableId)))
        {
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
                if (Search()) return true;
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
