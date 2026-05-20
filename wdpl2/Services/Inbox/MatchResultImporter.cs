namespace Wdpl2.Services.Inbox;

public enum ImportSidePreference { Auto, Home, Away }

public interface IMatchResultImporter
{
    /// <summary>
    /// Writes an agreed (or admin-resolved) match result into the local data store:
    /// resolves/creates players, fills the fixture's <see cref="Fixture.Frames"/>,
    /// and persists. Returns a short summary suitable for the status bar.
    /// </summary>
    Task<MatchImportResult> ImportAsync(FixtureSubmissionGroup group,
        ImportSidePreference prefer = ImportSidePreference.Auto,
        CancellationToken ct = default);
}

public sealed class MatchImportResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public int FramesWritten { get; init; }
    public int PlayersCreated { get; init; }
}

public sealed class MatchResultImporter : IMatchResultImporter
{
    private readonly IDataStore _store;
    private readonly ISeasonService _season;

    public MatchResultImporter(IDataStore store, ISeasonService season)
    {
        _store = store;
        _season = season;
    }

    public async Task<MatchImportResult> ImportAsync(FixtureSubmissionGroup group,
        ImportSidePreference prefer = ImportSidePreference.Auto,
        CancellationToken ct = default)
    {
        var payload = prefer switch
        {
            ImportSidePreference.Home => group.HomePayload ?? group.AwayPayload,
            ImportSidePreference.Away => group.AwayPayload ?? group.HomePayload,
            _                         => group.HomePayload ?? group.AwayPayload,
        } ?? throw new InvalidOperationException("No payload to import.");

        var seasonId = payload.SeasonId ?? _season.CurrentSeasonId;
        var fixtures = await _store.GetFixturesAsync(seasonId, ct).ConfigureAwait(false);
        var fixture = fixtures.FirstOrDefault(f => f.Id == group.FixtureId);
        if (fixture is null)
        {
            return new MatchImportResult
            {
                Success = false,
                Message = $"Fixture {group.FixtureId} not found in local season."
            };
        }

        var players = await _store.GetPlayersAsync(seasonId, ct).ConfigureAwait(false);
        var byId    = players.Where(p => p.Id != Guid.Empty).ToDictionary(p => p.Id);
        var byKey   = players
            .GroupBy(p => PlayerKey(p.FullName, p.TeamId))
            .ToDictionary(g => g.Key, g => g.First());

        int created = 0;
        Guid? ResolveOrCreate(Guid? id, string? name, Guid teamId)
        {
            if (id.HasValue && byId.ContainsKey(id.Value)) return id;
            if (string.IsNullOrWhiteSpace(name)) return null;

            var key = PlayerKey(name, teamId);
            if (byKey.TryGetValue(key, out var existing)) return existing.Id;

            var split = SplitName(name);
            var p = new Player
            {
                Id        = Guid.NewGuid(),
                SeasonId  = seasonId,
                TeamId    = teamId,
                FirstName = split.first,
                LastName  = split.last,
                IsActive  = true,
                Notes     = "Created from captain submission",
            };
            players.Add(p);
            byId[p.Id]  = p;
            byKey[key]  = p;
            created++;
            return p.Id;
        }

        // Persist any newly-created players first.
        var newFrames = new List<FrameResult>();
        foreach (var f in payload.Frames.OrderBy(x => x.Number))
        {
            var homeId  = ResolveOrCreate(f.HomePlayerId,  f.HomePlayerName,  fixture.HomeTeamId);
            var awayId  = ResolveOrCreate(f.AwayPlayerId,  f.AwayPlayerName,  fixture.AwayTeamId);
            var home2Id = f.HomePlayer2Id;
            var away2Id = f.AwayPlayer2Id;

            newFrames.Add(new FrameResult
            {
                Number        = f.Number,
                HomePlayerId  = homeId,
                AwayPlayerId  = awayId,
                HomePlayer2Id = home2Id,
                AwayPlayer2Id = away2Id,
                Winner        = ParseWinner(f.Winner),
                EightBall     = f.EightBall,
                IsDoubles     = f.IsDoubles,
            });
        }

        // Save any created players.
        if (created > 0)
        {
            foreach (var p in players.Where(p => string.Equals(p.Notes, "Created from captain submission", StringComparison.Ordinal)))
            {
                // Only save the ones we just added (they won't exist in the store yet).
                try { await _store.AddPlayerAsync(p, ct).ConfigureAwait(false); } catch { /* ignore duplicates */ }
            }
        }

        fixture.Frames = newFrames;
        fixture.ModifiedDate = DateTime.UtcNow;
        await _store.UpdateFixtureAsync(fixture, ct).ConfigureAwait(false);
        await _store.SaveAsync(ct).ConfigureAwait(false);

        return new MatchImportResult
        {
            Success = true,
            FramesWritten = newFrames.Count,
            PlayersCreated = created,
            Message = $"Imported {newFrames.Count} frame(s)" + (created > 0 ? $", created {created} player(s)." : "."),
        };
    }

    private static FrameWinner ParseWinner(string? w) =>
        (w?.ToLowerInvariant()) switch
        {
            "home" => FrameWinner.Home,
            "away" => FrameWinner.Away,
            _       => FrameWinner.None,
        };

    private static string PlayerKey(string name, Guid? teamId) =>
        (teamId?.ToString() ?? "") + "|" + (name ?? "").Trim().ToLowerInvariant();

    private static (string first, string last) SplitName(string fullName)
    {
        var parts = (fullName ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return ("", "");
        if (parts.Length == 1) return (parts[0], "");
        return (parts[0], parts[1]);
    }
}
