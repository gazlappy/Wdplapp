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

        UpdateAllSwatches();
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

            UpdateAllSwatches();
            ShowSchemePreview(scheme.Value);
        }
    }

    private void ShowSchemePreview(ColorScheme scheme)
    {
        SchemePreviewLayout.Children.Clear();
        var colors = new[] { scheme.Primary, scheme.Secondary, scheme.Accent, scheme.Background, scheme.CardBackground, scheme.Text };
        foreach (var hex in colors)
        {
            if (TryParseColor(hex, out var color))
            {
                SchemePreviewLayout.Children.Add(new BoxView
                {
                    Color = color,
                    WidthRequest = 32,
                    HeightRequest = 32,
                    CornerRadius = 16,
                    Margin = new Thickness(0, 0, 4, 0)
                });
            }
        }
        SchemePreviewLayout.IsVisible = true;
    }

    private void OnColorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is Entry entry)
        {
            var hex = entry.Text?.Trim();
            if (string.IsNullOrEmpty(hex)) return;

            BoxView? swatch = entry == PrimaryColorEntry ? PrimaryColorSwatch
                : entry == SecondaryColorEntry ? SecondaryColorSwatch
                : entry == AccentColorEntry ? AccentColorSwatch
                : entry == BackgroundColorEntry ? BackgroundColorSwatch
                : entry == CardBackgroundColorEntry ? CardBackgroundColorSwatch
                : entry == TextColorEntry ? TextColorSwatch
                : entry == TextSecondaryColorEntry ? TextSecondaryColorSwatch
                : entry == HeaderTextColorEntry ? HeaderTextColorSwatch
                : null;

            if (swatch != null && TryParseColor(hex, out var color))
                swatch.Color = color;
        }

        UpdatePreviewBar();
    }

    private void UpdateAllSwatches()
    {
        UpdateSwatch(PrimaryColorEntry, PrimaryColorSwatch);
        UpdateSwatch(SecondaryColorEntry, SecondaryColorSwatch);
        UpdateSwatch(AccentColorEntry, AccentColorSwatch);
        UpdateSwatch(BackgroundColorEntry, BackgroundColorSwatch);
        UpdateSwatch(CardBackgroundColorEntry, CardBackgroundColorSwatch);
        UpdateSwatch(TextColorEntry, TextColorSwatch);
        UpdateSwatch(TextSecondaryColorEntry, TextSecondaryColorSwatch);
        UpdateSwatch(HeaderTextColorEntry, HeaderTextColorSwatch);
        UpdatePreviewBar();
    }

    private void UpdateSwatch(Entry entry, BoxView swatch)
    {
        if (TryParseColor(entry.Text, out var color))
            swatch.Color = color;
    }

    private void UpdatePreviewBar()
    {
        if (TryParseColor(PrimaryColorEntry.Text, out var c1)) PreviewPrimary.Color = c1;
        if (TryParseColor(SecondaryColorEntry.Text, out var c2)) PreviewSecondary.Color = c2;
        if (TryParseColor(AccentColorEntry.Text, out var c3)) PreviewAccent.Color = c3;
        if (TryParseColor(BackgroundColorEntry.Text, out var c4)) PreviewBackground.Color = c4;
        if (TryParseColor(CardBackgroundColorEntry.Text, out var c5)) PreviewCardBg.Color = c5;
        if (TryParseColor(TextColorEntry.Text, out var c6)) PreviewText.Color = c6;
        if (TryParseColor(TextSecondaryColorEntry.Text, out var c7)) PreviewTextSecondary.Color = c7;
        if (TryParseColor(HeaderTextColorEntry.Text, out var c8)) PreviewHeaderText.Color = c8;
    }

    private static bool TryParseColor(string? hex, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        try
        {
            color = Color.FromArgb(hex.StartsWith('#') ? hex : $"#{hex}");
            return true;
        }
        catch
        {
            return false;
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
