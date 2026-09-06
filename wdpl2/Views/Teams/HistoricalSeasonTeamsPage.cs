using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Views;

public sealed class HistoricalSeasonTeamsPage : ContentPage
{
    private readonly HistoricalSeasonTeams _draft;
    private readonly Picker _season = new() { Title = "Previous season", ItemDisplayBinding = new Binding(nameof(Season.Name)) };
    private readonly Entry _search = new() { Placeholder = "Search teams" };
    private readonly VerticalStackLayout _available = new() { Spacing = 8 };
    private readonly VerticalStackLayout _review = new() { Spacing = 8 };
    private readonly Label _summary = new();
    private readonly Label _status = new();
    private readonly Button _save = new() { Text = "Add selected teams", BackgroundColor = Color.FromArgb("#16634B"), TextColor = Colors.White };
    private readonly VerticalStackLayout _content;
    private int _sourcePage;
    private int _reviewPage;
    private bool _busy;
    private bool _saved;

    public HistoricalSeasonTeamsPage(IDataStore store, Guid seasonId)
    {
        _draft = new HistoricalSeasonTeams(store, seasonId);
        Title = "Add previous-season teams";
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F5F7F6"), Color.FromArgb("#111A17"));
        _season.ItemsSource = _draft.Preview.Seasons.Where(s => s.Id != seasonId)
            .OrderByDescending(s => s.StartDate).ThenBy(s => s.Name).ToList();
        _season.SelectedIndexChanged += (_, _) => { _sourcePage = 0; Refresh(); };
        _search.TextChanged += (_, _) => { _sourcePage = 0; Refresh(); };
        _save.Clicked += OnSave;
        var destination = _draft.Preview.Seasons.Single(s => s.Id == seasonId).Name;
        _content = new VerticalStackLayout
        {
            Padding = 20, Spacing = 12,
            Children =
            {
                new Label { Text = "Add returning teams", FontSize = 28, FontAttributes = FontAttributes.Bold },
                new Label { Text = $"Destination: {destination}", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Copy team names, food settings and logo references from one or more previous seasons. Historical records stay unchanged and the destination season is not activated." },
                new Label { Text = "Divisions, venues, tables, captains, credentials, players and results are not copied. Configure placements afterwards; select a copied team to add its previous-season players." },
                new Label { Text = "1. Choose teams", FontSize = 18, FontAttributes = FontAttributes.Bold },
                _season, _search, _available,
                new Label { Text = "2. Review additions", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Teams with the same explicit identity are selected only once. Matching names alone do not identify the same team and will not be merged." },
                _summary, _review, _status, _save
            }
        };
        Content = new ScrollView { Content = _content };
        Refresh();
    }

    private string Describe(Team team)
    {
        var season = _draft.Preview.Seasons.FirstOrDefault(s => s.Id == team.SeasonId)?.Name;
        var division = _draft.Preview.Divisions.FirstOrDefault(d => d.Id == team.DivisionId)?.Name ?? "No division";
        var venue = _draft.Preview.Venues.FirstOrDefault(v => v.Id == team.VenueId)?.Name ?? "No venue";
        return $"{team.Name}\nFrom {season} · {division} · {venue}";
    }

    private void Edit(Action edit)
    {
        if (_busy || _saved) return;
        try { edit(); _status.Text = "Selection updated. Nothing saved yet."; Refresh(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { _status.Text = ex.Message; }
    }

    private void Refresh()
    {
        _available.Children.Clear();
        var teams = _season.SelectedItem is Season season ? _draft.SourceTeams(season.Id, _search.Text) : new List<Team>();
        var page = new ReviewPagination<Team>(teams, _sourcePage, 10);
        _sourcePage = page.PageIndex;
        _available.Children.Add(Pager(page.PageIndex, page.PageCount, i => { _sourcePage = i; Refresh(); }));
        foreach (var team in page.Items)
        {
            var reason = _draft.UnavailableReason(team);
            var selected = _draft.IsSelected(team);
            var add = new Button { Text = selected ? "Selected" : "Select team", IsEnabled = reason == null && !selected };
            add.Clicked += (_, _) => Edit(() => _draft.Add(team.Id));
            _available.Children.Add(Card(Describe(team) + (reason == null ? "" : $"\n{reason}"), add));
        }
        if (teams.Count == 0) _available.Children.Add(new Label { Text = "Choose a previous season with teams, or adjust the search." });
        _review.Children.Clear();
        _summary.Text = $"{_draft.Selected.Count} team(s) selected. Nothing saved yet.";
        var review = new ReviewPagination<Team>(_draft.Selected, _reviewPage, 10);
        _reviewPage = review.PageIndex;
        _review.Children.Add(Pager(review.PageIndex, review.PageCount, i => { _reviewPage = i; Refresh(); }));
        foreach (var team in review.Items)
        {
            var sameName = _draft.Preview.Teams.Any(t => t.SeasonId == _draft.SeasonId && string.Equals(t.Name?.Trim(), team.Name?.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                _draft.Selected.Any(t => t.Id != team.Id && string.Equals(t.Name?.Trim(), team.Name?.Trim(), StringComparison.OrdinalIgnoreCase));
            var warning = sameName ? "\nA team with this name already exists or is selected, but has a different identity. These will remain separate." : "";
            var remove = new Button { Text = "Remove selection" };
            remove.Clicked += (_, _) => Edit(() => _draft.Remove(team.Id));
            _review.Children.Add(Card($"{Describe(team)}\nNew-season placements will be unassigned.{warning}", remove));
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
            var destination = _draft.Preview.Seasons.Single(s => s.Id == _draft.SeasonId);
            if (!await DisplayAlert("Add teams?", $"Add {_draft.Selected.Count} team(s) to '{destination.Name}'? Previous seasons will remain unchanged. Players and placements will not be copied.", "Add teams", "Cancel")) return;
            await _draft.SaveAsync();
            _saved = true;
            await DisplayAlert("Teams added", "Configure the teams' divisions, venues and tables. Select a team to add players from previous seasons.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            await DisplayAlert("Teams not added", ex.Message, "OK");
        }
        finally { _busy = false; _content.IsEnabled = !_saved; }
    }

    protected override bool OnBackButtonPressed() => _busy || base.OnBackButtonPressed();
}
