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

        int count = teams.GroupBy(t => t.DivisionId).Max(g => g.Count());
        if (count < 2) throw Invalid("At least two teams are required in the template division.");
        var schedule = new SharedFixtureSheetSchedule { SlotCount = count + count % 2 };
        var dates = fixtures.Select(f => f.Date.Date).Distinct().OrderBy(d => d).ToList();
        int rounds = schedule.SlotCount - 1;
        if (dates.Count % rounds != 0)
            throw Invalid("The dates do not form complete legs of the numbered draw. Regenerate the fixtures with the corrected generator.");
        schedule.Pairings.AddRange(NumberedFixtureDraw.Create(schedule.SlotCount, dates.Count / rounds)
            .Select(p => new Pairing(dates[p.Round], p.Home, p.Away)));
        var tableGroups = teams.Where(t => t.VenueId.HasValue && t.TableId.HasValue && t.TableId != Guid.Empty)
            .GroupBy(t => (t.VenueId, t.TableId)).ToList();
        foreach (var table in tableGroups)
        {
            if (table.Count() > 2)
                throw Invalid($"More than two teams share a table: {string.Join(", ", table.Select(t => t.Name))}.");
        }
        foreach (var date in fixtures.GroupBy(f => f.Date.Date))
            if (date.Where(f => f.VenueId.HasValue && f.TableId.HasValue && f.TableId != Guid.Empty)
                .GroupBy(f => (f.VenueId, f.TableId)).Any(g => g.Count() > 1)
                || date.Select(f => byId[f.HomeTeamId]).Where(t => t.VenueId.HasValue && t.TableId.HasValue && t.TableId != Guid.Empty)
                .GroupBy(t => (t.VenueId, t.TableId)).Any(g => g.Count() > 1))
                throw Invalid($"A home table is double-booked on {date.Key:dd MMM yyyy}.");

        string Signature(IEnumerable<(DateTime date, bool home)> edges) => string.Join(";",
            edges.OrderBy(e => e.date).ThenBy(e => e.home).Select(e => $"{e.date:yyyy-MM-dd}:{e.home}"));
        var templateEdges = new Dictionary<(int, int), string>();
        for (int x = 1; x <= schedule.SlotCount; x++)
            for (int y = 1; y <= schedule.SlotCount; y++)
                templateEdges[(x, y)] = Signature(schedule.Pairings
                    .Where(p => (p.Home == x && p.Away == y) || (p.Home == y && p.Away == x))
                    .Select(p => (p.Date, p.Home == x)));
        var edges = new Dictionary<(Guid, Guid), string>();
        foreach (var x in teams)
            foreach (var y in teams.Where(t => t.DivisionId == x.DivisionId))
                edges[(x.Id, y.Id)] = Signature(fixtures
                        .Where(f => (f.HomeTeamId == x.Id && f.AwayTeamId == y.Id) || (f.HomeTeamId == y.Id && f.AwayTeamId == x.Id))
                        .Select(f => (f.Date.Date, f.HomeTeamId == x.Id)));
        var domains = teams.ToDictionary(t => t.Id, t => Enumerable.Range(1, schedule.SlotCount)
                .Where(slot => fixtures.Where(f => f.HomeTeamId == t.Id || f.AwayTeamId == t.Id)
                    .All(f => schedule.Pairings.Any(p => p.Date == f.Date.Date
                        && (f.HomeTeamId == t.Id ? p.Home == slot : p.Away == slot)))).ToList());
        var partners = tableGroups.Where(g => g.Count() == 2).SelectMany(g => new[]
            { (g.First().Id, Other: g.Last().Id), (g.Last().Id, Other: g.First().Id) }).ToDictionary(p => p.Id, p => p.Other);
        var assigned = schedule.TeamNumbers;
        int attempts = 0;
        bool exhausted = false;
        bool Search()
        {
            if (++attempts > 200000) { exhausted = true; return false; }
            if (assigned.Count == teams.Count) return true;
            var next = teams.Where(t => !assigned.ContainsKey(t.Id))
                .Select(t => (team: t, slots: domains[t.Id].Where(slot =>
                    assigned.Where(a => byId[a.Key].DivisionId == t.DivisionId)
                        .All(a => a.Value != slot && edges[(t.Id, a.Key)] == templateEdges[(slot, a.Value)])
                    && (!partners.TryGetValue(t.Id, out var partner) || !assigned.TryGetValue(partner, out int other)
                        || NumberedFixtureDraw.AreTablePartners(slot, other))).ToList()))
                .OrderBy(t => t.slots.Count).ThenBy(t => t.team.Name).ThenBy(t => t.team.Id).First();
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
            ? "The numbering search limit was reached; compatibility could not be established."
            : "The saved matches cannot fit the numbered draw with odd/even table partners. Regenerate the fixtures with the corrected generator.");

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
        foreach (var pair in partners)
            if (!NumberedFixtureDraw.AreTablePartners(assigned[pair.Key], assigned[pair.Value]))
                throw Invalid("A shared-table team pair is not assigned consecutive odd/even numbers.");
        return schedule;
    }
}
