using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class PlayersSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public PlayersSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        // Columns
        ShowPositionCheck.IsChecked = settings.PlayersShowPosition;
        ShowTeamCheck.IsChecked = settings.PlayersShowTeam;
        ShowPlayedCheck.IsChecked = settings.PlayersShowPlayed;
        ShowWonCheck.IsChecked = settings.PlayersShowWon;
        ShowLostCheck.IsChecked = settings.PlayersShowLost;
        ShowWinPercentageCheck.IsChecked = settings.PlayersShowWinPercentage;
        ShowEightBallsCheck.IsChecked = settings.PlayersShowEightBalls;
        ShowRatingCheck.IsChecked = settings.PlayersShowRating;
        ShowAverageCheck.IsChecked = settings.PlayersShowAverage;
        ShowFormCheck.IsChecked = settings.PlayersShowForm;
        ShowPhotoCheck.IsChecked = settings.PlayersShowPhoto;
        
        // Sort
        var sortOptions = SortByPicker.ItemsSource as IList<string>;
        if (sortOptions != null)
        {
            var index = sortOptions.IndexOf(settings.PlayersSortBy);
            SortByPicker.SelectedIndex = index >= 0 ? index : 0;
        }
        
        // Filter
        UsePercentageFilterCheck.IsChecked = settings.PlayersUsePercentageFilter;
        MinGamesEntry.Text = settings.PlayersMinGames.ToString();
        MinFramesPercentageSlider.Value = settings.PlayersMinFramesPercentage;
        PercentageValueLabel.Text = $"{settings.PlayersMinFramesPercentage}%";
        
        // Show/hide appropriate filter section
        UpdateFilterSectionVisibility();
        
        PlayersPerPageEntry.Text = settings.PlayersPerPage.ToString();
    }

    private void OnFilterTypeChanged(object? sender, CheckedChangedEventArgs e)
    {
        UpdateFilterSectionVisibility();
    }
    
    private void UpdateFilterSectionVisibility()
    {
        var usePercentage = UsePercentageFilterCheck.IsChecked;
        FixedFilterSection.IsVisible = !usePercentage;
        PercentageFilterSection.IsVisible = usePercentage;
    }
    
    private void OnPercentageSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        var value = (int)Math.Round(e.NewValue);
        PercentageValueLabel.Text = $"{value}%";
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            // Columns
            settings.PlayersShowPosition = ShowPositionCheck.IsChecked;
            settings.PlayersShowTeam = ShowTeamCheck.IsChecked;
            settings.PlayersShowPlayed = ShowPlayedCheck.IsChecked;
            settings.PlayersShowWon = ShowWonCheck.IsChecked;
            settings.PlayersShowLost = ShowLostCheck.IsChecked;
            settings.PlayersShowWinPercentage = ShowWinPercentageCheck.IsChecked;
            settings.PlayersShowEightBalls = ShowEightBallsCheck.IsChecked;
            settings.PlayersShowRating = ShowRatingCheck.IsChecked;
            settings.PlayersShowAverage = ShowAverageCheck.IsChecked;
            settings.PlayersShowForm = ShowFormCheck.IsChecked;
            settings.PlayersShowPhoto = ShowPhotoCheck.IsChecked;
            
            // Sort
            if (SortByPicker.SelectedItem is string sortBy)
                settings.PlayersSortBy = sortBy;
            
            // Filter
            settings.PlayersUsePercentageFilter = UsePercentageFilterCheck.IsChecked;
            
            if (int.TryParse(MinGamesEntry.Text, out int minGames))
                settings.PlayersMinGames = minGames;
            
            settings.PlayersMinFramesPercentage = (int)Math.Round(MinFramesPercentageSlider.Value);
            
            if (int.TryParse(PlayersPerPageEntry.Text, out int perPage))
                settings.PlayersPerPage = perPage;
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Players settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
