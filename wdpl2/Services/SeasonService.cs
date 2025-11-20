using System;
using System.Linq; // ADD THIS
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// Shared service for managing the currently selected season across all pages.
    /// </summary>
    public static class SeasonService
    {
        private static Guid? _currentSeasonId;

        /// <summary>
        /// Event fired when the selected season changes.
        /// </summary>
        public static event EventHandler<SeasonChangedEventArgs>? SeasonChanged;

        /// <summary>
        /// Gets or sets the currently selected season ID.
        /// Setting this value triggers the SeasonChanged event.
        /// </summary>
        public static Guid? CurrentSeasonId
        {
            get => _currentSeasonId;
            set
            {
                if (_currentSeasonId != value)
                {
                    var oldSeasonId = _currentSeasonId;
                    _currentSeasonId = value;

                    // Find the season object
                    Season? season = null;
                    if (_currentSeasonId.HasValue)
                    {
                        season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId.Value);
                    }

                    SeasonChanged?.Invoke(null, new SeasonChangedEventArgs(oldSeasonId, _currentSeasonId, season));
                }
            }
        }

        /// <summary>
        /// Initialize with the active season from DataStore.
        /// </summary>
        public static void Initialize()
        {
            if (DataStore.Data.ActiveSeasonId.HasValue)
            {
                _currentSeasonId = DataStore.Data.ActiveSeasonId;
            }
            else
            {
                // Select the first season if available
                var firstSeason = DataStore.Data.Seasons.OrderByDescending(s => s.StartDate).FirstOrDefault();
                if (firstSeason != null)
                {
                    _currentSeasonId = firstSeason.Id;
                }
            }
        }

        /// <summary>
        /// Get the current season object.
        /// </summary>
        public static Season? GetCurrentSeason()
        {
            if (!_currentSeasonId.HasValue) return null;
            return DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId.Value);
        }
    }

    /// <summary>
    /// Event args for season change notifications.
    /// </summary>
    public class SeasonChangedEventArgs : EventArgs
    {
        public Guid? OldSeasonId { get; }
        public Guid? NewSeasonId { get; }
        public Season? NewSeason { get; }

        public SeasonChangedEventArgs(Guid? oldSeasonId, Guid? newSeasonId, Season? newSeason)
        {
            OldSeasonId = oldSeasonId;
            NewSeasonId = newSeasonId;
            NewSeason = newSeason;
        }
    }
}