using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class SearchPage : ContentPage
{
    public SearchPage()
    {
        InitializeComponent();
    }

    private void OnSearch(object sender, EventArgs e)
    {
        PerformSearch(SearchInput.Text);
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        // Live search after 2+ characters
        if ((e.NewTextValue?.Length ?? 0) >= 2)
            PerformSearch(e.NewTextValue);
        else
            ResultsList.ItemsSource = null;
    }

    private void PerformSearch(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        var data = DataStore.Data;
        var seasonId = SeasonService.Current.CurrentSeasonId;
        var results = SearchService.Search(data, query, seasonId);
        ResultsList.ItemsSource = results;
    }

    private async void OnResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SearchService.SearchResult result)
            return;

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

        // Deselect
        ((CollectionView)sender).SelectedItem = null;
    }
}
