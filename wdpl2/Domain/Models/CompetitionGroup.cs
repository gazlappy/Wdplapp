namespace Wdpl2.Models
{
    /// <summary>
    /// A group in a group stage competition
    /// </summary>
    public sealed class CompetitionGroup
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public int GroupNumber { get; set; }

        /// <summary>Which round of groups this belongs to (1 = first round, 2 = second, etc.)</summary>
        public int GroupRound { get; set; } = 1;

        /// <summary>The venue this group is assigned to play at.</summary>
        public Guid? VenueId { get; set; }

        /// <summary>Display name of the assigned venue.</summary>
        public string? VenueName { get; set; }

        /// <summary>The specific table ID at the venue this group plays on.</summary>
        public Guid? TableId { get; set; }

        /// <summary>Display label of the assigned table (e.g. "Table 1").</summary>
        public string? TableLabel { get; set; }

        /// <summary>Which table number at the venue (1-based, legacy fallback).</summary>
        public int TableNumber { get; set; }

        /// <summary>Participants in this group</summary>
        public List<Guid> ParticipantIds { get; set; } = new();

        /// <summary>Group stage matches (round robin within group)</summary>
        public List<CompetitionMatch> Matches { get; set; } = new();

        /// <summary>Group standings (calculated from matches)</summary>
        public List<GroupStanding> Standings { get; set; } = new();

        /// <summary>Formatted venue and table assignment for display.</summary>
        public string VenueDisplay => VenueId.HasValue && !string.IsNullOrEmpty(VenueName)
            ? (!string.IsNullOrEmpty(TableLabel) ? $"{VenueName} — {TableLabel}" : $"{VenueName}")
            : "";

        public override string ToString() => Name;
    }

    /// <summary>
    /// Standing of a participant within a group
    /// </summary>
    public sealed class GroupStanding
    {
        public Guid ParticipantId { get; set; }
        public int Position { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int FramesFor { get; set; }
        public int FramesAgainst { get; set; }
        public int FrameDifference => FramesFor - FramesAgainst;
        public int Points { get; set; }
    }
}
