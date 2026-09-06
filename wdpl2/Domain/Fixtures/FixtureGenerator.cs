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

            GeneratedScheduleValidator.ValidateSetup(league, seasonId);
            var season = league.Seasons.Single(s => s.Id == seasonId);
            var end = endDate ?? season.EndDate;
            var kick = kickoff ?? new TimeSpan(19, 30, 0);
            var blackouts = blackoutDates ?? season.BlackoutDates;
            var dates = GeneratedScheduleValidator.MatchDates(startDate, end, matchNight, blackouts);
            var fixtures = SharedDrawScheduler.Generate(league, seasonId, dates, roundsPerOpponent, kick);
            GeneratedScheduleValidator.Validate(league, seasonId, fixtures, startDate, end, matchNight, kick, roundsPerOpponent, blackouts);
            return fixtures;
        }

        private static int[] FindComponents(List<int>[] graph)
        {
            var reverse = graph.Select(_ => new List<int>()).ToArray();
            for (int i = 0; i < graph.Length; i++)
                foreach (int next in graph[i]) reverse[next].Add(i);
            var seen = new bool[graph.Length];
            var order = new List<int>();
            for (int root = 0; root < graph.Length; root++)
            {
                var stack = new Stack<(int node, bool finish)>();
                stack.Push((root, false));
                while (stack.Count > 0)
                {
                    var (node, finish) = stack.Pop();
                    if (finish) { order.Add(node); continue; }
                    if (seen[node]) continue;
                    seen[node] = true;
                    stack.Push((node, true));
                    foreach (int next in graph[node])
                        if (!seen[next]) stack.Push((next, false));
                }
            }
            var components = Enumerable.Repeat(-1, graph.Length).ToArray();
            int component = 0;
            foreach (int root in order.AsEnumerable().Reverse())
            {
                if (components[root] >= 0) continue;
                var stack = new Stack<int>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    int node = stack.Pop();
                    if (components[node] >= 0) continue;
                    components[node] = component;
                    foreach (int next in reverse[node]) stack.Push(next);
                }
                component++;
            }
            return components;
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
