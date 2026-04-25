using Wdpl2.Services;

namespace Wdpl2.Models
{
    // ---------- Root container ----------
    public sealed class LeagueData
    {
        public List<Division> Divisions { get; set; } = new();
        public List<Team> Teams { get; set; } = new();
        public List<Player> Players { get; set; } = new();
        public List<Venue> Venues { get; set; } = new();

        // Fixtures + Seasons are in their own files (Models/Fixture.cs, Models/Season.cs)
        public List<Fixture> Fixtures { get; set; } = new();
        public List<Season> Seasons { get; set; } = new();
        public Guid? ActiveSeasonId { get; set; }

        /// <summary>Competitions/tournaments.</summary>
        public List<Competition> Competitions { get; set; } = new();

        /// <summary>Doubles pair ratings (imported from HTML or calculated from doubles frames).</summary>
        public List<DoublesPairing> DoublesPairings { get; set; } = new();

        /// <summary>User-created calendar events (meetings, socials, deadlines, etc.).</summary>
        public List<CalendarEvent> CalendarEvents { get; set; } = new();

        /// <summary>Calendar page display and behaviour settings.</summary>
        public CalendarSettings CalendarSettings { get; set; } = new();

        /// <summary>Application settings for league behavior.</summary>
        public AppSettings Settings { get; set; } = new();

        /// <summary>
        /// Resolve the effective <see cref="AppSettings"/> for a season.
        /// Returns the season's own settings if customised, otherwise the global <see cref="Settings"/>.
        /// </summary>
        public AppSettings GetSettingsForSeason(Guid? seasonId)
        {
            if (seasonId.HasValue)
            {
                var season = Seasons.FirstOrDefault(s => s.Id == seasonId.Value);
                if (season?.Settings != null)
                    return season.Settings;
            }
            return Settings;
        }

        /// <summary>
        /// Returns true if the season with the given ID is locked (read-only).
        /// Returns false if <paramref name="seasonId"/> is null or no matching season is found.
        /// </summary>
        public bool IsSeasonLocked(Guid? seasonId)
        {
            if (!seasonId.HasValue) return false;
            var season = Seasons.FirstOrDefault(s => s.Id == seasonId.Value);
            return season?.IsLocked == true;
        }

        /// <summary>Website settings for HTML generation and FTP upload.</summary>
        public WebsiteSettings WebsiteSettings { get; set; } = new();

        /// <summary>Fixtures sheet settings for printable fixture sheet generation.</summary>
        public FixturesSheetSettings FixturesSheetSettings { get; set; } = new();

        /// <summary>
        /// Get all entities for a specific season (divisions, venues, teams, players, fixtures).
        /// </summary>
        public (List<Division> divisions, List<Venue> venues, List<Team> teams, List<Player> players, List<Fixture> fixtures)
            GetSeasonData(Guid seasonId)
        {
            return (
                Divisions.Where(d => d.SeasonId == seasonId).ToList(),
                Venues.Where(v => v.SeasonId == seasonId).ToList(),
                Teams.Where(t => t.SeasonId == seasonId).ToList(),
                Players.Where(p => p.SeasonId == seasonId).ToList(),
                Fixtures.Where(f => f.SeasonId == seasonId).ToList()
            );
        }

        /// <summary>
        /// Delete a season and ALL associated data (cascading delete).
        /// </summary>
        public void DeleteSeasonCascade(Guid seasonId)
        {
            // Remove all fixtures for this season
            Fixtures.RemoveAll(f => f.SeasonId == seasonId);

            // Remove all players for this season
            Players.RemoveAll(p => p.SeasonId == seasonId);

            // Remove all teams for this season
            Teams.RemoveAll(t => t.SeasonId == seasonId);

            // Remove all venues for this season
            Venues.RemoveAll(v => v.SeasonId == seasonId);

            // Remove all divisions for this season
            Divisions.RemoveAll(d => d.SeasonId == seasonId);

            // Remove all competitions for this season
            Competitions.RemoveAll(c => c.SeasonId == seasonId);

            // Remove all doubles pairings for this season
            DoublesPairings.RemoveAll(dp => dp.SeasonId == seasonId);

            // Finally remove the season itself
            Seasons.RemoveAll(s => s.Id == seasonId);

            // If this was the active season, clear it
            if (ActiveSeasonId == seasonId)
                ActiveSeasonId = null;
        }

        /// <summary>
        /// Remove any orphaned entities that have no valid season reference.
        /// Call after deleting seasons to clean up stale data.
        /// </summary>
        public void CleanupOrphans()
        {
            var validSeasonIds = new HashSet<Guid>(Seasons.Select(s => s.Id));

            Fixtures.RemoveAll(f => f.SeasonId == null || !validSeasonIds.Contains(f.SeasonId.Value));
            Players.RemoveAll(p => p.SeasonId == null || !validSeasonIds.Contains(p.SeasonId.Value));
            Teams.RemoveAll(t => t.SeasonId == null || !validSeasonIds.Contains(t.SeasonId.Value));
            Venues.RemoveAll(v => v.SeasonId == null || !validSeasonIds.Contains(v.SeasonId.Value));
            Divisions.RemoveAll(d => d.SeasonId == null || !validSeasonIds.Contains(d.SeasonId.Value));
            Competitions.RemoveAll(c => c.SeasonId == null || !validSeasonIds.Contains(c.SeasonId.Value));
            DoublesPairings.RemoveAll(dp => dp.SeasonId == null || !validSeasonIds.Contains(dp.SeasonId.Value));
        }
    }
}
