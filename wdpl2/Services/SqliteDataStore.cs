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
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SqliteDataStore(LeagueContext context)
    {
        _context = context;
    }

    // ====== COMPETITIONS ======
    public async Task<List<Competition>> GetCompetitionsAsync(Guid? seasonId, CancellationToken ct = default)
    {
        if (!seasonId.HasValue)
            return new List<Competition>();

        await _gate.WaitAsync(ct);
        try
        {
            return await _context.Competitions
                .Where(c => c.SeasonId == seasonId)
                .OrderByDescending(c => c.CreatedDate)
                .AsNoTracking()
                .ToListAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task AddCompetitionAsync(Competition competition)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Competitions.Add(competition);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
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
        await _gate.WaitAsync();
        try
        {
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
        finally { _gate.Release(); }
    }

    public async Task DeleteCompetitionAsync(Competition competition)
    {
        // Competition may be detached (loaded with AsNoTracking), so find the
        // tracked entity by ID instead of attaching the detached graph – this
        // avoids "shadow key property unknown" errors on owned JSON collections.
        await _gate.WaitAsync();
        try
        {
            var tracked = await _context.Competitions.FindAsync(competition.Id);
            if (tracked != null)
            {
                _context.Competitions.Remove(tracked);
                await _context.SaveChangesAsync();
            }
        }
        finally { _gate.Release(); }
    }

    // ====== PLAYERS ======
    public async Task<List<Player>> GetPlayersAsync(Guid? seasonId, CancellationToken ct = default)
    {
        if (!seasonId.HasValue)
            return new List<Player>();

        await _gate.WaitAsync(ct);
        try
        {
            return await _context.Players
                .Where(p => p.SeasonId == seasonId)
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .AsNoTracking()
                .ToListAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task AddPlayerAsync(Player player)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Players.Add(player);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdatePlayerAsync(Player player)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Players.Update(player);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task DeletePlayerAsync(Player player)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Players.Remove(player);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    // ====== TEAMS ======
    public async Task<List<Team>> GetTeamsAsync(Guid? seasonId, CancellationToken ct = default)
    {
        if (!seasonId.HasValue)
            return new List<Team>();

        await _gate.WaitAsync(ct);
        try
        {
            return await _context.Teams
                .Where(t => t.SeasonId == seasonId)
                .OrderBy(t => t.Name)
                .AsNoTracking()
                .ToListAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task AddTeamAsync(Team team)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Teams.Add(team);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateTeamAsync(Team team)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Teams.Update(team);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteTeamAsync(Team team)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Teams.Remove(team);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    // ====== VENUES ======
    public async Task<List<Venue>> GetVenuesAsync(Guid? seasonId, CancellationToken ct = default)
    {
        if (!seasonId.HasValue)
            return new List<Venue>();

        await _gate.WaitAsync(ct);
        try
        {
            return await _context.Venues
                .Where(v => v.SeasonId == seasonId)
                .OrderBy(v => v.Name)
                .AsNoTracking()
                .ToListAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task AddVenueAsync(Venue venue)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateVenueAsync(Venue venue)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Venues.Update(venue);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteVenueAsync(Venue venue)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Venues.Remove(venue);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    // ====== DIVISIONS ======
    public async Task<List<Division>> GetDivisionsAsync(Guid? seasonId, CancellationToken ct = default)
    {
        if (!seasonId.HasValue)
            return new List<Division>();

        await _gate.WaitAsync(ct);
        try
        {
            return await _context.Divisions
                .Where(d => d.SeasonId == seasonId)
                .OrderBy(d => d.Name)
                .AsNoTracking()
                .ToListAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task AddDivisionAsync(Division division)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Divisions.Add(division);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateDivisionAsync(Division division)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Divisions.Update(division);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteDivisionAsync(Division division)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Divisions.Remove(division);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    // ====== FIXTURES ======
    public async Task<List<Fixture>> GetFixturesAsync(Guid? seasonId, CancellationToken ct = default)
    {
        if (!seasonId.HasValue)
            return new List<Fixture>();

        await _gate.WaitAsync(ct);
        try
        {
            return await _context.Fixtures
                .Where(f => f.SeasonId == seasonId)
                .OrderBy(f => f.Date)
                .AsNoTracking()
                .ToListAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task AddFixtureAsync(Fixture fixture)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Fixtures.Add(fixture);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateFixtureAsync(Fixture fixture)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Fixtures.Update(fixture);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteFixtureAsync(Fixture fixture)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Fixtures.Remove(fixture);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    // ====== SEASONS ======
    public async Task<List<Season>> GetSeasonsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await _context.Seasons
                .OrderByDescending(s => s.StartDate)
                .AsNoTracking()
                .ToListAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task AddSeasonAsync(Season season)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Seasons.Add(season);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateSeasonAsync(Season season)
    {
        await _gate.WaitAsync();
        try
        {
            _context.Seasons.Update(season);
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteSeasonAsync(Season season)
    {
        // Cascade delete all entities belonging to this season
        await _gate.WaitAsync();
        try
        {
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
        finally { _gate.Release(); }
    }

    // ====== COMMON ======
    public async Task SaveAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await _context.SaveChangesAsync();
        }
        finally { _gate.Release(); }
    }

    public LeagueData GetData()
    {
        // For backward compatibility, load all data into memory
        // This should be phased out as we migrate fully to EF Core
        _gate.Wait();
        try
        {
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
        finally { _gate.Release(); }
    }
}
