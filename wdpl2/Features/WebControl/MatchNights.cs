using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Domain.Competitions;
using Wdpl2.Domain.Fixtures;
using Wdpl2.Models;

namespace Wdpl2.Features.WebControl;

/// <summary>One match a card could be opened on, league night or cup tie.</summary>
public sealed record NightMatch(Guid Id, string Label, bool IsCup);

/// <summary>Everything still waiting for a card, gathered by the night it is played on.</summary>
public sealed record MatchNight(DateTime Date, List<NightMatch> Matches);

/// <summary>
/// Works out what "open the whole night" would open.
/// </summary>
public static class MatchNights
{
    /// <summary>
    /// The season's matches with no card yet, by date, earliest night first.
    /// </summary>
    /// <remarks>
    /// Cup ties sit beside the league's own fixtures: they are played on the
    /// same nights and scored the same way, so the secretary opening a Tuesday
    /// should not have to visit two pages to do it.
    /// <para>
    /// Left out: anything that already has a card on the website (opening it
    /// would be refused anyway), and anything the app already holds a score
    /// for, so opening a night twice cannot invite the captains to score a
    /// match that has been played.
    /// </para>
    /// </remarks>
    public static List<MatchNight> Waiting(LeagueData league, Season season, IEnumerable<Guid> withCards)
    {
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(season);

        var hasCard = withCards?.ToHashSet() ?? new HashSet<Guid>();
        var teams = league.Teams.ToDictionary(t => t.Id, t => t.Name ?? "?");
        var waiting = new List<(DateTime Date, NightMatch Match)>();

        foreach (var fixture in league.Fixtures.Where(f => f.SeasonId == season.Id))
        {
            if (hasCard.Contains(fixture.Id)) continue;
            if (fixture.Frames.Any(f => f.Winner != FrameWinner.None)) continue;

            var label = $"{teams.GetValueOrDefault(fixture.HomeTeamId, "?")} v {teams.GetValueOrDefault(fixture.AwayTeamId, "?")}";
            waiting.Add((fixture.Date.Date, new NightMatch(fixture.Id, label, IsCup: false)));
        }

        foreach (var tie in CupTie.For(league, season))
        {
            if (tie.IsComplete || hasCard.Contains(tie.Id)) continue;
            waiting.Add((tie.Date.Date, new NightMatch(tie.Id, $"{tie.CompetitionName}: {tie.Describe(teams)}", IsCup: true)));
        }

        return waiting
            .GroupBy(w => w.Date)
            .OrderBy(g => g.Key)
            .Select(g => new MatchNight(g.Key, g.Select(w => w.Match).ToList()))
            .ToList();
    }

    /// <summary>
    /// The night to offer first: tonight if there is one, else the next to come,
    /// else the last that went by. -1 when there are none at all.
    /// </summary>
    public static int Nearest(IReadOnlyList<MatchNight> nights, DateTime today)
    {
        ArgumentNullException.ThrowIfNull(nights);
        if (nights.Count == 0) return -1;

        for (var i = 0; i < nights.Count; i++)
            if (nights[i].Date >= today.Date) return i;

        return nights.Count - 1;
    }
}
