using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class ColorsSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public ColorsSettingsPage()
    {
        InitializeComponent();
        
        ColorSchemePicker.ItemsSource = WebsiteSettings.ColorSchemes.Select(cs => cs.Value.Name).ToList();
        
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        PrimaryColorEntry.Text = settings.PrimaryColor;
        SecondaryColorEntry.Text = settings.SecondaryColor;
        AccentColorEntry.Text = settings.AccentColor;
        BackgroundColorEntry.Text = settings.BackgroundColor;
        CardBackgroundColorEntry.Text = settings.CardBackgroundColor;
        TextColorEntry.Text = settings.TextColor;
        TextSecondaryColorEntry.Text = settings.TextSecondaryColor;
        HeaderTextColorEntry.Text = settings.HeaderTextColor;
    }

    private void OnColorSchemeChanged(object sender, EventArgs e)
    {
        if (ColorSchemePicker.SelectedIndex < 0) return;
        
        var selectedName = ColorSchemePicker.SelectedItem?.ToString();
        var scheme = WebsiteSettings.ColorSchemes.FirstOrDefault(cs => cs.Value.Name == selectedName);
        
        if (scheme.Value != null)
        {
            PrimaryColorEntry.Text = scheme.Value.Primary;
            SecondaryColorEntry.Text = scheme.Value.Secondary;
            AccentColorEntry.Text = scheme.Value.Accent;
            BackgroundColorEntry.Text = scheme.Value.Background;
            CardBackgroundColorEntry.Text = scheme.Value.CardBackground;
            TextColorEntry.Text = scheme.Value.Text;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.PrimaryColor = PrimaryColorEntry.Text?.Trim() ?? "#3B82F6";
            settings.SecondaryColor = SecondaryColorEntry.Text?.Trim() ?? "#10B981";
            settings.AccentColor = AccentColorEntry.Text?.Trim() ?? "#F59E0B";
            settings.BackgroundColor = BackgroundColorEntry.Text?.Trim() ?? "#F8FAFC";
            settings.CardBackgroundColor = CardBackgroundColorEntry.Text?.Trim() ?? "#FFFFFF";
            settings.TextColor = TextColorEntry.Text?.Trim() ?? "#0F172A";
            settings.TextSecondaryColor = TextSecondaryColorEntry.Text?.Trim() ?? "#64748B";
            settings.HeaderTextColor = HeaderTextColorEntry.Text?.Trim() ?? "#FFFFFF";
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Color settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
