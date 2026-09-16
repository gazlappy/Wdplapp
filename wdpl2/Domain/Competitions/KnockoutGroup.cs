using Wdpl2.Models;

namespace Wdpl2.Domain.Competitions;

/// <summary>
/// Reading a group that was drawn out and played as a knockout.
/// </summary>
/// <remarks>
/// A round-robin group is read off its table. A knockout has no table — who
/// went through is decided by the tree — so the same questions have to be
/// answered a different way.
/// <para>
/// Pure, and separate from both the editor and the website generator, because
/// all three need the same answers and were otherwise going to work them out
/// independently.
/// </para>
/// </remarks>
public static class KnockoutGroup
{
    /// <summary>A group whose matches form a tree rather than a round robin.</summary>
    public static bool IsKnockout(CompetitionGroup group) =>
        group.Matches.Any(m => m.RoundNumber > 0);

    /// <summary>The last round of the tree, or 0 if there is no tree.</summary>
    public static int FinalRound(CompetitionGroup group) =>
        IsKnockout(group) ? group.Matches.Max(m => m.RoundNumber) : 0;

    /// <summary>The ties of the last round played.</summary>
    /// <remarks>
    /// Not necessarily a final. A group that sends two through stops when two
    /// are left, so its last round is two ties and both winners qualify.
    /// </remarks>
    public static List<CompetitionMatch> LastRound(CompetitionGroup group)
    {
        var last = FinalRound(group);

        return last == 0
            ? new List<CompetitionMatch>()
            : group.Matches.Where(m => m.RoundNumber == last).OrderBy(m => m.Slot).ToList();
    }

    /// <summary>The final, when the group was played down to one winner.</summary>
    public static CompetitionMatch? Final(CompetitionGroup group)
    {
        var last = LastRound(group);
        return last.Count == 1 ? last[0] : null;
    }

    /// <summary>
    /// Who won the group, or null unless it was played down to one winner.
    /// </summary>
    public static Guid? Winner(CompetitionGroup group)
    {
        var final = Final(group);
        return final is { IsComplete: true } ? final.WinnerId : null;
    }

    /// <summary>The beaten finalist, when there was a final.</summary>
    public static Guid? RunnerUp(CompetitionGroup group)
    {
        var final = Final(group);
        if (final is not { IsComplete: true } || final.WinnerId is null) return null;

        return final.WinnerId == final.Participant1Id ? final.Participant2Id : final.Participant1Id;
    }

    /// <summary>
    /// Who goes through to the next draw, in the order their ties are listed.
    /// </summary>
    /// <remarks>
    /// The winners of the last round played. A group that stops with two left
    /// sends both through; one played to a final sends its winner, and its
    /// beaten finalist too if the competition asks for two.
    /// <para>
    /// Read off the tree rather than out of the stored standings, so a group
    /// collected by an older build - or one whose table was left over from when
    /// it was a round robin - still shows the right people as through.
    /// </para>
    /// </remarks>
    public static List<Guid> Through(CompetitionGroup group, int places)
    {
        var through = new List<Guid>();
        if (places < 1) return through;

        foreach (var tie in LastRound(group))
        {
            if (through.Count >= places) break;
            if (tie is { IsComplete: true, WinnerId: { } winner }) through.Add(winner);
        }

        // A group played all the way to a final leaves only one standing, so the
        // beaten finalist takes the second place when one is being offered.
        if (through.Count < places && RunnerUp(group) is { } runnerUp && !through.Contains(runnerUp))
        {
            through.Add(runnerUp);
        }

        return through;
    }

    /// <summary>
    /// How many places this group has actually decided.
    /// </summary>
    /// <remarks>
    /// Past the last round a knockout has nothing to say: players who never met
    /// cannot be separated. Telling the secretary that is better than inventing
    /// an order.
    /// </remarks>
    public static int PlacesDecided(CompetitionGroup group)
    {
        var last = LastRound(group);
        if (last.Count == 0 || last.Any(m => !m.IsComplete)) return 0;

        // Down to a final: the winner, and the beaten finalist behind them.
        return last.Count == 1 ? 2 : last.Count;
    }

    /// <summary>
    /// The group's result as standings, so the rest of the app can read it.
    /// </summary>
    /// <remarks>
    /// Positions come from the tree, not from points: first is the winner,
    /// second the beaten finalist. Frames are carried across so the figures
    /// shown are the ones that were played, but they decide nothing here.
    /// </remarks>
    public static List<GroupStanding> Standings(CompetitionGroup group)
    {
        if (!IsKnockout(group)) return group.Standings;

        var rows = new Dictionary<Guid, GroupStanding>();

        foreach (var id in group.ParticipantIds)
        {
            rows[id] = new GroupStanding { ParticipantId = id };
        }

        foreach (var match in group.Matches.Where(m => m.IsComplete))
        {
            // A bye is not a match anybody played, so it counts for nothing
            // beyond sending its player on.
            if (match.Participant1Id is null || match.Participant2Id is null) continue;

            Record(rows, match.Participant1Id.Value, match.Participant1Score, match.Participant2Score,
                   match.WinnerId == match.Participant1Id);
            Record(rows, match.Participant2Id.Value, match.Participant2Score, match.Participant1Score,
                   match.WinnerId == match.Participant2Id);
        }

        // Positions come from the tree: everyone still standing at the end is
        // through, in the order their ties are listed, and a final puts its
        // beaten finalist second.
        var through = Through(group, PlacesDecided(group));

        foreach (var row in rows.Values)
        {
            var at = through.IndexOf(row.ParticipantId);
            row.Position = at < 0 ? 0 : at + 1;
        }

        return rows.Values
            .OrderBy(r => r.Position == 0 ? int.MaxValue : r.Position)
            .ThenByDescending(r => r.Won)
            .ThenByDescending(r => r.FrameDifference)
            .ToList();
    }

    private static void Record(
        IReadOnlyDictionary<Guid, GroupStanding> rows, Guid id, int scored, int against, bool won)
    {
        if (!rows.TryGetValue(id, out var row)) return;

        row.Played++;
        row.FramesFor += scored;
        row.FramesAgainst += against;

        if (won) row.Won++;
        else row.Lost++;
    }
}
