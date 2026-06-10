using System.Collections.Generic;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Interface for data persistence operations
/// </summary>
public interface IDataStore
{
    /// <summary>
    /// Get all competitions for a season
    /// </summary>
    Task<List<Competition>> GetCompetitionsAsync(Guid? seasonId, CancellationToken ct = default);
    
    /// <summary>
    /// Add a new competition
    /// </summary>
    Task AddCompetitionAsync(Competition competition, CancellationToken ct = default);

    /// <summary>
    /// Update an existing competition
    /// </summary>
    Task UpdateCompetitionAsync(Competition competition, CancellationToken ct = default);

    /// <summary>
    /// Delete a competition
    /// </summary>
    Task DeleteCompetitionAsync(Competition competition, CancellationToken ct = default);
    
    /// <summary>
    /// Get all players for a season
    /// </summary>
    Task<List<Player>> GetPlayersAsync(Guid? seasonId, CancellationToken ct = default);
    
    /// <summary>
    /// Add a new player
    /// </summary>
    Task AddPlayerAsync(Player player, CancellationToken ct = default);

    /// <summary>
    /// Update an existing player
    /// </summary>
    Task UpdatePlayerAsync(Player player, CancellationToken ct = default);

    /// <summary>
    /// Delete a player
    /// </summary>
    Task DeletePlayerAsync(Player player, CancellationToken ct = default);
    
    /// <summary>
    /// Get all teams for a season
    /// </summary>
    Task<List<Team>> GetTeamsAsync(Guid? seasonId, CancellationToken ct = default);
    
    /// <summary>
    /// Add a new team
    /// </summary>
    Task AddTeamAsync(Team team, CancellationToken ct = default);

    /// <summary>
    /// Update an existing team
    /// </summary>
    Task UpdateTeamAsync(Team team, CancellationToken ct = default);

    /// <summary>
    /// Delete a team
    /// </summary>
    Task DeleteTeamAsync(Team team, CancellationToken ct = default);
    
    /// <summary>
    /// Get all venues for a season
    /// </summary>
    Task<List<Venue>> GetVenuesAsync(Guid? seasonId, CancellationToken ct = default);
    
    /// <summary>
    /// Add a new venue
    /// </summary>
    Task AddVenueAsync(Venue venue, CancellationToken ct = default);

    /// <summary>
    /// Update an existing venue
    /// </summary>
    Task UpdateVenueAsync(Venue venue, CancellationToken ct = default);

    /// <summary>
    /// Delete a venue
    /// </summary>
    Task DeleteVenueAsync(Venue venue, CancellationToken ct = default);
    
    /// <summary>
    /// Get all divisions for a season
    /// </summary>
    Task<List<Division>> GetDivisionsAsync(Guid? seasonId, CancellationToken ct = default);
    
    /// <summary>
    /// Add a new division
    /// </summary>
    Task AddDivisionAsync(Division division, CancellationToken ct = default);

    /// <summary>
    /// Update an existing division
    /// </summary>
    Task UpdateDivisionAsync(Division division, CancellationToken ct = default);

    /// <summary>
    /// Delete a division
    /// </summary>
    Task DeleteDivisionAsync(Division division, CancellationToken ct = default);
    
    /// <summary>
    /// Get all fixtures for a season
    /// </summary>
    Task<List<Fixture>> GetFixturesAsync(Guid? seasonId, CancellationToken ct = default);
    
    /// <summary>
    /// Add a new fixture
    /// </summary>
    Task AddFixtureAsync(Fixture fixture, CancellationToken ct = default);

    /// <summary>
    /// Update an existing fixture
    /// </summary>
    Task UpdateFixtureAsync(Fixture fixture, CancellationToken ct = default);

    /// <summary>
    /// Delete a fixture
    /// </summary>
    Task DeleteFixtureAsync(Fixture fixture, CancellationToken ct = default);

    /// <summary>
    /// Atomically replace all fixtures for a season with the supplied set.
    /// Used by fixture generation so the whole batch persists in one transaction.
    /// </summary>
    Task ReplaceFixturesForSeasonAsync(Guid seasonId, IReadOnlyList<Fixture> fixtures, CancellationToken ct = default);

    /// <summary>
    /// Bulk-insert a season's entities (divisions, venues, teams, players) in a single
    /// transaction. Used by the season setup wizard when copying a previous season.
    /// Any null collection is skipped.
    /// </summary>
    Task AddSeasonEntitiesAsync(
        IReadOnlyList<Division>? divisions = null,
        IReadOnlyList<Venue>? venues = null,
        IReadOnlyList<Team>? teams = null,
        IReadOnlyList<Player>? players = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get all seasons
    /// </summary>
    Task<List<Season>> GetSeasonsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Add a new season
    /// </summary>
    Task AddSeasonAsync(Season season, CancellationToken ct = default);

    /// <summary>
    /// Update an existing season
    /// </summary>
    Task UpdateSeasonAsync(Season season, CancellationToken ct = default);

    /// <summary>
    /// Delete a season
    /// </summary>
    Task DeleteSeasonAsync(Season season, CancellationToken ct = default);

    /// <summary>
    /// Save all changes to disk
    /// </summary>
    Task SaveAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get the underlying data (for backward compatibility)
    /// </summary>
    LeagueData GetData();
}
