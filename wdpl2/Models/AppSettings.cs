using System;
using System.Collections.Generic;
using System.Linq;

namespace Wdpl2.Models
{
    /// <summary>
    /// Application-wide settings for configuring league behavior.
    /// </summary>
    /// <remarks>
    /// ?? RATING CALCULATION COMPATIBILITY NOTE:
    /// The player rating calculation in LeagueTablesPage.CalculateVBAStyleRating() currently
    /// replicates a bug from the original VBA Access database to maintain historical compatibility.
    /// See detailed documentation in LeagueTablesPage.xaml.cs above the RenderPlayerRatingsHeader() method.
    /// </remarks>
    public sealed class AppSettings
    {
        // ========== Player Rating System ==========

        /// <summary>Starting rating for all new players (default 1000).</summary>
        public int RatingStartValue { get; set; } = 1000;  // VBA uses 1000

        /// <summary>Base weighting constant from VBA (Rating = 240).</summary>
        /// <remarks>VBA calculates initial BiasX as: 240 + (RatingsBias × TotalFrames)</remarks>
        public int RatingWeighting { get; set; } = 240;  // VBA constant: Rating = 240

      /// <summary>Amount to reduce weighting for each subsequent frame (default 4).</summary>
 public int RatingsBias { get; set; } = 4;  // VBA constant "RatingBias = 4"

        /// <summary>Win factor - multiplier applied to opponent's rating on win (default 1.0 = 100%).</summary>
        public double WinFactor { get; set; } = 1.25;  // VBA: RATINGWIN = 1.25

        /// <summary>Loss factor - multiplier applied to opponent's rating on loss (default 1.0 = 100%).</summary>
        public double LossFactor { get; set; } = 0.75;  // VBA: RATINGLOSE = 0.75

        /// <summary>8-Ball factor - multiplier applied to opponent's rating on 8-ball win (default 1.5 = 150%).</summary>
        public double EightBallFactor { get; set; } = 1.35;  // VBA: RATING8BALL = 1.35

        /// <summary>Enable/disable 8-ball factor (default true).</summary>
        public bool UseEightBallFactor { get; set; } = true;

        /// <summary>Minimum percentage of available frames to appear in player ratings table (default 60%).</summary>
        /// <remarks>
        /// Players must play at least this percentage of the maximum frames available in the season.
        /// For example, if max frames is 30 and this is 60, players need 18 frames to appear.
        /// All players still have ratings calculated regardless of this threshold.
        /// </remarks>
        public int MinFramesPercentage { get; set; } = 60;

        // ========== Match Scoring ==========

        /// <summary>Bonus points awarded for winning a match (default 2).</summary>
        /// <remarks>Total points = Frames Won + Win Bonus (if won)</remarks>
        public int MatchWinBonus { get; set; } = 2;

        /// <summary>Bonus points awarded for drawing a match (default 1).</summary>
        /// <remarks>Total points = Frames Won + Draw Bonus (if drawn)</remarks>
        public int MatchDrawBonus { get; set; } = 1;

        /// <summary>DEPRECATED: Use MatchWinBonus instead. Kept for backward compatibility.</summary>
        [Obsolete("Use MatchWinBonus - points now calculated as Frames Won + Match Win Bonus")]
        public int PointsForWin
        {
            get => MatchWinBonus;
            set => MatchWinBonus = value;
        }

        /// <summary>DEPRECATED: Use MatchDrawBonus instead. Kept for backward compatibility.</summary>
        [Obsolete("Use MatchDrawBonus - points now calculated as Frames Won + Match Draw Bonus")]
        public int PointsForDraw
        {
            get => MatchDrawBonus;
            set => MatchDrawBonus = value;
        }

        // ========== Fixture Defaults ==========

        /// <summary>Default number of frames per match (default 10).</summary>
        public int DefaultFramesPerMatch { get; set; } = 10;

        /// <summary>Default match day of week (default Tuesday).</summary>
        public DayOfWeek DefaultMatchDay { get; set; } = DayOfWeek.Tuesday;

        /// <summary>Default match start time (default 19:30).</summary>
        public TimeSpan DefaultMatchTime { get; set; } = new TimeSpan(19, 30, 0);

        /// <summary>Default number of times teams play each other (default 2).</summary>
        public int DefaultRoundsPerOpponent { get; set; } = 2;

        // ========== Notification Settings (Phase 3) ==========

        /// <summary>Enable/disable automatic match reminder notifications (default true).</summary>
        public bool MatchRemindersEnabled { get; set; } = true;

        /// <summary>Hours before match to send reminder notification (default 2).</summary>
        /// <remarks>Valid values: 1, 2, 4, 6, 12, or 24 hours</remarks>
        public int ReminderHoursBefore { get; set; } = 2;

        /// <summary>Enable/disable result notifications (default false).</summary>
        /// <remarks>Send notification when match results are posted</remarks>
        public bool ResultNotificationsEnabled { get; set; } = false;

        /// <summary>Enable/disable weekly fixture list notifications (default false).</summary>
        /// <remarks>Send weekly summary of upcoming fixtures every Monday morning</remarks>
        public bool WeeklyFixtureListEnabled { get; set; } = false;

        /// <summary>Day of week for weekly fixture list (default Monday).</summary>
        public DayOfWeek WeeklyFixtureDay { get; set; } = DayOfWeek.Monday;

        /// <summary>Time of day for weekly fixture list (default 09:00).</summary>
        public TimeSpan WeeklyFixtureTime { get; set; } = new TimeSpan(9, 0, 0);

        /// <summary>Reset all settings to defaults.</summary>
        public void ResetToDefaults()
        {
            // Player Ratings (VBA-compatible values)
            RatingStartValue = 1000;  // VBA: RATINGSTART = 1000
            RatingWeighting = 240;     // VBA constant: Rating = 240
            RatingsBias = 4;           // VBA constant: RatingBias = 4
            WinFactor = 1.25;          // VBA: RATINGWIN = 1.25
            LossFactor = 0.75;         // VBA: RATINGLOSE = 0.75
            EightBallFactor = 1.35;    // VBA: RATING8BALL = 1.35
            UseEightBallFactor = true;
            MinFramesPercentage = 60;

            // Match Scoring
            MatchWinBonus = 2;
            MatchDrawBonus = 1;

            // Fixture Defaults
            DefaultFramesPerMatch = 10;
            DefaultMatchDay = DayOfWeek.Tuesday;
            DefaultMatchTime = new TimeSpan(19, 30, 0);
            DefaultRoundsPerOpponent = 2;

            // Notification Settings (Phase 3)
            MatchRemindersEnabled = true;
            ReminderHoursBefore = 2;
            ResultNotificationsEnabled = false;
            WeeklyFixtureListEnabled = false;
            WeeklyFixtureDay = DayOfWeek.Monday;
            WeeklyFixtureTime = new TimeSpan(9, 0, 0);
        }

        /// <summary>
        /// Calculate the minimum number of frames required to appear in ratings table.
        /// </summary>
        /// <param name="maxFramesInSeason">Maximum frames any player could play in the season</param>
        /// <returns>Minimum frames required (rounded up)</returns>
        public int CalculateMinimumFrames(int maxFramesInSeason)
        {
            if (maxFramesInSeason <= 0) return 0;
            return (int)Math.Ceiling(maxFramesInSeason * (MinFramesPercentage / 100.0));
        }
    }

    /// <summary>
    /// Helper class to track a single frame result from a player's perspective.
    /// Used for VBA-style cumulative weighted rating calculation.
    /// </summary>
    public class PlayerFrameHistory
    {
        public Guid PlayerId { get; set; }
        public Guid OpponentId { get; set; }
        public int OpponentRating { get; set; }
        public bool Won { get; set; }
        public bool EightBall { get; set; }
        public int FrameNumber { get; set; }
        public DateTime MatchDate { get; set; }
    }
}