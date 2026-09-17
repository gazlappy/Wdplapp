using Wdpl2.Models;

namespace Wdpl2.Domain.Players;

/// <summary>
/// Finds the same person recorded twice, and puts them back together.
/// </summary>
/// <remarks>
/// A player row belongs to one season, so the same person legitimately has one
/// row per season - that is not a duplicate, and collapsing them would empty a
/// season's roster. Two rows in the SAME season are a duplicate, and splitting
/// somebody's record in half is exactly what a merge is for.
/// <para>
/// So a merge does two different things at once, and says which:
/// </para>
/// <list type="bullet">
/// <item>Rows in one season collapse into one, and everything that pointed at
/// the others is repointed at the survivor.</item>
/// <item>Rows in different seasons are linked by <see cref="Player.GlobalPlayerId"/>,
/// which is what career stats and achievements read. Nothing is deleted.</item>
/// </list>
/// <para>
/// Nothing here guesses: <see cref="Find"/> proposes and the secretary decides,
/// the same bargain <c>CaptainPlayerMatcher</c> makes.
/// </para>
/// </remarks>
public static class PlayerMerge
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

        /// <summary>True when merging would delete rows rather than only link them.</summary>
        public bool Collapses => SeasonsWithClash > 0;
    }

    /// <summary>What a merge actually did.</summary>
    public sealed record Report(int Removed, int Linked, int References)
    {
        public override string ToString()
        {
            var parts = new List<string>();
            if (Removed > 0) parts.Add($"removed {Removed} duplicate row(s)");
            if (References > 0) parts.Add($"moved {References} reference(s) onto the one that stays");
            if (Linked > 0) parts.Add($"linked {Linked} season(s) as the same person");

            if (parts.Count == 0) return "Nothing to merge.";

            var said = string.Join(", ", parts);
            return char.ToUpper(said[0]) + said[1..] + ".";
        }
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
            if (!parent.TryGetValue(id, out var up) || up == id) return id;
            var root = Root(up);
            parent[id] = root;
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

        // "D Marsh" and "Dave Marsh". Common because a captain writes what fits
        // on the card and the app holds what is on the registration form.
        foreach (var group in players.GroupBy(Initialled).Where(g => g.Key.Length > 0 && g.Count() > 1))
        {
            var rows = group.ToList();
            for (var i = 1; i < rows.Count; i++)
                Join(rows[0], rows[i], Confidence.Likely, "an initial against a forename");
        }

        // One letter out, same surname: a typo rather than a second person.
        foreach (var group in players.Where(p => Surname(p).Length > 0).GroupBy(Surname))
        {
            var rows = group.ToList();
            for (var i = 0; i < rows.Count; i++)
            {
                for (var j = i + 1; j < rows.Count; j++)
                {
                    if (Within(Forename(rows[i]), Forename(rows[j]), 1))
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
            .OrderBy(d => d.Confidence)
            .ThenByDescending(d => d.Collapses)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// True when these rows are already dealt with: one person, one row each
    /// season, all linked. Nothing left to merge.
    /// </summary>
    private static bool Resolved(IReadOnlyList<Player> rows)
    {
        if (rows.Select(Identity).Distinct().Count() > 1) return false;

        return rows.Where(p => p.SeasonId.HasValue)
                   .GroupBy(p => p.SeasonId!.Value)
                   .All(g => g.Count() == 1);
    }

    // ----------------------------------------------------------------- merge

    /// <summary>
    /// Makes these rows one person.
    /// </summary>
    /// <param name="keepId">
    /// The row whose name and details survive, and whose identity the rest take.
    /// </param>
    /// <remarks>
    /// Rows sharing a season collapse into one - the survivor being whichever
    /// the league has most invested in, unless that is the row the secretary
    /// chose to keep. Rows in other seasons are only linked.
    /// </remarks>
    public static Report Apply(LeagueData league, Guid keepId, IEnumerable<Guid> alsoIds)
    {
        ArgumentNullException.ThrowIfNull(league);

        var wanted = new HashSet<Guid>(alsoIds) { keepId };

        var rows = league.Players.Where(p => wanted.Contains(p.Id)).ToList();
        var keeper = rows.FirstOrDefault(p => p.Id == keepId);

        if (keeper is null || rows.Count < 2) return new Report(0, 0, 0);

        var identity = Identity(keeper);
        var removed = 0;
        var references = 0;

        // Within a season there can be only one. Between seasons there must be
        // one each, or that season loses the player altogether.
        foreach (var season in rows.GroupBy(p => p.SeasonId))
        {
            var survivor = season.Contains(keeper) ? keeper : Preferred(league, season.ToList());

            foreach (var loser in season.Where(p => p.Id != survivor.Id))
            {
                references += Repoint(league, loser.Id, survivor.Id);
                Absorb(survivor, loser);
                league.Players.Remove(loser);
                removed++;
            }

            survivor.GlobalPlayerId = identity;
            survivor.ModifiedDate = DateTime.UtcNow;
        }

        var linked = league.Players.Count(p => p.GlobalPlayerId == identity);

        return new Report(removed, linked, references);
    }

    /// <summary>
    /// Which row of a season the league has most invested in.
    /// </summary>
    /// <remarks>
    /// A row with frames against it is the one the season actually played;
    /// keeping the empty one and repointing onto it would work, but it throws
    /// away the row every other record already agrees about.
    /// </remarks>
    private static Player Preferred(LeagueData league, List<Player> rows) =>
        rows.OrderByDescending(p => p.TeamId.HasValue)
            .ThenByDescending(p => References(league, p.Id))
            .ThenBy(p => p.CreatedDate)
            .First();

    /// <summary>Keeps anything the losing row had that the survivor does not.</summary>
    private static void Absorb(Player survivor, Player loser)
    {
        survivor.TeamId ??= loser.TeamId;

        if (string.IsNullOrWhiteSpace(survivor.Notes) && !string.IsNullOrWhiteSpace(loser.Notes))
            survivor.Notes = loser.Notes;

        // A person who is playing is playing, whichever row said so.
        if (loser.IsActive) survivor.IsActive = true;

        survivor.TransferHistory.AddRange(loser.TransferHistory);
        survivor.Availability.AddRange(loser.Availability);
    }

    // ------------------------------------------------------------ the fiddly bit

    /// <summary>How many stored records name this player.</summary>
    public static int References(LeagueData league, Guid playerId) =>
        Rewrite(league, playerId, playerId, dryRun: true);

    /// <summary>Points every stored reference at a different player.</summary>
    private static int Repoint(LeagueData league, Guid from, Guid to) =>
        Rewrite(league, from, to, dryRun: false);

    /// <summary>
    /// Every place the league stores a player's id, in one list.
    /// </summary>
    /// <remarks>
    /// Counting and rewriting are the same walk, so a reference that is counted
    /// is a reference that gets moved. Keeping them apart is how one gets
    /// forgotten and a merge silently orphans a frame.
    /// </remarks>
    private static int Rewrite(LeagueData league, Guid from, Guid to, bool dryRun)
    {
        var hits = 0;

        Guid? Swap(Guid? value)
        {
            if (value != from) return value;
            hits++;
            return dryRun ? value : to;
        }

        void SwapList(List<Guid> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != from) continue;
                hits++;
                if (!dryRun) list[i] = to;
            }

            if (!dryRun)
            {
                // The survivor may already have been in the list.
                var seen = new HashSet<Guid>();
                list.RemoveAll(id => !seen.Add(id));
            }
        }

        foreach (var fixture in league.Fixtures)
        {
            foreach (var frame in fixture.Frames)
            {
                frame.HomePlayerId = Swap(frame.HomePlayerId);
                frame.AwayPlayerId = Swap(frame.AwayPlayerId);
                frame.HomePlayer2Id = Swap(frame.HomePlayer2Id);
                frame.AwayPlayer2Id = Swap(frame.AwayPlayer2Id);
            }
        }

        foreach (var team in league.Teams)
            team.CaptainPlayerId = Swap(team.CaptainPlayerId);

        // Another player may name this one as their cross-season identity.
        foreach (var player in league.Players.Where(p => p.Id != from))
            player.GlobalPlayerId = Swap(player.GlobalPlayerId);

        foreach (var pair in league.DoublesPairings)
        {
            pair.Player1Id = Swap(pair.Player1Id);
            pair.Player2Id = Swap(pair.Player2Id);
        }

        foreach (var competition in league.Competitions)
        {
            SwapList(competition.ParticipantIds);
            SwapList(competition.NoShowIds);

            // A doubles pair names two players outright, not optionally.
            foreach (var doubles in competition.DoublesTeams)
            {
                if (doubles.Player1Id == from) { hits++; if (!dryRun) doubles.Player1Id = to; }
                if (doubles.Player2Id == from) { hits++; if (!dryRun) doubles.Player2Id = to; }
            }

            foreach (var round in competition.Rounds)
            {
                round.OrganiserParticipantId = Swap(round.OrganiserParticipantId);
                foreach (var match in round.Matches) SwapMatch(match);
            }

            foreach (var group in competition.Groups.Concat(competition.PreviousGroups))
            {
                SwapList(group.ParticipantIds);
                group.OrganiserParticipantId = Swap(group.OrganiserParticipantId);

                foreach (var match in group.Matches) SwapMatch(match);
                foreach (var standing in group.Standings)
                {
                    if (standing.ParticipantId != from) continue;
                    hits++;
                    if (!dryRun) standing.ParticipantId = to;
                }
            }
        }

        // The website's id for a player the secretary linked to this one.
        foreach (var key in league.CollectedWebPlayers
                     .Where(kv => kv.Value == from)
                     .Select(kv => kv.Key)
                     .ToList())
        {
            hits++;
            if (!dryRun) league.CollectedWebPlayers[key] = to;
        }

        return hits;

        void SwapMatch(CompetitionMatch match)
        {
            match.Participant1Id = Swap(match.Participant1Id);
            match.Participant2Id = Swap(match.Participant2Id);
            match.WinnerId = Swap(match.WinnerId);
        }
    }

    // ----------------------------------------------------------------- names

    private static string Name(Player player) =>
        string.Join(' ', $"{player.FirstName} {player.LastName}"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();

    private static string Forename(Player player) => (player.FirstName ?? "").Trim().ToLowerInvariant();
    private static string Surname(Player player) => (player.LastName ?? "").Trim().ToLowerInvariant();

    /// <summary>"Dave Marsh" and "D Marsh" both come out as "d marsh".</summary>
    private static string Initialled(Player player)
    {
        var first = Forename(player);
        var last = Surname(player);

        return first.Length == 0 || last.Length == 0 ? "" : $"{first[0]} {last}";
    }

    /// <summary>The fullest spelling in the set, which is the one worth keeping.</summary>
    private static string Best(IEnumerable<Player> rows) =>
        rows.Select(p => $"{p.FirstName} {p.LastName}".Trim())
            .OrderByDescending(n => n.Length)
            .First();

    private static int SeasonOrder(LeagueData league, Player player)
    {
        var season = league.Seasons.FirstOrDefault(s => s.Id == player.SeasonId);
        return season is null ? int.MaxValue : -(int)(season.StartDate.Ticks / TimeSpan.TicksPerDay);
    }

    /// <summary>True when two names are no more than <paramref name="allowed"/> edits apart.</summary>
    private static bool Within(string left, string right, int allowed)
    {
        if (left.Length == 0 || right.Length == 0) return false;
        if (left == right) return false;                       // handled as the same name
        if (Math.Abs(left.Length - right.Length) > allowed) return false;

        // Two letters is too short for one letter of difference to mean a typo:
        // "Jo" and "Al" would pair up.
        if (Math.Min(left.Length, right.Length) <= 3) return false;

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++) previous[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length] <= allowed;
    }
}
