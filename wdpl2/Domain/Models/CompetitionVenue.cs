namespace Wdpl2.Models
{
    /// <summary>
    /// A venue selected for a group stage competition, with the specific tables in use.
    /// </summary>
    public sealed class CompetitionVenue
    {
        public Guid VenueId { get; set; }
        public string VenueName { get; set; } = "";

        /// <summary>The specific tables selected at this venue.</summary>
        public List<SelectedTable> SelectedTables { get; set; } = new();

        /// <summary>Number of selected tables (replaces the old TableCount property).</summary>
        public int TableCount => SelectedTables.Count;
    }

    /// <summary>
    /// A specific table selected from a venue.
    /// </summary>
    public sealed class SelectedTable
    {
        public Guid TableId { get; set; }
        public string Label { get; set; } = "";
    }
}
