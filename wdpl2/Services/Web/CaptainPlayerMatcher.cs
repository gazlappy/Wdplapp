using Wdpl2.Models;

namespace Wdpl2.Services.Web;

/// <summary>
/// Suggests who a captain's new player might already be.
/// </summary>
/// <remarks>
/// Captains type a name into one box on the night, so what arrives is rarely
/// spelled the way the app holds it — "test" for "Test test", a forename for
/// someone on file by both names. Matching is therefore deliberately generous
/// and deliberately non-binding: it proposes, the secretary decides. Nothing
/// here merges anyone on its own.
/// </remarks>
public static class CaptainPlayerMatcher
{
    public sealed record Candidate(Player Player, bool IsExact, string Reason);

    /// <summary>
    /// Players already in the season who could be the person a captain added.
    /// </summary>
    /// <param name="unassignedOnly">
    /// Restricts matching to players not yet in a squad. A captain adding
    /// someone who turns up is usually a player on file but not placed, and a
    /// player who already has a team is far more likely to be a different
    /// person of the same name.
    /// </param>
    public static List<Candidate> Suggest(
        string incomingName,
        Guid seasonId,
        IEnumerable<Player> players,
        bool unassignedOnly = true)
    {
        var incoming = Normalise(incomingName);
        if (incoming.Length == 0) return new List<Candidate>();

        var matches = new List<Candidate>();

        foreach (var player in players)
        {
            if (player.SeasonId != seasonId) continue;
            if (unassignedOnly && player.TeamId.HasValue) continue;

            var full = Normalise($"{player.FirstName} {player.LastName}");
            if (full.Length == 0) continue;

            if (full == incoming)
            {
                matches.Add(new Candidate(player, true, "same name"));
                continue;
            }

            // A single word from the captain is usually a forename or the name
            // everyone calls them by, so match it against either part rather
            // than insisting on the full spelling.
            if (!incoming.Contains(' '))
            {
                var first = Normalise(player.FirstName ?? "");
                var last = Normalise(player.LastName ?? "");

                if (incoming == first || incoming == last)
                    matches.Add(new Candidate(player, false, $"matches part of {Describe(player)}"));
            }
        }

        return matches
            .OrderByDescending(m => m.IsExact)
            .ThenBy(m => Describe(m.Player), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static string Describe(Player player)
    {
        var name = $"{player.FirstName} {player.LastName}".Trim();
        return name.Length == 0 ? "(unnamed)" : name;
    }

    /// <summary>Casing and spacing are how people type, not who they are.</summary>
    private static string Normalise(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
              .ToLowerInvariant();
}
