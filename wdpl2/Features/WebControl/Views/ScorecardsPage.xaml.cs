using Wdpl2.Domain.Fixtures;
using Wdpl2.Models;
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

    private readonly List<Fixture> _openable = new();
    private List<ScorecardState> _states = new();

    public ScorecardsPage()
    {
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
            .OrderBy(f => f.Date));

        FixturePicker.ItemsSource = _openable
            .Select(f =>
            {
                var home = teams.GetValueOrDefault(f.HomeTeamId, "?");
                var away = teams.GetValueOrDefault(f.AwayTeamId, "?");
                return $"{f.Date:ddd dd MMM}  {home} v {away}";
            })
            .ToList();

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
                grid.Add(claim, 1);
            }
            else if (state.Owner == CardOwner.Live)
            {
                var note = new Label
                {
                    Text = "captains scoring",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#94A3B8"),
                    VerticalOptions = LayoutOptions.Center,
                };
                grid.Add(note, 1);
            }

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
        var fixture = _openable[FixturePicker.SelectedIndex];

        // A fixture that already has frames built in the app is authoritative -
        // that IS the card. Otherwise resolve the season's format, using the
        // same precedence the rest of the app uses.
        var season = League.Seasons.FirstOrDefault(s => s.Id == fixture.SeasonId);
        var format = MatchFormat.For(season, League.Settings);

        var framesTotal = fixture.Frames.Count > 0 ? fixture.Frames.Count : format.TotalFrames;
        var maxPerPlayer = Math.Max(1, League.Settings.MaxFramesPerPlayer);

        if (!await DisplayAlert("Open for live scoring?",
                $"{FixturePicker.ItemsSource[FixturePicker.SelectedIndex]}\n{framesTotal} frames\n\n" +
                "The website will own this scorecard until you collect it. This app will not change its frames in the meantime.",
                "Open", "Cancel"))
            return;

        OpenButton.IsEnabled = false;
        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var state = await ScorecardService.OpenAsync(client, fixture, format, maxPerPlayer);
            Report($"Open for live scoring. Captains can now score at your website's /captain/ page.", error: false);
            await LoadStatesAsync();
        }
        catch (WebApiException ex)
        {
            Report(ex.Code switch
            {
                "already_live" => "That match is already open for live scoring.",
                "awaiting_claim" => "That match has been finished by the captains. Collect it first.",
                "unknown_fixture" => "That fixture has not been published to the website yet. Publish the season first.",
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
        if (!await DisplayAlert("Collect this card?",
                $"{state.HomeTeam} v {state.AwayTeam}\nFinished {state.HomeScore}–{state.AwayScore}\n\n" +
                "The result will be written into this season and the website copy frozen.",
                "Collect", "Cancel"))
            return;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var (claimed, frames, already) = await ScorecardService.ClaimAsync(client, state.FixtureId);

            var applied = ApplyToFixture(state.FixtureId, frames);
            DataStore.SaveJsonOnly();

            Report(already
                ? $"Already collected previously; re-applied {applied} frames. Nothing was duplicated."
                : $"Collected. {applied} frames written into the season.", error: false);

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
    /// Writes collected frames into the local fixture.
    /// </summary>
    /// <remarks>
    /// Matching is by frame number, which is the identity both sides agreed on
    /// when the card was opened. Frames the captains left unplayed are left
    /// alone rather than being blanked.
    /// </remarks>
    private static int ApplyToFixture(Guid fixtureId, List<ScorecardService.ClaimedFrame> frames)
    {
        var fixture = League.Fixtures.FirstOrDefault(f => f.Id == fixtureId);
        if (fixture is null) return 0;

        var applied = 0;

        foreach (var claimed in frames.Where(f => f.Winner != FrameWinner.None))
        {
            var frame = fixture.Frames.FirstOrDefault(f => f.Number == claimed.Number);
            if (frame is null)
            {
                frame = new FrameResult { Number = claimed.Number };
                fixture.Frames.Add(frame);
            }

            frame.Winner = claimed.Winner;
            frame.EightBall = claimed.EightBall;
            if (claimed.HomePlayerId.HasValue) frame.HomePlayerId = claimed.HomePlayerId;
            if (claimed.AwayPlayerId.HasValue) frame.AwayPlayerId = claimed.AwayPlayerId;
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
