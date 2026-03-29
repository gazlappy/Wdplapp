using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// SQLite-based implementation of IDataStore using Entity Framework Core.
/// Provides high-performance data access with automatic relationship management.
/// </summary>
public class SqliteDataStore : IDataStore
{
    private readonly LeagueContext _context;

    public SqliteDataStore(LeagueContext context)
    {
        _context = context;
    }

    // ====== COMPETITIONS ======
    public async Task<List<Competition>> GetCompetitionsAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return new List<Competition>();

        return await _context.Competitions
            .Where(c => c.SeasonId == seasonId)
            .OrderByDescending(c => c.CreatedDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddCompetitionAsync(Competition competition)
    {
        _context.Competitions.Add(competition);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateCompetitionAsync(Competition competition)
    {
        // EF Core's JSON change tracking for deeply nested OwnsMany().ToJson()
        // collections (Rounds → Matches, Groups → Standings, etc.) is broken in
        // two ways:
        //   1. Copying collections to a tracked entity → NullReferenceException
        //      in FindJsonPartialUpdateInfo (partial-update diff crash).
        //   2. Update() on a detached entity → InvalidOperationException because
        //      the __synthesizedOrdinal shadow keys were never populated.
        //
        // Workaround: delete the old row and re-insert within a transaction.
        // Add() works because it creates fresh tracking entries with proper
        // ordinal values. Competition has no FK references from other tables
        // (see LeagueContext note), so delete + re-insert is safe.
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM Competitions WHERE Id = {competition.Id}");

            // Clear the tracker so the subsequent Add doesn't conflict
            // with any stale entries from FindAsync or prior operations.
            _context.ChangeTracker.Clear();

            _context.Competitions.Add(competition);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteCompetitionAsync(Competition competition)
    {
        // Competition may be detached (loaded with AsNoTracking), so find the
        // tracked entity by ID instead of attaching the detached graph – this
        // avoids "shadow key property unknown" errors on owned JSON collections.
        var tracked = await _context.Competitions.FindAsync(competition.Id);
        if (tracked != null)
        {
            _context.Competitions.Remove(tracked);
            await _context.SaveChangesAsync();
        }
    }

    // ====== PLAYERS ======
    public async Task<List<Player>> GetPlayersAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return new List<Player>();

        return await _context.Players
            .Where(p => p.SeasonId == seasonId)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddPlayerAsync(Player player)
    {
        _context.Players.Add(player);
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePlayerAsync(Player player)
    {
        _context.Players.Update(player);
        await _context.SaveChangesAsync();
    }

    public async Task DeletePlayerAsync(Player player)
    {
        _context.Players.Remove(player);
        await _context.SaveChangesAsync();
    }

    // ====== TEAMS ======
    public async Task<List<Team>> GetTeamsAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return new List<Team>();

        return await _context.Teams
            .Where(t => t.SeasonId == seasonId)
            .OrderBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddTeamAsync(Team team)
    {
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateTeamAsync(Team team)
    {
        _context.Teams.Update(team);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteTeamAsync(Team team)
    {
        _context.Teams.Remove(team);
        await _context.SaveChangesAsync();
    }

    // ====== VENUES ======
    public async Task<List<Venue>> GetVenuesAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return new List<Venue>();

        return await _context.Venues
            .Where(v => v.SeasonId == seasonId)
            .OrderBy(v => v.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddVenueAsync(Venue venue)
    {
        _context.Venues.Add(venue);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateVenueAsync(Venue venue)
    {
        _context.Venues.Update(venue);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteVenueAsync(Venue venue)
    {
        _context.Venues.Remove(venue);
        await _context.SaveChangesAsync();
    }

    // ====== DIVISIONS ======
    public async Task<List<Division>> GetDivisionsAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return new List<Division>();

        return await _context.Divisions
            .Where(d => d.SeasonId == seasonId)
            .OrderBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddDivisionAsync(Division division)
    {
        _context.Divisions.Add(division);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateDivisionAsync(Division division)
    {
        _context.Divisions.Update(division);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteDivisionAsync(Division division)
    {
        _context.Divisions.Remove(division);
        await _context.SaveChangesAsync();
    }

    // ====== FIXTURES ======
    public async Task<List<Fixture>> GetFixturesAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return new List<Fixture>();

        return await _context.Fixtures
            .Where(f => f.SeasonId == seasonId)
            .OrderBy(f => f.Date)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddFixtureAsync(Fixture fixture)
    {
        _context.Fixtures.Add(fixture);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateFixtureAsync(Fixture fixture)
    {
        _context.Fixtures.Update(fixture);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteFixtureAsync(Fixture fixture)
    {
        _context.Fixtures.Remove(fixture);
        await _context.SaveChangesAsync();
    }

    // ====== SEASONS ======
    public async Task<List<Season>> GetSeasonsAsync()
    {
        return await _context.Seasons
            .OrderByDescending(s => s.StartDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddSeasonAsync(Season season)
    {
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateSeasonAsync(Season season)
    {
        _context.Seasons.Update(season);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteSeasonAsync(Season season)
    {
        // Cascade delete all entities belonging to this season
        var seasonId = season.Id;

        _context.Fixtures.RemoveRange(_context.Fixtures.Where(f => f.SeasonId == seasonId));
        _context.Players.RemoveRange(_context.Players.Where(p => p.SeasonId == seasonId));
        _context.Teams.RemoveRange(_context.Teams.Where(t => t.SeasonId == seasonId));
        _context.Venues.RemoveRange(_context.Venues.Where(v => v.SeasonId == seasonId));
        _context.Divisions.RemoveRange(_context.Divisions.Where(d => d.SeasonId == seasonId));
        _context.Competitions.RemoveRange(_context.Competitions.Where(c => c.SeasonId == seasonId));
        _context.Seasons.Remove(season);

        await _context.SaveChangesAsync();
    }

    // ====== COMMON ======
    public async Task SaveAsync()
    {
        await _context.SaveChangesAsync();
    }

    public LeagueData GetData()
    {
        // For backward compatibility, load all data into memory
        // This should be phased out as we migrate fully to EF Core
        return new LeagueData
        {
            Seasons = _context.Seasons.ToList(),
            Divisions = _context.Divisions.ToList(),
            Teams = _context.Teams.ToList(),
            Players = _context.Players.ToList(),
            Venues = _context.Venues.ToList(),
            Fixtures = _context.Fixtures.ToList(),
            Competitions = _context.Competitions.ToList()
        };
    }
}
