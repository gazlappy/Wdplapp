using System.ComponentModel.DataAnnotations;

namespace Wdpl2.Models
{
    public sealed class Team
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Link to the season this team belongs to.</summary>
        public Guid? SeasonId { get; set; }

        /// <summary>Global team identity - links same team across multiple seasons for career tracking.</summary>
        public Guid? GlobalTeamId { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        /// <summary>Division this team plays in.</summary>
        public Guid? DivisionId { get; set; }

        /// <summary>Home venue + (optional) preferred table.</summary>
        public Guid? VenueId { get; set; }
        public Guid? TableId { get; set; }

        /// <summary>TeamsPage uses this toggle.</summary>
        public bool ProvidesFood { get; set; }

        /// <summary>TeamsPage uses this to pick the captain (player).</summary>
        public Guid? CaptainPlayerId { get; set; }

        /// <summary>Legacy/simple captain name (keep for compatibility if any old UI binds to it).</summary>
        public string? Captain { get; set; }

        /// <summary>Some UIs track if the captain played.</summary>
        public bool CaptainPlayed { get; set; }

        public string? Notes { get; set; }

        /// <summary>When this record was created.</summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>When this record was last modified.</summary>
        public DateTime? ModifiedDate { get; set; }

        public override string ToString() => Name ?? "";
    }
}
