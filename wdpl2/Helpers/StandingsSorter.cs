using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Helpers;

/// <summary>
/// Shared standings sort logic that respects the configurable tiebreaker order in <see cref="AppSettings"/>.
/// All league table, export, website, and simulator sort paths should call through here
/// so that tiebreaker behaviour is consistent across the entire app.
/// </summary>
public static class StandingsSorter
{
    /// <summary>
    /// Sort standings by Points (descending) then by the configured tiebreaker criteria.
    /// </summary>
    /// <typeparam name="T">Any standings row type.</typeparam>
    /// <param name="items">Unsorted standings rows.</param>
    /// <param name="settings">App settings containing <see cref="AppSettings.TiebreakerOrder"/>.</param>
    /// <param name="getPoints">Selector for the points value.</param>
    /// <param name="getFramesFor">Selector for frames scored.</param>
    /// <param name="getFramesAgainst">Selector for frames conceded.</param>
    /// <param name="getWins">Selector for matches won.</param>
    /// <param name="getTeamId">Selector for the team/participant id (used for head-to-head).</param>
    /// <param name="fixtures">Completed fixtures – only required when HeadToHead is in the tiebreaker list.</param>
    public static List<T> Sort<T>(
        IEnumerable<T> items,
        AppSettings? settings,
        Func<T, int> getPoints,
        Func<T, int> getFramesFor,
        Func<T, int> getFramesAgainst,
        Func<T, int> getWins,
        Func<T, Guid> getTeamId,
        IReadOnlyList<Fixture>? fixtures = null)
    {
        var order = settings?.TiebreakerOrder;
        if (order == null || order.Count == 0)
        {
            // Fallback: default 3-tier sort
            return items
                .OrderByDescending(getPoints)
                .ThenByDescending(x => getFramesFor(x) - getFramesAgainst(x))
                .ThenByDescending(getFramesFor)
                .ToList();
        }

        // Build head-to-head lookup lazily (only if needed)
        Dictionary<(Guid, Guid), int>? h2hCache = null;

        IOrderedEnumerable<T> sorted = items.OrderByDescending(getPoints);

        foreach (var criterion in order)
        {
            var captured = criterion; // avoid closure issues
            sorted = captured switch
            {
                TiebreakerCriterion.FrameDifference =>
                    sorted.ThenByDescending(x => getFramesFor(x) - getFramesAgainst(x)),

                TiebreakerCriterion.FramesFor =>
                    sorted.ThenByDescending(getFramesFor),

                TiebreakerCriterion.Wins =>
                    sorted.ThenByDescending(getWins),

                TiebreakerCriterion.HeadToHead =>
                    sorted.ThenByDescending(x =>
                    {
                        h2hCache ??= BuildHeadToHeadCache(fixtures);
                        return GetH2HPoints(getTeamId(x), h2hCache);
                    }),

                _ => sorted
            };
        }

        return sorted.ToList();
    }

    /// <summary>
    /// Builds a cache of head-to-head frame differences: (teamA, teamB) → net frames for teamA.
    /// </summary>
    private static Dictionary<(Guid, Guid), int> BuildHeadToHeadCache(IReadOnlyList<Fixture>? fixtures)
    {
        var cache = new Dictionary<(Guid, Guid), int>();
        if (fixtures == null) return cache;

        foreach (var f in fixtures)
        {
            if (f.Frames.Count == 0) continue;

            var key = (f.HomeTeamId, f.AwayTeamId);
            var reverseKey = (f.AwayTeamId, f.HomeTeamId);

            if (!cache.ContainsKey(key)) cache[key] = 0;
            if (!cache.ContainsKey(reverseKey)) cache[reverseKey] = 0;

            cache[key] += f.HomeScore - f.AwayScore;
            cache[reverseKey] += f.AwayScore - f.HomeScore;
        }

        return cache;
    }

    /// <summary>
    /// Sums the net head-to-head frame difference for a team across all opponents.
    /// Higher is better.
    /// </summary>
    private static int GetH2HPoints(Guid teamId, Dictionary<(Guid, Guid), int> h2hCache)
    {
        return h2hCache
            .Where(kvp => kvp.Key.Item1 == teamId)
            .Sum(kvp => kvp.Value);
    }
}
