namespace Wdpl2.Models
{
    /// <summary>
    /// Settings for group stage competitions
    /// </summary>
    public sealed class GroupStageSettings
    {
        /// <summary>Number of groups to create (e.g., 8 groups of 16 = 128 players)</summary>
        public int NumberOfGroups { get; set; } = 4;

        /// <summary>Number of top players from each group advancing to knockout</summary>
        public int TopPlayersAdvance { get; set; } = 2;

        /// <summary>Number of lower players from each group going to plate competition (ignored when AllLosersToPlate is true)</summary>
        public int LowerPlayersToPlate { get; set; } = 2;

        /// <summary>When true, all non-winners go into the plate instead of a fixed count per group.</summary>
        public bool AllLosersToPlate { get; set; } = true;

        /// <summary>Whether to create a plate competition automatically</summary>
        public bool CreatePlateCompetition { get; set; } = true;

        /// <summary>Name suffix for plate competition (e.g., "Plate")</summary>
        public string PlateNameSuffix { get; set; } = "Plate";

        /// <summary>Selected venues with specific tables for this competition night.</summary>
        public List<CompetitionVenue> SelectedVenues { get; set; } = new();

        /// <summary>Scheduled date for the current group round.</summary>
        public DateTime? GroupDate { get; set; }
    }
}
