using Wdpl2.Models;

namespace Wdpl2.Services;

public sealed class SharedFixtureSheetSchedule
{
    public sealed record Pairing(DateTime Date, int Home, int Away);

    public int SlotCount { get; private init; }
    public List<Pairing> Pairings { get; private init; } = new();
    public Dictionary<Guid, int> TeamNumbers { get; } = new();

    public static SharedFixtureSheetSchedule Create(List<Division> divisions, List<Team> teams, List<Fixture> fixtures)
    {
        InvalidOperationException Invalid(string detail) => new(
            $"Cannot create one shared numbered fixture grid: {detail} Saved fixtures have not been changed. Review the division schedules; matching dates alone do not guarantee a common numbered draw.");
        if (fixtures.Count == 0) throw Invalid("No fixtures are scheduled.");
        if (teams.Select(t => t.Id).Distinct().Count() != teams.Count)
            throw Invalid("Duplicate team records were found.");
        var byId = teams.ToDictionary(t => t.Id);
        foreach (var f in fixtures)
            if (!divisions.Any(d => d.Id == f.DivisionId)
                || !byId.TryGetValue(f.HomeTeamId, out var h) || !byId.TryGetValue(f.AwayTeamId, out var a)
                || h.Id == a.Id || h.DivisionId != f.DivisionId || a.DivisionId != f.DivisionId)
                throw Invalid("A fixture has an unknown team or invalid division placement.");
        foreach (var date in fixtures.GroupBy(f => f.Date.Date))
            if (date.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).GroupBy(id => id).Any(g => g.Count() > 1))
                throw Invalid($"A team plays more than once on {date.Key:dd MMM yyyy}.");
        if (teams.Any(t => !divisions.Any(d => d.Id == t.DivisionId)))
            throw Invalid("A team has no valid division.");

        var primary = divisions.OrderByDescending(d => teams.Count(t => t.DivisionId == d.Id))
            .ThenBy(d => d.Name).ThenBy(d => d.Id).First();
        var primaryTeams = teams.Where(t => t.DivisionId == primary.Id).ToList();
        var primaryFixtures = fixtures.Where(f => f.DivisionId == primary.Id).ToList();
        int count = primaryTeams.Count;
        if (count < 2) throw Invalid("At least two teams are required in the template division.");
        var schedule = new SharedFixtureSheetSchedule { SlotCount = count + count % 2 };
        // Seed the template from actual home/away fixtures, not a separately generated draw.
        foreach (var f in primaryFixtures.OrderBy(f => f.Date).ThenBy(f => byId[f.HomeTeamId].Name).ThenBy(f => f.HomeTeamId))
            foreach (var id in new[] { f.HomeTeamId, f.AwayTeamId })
                if (!schedule.TeamNumbers.ContainsKey(id)) schedule.TeamNumbers[id] = schedule.TeamNumbers.Count + 1;
        foreach (var t in primaryTeams.OrderBy(t => t.Name).ThenBy(t => t.Id))
            if (!schedule.TeamNumbers.ContainsKey(t.Id)) schedule.TeamNumbers[t.Id] = schedule.TeamNumbers.Count + 1;
        foreach (var date in fixtures.Select(f => f.Date.Date).Distinct().OrderBy(d => d))
        {
            var matches = primaryFixtures.Where(f => f.Date.Date == date).ToList();
            if (matches.Count != count / 2)
                throw Invalid($"{primary.Name} does not have a complete template round on {date:dd MMM yyyy}.");
            foreach (var f in matches)
                schedule.Pairings.Add(new(date, schedule.TeamNumbers[f.HomeTeamId], schedule.TeamNumbers[f.AwayTeamId]));
            if (count % 2 != 0)
            {
                var playing = matches.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).ToHashSet();
                var idle = primaryTeams.Single(t => !playing.Contains(t.Id));
                schedule.Pairings.Add(new(date, schedule.TeamNumbers[idle.Id], schedule.SlotCount));
            }
        }

        string Signature(IEnumerable<(DateTime date, bool home)> edges) => string.Join(";",
            edges.OrderBy(e => e.date).ThenBy(e => e.home).Select(e => $"{e.date:yyyy-MM-dd}:{e.home}"));
        var templateEdges = new Dictionary<(int, int), string>();
        for (int x = 1; x <= schedule.SlotCount; x++)
            for (int y = 1; y <= schedule.SlotCount; y++)
                templateEdges[(x, y)] = Signature(schedule.Pairings
                    .Where(p => (p.Home == x && p.Away == y) || (p.Home == y && p.Away == x))
                    .Select(p => (p.Date, p.Home == x)));
        foreach (var division in divisions.Where(d => d.Id != primary.Id))
        {
            var members = teams.Where(t => t.DivisionId == division.Id).OrderBy(t => t.Name).ThenBy(t => t.Id).ToList();
            var matches = fixtures.Where(f => f.DivisionId == division.Id).ToList();
            var edges = new Dictionary<(Guid, Guid), string>();
            foreach (var x in members)
                foreach (var y in members)
                    edges[(x.Id, y.Id)] = Signature(matches
                        .Where(f => (f.HomeTeamId == x.Id && f.AwayTeamId == y.Id) || (f.HomeTeamId == y.Id && f.AwayTeamId == x.Id))
                        .Select(f => (f.Date.Date, f.HomeTeamId == x.Id)));
            var domains = members.ToDictionary(t => t.Id, t => Enumerable.Range(1, schedule.SlotCount)
                .Where(slot => matches.Where(f => f.HomeTeamId == t.Id || f.AwayTeamId == t.Id)
                    .All(f => schedule.Pairings.Any(p => p.Date == f.Date.Date
                        && (f.HomeTeamId == t.Id ? p.Home == slot : p.Away == slot)))).ToList());
            var assigned = new Dictionary<Guid, int>();
            int attempts = 0;
            bool exhausted = false;
            bool Search()
            {
                if (++attempts > 200000) { exhausted = true; return false; }
                if (assigned.Count == members.Count) return true;
                var next = members.Where(t => !assigned.ContainsKey(t.Id))
                    .Select(t => (team: t, slots: domains[t.Id].Where(slot => !assigned.ContainsValue(slot)
                        && assigned.All(a => edges[(t.Id, a.Key)] == templateEdges[(slot, a.Value)])).ToList()))
                    .OrderBy(t => t.slots.Count).First();
                foreach (int slot in next.slots)
                {
                    assigned[next.team.Id] = slot;
                    if (Search()) return true;
                    assigned.Remove(next.team.Id);
                    if (exhausted) break;
                }
                return false;
            }
            if (!Search()) throw Invalid(exhausted
                ? $"The numbering search limit was reached for {division.Name}; compatibility could not be established."
                : $"{division.Name}'s saved home/away pairings cannot be matched to {primary.Name} by renumbering teams.");
            foreach (var entry in assigned) schedule.TeamNumbers.Add(entry.Key, entry.Value);
        }

        // Independently expand the shared grid through each key. BYE slots create no match.
        foreach (var division in divisions)
        {
            var key = teams.Where(t => t.DivisionId == division.Id).ToDictionary(t => schedule.TeamNumbers[t.Id], t => t.Id);
            var expected = schedule.Pairings.Where(p => key.ContainsKey(p.Home) && key.ContainsKey(p.Away))
                .Select(p => (p.Date, Home: key[p.Home], Away: key[p.Away])).ToHashSet();
            var actual = fixtures.Where(f => f.DivisionId == division.Id)
                .Select(f => (f.Date.Date, Home: f.HomeTeamId, Away: f.AwayTeamId)).ToList();
            if (actual.Count != expected.Count || !expected.SetEquals(actual))
                throw Invalid($"The shared grid does not reproduce every fixture in {division.Name}.");
        }
        return schedule;
    }
}
