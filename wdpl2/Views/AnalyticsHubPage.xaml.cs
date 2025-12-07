using Microsoft.Maui.Controls;

namespace Wdpl2.Views;

public partial class AnalyticsHubPage : ContentPage
{
    public AnalyticsHubPage()
    {
        InitializeComponent();
    }

    private async void OnCareerStatsClicked(object? sender, System.EventArgs e)
    {
        await Navigation.PushAsync(new CareerStatsPage());
    }

    private async void OnFrameStatsClicked(object? sender, System.EventArgs e)
    {
        await Navigation.PushAsync(new FrameStatsPage());
    }

    private async void OnAchievementsClicked(object? sender, System.EventArgs e)
    {
        await Navigation.PushAsync(new AchievementsPage());
    }

    private async void OnSeasonAwardsClicked(object? sender, System.EventArgs e)
    {
        await Navigation.PushAsync(new SeasonAwardsPage());
    }

    private async void OnMatchDayClicked(object? sender, System.EventArgs e)
    {
        await Navigation.PushAsync(new MatchDayDashboardPage());
    }

    private async void OnTeamAnalyticsClicked(object? sender, System.EventArgs e)
    {
        await Navigation.PushAsync(new TeamAnalyticsPage());
    }

    private async void OnWhatIfClicked(object? sender, System.EventArgs e)
    {
        await Navigation.PushAsync(new WhatIfSimulatorPage());
    }
}
