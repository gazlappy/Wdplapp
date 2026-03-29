using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class RulesSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private static readonly (string asset, string fallbackSubtitle)[] Sections =
    [
        ("Rules/Constitution.txt", "League constitution"),
        ("Rules/MatchRules.txt", "League match rules"),
        ("Rules/EpaRules.txt", "EPA International Rules"),
    ];

    public RulesSettingsPage()
    {
        InitializeComponent();
        ShowRulesSwitch.IsToggled = League.WebsiteSettings.ShowRules;
        _ = LoadTocAsync();
    }

    // ── TOC loading ──────────────────────────────────────────────────────

    private async Task LoadTocAsync()
    {
        try
        {
            var constitutionText = await LoadAssetAsync(Sections[0].asset);
            var matchRulesText = await LoadAssetAsync(Sections[1].asset);
            var epaRulesText = await LoadAssetAsync(Sections[2].asset);

            PopulateSection(constitutionText, ConstitutionSubtitle, ConstitutionToc, Sections[0].fallbackSubtitle, Color.FromArgb("#4F46E5"));
            PopulateSection(matchRulesText, MatchRulesSubtitle, MatchRulesToc, Sections[1].fallbackSubtitle, Color.FromArgb("#059669"));
            PopulateSection(epaRulesText, EpaRulesSubtitle, EpaRulesToc, Sections[2].fallbackSubtitle, Color.FromArgb("#D97706"));
        }
        catch
        {
            // Gracefully degrade — leave "Loading..." text
        }
    }

    private static void PopulateSection(string text, Label subtitleLabel, VerticalStackLayout tocLayout, string fallback, Color accentColor)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            subtitleLabel.Text = "No content found";
            return;
        }

        // Extract subtitle from the first two non-empty lines
        var lines = text.Split('\n');
        var headerLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(2).ToArray();
        subtitleLabel.Text = headerLines.Length >= 2
            ? $"{headerLines[0].Trim()} — {headerLines[1].Trim()}"
            : headerLines.Length == 1 ? headerLines[0].Trim() : fallback;

        // Parse headings for TOC
        var headings = ParseHeadings(text);
        tocLayout.Children.Clear();

        if (headings.Count == 0)
        {
            tocLayout.Children.Add(new Label { Text = "No numbered sections found", FontSize = 12, TextColor = Colors.Gray });
            return;
        }

        foreach (var heading in headings)
        {
            tocLayout.Children.Add(new Label
            {
                Text = heading,
                FontSize = 12,
                TextColor = accentColor,
                Padding = new Thickness(8, 1, 0, 1),
            });
        }
    }

    private static List<string> ParseHeadings(string text)
    {
        var headings = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return headings;

        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line.Trim(), @"^(\d+)\.\s+(.+)$");
            if (match.Success)
                headings.Add($"{match.Groups[1].Value}. {match.Groups[2].Value.Trim()}");
        }
        return headings;
    }

    // ── Asset loading ────────────────────────────────────────────────────

    private static async Task<string> LoadAssetAsync(string assetPath)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(assetPath);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch
        {
            return "";
        }
    }

    // ── Save ─────────────────────────────────────────────────────────────

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            SaveBtn.IsEnabled = false;

            var settings = League.WebsiteSettings;
            settings.ShowRules = ShowRulesSwitch.IsToggled;

            // Load full content from resource files into settings for the website generator
            settings.ConstitutionContent = (await LoadAssetAsync(Sections[0].asset)).Trim();
            settings.MatchRulesContent = (await LoadAssetAsync(Sections[1].asset)).Trim();
            settings.EpaRulesContent = (await LoadAssetAsync(Sections[2].asset)).Trim();

            DataStore.Save();

            await DisplayAlert("Saved", "Rules settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
        finally
        {
            SaveBtn.IsEnabled = true;
        }
    }
}
