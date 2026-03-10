using System;
using System.Linq;
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
        /// Force trigger the SeasonChanged event even if the ID hasn't changed.
        /// Use this when you need to force all pages to refresh their data.
        /// </summary>
        public static void ForceRefresh()
        {
            Season? season = null;
            if (_currentSeasonId.HasValue)
            {
                season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId.Value);
            }

            SeasonChanged?.Invoke(null, new SeasonChangedEventArgs(_currentSeasonId, _currentSeasonId, season));
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
                // Select the first active season if available
                var activeSeason = DataStore.Data.Seasons.FirstOrDefault(s => s.IsActive);
                if (activeSeason != null)
                {
                    _currentSeasonId = activeSeason.Id;
                }
                else
                {
                    // Select the first season by start date if available
                    var firstSeason = DataStore.Data.Seasons.OrderByDescending(s => s.StartDate).FirstOrDefault();
                    if (firstSeason != null)
                    {
                        _currentSeasonId = firstSeason.Id;
                    }
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