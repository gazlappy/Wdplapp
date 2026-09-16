using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services.Web;

/// <summary>
/// Publishes the parts of a competition that get run at a venue, and collects
/// the results back.
/// </summary>
/// <remarks>
/// A competition night happens in several pubs at once, each group organised by
/// one of its own players. What gets published is a <em>session</em>: a group of
/// a group stage, or a round of a knockout, with its players, its matches and a
/// PIN that opens that session alone.
/// <para>
/// The app stays the record of the competition. A session is a place for results
/// to be entered while the night is on; collecting brings them back here, and
/// from then on the website copy is frozen.
/// </para>
/// </remarks>
public sealed class CompetitionNightService
{
    /// <summary>Long enough that guessing it is hopeless against the rate limit.</summary>
    public const int PinLength = 5;

    /// <summary>A part of a competition that somebody runs on the night.</summary>
    public sealed record Session(
        Guid Id,
        string Kind,
        Guid RefId,
        string Name,
        Guid? OrganiserId,
        string? OrganiserName,
        string? VenueName,
        string? TableLabel,
        int BestOf,
        int FramesToWin,
        IReadOnlyList<Guid> ParticipantIds,
        IReadOnlyList<CompetitionMatch> Matches,
        string? Pin);

    /// <summary>What the website holds for one session.</summary>
    public sealed record SessionState(
        Guid Id, string Competition, string Name, string Kind, Guid RefId,
        string? VenueName, string? OrganiserName, string State,
        int Matches, int Played, int Present, bool Collected);

    /// <summary>A result the runner entered, ready to go back into the app.</summary>
    public sealed record CollectedMatch(Guid MatchId, int P1Score, int P2Score, Guid? WinnerId, bool IsComplete);

    // --------------------------------------------------------------- building

    /// <summary>
    /// The sessions a competition currently has to run.
    /// </summary>
    /// <remarks>
    /// Group stages give one session per group. Knockouts give one per round
    /// that still has something to play - a round already decided is nobody's
    /// night to run.
    /// </remarks>
    public static List<Session> Build(Competition competition, IReadOnlyDictionary<Guid, string> names)
    {
        var sessions = new List<Session>();

        foreach (var group in competition.Groups)
        {
            sessions.Add(new Session(
                Id: Stable(competition.Id, group.Id),
                Kind: "group",
                RefId: group.Id,
                Name: string.IsNullOrWhiteSpace(group.Name) ? $"Group {group.GroupNumber}" : group.Name,
                OrganiserId: group.OrganiserParticipantId,
                OrganiserName: Name(names, group.OrganiserParticipantId),
                VenueName: group.VenueName,
                TableLabel: group.TableLabel,
                BestOf: competition.BestOf,
                FramesToWin: competition.FramesToWin,
                ParticipantIds: group.ParticipantIds,
                Matches: group.Matches,
                Pin: group.RunnerPin));
        }

        foreach (var round in competition.Rounds)
        {
            // A group stage's rounds are covered by the groups themselves.
            if (round.IsGroupStage || round.GroupId.HasValue) continue;
            if (round.Matches.All(m => m.IsComplete)) continue;

            var players = round.Matches
                .SelectMany(m => new[] { m.Participant1Id, m.Participant2Id })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (players.Count == 0) continue;

            sessions.Add(new Session(
                Id: Stable(competition.Id, round.Id),
                Kind: "round",
                RefId: round.Id,
                Name: string.IsNullOrWhiteSpace(round.Name) ? $"Round {round.RoundNumber}" : round.Name,
                OrganiserId: round.OrganiserParticipantId,
                OrganiserName: Name(names, round.OrganiserParticipantId),
                VenueName: round.Matches.Select(m => m.VenueName).FirstOrDefault(v => !string.IsNullOrEmpty(v)),
                TableLabel: null,
                BestOf: round.GetEffectiveBestOf(competition),
                FramesToWin: round.GetFramesToWin(competition),
                ParticipantIds: players,
                Matches: round.Matches,
                Pin: round.RunnerPin));
        }

        return sessions;
    }

    /// <summary>
    /// The payload for <c>comps/push</c>, and what was left out.
    /// </summary>
    /// <remarks>
    /// A session with no PIN is not published at all. Publishing it would put a
    /// group of players' names on the website that nobody could open, and imply
    /// the night is ready to run when it is not.
    /// </remarks>
    public static (object Payload, List<string> Skipped) BuildPayload(
        Competition competition, IReadOnlyList<Session> sessions, IReadOnlyDictionary<Guid, string> names)
    {
        var skipped = new List<string>();
        var published = new List<object>();

        foreach (var session in sessions)
        {
            if (string.IsNullOrWhiteSpace(session.Pin))
            {
                skipped.Add(session.Name);
                continue;
            }

            published.Add(new
            {
                id = session.Id,
                kind = session.Kind,
                refId = session.RefId,
                name = session.Name,
                organiserName = session.OrganiserName,
                venueName = session.VenueName,
                tableLabel = session.TableLabel,
                bestOf = session.BestOf,
                framesToWin = session.FramesToWin,
                allowOrder = true,
                pinHash = PasswordHash.Create(session.Pin!.Trim()),
                players = session.ParticipantIds
                    .Select(id => new { id, name = Name(names, id) ?? "(unnamed)" })
                    .ToList(),
                matches = session.Matches.Select(m => new
                {
                    id = m.Id,
                    p1Id = m.Participant1Id,
                    p2Id = m.Participant2Id,
                }).ToList(),
            });
        }

        var payload = new
        {
            competitionId = competition.Id,
            seasonId = competition.SeasonId,
            competition = competition.Name,
            sessions = published,
        };

        return (payload, skipped);
    }

    // -------------------------------------------------------------- the wire

    public static async Task<int> PushAsync(
        WebApiClient client, Competition competition, IReadOnlyList<Session> sessions,
        IReadOnlyDictionary<Guid, string> names)
    {
        var (payload, _) = BuildPayload(competition, sessions, names);
        var result = await client.AdminAsync("comps", "push", payload);

        return result.TryGetProperty("sessions", out var n) && n.ValueKind == JsonValueKind.Number
            ? n.GetInt32() : 0;
    }

    /// <summary>What the website currently holds, for every competition.</summary>
    public static async Task<List<SessionState>> StateAsync(WebApiClient client)
    {
        var rows = await client.AdminAsync("comps", "state");
        var states = new List<SessionState>();

        if (rows.ValueKind != JsonValueKind.Array) return states;

        foreach (var row in rows.EnumerateArray())
        {
            var id = Guid(row, "id");
            var refId = Guid(row, "ref_id");
            if (id is null || refId is null) continue;

            states.Add(new SessionState(
                id.Value,
                Text(row, "competition") ?? "",
                Text(row, "name") ?? "",
                Text(row, "kind") ?? "group",
                refId.Value,
                Text(row, "venue_name"),
                Text(row, "organiser_name"),
                Text(row, "state") ?? "open",
                Int(row, "matches"),
                Int(row, "played"),
                Int(row, "present"),
                Text(row, "collected_at") is not null));
        }

        return states;
    }

    /// <summary>
    /// Takes one session's results back and freezes the website copy.
    /// </summary>
    /// <remarks>
    /// Safe to repeat. Collecting again returns the same results and writes the
    /// same values, so a lost reply costs nothing but a second press.
    /// </remarks>
    public static async Task<List<CollectedMatch>> CollectAsync(WebApiClient client, Guid sessionId)
    {
        var result = await client.AdminAsync("comps", "collect", new { sessionId });
        var collected = new List<CollectedMatch>();

        if (!result.TryGetProperty("matches", out var matches) || matches.ValueKind != JsonValueKind.Array)
            return collected;

        foreach (var row in matches.EnumerateArray())
        {
            var matchId = Guid(row, "match_id");
            if (matchId is null) continue;

            collected.Add(new CollectedMatch(
                matchId.Value,
                Int(row, "p1_score"),
                Int(row, "p2_score"),
                Guid(row, "winner_id"),
                Int(row, "is_complete") == 1));
        }

        return collected;
    }

    /// <summary>Writes collected results onto the competition's own matches.</summary>
    public static int Apply(Competition competition, IReadOnlyList<CollectedMatch> results)
    {
        var byId = new Dictionary<Guid, CompetitionMatch>();

        foreach (var match in competition.Groups.SelectMany(g => g.Matches)) byId[match.Id] = match;
        foreach (var match in competition.Rounds.SelectMany(r => r.Matches)) byId[match.Id] = match;

        var applied = 0;

        foreach (var result in results)
        {
            if (!result.IsComplete) continue;
            if (!byId.TryGetValue(result.MatchId, out var match)) continue;

            match.Participant1Score = result.P1Score;
            match.Participant2Score = result.P2Score;
            match.WinnerId = result.WinnerId;
            match.IsComplete = true;
            applied++;
        }

        return applied;
    }

    // ------------------------------------------------------------------ PINs

    /// <summary>
    /// A PIN for one session, avoiding any already in use in this competition.
    /// </summary>
    /// <remarks>
    /// Digits only: it gets read out across a noisy pub, and often typed by
    /// someone who did not write it down.
    /// </remarks>
    public static string NewPin(IEnumerable<string?> taken)
    {
        var used = taken.Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(p => p!.Trim())
                        .ToHashSet(StringComparer.Ordinal);

        for (var attempt = 0; attempt < 200; attempt++)
        {
            var pin = string.Concat(Enumerable.Range(0, PinLength)
                .Select(_ => (char)('0' + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10))));

            if (!used.Contains(pin)) return pin;
        }

        // 200 collisions in a five-digit space means something is very wrong;
        // a duplicate is still usable, since a PIN only opens its own session.
        return string.Concat(Enumerable.Range(0, PinLength)
            .Select(_ => (char)('0' + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10))));
    }

    /// <summary>
    /// The session id for a group or round, derived rather than stored.
    /// </summary>
    /// <remarks>
    /// The same group must publish to the same row every time, or each push
    /// would orphan the results entered against the last one. Deriving it from
    /// the competition and the group means no extra field to keep in step.
    /// </remarks>
    public static Guid Stable(Guid competitionId, Guid refId)
    {
        Span<byte> bytes = stackalloc byte[32];
        competitionId.TryWriteBytes(bytes[..16]);
        refId.TryWriteBytes(bytes[16..]);

        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return new Guid(hash[..16]);
    }

    private static string? Name(IReadOnlyDictionary<Guid, string> names, Guid? id) =>
        id.HasValue && names.TryGetValue(id.Value, out var name) ? name : null;

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
