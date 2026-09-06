using Wdpl2.Models;

namespace Wdpl2.Services;

public static class GeneratedScheduleValidator
{
    public static void ValidateSetup(LeagueData data, Guid seasonId)
    {
        var errors = new List<string>();
        var season = data.Seasons.SingleOrDefault(s => s.Id == seasonId)
            ?? throw new InvalidOperationException("Season not found.");
        if (season.IsLocked) errors.Add("The season is locked.");
        var teams = data.Teams.Where(t => t.SeasonId == seasonId).ToList();
        if (teams.Count < 2) errors.Add("At least two teams are required.");
        if (teams.GroupBy(t => t.Id).Any(g => g.Count() > 1) || teams.Any(t => t.Id == Guid.Empty))
            errors.Add("Team IDs must be unique and non-empty.");
        if (teams.GroupBy(t => t.GlobalTeamId ?? t.Id).Any(g => g.Count() > 1))
            errors.Add("The season contains duplicate team identities. Resolve them before generating fixtures.");
        foreach (var team in teams)
        {
            if (!data.Divisions.Any(d => d.Id == team.DivisionId && d.SeasonId == seasonId))
                errors.Add($"{team.Name}: assign a division in this season.");
            var venue = data.Venues.SingleOrDefault(v => v.Id == team.VenueId && v.SeasonId == seasonId);
            if (venue == null) errors.Add($"{team.Name}: assign a home venue in this season.");
            else if (!team.TableId.HasValue || team.TableId == Guid.Empty || !venue.Tables.Any(t => t.Id == team.TableId))
                errors.Add($"{team.Name}: assign a defined home table at {venue.Name}.");
        }
        foreach (var group in teams.GroupBy(t => t.DivisionId).Where(g => g.Count() < 2))
            errors.Add($"Division containing {group.First().Name} needs at least two teams.");
        Throw(errors);
    }

    public static List<DateTime> MatchDates(DateTime start, DateTime end, DayOfWeek night, IEnumerable<DateTime> blackouts)
    {
        if (end.Date < start.Date) throw new InvalidOperationException("Season end must be on or after its start.");
        if (!Enum.IsDefined(night)) throw new InvalidOperationException("Choose a valid match night.");
        var excluded = blackouts.Select(d => d.Date).ToHashSet();
        var date = start.Date.AddDays(((int)night - (int)start.DayOfWeek + 7) % 7);
        var dates = new List<DateTime>();
        while (date <= end.Date)
        {
            if (!excluded.Contains(date)) dates.Add(date);
            if (date > DateTime.MaxValue.AddDays(-7)) break;
            date = date.AddDays(7);
        }
        return dates;
    }

    public static void Validate(LeagueData data, Guid seasonId, IReadOnlyList<Fixture> fixtures,
        DateTime start, DateTime end, DayOfWeek night, TimeSpan kickoff, int legs, IEnumerable<DateTime> blackouts)
    {
        ValidateSetup(data, seasonId);
        if (legs < 1) throw new InvalidOperationException("Rounds per opponent must be positive.");
        if (kickoff < TimeSpan.Zero || kickoff >= TimeSpan.FromDays(1)) throw new InvalidOperationException("Invalid kickoff time.");
        var dates = MatchDates(start, end, night, blackouts);
        var errors = new List<string>();
        var teams = data.Teams.Where(t => t.SeasonId == seasonId).ToDictionary(t => t.Id);
        if (fixtures.Select(f => f.Id).Distinct().Count() != fixtures.Count || fixtures.Any(f => f.Id == Guid.Empty))
            errors.Add("Fixture IDs must be unique and non-empty.");
        foreach (var fixture in fixtures)
        {
            if (fixture.SeasonId != seasonId || !teams.TryGetValue(fixture.HomeTeamId, out var home) ||
                !teams.TryGetValue(fixture.AwayTeamId, out var away))
            { errors.Add("A fixture references a team or season outside this schedule."); continue; }
            if (home.Id == away.Id || home.DivisionId != away.DivisionId || fixture.DivisionId != home.DivisionId)
                errors.Add($"Invalid pairing: {home.Name} / {away.Name}.");
            if (fixture.VenueId != home.VenueId || fixture.TableId != home.TableId)
                errors.Add($"{home.Name}: fixture is not on its assigned home venue/table.");
            if (!dates.Contains(fixture.Date.Date) || fixture.Date.TimeOfDay != kickoff)
                errors.Add($"{home.Name}: {fixture.Date:dd MMM yyyy HH:mm} is not an allowed match night/time.");
        }
        foreach (var day in fixtures.GroupBy(f => f.Date.Date))
        {
            foreach (var repeated in day.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).GroupBy(id => id).Where(g => g.Count() > 1))
                errors.Add($"{teams.GetValueOrDefault(repeated.Key)?.Name ?? repeated.Key.ToString()} plays more than once on {day.Key:dd MMM yyyy}.");
            foreach (var slot in day.GroupBy(f => (f.VenueId, f.TableId)).Where(g => g.Count() > 1))
                errors.Add($"Home table double-booked on {day.Key:dd MMM yyyy}: {string.Join(", ", slot.Select(f => teams.GetValueOrDefault(f.HomeTeamId)?.Name))}.");
        }
        int maximum = teams.Values.GroupBy(t => t.DivisionId).Max(g => g.Count());
        int rounds = (maximum + maximum % 2 - 1) * legs;
        foreach (var division in teams.Values.GroupBy(t => t.DivisionId))
        {
            var members = division.ToList();
            var matches = fixtures.Where(f => f.DivisionId == division.Key).ToList();
            if (dates.Count < rounds) errors.Add($"{members[0].Name}'s division requires {rounds} match nights; only {dates.Count} available.");
            var expectedDates = dates.Take(rounds).ToHashSet();
            if (matches.Any(f => !expectedDates.Contains(f.Date.Date))
                || (members.Count == maximum && expectedDates.Any(d => matches.Count(f => f.Date.Date == d) != members.Count / 2)))
                errors.Add($"Division containing {members[0].Name}: every round must be complete on its scheduled night.");
            for (int i = 0; i < members.Count; i++)
                for (int j = i + 1; j < members.Count; j++)
                {
                    var a = members[i]; var b = members[j];
                    int home = matches.Count(f => f.HomeTeamId == a.Id && f.AwayTeamId == b.Id);
                    int away = matches.Count(f => f.HomeTeamId == b.Id && f.AwayTeamId == a.Id);
                    if (home + away != legs || Math.Abs(home - away) > 1)
                        errors.Add($"{a.Name} / {b.Name}: incomplete or unbalanced home/away pairings.");
                }
        }
        Throw(errors);
        SharedFixtureSheetSchedule.Create(data.Divisions.Where(d => d.SeasonId == seasonId).ToList(), teams.Values.ToList(), fixtures.ToList());
    }

    private static void Throw(List<string> errors)
    {
        if (errors.Count != 0) throw new InvalidOperationException("Cannot create a safe schedule:\n" + string.Join("\n", errors.Distinct().Take(30)));
    }
}
