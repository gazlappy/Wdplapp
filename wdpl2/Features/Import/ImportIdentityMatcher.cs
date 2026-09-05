using System.Text.RegularExpressions;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

public static class ImportIdentityMatcher
{
    public static string Normalize(string? name) => Regex.Replace((name ?? "").Trim(), @"\s+", " ").ToUpperInvariant();

    public static Player? MatchPlayer(IEnumerable<Player> players, Guid seasonId, string firstName, string lastName, Guid? teamId)
    {
        var matches = players.Where(p => p.SeasonId == seasonId &&
            Normalize(p.FirstName) == Normalize(firstName) && Normalize(p.LastName) == Normalize(lastName)).ToList();
        if (teamId.HasValue) matches = matches.Where(p => p.TeamId == teamId).ToList();
        if (matches.Count > 1)
            throw new InvalidDataException($"Player '{firstName} {lastName}' matches multiple records in the target season. Resolve the duplicate identities before importing.");
        return matches.SingleOrDefault();
    }
}
