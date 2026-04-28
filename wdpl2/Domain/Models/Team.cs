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

        /// <summary>Captain contact email (used for the website Captains Area contact list).</summary>
        [MaxLength(120)]
        public string? CaptainEmail { get; set; }

        /// <summary>Captain contact phone (used for the website Captains Area contact list).</summary>
        [MaxLength(40)]
        public string? CaptainPhone { get; set; }

        /// <summary>
        /// Captain login PIN (4-12 chars) for the generated website's Captains Area.
        /// Stored in plain text inside the app database; only a SHA-256 hash is published
        /// to the static site. Empty/null disables login for this team.
        /// </summary>
        [MaxLength(32)]
        public string? CaptainPin { get; set; }

        public string? Notes { get; set; }

        /// <summary>
        /// Optional reference to a logo in <see cref="WebsiteSettings.LogoCatalog"/>.
        /// Used for team crests on the generated website and printable sheets.
        /// </summary>
        [MaxLength(64)]
        public string? LogoCatalogId { get; set; }

        /// <summary>When this record was created.</summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>When this record was last modified.</summary>
        public DateTime? ModifiedDate { get; set; }

        public override string ToString() => Name ?? "";
    }
}
