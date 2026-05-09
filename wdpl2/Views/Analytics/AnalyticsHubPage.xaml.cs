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
        var page = Application.Current?.Handler?.MauiContext?.Services.GetService<CareerStatsPage>()
            ?? throw new System.InvalidOperationException("CareerStatsPage not registered");
        await Navigation.PushAsync(page);
    }

    private async void OnFrameStatsClicked(object? sender, System.EventArgs e)
    {
        var page = Application.Current?.Handler?.MauiContext?.Services.GetService<FrameStatsPage>()
            ?? throw new System.InvalidOperationException("FrameStatsPage not registered");
        await Navigation.PushAsync(page);
    }

    private async void OnAchievementsClicked(object? sender, System.EventArgs e)
    {
        var page = Application.Current?.Handler?.MauiContext?.Services.GetService<AchievementsPage>()
            ?? throw new System.InvalidOperationException("AchievementsPage not registered");
        await Navigation.PushAsync(page);
    }

    private async void OnSeasonAwardsClicked(object? sender, System.EventArgs e)
    {
        var page = Application.Current?.Handler?.MauiContext?.Services.GetService<SeasonAwardsPage>()
            ?? throw new System.InvalidOperationException("SeasonAwardsPage not registered");
        await Navigation.PushAsync(page);
    }

    private async void OnMatchDayClicked(object? sender, System.EventArgs e)
    {
        var page = Application.Current?.Handler?.MauiContext?.Services.GetService<MatchDayDashboardPage>()
            ?? throw new System.InvalidOperationException("MatchDayDashboardPage not registered");
        await Navigation.PushAsync(page);
    }

    private async void OnTeamAnalyticsClicked(object? sender, System.EventArgs e)
    {
        var page = Application.Current?.Handler?.MauiContext?.Services.GetService<TeamAnalyticsPage>()
            ?? throw new System.InvalidOperationException("TeamAnalyticsPage not registered");
        await Navigation.PushAsync(page);
    }

    private async void OnWhatIfClicked(object? sender, System.EventArgs e)
    {
        var page = Application.Current?.Handler?.MauiContext?.Services.GetService<WhatIfSimulatorPage>()
            ?? throw new System.InvalidOperationException("WhatIfSimulatorPage not registered");
        await Navigation.PushAsync(page);
    }

    private async void OnSeasonComparisonClicked(object? sender, System.EventArgs e)
    {
        var page = Application.Current?.Handler?.MauiContext?.Services.GetService<SeasonComparisonPage>()
            ?? throw new System.InvalidOperationException("SeasonComparisonPage not registered");
        await Navigation.PushAsync(page);
    }
}
