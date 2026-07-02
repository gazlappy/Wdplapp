using System.Text.Json.Serialization;

namespace Wdpl2.Services.Inbox;

/// <summary>
/// Strongly-typed shape of the JSON payload that captains POST to
/// <c>captain/submit-result.php</c> and that lands in the <c>submissions</c>
/// table as <c>type = "match_result"</c>.
/// </summary>
public sealed class MatchResultPayload
{
    [JsonPropertyName("fixture_id")]
    public Guid FixtureId { get; set; }

    [JsonPropertyName("season_id")]
    public Guid? SeasonId { get; set; }

    [JsonPropertyName("submitted_by")]
    public MatchResultSubmitter SubmittedBy { get; set; } = new();

    [JsonPropertyName("fixture_meta")]
    public MatchResultMeta FixtureMeta { get; set; } = new();

    [JsonPropertyName("frames")]
    public List<MatchResultFrame> Frames { get; set; } = new();

    [JsonPropertyName("new_players")]
    public List<MatchResultNewPlayer> NewPlayers { get; set; } = new();

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class MatchResultSubmitter
{
    [JsonPropertyName("team_id")]   public Guid TeamId { get; set; }
    [JsonPropertyName("team_name")] public string? TeamName { get; set; }
    [JsonPropertyName("side")]      public string? Side { get; set; } // "home" | "away"
    [JsonPropertyName("username")]  public string? Username { get; set; }
}

public sealed class MatchResultMeta
{
    [JsonPropertyName("home_team_id")]   public Guid HomeTeamId { get; set; }
    [JsonPropertyName("away_team_id")]   public Guid AwayTeamId { get; set; }
    [JsonPropertyName("home_team_name")] public string? HomeTeamName { get; set; }
    [JsonPropertyName("away_team_name")] public string? AwayTeamName { get; set; }
    [JsonPropertyName("fixture_date")]   public string? FixtureDate { get; set; }
}

public sealed class MatchResultFrame
{
    [JsonPropertyName("number")]            public int Number { get; set; }
    [JsonPropertyName("home_player_id")]    public Guid? HomePlayerId { get; set; }
    [JsonPropertyName("home_player_name")]  public string? HomePlayerName { get; set; }
    [JsonPropertyName("away_player_id")]    public Guid? AwayPlayerId { get; set; }
    [JsonPropertyName("away_player_name")]  public string? AwayPlayerName { get; set; }
    [JsonPropertyName("home_player2_id")]   public Guid? HomePlayer2Id { get; set; }
    [JsonPropertyName("home_player2_name")] public string? HomePlayer2Name { get; set; }
    [JsonPropertyName("away_player2_id")]   public Guid? AwayPlayer2Id { get; set; }
    [JsonPropertyName("away_player2_name")] public string? AwayPlayer2Name { get; set; }
    [JsonPropertyName("winner")]            public string? Winner { get; set; } // "home" | "away" | null
    [JsonPropertyName("eight_ball")]        public bool EightBall { get; set; }
    [JsonPropertyName("is_doubles")]        public bool IsDoubles { get; set; }
}

public sealed class MatchResultNewPlayer
{
    [JsonPropertyName("player_id")] public Guid? PlayerId { get; set; }
    [JsonPropertyName("name")]      public string? Name { get; set; }
    [JsonPropertyName("team_id")]   public Guid? TeamId { get; set; }
}
