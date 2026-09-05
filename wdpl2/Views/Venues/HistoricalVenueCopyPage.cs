using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Views;

public sealed class HistoricalVenueCopyPage : ContentPage
{
    private readonly HistoricalVenueCopy _copy;
    private readonly Picker _source = new() { Title = "Previous season", ItemDisplayBinding = new Binding(nameof(Season.Name)) };
    private readonly Entry _search = new() { Placeholder = "Search source venues" };
    private readonly VerticalStackLayout _available = new() { Spacing = 8 };
    private readonly VerticalStackLayout _review = new() { Spacing = 8 };
    private readonly Label _summary = new();
    private readonly Label _status = new();
    private readonly Button _import = new() { Text = "Import selected venues", BackgroundColor = Color.FromArgb("#16634B"), TextColor = Colors.White };
    private readonly VerticalStackLayout _content;
    private int _sourcePage;
    private int _reviewPage;
    private bool _busy;
    private bool _saved;

    public HistoricalVenueCopyPage(IDataStore store, Guid destinationId)
    {
        _copy = new HistoricalVenueCopy(store, destinationId);
        var destination = _copy.Preview.Seasons.Single(s => s.Id == destinationId);
        Title = "Copy previous-season venues";
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F5F7F6"), Color.FromArgb("#111A17"));
        _source.ItemsSource = _copy.Preview.Seasons.Where(s => s.Id != destinationId)
            .OrderByDescending(s => s.StartDate).ThenBy(s => s.Name).ToList();
        _source.SelectedIndexChanged += (_, _) => { _sourcePage = 0; Refresh(); };
        _search.TextChanged += (_, _) => { _sourcePage = 0; Refresh(); };
        _import.Clicked += OnImport;
        _content = new VerticalStackLayout
        {
            Padding = 20, Spacing = 12,
            Children =
            {
                new Label { Text = "Copy venues", FontSize = 28, FontAttributes = FontAttributes.Bold },
                new Label { Text = $"Destination: {destination.Name}", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Copies names, addresses, notes and tables with new IDs. Previous seasons and team assignments stay unchanged. The destination is not activated." },
                new Label { Text = "1. Choose venues", FontSize = 18, FontAttributes = FontAttributes.Bold },
                _source, _search, _available,
                new Label { Text = "2. Review selections", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Existing venues are never merged or overwritten. Importing a venue again creates another copy. Check matching-name warnings before continuing." },
                _summary, _review, _status, _import
            }
        };
        Content = new ScrollView { Content = _content };
        Refresh();
    }

    private string Details(Venue venue) => $"{venue.Name}\n{venue.Address}\n{venue.Notes}\nTables: " +
        (venue.Tables.Count == 0 ? "None" : string.Join(", ", venue.Tables.Select(t => $"{t.Label} (max {t.MaxTeams} teams)")));

    private void Refresh()
    {
        _available.Children.Clear();
        var venues = _copy.Preview.Venues.Where(v => v.SeasonId == (_source.SelectedItem as Season)?.Id && v.SeasonId.HasValue)
            .Where(v => string.IsNullOrWhiteSpace(_search.Text) || v.Name.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => v.Name).ThenBy(v => v.Id).ToList();
        var sourcePage = new ReviewPagination<Venue>(venues, _sourcePage, 10);
        _sourcePage = sourcePage.PageIndex;
        _available.Children.Add(Pager(sourcePage.PageIndex, sourcePage.PageCount, i => { _sourcePage = i; Refresh(); }));
        foreach (var venue in sourcePage.Items)
        {
            var add = new Button { Text = _copy.IsSelected(venue.Id) ? "Selected" : "Select venue", IsEnabled = !_copy.IsSelected(venue.Id) };
            add.Clicked += (_, _) => { _copy.Add(venue.Id); Refresh(); };
            _available.Children.Add(Card(Details(venue), add));
        }
        if (venues.Count == 0) _available.Children.Add(new Label { Text = "Choose a previous season with venues, or adjust the search." });
        _review.Children.Clear();
        _summary.Text = $"{_copy.Selected.Count} venue(s) selected. Nothing saved yet.";
        var reviewPage = new ReviewPagination<Venue>(_copy.Selected, _reviewPage, 10);
        _reviewPage = reviewPage.PageIndex;
        _review.Children.Add(Pager(reviewPage.PageIndex, reviewPage.PageCount, i => { _reviewPage = i; Refresh(); }));
        foreach (var venue in reviewPage.Items)
        {
            var source = _copy.SourceFor(venue.Id);
            var season = _copy.Preview.Seasons.Single(s => s.Id == source.SeasonId);
            var matching = _copy.Preview.Venues.Where(v => v.SeasonId == _copy.DestinationId && string.Equals(v.Name.Trim(), venue.Name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            var warning = matching.Count == 0 ? "" : "\nAlready in destination (will NOT be merged): " + string.Join("; ", matching.Select(v => $"{v.Name}, {v.Address}, {v.Tables.Count} table(s)"));
            if (_copy.Selected.Any(v => v.Id != venue.Id && string.Equals(v.Name.Trim(), venue.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                warning += "\nAnother selected venue has the same name; these will remain separate.";
            var remove = new Button { Text = "Remove selection" };
            remove.Clicked += (_, _) => { _copy.Remove(venue.Id); Refresh(); };
            _review.Children.Add(Card($"From {season.Name}\n{Details(venue)}{warning}", remove));
        }
        _import.IsEnabled = _copy.Selected.Count > 0 && !_saved;
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

    private async void OnImport(object? sender, EventArgs e)
    {
        if (_busy || _saved) return;
        _busy = true;
        _content.IsEnabled = false;
        try
        {
            var destination = _copy.Preview.Seasons.Single(s => s.Id == _copy.DestinationId);
            if (!await DisplayAlert("Import venues?", $"Copy {_copy.Selected.Count} venues into '{destination.Name}'? Existing venues will not be merged or replaced.", "Import", "Cancel")) return;
            await _copy.SaveAsync();
            _saved = true;
            await DisplayAlert("Venues imported", "The selected venues and tables have been copied. Assign teams to the new venues/tables separately.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            await DisplayAlert("Could not import venues", ex.Message, "OK");
        }
        finally
        {
            _busy = false;
            _content.IsEnabled = !_saved;
        }
    }

    protected override bool OnBackButtonPressed() => _busy || base.OnBackButtonPressed();
}
