using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services.Inbox;

/// <summary>Maps only explicit league identities; never creates or merges players.</summary>
public static class AdminScorecardMapper
{
    public static JsonElement CreateLocalFrames(LeagueData league, AdminSyncReviewItem item)
    {
        if (item.LocalSnapshot is not { } expected || !Guid.TryParse(item.Change.Id, out var id))
            throw new InvalidOperationException("Review the local fixture first.");
        var fixture = league.Fixtures.SingleOrDefault(f => f.Id == id)
            ?? throw new InvalidOperationException("The local fixture is missing.");
        var frames = JsonSerializer.SerializeToElement(fixture.Frames.Select(f => new
        {
            number = f.Number, is_doubles = f.IsDoubles, eight_ball = f.EightBall,
            winner = f.Winner switch { FrameWinner.Home => "home", FrameWinner.Away => "away", FrameWinner.None => "none", _ => throw new InvalidOperationException("Invalid winner.") },
            home_player_id = f.HomePlayerId, home_player2_id = f.HomePlayer2Id,
            away_player_id = f.AwayPlayerId, away_player2_id = f.AwayPlayer2Id
        }));
        var payload = JsonSerializer.SerializeToElement(new { state = new
        {
            home_team_id = fixture.HomeTeamId, away_team_id = fixture.AwayTeamId, frames
        } });
        CreateReviewedDraft(league, item.Change with { Payload = payload }, expected);
        return frames;
    }

    public static JsonElement Capture(LeagueData league, Guid fixtureId)
    {
        var fixture = league.Fixtures.SingleOrDefault(f => f.Id == fixtureId)
            ?? throw new InvalidOperationException("The fixture is not present locally.");
        return JsonSerializer.SerializeToElement(fixture);
    }

    public static Fixture CreateReviewedDraft(LeagueData league, AdminSyncChange change, JsonElement expectedLocal)
    {
        if (change.Kind != "scorecard" || !Guid.TryParse(change.Id, out var fixtureId) ||
            !Guid.TryParse(change.SeasonId, out var seasonId))
            throw new InvalidOperationException("A scorecard must identify its fixture and season explicitly.");
        var fixture = league.Fixtures.SingleOrDefault(f => f.Id == fixtureId && f.SeasonId == seasonId)
            ?? throw new InvalidOperationException("The server fixture does not match a local season fixture.");
        var season = league.Seasons.SingleOrDefault(s => s.Id == seasonId)
            ?? throw new InvalidOperationException("The scorecard season is missing locally.");
        if (season.IsLocked) throw new InvalidOperationException("This season is locked. No scorecard changes can be applied.");
        if (!JsonElement.DeepEquals(Capture(league, fixtureId), expectedLocal))
            throw new InvalidOperationException("The local fixture changed after review. Compare the latest values again.");
        var payload = change.Payload;
        var state = payload.GetProperty("state");
        if (!Guid.TryParse(state.GetProperty("home_team_id").GetString(), out var home) || home != fixture.HomeTeamId ||
            !Guid.TryParse(state.GetProperty("away_team_id").GetString(), out var away) || away != fixture.AwayTeamId)
            throw new InvalidOperationException("The scorecard teams do not match the local fixture.");
        if (!league.Teams.Any(t => t.Id == home && t.SeasonId == seasonId) ||
            !league.Teams.Any(t => t.Id == away && t.SeasonId == seasonId))
            throw new InvalidOperationException("Both teams must exist in the identified season.");
        var frames = state.GetProperty("frames");
        if (frames.ValueKind != JsonValueKind.Array || frames.GetArrayLength() > 100)
            throw new InvalidOperationException("Invalid scorecard frame collection.");
        var draft = JsonSerializer.Deserialize<Fixture>(expectedLocal.GetRawText())!;
        var replacement = new List<FrameResult>();
        var numbers = new HashSet<int>();
        foreach (var frame in frames.EnumerateArray())
        {
            var number = frame.GetProperty("number").GetInt32();
            if (number < 1 || number > 100 || !numbers.Add(number))
                throw new InvalidOperationException("Frame numbers must be unique and between 1 and 100.");
            var doubles = frame.GetProperty("is_doubles").GetBoolean();
            Guid? Player(string key, Guid team)
            {
                if (!frame.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return null;
                if (!Guid.TryParse(value.GetString(), out var id)) throw new InvalidOperationException("Invalid player identity.");
                if (FrameResult.IsVoidPlayer(id)) return id;
                if (!league.Players.Any(p => p.Id == id && p.SeasonId == seasonId && p.TeamId == team))
                    throw new InvalidOperationException("A scorecard player is not on the identified local season team. Review the roster first.");
                return id;
            }
            var winner = frame.GetProperty("winner").GetString() switch
            {
                "home" => FrameWinner.Home, "away" => FrameWinner.Away,
                null or "none" => FrameWinner.None,
                _ => throw new InvalidOperationException("Invalid frame winner.")
            };
            var mapped = new FrameResult
            {
                Number = number, IsDoubles = doubles, Winner = winner,
                EightBall = frame.GetProperty("eight_ball").GetBoolean(),
                HomePlayerId = Player("home_player_id", home), AwayPlayerId = Player("away_player_id", away),
                HomePlayer2Id = Player("home_player2_id", home), AwayPlayer2Id = Player("away_player2_id", away)
            };
            if ((!doubles && (mapped.HomePlayer2Id != null || mapped.AwayPlayer2Id != null)) ||
                (mapped.HomePlayerId != null && !FrameResult.IsVoidPlayer(mapped.HomePlayerId) && mapped.HomePlayerId == mapped.HomePlayer2Id) ||
                (mapped.AwayPlayerId != null && !FrameResult.IsVoidPlayer(mapped.AwayPlayerId) && mapped.AwayPlayerId == mapped.AwayPlayer2Id) ||
                (mapped.EightBall && winner == FrameWinner.None) ||
                (winner != FrameWinner.None && (mapped.HomePlayerId == null || mapped.AwayPlayerId == null ||
                    (doubles && (mapped.HomePlayer2Id == null || mapped.AwayPlayer2Id == null)))))
                throw new InvalidOperationException("Frame participants or eight-ball state are incomplete or inconsistent.");
            replacement.Add(mapped);
        }
        draft.Frames = replacement.OrderBy(f => f.Number).ToList();
        draft.ModifiedDate = DateTime.UtcNow;
        return draft;
    }
}
