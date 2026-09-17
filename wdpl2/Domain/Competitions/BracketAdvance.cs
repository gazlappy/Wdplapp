using Wdpl2.Models;

namespace Wdpl2.Domain.Competitions;

/// <summary>
/// Where the winner of a knockout match goes next.
/// </summary>
/// <remarks>
/// The rule is only "two matches feed the one below them, top slot first", but
/// it is now applied from two places - the competition editor and a cup card
/// collected from the website - and a bracket that advanced a team one way in
/// the app and another way from the web would be worse than one that did not
/// advance at all.
/// </remarks>
public static class BracketAdvance
{
    /// <summary>Puts the winner of a finished match into the round below.</summary>
    public static void Advance(Competition competition, CompetitionRound round, CompetitionMatch match)
    {
        var (next, slotIsFirst) = NextSlot(competition, round, match);
        if (next is null || !match.WinnerId.HasValue) return;

        if (slotIsFirst) next.Participant1Id = match.WinnerId;
        else next.Participant2Id = match.WinnerId;
    }

    /// <summary>Takes back out whatever this match had sent through.</summary>
    public static void Clear(Competition competition, CompetitionRound round, CompetitionMatch match)
    {
        var (next, slotIsFirst) = NextSlot(competition, round, match);
        if (next is null) return;

        if (slotIsFirst) next.Participant1Id = null;
        else next.Participant2Id = null;
    }

    private static (CompetitionMatch? Match, bool SlotIsFirst) NextSlot(
        Competition competition, CompetitionRound round, CompetitionMatch match)
    {
        var nextRound = competition.Rounds.FirstOrDefault(r => r.RoundNumber == round.RoundNumber + 1);
        if (nextRound is null) return (null, false);

        var index = round.Matches.IndexOf(match);
        if (index < 0) return (null, false);

        var nextIndex = index / 2;
        if (nextIndex >= nextRound.Matches.Count) return (null, false);

        return (nextRound.Matches[nextIndex], index % 2 == 0);
    }
}
