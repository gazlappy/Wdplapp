using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wdpl2.Services.Inbox;

/// <summary>
/// Payload of a <c>roster_change</c> submission logged by
/// <c>captain/roster.php</c> when a captain edits their team roster online.
/// </summary>
public sealed class RosterChangePayload
{
    /// <summary>"add" | "rename" | "retire" | "reactivate"</summary>
    [JsonPropertyName("action")]    public string? Action { get; set; }
    [JsonPropertyName("player_id")] public Guid? PlayerId { get; set; }
    [JsonPropertyName("full_name")] public string? FullName { get; set; }
    [JsonPropertyName("old_name")]  public string? OldName { get; set; }
    [JsonPropertyName("team_id")]   public Guid? TeamId { get; set; }
    [JsonPropertyName("team_name")] public string? TeamName { get; set; }
}

public interface IRosterChangeImporter
{
    /// <summary>
    /// Applies a captain's roster change (add / rename / retire / reactivate)
    /// to the local data store. Returns a short summary for the status bar.
    /// </summary>
    Task<RosterImportResult> ImportAsync(WebSubmission submission, CancellationToken ct = default);
}

public sealed class RosterImportResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
}

public sealed class RosterChangeImporter : IRosterChangeImporter
{
    private readonly IDataStore _store;
    private readonly ISeasonService _season;

    public RosterChangeImporter(IDataStore store, ISeasonService season)
    {
        _store = store;
        _season = season;
    }

    public async Task<RosterImportResult> ImportAsync(WebSubmission submission, CancellationToken ct = default)
    {
        RosterChangePayload? p;
        try
        {
            p = submission.Payload.Deserialize<RosterChangePayload>();
        }
        catch (Exception ex)
        {
            return new RosterImportResult { Success = false, Message = $"Bad payload: {ex.Message}" };
        }
        if (p is null || string.IsNullOrWhiteSpace(p.Action))
            return new RosterImportResult { Success = false, Message = "Empty roster payload." };

        var seasonId = ParseGuid(submission.SeasonId) ?? _season.CurrentSeasonId;
        var players  = await _store.GetPlayersAsync(seasonId, ct).ConfigureAwait(false);

        var action = p.Action.Trim().ToLowerInvariant();
        var name   = (p.FullName ?? "").Trim();

        // Locate by portal GUID first, then by (team, name).
        var existing = p.PlayerId.HasValue && p.PlayerId.Value != Guid.Empty
            ? players.FirstOrDefault(x => x.Id == p.PlayerId.Value)
            : null;
        existing ??= players.FirstOrDefault(x =>
            x.TeamId == p.TeamId &&
            string.Equals(x.FullName.Trim(), action == "rename" ? (p.OldName ?? "").Trim() : name,
                StringComparison.OrdinalIgnoreCase));

        switch (action)
        {
            case "add":
            case "reactivate":
                if (existing is not null)
                {
                    if (existing.IsActive)
                        return new RosterImportResult { Success = true, Message = $"{existing.FullName} already on the roster." };
                    existing.IsActive = true;
                    existing.DeactivatedDate = null;
                    existing.DeactivationReason = null;
                    existing.ModifiedDate = DateTime.UtcNow;
                    await _store.UpdatePlayerAsync(existing, ct).ConfigureAwait(false);
                    await _store.SaveAsync(ct).ConfigureAwait(false);
                    return new RosterImportResult { Success = true, Message = $"Reactivated {existing.FullName}." };
                }
                if (name == "")
                    return new RosterImportResult { Success = false, Message = "No player name in payload." };
                if (p.TeamId is null || p.TeamId == Guid.Empty)
                    return new RosterImportResult { Success = false, Message = "No team id in payload." };
                var split = SplitName(name);
                var np = new Player
                {
                    Id        = (p.PlayerId.HasValue && p.PlayerId.Value != Guid.Empty) ? p.PlayerId.Value : Guid.NewGuid(),
                    SeasonId  = seasonId,
                    TeamId    = p.TeamId,
                    FirstName = split.first,
                    LastName  = split.last,
                    IsActive  = true,
                    Notes     = "Added by captain via portal",
                };
                await _store.AddPlayerAsync(np, ct).ConfigureAwait(false);
                await _store.SaveAsync(ct).ConfigureAwait(false);
                return new RosterImportResult { Success = true, Message = $"Added {name} to {p.TeamName ?? "team"}." };

            case "rename":
                if (existing is null)
                    return new RosterImportResult { Success = false, Message = $"Player '{p.OldName ?? name}' not found locally." };
                if (name == "")
                    return new RosterImportResult { Success = false, Message = "No new name in payload." };
                var s2 = SplitName(name);
                existing.FirstName = s2.first;
                existing.LastName  = s2.last;
                existing.ModifiedDate = DateTime.UtcNow;
                await _store.UpdatePlayerAsync(existing, ct).ConfigureAwait(false);
                await _store.SaveAsync(ct).ConfigureAwait(false);
                return new RosterImportResult { Success = true, Message = $"Renamed {p.OldName} to {name}." };

            case "retire":
                if (existing is null)
                    return new RosterImportResult { Success = false, Message = $"Player '{name}' not found locally." };
                if (!existing.IsActive)
                    return new RosterImportResult { Success = true, Message = $"{existing.FullName} already retired." };
                existing.IsActive = false;
                existing.DeactivatedDate = DateTime.UtcNow;
                existing.DeactivationReason = "Retired by captain via portal";
                existing.ModifiedDate = DateTime.UtcNow;
                await _store.UpdatePlayerAsync(existing, ct).ConfigureAwait(false);
                await _store.SaveAsync(ct).ConfigureAwait(false);
                return new RosterImportResult { Success = true, Message = $"Retired {existing.FullName}." };

            default:
                return new RosterImportResult { Success = false, Message = $"Unknown roster action '{p.Action}'." };
        }
    }

    private static Guid? ParseGuid(string? s) =>
        Guid.TryParse(s, out var g) && g != Guid.Empty ? g : null;

    private static (string first, string last) SplitName(string fullName)
    {
        var parts = (fullName ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return ("", "");
        if (parts.Length == 1) return (parts[0], "");
        return (parts[0], parts[1]);
    }
}
