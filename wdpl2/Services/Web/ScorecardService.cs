using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services.Web;

/// <summary>
/// The app's side of the scorecard handoff.
/// </summary>
/// <remarks>
/// Ownership moves only through these calls - open hands a fixture to the
/// website, claim takes it back. See <c>wdpl2/Docs/WebPlatform.md</c>.
/// </remarks>
public sealed class ScorecardService
{
    /// <summary>Every card the website knows about, and who owns each one.</summary>
    public static async Task<List<ScorecardState>> GetStatesAsync(WebApiClient client)
    {
        var rows = await client.AdminAsync("scorecards", "state");
        var states = new List<ScorecardState>();

        if (rows.ValueKind != JsonValueKind.Array) return states;

        foreach (var row in rows.EnumerateArray())
            states.Add(ReadState(row));

        return states;
    }

    /// <summary>
    /// Hands a fixture to the website for live scoring.
    /// </summary>
    /// <remarks>
    /// The frame template comes from the app because the app decides how many
    /// frames a match has and which are doubles. Captains fill in players and
    /// winners; they never invent frames.
    /// </remarks>
    public static async Task<ScorecardState> OpenAsync(WebApiClient client, Fixture fixture, int framesTotal)
    {
        var frames = new List<object>();

        if (fixture.Frames.Count > 0)
        {
            foreach (var frame in fixture.Frames.OrderBy(f => f.Number))
                frames.Add(new { frameNo = frame.Number, isDoubles = frame.IsDoubles });
        }
        else
        {
            for (var i = 1; i <= Math.Max(1, framesTotal); i++)
                frames.Add(new { frameNo = i, isDoubles = false });
        }

        var result = await client.AdminAsync("scorecards", "open", new
        {
            fixtureId = fixture.Id,
            frames,
        });

        return ReadState(result);
    }

    /// <summary>
    /// Collects a finalised card and returns its frames for the app to apply.
    /// </summary>
    /// <remarks>
    /// Safe to retry: claiming an already-claimed card returns the same answer
    /// rather than failing, so a lost response cannot strand a card. That is
    /// the recovery gap the previous backend never closed.
    /// </remarks>
    public static async Task<(ScorecardState State, List<ClaimedFrame> Frames, bool AlreadyClaimed)> ClaimAsync(
        WebApiClient client, Guid fixtureId)
    {
        var result = await client.AdminAsync("scorecards", "claim", new { fixtureId });

        var already = result.TryGetProperty("alreadyClaimed", out var a) && a.ValueKind == JsonValueKind.True;
        return (ReadState(result), ReadFrames(result), already);
    }

    public static async Task<ScorecardState> ReopenAsync(WebApiClient client, Guid fixtureId)
    {
        var result = await client.AdminAsync("scorecards", "reopen", new { fixtureId });
        return ReadState(result);
    }

    public sealed class ClaimedFrame
    {
        public int Number { get; init; }
        public FrameWinner Winner { get; init; }
        public bool EightBall { get; init; }
        public Guid? HomePlayerId { get; init; }
        public Guid? AwayPlayerId { get; init; }
    }

    private static List<ClaimedFrame> ReadFrames(JsonElement card)
    {
        var frames = new List<ClaimedFrame>();
        if (!card.TryGetProperty("frames", out var list) || list.ValueKind != JsonValueKind.Array)
            return frames;

        foreach (var frame in list.EnumerateArray())
        {
            frames.Add(new ClaimedFrame
            {
                Number = Int(frame, "frame_no"),
                Winner = Text(frame, "winner") switch
                {
                    "home" => FrameWinner.Home,
                    "away" => FrameWinner.Away,
                    _ => FrameWinner.None,
                },
                EightBall = Int(frame, "eight_ball") == 1,
                HomePlayerId = OptionalGuid(frame, "home_player_id"),
                AwayPlayerId = OptionalGuid(frame, "away_player_id"),
            });
        }

        return frames;
    }

    private static ScorecardState ReadState(JsonElement row) => new()
    {
        FixtureId = OptionalGuid(row, "fixture_id") ?? Guid.Empty,
        Owner = ScorecardState.ParseOwner(Text(row, "state")),
        Version = Int(row, "version"),
        HomeTeam = Text(row, "home_team_name") ?? "",
        AwayTeam = Text(row, "away_team_name") ?? "",
        HomeScore = Int(row, "home_score"),
        AwayScore = Int(row, "away_score"),
        FramesPlayed = Int(row, "frames_played"),
        FramesTotal = Int(row, "frames_total"),
        MatchDate = DateTime.TryParse(Text(row, "match_date"), out var d) ? d : null,
    };

    // MySQL hands back numbers as strings through PDO, so accept either form.
    private static int Int(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value)) return 0;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String => int.TryParse(value.GetString(), out var n) ? n : 0,
            _ => 0,
        };
    }

    private static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static Guid? OptionalGuid(JsonElement row, string name) =>
        Guid.TryParse(Text(row, name), out var id) ? id : null;
}
