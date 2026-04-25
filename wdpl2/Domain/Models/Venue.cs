using System.ComponentModel.DataAnnotations;

namespace Wdpl2.Models
{
    public sealed class Venue
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Link to the season this venue belongs to.</summary>
        public Guid? SeasonId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(250)]
        public string? Address { get; set; }
        public string? Notes { get; set; }

        /// <summary>When this record was created.</summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>When this record was last modified.</summary>
        public DateTime? ModifiedDate { get; set; }

        public List<VenueTable> Tables { get; set; } = new();

        public override string ToString() => Name;
    }

    public sealed class VenueTable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required, MaxLength(50)]
        public string Label { get; set; } = "";
        public int MaxTeams { get; set; } = 2;
        public override string ToString() => Label;
    }
}
