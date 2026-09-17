using Wdpl2.Domain.Competitions;
using Wdpl2.Domain.Fixtures;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Web;

namespace Wdpl2.Views.WebControl;

/// <summary>
/// Hands fixtures to the website for live scoring and collects them back.
/// </summary>
/// <remarks>
/// This page is where ownership changes hands. While a card is live or
/// finalised the website owns it and the app must not edit those frames; only
/// collecting it returns ownership here.
/// </remarks>
public partial class ScorecardsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    /// <summary>
    /// A match a card can be opened on - a league fixture, or a cup tie.
    /// </summary>
    /// <remarks>
    /// The website holds both in one table, so the only thing this page needs
    /// to keep apart is where a collected result is written back to.
    /// </remarks>
    private sealed record Openable(Guid Id, string Label, DateTime Date, CupTie? Cup)
    {
        public bool IsCup => Cup is not null;
    }

    private readonly List<Openable> _openable = new();
    private List<ScorecardState> _states = new();
    private readonly IDataStore _dataStore;

    public ScorecardsPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LoadFixtures();
        await LoadStatesAsync();
    }

    private void LoadFixtures()
    {
        _openable.Clear();

        var season = League.Seasons.FirstOrDefault(s => s.IsActive)
                     ?? League.Seasons.OrderByDescending(s => s.StartDate).FirstOrDefault();
        if (season is null) return;

        var teams = League.Teams.Where(t => t.SeasonId == season.Id).ToDictionary(t => t.Id, t => t.Name ?? "?");

        _openable.AddRange(League.Fixtures
            .Where(f => f.SeasonId == season.Id)
            .Select(f => new Openable(
                f.Id,
                $"{f.Date:ddd dd MMM}  {teams.GetValueOrDefault(f.HomeTeamId, "?")} "
                + $"v {teams.GetValueOrDefault(f.AwayTeamId, "?")}",
                f.Date,
                null)));

        // A cup tie already played is left out: its card has been collected and
        // the competition has moved on, so opening it again would only invite
        // scoring a round that is over.
        _openable.AddRange(CupTie.For(League, season)
            .Where(t => !t.IsComplete)
            .Select(t => new Openable(
                t.Id,
                $"{t.Date:ddd dd MMM}  {t.Describe(teams)}  ({t.CompetitionName} - {t.RoundName})",
                t.Date,
                t)));

        _openable.Sort((a, b) => a.Date.CompareTo(b.Date));

        FixturePicker.ItemsSource = _openable.Select(o => o.Label).ToList();

        if (_openable.Count > 0) FixturePicker.SelectedIndex = 0;
    }

    private async Task LoadStatesAsync()
    {
        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);
            _states = await ScorecardService.GetStatesAsync(client);
            RenderStates();
        }
        catch (WebApiException ex)
        {
            Report(ex.Code == "unknown_module"
                ? "The website does not have the scorecards module yet. Deploy the backend, then install tables."
                : ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
    }

    private void RenderStates()
    {
        CardList.Children.Clear();
        EmptyLabel.IsVisible = _states.Count == 0;

        foreach (var state in _states)
        {
            var heading = new Label
            {
                Text = $"{state.HomeTeam} v {state.AwayTeam}",
                FontAttributes = FontAttributes.Bold,
                FontSize = 13,
                TextColor = Color.FromArgb("#1E293B"),
            };

            var detail = new Label
            {
                Text = state.Describe(),
                FontSize = 11,
                TextColor = Color.FromArgb(state.Owner switch
                {
                    CardOwner.Live => "#059669",
                    CardOwner.Finalised => "#D97706",
                    _ => "#64748B",
                }),
            };

            var grid = new Grid
            {
                ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) },
                ColumnSpacing = 8,
            };
            grid.Add(new VerticalStackLayout { Spacing = 2, Children = { heading, detail } });

            var captured = state;

            var actions = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };

            if (state.Owner == CardOwner.Finalised)
            {
                var claim = new Button
                {
                    Text = "Collect",
                    BackgroundColor = Color.FromArgb("#059669"),
                    TextColor = Colors.White,
                    CornerRadius = 8,
                    FontSize = 12,
                    Padding = new Thickness(14, 6),
                };
                claim.Clicked += async (_, _) => await ClaimAsync(captured);
                actions.Add(claim);
            }
            else if (state.Owner == CardOwner.Live)
            {
                actions.Add(new Label
                {
                    Text = "captains scoring",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#94A3B8"),
                    VerticalOptions = LayoutOptions.Center,
                });
            }
            else if (state.Owner == CardOwner.Claimed)
            {
                // Collecting is idempotent, so a card that was collected but
                // did not land - a crash, or the season restored from a backup
                // taken before it - can simply be collected again.
                var again = new Button
                {
                    Text = "Collect again",
                    BackgroundColor = Colors.Transparent,
                    TextColor = Color.FromArgb("#475569"),
                    BorderColor = Color.FromArgb("#CBD5E1"),
                    BorderWidth = 1,
                    CornerRadius = 8,
                    FontSize = 12,
                    Padding = new Thickness(12, 6),
                };
                again.Clicked += async (_, _) => await ClaimAsync(captured);
                actions.Add(again);
            }

            // A card opened by mistake has no other way out: it cannot be
            // reopened, cannot be collected before it is finished, and blocks
            // opening another for the same fixture.
            if (state.Owner is CardOwner.Live or CardOwner.Finalised)
            {
                var close = new Button
                {
                    Text = "Close",
                    BackgroundColor = Colors.Transparent,
                    TextColor = Color.FromArgb("#EF4444"),
                    BorderColor = Color.FromArgb("#FCA5A5"),
                    BorderWidth = 1,
                    CornerRadius = 8,
                    FontSize = 12,
                    Padding = new Thickness(12, 6),
                };
                close.Clicked += async (_, _) => await CloseAsync(captured);
                actions.Add(close);
            }

            grid.Add(actions, 1);

            CardList.Children.Add(new Frame
            {
                BorderColor = Color.FromArgb("#E2E8F0"),
                BackgroundColor = Color.FromArgb("#F8FAFC"),
                CornerRadius = 8,
                Padding = new Thickness(12, 10),
                HasShadow = false,
                Content = grid,
            });
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadStatesAsync();

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        if (FixturePicker.SelectedIndex < 0 || FixturePicker.SelectedIndex >= _openable.Count) return;
        var chosen = _openable[FixturePicker.SelectedIndex];

        // One source for the match format: Settings. Not the season, not the
        // fixture's existing frames - see MatchFormat.
        var format = MatchFormat.From(League.Settings);

        var cupNote = chosen.IsCup
            ? "\n\nCup tie: the captains toss for it on the card, and the winner of the toss "
              + "fills it in as home."
            : "";

        if (!await DisplayAlert("Open for live scoring?",
                $"{chosen.Label}\n{format}{cupNote}\n\n" +
                "The website will own this scorecard until you collect it. This app will not change its frames in the meantime.",
                "Open", "Cancel"))
            return;

        OpenButton.IsEnabled = false;
        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var state = await ScorecardService.OpenAsync(client, chosen.Id, format);
            Report($"Open for live scoring. Captains can now score at your website's /captain/ page.", error: false);
            await LoadStatesAsync();
        }
        catch (WebApiException ex)
        {
            Report(ex.Code switch
            {
                "already_live" => "That match is already open for live scoring.",
                "awaiting_claim" => "That match has been finished by the captains. Collect it first.",
                "unknown_fixture" => chosen.IsCup
                    ? "That cup tie has not been published to the website yet. Publish the season "
                      + "first - cup ties go up with it."
                    : "That fixture has not been published to the website yet. Publish the season first.",
                _ => ex.Message,
            }, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
        finally
        {
            OpenButton.IsEnabled = true;
        }
    }

    private async Task ClaimAsync(ScorecardState state)
    {
        var again = state.Owner == CardOwner.Claimed;

        if (!await DisplayAlert(again ? "Collect this card again?" : "Collect this card?",
                $"{state.HomeTeam} v {state.AwayTeam}\nFinished {state.HomeScore}–{state.AwayScore}\n\n" +
                (again
                    ? "This was collected before. Reading it again writes the same result into "
                      + "the season; nothing is duplicated and the website copy stays frozen."
                    : "The result will be written into this season and the website copy frozen."),
                "Collect", "Cancel"))
            return;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var (claimed, frames, wasClaimed) = await ScorecardService.ClaimAsync(client, state.FixtureId);

            // Settle the players first, or the slots naming them land blank.
            var pulled = await ResolvePlayersAsync(client, state, frames);
            if (pulled is null) return;

            var note = pulled > 0 ? $" Took in {pulled} player(s) the captains added." : "";

            // A cup tie is not a league fixture, so its result goes back into
            // the competition it came out of - and the winner moves on.
            var cup = await ApplyToCupTieAsync(state, frames);
            if (cup is not null)
            {
                Report(wasClaimed
                    ? $"Already collected previously; {cup} Nothing was duplicated."
                    : $"Collected. {cup}{note}", error: false);

                await LoadStatesAsync();
                LoadFixtures();
                return;
            }

            var applied = await ApplyToFixtureAsync(state.FixtureId, frames);

            Report(wasClaimed
                ? $"Already collected previously; re-applied {applied} frames.{note} Nothing was duplicated."
                : $"Collected. {applied} frames written into the season.{note}", error: false);

            await LoadStatesAsync();
        }
        catch (WebApiException ex)
        {
            Report(ex.Code switch
            {
                "still_live" => "That match is still being scored. It has to be finished first.",
                _ => ex.Message,
            }, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
    }

    /// <summary>
    /// Abandons a card so the fixture can be opened again.
    /// </summary>
    /// <remarks>
    /// Warns in proportion to what is lost: a card nobody has scored is a
    /// trivial thing to discard, one with frames on it is not.
    /// </remarks>
    private async Task CloseAsync(ScorecardState state)
    {
        var scored = state.FramesPlayed > 0;

        var warning = scored
            ? $"{state.FramesPlayed} frame(s) have been scored ({state.HomeScore}–{state.AwayScore}). "
              + "Closing throws that away and cannot be undone. If the match is finished, "
              + "ask the captains to sign it off and collect it instead."
            : "Nothing has been scored on it yet.";

        if (!await DisplayAlert("Close this card?",
                $"{state.HomeTeam} v {state.AwayTeam}\n\n{warning}\n\n"
                + "The fixture goes back to this app and can be opened again.",
                scored ? "Close and lose the scores" : "Close", "Cancel"))
            return;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var closed = await ScorecardService.CloseAsync(client, state.FixtureId);

            Report(closed.HadScoring
                ? $"Closed. {closed.FramesPlayed} scored frame(s) were discarded."
                : "Closed. The fixture can be opened again.", error: false);

            await LoadStatesAsync();
        }
        catch (WebApiException ex)
        {
            Report(ex.Code == "already_claimed"
                ? "That card has already been collected, so there is nothing to close."
                : ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
    }

    /// <summary>
    /// Settles any player the card names that the app cannot resolve.
    /// </summary>
    /// <remarks>
    /// Returns null if the secretary cancelled, which abandons the collect
    /// rather than writing a card with holes in it. The website copy is
    /// untouched either way, so it can simply be collected again.
    /// </remarks>
    private async Task<int?> ResolvePlayersAsync(
        WebApiClient client, ScorecardState state, List<ScorecardService.ClaimedFrame> frames)
    {
        var waiting = await CaptainRosterService.UnresolvedAsync(
            client, _dataStore, League,
            frames.SelectMany(f => new[] { f.HomePlayerId, f.AwayPlayerId })
                  .Where(id => id.HasValue)
                  .Select(id => id!.Value));

        if (waiting.Count == 0) return 0;

        var squad = await _dataStore.GetPlayersAsync(waiting[0].SeasonId);

        var decisions = await CollectPlayersPage.AskAsync(
            this, waiting, squad,
            $"{state.HomeTeam} v {state.AwayTeam} names "
            + (waiting.Count == 1 ? "a player" : $"{waiting.Count} players")
            + " the app does not have yet.");

        if (decisions is null) return null;

        // Leaving one waiting means its frames would land blank, so say so
        // rather than writing a card that quietly looks wrong.
        if (decisions.Count < waiting.Count)
        {
            var left = waiting.Count - decisions.Count;
            if (!await DisplayAlert("Collect without them?",
                    $"{left} player(s) will be left waiting, so the frames they played "
                    + "will show blank until you collect them and collect this card again.",
                    "Collect anyway", "Go back"))
                return null;
        }

        if (decisions.Count == 0) return 0;

        var (created, linked) = await CaptainRosterService.CollectAsync(
            client, _dataStore, League, decisions);

        return created + linked;
    }

    /// <summary>
    /// Writes collected frames into the local fixture, in both places the app
    /// keeps fixtures.
    /// </summary>
    /// <remarks>
    /// Persistence here is hybrid: the JSON snapshot behind <c>DataStore.Data</c>
    /// and the SQLite copy behind <see cref="IDataStore"/>. The Fixtures page
    /// reads the SQLite copy, so writing only the snapshot leaves the result
    /// invisible in the app even though it collected cleanly.
    /// <para>
    /// The SQLite row is the one the app edits, so it is the one the frames are
    /// overlaid onto; the snapshot is brought level from the same result rather
    /// than being copied over the top of it.
    /// </para>
    /// </remarks>
    private async Task<int> ApplyToFixtureAsync(Guid fixtureId, List<ScorecardService.ClaimedFrame> frames)
    {
        var scored = frames.Where(f => f.Winner != FrameWinner.None).ToList();

        var stored = await _dataStore.GetFixtureAsync(fixtureId);

        var applied = 0;

        if (stored is not null)
        {
            applied = Overlay(stored, scored);
            await _dataStore.UpdateFixtureAsync(stored);
        }

        var snapshot = League.Fixtures.FirstOrDefault(f => f.Id == fixtureId);
        if (snapshot is not null)
        {
            var count = Overlay(snapshot, scored);
            if (stored is null) applied = count;
            DataStore.SaveJsonOnly();
        }

        return applied;
    }

    /// <summary>
    /// Writes a collected cup card into the competition the tie belongs to.
    /// </summary>
    /// <remarks>
    /// Returns null when the card is an ordinary league fixture, so the caller
    /// falls through to the usual path. The frames themselves are not kept: a
    /// knockout round records who won and by how much, and the card on the
    /// website stays frozen as the detailed record of the night.
    /// <para>
    /// The scoreline stays in the columns the card used. Which team fills the
    /// card in first is what the toss decided, not which column a frame is won
    /// in, so home here is still the team the draw listed first.
    /// </para>
    /// </remarks>
    private async Task<string?> ApplyToCupTieAsync(
        ScorecardState state, List<ScorecardService.ClaimedFrame> frames)
    {
        var season = League.Seasons.FirstOrDefault(s => s.IsActive)
                     ?? League.Seasons.OrderByDescending(s => s.StartDate).FirstOrDefault();
        if (season is null) return null;

        var tie = CupTie.For(League, season).FirstOrDefault(t => t.Id == state.FixtureId);
        if (tie is null) return null;

        var competition = await _dataStore.GetCompetitionAsync(tie.CompetitionId);
        if (competition is null) return "the competition it belongs to is no longer in the app.";

        var found = CupTie.Locate(competition, tie.Id);
        if (found is null) return "the tie is no longer in that competition's bracket.";

        var (_, round, match) = found.Value;

        var home = frames.Count(f => f.Winner == FrameWinner.Home);
        var away = frames.Count(f => f.Winner == FrameWinner.Away);

        match.Participant1Score = home;
        match.Participant2Score = away;

        // A drawn cup tie has to be settled at the table, so it is left open
        // rather than sent through on a coin toss of our own.
        if (home == away)
        {
            match.WinnerId = null;
            match.IsComplete = false;
            BracketAdvance.Clear(competition, round, match);
        }
        else
        {
            match.WinnerId = home > away ? match.Participant1Id : match.Participant2Id;
            match.IsComplete = true;
            BracketAdvance.Advance(competition, round, match);
        }

        await _dataStore.UpdateCompetitionAsync(competition);

        var snapshot = League.Competitions.FirstOrDefault(c => c.Id == competition.Id);
        if (snapshot is not null)
        {
            var mirror = CupTie.Locate(snapshot, tie.Id);
            if (mirror is not null)
            {
                mirror.Value.Match.Participant1Score = match.Participant1Score;
                mirror.Value.Match.Participant2Score = match.Participant2Score;
                mirror.Value.Match.WinnerId = match.WinnerId;
                mirror.Value.Match.IsComplete = match.IsComplete;

                if (match.IsComplete) BracketAdvance.Advance(snapshot, mirror.Value.Round, mirror.Value.Match);
                else BracketAdvance.Clear(snapshot, mirror.Value.Round, mirror.Value.Match);
            }

            DataStore.SaveJsonOnly();
        }

        var teams = League.Teams.ToDictionary(t => t.Id, t => t.Name ?? "?");

        if (!match.WinnerId.HasValue)
            return $"{tie.CompetitionName} {tie.RoundName} finished level at {home}-{away}, "
                 + "so nobody has gone through. Settle it in the Competitions tab.";

        var winner = teams.GetValueOrDefault(match.WinnerId.Value, "The winner");
        return $"{winner} win {Math.Max(home, away)}-{Math.Min(home, away)} and go through "
             + $"in the {tie.CompetitionName} {tie.RoundName}.";
    }

    /// <summary>
    /// Lays collected frames over a fixture, matching on frame number.
    /// </summary>
    /// <remarks>
    /// Frame number is the identity both sides agreed on when the card was
    /// opened. Frames the captains left unplayed are not in the list at all, so
    /// anything already recorded against them survives rather than being blanked.
    /// </remarks>
    private static int Overlay(Fixture fixture, List<ScorecardService.ClaimedFrame> scored)
    {
        // A player the secretary linked to someone already here is named by the
        // website's id in these frames, and by the app's id everywhere else.
        var links = League.CollectedWebPlayers;
        Guid Resolve(Guid id) => links.TryGetValue(id, out var linked) ? linked : id;

        var applied = 0;

        foreach (var claimed in scored)
        {
            var frame = fixture.Frames.FirstOrDefault(f => f.Number == claimed.Number);
            if (frame is null)
            {
                frame = new FrameResult { Number = claimed.Number };
                fixture.Frames.Add(frame);
            }

            frame.Winner = claimed.Winner;
            frame.EightBall = claimed.EightBall;
            if (claimed.HomePlayerId.HasValue) frame.HomePlayerId = Resolve(claimed.HomePlayerId.Value);
            if (claimed.AwayPlayerId.HasValue) frame.AwayPlayerId = Resolve(claimed.AwayPlayerId.Value);
            applied++;
        }

        fixture.Frames.Sort((a, b) => a.Number.CompareTo(b.Number));
        fixture.ModifiedDate = DateTime.UtcNow;
        return applied;
    }

    private void Report(string message, bool error)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb(error ? "#EF4444" : "#10B981");
    }
}
