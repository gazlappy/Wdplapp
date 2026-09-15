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
    public int SchemaVersion => 3;

    public IReadOnlyList<string> ServerFiles { get; } = new[]
    {
        "api/modules/scorecards/Module.php",
        "api/modules/scorecards/Rules.php",
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
