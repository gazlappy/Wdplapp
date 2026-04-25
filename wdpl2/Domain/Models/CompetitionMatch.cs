namespace Wdpl2.Models
{
    /// <summary>
    /// A single match in a competition
    /// </summary>
    public sealed class CompetitionMatch
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? Participant1Id { get; set; }
        public Guid? Participant2Id { get; set; }
        public Guid? WinnerId { get; set; }
        public int Participant1Score { get; set; }
        public int Participant2Score { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public bool IsComplete { get; set; }
        public string? Notes { get; set; }

        /// <summary>For losers bracket in double elimination</summary>
        public bool IsLosersBracket { get; set; }

        /// <summary>Group ID if this match belongs to a group stage</summary>
        public Guid? GroupId { get; set; }

        /// <summary>The venue this match is assigned to play at.</summary>
        public Guid? VenueId { get; set; }

        /// <summary>Display name of the assigned venue.</summary>
        public string? VenueName { get; set; }

        /// <summary>The specific table ID at the venue this match plays on.</summary>
        public Guid? TableId { get; set; }

        /// <summary>Display label of the assigned table (e.g. "Table 1").</summary>
        public string? TableLabel { get; set; }

        /// <summary>Formatted venue and table assignment for display.</summary>
        public string VenueDisplay => VenueId.HasValue && !string.IsNullOrEmpty(VenueName)
            ? (!string.IsNullOrEmpty(TableLabel) ? $"{VenueName} — {TableLabel}" : $"{VenueName}")
            : "";

        public override string ToString() => $"Match {Id}";
    }
}
