using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Models;

namespace Wdpl2.Services.Inbox;

/// <summary>Applies an explicitly reviewed scorecard against current database identities.</summary>
public sealed class AdminScorecardPersistence(LeagueContext context)
{
    public async Task<JsonElement> CaptureLocalFramesAsync(AdminSyncReviewItem item, CancellationToken ct = default)
    {
        if (!Guid.TryParse(item.Change.SeasonId, out var seasonId) || !Guid.TryParse(item.Change.Id, out var fixtureId))
            throw new InvalidOperationException("Explicit fixture and season identities are required.");
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var league = new LeagueData
        {
            Seasons = await context.Seasons.AsNoTracking().Where(s => s.Id == seasonId).ToListAsync(ct),
            Teams = await context.Teams.AsNoTracking().Where(t => t.SeasonId == seasonId).ToListAsync(ct),
            Players = await context.Players.AsNoTracking().Where(p => p.SeasonId == seasonId).ToListAsync(ct),
            Fixtures = await context.Fixtures.AsNoTracking().Where(f => f.Id == fixtureId).ToListAsync(ct)
        };
        var frames = AdminScorecardMapper.CreateLocalFrames(league, item);
        await transaction.CommitAsync(ct);
        return frames;
    }

    public async Task<Fixture> ApplyServerAsync(AdminSyncReviewItem item, CancellationToken ct = default)
    {
        if (item.LocalSnapshot is not { } expected || !Guid.TryParse(item.Change.SeasonId, out var seasonId) ||
            !Guid.TryParse(item.Change.Id, out var fixtureId) || item.ResolutionRequestId == Guid.Empty)
            throw new InvalidOperationException("Capture and review the local fixture before applying a server scorecard.");
        await context.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS AdminSyncApplications (RequestId TEXT PRIMARY KEY, Fingerprint TEXT NOT NULL, AppliedJson TEXT NOT NULL)", ct);
        var requestId = item.ResolutionRequestId.ToString();
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { item.Change, Expected = expected }))));
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var league = new LeagueData
            {
                Seasons = await context.Seasons.AsNoTracking().Where(s => s.Id == seasonId).ToListAsync(ct),
                Teams = await context.Teams.AsNoTracking().Where(t => t.SeasonId == seasonId).ToListAsync(ct),
                Players = await context.Players.AsNoTracking().Where(p => p.SeasonId == seasonId).ToListAsync(ct),
                Fixtures = await context.Fixtures.AsNoTracking().Where(f => f.Id == fixtureId).ToListAsync(ct)
            };
            var receipt = await context.Database.SqlQueryRaw<ApplicationReceipt>(
                "SELECT Fingerprint, AppliedJson FROM AdminSyncApplications WHERE RequestId = {0}", requestId).SingleOrDefaultAsync(ct);
            if (receipt != null)
            {
                if (receipt.Fingerprint != fingerprint || league.Seasons.Count != 1 || league.Seasons[0].IsLocked ||
                    !JsonElement.DeepEquals(AdminScorecardMapper.Capture(league, fixtureId), JsonSerializer.Deserialize<JsonElement>(receipt.AppliedJson)))
                    throw new InvalidOperationException("The previously applied scorecard or season changed. Review again before acknowledging.");
                await transaction.CommitAsync(ct);
                return league.Fixtures.Single();
            }
            var draft = AdminScorecardMapper.CreateReviewedDraft(league, item.Change, expected);
            // Match the existing fixture persistence strategy for EF's owned JSON frames.
            var deleted = await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Fixtures WHERE Id = {fixtureId}", ct);
            if (deleted != 1) throw new InvalidOperationException("The reviewed fixture no longer exists.");
            context.ChangeTracker.Clear();
            context.Fixtures.Add(draft);
            await context.SaveChangesAsync(ct);
            context.ChangeTracker.Clear();
            var persisted = await context.Fixtures.AsNoTracking().SingleAsync(f => f.Id == fixtureId, ct);
            var appliedJson = JsonSerializer.Serialize(persisted);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO AdminSyncApplications (RequestId, Fingerprint, AppliedJson) VALUES ({requestId}, {fingerprint}, {appliedJson})", ct);
            await transaction.CommitAsync(ct);
            return persisted;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    private sealed class ApplicationReceipt
    {
        public string Fingerprint { get; set; } = "";
        public string AppliedJson { get; set; } = "";
    }
}
