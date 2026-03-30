using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class RulesSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private sealed class RuleSection
    {
        public required string Number { get; init; }
        public required string Title { get; init; }
        public required int CategoryIndex { get; init; }
        public string Content { get; set; } = "";
    }

    private static readonly (string asset, string label, string icon, Color accent)[] Categories =
    [
        ("Rules/Constitution.txt", "Constitution", "\U0001F4DC", Color.FromArgb("#4F46E5")),
        ("Rules/MatchRules.txt", "Match Rules", "\U0001F3C6", Color.FromArgb("#059669")),
        ("Rules/EpaRules.txt", "EPA Rules", "\U0001F3B1", Color.FromArgb("#D97706")),
    ];

    private readonly List<RuleSection> _sections = [];
    private readonly List<Border> _tocItems = [];
    private readonly string[] _preambles = ["", "", ""];
    private int _selectedIndex = -1;
    private bool _suppressEditorChange;

    public RulesSettingsPage()
    {
        InitializeComponent();
        ShowRulesSwitch.IsToggled = League.WebsiteSettings.ShowRules;
        _ = LoadAsync();
    }

    // ── Loading ──────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        try
        {
            for (int cat = 0; cat < Categories.Length; cat++)
            {
                var text = await LoadAssetAsync(Categories[cat].asset);
                var (preamble, parsed) = ParseSections(text);
                _preambles[cat] = preamble;

                // Category header
                TocPanel.Children.Add(new Label
                {
                    Text = $"{Categories[cat].icon} {Categories[cat].label}",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Categories[cat].accent,
                    Padding = new Thickness(16, cat > 0 ? 16 : 8, 16, 4),
                });

                foreach (var (number, title, content) in parsed)
                {
                    int index = _sections.Count;
                    _sections.Add(new RuleSection
                    {
                        Number = number,
                        Title = title,
                        CategoryIndex = cat,
                        Content = content,
                    });

                    var label = new Label
                    {
                        Text = $"{number}. {title}",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#4B5563"),
                        Padding = new Thickness(12, 6),
                        LineBreakMode = LineBreakMode.TailTruncation,
                    };

                    var border = new Border
                    {
                        Content = label,
                        StrokeThickness = 0,
                        BackgroundColor = Colors.Transparent,
                        Padding = new Thickness(0),
                        Margin = new Thickness(8, 0),
                        StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    };

                    int captured = index;
                    var tap = new TapGestureRecognizer();
                    tap.Tapped += (_, _) => SelectSection(captured);
                    border.GestureRecognizers.Add(tap);

                    _tocItems.Add(border);
                    TocPanel.Children.Add(border);
                }
            }

            if (_sections.Count > 0)
                SelectSection(0);
        }
        catch
        {
            // Gracefully degrade
        }
    }

    // ── Section selection ────────────────────────────────────────────────

    private void SelectSection(int index)
    {
        if (index < 0 || index >= _sections.Count) return;

        // Deselect previous
        if (_selectedIndex >= 0 && _selectedIndex < _tocItems.Count)
        {
            _tocItems[_selectedIndex].BackgroundColor = Colors.Transparent;
            if (_tocItems[_selectedIndex].Content is Label prev)
            {
                prev.FontAttributes = FontAttributes.None;
                prev.TextColor = Color.FromArgb("#4B5563");
            }
        }

        _selectedIndex = index;
        var section = _sections[index];
        var accent = Categories[section.CategoryIndex].accent;

        // Highlight selected TOC item
        _tocItems[index].BackgroundColor = accent.WithAlpha(0.1f);
        if (_tocItems[index].Content is Label sel)
        {
            sel.FontAttributes = FontAttributes.Bold;
            sel.TextColor = accent;
        }

        // Update content panel
        PlaceholderPanel.IsVisible = false;

        ContentCategory.Text = Categories[section.CategoryIndex].label.ToUpperInvariant();
        ContentCategory.TextColor = accent;
        ContentCategory.IsVisible = true;

        ContentTitle.Text = $"{section.Number}. {section.Title}";
        ContentTitle.IsVisible = true;

        ContentDivider.Color = accent;
        ContentDivider.IsVisible = true;

        _suppressEditorChange = true;
        ContentEditor.Text = section.Content;
        _suppressEditorChange = false;
        EditorFrame.IsVisible = true;

        ContentScrollView.ScrollToAsync(0, 0, false);
    }

    // ── Editor changes ───────────────────────────────────────────────────

    private void OnContentEditorChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressEditorChange || _selectedIndex < 0 || _selectedIndex >= _sections.Count)
            return;

        _sections[_selectedIndex].Content = e.NewTextValue ?? "";
    }

    // ── Parsing ──────────────────────────────────────────────────────────

    private static (string preamble, List<(string number, string title, string content)> sections) ParseSections(string text)
    {
        var sections = new List<(string, string, string)>();
        if (string.IsNullOrWhiteSpace(text)) return ("", sections);

        var lines = text.Split('\n');
        string? currentNumber = null;
        string? currentTitle = null;
        var contentLines = new List<string>();
        var preambleLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var match = Regex.Match(trimmed, @"^(\d+[a-z]?)\.\s+(.+)$");

            if (match.Success)
            {
                if (currentNumber != null)
                    sections.Add((currentNumber, currentTitle!, string.Join('\n', contentLines).Trim()));

                currentNumber = match.Groups[1].Value;
                currentTitle = match.Groups[2].Value.Trim();
                contentLines.Clear();
            }
            else if (currentNumber != null)
            {
                contentLines.Add(line.TrimEnd());
            }
            else
            {
                preambleLines.Add(line.TrimEnd());
            }
        }

        if (currentNumber != null)
            sections.Add((currentNumber, currentTitle!, string.Join('\n', contentLines).Trim()));

        return (string.Join('\n', preambleLines).TrimEnd(), sections);
    }

    // ── Text reconstruction ──────────────────────────────────────────────

    private string ReconstructText(int categoryIndex)
    {
        var sb = new StringBuilder();
        sb.Append(_preambles[categoryIndex]);

        foreach (var section in _sections.Where(s => s.CategoryIndex == categoryIndex))
        {
            sb.Append("\n\n");
            sb.Append($"{section.Number}. {section.Title}");
            if (!string.IsNullOrWhiteSpace(section.Content))
            {
                sb.Append('\n');
                sb.Append(section.Content.TrimEnd());
            }
        }

        return sb.ToString().TrimEnd();
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

            settings.ConstitutionContent = ReconstructText(0);
            settings.MatchRulesContent = ReconstructText(1);
            settings.EpaRulesContent = ReconstructText(2);

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
