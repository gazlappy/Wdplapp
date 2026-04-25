using System.Text.Json.Serialization;

namespace Wdpl2.Models
{
    /// <summary>
    /// Represents a doubles pair's rating entry for a season/division.
    /// Imported from HTML doubles ratings tables or calculated from doubles frames.
    /// </summary>
    public sealed class DoublesPairing
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Season this pairing belongs to.</summary>
        public Guid? SeasonId { get; set; }

        /// <summary>Division this pairing plays in.</summary>
        public Guid? DivisionId { get; set; }

        /// <summary>Team this pair plays for.</summary>
        public Guid? TeamId { get; set; }

        /// <summary>First player in the pair.</summary>
        public Guid? Player1Id { get; set; }

        /// <summary>Second player in the pair.</summary>
        public Guid? Player2Id { get; set; }

        /// <summary>First player name (for display when player record not linked).</summary>
        public string Player1Name { get; set; } = "";

        /// <summary>Second player name (for display when player record not linked).</summary>
        public string Player2Name { get; set; } = "";

        /// <summary>Team name (for display when team record not linked).</summary>
        public string TeamName { get; set; } = "";

        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }

        /// <summary>Best rating achieved during the season.</summary>
        public int BestRating { get; set; }

        /// <summary>Date the best rating was achieved.</summary>
        public DateTime? BestRatingDate { get; set; }

        /// <summary>Current (or final) rating.</summary>
        public int CurrentRating { get; set; }

        [JsonIgnore]
        public string PairDisplayName => $"{Player1Name} & {Player2Name}";

        [JsonIgnore]
        public double WinPercentage => Played > 0 ? (Won * 100.0 / Played) : 0;
    }
}
