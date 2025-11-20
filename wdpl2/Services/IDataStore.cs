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
    Task<List<Competition>> GetCompetitionsAsync(Guid? seasonId);
    
    /// <summary>
    /// Add a new competition
    /// </summary>
    Task AddCompetitionAsync(Competition competition);
    
    /// <summary>
    /// Update an existing competition
    /// </summary>
    Task UpdateCompetitionAsync(Competition competition);
    
    /// <summary>
    /// Delete a competition
    /// </summary>
    Task DeleteCompetitionAsync(Competition competition);
    
    /// <summary>
    /// Get all players for a season
    /// </summary>
    Task<List<Player>> GetPlayersAsync(Guid? seasonId);
    
    /// <summary>
    /// Add a new player
    /// </summary>
    Task AddPlayerAsync(Player player);
    
    /// <summary>
    /// Update an existing player
    /// </summary>
    Task UpdatePlayerAsync(Player player);
    
    /// <summary>
    /// Delete a player
    /// </summary>
    Task DeletePlayerAsync(Player player);
    
    /// <summary>
    /// Get all teams for a season
    /// </summary>
    Task<List<Team>> GetTeamsAsync(Guid? seasonId);
    
    /// <summary>
    /// Add a new team
    /// </summary>
    Task AddTeamAsync(Team team);
    
    /// <summary>
    /// Update an existing team
    /// </summary>
    Task UpdateTeamAsync(Team team);
    
    /// <summary>
    /// Delete a team
    /// </summary>
    Task DeleteTeamAsync(Team team);
    
    /// <summary>
    /// Get all venues for a season
    /// </summary>
    Task<List<Venue>> GetVenuesAsync(Guid? seasonId);
    
    /// <summary>
    /// Add a new venue
    /// </summary>
    Task AddVenueAsync(Venue venue);
    
    /// <summary>
    /// Update an existing venue
    /// </summary>
    Task UpdateVenueAsync(Venue venue);
    
    /// <summary>
    /// Delete a venue
    /// </summary>
    Task DeleteVenueAsync(Venue venue);
    
    /// <summary>
    /// Get all divisions for a season
    /// </summary>
    Task<List<Division>> GetDivisionsAsync(Guid? seasonId);
    
    /// <summary>
    /// Add a new division
    /// </summary>
    Task AddDivisionAsync(Division division);
    
    /// <summary>
    /// Update an existing division
    /// </summary>
    Task UpdateDivisionAsync(Division division);
    
    /// <summary>
    /// Delete a division
    /// </summary>
    Task DeleteDivisionAsync(Division division);
    
    /// <summary>
    /// Get all fixtures for a season
    /// </summary>
    Task<List<Fixture>> GetFixturesAsync(Guid? seasonId);
    
    /// <summary>
    /// Add a new fixture
    /// </summary>
    Task AddFixtureAsync(Fixture fixture);
    
    /// <summary>
    /// Update an existing fixture
    /// </summary>
    Task UpdateFixtureAsync(Fixture fixture);
    
    /// <summary>
    /// Delete a fixture
    /// </summary>
    Task DeleteFixtureAsync(Fixture fixture);
    
    /// <summary>
    /// Get all seasons
    /// </summary>
    Task<List<Season>> GetSeasonsAsync();
    
    /// <summary>
    /// Add a new season
    /// </summary>
    Task AddSeasonAsync(Season season);
    
    /// <summary>
    /// Update an existing season
    /// </summary>
    Task UpdateSeasonAsync(Season season);
    
    /// <summary>
    /// Delete a season
    /// </summary>
    Task DeleteSeasonAsync(Season season);
    
    /// <summary>
    /// Save all changes to disk
    /// </summary>
    Task SaveAsync();
    
    /// <summary>
    /// Get the underlying data (for backward compatibility)
    /// </summary>
    LeagueData GetData();
}
