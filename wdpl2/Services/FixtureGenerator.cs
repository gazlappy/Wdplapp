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

        public static List<Fixture> GenerateAndSave(LeagueDataService data, GenerateOptions opts)
            => GenerateAndSaveAsync(data, opts).GetAwaiter().GetResult();

        public static async Task<List<Fixture>> GenerateAndSaveAsync(LeagueDataService data, GenerateOptions opts)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (opts == null) throw new ArgumentNullException(nameof(opts));
            if (opts.SeasonId == Guid.Empty) throw new ArgumentException("SeasonId is required.", nameof(opts));

            var fixtures = Generate(
                data.League,
                opts.SeasonId,
                opts.StartDate,
                opts.MatchNight,
                roundsPerOpponent: opts.RoundsPerOpponent,
                kickoff: opts.Kickoff);

            bool clear = opts.ClearExistingForSeason || opts.ClearExisting;
            if (clear) data.ReplaceFixturesForSeason(opts.SeasonId, fixtures);
            else data.League.Fixtures.AddRange(fixtures);

            await data.SaveAsync().ConfigureAwait(false);
            return fixtures;
        }

        public static List<Fixture> Generate(
            LeagueData league,
            Guid seasonId,
            DateTime startDate,
            DayOfWeek matchNight,
            int roundsPerOpponent = 2,
            TimeSpan? kickoff = null)
        {
            if (league == null) throw new ArgumentNullException(nameof(league));
            if (roundsPerOpponent < 1) throw new ArgumentOutOfRangeException(nameof(roundsPerOpponent));

            var allFixtures = new List<Fixture>();
            var kick = kickoff ?? new TimeSpan(19, 30, 0);

            DateTime AlignToMatchNight(DateTime d)
            {
                int diff = ((int)matchNight - (int)d.DayOfWeek + 7) % 7;
                return d.Date.AddDays(diff);
            }

            var venueTables = league.Venues?.ToDictionary(
                v => v.Id,
                v => (IReadOnlyList<VenueTable>)(v.Tables?.OrderBy(t => t.Label).ToList() ?? new List<VenueTable>())
            ) ?? new Dictionary<Guid, IReadOnlyList<VenueTable>>();

            var bookedByDate = new Dictionary<DateTime, HashSet<(Guid venueId, Guid tableId)>>();
            var teamBookedByDate = new Dictionary<DateTime, HashSet<Guid>>();

            DateTime currentRoundDate = AlignToMatchNight(startDate);

            foreach (var division in league.Divisions.OrderBy(d => d.Name))
            {
                var teams = league.Teams.Where(t => t.DivisionId == division.Id)
                                        .OrderBy(t => t.Name).ToList();
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

                foreach (var matchDay in allRounds)
                {
                    var dateKey = currentRoundDate.Date;
                    if (!bookedByDate.ContainsKey(dateKey)) bookedByDate[dateKey] = new();
                    if (!teamBookedByDate.ContainsKey(dateKey)) teamBookedByDate[dateKey] = new();

                    foreach (var (home, away) in matchDay)
                    {
                        if (teamBookedByDate[dateKey].Contains(home.Id) ||
                            teamBookedByDate[dateKey].Contains(away.Id))
                        {
                            allFixtures.Add(AllocateOnNextNight(
                                league, seasonId, division, home, away, venueTables,
                                bookedByDate, teamBookedByDate, currentRoundDate, matchNight, kick));
                            continue;
                        }

                        var (fx, placed) = TryCreateFixtureAtHomeVenue(
                            league, seasonId, division, home, away, dateKey, kick,
                            venueTables, bookedByDate, teamBookedByDate);

                        allFixtures.Add(placed ? fx : AllocateOnNextNight(
                            league, seasonId, division, home, away, venueTables,
                            bookedByDate, teamBookedByDate, currentRoundDate, matchNight, kick));
                    }

                    currentRoundDate = currentRoundDate.AddDays(7);
                }
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
            Dictionary<DateTime, HashSet<(Guid venueId, Guid tableId)>> bookedByDate,
            Dictionary<DateTime, HashSet<Guid>> teamBookedByDate)
        {
            Guid? homeVenueId = home.VenueId ?? away.VenueId;

            var venueCandidates = new List<Guid>();
            if (homeVenueId.HasValue) venueCandidates.Add(homeVenueId.Value);
            foreach (var v in league.Venues.Select(v => v.Id))
                if (!venueCandidates.Contains(v) && venueTables.TryGetValue(v, out var tbls) && tbls.Count > 0)
                    venueCandidates.Add(v);

            var bookings = bookedByDate[dateKey];
            var teamBookings = teamBookedByDate[dateKey];

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
                        TableId = table.Id
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
            Dictionary<DateTime, HashSet<(Guid venueId, Guid tableId)>> bookedByDate,
            Dictionary<DateTime, HashSet<Guid>> teamBookedByDate,
            DateTime currentRoundDate,
            DayOfWeek matchNight,
            TimeSpan kickoff)
        {
            int safety = 0;
            while (safety++ < 52)
            {
                var dateKey = currentRoundDate.AddDays(7 * safety).Date;
                if (!bookedByDate.ContainsKey(dateKey)) bookedByDate[dateKey] = new();
                if (!teamBookedByDate.ContainsKey(dateKey)) teamBookedByDate[dateKey] = new();

                var (fx, ok) = TryCreateFixtureAtHomeVenue(
                    league, seasonId, division, home, away, dateKey, kickoff,
                    venueTables, bookedByDate, teamBookedByDate);

                if (ok) return fx;
            }

            return new Fixture
            {
                Id = Guid.NewGuid(),
                SeasonId = seasonId,
                DivisionId = division.Id,
                Date = currentRoundDate.AddDays(365).Date.Add(kickoff),
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
