using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Implementation of IDataStore that wraps the static DataStore
/// </summary>
public class DataStoreService : IDataStore
{
    // Competitions
    public Task<List<Competition>> GetCompetitionsAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return Task.FromResult(new List<Competition>());
        
        var competitions = DataStore.Data.Competitions
            .Where(c => c.SeasonId == seasonId)
            .OrderByDescending(c => c.CreatedDate)
            .ToList();
        
        return Task.FromResult(competitions);
    }

    public Task AddCompetitionAsync(Competition competition)
    {
        DataStore.Data.Competitions.Add(competition);
        return Task.CompletedTask;
    }

    public Task UpdateCompetitionAsync(Competition competition)
    {
        return Task.CompletedTask;
    }

    public Task DeleteCompetitionAsync(Competition competition)
    {
        DataStore.Data.Competitions.Remove(competition);
        return Task.CompletedTask;
    }

    // Players
    public Task<List<Player>> GetPlayersAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return Task.FromResult(new List<Player>());
        
        var players = DataStore.Data.Players
            .Where(p => p.SeasonId == seasonId)
            .OrderBy(p => p.FullName)
            .ToList();
        
        return Task.FromResult(players);
    }

    public Task AddPlayerAsync(Player player)
    {
        DataStore.Data.Players.Add(player);
        return Task.CompletedTask;
    }

    public Task UpdatePlayerAsync(Player player)
    {
        return Task.CompletedTask;
    }

    public Task DeletePlayerAsync(Player player)
    {
        DataStore.Data.Players.Remove(player);
        return Task.CompletedTask;
    }

    // Teams
    public Task<List<Team>> GetTeamsAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return Task.FromResult(new List<Team>());
        
        var teams = DataStore.Data.Teams
            .Where(t => t.SeasonId == seasonId)
            .OrderBy(t => t.Name)
            .ToList();
        
        return Task.FromResult(teams);
    }

    public Task AddTeamAsync(Team team)
    {
        DataStore.Data.Teams.Add(team);
        return Task.CompletedTask;
    }

    public Task UpdateTeamAsync(Team team)
    {
        return Task.CompletedTask;
    }

    public Task DeleteTeamAsync(Team team)
    {
        DataStore.Data.Teams.Remove(team);
        return Task.CompletedTask;
    }

    // Venues
    public Task<List<Venue>> GetVenuesAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return Task.FromResult(new List<Venue>());
        
        var venues = DataStore.Data.Venues
            .Where(v => v.SeasonId == seasonId)
            .OrderBy(v => v.Name)
            .ToList();
        
        return Task.FromResult(venues);
    }

    public Task AddVenueAsync(Venue venue)
    {
        DataStore.Data.Venues.Add(venue);
        return Task.CompletedTask;
    }

    public Task UpdateVenueAsync(Venue venue)
    {
        return Task.CompletedTask;
    }

    public Task DeleteVenueAsync(Venue venue)
    {
        DataStore.Data.Venues.Remove(venue);
        return Task.CompletedTask;
    }

    // Divisions
    public Task<List<Division>> GetDivisionsAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return Task.FromResult(new List<Division>());
        
        var divisions = DataStore.Data.Divisions
            .Where(d => d.SeasonId == seasonId)
            .OrderBy(d => d.Name)
            .ToList();
        
        return Task.FromResult(divisions);
    }

    public Task AddDivisionAsync(Division division)
    {
        DataStore.Data.Divisions.Add(division);
        return Task.CompletedTask;
    }

    public Task UpdateDivisionAsync(Division division)
    {
        return Task.CompletedTask;
    }

    public Task DeleteDivisionAsync(Division division)
    {
        DataStore.Data.Divisions.Remove(division);
        return Task.CompletedTask;
    }

    // Fixtures
    public Task<List<Fixture>> GetFixturesAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return Task.FromResult(new List<Fixture>());
        
        var fixtures = DataStore.Data.Fixtures
            .Where(f => f.SeasonId == seasonId)
            .OrderBy(f => f.Date)
            .ToList();
        
        return Task.FromResult(fixtures);
    }

    public Task AddFixtureAsync(Fixture fixture)
    {
        DataStore.Data.Fixtures.Add(fixture);
        return Task.CompletedTask;
    }

    public Task UpdateFixtureAsync(Fixture fixture)
    {
        return Task.CompletedTask;
    }

    public Task DeleteFixtureAsync(Fixture fixture)
    {
        DataStore.Data.Fixtures.Remove(fixture);
        return Task.CompletedTask;
    }

    // Seasons
    public Task<List<Season>> GetSeasonsAsync()
    {
        var seasons = DataStore.Data.Seasons
            .OrderByDescending(s => s.StartDate)
            .ToList();
        
        return Task.FromResult(seasons);
    }

    public Task AddSeasonAsync(Season season)
    {
        DataStore.Data.Seasons.Add(season);
        return Task.CompletedTask;
    }

    public Task UpdateSeasonAsync(Season season)
    {
        return Task.CompletedTask;
    }

    public Task DeleteSeasonAsync(Season season)
    {
        DataStore.Data.Seasons.Remove(season);
        return Task.CompletedTask;
    }

    // Common
    public Task SaveAsync()
    {
        DataStore.Save();
        return Task.CompletedTask;
    }

    public LeagueData GetData()
    {
        return DataStore.Data;
    }
}
