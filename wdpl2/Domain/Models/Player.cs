using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wdpl2.Models
{
    public sealed class Player
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Link to the season this player belongs to.</summary>
        public Guid? SeasonId { get; set; }

        /// <summary>Global player identity - links same person across multiple seasons for career tracking.</summary>
        public Guid? GlobalPlayerId { get; set; }

        // Maintain both single Name and split First/Last for compatibility
        private string _name = string.Empty;
        public string Name
        {
            get => string.IsNullOrWhiteSpace(_name) ? FullName : _name;
            set => _name = value ?? "";
        }

        [Required, MaxLength(50)]
        public string FirstName { get; set; } = "";
        [Required, MaxLength(50)]
        public string LastName { get; set; } = "";

        [JsonIgnore]
        public string FullName => string.Join(" ", new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

        /// <summary>Team this player belongs to.</summary>
        public Guid? TeamId { get; set; }

        /// <summary>
        /// Whether the player is active and can be selected for matches.
        /// Inactive players keep their historical results but cannot play new frames.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Date the player was deactivated (if applicable).
        /// </summary>
        public DateTime? DeactivatedDate { get; set; }

        /// <summary>
        /// Reason for deactivation (optional).
        /// </summary>
        public string? DeactivationReason { get; set; }

        /// <summary>
        /// Transfer history for this player within the season.
        /// </summary>
        public List<PlayerTransfer> TransferHistory { get; set; } = new();

        /// <summary>
        /// Per-date availability records for this player.
        /// </summary>
        public List<PlayerAvailability> Availability { get; set; } = new();

        public string? Notes { get; set; }

        /// <summary>When this record was created.</summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>When this record was last modified.</summary>
        public DateTime? ModifiedDate { get; set; }

        public override string ToString() => FullName;
    }
}
