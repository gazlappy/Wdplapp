namespace Wdpl2.Models
{
    /// <summary>
    /// A round in a knockout competition (e.g., Quarter-Finals, Semi-Finals, Final)
    /// </summary>
    public sealed class CompetitionRound
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public int RoundNumber { get; set; }
        public List<CompetitionMatch> Matches { get; set; } = new();

        /// <summary>Whether this round is part of a group stage</summary>
        public bool IsGroupStage { get; set; }

        /// <summary>Group ID if this round belongs to a specific group</summary>
        public Guid? GroupId { get; set; }

        /// <summary>Scheduled date for this round.</summary>
        public DateTime? Date { get; set; }

        /// <summary>
        /// The participant nominated to run this round at the venue.
        /// </summary>
        /// <remarks>
        /// Groups have carried this for a while; a knockout round played at a
        /// venue needs the same person, and for the same reason - somebody has
        /// to be holding the phone.
        /// </remarks>
        public Guid? OrganiserParticipantId { get; set; }

        /// <summary>
        /// The PIN that opens this round on the website, in plain text.
        /// </summary>
        /// <remarks>
        /// Kept here so the secretary can read it back and pass it on. Only its
        /// hash is ever published; see <see cref="CompetitionGroup.RunnerPin"/>.
        /// </remarks>
        public string? RunnerPin { get; set; }

        /// <summary>Venues/tables available for this round.</summary>
        public List<CompetitionVenue> SelectedVenues { get; set; } = new();

        /// <summary>Total number of selected tables across all venues for this round.</summary>
        public int TotalTables => SelectedVenues.Sum(v => v.TableCount);

        /// <summary>
        /// Per-round override for the "Best of" frame count.
        /// null = inherit the competition-level <see cref="Competition.BestOf"/> value.
        /// 0 = unlimited (explicit).
        /// </summary>
        public int? BestOf { get; set; }

        /// <summary>Effective Best Of for this round, falling back to the competition default when not set.</summary>
        public int GetEffectiveBestOf(Competition? competition) =>
            BestOf ?? competition?.BestOf ?? 0;

        /// <summary>Frames needed to win a match in this round, using the effective Best Of.</summary>
        public int GetFramesToWin(Competition? competition)
        {
            var bo = GetEffectiveBestOf(competition);
            return bo > 0 ? (bo + 1) / 2 : 0;
        }

        public override string ToString() => Name;
    }
}
