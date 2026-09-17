using Wdpl2.Models;

namespace Wdpl2.Domain.Players;

/// <summary>
/// Finds the same person recorded more than once, and ties those rows together.
/// </summary>
/// <remarks>
/// A player row belongs to one season, so somebody who has played ten seasons
/// has ten rows. That is the design, not a fault - but unless those rows are
/// tied together the app sees ten different people, and a career gets counted
/// in pieces. <see cref="Player.GlobalPlayerId"/> is the tie, and career stats
/// and achievements are what read it.
/// <para>
/// Linking deletes nothing and can be undone, which is the whole point: the
/// records stay exactly as they are and only the app's idea of who is who
/// changes. Two rows that share a season are still two rows on that season's
/// roster afterwards - linking is not a way to remove one, and this says so
/// rather than pretending otherwise.
/// </para>
/// <para>
/// Nothing here decides anything. <see cref="Find"/> proposes and the secretary
/// disposes, the same bargain <c>CaptainPlayerMatcher</c> makes.
/// </para>
/// </remarks>
public static class PlayerLinks
{
    /// <summary>How sure the finder is that two rows are one person.</summary>
    public enum Confidence
    {
        /// <summary>The same name, spelled the same way.</summary>
        Certain,

        /// <summary>An initial against a forename, or a one-letter difference.</summary>
        Likely,

        /// <summary>Close enough to be worth a human's eye, and no closer.</summary>
        Possible,
    }

    /// <summary>One set of rows that look like the same person.</summary>
    public sealed record Duplicate(
        string Name,
        Confidence Confidence,
        string Reason,
        IReadOnlyList<Player> Players)
    {
        /// <summary>Seasons in which more than one of these rows appears.</summary>
        public int SeasonsWithClash =>
            Players.Where(p => p.SeasonId.HasValue)
                   .GroupBy(p => p.SeasonId!.Value)
                   .Count(g => g.Count() > 1);

        /// <summary>
        /// True when this is one person per season, so linking is the whole fix.
        /// </summary>
        /// <remarks>
        /// A set with two rows in one season is a different problem: linking
        /// ties their record together but the roster still lists them twice,
        /// and only a person can decide which row the season should keep.
        /// </remarks>
        public bool Tidy => SeasonsWithClash == 0;
    }

    /// <summary>What a link did.</summary>
    public sealed record Report(int Linked, int Groups)
    {
        public override string ToString() => Linked == 0
            ? "Nothing to link."
            : $"Linked {Linked} player row(s)"
              + (Groups > 1 ? $" across {Groups} people" : "")
              + " as the same person. Nothing was deleted.";
    }

    /// <summary>The identity a player is known by across seasons.</summary>
    public static Guid Identity(Player player) => player.GlobalPlayerId ?? player.Id;

    // ------------------------------------------------------------------ find

    /// <summary>
    /// Every set of rows in the league that look like one person.
    /// </summary>
    /// <remarks>
    /// Across all seasons on purpose: a name misspelled once and then copied
    /// forward is the usual way a career gets split in two, and looking at one
    /// season at a time is how it stays that way.
    /// </remarks>
    public static List<Duplicate> Find(LeagueData league)
    {
        ArgumentNullException.ThrowIfNull(league);

        var players = league.Players.Where(p => Name(p).Length > 0).ToList();
        var by = players.ToDictionary(p => p.Id);

        // Rows are joined into sets rather than matched pair by pair, so a third
        // row that reaches the set by a different rule joins it instead of
        // being left out. "Dave Marsh" twice and "D Marsh" once is one person
        // three times, not a pair and a stray.
        var parent = new Dictionary<Guid, Guid>();
        var weakest = new Dictionary<Guid, Confidence>();
        var reasons = new Dictionary<Guid, SortedSet<string>>();

        Guid Root(Guid id)
        {
            var root = id;
            while (parent.TryGetValue(root, out var up) && up != root) root = up;

            // Flatten on the way back, or a long chain is walked again every time.
            var walk = id;
            while (parent.TryGetValue(walk, out var up) && up != walk)
            {
                parent[walk] = root;
                walk = up;
            }

            return root;
        }

        void Join(Player left, Player right, Confidence confidence, string reason)
        {
            parent.TryAdd(left.Id, left.Id);
            parent.TryAdd(right.Id, right.Id);

            var a = Root(left.Id);
            var b = Root(right.Id);

            // Already together. A second rule agreeing about the same pair says
            // nothing new, and must not drag the set's confidence down with it -
            // two identical names are also an initial against a forename.
            if (a == b) return;

            var seen = new SortedSet<string>(StringComparer.Ordinal) { reason };
            var weak = confidence;

            foreach (var side in new[] { a, b })
            {
                if (reasons.TryGetValue(side, out var had)) seen.UnionWith(had);
                if (weakest.TryGetValue(side, out var was) && was > weak) weak = was;
            }

            parent[b] = a;

            // A set is only as sure as the loosest join holding it together,
            // because the secretary is asked to approve the whole set at once.
            weakest[a] = weak;
            reasons[a] = seen;
        }

        // Spelled identically. Nothing to weigh up: one of them is a mistake,
        // or they are one person in two seasons who were never linked.
        foreach (var group in players.GroupBy(Name).Where(g => g.Count() > 1))
        {
            var rows = group.ToList();
            for (var i = 1; i < rows.Count; i++)
                Join(rows[0], rows[i], Confidence.Certain, "the same name");
        }

        // Everything else is decided within a surname, and never on an initial
        // alone: Jack and Joel Martin share a surname and a first letter and
        // are two people. See PlayerNames for what counts as one name.
        foreach (var group in players.Where(p => Surname(p).Length > 0).GroupBy(Surname))
        {
            var rows = group.ToList();

            // The full forenames on the books for this surname. A bare initial
            // is only ever matched when exactly one of these starts with it.
            var full = rows.Where(p => !PlayerNames.IsInitial(Forename(p)))
                           .Select(Forename)
                           .Where(n => n.Length > 0)
                           .Distinct()
                           .ToList();

            foreach (var initial in rows.Where(p => PlayerNames.IsInitial(Forename(p))))
            {
                var letter = PlayerNames.Letter(Forename(initial));
                var candidates = full.Where(n => n[0] == letter).ToList();

                // Two Smiths behind one J is a question, not an answer.
                if (candidates.Count != 1) continue;

                foreach (var match in rows.Where(p => Forename(p) == candidates[0]))
                    Join(initial, match, Confidence.Likely, "an initial standing for a name");
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var left = Forename(rows[i]);
                if (left.Length == 0 || PlayerNames.IsInitial(left)) continue;

                for (var j = i + 1; j < rows.Count; j++)
                {
                    var right = Forename(rows[j]);
                    if (right.Length == 0 || PlayerNames.IsInitial(right)) continue;

                    if (PlayerNames.SameFamily(left, right))
                        Join(rows[i], rows[j], Confidence.Likely, "one name and its short form");
                    else if (PlayerNames.Shortened(left, right))
                        Join(rows[i], rows[j], Confidence.Likely, "one name shortened");
                    else if (PlayerNames.OneLetterApart(left, right))
                        Join(rows[i], rows[j], Confidence.Likely, "one letter apart");
                }
            }
        }

        var found = new List<Duplicate>();

        foreach (var set in parent.Keys.GroupBy(Root))
        {
            var rows = set.Select(id => by[id]).ToList();
            if (rows.Count < 2) continue;
            if (Resolved(rows)) continue;

            found.Add(new Duplicate(
                Best(rows),
                weakest.TryGetValue(set.Key, out var confidence) ? confidence : Confidence.Possible,
                reasons.TryGetValue(set.Key, out var why) ? string.Join("; ", why) : "similar names",
                rows.OrderBy(p => SeasonOrder(league, p)).ThenBy(p => Name(p)).ToList()));
        }

        return found
            .OrderByDescending(d => !d.Tidy)
            .ThenBy(d => d.Confidence)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// True when these rows are already dealt with: one person, one row each
    /// season, all linked. Nothing left to do.
    /// </summary>
    private static bool Resolved(IReadOnlyList<Player> rows)
    {
        if (rows.Select(Identity).Distinct().Count() > 1) return false;

        return rows.Where(p => p.SeasonId.HasValue)
                   .GroupBy(p => p.SeasonId!.Value)
                   .All(g => g.Count() == 1);
    }

    // ------------------------------------------------------------------ link

    /// <summary>
    /// Records that these rows are one person.
    /// </summary>
    /// <param name="keepId">
    /// The row whose identity the others take. If it already has one, that is
    /// the one kept - a player linked across three seasons who picks up a
    /// fourth must not have the other three detached from their career.
    /// </param>
    /// <remarks>
    /// Sets <see cref="Player.GlobalPlayerId"/> and nothing else. No row is
    /// deleted, no frame is rewritten, and <see cref="Unlink"/> undoes it.
    /// </remarks>
    public static Report Link(LeagueData league, Guid keepId, IEnumerable<Guid> alsoIds)
    {
        ArgumentNullException.ThrowIfNull(league);

        var wanted = new HashSet<Guid>(alsoIds) { keepId };

        var rows = league.Players.Where(p => wanted.Contains(p.Id)).ToList();
        var keeper = rows.FirstOrDefault(p => p.Id == keepId);

        if (keeper is null || rows.Count < 2) return new Report(0, 0);

        var identity = Identity(keeper);
        var changed = 0;

        foreach (var row in rows)
        {
            if (row.GlobalPlayerId == identity) continue;

            row.GlobalPlayerId = identity;
            row.ModifiedDate = DateTime.UtcNow;
            changed++;
        }

        return new Report(changed, 1);
    }

    /// <summary>Links every set that needs nothing decided: one row per season.</summary>
    public static Report LinkAll(LeagueData league, IEnumerable<Duplicate> duplicates)
    {
        ArgumentNullException.ThrowIfNull(league);

        var linked = 0;
        var groups = 0;

        foreach (var duplicate in duplicates.Where(d => d.Tidy))
        {
            // The fullest spelling, which is the row worth being known by.
            var keeper = duplicate.Players
                .OrderByDescending(p => p.GlobalPlayerId.HasValue)
                .ThenByDescending(p => $"{p.FirstName} {p.LastName}".Trim().Length)
                .First();

            var report = Link(league, keeper.Id, duplicate.Players.Select(p => p.Id));
            if (report.Linked == 0) continue;

            linked += report.Linked;
            groups++;
        }

        return new Report(linked, groups);
    }

    /// <summary>Takes rows back apart, so a wrong link is not a lasting one.</summary>
    public static Report Unlink(LeagueData league, IEnumerable<Guid> playerIds)
    {
        ArgumentNullException.ThrowIfNull(league);

        var wanted = new HashSet<Guid>(playerIds);
        var changed = 0;

        foreach (var player in league.Players.Where(p => wanted.Contains(p.Id) && p.GlobalPlayerId.HasValue))
        {
            player.GlobalPlayerId = null;
            player.ModifiedDate = DateTime.UtcNow;
            changed++;
        }

        return new Report(changed, changed > 0 ? 1 : 0);
    }

    // ------------------------------------------------------------ references

    /// <summary>
    /// How many stored records name each player, counted in one pass.
    /// </summary>
    /// <remarks>
    /// Shown so the secretary can see which row a season actually played. It
    /// walks the whole league, so it is counted once for everybody rather than
    /// once per player: asking per row turned a page of 400 names into forty
    /// million passes over the fixtures and hung the app.
    /// </remarks>
    public static Dictionary<Guid, int> CountReferences(LeagueData league)
    {
        ArgumentNullException.ThrowIfNull(league);

        var counts = new Dictionary<Guid, int>();

        void Count(Guid? id)
        {
            if (id is not Guid value) return;
            counts[value] = counts.TryGetValue(value, out var had) ? had + 1 : 1;
        }

        foreach (var fixture in league.Fixtures)
        {
            foreach (var frame in fixture.Frames)
            {
                Count(frame.HomePlayerId);
                Count(frame.AwayPlayerId);
                Count(frame.HomePlayer2Id);
                Count(frame.AwayPlayer2Id);
            }
        }

        foreach (var team in league.Teams) Count(team.CaptainPlayerId);

        foreach (var pair in league.DoublesPairings)
        {
            Count(pair.Player1Id);
            Count(pair.Player2Id);
        }

        foreach (var competition in league.Competitions)
        {
            foreach (var id in competition.ParticipantIds) Count(id);
            foreach (var id in competition.NoShowIds) Count(id);

            foreach (var doubles in competition.DoublesTeams)
            {
                Count(doubles.Player1Id);
                Count(doubles.Player2Id);
            }

            foreach (var round in competition.Rounds)
            {
                Count(round.OrganiserParticipantId);
                foreach (var match in round.Matches) CountMatch(match);
            }

            foreach (var group in competition.Groups.Concat(competition.PreviousGroups))
            {
                Count(group.OrganiserParticipantId);
                foreach (var id in group.ParticipantIds) Count(id);
                foreach (var match in group.Matches) CountMatch(match);
                foreach (var standing in group.Standings) Count(standing.ParticipantId);
            }
        }

        return counts;

        void CountMatch(CompetitionMatch match)
        {
            Count(match.Participant1Id);
            Count(match.Participant2Id);
            Count(match.WinnerId);
        }
    }

    // ----------------------------------------------------------------- names

    private static string Name(Player player) =>
        string.Join(' ', $"{player.FirstName} {player.LastName}"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();

    private static string Forename(Player player) => (player.FirstName ?? "").Trim().ToLowerInvariant();
    private static string Surname(Player player) => (player.LastName ?? "").Trim().ToLowerInvariant();


    /// <summary>The fullest spelling in the set, which is the one worth keeping.</summary>
    private static string Best(IEnumerable<Player> rows) =>
        rows.Select(p => $"{p.FirstName} {p.LastName}".Trim())
            .OrderByDescending(n => n.Length)
            .First();

    private static long SeasonOrder(LeagueData league, Player player)
    {
        var season = league.Seasons.FirstOrDefault(s => s.Id == player.SeasonId);
        return season is null ? long.MaxValue : -season.StartDate.Ticks;
    }

}
