using Wdpl2.Models;

namespace Wdpl2.Domain.Players;

/// <summary>
/// One person's whole record, however many season rows it is spread across.
/// </summary>
/// <remarks>
/// Every page that shows a career - career stats, achievements, frame stats, a
/// player's profile - has to answer the same question first: which rows are
/// this person? They each used to answer it differently, and all four answers
/// were wrong in their own way. One dropped single-season players entirely;
/// two threw away every row but the biggest when two sets shared a name; the
/// profile page merged anyone with the same name whether or not the league had
/// said they were the same person.
/// <para>
/// There is one answer now, and it is the one the secretary gave: rows tied
/// together by <see cref="Player.GlobalPlayerId"/> are one career, and a row
/// nobody tied to anything is a career of its own. Names are not consulted.
/// Two players called Dave Smith stay two players until somebody says
/// otherwise, and the moment somebody does, every page agrees.
/// </para>
/// </remarks>
public static class PlayerCareers
{
    /// <summary>One person, and every row that is them.</summary>
    public sealed record Career(Guid Id, string Name, IReadOnlyList<Player> Rows)
    {
        public IReadOnlyList<Guid> PlayerIds => Rows.Select(p => p.Id).ToList();

        public IReadOnlyList<Guid> SeasonIds =>
            Rows.Where(p => p.SeasonId.HasValue).Select(p => p.SeasonId!.Value).Distinct().ToList();

        public int Seasons => SeasonIds.Count;

        /// <summary>True when more than one row was tied together to make this.</summary>
        public bool IsLinked => Rows.Count > 1;

        public string Describe() => Seasons == 1 ? "1 season" : $"{Seasons} seasons";
    }

    /// <summary>The identity a row belongs to.</summary>
    public static Guid Identity(Player player) => player.GlobalPlayerId ?? player.Id;

    /// <summary>
    /// Everybody the league has ever had, one entry each.
    /// </summary>
    public static List<Career> All(LeagueData league)
    {
        ArgumentNullException.ThrowIfNull(league);

        return league.Players
            .Where(p => p.FullName.Trim().Length > 0)
            .GroupBy(Identity)
            .Select(g => new Career(g.Key, Best(g), g.ToList()))
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The career a given id belongs to, whether that is an identity or a row.
    /// </summary>
    /// <remarks>
    /// Pages are navigated to with whichever of the two the caller had, so both
    /// are accepted rather than leaving each caller to work out which it holds.
    /// </remarks>
    public static Career? For(LeagueData league, Guid id)
    {
        ArgumentNullException.ThrowIfNull(league);

        var rows = league.Players.Where(p => Identity(p) == id).ToList();

        if (rows.Count == 0)
        {
            var row = league.Players.FirstOrDefault(p => p.Id == id);
            if (row is null) return null;

            rows = league.Players.Where(p => Identity(p) == Identity(row)).ToList();
        }

        return new Career(Identity(rows[0]), Best(rows), rows);
    }

    /// <summary>
    /// What to call somebody recorded under several spellings.
    /// </summary>
    /// <remarks>
    /// The fullest spelling, because "David Howell" says more than "D Howell" -
    /// but a name typed in capitals is a spelling of convenience rather than a
    /// fuller one, so it loses to the same name written properly.
    /// </remarks>
    private static string Best(IEnumerable<Player> rows) =>
        rows.Select(p => p.FullName.Trim())
            .Where(n => n.Length > 0)
            .DefaultIfEmpty("(unnamed)")
            .OrderByDescending(n => n.Length)
            .ThenBy(n => n == n.ToUpperInvariant())
            .First();
}
