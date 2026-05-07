using System;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// Interface for managing the currently selected season across the app.
    /// </summary>
    public interface ISeasonService
    {
        event EventHandler<SeasonChangedEventArgs>? SeasonChanged;
        Guid? CurrentSeasonId { get; set; }
        void ForceRefresh();
        void Initialize();
        Season? GetCurrentSeason();
    }

    /// <summary>
    /// Shared service for managing the currently selected season across all pages.
    /// Registered as a singleton in DI. Use <see cref="Current"/> for non-DI contexts (Views).
    /// </summary>
    public class SeasonService : ISeasonService
    {
        /// <summary>
        /// Static accessor for Views and other non-DI contexts.
        /// Set automatically when the singleton is created via DI.
        /// </summary>
        public static SeasonService Current { get; private set; } = null!;

        private readonly IDataStore _dataStore;
        private Guid? _currentSeasonId;

        public SeasonService(IDataStore dataStore)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            Current = this;
        }

        /// <summary>
        /// Event fired when the selected season changes.
        /// </summary>
        public event EventHandler<SeasonChangedEventArgs>? SeasonChanged;

        /// <summary>
        /// Gets or sets the currently selected season ID.
        /// Setting this value triggers the SeasonChanged event.
        /// </summary>
        public Guid? CurrentSeasonId
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
                        season = _dataStore.GetData().Seasons.FirstOrDefault(s => s.Id == _currentSeasonId.Value);
                    }

                    SeasonChanged?.Invoke(this, new SeasonChangedEventArgs(oldSeasonId, _currentSeasonId, season));
                }
            }
        }

        /// <summary>
        /// Force trigger the SeasonChanged event even if the ID hasn't changed.
        /// Use this when you need to force all pages to refresh their data.
        /// </summary>
        public void ForceRefresh()
        {
            Season? season = null;
            if (_currentSeasonId.HasValue)
            {
                season = _dataStore.GetData().Seasons.FirstOrDefault(s => s.Id == _currentSeasonId.Value);
            }

            SeasonChanged?.Invoke(this, new SeasonChangedEventArgs(_currentSeasonId, _currentSeasonId, season));
        }

        /// <summary>
        /// Initialize with the active season from DataStore.
        /// </summary>
        public void Initialize()
        {
            var data = _dataStore.GetData();
            if (data.ActiveSeasonId.HasValue)
            {
                _currentSeasonId = data.ActiveSeasonId;
            }
            else
            {
                // Select the first active season if available
                var activeSeason = data.Seasons.FirstOrDefault(s => s.IsActive);
                if (activeSeason != null)
                {
                    _currentSeasonId = activeSeason.Id;
                }
                else
                {
                    // Select the first season by start date if available
                    var firstSeason = data.Seasons.OrderByDescending(s => s.StartDate).FirstOrDefault();
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
        public Season? GetCurrentSeason()
        {
            if (!_currentSeasonId.HasValue) return null;
            return _dataStore.GetData().Seasons.FirstOrDefault(s => s.Id == _currentSeasonId.Value);
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