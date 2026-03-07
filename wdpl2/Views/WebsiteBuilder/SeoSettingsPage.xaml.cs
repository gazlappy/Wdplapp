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
        OgImageEntry.Text = settings.OgImage;
        GenerateSitemapCheck.IsChecked = settings.GenerateSitemap;
        CustomCssEditor.Text = settings.CustomCss;
        CustomHeadHtmlEditor.Text = settings.CustomHeadHtml;
        CustomBodyStartHtmlEditor.Text = settings.CustomBodyStartHtml;
        CustomBodyEndHtmlEditor.Text = settings.CustomBodyEndHtml;

        UpdateMetaCharCount();
        UpdateCssStats();
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

    private void OnCustomCssChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateCssStats();
    }

    private void UpdateCssStats()
    {
        var css = CustomCssEditor.Text;
        if (string.IsNullOrWhiteSpace(css))
        {
            CssStatsLabel.Text = "";
            return;
        }

        var lines = css.Split('\n').Length;
        var rules = css.Split('{').Length - 1;
        CssStatsLabel.Text = $"{lines} line{(lines == 1 ? "" : "s")}, ~{rules} rule{(rules == 1 ? "" : "s")}";
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.MetaDescription = MetaDescriptionEditor.Text?.Trim() ?? "";
            settings.MetaKeywords = MetaKeywordsEntry.Text?.Trim() ?? "";
            settings.OgImage = OgImageEntry.Text?.Trim() ?? "";
            settings.GenerateSitemap = GenerateSitemapCheck.IsChecked;
            settings.CustomCss = CustomCssEditor.Text?.Trim() ?? "";
            settings.CustomHeadHtml = CustomHeadHtmlEditor.Text?.Trim() ?? "";
            settings.CustomBodyStartHtml = CustomBodyStartHtmlEditor.Text?.Trim() ?? "";
            settings.CustomBodyEndHtml = CustomBodyEndHtmlEditor.Text?.Trim() ?? "";
            
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
