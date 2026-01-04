using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class SeoSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public SeoSettingsPage()
    {
        InitializeComponent();
        MetaDescriptionEditor.TextChanged += OnMetaDescriptionChanged;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        MetaDescriptionEditor.Text = settings.MetaDescription;
        MetaKeywordsEntry.Text = settings.MetaKeywords;
        GenerateSitemapCheck.IsChecked = settings.GenerateSitemap;
        CustomCssEditor.Text = settings.CustomCss;
        
        UpdateMetaCharCount();
    }

    private void OnMetaDescriptionChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateMetaCharCount();
    }

    private void UpdateMetaCharCount()
    {
        var length = MetaDescriptionEditor.Text?.Length ?? 0;
        MetaCharCount.Text = $"{length} / 160 characters";
        
        if (length > 160)
            MetaCharCount.TextColor = Color.FromArgb("#EF4444");
        else if (length > 140)
            MetaCharCount.TextColor = Color.FromArgb("#F59E0B");
        else
            MetaCharCount.TextColor = Color.FromArgb("#9CA3AF");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.MetaDescription = MetaDescriptionEditor.Text?.Trim() ?? "";
            settings.MetaKeywords = MetaKeywordsEntry.Text?.Trim() ?? "";
            settings.GenerateSitemap = GenerateSitemapCheck.IsChecked;
            settings.CustomCss = CustomCssEditor.Text?.Trim() ?? "";
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "SEO settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
