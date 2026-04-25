using System.ComponentModel.DataAnnotations;

namespace Wdpl2.Models
{
    public sealed class Division
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Link to the season this division belongs to.</summary>
        public Guid? SeasonId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Free-text notes used by DivisionsPage.</summary>
        public string? Notes { get; set; }

        /// <summary>When this record was created.</summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>When this record was last modified.</summary>
        public DateTime? ModifiedDate { get; set; }

        public override string ToString() => Name;
    }
}
