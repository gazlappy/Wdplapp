using System.Text.Json;
using Wdpl2.Domain.Fixtures;
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
    /// <param name="fixtureId">
    /// The league fixture, or the cup tie - both live in the same table on the
    /// website. Whether the card is filled in the league's way or the cup's is
    /// the website's own reading of that row, not something said here, so a cup
    /// tie cannot be opened as a league card by mistake.
    /// </param>
    public static async Task<ScorecardState> OpenAsync(
        WebApiClient client, Guid fixtureId, MatchFormat format)
    {
        // The format comes from Settings and nowhere else, so a card always has
        // the number of frames the league actually plays. Doubles are marked on
        // the card by the captains, not decided here.
        var result = await client.AdminAsync("scorecards", "open", new
        {
            fixtureId,
            framesTotal = format.TotalFrames,
            maxPerPlayer = format.MaxFramesPerPlayer,
            doublesFrames = Array.Empty<int>(),
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

    /// <summary>What a card held when it was abandoned.</summary>
    public sealed record ClosedCard(int FramesPlayed, int FramesTotal, int HomeScore, int AwayScore)
    {
        /// <summary>True when scoring had started, so closing loses real work.</summary>
        public bool HadScoring => FramesPlayed > 0;
    }

    /// <summary>
    /// Abandons a card and takes the fixture back.
    /// </summary>
    /// <remarks>
    /// The way out of a card opened by mistake: it cannot be reopened, cannot
    /// be collected before it is finished, and blocks opening another. Whatever
    /// the captains had entered is discarded, so the caller should confirm
    /// first - the returned figures are there to make that warning specific.
    /// </remarks>
    public static async Task<ClosedCard> CloseAsync(WebApiClient client, Guid fixtureId)
    {
        var result = await client.AdminAsync("scorecards", "close", new { fixtureId });

        return new ClosedCard(
            Int(result, "frames_played"),
            Int(result, "frames_total"),
            Int(result, "home_score"),
            Int(result, "away_score"));
    }

    public sealed class ClaimedFrame
    {
        public int Number { get; init; }
        public FrameWinner Winner { get; init; }
        public bool EightBall { get; init; }
        public bool IsDoubles { get; init; }
        public Guid? HomePlayerId { get; init; }
        public Guid? AwayPlayerId { get; init; }
        public Guid? HomePlayer2Id { get; init; }
        public Guid? AwayPlayer2Id { get; init; }

        /// <summary>
        /// Names for picks made without an id - someone typed in on the night
        /// who is not on the roster. Kept so the secretary sees who actually
        /// played rather than an empty slot.
        /// </summary>
        public string? HomePlayerName { get; init; }
        public string? AwayPlayerName { get; init; }
        public string? HomePlayer2Name { get; init; }
        public string? AwayPlayer2Name { get; init; }

        /// <summary>A pick with a name but no id needs a player creating.</summary>
        public bool HasUnregisteredPlayer =>
            (HomePlayerId is null && !string.IsNullOrWhiteSpace(HomePlayerName))
            || (AwayPlayerId is null && !string.IsNullOrWhiteSpace(AwayPlayerName));
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
                IsDoubles = Int(frame, "is_doubles") == 1,
                HomePlayerId = OptionalGuid(frame, "home_player_id"),
                AwayPlayerId = OptionalGuid(frame, "away_player_id"),
                HomePlayer2Id = OptionalGuid(frame, "home_player2_id"),
                AwayPlayer2Id = OptionalGuid(frame, "away_player2_id"),
                HomePlayerName = Text(frame, "home_player_name"),
                AwayPlayerName = Text(frame, "away_player_name"),
                HomePlayer2Name = Text(frame, "home_player2_name"),
                AwayPlayer2Name = Text(frame, "away_player2_name"),
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

        // For a cup tie these two are the coin's answer, not the draw's: the
        // website puts the toss winner in the home column and leaves it there.
        HomeTeamId = OptionalGuid(row, "home_team_id"),
        AwayTeamId = OptionalGuid(row, "away_team_id"),
        IsCup = row.TryGetProperty("is_cup", out var cup)
                && (cup.ValueKind == JsonValueKind.True || Text(row, "is_cup") == "1"),
        DecidedBy = Text(row, "decided_by") switch
        {
            "home" => FrameWinner.Home,
            "away" => FrameWinner.Away,
            _ => FrameWinner.None,
        },
        HomeScore = Int(row, "home_score"),
        AwayScore = Int(row, "away_score"),
        FramesPlayed = Int(row, "frames_played"),
        FramesTotal = Int(row, "frames_total"),
        MatchDate = DateTime.TryParse(Text(row, "match_date"), out var d) ? d : null,
        HomeSigned = Text(row, "home_finalised_at") is not null,
        AwaySigned = Text(row, "away_finalised_at") is not null,
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
