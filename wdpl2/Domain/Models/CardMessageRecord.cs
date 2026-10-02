namespace Wdpl2.Models;

/// <summary>
/// A note a captain wrote on a scorecard, kept in the app for good.
/// </summary>
/// <remarks>
/// The website holds a card's note only until the card is opened again, so
/// the app keeps its own copy: every message it has ever read, including each
/// version of a note a captain later changed.
/// </remarks>
public sealed class CardMessageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FixtureId { get; set; }

    /// <summary>The season the match belongs to, where it could be told.</summary>
    public Guid? SeasonId { get; set; }

    public string HomeTeam { get; set; } = "";
    public string AwayTeam { get; set; } = "";
    public bool IsCup { get; set; }
    public DateTime? MatchDate { get; set; }

    public string Text { get; set; } = "";

    /// <summary>When the captain wrote it, as the website recorded it (local time).</summary>
    public DateTime? WrittenAt { get; set; }

    /// <summary>When the app first saw it.</summary>
    public DateTime KeptAt { get; set; } = DateTime.Now;
}
