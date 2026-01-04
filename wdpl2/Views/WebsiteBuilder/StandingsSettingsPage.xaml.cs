using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class StandingsSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public StandingsSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
        
        HighlightTopCheck.CheckedChanged += (s, e) => TopHighlightOptions.IsVisible = e.Value;
        HighlightBottomCheck.CheckedChanged += (s, e) => BottomHighlightOptions.IsVisible = e.Value;
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        // Columns
        ShowPositionCheck.IsChecked = settings.StandingsShowPosition;
        ShowPlayedCheck.IsChecked = settings.StandingsShowPlayed;
        ShowWonCheck.IsChecked = settings.StandingsShowWon;
        ShowDrawnCheck.IsChecked = settings.StandingsShowDrawn;
        ShowLostCheck.IsChecked = settings.StandingsShowLost;
        ShowFramesForCheck.IsChecked = settings.StandingsShowFramesFor;
        ShowFramesAgainstCheck.IsChecked = settings.StandingsShowFramesAgainst;
        ShowFramesDiffCheck.IsChecked = settings.StandingsShowFramesDiff;
        ShowPointsCheck.IsChecked = settings.StandingsShowPoints;
        ShowFormCheck.IsChecked = settings.StandingsShowForm;
        
        // Visual
        ShowMedalsCheck.IsChecked = settings.StandingsShowMedals;
        
        // Highlights
        HighlightTopCheck.IsChecked = settings.StandingsHighlightTop;
        HighlightTopCountEntry.Text = settings.StandingsHighlightTopCount.ToString();
        TopHighlightColorEntry.Text = settings.StandingsTopHighlightColor;
        TopHighlightOptions.IsVisible = settings.StandingsHighlightTop;
        
        HighlightBottomCheck.IsChecked = settings.StandingsHighlightBottom;
        HighlightBottomCountEntry.Text = settings.StandingsHighlightBottomCount.ToString();
        BottomHighlightColorEntry.Text = settings.StandingsBottomHighlightColor;
        BottomHighlightOptions.IsVisible = settings.StandingsHighlightBottom;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            // Columns
            settings.StandingsShowPosition = ShowPositionCheck.IsChecked;
            settings.StandingsShowPlayed = ShowPlayedCheck.IsChecked;
            settings.StandingsShowWon = ShowWonCheck.IsChecked;
            settings.StandingsShowDrawn = ShowDrawnCheck.IsChecked;
            settings.StandingsShowLost = ShowLostCheck.IsChecked;
            settings.StandingsShowFramesFor = ShowFramesForCheck.IsChecked;
            settings.StandingsShowFramesAgainst = ShowFramesAgainstCheck.IsChecked;
            settings.StandingsShowFramesDiff = ShowFramesDiffCheck.IsChecked;
            settings.StandingsShowPoints = ShowPointsCheck.IsChecked;
            settings.StandingsShowForm = ShowFormCheck.IsChecked;
            
            // Visual
            settings.StandingsShowMedals = ShowMedalsCheck.IsChecked;
            
            // Highlights
            settings.StandingsHighlightTop = HighlightTopCheck.IsChecked;
            if (int.TryParse(HighlightTopCountEntry.Text, out int topCount))
                settings.StandingsHighlightTopCount = topCount;
            if (!string.IsNullOrWhiteSpace(TopHighlightColorEntry.Text))
                settings.StandingsTopHighlightColor = TopHighlightColorEntry.Text;
            
            settings.StandingsHighlightBottom = HighlightBottomCheck.IsChecked;
            if (int.TryParse(HighlightBottomCountEntry.Text, out int bottomCount))
                settings.StandingsHighlightBottomCount = bottomCount;
            if (!string.IsNullOrWhiteSpace(BottomHighlightColorEntry.Text))
                settings.StandingsBottomHighlightColor = BottomHighlightColorEntry.Text;
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Standings settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
