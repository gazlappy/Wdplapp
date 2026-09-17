using Wdpl2.Domain.Competitions;
using Wdpl2.Helpers;
using Wdpl2.Models;

namespace Wdpl2.Services.Web;

/// <summary>
/// Builds the payload the <c>league/push</c> action expects.
/// </summary>
/// <remarks>
/// A snapshot is always a complete picture of ONE season. The server replaces
/// that season wholesale, so a partial snapshot would silently delete whatever
/// it omitted - hence no "changed rows only" mode.
/// <para>
/// Standings are calculated here with <see cref="StandingsCalculator"/> rather
/// than in SQL, so the website's tables are produced by exactly the same code
/// as the app's, including points deductions.
/// </para>
/// </remarks>
public static class LeagueSnapshot
{
    public sealed class Counts
    {
        public int Divisions { get; init; }
        public int Venues { get; init; }
        public int Teams { get; init; }
        public int Players { get; init; }
        public int Fixtures { get; init; }
        public int Standings { get; init; }

        public override string ToString() =>
            $"{Teams} teams, {Players} players, {Fixtures} fixtures, {Standings} table rows";
    }

    /// <summary>Builds the push payload for one season.</summary>
    public static (object Payload, Counts Counts) Build(LeagueData league, Season season, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(season);

        var divisions = league.Divisions.Where(d => d.SeasonId == season.Id).ToList();
        var venues = league.Venues.Where(v => v.SeasonId == season.Id).ToList();
        var teams = league.Teams.Where(t => t.SeasonId == season.Id).ToList();
        var players = league.Players.Where(p => p.SeasonId == season.Id).ToList();
        var fixtures = league.Fixtures.Where(f => f.SeasonId == season.Id).ToList();

        // Divisions carry no explicit order, so publish them in the order the
        // app shows them and let the website preserve it.
        var order = divisions
            .Select((d, index) => (d.Id, index))
            .ToDictionary(x => x.Id, x => x.index);

        var standings = BuildStandings(divisions, teams, fixtures, settings);
        var format = Wdpl2.Domain.Fixtures.MatchFormat.From(settings);

        // Cup ties ride along in the fixtures list so a card can be opened on
        // them and the captains' own pages find them. They are marked as cup so
        // the league's tables, results and fixture list ignore them - and they
        // are deliberately not in `fixtures` above, which is what the standings
        // are calculated from.
        var cupTies = CupTie.For(league, season);

        var payload = new
        {
            season = new
            {
                id = season.Id,
                name = season.Name,
                startDate = season.StartDate.ToString("yyyy-MM-dd"),
                endDate = season.EndDate.ToString("yyyy-MM-dd"),
                isCurrent = season.IsActive,

                // The match format travels with the season so the website can
                // open a card on its own - from the admin portal - without a
                // second copy of the setting drifting from the app's.
                framesTotal = format.TotalFrames,
                maxPerPlayer = format.MaxFramesPerPlayer,
            },
            divisions = divisions.Select(d => new
            {
                id = d.Id,
                name = d.Name,
                sortOrder = order.TryGetValue(d.Id, out var i) ? i : 0,
            }).ToList(),
            venues = venues.Select(v => new
            {
                id = v.Id,
                name = v.Name,
                address = v.Address,
            }).ToList(),
            teams = teams.Select(t => new
            {
                id = t.Id,
                divisionId = t.DivisionId,
                venueId = t.VenueId,
                name = t.Name ?? "",
                captainName = t.Captain,
            }).ToList(),
            players = players.Select(p => new
            {
                id = p.Id,
                teamId = p.TeamId,
                name = p.Name,
                isActive = p.IsActive,
            }).ToList(),
            fixtures = fixtures.Select(f => new
            {
                id = f.Id,
                divisionId = f.DivisionId,
                homeTeamId = f.HomeTeamId,
                awayTeamId = f.AwayTeamId,
                venueId = f.VenueId,
                date = f.Date.ToString("yyyy-MM-dd"),
                weekNo = f.Frames.FirstOrDefault()?.WeekNo ?? 0,
                homeScore = f.HomeScore,
                awayScore = f.AwayScore,
                framesTotal = f.Frames.Count,
                played = f.Frames.Any(fr => fr.Winner != FrameWinner.None),
                kind = "league",
                label = (string?)null,
            })
            .Concat(cupTies.Select(t => new
            {
                id = t.Id,
                divisionId = (Guid?)null,
                homeTeamId = t.HomeTeamId,
                awayTeamId = t.AwayTeamId,
                venueId = t.VenueId,
                date = t.Date.ToString("yyyy-MM-dd"),
                weekNo = 0,
                homeScore = t.HomeScore,
                awayScore = t.AwayScore,
                framesTotal = format.TotalFrames,
                played = t.IsComplete,
                kind = "cup",

                // A cup tie has a name a league fixture does not need: without
                // it the website can only call it "a cup tie", and the captains
                // signing in would have no idea which round they are playing.
                label = $"{t.CompetitionName} — {t.RoundName}",
            }))
            .ToList(),
            standings,
        };

        var counts = new Counts
        {
            Divisions = divisions.Count,
            Venues = venues.Count,
            Teams = teams.Count,
            Players = players.Count,
            Fixtures = fixtures.Count + cupTies.Count,
            Standings = standings.Count,
        };

        return (payload, counts);
    }

    /// <summary>
    /// One table per division, using the app's own calculator so the published
    /// table cannot drift from what the app displays.
    /// </summary>
    private static List<object> BuildStandings(
        List<Division> divisions, List<Team> teams, List<Fixture> fixtures, AppSettings settings)
    {
        var rows = new List<object>();

        foreach (var division in divisions)
        {
            var divisionTeams = teams.Where(t => t.DivisionId == division.Id).ToList();
            if (divisionTeams.Count == 0) continue;

            var divisionFixtures = fixtures.Where(f => f.DivisionId == division.Id);
            var table = StandingsCalculator.Calculate(divisionTeams, divisionFixtures, settings);

            foreach (var standing in table)
            {
                rows.Add(new
                {
                    divisionId = division.Id,
                    teamId = standing.TeamId,
                    position = standing.Position,
                    played = standing.Played,
                    won = standing.Won,
                    drawn = standing.Drawn,
                    lost = standing.Lost,
                    framesFor = standing.FramesFor,
                    framesAgainst = standing.FramesAgainst,
                    deducted = standing.Deducted,
                    points = standing.Points,
                });
            }
        }

        return rows;
    }
}
