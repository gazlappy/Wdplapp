namespace Wdpl2.Models
{
    /// <summary>
    /// Records a player transfer from one team to another within a season.
    /// This preserves the player's historical performance at each team.
    /// </summary>
    public sealed class PlayerTransfer
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>The team the player transferred FROM.</summary>
        public Guid FromTeamId { get; set; }

        /// <summary>Name of the team at time of transfer (for historical reference).</summary>
        public string FromTeamName { get; set; } = "";

        /// <summary>The team the player transferred TO.</summary>
        public Guid ToTeamId { get; set; }

        /// <summary>Name of the team at time of transfer (for historical reference).</summary>
        public string ToTeamName { get; set; } = "";

        /// <summary>Date of the transfer.</summary>
        public DateTime TransferDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Player's rating at the time of transfer.
        /// This allows carrying over their rating to the new team.
        /// </summary>
        public int RatingAtTransfer { get; set; }

        /// <summary>
        /// Frames played at the time of transfer.
        /// </summary>
        public int FramesPlayedAtTransfer { get; set; }

        /// <summary>
        /// Wins at the time of transfer.
        /// </summary>
        public int WinsAtTransfer { get; set; }

        /// <summary>
        /// Losses at the time of transfer.
        /// </summary>
        public int LossesAtTransfer { get; set; }

        /// <summary>Optional notes about the transfer.</summary>
        public string? Notes { get; set; }
    }
}
