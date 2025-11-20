using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Wdpl2.Models
{
    /// <summary>One scheduled match between two teams.</summary>
    public sealed class Fixture
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Optional links
        public Guid? SeasonId { get; set; }
        public Guid? DivisionId { get; set; }

        public Guid HomeTeamId { get; set; }
        public Guid AwayTeamId { get; set; }

        // Where it takes place (usually the home team's venue/table)
        public Guid? VenueId { get; set; }
        public Guid? TableId { get; set; }

        /// <summary>Local start date/time.</summary>
        public DateTime Date { get; set; }

        /// <summary>Frame-by-frame results.</summary>
        public List<FrameResult> Frames { get; set; } = new();

        /// <summary>Computed match score (winner is higher).</summary>
        [JsonIgnore] public int HomeScore => Frames.Count(f => f.Winner == FrameWinner.Home);
        [JsonIgnore] public int AwayScore => Frames.Count(f => f.Winner == FrameWinner.Away);

        public override string ToString() => $"{Date:ddd dd MMM} — {HomeTeamId} vs {AwayTeamId}";
    }

    public sealed class FrameResult
    {
        public int Number { get; set; } // 1-based

        public Guid? HomePlayerId { get; set; }
        public Guid? AwayPlayerId { get; set; }

        public FrameWinner Winner { get; set; } = FrameWinner.None;

        /// <summary>Ticked when the winning visit included an 8-ball (or “dish”).</summary>
        public bool EightBall { get; set; }
    }

    public enum FrameWinner
    {
        None = 0,
        Home = 1,
        Away = 2
    }
}
