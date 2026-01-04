using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class ResultsSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public ResultsSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        ShowScoreCheck.IsChecked = settings.ResultsShowScore;
        ShowDateCheck.IsChecked = settings.ResultsShowDate;
        ShowTimeCheck.IsChecked = settings.ResultsShowTime;
        ShowVenueCheck.IsChecked = settings.ResultsShowVenue;
        ShowDivisionCheck.IsChecked = settings.ResultsShowDivision;
        HighlightWinnerCheck.IsChecked = settings.ResultsHighlightWinner;
        ShowFrameDetailsCheck.IsChecked = settings.ResultsShowFrameDetails;
        
        GroupByDateCheck.IsChecked = settings.ResultsGroupByDate;
        GroupByWeekCheck.IsChecked = settings.ResultsGroupByWeek;
        
        // Date format
        var formats = DateFormatPicker.ItemsSource as IList<string>;
        if (formats != null)
        {
            var index = formats.IndexOf(settings.ResultsDateFormat);
            DateFormatPicker.SelectedIndex = index >= 0 ? index : 0;
        }
        
        ResultsPerPageEntry.Text = settings.ResultsPerPage.ToString();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.ResultsShowScore = ShowScoreCheck.IsChecked;
            settings.ResultsShowDate = ShowDateCheck.IsChecked;
            settings.ResultsShowTime = ShowTimeCheck.IsChecked;
            settings.ResultsShowVenue = ShowVenueCheck.IsChecked;
            settings.ResultsShowDivision = ShowDivisionCheck.IsChecked;
            settings.ResultsHighlightWinner = HighlightWinnerCheck.IsChecked;
            settings.ResultsShowFrameDetails = ShowFrameDetailsCheck.IsChecked;
            
            settings.ResultsGroupByDate = GroupByDateCheck.IsChecked;
            settings.ResultsGroupByWeek = GroupByWeekCheck.IsChecked;
            
            if (DateFormatPicker.SelectedItem is string dateFormat)
                settings.ResultsDateFormat = dateFormat;
            
            if (int.TryParse(ResultsPerPageEntry.Text, out int perPage))
                settings.ResultsPerPage = perPage;
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Results settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
