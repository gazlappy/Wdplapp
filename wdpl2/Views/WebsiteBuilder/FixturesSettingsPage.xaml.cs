using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class FixturesSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public FixturesSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        ShowDateCheck.IsChecked = settings.FixturesShowDate;
        ShowTimeCheck.IsChecked = settings.FixturesShowTime;
        ShowVenueCheck.IsChecked = settings.FixturesShowVenue;
        ShowDivisionCheck.IsChecked = settings.FixturesShowDivision;
        ShowCountdownCheck.IsChecked = settings.FixturesShowCountdown;
        
        GroupByDateCheck.IsChecked = settings.FixturesGroupByDate;
        GroupByWeekCheck.IsChecked = settings.FixturesGroupByWeek;
        
        // Date format
        var formats = DateFormatPicker.ItemsSource as IList<string>;
        if (formats != null)
        {
            var index = formats.IndexOf(settings.FixturesDateFormat);
            DateFormatPicker.SelectedIndex = index >= 0 ? index : 0;
        }
        
        FixturesPerPageEntry.Text = settings.FixturesPerPage.ToString();
        
        // Printable fixtures sheet options
        ShowPrintableSheetCheck.IsChecked = settings.FixturesShowPrintableSheet;
        SheetDefaultExpandedCheck.IsChecked = settings.FixturesSheetDefaultExpanded;
        SheetTitleEntry.Text = settings.FixturesSheetTitle;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.FixturesShowDate = ShowDateCheck.IsChecked;
            settings.FixturesShowTime = ShowTimeCheck.IsChecked;
            settings.FixturesShowVenue = ShowVenueCheck.IsChecked;
            settings.FixturesShowDivision = ShowDivisionCheck.IsChecked;
            settings.FixturesShowCountdown = ShowCountdownCheck.IsChecked;
            
            settings.FixturesGroupByDate = GroupByDateCheck.IsChecked;
            settings.FixturesGroupByWeek = GroupByWeekCheck.IsChecked;
            
            if (DateFormatPicker.SelectedItem is string dateFormat)
                settings.FixturesDateFormat = dateFormat;
            
            if (int.TryParse(FixturesPerPageEntry.Text, out int perPage))
                settings.FixturesPerPage = perPage;
            
            // Printable fixtures sheet options
            settings.FixturesShowPrintableSheet = ShowPrintableSheetCheck.IsChecked;
            settings.FixturesSheetDefaultExpanded = SheetDefaultExpandedCheck.IsChecked;
            settings.FixturesSheetTitle = string.IsNullOrWhiteSpace(SheetTitleEntry.Text) 
                ? "Printable Fixtures Sheet" 
                : SheetTitleEntry.Text;
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Fixtures settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
