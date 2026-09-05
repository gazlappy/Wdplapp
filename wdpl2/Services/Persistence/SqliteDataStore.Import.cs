using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wdpl2.Models;
using Wdpl2.Services.Import;

namespace Wdpl2.Services;

public partial class SqliteDataStore
{
    public async Task CommitImportAsync(LeagueData baseline, LeagueData imported, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            var metadataBefore = ImportWorkspace.Clone(DataStore.Data);
            bool metadataWritten = false;
            try
            {
                _context.ChangeTracker.Clear();
                var placementBefore = await ReadImportPlacementAsync(ct);
                var locked = await _context.Seasons.AsNoTracking().Where(s => s.IsLocked).Select(s => s.Id).ToListAsync(ct);
                var seasons = await ValidateDeltaAsync(baseline.Seasons, imported.Seasons, s => s.Id, s => s.Id, locked, ct);
                var divisions = await ValidateDeltaAsync(baseline.Divisions, imported.Divisions, s => s.Id, s => s.SeasonId, locked, ct);
                var venues = await ValidateDeltaAsync(baseline.Venues, imported.Venues, s => s.Id, s => s.SeasonId, locked, ct);
                var teams = await ValidateDeltaAsync(baseline.Teams, imported.Teams, s => s.Id, s => s.SeasonId, locked, ct);
                var players = await ValidateDeltaAsync(baseline.Players, imported.Players, s => s.Id, s => s.SeasonId, locked, ct);
                var fixtures = await ValidateDeltaAsync(baseline.Fixtures, imported.Fixtures, s => s.Id, s => s.SeasonId, locked, ct);
                var competitions = await ValidateDeltaAsync(baseline.Competitions, imported.Competitions, s => s.Id, s => s.SeasonId, locked, ct);

                foreach (var season in seasons.Upserts.Where(s => !baseline.Seasons.Any(b => b.Id == s.Id)))
                    season.IsActive = false;

                await UpsertImportAsync(seasons.Upserts, ct);
                await UpsertImportAsync(divisions.Upserts, ct);
                await UpsertImportAsync(venues.Upserts, ct);
                await UpsertImportAsync(teams.Upserts, ct);
                await UpsertImportAsync(players.Upserts, ct);
                await UpsertImportAsync(fixtures.Upserts, ct);
                await UpsertImportAsync(competitions.Upserts, ct);
                await DeleteImportAsync(competitions.Deleted, ct);
                await DeleteImportAsync(fixtures.Deleted, ct);
                await DeleteImportAsync(players.Deleted, ct);
                await DeleteImportAsync(teams.Deleted, ct);
                await DeleteImportAsync(venues.Deleted, ct);
                await DeleteImportAsync(divisions.Deleted, ct);
                await DeleteImportAsync(seasons.Deleted, ct);

                ImportPlacementValidator.ThrowIfNewIssues(placementBefore, await ReadImportPlacementAsync(ct));

                if (!ImportWorkspace.Equal(baseline.DoublesPairings, imported.DoublesPairings))
                {
                    if (!ImportWorkspace.Equal(baseline.DoublesPairings, metadataBefore.DoublesPairings))
                        throw new InvalidOperationException("Doubles data changed since this preview. Reload the import before saving.");
                    if (imported.DoublesPairings.Any(p => p.SeasonId is Guid sid && locked.Contains(sid) &&
                        !baseline.DoublesPairings.Any(b => ImportWorkspace.Equal(b, p))) ||
                        baseline.DoublesPairings.Any(p => p.SeasonId is Guid sid && locked.Contains(sid) &&
                        !imported.DoublesPairings.Any(b => ImportWorkspace.Equal(b, p))))
                        throw new InvalidOperationException("Cannot import into a locked season.");
                }
                DataStore.WriteImportMetadata(baseline, imported);
                metadataWritten = true;
                await transaction.CommitAsync(ct);
                InvalidateSnapshot();
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                if (metadataWritten) DataStore.WriteImportMetadata(imported, metadataBefore);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    private async Task<LeagueData> ReadImportPlacementAsync(CancellationToken ct) => new()
    {
        Seasons = await _context.Seasons.AsNoTracking().ToListAsync(ct),
        Divisions = await _context.Divisions.AsNoTracking().ToListAsync(ct),
        Venues = await _context.Venues.AsNoTracking().ToListAsync(ct),
        Teams = await _context.Teams.AsNoTracking().ToListAsync(ct),
        Players = await _context.Players.AsNoTracking().ToListAsync(ct),
        Fixtures = await _context.Fixtures.AsNoTracking().ToListAsync(ct),
        Competitions = await _context.Competitions.AsNoTracking().ToListAsync(ct)
    };

    private async Task<(List<T> Upserts, List<T> Deleted)> ValidateDeltaAsync<T>(List<T> before, List<T> after,
        Func<T, Guid> id, Func<T, Guid?> seasonId, List<Guid> locked, CancellationToken ct) where T : class
    {
        var old = before.ToDictionary(id);
        var proposed = after.ToDictionary(id);
        if (proposed.ContainsKey(Guid.Empty)) throw new InvalidDataException("An imported record has no ID.");
        var changed = after.Where(x => !old.TryGetValue(id(x), out var b) || !ImportWorkspace.Equal(b, x)).ToList();
        var deleted = before.Where(x => !proposed.ContainsKey(id(x))).ToList();
        var current = (await _context.Set<T>().AsNoTracking().ToListAsync(ct)).ToDictionary(id);
        foreach (var item in changed.Concat(deleted))
        {
            var key = id(item);
            if (seasonId(item) is Guid sid && locked.Contains(sid) ||
                old.TryGetValue(key, out var previous) && seasonId(previous) is Guid oldSid && locked.Contains(oldSid))
                throw new InvalidOperationException("Cannot change a locked season. Unlock it before importing.");
            if (old.TryGetValue(key, out var original))
            {
                if (!current.TryGetValue(key, out var now) || !PersistedEqual(original, now))
                    throw new InvalidOperationException($"A {typeof(T).Name} record changed since this preview. Reload before importing.");
            }
            else if (current.ContainsKey(key))
                throw new InvalidOperationException($"An imported {typeof(T).Name} ID already exists. Reload before importing.");
        }
        return (changed, deleted);
    }

    private static bool PersistedEqual<T>(T left, T right)
    {
        var a = ImportWorkspace.Clone(left);
        var b = ImportWorkspace.Clone(right);
        if (a is Season sa && b is Season sb)
        {
            sa.Settings = sb.Settings = null;
            sa.BlackoutDateTitles = new();
            sb.BlackoutDateTitles = new();
        }
        return ImportWorkspace.Equal(a, b);
    }

    private async Task UpsertImportAsync<T>(List<T> entities, CancellationToken ct) where T : class
    {
        var type = _context.Model.FindEntityType(typeof(T))!;
        var table = type.GetTableName()!;
        var tableId = StoreObjectIdentifier.Table(table, type.GetSchema());
        var keyProperty = type.FindPrimaryKey()!.Properties.Single().PropertyInfo!;
        foreach (var entity in entities)
        {
            var id = (Guid)keyProperty.GetValue(entity)!;
            var exists = await _context.Set<T>().FindAsync([id], ct) != null;
            _context.ChangeTracker.Clear();
            if (!exists)
            {
                _context.Set<T>().Add(ImportWorkspace.Clone(entity));
                await _context.SaveChangesAsync(ct);
            }
            else
            {
                // Stage a fresh row so EF serializes owned JSON correctly. Copy columns in place
                // rather than deleting the original row, which would trigger cascading FK changes.
                var staged = ImportWorkspace.Clone(entity);
                var temporaryId = Guid.NewGuid();
                keyProperty.SetValue(staged, temporaryId);
                _context.Set<T>().Add(staged);
                await _context.SaveChangesAsync(ct);
                var columns = type.GetProperties().Where(p => !p.IsPrimaryKey()).Select(p => p.GetColumnName(tableId)!)
                    .Concat(type.GetNavigations().Where(n => n.TargetEntityType.IsOwned())
                        .Select(n => n.TargetEntityType.GetContainerColumnName()).OfType<string>()).Distinct().ToList();
                var assignments = string.Join(", ", columns.Select(c => $"\"{c}\" = (SELECT \"{c}\" FROM \"{table}\" WHERE \"Id\" = {{0}})"));
                await _context.Database.ExecuteSqlRawAsync($"UPDATE \"{table}\" SET {assignments} WHERE \"Id\" = {{1}}", [temporaryId, id], ct);
                await _context.Database.ExecuteSqlRawAsync($"DELETE FROM \"{table}\" WHERE \"Id\" = {{0}}", [temporaryId], ct);
            }
            _context.ChangeTracker.Clear();
        }
    }

    private async Task DeleteImportAsync<T>(List<T> deleted, CancellationToken ct) where T : class
    {
        var type = _context.Model.FindEntityType(typeof(T))!;
        var table = type.GetTableName()!;
        var key = type.FindPrimaryKey()!.Properties.Single().PropertyInfo!;
        foreach (var item in deleted)
            await _context.Database.ExecuteSqlRawAsync($"DELETE FROM \"{table}\" WHERE \"Id\" = {{0}}", [key.GetValue(item)!], ct);
    }
}
