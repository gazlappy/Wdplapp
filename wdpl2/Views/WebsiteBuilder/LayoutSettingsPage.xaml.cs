using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class LayoutSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private static readonly int[] WidthValues = [960, 1200, 1400, 1600];

    private static readonly Dictionary<string, string> HeaderLayoutDescriptions = new()
    {
        ["centered"] = "Classic centered layout with logo, title, and subtitle stacked",
        ["split"] = "Logo on left, title group right, badge far right",
        ["banner"] = "Large hero-style banner with stacked elements",
        ["compact"] = "Condensed single-line header for minimal vertical space",
        ["minimal-bar"] = "Ultra-thin bar with logo and title inline",
        ["two-row"] = "First row: logo + badge, second row: title + subtitle",
        ["card"] = "Header content inside a floating card with shadow",
        ["scoreboard"] = "Grid-style layout resembling a sports scoreboard",
        ["glass"] = "Frosted glass effect with blur and transparency",
        ["animated-gradient"] = "Smoothly shifting gradient colors animation",
        ["wave-gradient"] = "Flowing wave-like gradient animation",
        ["mesh-gradient"] = "Multi-point radial gradient mesh background",
        ["stadium"] = "Dark stadium-lighting effect with spotlight radials",
        ["pulse-glow"] = "Subtle pulsing glow animation on the header",
        ["shimmer"] = "Shimmering light sweep effect across the header",
        ["aurora"] = "Northern lights inspired color animation",
        ["neon"] = "Dark background with neon glow accents",
        ["spotlight-sweep"] = "Moving spotlight beam sweeping across the header",
        ["breathing"] = "Gentle scaling/breathing animation effect",
        ["championship"] = "Dramatic clipped shape with pointed bottom edge",
        ["overlay-hero"] = "Overlaps content below for a hero image effect",
        ["text-only"] = "Clean text on transparent background, no decoration",
        ["underline"] = "Transparent with a bold colored bottom border",
        ["transparent"] = "Fully transparent, blends with page background",
    };

    public LayoutSettingsPage()
    {
        InitializeComponent();
        HeaderLayoutPicker.SelectedIndexChanged += OnHeaderLayoutChanged;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        // Page Layout
        var widthIndex = Array.IndexOf(WidthValues, settings.MaxContentWidth);
        MaxContentWidthPicker.SelectedIndex = widthIndex >= 0 ? widthIndex : 1;
        SetPickerValue(PageLayoutPicker, settings.PageLayout);
        SidebarWidthEntry.Text = settings.SidebarWidth.ToString();
        SectionSpacingEntry.Text = settings.SectionSpacing.ToString();
        CardSpacingEntry.Text = settings.CardSpacing.ToString();
        
        // Theme Effects
        EnableAnimationsCheck.IsChecked = settings.EnableAnimations;
        EnableGradientsCheck.IsChecked = settings.EnableGradients;
        EnableShadowsCheck.IsChecked = settings.EnableShadows;
        EnableRoundedCornersCheck.IsChecked = settings.EnableRoundedCorners;
        ShowLastUpdatedCheck.IsChecked = settings.ShowLastUpdated;
        
        // Typography
        SetPickerValue(FontFamilyPicker, settings.FontFamily);
        SetPickerValue(HeaderFontFamilyPicker, settings.HeaderFontFamily);
        BaseFontSizeEntry.Text = settings.BaseFontSize.ToString();
        BorderRadiusEntry.Text = settings.BorderRadius.ToString();
        
        // Buttons
        SetPickerValue(ButtonStylePicker, settings.ButtonStyle);
        ButtonRoundedCheck.IsChecked = settings.ButtonRounded;
        
        // Header
        SetPickerValue(HeaderStylePicker, settings.HeaderStyle);
        SetPickerValue(HeaderLayoutPicker, settings.HeaderLayout);
        UpdateHeaderLayoutDescription();
        SetPickerValue(HeaderAlignmentPicker, settings.HeaderAlignment);
        ShowHeaderPatternCheck.IsChecked = settings.ShowHeaderPattern;
        ShowSeasonBadgeCheck.IsChecked = settings.ShowSeasonBadge;

        // Navigation
        SetPickerValue(NavStylePicker, settings.NavStyle);
        SetPickerValue(NavPositionPicker, settings.NavPosition);
        NavStickyCheck.IsChecked = settings.NavSticky;
        ShowNavIconsCheck.IsChecked = settings.ShowNavIcons;
        
        // Footer
        SetPickerValue(FooterStylePicker, settings.FooterStyle);
        ShowFooterSocialLinksCheck.IsChecked = settings.ShowFooterSocialLinks;
        ShowFooterContactCheck.IsChecked = settings.ShowFooterContact;
        ShowPoweredByCheck.IsChecked = settings.ShowPoweredBy;
        CustomFooterTextEntry.Text = settings.CustomFooterText;
        CopyrightTextEntry.Text = settings.CopyrightText;

        FooterNotesContainer.Children.Clear();
        foreach (var note in settings.FooterNotes)
            AddFooterNoteRow(note);
        
        // Tables & Cards
        TableStripedCheck.IsChecked = settings.TableStriped;
        TableHoverableCheck.IsChecked = settings.TableHoverable;
        TableBorderedCheck.IsChecked = settings.TableBordered;
        TableCompactCheck.IsChecked = settings.TableCompact;
        SetPickerValue(TableHeaderStylePicker, settings.TableHeaderStyle);
        SetPickerValue(CardStylePicker, settings.CardStyle);
        SetPickerValue(CardAccentPositionPicker, settings.CardAccentPosition);
        CardShowTopAccentCheck.IsChecked = settings.CardShowTopAccent;
    }

    private void SetPickerValue(Picker picker, string value)
    {
        if (picker.ItemsSource is IList<string> items)
        {
            var index = items.IndexOf(value);
            if (index >= 0) picker.SelectedIndex = index;
        }
    }

    private string GetPickerValue(Picker picker, string defaultValue)
        => picker.SelectedItem?.ToString() ?? defaultValue;

    private void OnHeaderLayoutChanged(object? sender, EventArgs e)
    {
        UpdateHeaderLayoutDescription();
    }

    private void UpdateHeaderLayoutDescription()
    {
        var layout = HeaderLayoutPicker.SelectedItem?.ToString();
        if (layout != null && HeaderLayoutDescriptions.TryGetValue(layout, out var desc))
        {
            HeaderLayoutDescription.Text = desc;
            HeaderLayoutDescription.IsVisible = true;
        }
        else
        {
            HeaderLayoutDescription.IsVisible = false;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            // Page Layout
            if (MaxContentWidthPicker.SelectedIndex >= 0 && MaxContentWidthPicker.SelectedIndex < WidthValues.Length)
                settings.MaxContentWidth = WidthValues[MaxContentWidthPicker.SelectedIndex];
            settings.PageLayout = GetPickerValue(PageLayoutPicker, "full-width");
            if (int.TryParse(SidebarWidthEntry.Text, out int sidebarWidth))
                settings.SidebarWidth = sidebarWidth;
            if (int.TryParse(SectionSpacingEntry.Text, out int sectionSpacing))
                settings.SectionSpacing = sectionSpacing;
            if (int.TryParse(CardSpacingEntry.Text, out int cardSpacing))
                settings.CardSpacing = cardSpacing;
            
            // Theme Effects
            settings.EnableAnimations = EnableAnimationsCheck.IsChecked;
            settings.EnableGradients = EnableGradientsCheck.IsChecked;
            settings.EnableShadows = EnableShadowsCheck.IsChecked;
            settings.EnableRoundedCorners = EnableRoundedCornersCheck.IsChecked;
            settings.ShowLastUpdated = ShowLastUpdatedCheck.IsChecked;
            
            // Typography
            settings.FontFamily = GetPickerValue(FontFamilyPicker, "Inter");
            settings.HeaderFontFamily = GetPickerValue(HeaderFontFamilyPicker, "Inter");
            if (int.TryParse(BaseFontSizeEntry.Text, out int fontSize))
                settings.BaseFontSize = fontSize;
            if (int.TryParse(BorderRadiusEntry.Text, out int borderRadius))
                settings.BorderRadius = borderRadius;
            
            // Buttons
            settings.ButtonStyle = GetPickerValue(ButtonStylePicker, "filled");
            settings.ButtonRounded = ButtonRoundedCheck.IsChecked;
            
            // Header
            settings.HeaderStyle = GetPickerValue(HeaderStylePicker, "gradient");
            settings.HeaderLayout = GetPickerValue(HeaderLayoutPicker, "centered");
            settings.HeaderAlignment = GetPickerValue(HeaderAlignmentPicker, "center");
            settings.ShowHeaderPattern = ShowHeaderPatternCheck.IsChecked;
            settings.ShowSeasonBadge = ShowSeasonBadgeCheck.IsChecked;

            // Navigation
            settings.NavStyle = GetPickerValue(NavStylePicker, "pills");
            settings.NavPosition = GetPickerValue(NavPositionPicker, "center");
            settings.NavSticky = NavStickyCheck.IsChecked;
            settings.ShowNavIcons = ShowNavIconsCheck.IsChecked;
            
            // Footer
            settings.FooterStyle = GetPickerValue(FooterStylePicker, "dark");
            settings.ShowFooterSocialLinks = ShowFooterSocialLinksCheck.IsChecked;
            settings.ShowFooterContact = ShowFooterContactCheck.IsChecked;
            settings.ShowPoweredBy = ShowPoweredByCheck.IsChecked;
            settings.CustomFooterText = CustomFooterTextEntry.Text?.Trim() ?? "";
            settings.CopyrightText = CopyrightTextEntry.Text?.Trim() ?? "";

            settings.FooterNotes.Clear();
            foreach (var child in FooterNotesContainer.Children)
            {
                if (child is Grid g && g.Children.OfType<Entry>().FirstOrDefault() is Entry entry)
                {
                    var text = entry.Text?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(text))
                        settings.FooterNotes.Add(text);
                }
            }
            
            // Tables & Cards
            settings.TableStriped = TableStripedCheck.IsChecked;
            settings.TableHoverable = TableHoverableCheck.IsChecked;
            settings.TableBordered = TableBorderedCheck.IsChecked;
            settings.TableCompact = TableCompactCheck.IsChecked;
            settings.TableHeaderStyle = GetPickerValue(TableHeaderStylePicker, "gradient");
            settings.CardStyle = GetPickerValue(CardStylePicker, "elevated");
            settings.CardAccentPosition = GetPickerValue(CardAccentPositionPicker, "top");
            settings.CardShowTopAccent = CardShowTopAccentCheck.IsChecked;
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Layout settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }

    private void OnAddFooterNoteClicked(object? sender, EventArgs e)
    {
        AddFooterNoteRow("");
    }

    private void AddFooterNoteRow(string text)
    {
        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(36))],
            ColumnSpacing = 6
        };
        var entry = new Entry { Placeholder = "Footer note text...", Text = text };
        var removeBtn = new Button
        {
            Text = "✕",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#EF4444"),
            FontSize = 14,
            HeightRequest = 36,
            WidthRequest = 36,
            Padding = 0
        };
        removeBtn.Clicked += (_, _) => FooterNotesContainer.Children.Remove(grid);
        grid.Add(entry, 0);
        grid.Add(removeBtn, 1);
        FooterNotesContainer.Children.Add(grid);
    }
}
