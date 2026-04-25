using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class DivisionsSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public DivisionsSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        // Layout
        var layouts = LayoutPicker.ItemsSource as IList<string>;
        if (layouts != null)
        {
            var index = layouts.IndexOf(settings.DivisionsLayout);
            LayoutPicker.SelectedIndex = index >= 0 ? index : 0;
        }
        
        // Display options
        ShowTeamCountCheck.IsChecked = settings.DivisionsShowTeamCount;
        ShowPlayerCountCheck.IsChecked = settings.DivisionsShowPlayerCount;
        ShowDescriptionCheck.IsChecked = settings.DivisionsShowDescription;
        ShowTeamListCheck.IsChecked = settings.DivisionsShowTeamList;
        ShowMiniStandingsCheck.IsChecked = settings.DivisionsShowMiniStandings;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            // Layout
            if (LayoutPicker.SelectedItem is string layout)
                settings.DivisionsLayout = layout;
            
            // Display options
            settings.DivisionsShowTeamCount = ShowTeamCountCheck.IsChecked;
            settings.DivisionsShowPlayerCount = ShowPlayerCountCheck.IsChecked;
            settings.DivisionsShowDescription = ShowDescriptionCheck.IsChecked;
            settings.DivisionsShowTeamList = ShowTeamListCheck.IsChecked;
            settings.DivisionsShowMiniStandings = ShowMiniStandingsCheck.IsChecked;
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Divisions settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
