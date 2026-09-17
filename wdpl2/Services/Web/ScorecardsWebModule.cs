using Wdpl2.Models;

namespace Wdpl2.Services.Web;

/// <summary>
/// Live scorecards: hands a fixture to the website for captains to score, then
/// collects the finished card back.
/// </summary>
public sealed class ScorecardsWebModule : IWebModule
{
    public string Id => "scorecards";
    public string Title => "Live scorecards";
    public string Description => "Let captains score a match live, then collect the finished card.";
    public string Icon => "\U0001F3B1"; // billiards
    public int SchemaVersion => 4;

    public IReadOnlyList<string> ServerFiles { get; } = new[]
    {
        "api/modules/scorecards/Module.php",
        "api/modules/scorecards/Rules.php",
        "api/modules/scorecards/CupRules.php",
        // The captain's scoring page. Served from the site root, same origin as
        // the API, so its session cookie and fetch calls need no CORS handling.
        "captain/index.html",
    };
}

/// <summary>Where a scorecard lives right now.</summary>
public enum CardOwner
{
    /// <summary>No card on the website. The app owns this fixture outright.</summary>
    Desktop,

    /// <summary>The website owns it. Captains are scoring; the app must not edit it.</summary>
    Live,

    /// <summary>Scoring has ended. Nobody writes until the app collects it.</summary>
    Finalised,

    /// <summary>Collected. The app owns it again and the server copy is frozen.</summary>
    Claimed,
}

public sealed class ScorecardState
{
    public Guid FixtureId { get; init; }
    public CardOwner Owner { get; init; }
    public int Version { get; init; }
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";
    public int HomeScore { get; init; }
    public int AwayScore { get; init; }
    public int FramesPlayed { get; init; }
    public int FramesTotal { get; init; }
    public DateTime? MatchDate { get; init; }

    /// <summary>
    /// The two teams as the card has them.
    /// </summary>
    /// <remarks>
    /// For a league night this is the fixture's own home and away. For a cup
    /// tie it is the coin's: the website puts the toss winner in the home
    /// column, so the scoreline can be read straight off without translating
    /// it back through the draw.
    /// </remarks>
    public Guid? HomeTeamId { get; init; }
    public Guid? AwayTeamId { get; init; }

    /// <summary>True when this is a cup tie rather than a league night.</summary>
    public bool IsCup { get; init; }

    /// <summary>
    /// The side that has already won a cup tie, if one has.
    /// </summary>
    /// <remarks>
    /// A cup tie is the first to eight of fifteen, so it can be finished with
    /// frames still unplayed - and often is, because the rest decide nothing.
    /// </remarks>
    public FrameWinner DecidedBy { get; init; }

    /// <summary>
    /// Whether each captain has signed the card off. Both must sign before the
    /// card can be collected, which is the online equivalent of both captains
    /// signing the paper card.
    /// </summary>
    public bool HomeSigned { get; init; }
    public bool AwaySigned { get; init; }

    /// <summary>
    /// True when the app must not edit this fixture's frames, because the
    /// website owns them. This is the single-writer rule in code.
    /// </summary>
    public bool IsOwnedByWebsite => Owner is CardOwner.Live or CardOwner.Finalised;

    /// <summary>The winning team's id, once a cup tie is decided.</summary>
    public Guid? WinnerTeamId => DecidedBy switch
    {
        FrameWinner.Home => HomeTeamId,
        FrameWinner.Away => AwayTeamId,
        _ => null,
    };

    public string Describe() => Owner switch
    {
        CardOwner.Live when HomeSigned || AwaySigned =>
            $"Live — {HomeScore}–{AwayScore}, waiting for the "
            + (HomeSigned ? "away" : "home") + " captain to sign off",
        CardOwner.Live => $"Live — {HomeScore}–{AwayScore} after {FramesPlayed} of {FramesTotal}",
        CardOwner.Finalised => $"Both captains signed {HomeScore}–{AwayScore}, waiting to be collected",
        CardOwner.Claimed => $"Collected — {HomeScore}–{AwayScore}",
        _ => "Not open online",
    };

    public static CardOwner ParseOwner(string? state) => state switch
    {
        "live" => CardOwner.Live,
        "finalised" => CardOwner.Finalised,
        "claimed" => CardOwner.Claimed,
        _ => CardOwner.Desktop,
    };
}
