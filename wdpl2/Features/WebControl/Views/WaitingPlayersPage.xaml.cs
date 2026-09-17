using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Web;

namespace Wdpl2.Views.WebControl;

/// <summary>
/// Players captains added on the website, and the way to bring them home.
/// </summary>
/// <remarks>
/// Collecting a scorecard already picks up anyone it names, but a captain can
/// add a player to their squad without that player being on a card - someone
/// who turned up and did not get a frame, or a signing put on before the night.
/// Those would sit on the website indefinitely, so they need a place of their
/// own rather than a corner of the page about PINs.
/// <para>
/// The list is read on opening rather than behind a button: a page about what
/// is waiting should say what is waiting.
/// </para>
/// </remarks>
public partial class WaitingPlayersPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private readonly IDataStore _dataStore;
    private List<CaptainRosterService.AddedPlayer> _waiting = new();

    public WaitingPlayersPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        RefreshButton.IsEnabled = false;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            _waiting = await CaptainRosterService.GetUncollectedAsync(client);
            Render();
            Report("", error: false);
        }
        catch (WebApiException ex)
        {
            CountLabel.Text = "Could not read the website.";
            Report(ex.Code == "unknown_module"
                ? "The website does not have the captains module yet. Deploy the backend, then install tables."
                : ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            CountLabel.Text = "Could not read the website.";
            Report(ex.Message, error: true);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void Render()
    {
        WaitingList.Children.Clear();

        EmptyLabel.IsVisible = _waiting.Count == 0;
        CollectButton.IsEnabled = _waiting.Count > 0;

        CountLabel.Text = _waiting.Count switch
        {
            0 => "Nobody waiting",
            1 => "1 player waiting",
            _ => $"{_waiting.Count} players waiting",
        };

        // Grouped by team, because that is how the secretary checks them: a
        // name means little on its own and a lot next to the side that added it.
        foreach (var team in _waiting.GroupBy(p => p.TeamName).OrderBy(g => g.Key))
        {
            var names = new VerticalStackLayout { Spacing = 2 };

            names.Add(new Label
            {
                Text = team.Key,
                FontAttributes = FontAttributes.Bold,
                FontSize = 13,
                TextColor = Color.FromArgb("#1E293B"),
            });

            foreach (var player in team.OrderBy(p => p.Name))
            {
                names.Add(new Label
                {
                    Text = player.Describe() + (player.IsActive ? "" : " (retired by the captain)"),
                    FontSize = 11,
                    TextColor = Color.FromArgb("#64748B"),
                });
            }

            WaitingList.Children.Add(new Frame
            {
                BorderColor = Color.FromArgb("#E2E8F0"),
                BackgroundColor = Color.FromArgb("#F8FAFC"),
                CornerRadius = 8,
                Padding = new Thickness(12, 10),
                HasShadow = false,
                Content = names,
            });
        }
    }

    private async void OnCollectClicked(object? sender, EventArgs e)
    {
        if (_waiting.Count == 0) return;

        CollectButton.IsEnabled = false;

        try
        {
            // A waiting player belongs to whichever season their team is in, and
            // the suggestions are matched within that season - so the squad has
            // to cover every season the list touches, not just the current one.
            var squad = new List<Player>();
            foreach (var seasonId in _waiting.Select(p => p.SeasonId).Distinct())
            {
                squad.AddRange(await _dataStore.GetPlayersAsync(seasonId));
            }

            var decisions = await CollectPlayersPage.AskAsync(
                this, _waiting, squad,
                _waiting.Count == 1
                    ? "One player is waiting to come into the app."
                    : $"{_waiting.Count} players are waiting to come into the app.");

            if (decisions is null || decisions.Count == 0)
            {
                Report("Nothing collected.", error: false);
                return;
            }

            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var (created, linked) = await CaptainRosterService.CollectAsync(
                client, _dataStore, League, decisions);

            var left = _waiting.Count - decisions.Count;

            _waiting.RemoveAll(p => decisions.Any(d => d.Player.Id == p.Id));
            Render();

            Report(Describe(created, linked, left), error: false);
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
            CollectButton.IsEnabled = _waiting.Count > 0;
        }
    }

    private static string Describe(int created, int linked, int left)
    {
        var parts = new List<string>();
        if (created > 0) parts.Add($"added {created} new player(s)");
        if (linked > 0) parts.Add($"linked {linked} to players already here");
        if (left > 0) parts.Add($"left {left} waiting");

        if (parts.Count == 0) return "Nothing collected.";

        var said = string.Join(", ", parts);
        return char.ToUpper(said[0]) + said[1..]
               + ". Publish the season to finish tying them in.";
    }

    private void Report(string message, bool error)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb(error ? "#EF4444" : "#10B981");
    }
}
