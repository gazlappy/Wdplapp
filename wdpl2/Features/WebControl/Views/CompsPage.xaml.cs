using System.Text;
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

    public CompsPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCompetitionsAsync();
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
    /// Team knockouts are left out: those are matches between teams, played on
    /// the league's own scorecards, not a night one player organises.
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

        _competitions.RemoveAll(c => c.Format == CompetitionFormat.TeamKnockout);

        CompetitionPicker.ItemsSource = _competitions.Select(c => c.Name).ToList();

        if (_competitions.Count == 0)
        {
            SummaryLabel.Text = "No singles or doubles competition in this league yet.";
            return;
        }

        CompetitionPicker.SelectedIndex = 0;
        BuildRows();
    }

    private Competition? Selected =>
        CompetitionPicker.SelectedIndex >= 0 && CompetitionPicker.SelectedIndex < _competitions.Count
            ? _competitions[CompetitionPicker.SelectedIndex]
            : null;

    private void OnCompetitionChanged(object? sender, EventArgs e) => BuildRows();

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
        SessionList.Children.Clear();
        _rows.Clear();

        var competition = Selected;
        if (competition is null) return;

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
            BuildLive();
            Report("Read back from the website.", error: false);
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

    private void BuildLive()
    {
        LiveList.Children.Clear();
        NoLiveLabel.IsVisible = _live.Count == 0;

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
                    : $"{state.Played} of {state.Matches} matches played · {state.Present} here"
                      + (state.OrganiserName is null ? "" : $" · run by {state.OrganiserName}"),
                FontSize = 11,
                TextColor = Color.FromArgb(state.Collected ? "#94A3B8" : "#475569"),
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

    private void Report(string message, bool error)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb(error ? "#EF4444" : "#10B981");
    }
}
