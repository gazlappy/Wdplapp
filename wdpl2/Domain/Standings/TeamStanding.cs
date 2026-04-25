using System;
using System.Collections.Generic;

namespace Wdpl2.Models;

/// <summary>
/// Represents a team's standings in a league table.
/// Used as the canonical output of <see cref="Helpers.StandingsCalculator"/>.
/// </summary>
public class TeamStanding
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = "";
    public int Position { get; set; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int FramesFor { get; set; }
    public int FramesAgainst { get; set; }
    public int FrameDifference => FramesFor - FramesAgainst;
    public int Deducted { get; set; }
    public int Points { get; set; }

    /// <summary>Recent match outcomes (W/L/D), newest first when form tracking is enabled.</summary>
    public List<char> RecentForm { get; set; } = [];
}
