using System.Text.RegularExpressions;
using Wdpl2.Models;
using static Wdpl2.Services.Web.ScorecardService;

namespace Wdpl2.Services.Web;

/// <summary>
/// The app's own copy of every captain's note it has read off the website.
/// </summary>
/// <remarks>
/// The website forgets a card's note when the card is opened again, and a
/// captain can rewrite a note at any time. So each distinct note on a card is
/// kept as its own message, and nothing is ever taken out: a rewritten note
/// adds a message, it does not replace one.
/// </remarks>
public static class MessageArchive
{
    /// <summary>
    /// Adds whatever of <paramref name="incoming"/> the archive does not hold.
    /// </summary>
    /// <returns>How many messages were added; the caller saves when it is not 0.</returns>
    public static int Keep(LeagueData league, IEnumerable<CardMessage> incoming)
    {
        var added = 0;

        foreach (var message in incoming)
        {
            var text = message.Text.Trim();
            if (text.Length == 0 || Holds(league, message.FixtureId, text)) continue;

            league.CardMessages.Add(new CardMessageRecord
            {
                FixtureId = message.FixtureId,
                SeasonId = SeasonOf(league, message.FixtureId, message.MatchDate),
                HomeTeam = message.HomeTeam,
                AwayTeam = message.AwayTeam,
                IsCup = message.IsCup,
                MatchDate = message.MatchDate,
                Text = text,
                WrittenAt = message.WrittenAt,
            });
            added++;
        }

        return added;
    }

    /// <summary>
    /// Keeps the note on a card being collected, in case it was never read in
    /// Messages before the card left the website's hands.
    /// </summary>
    public static int Keep(LeagueData league, ScorecardState card) =>
        Keep(league, new[]
        {
            new CardMessage(card.FixtureId, card.HomeTeam, card.AwayTeam, card.IsCup,
                card.MatchDate, WithoutStamp(card.Notes ?? ""), null, Unread: false),
        });

    /// <summary>
    /// Every kept message, newest first: by when it was written where known,
    /// otherwise by when the app first saw it.
    /// </summary>
    public static IEnumerable<CardMessageRecord> Newest(LeagueData league) =>
        league.CardMessages.OrderByDescending(m => m.WrittenAt ?? m.KeptAt);

    /// <summary>A card's note without the line a solo submission adds (as the website strips it).</summary>
    public static string WithoutStamp(string notes) =>
        Regex.Replace(notes, @"^\[Submitted from one device by the (home|away) captain\.\]\s*$", "",
            RegexOptions.Multiline).Trim();

    private static bool Holds(LeagueData league, Guid fixtureId, string text) =>
        league.CardMessages.Any(m => m.FixtureId == fixtureId && m.Text == text);

    private static Guid? SeasonOf(LeagueData league, Guid fixtureId, DateTime? matchDate)
    {
        var fixture = league.Fixtures.FirstOrDefault(f => f.Id == fixtureId);
        if (fixture?.SeasonId is { } id) return id;

        // A cup tie is not a league fixture; its date places it.
        if (matchDate is not { } date) return null;
        return league.Seasons
            .FirstOrDefault(s => date.Date >= s.StartDate.Date && date.Date <= s.EndDate.Date)?.Id;
    }
}
