using Wdpl2.Models;

namespace Wdpl2.Domain.Competitions;

/// <summary>
/// One team-knockout tie, seen as something a scorecard can be opened on.
/// </summary>
/// <remarks>
/// A cup tie is played exactly like a league night - same frames, same sign-off,
/// same collect - so it travels to the website through the fixtures table rather
/// than growing a second kind of card. It is marked <c>kind = "cup"</c> there so
/// the league's own pages (tables, results, fixture list) leave it alone, and so
/// the server can tell that the nomination order is the cup's rather than the
/// league's.
/// <para>
/// Which team is home is decided on the night by a toss, so the two teams are
/// listed here in the order the draw made them. The toss changes who fills the
/// card in first, not which column a frame is won in.
/// </para>
/// </remarks>
public sealed record CupTie(
    Guid Id,
    Guid CompetitionId,
    string CompetitionName,
    Guid RoundId,
    string RoundName,
    Guid HomeTeamId,
    Guid AwayTeamId,
    DateTime Date,
    Guid? VenueId,
    int HomeScore,
    int AwayScore,
    bool IsComplete)
{
    public string Describe(IReadOnlyDictionary<Guid, string> teams) =>
        $"{teams.GetValueOrDefault(HomeTeamId, "?")} v {teams.GetValueOrDefault(AwayTeamId, "?")}";

    /// <summary>
    /// Every tie in a season that has both teams known.
    /// </summary>
    /// <remarks>
    /// Ties already played are included, because publishing replaces the
    /// season's fixtures wholesale: dropping a finished tie would delete the
    /// row a collected card still points at. Deciding what may be OPENED is a
    /// separate question - see <see cref="IsComplete"/>.
    /// </remarks>
    public static List<CupTie> For(LeagueData league, Season season)
    {
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(season);

        var ties = new List<CupTie>();

        var comps = league.Competitions
            .Where(c => c.Format == CompetitionFormat.TeamKnockout && c.SeasonId == season.Id);

        foreach (var comp in comps)
        {
            foreach (var round in comp.Rounds.OrderBy(r => r.RoundNumber))
            {
                foreach (var match in round.Matches.OrderBy(m => m.Slot))
                {
                    // A bye, or a tie whose teams are still waiting on an
                    // earlier round, has nobody to hand a card to.
                    if (match.Participant1Id is not Guid home || match.Participant2Id is not Guid away)
                        continue;

                    ties.Add(new CupTie(
                        match.Id,
                        comp.Id,
                        comp.Name,
                        round.Id,
                        string.IsNullOrWhiteSpace(round.Name) ? $"Round {round.RoundNumber}" : round.Name,
                        home,
                        away,
                        match.ScheduledDate ?? round.Date ?? comp.StartDate ?? season.StartDate,
                        match.VenueId,
                        match.Participant1Score,
                        match.Participant2Score,
                        match.IsComplete));
                }
            }
        }

        return ties;
    }

    /// <summary>Finds the round and match a collected cup card belongs to.</summary>
    public static (Competition Comp, CompetitionRound Round, CompetitionMatch Match)? Locate(
        Competition competition, Guid matchId)
    {
        ArgumentNullException.ThrowIfNull(competition);

        foreach (var round in competition.Rounds)
        {
            var match = round.Matches.FirstOrDefault(m => m.Id == matchId);
            if (match is not null) return (competition, round, match);
        }

        return null;
    }
}
