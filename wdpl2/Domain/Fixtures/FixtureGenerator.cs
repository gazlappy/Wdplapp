// File: Services/FixtureGenerator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    public static class FixtureGenerator
    {
        public sealed class GenerateOptions
        {
            public Guid SeasonId { get; set; }
            public DateTime StartDate { get; set; }
            public DayOfWeek MatchNight { get; set; } = DayOfWeek.Tuesday;
            public int RoundsPerOpponent { get; set; } = 2;
            public TimeSpan Kickoff { get; set; } = new(19, 30, 0);
            public bool ClearExistingForSeason { get; set; } = true;
            public bool ClearExisting { get; set; } = true;
        }

        public static List<Fixture> Generate(
            LeagueData league,
            Guid seasonId,
            DateTime startDate,
            DayOfWeek matchNight,
            int roundsPerOpponent = 2,
            TimeSpan? kickoff = null,
            DateTime? endDate = null,
            IReadOnlyList<DateTime>? blackoutDates = null)
        {
            if (league == null) throw new ArgumentNullException(nameof(league));
            if (roundsPerOpponent < 1) throw new ArgumentOutOfRangeException(nameof(roundsPerOpponent));

            var allFixtures = new List<Fixture>();
            var kick = kickoff ?? new TimeSpan(19, 30, 0);
            var endDateOnly = endDate?.Date;
            var blackouts = blackoutDates != null
                ? new HashSet<DateTime>(blackoutDates.Select(d => d.Date))
                : new HashSet<DateTime>();

            DateTime AlignToMatchNight(DateTime d)
            {
                int diff = ((int)matchNight - (int)d.DayOfWeek + 7) % 7;
                return d.Date.AddDays(diff);
            }

            DateTime SkipBlackouts(DateTime d)
            {
                int safety = 0;
                while (blackouts.Contains(d.Date) && safety++ < 52)
                    d = d.AddDays(7);
                return d;
            }

            // Venues are season-scoped entities: only consider this season's venues,
            // otherwise stale VenueIds from other seasons leak into the schedule.
            // A venue with no defined tables is treated as having one implicit
            // table (sentinel Id = Guid.Empty) so it can still host one match
            // per night instead of being unschedulable.
            var seasonVenues = league.Venues?
                .Where(v => v.SeasonId == seasonId)
                .OrderBy(v => v.Name)
                .ToList() ?? new List<Venue>();

            var venueTables = seasonVenues.ToDictionary(
                v => v.Id,
                v => (IReadOnlyList<VenueTable>)(v.Tables is { Count: > 0 }
                    ? v.Tables.OrderBy(t => t.Label).ToList()
                    : new List<VenueTable> { new VenueTable { Id = Guid.Empty, Label = "" } })
            );

            // Deterministic venue fallback order (dictionary key order is not guaranteed)
            var orderedVenueIds = (IReadOnlyList<Guid>)seasonVenues.Select(v => v.Id).ToList();

            var bookedByDate = new Dictionary<DateTime, HashSet<(Guid venueId, Guid tableId)>>();
            var teamBookedByDate = new Dictionary<DateTime, HashSet<Guid>>();

            DateTime currentRoundDate = SkipBlackouts(AlignToMatchNight(startDate));

            var seasonDivisions = league.Divisions.Where(d => d.SeasonId == seasonId).ToList();

            // Build all rounds for every division up-front so they can be
            // interleaved onto the same match nights.
            var divisionRounds = new List<(Division division, List<List<(Team home, Team away)>> rounds)>();

            foreach (var division in seasonDivisions.OrderBy(d => d.Name))
            {
                var teams = league.Teams.Where(t => t.DivisionId == division.Id)
                                        .OrderBy(t => t.Name).ToList();

                // Fallback: if no teams have DivisionId matching this division,
                // discover teams by SeasonId (handles teams copied without DivisionId set)
                if (teams.Count < 2)
                {
                    if (seasonDivisions.Count == 1)
                    {
                        // Single division in season: use all season teams
                        teams = league.Teams.Where(t => t.SeasonId == seasonId)
                                            .OrderBy(t => t.Name).ToList();
                    }
                    else
                    {
                        // Multi-division: also include unassigned season teams
                        var unassigned = league.Teams
                            .Where(t => t.SeasonId == seasonId && !t.DivisionId.HasValue)
                            .OrderBy(t => t.Name).ToList();
                        if (unassigned.Count >= 2)
                            teams = unassigned;
                    }

                    // Auto-fix: assign the correct DivisionId so future operations work
                    foreach (var t in teams)
                    {
                        if (t.DivisionId != division.Id)
                            t.DivisionId = division.Id;
                    }
                }

                if (teams.Count < 2) continue;

                var rounds = CreateRoundRobin(teams);
                var allRounds = new List<List<(Team home, Team away)>>();

                foreach (var r in rounds) allRounds.Add(r.Select(p => p).ToList());
                if (roundsPerOpponent >= 2)
                    allRounds.AddRange(rounds.Select(r => r.Select(p => (p.away, p.home)).ToList()));
                for (int k = 3; k <= roundsPerOpponent; k++)
                {
                    bool swap = (k % 2 == 1);
                    allRounds.AddRange(rounds.Select(r => r.Select(p => swap ? (p.home, p.away) : (p.away, p.home)).ToList()));
                }

                divisionRounds.Add((division, allRounds));
            }

            // Schedule all divisions' fixtures for the same round on the same
            // match night so every division plays on the same dates.
            int maxRounds = divisionRounds.Count > 0
                ? divisionRounds.Max(dr => dr.rounds.Count)
                : 0;

            for (int roundIndex = 0; roundIndex < maxRounds; roundIndex++)
            {
                if (endDateOnly.HasValue && currentRoundDate.Date > endDateOnly.Value)
                    break;

                var dateKey = currentRoundDate.Date;
                if (!bookedByDate.ContainsKey(dateKey)) bookedByDate[dateKey] = new();
                if (!teamBookedByDate.ContainsKey(dateKey)) teamBookedByDate[dateKey] = new();

                foreach (var (division, rounds) in divisionRounds)
                {
                    if (roundIndex >= rounds.Count) continue;

                    foreach (var (home, away) in rounds[roundIndex])
                    {
                        if (teamBookedByDate[dateKey].Contains(home.Id) ||
                            teamBookedByDate[dateKey].Contains(away.Id))
                        {
                            allFixtures.Add(AllocateOnNextNight(
                                league, seasonId, division, home, away, venueTables, orderedVenueIds,
                                bookedByDate, teamBookedByDate, currentRoundDate, matchNight, kick,
                                endDateOnly, blackouts));
                            continue;
                        }

                        var (fx, placed) = TryCreateFixtureAtHomeVenue(
                            league, seasonId, division, home, away, dateKey, kick,
                            venueTables, orderedVenueIds, bookedByDate, teamBookedByDate);

                        allFixtures.Add(placed ? fx : AllocateOnNextNight(
                            league, seasonId, division, home, away, venueTables, orderedVenueIds,
                            bookedByDate, teamBookedByDate, currentRoundDate, matchNight, kick,
                            endDateOnly, blackouts));
                    }
                }

                currentRoundDate = SkipBlackouts(currentRoundDate.AddDays(7));
            }

            return allFixtures;
        }

        private static (Fixture fixture, bool placed) TryCreateFixtureAtHomeVenue(
            LeagueData league,
            Guid seasonId,
            Division division,
            Team home,
            Team away,
            DateTime dateKey,
            TimeSpan kickoff,
            IReadOnlyDictionary<Guid, IReadOnlyList<VenueTable>> venueTables,
            IReadOnlyList<Guid> orderedVenueIds,
            Dictionary<DateTime, HashSet<(Guid venueId, Guid tableId)>> bookedByDate,
            Dictionary<DateTime, HashSet<Guid>> teamBookedByDate)
        {
            Guid? homeVenueId = home.VenueId ?? away.VenueId;

            var venueCandidates = new List<Guid>();
            if (homeVenueId.HasValue && venueTables.ContainsKey(homeVenueId.Value))
                venueCandidates.Add(homeVenueId.Value);
            foreach (var v in orderedVenueIds)
                if (!venueCandidates.Contains(v))
                    venueCandidates.Add(v);

            var bookings = bookedByDate[dateKey];
            var teamBookings = teamBookedByDate[dateKey];

            // No venues defined for this season at all: schedule the match at the
            // home team's nominal venue without table tracking rather than failing.
            if (venueCandidates.Count == 0)
            {
                var fallbackFx = new Fixture
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    DivisionId = division.Id,
                    Date = dateKey.Add(kickoff),
                    HomeTeamId = home.Id,
                    AwayTeamId = away.Id,
                    VenueId = homeVenueId,
                    TableId = home.TableId
                };
                teamBookings.Add(home.Id);
                teamBookings.Add(away.Id);
                return (fallbackFx, true);
            }

            foreach (var venueId in venueCandidates)
            {
                if (!venueTables.TryGetValue(venueId, out var tables) || tables.Count == 0) continue;

                IEnumerable<VenueTable> ordered = tables;
                if (home.TableId.HasValue)
                {
                    var pref = tables.FirstOrDefault(t => t.Id == home.TableId.Value);
                    if (pref != null) ordered = new[] { pref }.Concat(tables.Where(t => t.Id != pref.Id));
                }

                foreach (var table in ordered)
                {
                    var key = (venueId, table.Id);
                    if (bookings.Contains(key)) continue;

                    var dateTime = dateKey.Add(kickoff);
                    var fx = new Fixture
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = seasonId,
                        DivisionId = division.Id,
                        Date = dateTime,
                        HomeTeamId = home.Id,
                        AwayTeamId = away.Id,
                        VenueId = venueId,
                        // Guid.Empty is the implicit "venue has no defined tables" sentinel
                        TableId = table.Id == Guid.Empty ? null : table.Id
                    };

                    bookings.Add(key);
                    teamBookings.Add(home.Id);
                    teamBookings.Add(away.Id);
                    return (fx, true);
                }
            }

            return (new Fixture(), false);
        }

        private static Fixture AllocateOnNextNight(
            LeagueData league,
            Guid seasonId,
            Division division,
            Team home,
            Team away,
            IReadOnlyDictionary<Guid, IReadOnlyList<VenueTable>> venueTables,
            IReadOnlyList<Guid> orderedVenueIds,
            Dictionary<DateTime, HashSet<(Guid venueId, Guid tableId)>> bookedByDate,
            Dictionary<DateTime, HashSet<Guid>> teamBookedByDate,
            DateTime currentRoundDate,
            DayOfWeek matchNight,
            TimeSpan kickoff,
            DateTime? endDateOnly = null,
            HashSet<DateTime>? blackouts = null)
        {
            int safety = 0;
            DateTime lastTried = currentRoundDate;
            while (safety++ < 52)
            {
                var dateKey = currentRoundDate.AddDays(7 * safety).Date;
                lastTried = dateKey;

                if (blackouts != null && blackouts.Contains(dateKey)) continue;
                if (endDateOnly.HasValue && dateKey > endDateOnly.Value) break;

                if (!bookedByDate.ContainsKey(dateKey)) bookedByDate[dateKey] = new();
                if (!teamBookedByDate.ContainsKey(dateKey)) teamBookedByDate[dateKey] = new();

                if (teamBookedByDate[dateKey].Contains(home.Id) ||
                    teamBookedByDate[dateKey].Contains(away.Id))
                    continue;

                var (fx, ok) = TryCreateFixtureAtHomeVenue(
                    league, seasonId, division, home, away, dateKey, kickoff,
                    venueTables, orderedVenueIds, bookedByDate, teamBookedByDate);

                if (ok) return fx;
            }

            // Could not place within the season window: schedule on the next match
            // night after the last tried date (unplaced, but visible and editable)
            // instead of pushing it a year out.
            var overflow = lastTried.AddDays(7).Date;
            return new Fixture
            {
                Id = Guid.NewGuid(),
                SeasonId = seasonId,
                DivisionId = division.Id,
                Date = overflow.Add(kickoff),
                HomeTeamId = home.Id,
                AwayTeamId = away.Id,
                VenueId = home.VenueId,
                TableId = home.TableId
            };
        }

        private static List<List<(Team home, Team away)>> CreateRoundRobin(IList<Team> inputTeams)
        {
            var teams = inputTeams.ToList();
            bool hadBye = false;

            if (teams.Count % 2 == 1)
            {
                teams.Add(new Team { Id = Guid.Empty, Name = "__BYE__" });
                hadBye = true;
            }

            int n = teams.Count;
            int rounds = n - 1;
            int half = n / 2;

            var list = new List<List<(Team home, Team away)>>(rounds);
            var rotating = new List<Team>(teams);

            for (int r = 0; r < rounds; r++)
            {
                var thisRound = new List<(Team home, Team away)>(half);

                for (int i = 0; i < half; i++)
                {
                    var t1 = rotating[i];
                    var t2 = rotating[n - 1 - i];

                    if (t1.Id == Guid.Empty || t2.Id == Guid.Empty) continue;

                    if (r % 2 == 0) thisRound.Add((t1, t2));
                    else thisRound.Add((t2, t1));
                }

                list.Add(thisRound);

                var fixedTeam = rotating[0];
                var tail = rotating.Skip(1).ToList();
                var last = tail[^1];
                tail.RemoveAt(tail.Count - 1);
                tail.Insert(0, last);
                rotating = new List<Team> { fixedTeam };
                rotating.AddRange(tail);
            }

            if (hadBye)
                foreach (var round in list)
                    round.RemoveAll(p => p.home.Id == Guid.Empty || p.away.Id == Guid.Empty);

            return list;
        }
    }
}
