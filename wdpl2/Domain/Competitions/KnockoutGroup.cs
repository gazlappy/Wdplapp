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

    /// <summary>The final itself, or null.</summary>
    public static CompetitionMatch? Final(CompetitionGroup group)
    {
        var last = FinalRound(group);
        return last == 0 ? null : group.Matches.FirstOrDefault(m => m.RoundNumber == last && m.Slot == 0);
    }

    /// <summary>Who won the group, or null while the final is still to play.</summary>
    public static Guid? Winner(CompetitionGroup group)
    {
        var final = Final(group);
        return final is { IsComplete: true } ? final.WinnerId : null;
    }

    /// <summary>The beaten finalist, or null.</summary>
    public static Guid? RunnerUp(CompetitionGroup group)
    {
        var final = Final(group);
        if (final is not { IsComplete: true } || final.WinnerId is null) return null;

        return final.WinnerId == final.Participant1Id ? final.Participant2Id : final.Participant1Id;
    }

    /// <summary>
    /// How many places a knockout can actually decide.
    /// </summary>
    /// <remarks>
    /// One if only the winner goes through, two if the beaten finalist does as
    /// well. Past that a knockout has nothing to say: the losing semi-finalists
    /// never played each other, so there is no third place without another
    /// match. Telling the secretary that is better than inventing an order.
    /// </remarks>
    public static int PlacesDecided(CompetitionGroup group)
    {
        if (Winner(group) is null) return 0;
        return RunnerUp(group) is null ? 1 : 2;
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

        var winner = Winner(group);
        var runnerUp = RunnerUp(group);

        foreach (var row in rows.Values)
        {
            row.Position = row.ParticipantId == winner ? 1
                         : row.ParticipantId == runnerUp ? 2
                         : 0;
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
