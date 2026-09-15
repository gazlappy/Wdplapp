using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services.Web;

/// <summary>
/// Brings players that captains added on the website into the app.
/// </summary>
/// <remarks>
/// Captains can add someone who turns up on the night. Those players live on
/// the website until the app takes them in; publishing deliberately leaves them
/// alone until then, so nothing is lost while the secretary is not looking.
/// <para>
/// This is the same hand-off the scorecards use: read, apply locally, then tell
/// the server it was done. Confirming only after the local save means a failure
/// leaves the player waiting to be collected again rather than disappearing
/// from both sides.
/// </para>
/// </remarks>
public sealed class CaptainRosterService
{
    public sealed record AddedPlayer(Guid Id, string Name, Guid TeamId, Guid SeasonId, string TeamName, bool IsActive);

    /// <summary>Players captains have added that the app has not taken in yet.</summary>
    public static async Task<List<AddedPlayer>> GetUncollectedAsync(WebApiClient client)
    {
        var rows = await client.AdminAsync("captains", "uncollected");
        var players = new List<AddedPlayer>();

        if (rows.ValueKind != JsonValueKind.Array) return players;

        foreach (var row in rows.EnumerateArray())
        {
            var id = Guid(row, "id");
            var teamId = Guid(row, "team_id");
            var seasonId = Guid(row, "season_id");

            // A player with no team or season cannot be placed, so leave it for
            // a human rather than guessing where it belongs.
            if (id is null || teamId is null || seasonId is null) continue;

            players.Add(new AddedPlayer(
                id.Value,
                Text(row, "name") ?? "(unnamed)",
                teamId.Value,
                seasonId.Value,
                Text(row, "team_name") ?? "(unknown team)",
                Int(row, "is_active") == 1));
        }

        return players;
    }

    /// <summary>
    /// Creates the players locally and confirms them to the website.
    /// </summary>
    /// <remarks>
    /// The website's id is reused deliberately. Scorecard frames already point
    /// at it, so a fresh id would orphan every frame that player appeared in.
    /// </remarks>
    public static async Task<int> CollectAsync(WebApiClient client, LeagueData league, List<AddedPlayer> players)
    {
        if (players.Count == 0) return 0;

        var added = 0;
        var confirmed = new List<Guid>();

        foreach (var incoming in players)
        {
            var existing = league.Players.FirstOrDefault(p => p.Id == incoming.Id);

            if (existing is null)
            {
                league.Players.Add(new Player
                {
                    Id = incoming.Id,
                    SeasonId = incoming.SeasonId,
                    TeamId = incoming.TeamId,
                    FirstName = FirstNameOf(incoming.Name),
                    LastName = LastNameOf(incoming.Name),
                    IsActive = incoming.IsActive,
                    Notes = "Added by the team captain online.",
                    CreatedDate = DateTime.UtcNow,
                });
                added++;
            }
            else
            {
                // Already here from a previous run that did not get as far as
                // confirming. Bring it up to date and confirm it this time.
                existing.TeamId = incoming.TeamId;
                existing.IsActive = incoming.IsActive;
                existing.ModifiedDate = DateTime.UtcNow;
            }

            confirmed.Add(incoming.Id);
        }

        DataStore.Save();

        // Only now that it is safely saved locally.
        await client.AdminAsync("captains", "markCollected", new { playerIds = confirmed });

        return added;
    }

    /// <summary>
    /// Splits a typed name into the app's first/last fields.
    /// </summary>
    /// <remarks>
    /// Captains type a whole name into one box. Everything before the last
    /// space is the forename, which handles "Dave Smith" and "Mary Jane Smith"
    /// and leaves a single word as a forename with no surname.
    /// </remarks>
    private static string FirstNameOf(string full)
    {
        var trimmed = full.Trim();
        var split = trimmed.LastIndexOf(' ');
        return split <= 0 ? trimmed : trimmed[..split].Trim();
    }

    private static string LastNameOf(string full)
    {
        var trimmed = full.Trim();
        var split = trimmed.LastIndexOf(' ');
        return split <= 0 ? "" : trimmed[(split + 1)..].Trim();
    }

    private static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static Guid? Guid(JsonElement row, string name) =>
        System.Guid.TryParse(Text(row, name), out var id) ? id : null;

    private static int Int(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var v)) return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetInt32(),
            JsonValueKind.String => int.TryParse(v.GetString(), out var n) ? n : 0,
            _ => 0,
        };
    }
}
