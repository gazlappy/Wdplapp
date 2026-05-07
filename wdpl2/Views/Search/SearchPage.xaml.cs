using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class SearchPage : ContentPage
{
    private const int SearchDebounceMs = 250;
    private readonly IDataStore _dataStore;
    private CancellationTokenSource? _searchCts;

    public SearchPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        InitializeComponent();
    }

    private void OnSearch(object sender, EventArgs e)
    {
        // Explicit search button click — bypass debounce.
        _searchCts?.Cancel();
        PerformSearch(SearchInput.Text);
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchCts?.Cancel();

        // Live search after 2+ characters
        if ((e.NewTextValue?.Length ?? 0) < 2)
        {
            ResultsList.ItemsSource = null;
            return;
        }

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        var query = e.NewTextValue;

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(SearchDebounceMs), () =>
        {
            if (cts.IsCancellationRequested) return;
            PerformSearch(query);
        });
    }

    private void PerformSearch(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        var data = _dataStore.GetData();
        var seasonId = SeasonService.Current.CurrentSeasonId;
        var results = SearchService.Search(data, query, seasonId);
        ResultsList.ItemsSource = results;
    }

    private async void OnResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SearchService.SearchResult result)
            return;

        // Deselect immediately to prevent re-tap during navigation.
        ((CollectionView)sender).SelectedItem = null;

        // Navigate based on type
        var route = result.Type switch
        {
            "Player" => "players",
            "Team" => "teams",
            "Venue" => "venues",
            "Division" => "divisions",
            "Season" => "seasons",
            _ => null
        };

        if (route != null)
        {
            await Shell.Current.GoToAsync($"//{route}");
        }
    }
}
