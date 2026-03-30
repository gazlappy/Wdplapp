using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Helpers;

/// <summary>
/// Centralised league standings calculation.
/// All league-table callers (ViewModel, code-behind, export, website, simulator) should use
/// this class so that scoring rules stay consistent.
///
/// Scoring formula (WDPL): Points = Frames Won + Win/Draw Bonus − Penalties.
/// </summary>
public static class StandingsCalculator
{
    /// <summary>
    /// Calculate raw standings for the given teams and fixtures.
    /// Results are NOT sorted — call <see cref="StandingsSorter.Sort{T}"/> afterwards.
    /// </summary>
    /// <param name="teams">Teams to include in the standings.</param>
    /// <param name="fixtures">Completed fixtures (pre-filtered or raw — fixtures with no frames are skipped).</param>
    /// <param name="settings">App settings containing win/draw bonus values.</param>
    /// <param name="trackForm">When true, populates <see cref="TeamStanding.RecentForm"/> (newest first).</param>
    public static List<TeamStanding> Calculate(
        IEnumerable<Team> teams,
        IEnumerable<Fixture> fixtures,
        AppSettings settings,
        bool trackForm = false)
    {
        var standings = new Dictionary<Guid, TeamStanding>();
        foreach (var team in teams)
        {
            standings[team.Id] = new TeamStanding
            {
                TeamId = team.Id,
                TeamName = team.Name ?? "Unknown"
            };
        }

        // When tracking form, process newest-first so Take(N) yields most recent results.
        var completed = fixtures.Where(f => f.Frames.Count > 0);
        var ordered = trackForm
            ? completed.OrderByDescending(f => f.Date)
            : completed;

        foreach (var fixture in ordered)
        {
            var hasHome = standings.TryGetValue(fixture.HomeTeamId, out var home);
            var hasAway = standings.TryGetValue(fixture.AwayTeamId, out var away);

            if (hasHome)
                ProcessSide(home!, fixture.HomeScore, fixture.AwayScore, fixture.HomeLatePenalty, settings, trackForm);
            if (hasAway)
                ProcessSide(away!, fixture.AwayScore, fixture.HomeScore, fixture.AwayLatePenalty, settings, trackForm);

            // Cancellation penalty
            if (fixture.CancelledByTeam != FrameWinner.None && fixture.CancellationPenalty > 0)
            {
                if (fixture.CancelledByTeam == FrameWinner.Home && hasHome)
                {
                    home!.Deducted += fixture.CancellationPenalty;
                    home.Points -= fixture.CancellationPenalty;
                }
                else if (fixture.CancelledByTeam == FrameWinner.Away && hasAway)
                {
                    away!.Deducted += fixture.CancellationPenalty;
                    away.Points -= fixture.CancellationPenalty;
                }
            }
        }

        return [.. standings.Values];
    }

    /// <summary>
    /// Process a single fixture result for two standings rows.
    /// Useful for the What-If simulator to layer simulated results on top of calculated standings.
    /// Does NOT apply late/cancellation penalties (simulated fixtures don't carry penalty data).
    /// </summary>
    public static void ProcessFixtureResult(
        TeamStanding home, TeamStanding away,
        int homeScore, int awayScore,
        AppSettings settings)
    {
        home.Played++;
        away.Played++;
        home.FramesFor += homeScore;
        home.FramesAgainst += awayScore;
        away.FramesFor += awayScore;
        away.FramesAgainst += homeScore;

        if (homeScore > awayScore)
        {
            home.Won++;
            away.Lost++;
            home.Points += homeScore + settings.MatchWinBonus;
            away.Points += awayScore;
        }
        else if (awayScore > homeScore)
        {
            away.Won++;
            home.Lost++;
            away.Points += awayScore + settings.MatchWinBonus;
            home.Points += homeScore;
        }
        else
        {
            home.Drawn++;
            away.Drawn++;
            home.Points += homeScore + settings.MatchDrawBonus;
            away.Points += awayScore + settings.MatchDrawBonus;
        }
    }

    private static void ProcessSide(
        TeamStanding standing, int teamScore, int oppScore, int latePenalty,
        AppSettings settings, bool trackForm)
    {
        standing.Played++;
        standing.FramesFor += teamScore;
        standing.FramesAgainst += oppScore;

        if (teamScore > oppScore)
        {
            standing.Won++;
            standing.Points += teamScore + settings.MatchWinBonus;
            if (trackForm) standing.RecentForm.Add('W');
        }
        else if (oppScore > teamScore)
        {
            standing.Lost++;
            standing.Points += teamScore;
            if (trackForm) standing.RecentForm.Add('L');
        }
        else
        {
            standing.Drawn++;
            standing.Points += teamScore + settings.MatchDrawBonus;
            if (trackForm) standing.RecentForm.Add('D');
        }

        // Late card penalty
        if (latePenalty > 0)
        {
            standing.Deducted += latePenalty;
            standing.Points -= latePenalty;
        }
    }
}
