using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Views;

public sealed class HistoricalTeamPlayersPage : ContentPage
{
    private sealed record TeamOption(Guid? Id, string Name);
    private readonly HistoricalTeamPlayers _draft;
    private readonly Picker _season = new() { Title = "Previous season", ItemDisplayBinding = new Binding(nameof(Season.Name)) };
    private readonly Picker _team = new() { Title = "Their team in that season", ItemDisplayBinding = new Binding("Name") };
    private readonly Entry _search = new() { Placeholder = "Search players" };
    private readonly VerticalStackLayout _available = new() { Spacing = 8 };
    private readonly VerticalStackLayout _review = new() { Spacing = 8 };
    private readonly Label _summary = new();
    private readonly Label _status = new();
    private readonly Button _save = new() { Text = "Add selected players", BackgroundColor = Color.FromArgb("#16634B"), TextColor = Colors.White };
    private readonly VerticalStackLayout _content;
    private int _sourcePage;
    private int _reviewPage;
    private bool _busy;
    private bool _saved;

    public HistoricalTeamPlayersPage(IDataStore store, Guid teamId)
    {
        _draft = new HistoricalTeamPlayers(store, teamId);
        Title = "Add previous-season players";
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F5F7F6"), Color.FromArgb("#111A17"));
        _season.ItemsSource = _draft.Preview.Seasons.Where(s => s.Id != _draft.SeasonId)
            .OrderByDescending(s => s.StartDate).ThenBy(s => s.Name).ToList();
        _season.SelectedIndexChanged += (_, _) =>
        {
            _sourcePage = 0;
            var options = _draft.Preview.Teams.Where(t => t.SeasonId == (_season.SelectedItem as Season)?.Id && t.SeasonId.HasValue)
                .OrderBy(t => t.Name).Select(t => new TeamOption(t.Id, t.Name ?? "Unnamed team")).ToList();
            options.Add(new TeamOption(null, "Unassigned players"));
            _team.ItemsSource = options;
            _team.SelectedIndex = -1;
            Refresh();
        };
        _team.SelectedIndexChanged += (_, _) => { _sourcePage = 0; Refresh(); };
        _search.TextChanged += (_, _) => { _sourcePage = 0; Refresh(); };
        _save.Clicked += OnSave;
        var seasonName = _draft.Preview.Seasons.Single(s => s.Id == _draft.SeasonId).Name;
        _content = new VerticalStackLayout
        {
            Padding = 20, Spacing = 12,
            Children =
            {
                new Label { Text = "Add players to team", FontSize = 28, FontAttributes = FontAttributes.Bold },
                new Label { Text = $"Destination: {_draft.Destination.Name} — {seasonName}", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Choose returning players from one or more seasons. Historical teams, results and rosters stay unchanged. New copies start active without old transfers or availability." },
                new Label { Text = "1. Choose players", FontSize = 18, FontAttributes = FontAttributes.Bold },
                _season, _team, _search, _available,
                new Label { Text = "2. Review additions", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Matching unassigned players in the destination season will be assigned without resetting their existing status or history. Players already assigned elsewhere require a transfer on Players. Matching names alone are never merged." },
                _summary, _review, _status, _save
            }
        };
        Content = new ScrollView { Content = _content };
        Refresh();
    }

    private string SourceDescription(Player player)
    {
        var season = _draft.Preview.Seasons.FirstOrDefault(s => s.Id == player.SeasonId)?.Name;
        var team = _draft.Preview.Teams.FirstOrDefault(t => t.Id == player.TeamId)?.Name ?? "Unassigned players";
        return $"{player.Name}\n{team} — {season}";
    }

    private void Edit(Action edit)
    {
        if (_busy || _saved) return;
        try { edit(); _status.Text = "Selection updated. Nothing saved yet."; Refresh(); }
        catch (InvalidOperationException ex) { _status.Text = ex.Message; }
    }

    private void Refresh()
    {
        _available.Children.Clear();
        var players = _season.SelectedItem is Season season && _team.SelectedItem is TeamOption team
            ? _draft.SourcePlayers(season.Id, team.Id, _search.Text) : new List<Player>();
        var page = new ReviewPagination<Player>(players, _sourcePage, 10);
        _sourcePage = page.PageIndex;
        _available.Children.Add(Pager(page.PageIndex, page.PageCount, i => { _sourcePage = i; Refresh(); }));
        foreach (var player in page.Items)
        {
            var reason = _draft.UnavailableReason(player);
            var selected = _draft.IsSelected(player);
            var add = new Button { Text = selected ? "Selected" : "Select player", IsEnabled = reason == null && !selected };
            add.Clicked += (_, _) => Edit(() => _draft.Add(player.Id));
            _available.Children.Add(Card(SourceDescription(player) + (reason == null ? "" : $"\n{reason}"), add));
        }
        if (players.Count == 0) _available.Children.Add(new Label { Text = "Choose a previous season and team, or adjust the search. No players found for the current filters." });
        _review.Children.Clear();
        _summary.Text = $"{_draft.Selected.Count} player(s) selected for {_draft.Destination.Name}. Nothing saved yet.";
        var review = new ReviewPagination<Player>(_draft.Selected, _reviewPage, 10);
        _reviewPage = review.PageIndex;
        _review.Children.Add(Pager(review.PageIndex, review.PageCount, i => { _reviewPage = i; Refresh(); }));
        foreach (var player in review.Items)
        {
            var action = _draft.WillAssignExisting(player) ? "Assign existing unassigned player" : "Create new-season player";
            var sameName = _draft.Preview.Players.Any(p => p.SeasonId == _draft.SeasonId && string.Equals(p.Name.Trim(), player.Name.Trim(), StringComparison.OrdinalIgnoreCase));
            var warning = sameName && !_draft.WillAssignExisting(player) ? "\nSame name exists in destination; no identity match, so a separate player will be created." : "";
            var remove = new Button { Text = "Remove selection" };
            remove.Clicked += (_, _) => Edit(() => _draft.Remove(player.Id));
            _review.Children.Add(Card($"{SourceDescription(player)}\n{action}{warning}", remove));
        }
        _save.IsEnabled = _draft.Selected.Count > 0 && !_saved;
    }

    private static View Card(string text, Button action)
    {
        var border = new Border
        {
            Padding = 12, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Content = new VerticalStackLayout { Spacing = 8, Children = { new Label { Text = text }, action } }
        };
        border.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#1F2924"));
        return border;
    }

    private static View Pager(int index, int count, Action<int> navigate)
    {
        var previous = new Button { Text = "Previous", IsEnabled = index > 0 };
        var next = new Button { Text = "Next", IsEnabled = index + 1 < count };
        previous.Clicked += (_, _) => navigate(index - 1);
        next.Clicked += (_, _) => navigate(index + 1);
        return new HorizontalStackLayout { Spacing = 8, Children = { previous, new Label { Text = $"{index + 1} / {count}", VerticalOptions = LayoutOptions.Center }, next } };
    }

    private async void OnSave(object? sender, EventArgs e)
    {
        if (_busy || _saved) return;
        _busy = true;
        _content.IsEnabled = false;
        try
        {
            var season = _draft.Preview.Seasons.Single(s => s.Id == _draft.SeasonId);
            if (!await DisplayAlert("Add players?", $"Add {_draft.Selected.Count} player(s) to '{_draft.Destination.Name}' in '{season.Name}'? Previous seasons will remain unchanged.", "Add players", "Cancel")) return;
            await _draft.SaveAsync();
            _saved = true;
            await DisplayAlert("Players added", "The selected players have been added to the team.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            await DisplayAlert("Players not added", ex.Message, "OK");
        }
        finally { _busy = false; _content.IsEnabled = !_saved; }
    }

    protected override bool OnBackButtonPressed() => _busy || base.OnBackButtonPressed();
}
