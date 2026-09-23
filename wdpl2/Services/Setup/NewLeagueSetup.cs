using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// What someone starting a league from nothing tells the setup wizard, and
/// the first season it becomes.
/// </summary>
/// <remarks>
/// Kept apart from the wizard's page so the rules - what counts as enough to
/// start, how teams find their division and venue - can be tested.
/// </remarks>
public sealed class NewLeagueSetup
{
    public string LeagueName { get; set; } = "";
    public string SeasonName { get; set; } = "";
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(8);
    public DayOfWeek MatchDay { get; set; } = DayOfWeek.Tuesday;
    public TimeSpan MatchTime { get; set; } = new(19, 30, 0);
    public int FramesPerMatch { get; set; } = 10;

    public List<string> Divisions { get; } = new();
    public List<string> Venues { get; } = new();

    /// <summary>Team names by division name, in the order they were typed.</summary>
    public Dictionary<string, List<string>> TeamsByDivision { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each team's home venue by name; a team left out has none yet.</summary>
    public Dictionary<string, string> HomeVenues { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> AllTeams =>
        Divisions.SelectMany(d => TeamsByDivision.TryGetValue(d, out var teams) ? teams : new List<string>());

    /// <summary>
    /// One name per line, as typed into a box: blank lines and repeats dropped,
    /// order kept.
    /// </summary>
    public static List<string> Lines(string? text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        // Either line ending: what a box gives back differs by platform, and
        // a list pasted from a spreadsheet can arrive with carriage returns.
        foreach (var line in (text ?? "").Split('\n', '\r'))
        {
            var name = line.Trim();
            if (name.Length > 0 && seen.Add(name))
                names.Add(name);
        }
        return names;
    }

    /// <summary>What still has to be put right before the league can be made. Empty when ready.</summary>
    public List<string> Problems() => [.. LeagueProblems(), .. DivisionProblems(), .. TeamProblems()];

    /// <summary>The league's and season's names and dates.</summary>
    public List<string> LeagueProblems()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(LeagueName))
            problems.Add("Give the league a name.");
        if (string.IsNullOrWhiteSpace(SeasonName))
            problems.Add("Give the first season a name.");
        else if (SeasonName.Trim().Length > 100)
            problems.Add("Keep the season name to 100 characters.");
        if (EndDate.Date < StartDate.Date)
            problems.Add("The season has to end after it starts.");
        if (FramesPerMatch < 1)
            problems.Add("A match needs at least one frame.");
        return problems;
    }

    public List<string> DivisionProblems() =>
        Divisions.Count == 0 ? ["Add at least one division."] : [];

    public List<string> TeamProblems()
    {
        var problems = new List<string>();
        foreach (var division in Divisions)
        {
            var count = TeamsByDivision.TryGetValue(division, out var teams) ? teams.Count : 0;
            if (count == 1)
                problems.Add($"{division} has only one team. A division needs two or more to play.");
        }
        if (Divisions.Count > 0 && !AllTeams.Any())
            problems.Add("Add the teams.");

        var repeated = AllTeams
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        foreach (var team in repeated)
            problems.Add($"{team} is in more than one division.");

        return problems;
    }

    /// <summary>The season and everything in it, linked together, not yet saved.</summary>
    public (Season Season, List<Division> Divisions, List<Venue> Venues, List<Team> Teams) Build()
    {
        var season = new Season
        {
            Name = SeasonName.Trim(),
            StartDate = StartDate.Date,
            EndDate = EndDate.Date,
            MatchDayOfWeek = MatchDay,
            MatchStartTime = MatchTime,
            FramesPerMatch = FramesPerMatch,
            IsActive = true,
        };

        var divisions = Divisions
            .Select(name => new Division { Name = name, SeasonId = season.Id })
            .ToList();

        var venues = Venues
            .Select(name => new Venue
            {
                Name = name,
                SeasonId = season.Id,
                Tables = { new VenueTable { Label = "Table 1" } },
            })
            .ToList();

        var venueByName = venues.ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);
        var teams = new List<Team>();
        foreach (var division in divisions)
        {
            if (!TeamsByDivision.TryGetValue(division.Name, out var names)) continue;
            foreach (var name in names)
            {
                var team = new Team { Name = name, SeasonId = season.Id, DivisionId = division.Id };
                if (HomeVenues.TryGetValue(name, out var venueName) && venueByName.TryGetValue(venueName, out var venue))
                {
                    team.VenueId = venue.Id;
                    team.TableId = venue.Tables[0].Id;
                }
                teams.Add(team);
            }
        }

        return (season, divisions, venues, teams);
    }

    /// <summary>
    /// Saves the new season with its divisions, venues and teams.
    /// </summary>
    /// <returns>The season, to be made the current one.</returns>
    public async Task<Season> SaveAsync(IDataStore store, CancellationToken ct = default)
    {
        var problems = Problems();
        if (problems.Count > 0)
            throw new InvalidOperationException(string.Join(" ", problems));

        var (season, divisions, venues, teams) = Build();
        await store.AddSeasonAsync(season, ct);
        await store.AddSeasonEntitiesAsync(divisions, venues, teams, ct: ct);
        return season;
    }
}
