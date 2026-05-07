using System.Diagnostics;
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
        var page = Application.Current?.Handler?.MauiContext?.Services.GetService<MatchDayDashboardPage>()
            ?? throw new InvalidOperationException("MatchDayDashboardPage not registered");
        await Navigation.PushAsync(page);
    }

    private async void OnLeagueTablesClicked(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("//Tables"); }
        catch (Exception ex) { Debug.WriteLine($"[DashboardPage] Tables tab navigation failed: {ex.Message}"); }
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("search");
    }

    private async void OnAnalyticsClicked(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("//Analytics"); }
        catch (Exception ex) { Debug.WriteLine($"[DashboardPage] Analytics tab navigation failed: {ex.Message}"); }
    }
}
