using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class LiveScoresSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public LiveScoresSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;

        EnableCheck.IsChecked = settings.ShowLiveScores;
        ShowOnHomeCheck.IsChecked = settings.LiveScoresShowOnHome;
        ShowFrameDetailCheck.IsChecked = settings.LiveScoresShowFrameDetail;

        NavLabelEntry.Text = settings.LiveScoresNavLabel;
        PageTitleEntry.Text = settings.LiveScoresPageTitle;
        EmptyMessageEntry.Text = settings.LiveScoresEmptyMessage;

        RefreshSecondsEntry.Text = settings.LiveScoresRefreshSeconds.ToString();
        ApiUrlEntry.Text = settings.LiveScoresApiBaseUrl;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;

            settings.ShowLiveScores = EnableCheck.IsChecked;
            settings.LiveScoresShowOnHome = ShowOnHomeCheck.IsChecked;
            settings.LiveScoresShowFrameDetail = ShowFrameDetailCheck.IsChecked;

            if (!string.IsNullOrWhiteSpace(NavLabelEntry.Text))
                settings.LiveScoresNavLabel = NavLabelEntry.Text.Trim();
            if (!string.IsNullOrWhiteSpace(PageTitleEntry.Text))
                settings.LiveScoresPageTitle = PageTitleEntry.Text.Trim();
            settings.LiveScoresEmptyMessage = (EmptyMessageEntry.Text ?? "").Trim();

            // Clamp to the same range the generator enforces so the UI never
            // silently disagrees with the emitted JavaScript.
            if (int.TryParse(RefreshSecondsEntry.Text, out var seconds))
                settings.LiveScoresRefreshSeconds = Math.Clamp(seconds, 5, 300);

            settings.LiveScoresApiBaseUrl = (ApiUrlEntry.Text ?? "").Trim();

            DataStore.Save();

            await DisplayAlert("Saved", "Live scores settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
