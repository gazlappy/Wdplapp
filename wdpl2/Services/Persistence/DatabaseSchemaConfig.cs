using System.Collections.Generic;

namespace Wdpl2.Services
{
    /// <summary>
    /// Configuration for mapping Access database schema to MAUI app models.
    /// Supports multiple schema versions.
    /// </summary>
    public class DatabaseSchemaConfig
    {
        // Division mapping
        public string DivisionTable { get; set; } = "tblDivisions";
        public string DivisionIdColumn { get; set; } = "ID";
        public string DivisionNameColumn { get; set; } = "DivisionName";
        public string DivisionNotesColumn { get; set; } = "BandColour"; // Using BandColour as notes

        // Venue mapping
        public string VenueTable { get; set; } = "tblTeams"; // Venues are embedded in teams table!
        public string VenueIdColumn { get; set; } = "VenueID";
        public string VenueNameColumn { get; set; } = "VenueName";
        public string VenueAddressColumn { get; set; } = "VenueNo"; // Using VenueNo as address
        public string VenueNotesColumn { get; set; } = "ContactNo";

        // Team mapping
        public string TeamTable { get; set; } = "tblTeams";
        public string TeamIdColumn { get; set; } = "TeamID";
        public string TeamNameColumn { get; set; } = "TeamName";
        public string TeamDivisionIdColumn { get; set; } = "Division";
        public string TeamVenueIdColumn { get; set; } = "VenueID";
        public string TeamCaptainColumn { get; set; } = "CaptainName";
        public string TeamNotesColumn { get; set; } = "ContactNo";
        public string TeamProvidesFoodColumn { get; set; } = "TeamID"; // Doesn't exist, will return ID

        // Player mapping
        public string PlayerTable { get; set; } = "tblPlayers";
        public string PlayerIdColumn { get; set; } = "PlayerID";
        public string PlayerFirstNameColumn { get; set; } = "PlayerName"; // Full name in one column!
        public string PlayerLastNameColumn { get; set; } = "PlayerName"; // Same column
        public string PlayerTeamIdColumn { get; set; } = "Team";
        public string PlayerNotesColumn { get; set; } = "Active";

        // Season mapping - from tblLeague table
        public string SeasonTable { get; set; } = "tblLeague";
        public string SeasonIdColumn { get; set; } = "ID";
        public string SeasonNameColumn { get; set; } = "SeasonName";
        public string SeasonStartDateColumn { get; set; } = "FirstMatchDate";
        public string SeasonEndDateColumn { get; set; } = "FirstMatchDate"; // Only one date available
        public string SeasonMatchDayColumn { get; set; } = "ID"; // Doesn't exist
        public string SeasonMatchTimeColumn { get; set; } = "ID"; // Doesn't exist
        public string SeasonFramesPerMatchColumn { get; set; } = "Frames";
        public string SeasonIsActiveColumn { get; set; } = "ID"; // Doesn't exist

        // Match/Fixture header mapping
        public string MatchHeaderTable { get; set; } = "tblMatchHeader";
        public string MatchNumberColumn { get; set; } = "MatchNo";
        public string MatchSeasonIdColumn { get; set; } = "MatchNo"; // Will need to derive from League
        public string MatchDivisionIdColumn { get; set; } = "Division";
        public string MatchHomeTeamIdColumn { get; set; } = "TeamHome";
        public string MatchAwayTeamIdColumn { get; set; } = "TeamAway";
        public string MatchDateColumn { get; set; } = "MatchNo"; // Need to join with tblFixtures
        public string MatchVenueIdColumn { get; set; } = "TeamHome"; // Use home team's venue

        // Match detail/frame mapping
        public string MatchDetailTable { get; set; } = "tblMatchDetail";
        public string FrameMatchNumberColumn { get; set; } = "MatchNo";
        public string FrameNumberColumn { get; set; } = "FrameNo";
        public string FramePlayer1Column { get; set; } = "Player1";
        public string FramePlayer2Column { get; set; } = "Player2";
        public string FrameHomeScoreColumn { get; set; } = "HomeScore";
        public string FrameAwayScoreColumn { get; set; } = "AwayScore";
        public string FrameEightBallColumn { get; set; } = "Achived8Ball";

        /// <summary>
        /// Predefined schema for your actual database.
        /// </summary>
        public static Dictionary<string, DatabaseSchemaConfig> KnownSchemas = new()
        {
            ["YourActualDatabase"] = new DatabaseSchemaConfig
            {
                // All the settings above are already correct
            },
            
            ["Current"] = new DatabaseSchemaConfig
            {
                // This was the wrong schema - keeping for reference
                DivisionTable = "tblDivisions",
                DivisionIdColumn = "DivisionID",
                DivisionNameColumn = "DivisionName",
                DivisionNotesColumn = "Notes",
                
                TeamTable = "tblTeams",
                TeamIdColumn = "TeamID",
                TeamNameColumn = "TeamName",
                
                PlayerTable = "tblPlayers",
                PlayerIdColumn = "PlayerID"
            }
        };
    }
}