using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// SQLite-based implementation of IDataStore using Entity Framework Core.
/// Provides high-performance data access with automatic relationship management.
/// </summary>
public partial class SqliteDataStore : IDataStore
{
    private readonly LeagueContext _context;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Cached LeagueData snapshot for the legacy GetData() path. Pages call
    // GetData() many times per refresh (often inside tight loops), and each
    // uncached call would issue 7 separate full-table queries against SQLite.
    // The cache is invalidated whenever SaveAsync runs or any Add/Update/Delete
    // mutates the database.
    //
    // NOTE: static (process-wide) on purpose. IDataStore is registered as
    // Transient so each page/service gets its own SqliteDataStore instance;
    // a per-instance cache would leave OTHER instances handing out stale data
    // after a write (the "Import on inbox doesn't update FixturesPage" bug).
    // The snapshot only contains AsNoTracking() POCOs so it's safe to share.
    private static LeagueData? _cachedSnapshot;
    private static readonly object _snapshotLock = new();

    private void InvalidateSnapshot()
    {
        lock (_snapshotLock) { _cachedSnapshot = null; }

        // Keep the legacy DataStore.Data JSON cache in sync with SQLite.
        // The website generator and several Website Builder pages read from
        // DataStore.Data directly (e.g. "private static LeagueData League =>
        // DataStore.Data;"), so without this refresh a fixture saved via the
        // typed Add/Update/Delete*Async methods would appear correctly on the
        // editor tab but the generated website would publish the stale copy.
        // Only fires on writes (this method is only invoked from mutating
        // methods), so reads stay cheap.
        try { DataStore.RefreshEntitiesFromDatabase(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SqliteDataStore: RefreshEntitiesFromDatabase failed: {ex.Message}");
        }
    }

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

    public async Task AddCompetitionAsync(Competition competition, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Competitions.Add(competition);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateCompetitionAsync(Competition competition, CancellationToken ct = default)
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
        await _gate.WaitAsync(ct);
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM Competitions WHERE Id = {competition.Id}", ct);

                // Clear the tracker so the subsequent Add doesn't conflict
                // with any stale entries from FindAsync or prior operations.
                _context.ChangeTracker.Clear();

                _context.Competitions.Add(competition);
                await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteCompetitionAsync(Competition competition, CancellationToken ct = default)
    {
        // Competition may be detached (loaded with AsNoTracking), so find the
        // tracked entity by ID instead of attaching the detached graph – this
        // avoids "shadow key property unknown" errors on owned JSON collections.
        await _gate.WaitAsync(ct);
        try
        {
            var tracked = await _context.Competitions.FindAsync(new object?[] { competition.Id }, ct);
            if (tracked != null)
            {
                _context.Competitions.Remove(tracked);
                await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
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

    public async Task AddPlayerAsync(Player player, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Players.Add(player);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdatePlayerAsync(Player player, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Players.Update(player);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task DeletePlayerAsync(Player player, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Players.Remove(player);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
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

    public async Task AddTeamAsync(Team team, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Teams.Add(team);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateTeamAsync(Team team, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Teams.Update(team);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteTeamAsync(Team team, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Teams.Remove(team);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
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

    public async Task AddVenueAsync(Venue venue, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateVenueAsync(Venue venue, CancellationToken ct = default)
    {
        // Venue.Tables is OwnsMany(...).ToJson(); same EF detached-update bug
        // as Fixture/Competition (see UpdateFixtureAsync comment). Use the
        // delete-and-reinsert workaround so table edits actually persist.
        await _gate.WaitAsync(ct);
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM Venues WHERE Id = {venue.Id}", ct);

                _context.ChangeTracker.Clear();

                _context.Venues.Add(venue);
                await _context.SaveChangesAsync(ct);
                InvalidateSnapshot();

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteVenueAsync(Venue venue, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Venues.Remove(venue);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
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

    public async Task AddDivisionAsync(Division division, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Divisions.Add(division);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateDivisionAsync(Division division, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Divisions.Update(division);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteDivisionAsync(Division division, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Divisions.Remove(division);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
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

    public async Task AddFixtureAsync(Fixture fixture, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Fixtures.Add(fixture);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateFixtureAsync(Fixture fixture, CancellationToken ct = default)
    {
        // Fixture.Frames is configured as OwnsMany(...).ToJson(). EF Core's
        // change tracking for owned JSON collections is broken on detached
        // entities (see the long note in UpdateCompetitionAsync above):
        //   • Update() throws on missing __synthesizedOrdinal shadow keys.
        //   • Even when it doesn't throw, the partial-update diff silently
        //     fails to write Frame mutations to disk – which is exactly the
        //     "Save button doesn't save edited fixtures" bug.
        // Workaround: delete the old row and re-insert in a transaction.
        await _gate.WaitAsync(ct);
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM Fixtures WHERE Id = {fixture.Id}", ct);

                _context.ChangeTracker.Clear();

                _context.Fixtures.Add(fixture);
                await _context.SaveChangesAsync(ct);
                InvalidateSnapshot();

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteFixtureAsync(Fixture fixture, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Fixtures.Remove(fixture);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task<int> DeleteFixturesAsync(Guid? seasonId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.ChangeTracker.Clear();
            var query = _context.Fixtures.AsQueryable();
            if (seasonId.HasValue) query = query.Where(f => f.SeasonId == seasonId);
            var fixtures = await query.ToListAsync(ct);
            var affectedSeasons = fixtures.Where(f => f.SeasonId.HasValue).Select(f => f.SeasonId!.Value).ToList();
            if (seasonId.HasValue) affectedSeasons.Add(seasonId.Value);
            var locked = await _context.Seasons.AsNoTracking()
                .Where(s => s.IsLocked && affectedSeasons.Contains(s.Id)).Select(s => s.Name).ToListAsync(ct);
            if (locked.Count > 0)
                throw new InvalidOperationException($"No fixtures were deleted. Locked seasons are protected: {string.Join(", ", locked)}. Use Delete Season for an unlocked season.");
            _context.Fixtures.RemoveRange(fixtures);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
            return fixtures.Count;
        }
        catch
        {
            _context.ChangeTracker.Clear();
            throw;
        }
        finally { _gate.Release(); }
    }

    public async Task SaveFixtureNumbersAsync(FixtureNumberEditor editor, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.ChangeTracker.Clear();
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(ct) : null;
            var seasonId = editor.SeasonId;
            var current = new LeagueData
            {
                Seasons = await _context.Seasons.AsNoTracking().Where(s => s.Id == seasonId).ToListAsync(ct),
                Teams = await _context.Teams.AsNoTracking().Where(t => t.SeasonId == seasonId).ToListAsync(ct),
                Divisions = await _context.Divisions.AsNoTracking().Where(d => d.SeasonId == seasonId).ToListAsync(ct),
                Venues = await _context.Venues.AsNoTracking().Where(v => v.SeasonId == seasonId).ToListAsync(ct),
                Settings = GetData().GetSettingsForSeason(seasonId)
            };
            var existing = await _context.Fixtures.Where(f => f.SeasonId == seasonId).ToListAsync(ct);
            var reviewed = editor.ValidateForSave(current, existing);
            var byId = existing.ToDictionary(f => f.Id);
            var keep = reviewed.Select(f => f.Id).ToHashSet();
            _context.Fixtures.RemoveRange(existing.Where(f => !keep.Contains(f.Id)));
            foreach (var fixture in reviewed)
            {
                if (byId.TryGetValue(fixture.Id, out var tracked))
                {
                    // Preserve IDs, result collections and metadata on unchanged matches.
                    tracked.Date = fixture.Date;
                    tracked.VenueId = fixture.VenueId;
                    tracked.TableId = fixture.TableId;
                }
                else _context.Fixtures.Add(fixture);
            }
            await _context.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
            InvalidateSnapshot();
        }
        catch
        {
            _context.ChangeTracker.Clear();
            throw;
        }
        finally { _gate.Release(); }
    }

    public Task ReplaceFixturesForSeasonAsync(Guid seasonId, IReadOnlyList<Fixture> fixtures, CancellationToken ct = default)
        => ReplaceFixturesAsync(seasonId, fixtures, false, ct);

    public Task ReplaceGeneratedFixturesForSeasonAsync(Guid seasonId, IReadOnlyList<Fixture> fixtures, CancellationToken ct = default)
        => ReplaceFixturesAsync(seasonId, fixtures, true, ct);

    private async Task ReplaceFixturesAsync(Guid seasonId, IReadOnlyList<Fixture> fixtures, bool validateGenerated, CancellationToken ct)
    {
        // Delete + insert inside a single SaveChanges call so the whole batch is
        // atomic (EF wraps one SaveChanges in an implicit transaction on relational
        // providers, and this also works on the InMemory provider used in tests).
        await _gate.WaitAsync(ct);
        try
        {
            _context.ChangeTracker.Clear();

            if (validateGenerated)
            {
                var current = new LeagueData();
                current.Seasons.AddRange(await _context.Seasons.AsNoTracking().Where(s => s.Id == seasonId).ToListAsync(ct));
                current.Teams.AddRange(await _context.Teams.AsNoTracking().Where(t => t.SeasonId == seasonId).ToListAsync(ct));
                current.Divisions.AddRange(await _context.Divisions.AsNoTracking().Where(d => d.SeasonId == seasonId).ToListAsync(ct));
                current.Venues.AddRange(await _context.Venues.AsNoTracking().Where(v => v.SeasonId == seasonId).ToListAsync(ct));
                var season = current.Seasons.SingleOrDefault() ?? throw new InvalidOperationException("Season not found.");
                var settings = GetData().GetSettingsForSeason(seasonId);
                GeneratedScheduleValidator.Validate(current, seasonId, fixtures, season.StartDate, season.EndDate,
                    settings.DefaultMatchDay, settings.DefaultMatchTime, settings.DefaultRoundsPerOpponent, season.BlackoutDates);
            }

            var existing = await _context.Fixtures
                .Where(f => f.SeasonId == seasonId)
                .ToListAsync(ct);
            _context.Fixtures.RemoveRange(existing);

            _context.Fixtures.AddRange(fixtures);

            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task AddSeasonEntitiesAsync(
        IReadOnlyList<Division>? divisions = null,
        IReadOnlyList<Venue>? venues = null,
        IReadOnlyList<Team>? teams = null,
        IReadOnlyList<Player>? players = null,
        CancellationToken ct = default)
    {
        // Single SaveChanges so the copy is all-or-nothing and the snapshot
        // cache is only invalidated once (the old per-entity Add*Async loop
        // refreshed the entire JSON cache for every row inserted).
        await _gate.WaitAsync(ct);
        try
        {
            _context.ChangeTracker.Clear();

            if (divisions is { Count: > 0 }) _context.Divisions.AddRange(divisions);
            if (venues is { Count: > 0 }) _context.Venues.AddRange(venues);
            if (teams is { Count: > 0 }) _context.Teams.AddRange(teams);
            if (players is { Count: > 0 }) _context.Players.AddRange(players);

            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
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

    public async Task AddSeasonAsync(Season season, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _context.Seasons.Add(season);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateSeasonAsync(Season season, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            // Callers pass detached snapshot instances; a different instance of
            // the same Season may still be tracked from a previous update, which
            // makes Update() throw "another instance with the same key value is
            // already being tracked". Clear the tracker so each update is clean.
            _context.ChangeTracker.Clear();
            _context.Seasons.Update(season);
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteSeasonAsync(Season season, CancellationToken ct = default)
    {
        // Cascade delete all entities belonging to this season
        await _gate.WaitAsync(ct);
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

            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    // ====== COMMON ======
    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await _context.SaveChangesAsync(ct);
            InvalidateSnapshot();
        }
        finally { _gate.Release(); }
    }

    [Obsolete("Use the typed Get*Async methods instead. This back-compat snapshot loads everything into memory and bypasses the async gate.")]
    public LeagueData GetData()
    {
        // Back-compat snapshot — does NOT take the async semaphore (would deadlock
        // if called from the UI thread while an async op holds the gate).
        // Cached so repeated calls within a page refresh don't re-query SQLite.
        lock (_snapshotLock)
        {
            if (_cachedSnapshot != null) return _cachedSnapshot;

            _cachedSnapshot = new LeagueData
            {
                Seasons = _context.Seasons.AsNoTracking().ToList(),
                Divisions = _context.Divisions.AsNoTracking().ToList(),
                Teams = _context.Teams.AsNoTracking().ToList(),
                Players = _context.Players.AsNoTracking().ToList(),
                Venues = _context.Venues.AsNoTracking().ToList(),
                Fixtures = _context.Fixtures.AsNoTracking().ToList(),
                Competitions = _context.Competitions.AsNoTracking().ToList()
            };

            // Carry over JSON-only fields that aren't stored in EF Core.
            // Without these, callers see ActiveSeasonId == null (which causes
            // pages like FixturesPage to early-return and show an empty list)
            // and lose access to Settings/WebsiteSettings/CalendarEvents.
            var json = DataStore.Data;
            if (json != null)
            {
                _cachedSnapshot.ActiveSeasonId = json.ActiveSeasonId;
                _cachedSnapshot.Settings = json.Settings;
                _cachedSnapshot.WebsiteSettings = json.WebsiteSettings;
                _cachedSnapshot.FixturesSheetSettings = json.FixturesSheetSettings;
                _cachedSnapshot.CalendarEvents = json.CalendarEvents;
                _cachedSnapshot.CalendarSettings = json.CalendarSettings;
                _cachedSnapshot.DoublesPairings = json.DoublesPairings;

                // Restore JSON-only Season properties (BlackoutDateTitles, Settings)
                // that EF Core doesn't persist.
                var jsonSeasonsById = json.Seasons.ToDictionary(s => s.Id, s => s);
                foreach (var s in _cachedSnapshot.Seasons)
                {
                    if (jsonSeasonsById.TryGetValue(s.Id, out var js))
                    {
                        s.BlackoutDateTitles = js.BlackoutDateTitles;
                        if (js.Settings != null) s.Settings = js.Settings;
                    }
                }
            }

            return _cachedSnapshot;
        }
    }
}
