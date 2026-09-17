using System.Text;
using Wdpl2.Domain.Competitions;
using Wdpl2.Domain.Fixtures;
using Wdpl2.Features.WebControl;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Web;

namespace Wdpl2.Views.WebControl;

/// <summary>
/// Hands a competition night to the players running it, and takes the results back.
/// </summary>
/// <remarks>
/// A competition night happens in several pubs at once. Each group is organised
/// by one of its own players, who gets a PIN that opens that group and nothing
/// else. This page is where those PINs are set and where the results come home.
/// <para>
/// The app stays the record of the competition: a published session is a place
/// to enter results while the night is on, and collecting writes them back here.
/// </para>
/// <para>
/// A team knockout works differently enough to have its own section of this
/// page. There is no group and no organiser: two teams play a full scorecard
/// between them, so the tie is handed to the two captains the way a league night
/// is - but it is still this competition's business, which is why it lives here
/// rather than on the Scorecards page.
/// </para>
/// </remarks>
public partial class CompsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private sealed class Row
    {
        public required CompetitionNightService.Session Session { get; init; }
        public required Entry Field { get; init; }
    }

    private readonly IDataStore _dataStore;
    private readonly List<Competition> _competitions = new();
    private readonly List<Row> _rows = new();
    private List<CompetitionNightService.SessionState> _live = new();

    private readonly List<CupTie> _ties = new();
    private List<ScorecardState> _cards = new();

    public CompsPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCompetitionsAsync();

        // Looks for anything the organisers or captains sent while the app was shut.
        if (IsCup) await LoadTiesAsync();
        else await LoadLiveAsync();
    }

    /// <summary>
    /// Competitions that have something to run.
    /// </summary>
    /// <remarks>
    /// Read from <see cref="IDataStore"/>, which is where the Competitions tab
    /// keeps them. The JSON snapshot holds its own copy, and taking the
    /// competition from there would mean collecting a night onto a version the
    /// app is not showing - the results would land somewhere nobody looks.
    /// <para>
    /// Team knockouts are in the list too, but they answer a different half of
    /// this page - see <see cref="BuildTies"/>.
    /// </para>
    /// </remarks>
    private async Task LoadCompetitionsAsync()
    {
        _competitions.Clear();

        // Season-scoped reads answer nothing for a null season, so each season
        // is asked for in turn rather than hoping for "all of them".
        foreach (var season in League.Seasons.OrderByDescending(x => x.StartDate))
        {
            _competitions.AddRange(await _dataStore.GetCompetitionsAsync(season.Id));
        }



        CompetitionPicker.ItemsSource = _competitions.Select(c => c.Name).ToList();

        if (_competitions.Count == 0)
        {
            SummaryLabel.Text = "No competition in this league yet.";
            return;
        }

        CompetitionPicker.SelectedIndex = 0;
        BuildRows();
    }

    private Competition? Selected =>
        CompetitionPicker.SelectedIndex >= 0 && CompetitionPicker.SelectedIndex < _competitions.Count
            ? _competitions[CompetitionPicker.SelectedIndex]
            : null;

    private async void OnCompetitionChanged(object? sender, EventArgs e)
    {
        BuildRows();
        if (IsCup) await LoadTiesAsync();
    }

    /// <summary>True when the chosen competition is played team against team.</summary>
    private bool IsCup => Selected?.Format == CompetitionFormat.TeamKnockout;

    /// <summary>
    /// Shows the half of this page the chosen competition actually uses.
    /// </summary>
    /// <remarks>
    /// The two halves have nothing in common: a singles night is organised by
    /// one of its players with a PIN, a cup tie is played on a scorecard by two
    /// captains. Showing both at once would only invite setting up the wrong one.
    /// </remarks>
    private void ShowTheRightHalf()
    {
        var cup = IsCup;

        CupFrame.IsVisible = cup;
        SessionsFrame.IsVisible = !cup;
        PublishFrame.IsVisible = !cup;
        LiveFrame.IsVisible = !cup;

        IntroLabel.Text = cup
            ? "Hand each tie to the two captains, then collect the finished card back into the draw."
            : "Give the player running each group a PIN, publish, and collect their results afterwards. "
              + "The PIN opens that group only.";
    }

    /// <summary>Participant names, for whichever kind of competition this is.</summary>
    private Dictionary<Guid, string> NamesFor(Competition competition)
    {
        var names = new Dictionary<Guid, string>();

        foreach (var player in League.Players)
        {
            names[player.Id] = $"{player.FirstName} {player.LastName}".Trim();
        }

        foreach (var pair in competition.DoublesTeams)
        {
            var one = names.TryGetValue(pair.Player1Id, out var a) ? a : "?";
            var two = names.TryGetValue(pair.Player2Id, out var b) ? b : "?";
            names[pair.Id] = $"{one} & {two}";
        }

        foreach (var team in League.Teams)
        {
            names[team.Id] = team.Name ?? "(unnamed team)";
        }

        return names;
    }

    private void BuildRows()
    {
        ShowTheRightHalf();

        SessionList.Children.Clear();
        _rows.Clear();

        var competition = Selected;
        if (competition is null) return;

        if (IsCup)
        {
            BuildTies();
            return;
        }

        var names = NamesFor(competition);
        var sessions = CompetitionNightService.Build(competition, names);

        NoneLabel.IsVisible = sessions.Count == 0;

        foreach (var session in sessions)
        {
            var field = new Entry
            {
                Text = session.Pin ?? "",
                Placeholder = "no PIN",
                FontSize = 13,
                Keyboard = Keyboard.Numeric,
                MaxLength = 8,
            };
            field.TextChanged += (_, _) => SavePin(session, field.Text);

            var where = string.IsNullOrEmpty(session.VenueName)
                ? "no venue set"
                : session.VenueName + (string.IsNullOrEmpty(session.TableLabel) ? "" : $" · {session.TableLabel}");

            var heading = new VerticalStackLayout
            {
                Spacing = 1,
                Children =
                {
                    new Label { Text = session.Name, FontSize = 13, FontAttributes = FontAttributes.Bold,
                                TextColor = Color.FromArgb("#1E293B") },
                    new Label { Text = $"{session.ParticipantIds.Count} players · {session.Matches.Count} matches · {where}",
                                FontSize = 10, TextColor = Color.FromArgb("#94A3B8") },
                },
            };

            var runner = new Label
            {
                Text = session.OrganiserName ?? "nobody nominated",
                FontSize = 12,
                TextColor = Color.FromArgb(session.OrganiserName is null ? "#D97706" : "#475569"),
                VerticalTextAlignment = TextAlignment.Center,
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) },
                },
                ColumnSpacing = 8,
                Padding = new Thickness(0, 4),
            };
            grid.Add(heading, 0, 0);
            grid.Add(runner, 1, 0);
            grid.Add(field, 2, 0);

            SessionList.Children.Add(grid);
            _rows.Add(new Row { Session = session, Field = field });
        }

        var withPin = sessions.Count(s => !string.IsNullOrWhiteSpace(s.Pin));
        var withRunner = sessions.Count(s => s.OrganiserId.HasValue);

        SummaryLabel.Text = sessions.Count == 0
            ? "Nothing to run yet."
            : $"{sessions.Count} to run · {withRunner} have someone nominated · {withPin} have a PIN.";
    }

    /// <summary>
    /// Writes a PIN back onto the group or round it belongs to.
    /// </summary>
    /// <remarks>
    /// The session built for the website is a copy, so the PIN has to be put
    /// back on the competition itself or it would be lost the moment the page
    /// rebuilt its rows.
    /// </remarks>
    private void SavePin(CompetitionNightService.Session session, string? pin)
    {
        var competition = Selected;
        if (competition is null) return;

        var clean = string.IsNullOrWhiteSpace(pin) ? null : pin.Trim();

        if (session.Kind == "group")
        {
            var group = competition.Groups.FirstOrDefault(g => g.Id == session.RefId);
            if (group is not null) group.RunnerPin = clean;
        }
        else
        {
            var round = competition.Rounds.FirstOrDefault(r => r.Id == session.RefId);
            if (round is not null) round.RunnerPin = clean;
        }

        // Written to the store the Competitions tab reads, and mirrored into the
        // snapshot so a publish built from either sees the same PIN.
        _ = PersistAsync(competition);
    }

    /// <summary>Saves a competition to both places the app keeps one.</summary>
    private async Task PersistAsync(Competition competition)
    {
        try
        {
            await _dataStore.UpdateCompetitionAsync(competition);
            await _dataStore.SaveAsync();

            var mirrored = League.Competitions.FirstOrDefault(c => c.Id == competition.Id);
            if (mirrored is not null)
            {
                League.Competitions[League.Competitions.IndexOf(mirrored)] = competition;
            }
            else
            {
                League.Competitions.Add(competition);
            }

            DataStore.SaveJsonOnly();
        }
        catch (Exception ex)
        {
            Report($"Could not save: {ex.Message}", error: true);
        }
    }

    private void OnFillPinsClicked(object? sender, EventArgs e)
    {
        var competition = Selected;
        if (competition is null) return;

        var taken = _rows.Select(r => r.Field.Text).ToList();
        var filled = 0;

        foreach (var row in _rows)
        {
            if (!string.IsNullOrWhiteSpace(row.Field.Text)) continue;

            var pin = CompetitionNightService.NewPin(taken);
            taken.Add(pin);

            // Setting Text raises TextChanged, which is what saves it.
            row.Field.Text = pin;
            filled++;
        }

        Report(filled == 0
            ? "Every group already has a PIN."
            : $"Gave {filled} group(s) a PIN. Publish to make them work.", error: false);
    }

    private async void OnPushClicked(object? sender, EventArgs e)
    {
        var competition = Selected;
        if (competition is null) return;

        if (competition.SeasonId is null)
        {
            Report("That competition is not in a season, so it cannot be published.", error: true);
            return;
        }

        PushButton.IsEnabled = false;

        try
        {
            var names = NamesFor(competition);
            var sessions = CompetitionNightService.Build(competition, names);

            var (_, skipped) = CompetitionNightService.BuildPayload(competition, sessions, names);

            if (skipped.Count == sessions.Count)
            {
                Report("Nothing has a PIN yet, so there is nothing to publish.", error: true);
                return;
            }

            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var written = await CompetitionNightService.PushAsync(client, competition, sessions, names);

            Report(skipped.Count == 0
                ? $"Published {written} group(s). The players running them can sign in now."
                : $"Published {written}. Left out {skipped.Count} with no PIN: {string.Join(", ", skipped)}.",
                error: false);

            await LoadLiveAsync();
        }
        catch (WebApiException ex)
        {
            Report(ex.Code == "unknown_module"
                ? "The website does not have the competitions module yet. Deploy the backend, then install tables."
                : ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
        finally
        {
            PushButton.IsEnabled = true;
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadLiveAsync();

    private async Task LoadLiveAsync()
    {
        RefreshButton.IsEnabled = false;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            _live = await CompetitionNightService.StateAsync(client);

            // A group the organiser has sent is theirs no longer. Waiting for a
            // second button to be pressed here is how "sent to the league" ends
            // up meaning "sitting on a website nobody is looking at".
            var brought = await CollectFinishedAsync(client);

            if (brought.Count > 0)
            {
                _live = await CompetitionNightService.StateAsync(client);
            }

            BuildLive();

            Report(brought.Count == 0
                ? "Read back from the website."
                : $"Took in {brought.Count} group(s) the organisers had sent: "
                  + string.Join(", ", brought) + ". Publish the website to show them.",
                error: false);
        }
        catch (WebApiException ex)
        {
            Report(ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Takes in every group an organiser has finished with.
    /// </summary>
    /// <remarks>
    /// Safe to run on every refresh: collecting is idempotent, and a group that
    /// has already been collected is skipped. Anything that fails is left for
    /// the next look rather than stopping the ones behind it.
    /// </remarks>
    private async Task<List<string>> CollectFinishedAsync(WebApiClient client)
    {
        var brought = new List<string>();

        var waiting = _live.Where(s => s.Finished && !s.Collected).ToList();
        if (waiting.Count == 0) return brought;

        foreach (var state in waiting)
        {
            try
            {
                var collected = await CompetitionNightService.CollectAsync(client, state.Id);
                var competition = await _dataStore.GetCompetitionAsync(state.CompetitionId);

                if (competition is null) continue;

                if (CompetitionNightService.Apply(competition, collected) > 0)
                {
                    await PersistAsync(competition);
                    brought.Add($"{competition.Name} — {state.Name}");
                }
            }
            catch (WebApiException)
            {
                // Left where it is; the next refresh will try again.
            }
        }

        if (brought.Count > 0) await LoadCompetitionsAsync();

        return brought;
    }

    private void BuildLive()
    {
        LiveList.Children.Clear();
        NoLiveLabel.IsVisible = _live.Count == 0;
        CloseAllButton.IsVisible = _live.Count > 0;

        foreach (var state in _live)
        {
            var captured = state;

            var heading = new Label
            {
                Text = $"{state.Competition} — {state.Name}",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1E293B"),
            };

            var detail = new Label
            {
                Text = state.Collected
                    ? $"Collected · {state.Played} of {state.Matches} matches"
                    : state.Finished
                        ? $"Sent by the organiser · {state.Played} of {state.Matches} played · ready to collect"
                        : $"{state.Played} of {state.Matches} matches played · {state.Present} here"
                          + (state.OrganiserName is null ? "" : $" · run by {state.OrganiserName}"),
                FontSize = 11,
                FontAttributes = state.Finished && !state.Collected
                    ? FontAttributes.Bold : FontAttributes.None,
                TextColor = Color.FromArgb(state.Collected ? "#94A3B8"
                    : state.Finished ? "#047857" : "#475569"),
            };

            var actions = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };

            if (!state.Collected)
            {
                var collect = new Button
                {
                    Text = "Collect",
                    BackgroundColor = Color.FromArgb("#059669"),
                    TextColor = Colors.White,
                    CornerRadius = 8,
                    FontSize = 12,
                    Padding = new Thickness(14, 6),
                };
                collect.Clicked += async (_, _) => await CollectAsync(captured);
                actions.Add(collect);
            }

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

            var grid = new Grid
            {
                ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) },
                ColumnSpacing = 8,
            };
            grid.Add(new VerticalStackLayout { Spacing = 2, Children = { heading, detail } });
            grid.Add(actions, 1);

            LiveList.Children.Add(new Frame
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

    /// <summary>
    /// Takes one group's results into the competition and freezes the website copy.
    /// </summary>
    /// <remarks>
    /// Safe to repeat: collecting again returns the same results and writes the
    /// same values, so a lost reply costs a second press and nothing else.
    /// </remarks>
    private async Task CollectAsync(CompetitionNightService.SessionState state)
    {
        if (!await DisplayAlert("Collect this group?",
                $"{state.Competition} — {state.Name}\n"
                + $"{state.Played} of {state.Matches} matches played.\n\n"
                + "The results are written into the competition and the website copy is frozen.",
                "Collect", "Cancel"))
            return;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var collected = await CompetitionNightService.CollectAsync(client, state.Id);

            // Fetched by id from the store the Competitions tab reads, so the
            // night is written onto the competition the app is actually showing.
            var competition = await _dataStore.GetCompetitionAsync(state.CompetitionId);

            if (competition is null)
            {
                Report("That competition is not in this league's data any more. Nothing was written.",
                    error: true);
                return;
            }

            var applied = CompetitionNightService.Apply(competition, collected);

            if (applied > 0) await PersistAsync(competition);

            Report(applied == 0
                ? "Collected, but nothing was written — the draw did not match anything in this competition."
                : $"Collected. {competition.Name} now holds the {applied} tie(s) that were played, "
                  + "in the order they were drawn. Publish the website to show it.",
                error: applied == 0);

            await LoadLiveAsync();
            await LoadCompetitionsAsync();
        }
        catch (WebApiException ex)
        {
            Report(ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
    }

    private async Task CloseAsync(CompetitionNightService.SessionState state)
    {
        var warning = state.Played > 0
            ? $"{state.Played} result(s) have been entered. Closing throws them away and cannot be undone."
            : "Nothing has been entered on it.";

        if (!await DisplayAlert("Close this group?",
                $"{state.Competition} — {state.Name}\n\n{warning}\n\n"
                + "The PIN stops working and the group comes off the website.",
                state.Played > 0 ? "Close and lose them" : "Close", "Cancel"))
            return;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            await client.AdminAsync("comps", "close", new { sessionId = state.Id });

            Report("Closed.", error: false);
            await LoadLiveAsync();
        }
        catch (WebApiException ex)
        {
            Report(ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
    }

    /// <summary>
    /// Takes every published group off the website in one go.
    /// </summary>
    /// <remarks>
    /// The end of a competition night: a dozen groups, each with a PIN that
    /// should stop working, and no appetite for a dozen confirmations.
    /// <para>
    /// Anything holding results that have not been collected is named before
    /// anything is closed, because closing throws those away and the only copy
    /// is on the website.
    /// </para>
    /// </remarks>
    private async void OnCloseAllClicked(object? sender, EventArgs e)
    {
        if (_live.Count == 0)
        {
            Report("Nothing is published, so there is nothing to close.", error: false);
            return;
        }

        // A collected group is already frozen and holds nothing that is not
        // safely in the app, so closing it costs nothing.
        var atRisk = _live
            .Where(s => !s.Collected && s.Played > 0)
            .OrderBy(s => s.Competition)
            .ThenBy(s => s.Name)
            .ToList();

        var warning = atRisk.Count == 0
            ? "Nothing has results waiting to be collected."
            : $"{atRisk.Count} still hold results that have not been collected, and closing "
              + "throws them away:\n  "
              + string.Join("\n  ", atRisk.Select(s => $"{s.Competition} — {s.Name} ({s.Played} played)"));

        if (!await DisplayAlert("Close all?",
                $"{_live.Count} group(s) come off the website and their PINs stop working.\n\n"
                + warning,
                atRisk.Count == 0 ? "Close all" : "Close and lose them", "Cancel"))
            return;

        CloseAllButton.IsEnabled = false;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var closed = 0;
            var failures = new List<string>();

            // One at a time, so a group that refuses does not stop the rest and
            // the secretary is told which one it was.
            foreach (var session in _live.ToList())
            {
                try
                {
                    await client.AdminAsync("comps", "close", new { sessionId = session.Id });
                    closed++;
                }
                catch (WebApiException ex)
                {
                    failures.Add($"{session.Name} — {ex.Message}");
                }
            }

            Report(failures.Count == 0
                ? $"Closed {closed} group(s). Nothing is live on the website now."
                : $"Closed {closed}. Could not close {failures.Count}: {string.Join("; ", failures)}",
                error: failures.Count > 0);

            await LoadLiveAsync();
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
        finally
        {
            CloseAllButton.IsEnabled = true;
        }
    }

    // ============================================================= cup ties

    /// <summary>
    /// The ties of this competition that a card could be opened on.
    /// </summary>
    /// <remarks>
    /// Read from the app's own bracket rather than from the website, so a tie
    /// appears here the moment the draw is made - before anything has been
    /// published. Publishing is what makes it openable; that failure is
    /// reported when it happens rather than by hiding the tie.
    /// </remarks>
    private void BuildTies()
    {
        TieList.Children.Clear();
        _ties.Clear();

        var competition = Selected;
        if (competition is null) return;

        var season = League.Seasons.FirstOrDefault(s => s.Id == competition.SeasonId)
                     ?? League.Seasons.FirstOrDefault(s => s.IsActive);
        if (season is null) return;

        var teams = League.Teams.ToDictionary(t => t.Id, t => t.Name ?? "?");

        _ties.AddRange(CupTie.For(League, season).Where(t => t.CompetitionId == competition.Id));

        // A tie already played keeps its row on the website - a collected card
        // still points at it - but there is nothing left to hand out.
        var open = _ties.Where(t => !t.IsComplete).ToList();

        TiePicker.ItemsSource = open
            .Select(t => $"{t.RoundName}  ·  {t.Describe(teams)}")
            .ToList();

        if (open.Count > 0) TiePicker.SelectedIndex = 0;

        OpenTieButton.IsEnabled = open.Count > 0;
        TiePicker.IsEnabled = open.Count > 0;

        SummaryLabel.Text = open.Count == 0
            ? $"{_ties.Count} tie(s), none left to play."
            : $"{open.Count} tie(s) still to play.";

        RenderTieCards();
    }

    /// <summary>The tie the picker is on, or null.</summary>
    private CupTie? ChosenTie
    {
        get
        {
            var open = _ties.Where(t => !t.IsComplete).ToList();
            return TiePicker.SelectedIndex >= 0 && TiePicker.SelectedIndex < open.Count
                ? open[TiePicker.SelectedIndex]
                : null;
        }
    }

    private async Task LoadTiesAsync()
    {
        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            _cards = await ScorecardService.GetStatesAsync(client);
            RenderTieCards();
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

    /// <summary>Whatever the website is holding for this competition's ties.</summary>
    private void RenderTieCards()
    {
        TieList.Children.Clear();

        var mine = new HashSet<Guid>(_ties.Select(t => t.Id));
        var cards = _cards.Where(c => mine.Contains(c.FixtureId)).ToList();

        NoTiesLabel.IsVisible = cards.Count == 0;

        foreach (var card in cards)
        {
            var tie = _ties.First(t => t.Id == card.FixtureId);
            var captured = card;

            var heading = new Label
            {
                Text = $"{tie.RoundName}  ·  {card.HomeTeam} v {card.AwayTeam}",
                FontAttributes = FontAttributes.Bold,
                FontSize = 13,
                TextColor = Color.FromArgb("#1E293B"),
            };

            var detail = new Label
            {
                Text = DescribeTie(card),
                FontSize = 11,
                TextColor = Color.FromArgb(card.Owner switch
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

            var actions = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };

            if (card.Owner == CardOwner.Finalised)
            {
                var collect = new Button
                {
                    Text = "Collect",
                    BackgroundColor = Color.FromArgb("#059669"),
                    TextColor = Colors.White,
                    CornerRadius = 8,
                    FontSize = 12,
                    Padding = new Thickness(14, 6),
                };
                collect.Clicked += async (_, _) => await CollectTieAsync(captured);
                actions.Add(collect);
            }
            else if (card.Owner == CardOwner.Live)
            {
                actions.Add(new Label
                {
                    Text = "captains scoring",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#94A3B8"),
                    VerticalOptions = LayoutOptions.Center,
                });
            }
            else if (card.Owner == CardOwner.Claimed)
            {
                // Collecting is idempotent, so a tie that was collected but did
                // not land can simply be collected again.
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
                again.Clicked += async (_, _) => await CollectTieAsync(captured);
                actions.Add(again);
            }

            if (card.Owner is CardOwner.Live or CardOwner.Finalised)
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
                close.Clicked += async (_, _) => await CloseTieAsync(captured);
                actions.Add(close);
            }

            grid.Add(actions, 1);

            TieList.Children.Add(new Frame
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

    /// <summary>
    /// A cup tie's state in the league's own terms.
    /// </summary>
    /// <remarks>
    /// Different from a league night's in one way that matters: it can be over
    /// with frames still unplayed, because it is the first to eight.
    /// </remarks>
    private static string DescribeTie(ScorecardState card)
    {
        var score = $"{card.HomeScore}–{card.AwayScore}";

        var won = card.DecidedBy switch
        {
            FrameWinner.Home => card.HomeTeam,
            FrameWinner.Away => card.AwayTeam,
            _ => null,
        };

        return card.Owner switch
        {
            CardOwner.Live when won is not null =>
                $"Live — {won} have it at {score}, waiting for both captains to sign off",
            CardOwner.Live =>
                $"Live — {score} after {card.FramesPlayed} of {card.FramesTotal}",
            CardOwner.Finalised when won is not null =>
                $"Both captains signed. {won} win {score} — waiting to be collected",
            CardOwner.Finalised => $"Both captains signed {score}, waiting to be collected",
            CardOwner.Claimed => $"Collected — {score}",
            _ => "Not open online",
        };
    }

    private async void OnCupRefreshClicked(object? sender, EventArgs e)
    {
        BuildTies();
        await LoadTiesAsync();
    }

    private async void OnOpenTieClicked(object? sender, EventArgs e)
    {
        var tie = ChosenTie;
        if (tie is null) return;

        var teams = League.Teams.ToDictionary(t => t.Id, t => t.Name ?? "?");
        var format = MatchFormat.From(League.Settings);

        if (!await DisplayAlert("Open this tie for live scoring?",
                $"{tie.CompetitionName} — {tie.RoundName}\n{tie.Describe(teams)}\n{format}\n\n"
                + "The captains toss for home and away on the card, then fill it in turns. "
                + "First to 8 frames wins the tie.\n\n"
                + "The website will own this card until you collect it.",
                "Open", "Cancel"))
            return;

        OpenTieButton.IsEnabled = false;
        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            await ScorecardService.OpenAsync(client, tie.Id, format);
            Report("Open for live scoring. Both captains can now score it at your website's /captain/ page.",
                   error: false);

            await LoadTiesAsync();
        }
        catch (WebApiException ex)
        {
            Report(ex.Code switch
            {
                "already_live" => "That tie is already open for live scoring.",
                "awaiting_claim" => "The captains have finished that tie. Collect it first.",
                "unknown_fixture" => "That tie has not been published to the website yet. "
                                     + "Publish the season from Web Control — cup ties go up with it.",
                _ => ex.Message,
            }, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
        finally
        {
            OpenTieButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Takes a finished cup card back and writes it into the draw.
    /// </summary>
    /// <remarks>
    /// The frames themselves are not kept here: a knockout round records who
    /// won and by how much, and the card on the website stays frozen as the
    /// detailed record of the night.
    /// </remarks>
    private async Task CollectTieAsync(ScorecardState card)
    {
        var tie = _ties.FirstOrDefault(t => t.Id == card.FixtureId);
        if (tie is null) return;

        var again = card.Owner == CardOwner.Claimed;

        if (!await DisplayAlert(again ? "Collect this tie again?" : "Collect this tie?",
                $"{tie.RoundName}\n{card.HomeTeam} v {card.AwayTeam}  {card.HomeScore}–{card.AwayScore}\n\n"
                + (again
                    ? "This was collected before. Reading it again writes the same result into the "
                      + "draw; nothing is duplicated and the website copy stays frozen."
                    : "The result goes into the draw and the winner moves on to the next round."),
                "Collect", "Cancel"))
            return;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var (claimed, frames, wasClaimed) = await ScorecardService.ClaimAsync(client, card.FixtureId);

            // Settle the players first, or the slots naming them land blank.
            var pulled = await CardCollect.ResolvePlayersAsync(
                this, client, _dataStore, League,
                $"{claimed.HomeTeam} v {claimed.AwayTeam}", frames);
            if (pulled is null) return;

            var outcome = await ApplyToTieAsync(tie, claimed);
            var note = pulled > 0 ? $" Took in {pulled} player(s) the captains added." : "";

            Report(wasClaimed
                ? $"Already collected previously; {outcome} Nothing was duplicated."
                : $"Collected. {outcome}{note}", error: false);

            BuildTies();
            await LoadTiesAsync();
        }
        catch (WebApiException ex)
        {
            Report(ex.Code switch
            {
                "still_live" => "That tie is still being scored. Both captains have to sign it off first.",
                _ => ex.Message,
            }, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
    }

    /// <summary>
    /// Writes a collected cup card into the bracket it came out of.
    /// </summary>
    /// <remarks>
    /// The card's home and away are the coin's answer, not the draw's, so the
    /// result is matched back by team id rather than by which column it sits in.
    /// </remarks>
    private async Task<string> ApplyToTieAsync(CupTie tie, ScorecardState card)
    {
        var competition = await _dataStore.GetCompetitionAsync(tie.CompetitionId);
        if (competition is null) return "the competition it belongs to is no longer in the app.";

        var found = CupTie.Locate(competition, tie.Id);
        if (found is null) return "the tie is no longer in that competition's bracket.";

        var (_, round, match) = found.Value;

        // A cup tie is the first to eight, so it can be finished with frames
        // still unplayed. Until somebody has eight it is not a draw - it is a
        // tie that is not over, and putting a winner in would invent one.
        var winner = card.WinnerTeamId;
        if (winner is null)
        {
            return $"{card.HomeTeam} v {card.AwayTeam} finished {card.HomeScore}–{card.AwayScore}, "
                 + "which is nobody's eight frames. Nothing has gone into the draw - "
                 + "reopen the card and finish the tie.";
        }

        CupTie.ApplyScore(match, card.HomeTeamId ?? Guid.Empty, card.HomeScore, card.AwayScore);
        match.WinnerId = winner;
        match.IsComplete = true;
        BracketAdvance.Advance(competition, round, match);

        await _dataStore.UpdateCompetitionAsync(competition);

        var snapshot = League.Competitions.FirstOrDefault(c => c.Id == competition.Id);
        if (snapshot is not null)
        {
            var mirror = CupTie.Locate(snapshot, tie.Id);
            if (mirror is not null)
            {
                CupTie.ApplyScore(mirror.Value.Match, card.HomeTeamId ?? Guid.Empty,
                                  card.HomeScore, card.AwayScore);
                mirror.Value.Match.WinnerId = winner;
                mirror.Value.Match.IsComplete = true;
                BracketAdvance.Advance(snapshot, mirror.Value.Round, mirror.Value.Match);
            }

            DataStore.SaveJsonOnly();
        }

        var name = League.Teams.FirstOrDefault(t => t.Id == winner)?.Name ?? "The winner";

        return $"{name} win {Math.Max(card.HomeScore, card.AwayScore)}–"
             + $"{Math.Min(card.HomeScore, card.AwayScore)} and go through "
             + $"in the {tie.CompetitionName} {tie.RoundName}.";
    }

    private async Task CloseTieAsync(ScorecardState card)
    {
        var scored = card.FramesPlayed > 0;

        var warning = scored
            ? $"{card.FramesPlayed} frame(s) have been scored ({card.HomeScore}–{card.AwayScore}). "
              + "Closing throws that away and cannot be undone. If the tie is finished, ask the "
              + "captains to sign it off and collect it instead."
            : "Nothing has been scored on it yet.";

        if (!await DisplayAlert("Close this card?",
                $"{card.HomeTeam} v {card.AwayTeam}\n\n{warning}\n\n"
                + "The tie goes back to this app and can be opened again.",
                scored ? "Close and lose the scores" : "Close", "Cancel"))
            return;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var closed = await ScorecardService.CloseAsync(client, card.FixtureId);

            Report(closed.HadScoring
                ? $"Closed. {closed.FramesPlayed} scored frame(s) were discarded."
                : "Closed. The tie can be opened again.", error: false);

            await LoadTiesAsync();
        }
        catch (WebApiException ex)
        {
            Report(ex.Code == "already_claimed"
                ? "That tie has already been collected, so there is nothing to close."
                : ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
    }

    private void Report(string message, bool error)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb(error ? "#EF4444" : "#10B981");
    }
}
