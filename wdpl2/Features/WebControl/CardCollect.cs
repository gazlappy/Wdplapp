using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Web;
using Wdpl2.Views.WebControl;

namespace Wdpl2.Features.WebControl;

/// <summary>
/// The part of collecting a card that is the same wherever it is collected from.
/// </summary>
/// <remarks>
/// League nights are collected on the Scorecards page and cup ties on the
/// Competition nights page, but a card names whoever actually played either
/// way - including people the captains added on the night. Settling those names
/// is the same conversation with the secretary in both places.
/// </remarks>
public static class CardCollect
{
    /// <summary>
    /// Settles any player the card names that the app cannot resolve.
    /// </summary>
    /// <returns>
    /// How many players were taken in, or null if the secretary cancelled -
    /// which abandons the collect rather than writing a card with holes in it.
    /// The website copy is untouched either way, so it can simply be collected
    /// again.
    /// </returns>
    public static async Task<int?> ResolvePlayersAsync(
        Page page, WebApiClient client, IDataStore dataStore, LeagueData league,
        string matchDescription, List<ScorecardService.ClaimedFrame> frames)
    {
        var waiting = await CaptainRosterService.UnresolvedAsync(
            client, dataStore, league,
            frames.SelectMany(f => new[] { f.HomePlayerId, f.AwayPlayerId })
                  .Where(id => id.HasValue)
                  .Select(id => id!.Value));

        if (waiting.Count == 0) return 0;

        var squad = await dataStore.GetPlayersAsync(waiting[0].SeasonId);

        var decisions = await CollectPlayersPage.AskAsync(
            page, waiting, squad,
            $"{matchDescription} names "
            + (waiting.Count == 1 ? "a player" : $"{waiting.Count} players")
            + " the app does not have yet.");

        if (decisions is null) return null;

        // Leaving one waiting means its frames would land blank, so say so
        // rather than writing a card that quietly looks wrong.
        if (decisions.Count < waiting.Count)
        {
            var left = waiting.Count - decisions.Count;
            if (!await page.DisplayAlert("Collect without them?",
                    $"{left} player(s) will be left waiting, so the frames they played "
                    + "will show blank until you collect them and collect this card again.",
                    "Collect anyway", "Go back"))
                return null;
        }

        if (decisions.Count == 0) return 0;

        var (created, linked) = await CaptainRosterService.CollectAsync(
            client, dataStore, league, decisions);

        return created + linked;
    }
}
