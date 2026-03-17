using Wdpl2.ViewModels;

namespace Wdpl2.Views;

public partial class DashboardPage : ContentPage
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnMatchDayClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new MatchDayDashboardPage());
    }

    private async void OnLeagueTablesClicked(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("//Tables"); }
        catch { /* tab may not exist */ }
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("search");
    }

    private async void OnAnalyticsClicked(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("//Analytics"); }
        catch { /* tab may not exist */ }
    }
}
