using Microsoft.Maui.Controls.Shapes;
using Wdpl2.Services;

namespace Wdpl2.Helpers;

/// <summary>
/// Shared UI building blocks for programmatic panel construction.
/// Provides a consistent, polished look across all settings / options pages.
/// </summary>
public static class PanelBuilder
{
    // ═══════════════════════════════════════════════════════════
    //  THEME-AWARE COLOURS
    // ═══════════════════════════════════════════════════════════

    public static bool IsDark => ThemeService.Current.IsDarkModeActive;

    /// <summary>Muted text for subtitles, help text, captions.</summary>
    public static Color SubtleText => IsDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280");

    /// <summary>Card / panel surface colour.</summary>
    public static Color CardBg => IsDark ? Color.FromArgb("#1F2937") : Colors.White;

    /// <summary>Card border / divider colour.</summary>
    public static Color CardStroke => IsDark ? Color.FromArgb("#374151") : Color.FromArgb("#E5E7EB");

    /// <summary>Tinted background for section-header icons.</summary>
    public static Color SectionIconBg => IsDark ? Color.FromArgb("#1E3A5F") : Color.FromArgb("#DBEAFE");

    /// <summary>Subtle field-row background colour.</summary>
    public static Color FieldBg => IsDark ? Color.FromArgb("#111827") : Color.FromArgb("#F9FAFB");

    /// <summary>Info banner background.</summary>
    public static Color InfoBg => IsDark ? Color.FromArgb("#0C2D48") : Color.FromArgb("#F0F9FF");

    /// <summary>Info banner border.</summary>
    public static Color InfoStroke => IsDark ? Color.FromArgb("#1E3A5F") : Color.FromArgb("#BAE6FD");

    /// <summary>Info banner text.</summary>
    public static Color InfoText => IsDark ? Color.FromArgb("#BAE6FD") : Color.FromArgb("#1E3A5F");

    /// <summary>Warning banner background.</summary>
    public static Color WarningBg => IsDark ? Color.FromArgb("#422006") : Color.FromArgb("#FFFBEB");

    /// <summary>Warning banner border.</summary>
    public static Color WarningStroke => IsDark ? Color.FromArgb("#92400E") : Color.FromArgb("#FDE68A");

    /// <summary>Warning banner text.</summary>
    public static Color WarningText => IsDark ? Color.FromArgb("#FDE68A") : Color.FromArgb("#92400E");

    /// <summary>Success banner background.</summary>
    public static Color SuccessBg => IsDark ? Color.FromArgb("#052E16") : Color.FromArgb("#F0FDF4");

    /// <summary>Success banner border.</summary>
    public static Color SuccessStroke => IsDark ? Color.FromArgb("#166534") : Color.FromArgb("#BBF7D0");

    /// <summary>Success banner text.</summary>
    public static Color SuccessText => IsDark ? Color.FromArgb("#BBF7D0") : Color.FromArgb("#166534");

    /// <summary>Standard title colour.</summary>
    public static Color TitleText => IsDark ? Colors.White : Color.FromArgb("#111827");

    /// <summary>Standard body text colour.</summary>
    public static Color BodyText => IsDark ? Color.FromArgb("#D1D5DB") : Color.FromArgb("#374151");

    // ═══════════════════════════════════════════════════════════
    //  SECTION HEADERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Section header with emoji icon in a tinted rounded square, title, and subtitle.
    /// </summary>
    public static View SectionHeader(string icon, string title, string subtitle)
    {
        var iconBorder = new Border
        {
            WidthRequest = 40,
            HeightRequest = 40,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            BackgroundColor = SectionIconBg,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = icon,
                FontSize = 20,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var titleLabel = new Label { Text = title, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = TitleText };
        var subtitleLabel = new Label { Text = subtitle, FontSize = 13, TextColor = SubtleText, Margin = new Thickness(0, 2, 0, 0) };

        return new HorizontalStackLayout
        {
            Spacing = 14,
            Margin = new Thickness(0, 0, 0, 16),
            Children =
            {
                iconBorder,
                new VerticalStackLayout
                {
                    Spacing = 2,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { titleLabel, subtitleLabel }
                }
            }
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  CARDS
    // ═══════════════════════════════════════════════════════════

    /// <summary>Themed card that wraps a section of related controls.</summary>
    public static Border Card(View content, Thickness? margin = null)
    {
        return new Border
        {
            Padding = 16,
            Margin = margin ?? new Thickness(0, 0, 0, 12),
            BackgroundColor = CardBg,
            Stroke = CardStroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = content
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  SETTING ROWS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// A row with label (left) and control (right), inside a subtle background.
    /// Optionally includes help text below the label.
    /// </summary>
    public static Grid SettingRow(string labelText, View control, string? helpText = null)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = new GridLength(180) } },
            Padding = new Thickness(12, 10),
            Margin = new Thickness(0, 0, 0, 2),
            BackgroundColor = FieldBg,
            MinimumHeightRequest = 44
        };

        var stack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
        var lbl = new Label { Text = labelText, FontSize = 14, TextColor = BodyText, VerticalTextAlignment = TextAlignment.Center };
        stack.Children.Add(lbl);

        if (helpText != null)
        {
            stack.Children.Add(new Label
            {
                Text = helpText,
                FontSize = 11,
                TextColor = SubtleText,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        control.VerticalOptions = LayoutOptions.Center;
        control.HorizontalOptions = LayoutOptions.End;

        grid.Add(stack, 0, 0);
        grid.Add(control, 1, 0);
        return grid;
    }

    /// <summary>A setting row pre-configured for a Switch.</summary>
    public static Grid SwitchRow(string label, bool value, Action<bool> setter, string? helpText = null)
    {
        var sw = new Switch { IsToggled = value };
        sw.Toggled += (_, _) => { setter(sw.IsToggled); DataStore.Save(); };
        return SettingRow(label, sw, helpText);
    }

    /// <summary>A setting row pre-configured for a numeric Entry. Returns the row and the entry for wiring events.</summary>
    public static (Grid Row, Entry Entry) NumericRow(string label, int value, string? helpText = null)
    {
        var entry = new Entry
        {
            Text = value.ToString(),
            Keyboard = Keyboard.Numeric,
            WidthRequest = 70,
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center
        };
        entry.SetAppThemeColor(Entry.BackgroundColorProperty, Colors.White, Color.FromArgb("#1F2937"));
        return (SettingRow(label, entry, helpText), entry);
    }

    /// <summary>A setting row pre-configured for a decimal Entry. Returns the row and the entry for wiring events.</summary>
    public static (Grid Row, Entry Entry) DecimalRow(string label, double value, string format = "0.00", string? helpText = null)
    {
        var entry = new Entry
        {
            Text = value.ToString(format),
            Keyboard = Keyboard.Numeric,
            WidthRequest = 70,
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center
        };
        entry.SetAppThemeColor(Entry.BackgroundColorProperty, Colors.White, Color.FromArgb("#1F2937"));
        return (SettingRow(label, entry, helpText), entry);
    }

    // ═══════════════════════════════════════════════════════════
    //  BANNERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>Blue info banner with ℹ icon.</summary>
    public static View InfoBanner(string message)
    {
        return StatusBanner(Emojis.Info, message, InfoBg, InfoStroke, InfoText);
    }

    /// <summary>Amber warning banner with ⚠ icon.</summary>
    public static View WarningBanner(string message)
    {
        return StatusBanner(Emojis.Warning, message, WarningBg, WarningStroke, WarningText);
    }

    /// <summary>Green success banner with ✅ icon.</summary>
    public static View SuccessBanner(string message)
    {
        return StatusBanner(Emojis.Success, message, SuccessBg, SuccessStroke, SuccessText);
    }

    private static View StatusBanner(string icon, string message, Color bg, Color stroke, Color textColor)
    {
        var row = new HorizontalStackLayout { Spacing = 10 };
        row.Children.Add(new Label { Text = icon, FontSize = 16, VerticalTextAlignment = TextAlignment.Start });
        row.Children.Add(new Label { Text = message, FontSize = 12, LineHeight = 1.4, TextColor = textColor });

        return new Border
        {
            Padding = 12,
            Margin = new Thickness(0, 8, 0, 0),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            BackgroundColor = bg,
            Stroke = stroke,
            Content = row
        };
    }

    /// <summary>
    /// Richer info panel with a title and multi-line body — for detailed explanations.
    /// </summary>
    public static View InfoPanel(string title, string body)
    {
        var titleLabel = new Label { Text = title, FontAttributes = FontAttributes.Bold, FontSize = 14, TextColor = InfoText };
        var bodyLabel = new Label { Text = body, FontSize = 12, LineHeight = 1.4, TextColor = InfoText };

        return new Border
        {
            Padding = 12,
            Margin = new Thickness(0, 8, 0, 0),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            BackgroundColor = InfoBg,
            Stroke = InfoStroke,
            Content = new VerticalStackLayout { Spacing = 6, Children = { titleLabel, bodyLabel } }
        };
    }

    /// <summary>Warning panel with title and body.</summary>
    public static View WarningPanel(string title, string body)
    {
        var titleLabel = new Label { Text = title, FontAttributes = FontAttributes.Bold, FontSize = 14, TextColor = WarningText };
        var bodyLabel = new Label { Text = body, FontSize = 12, LineHeight = 1.4, TextColor = WarningText };

        return new Border
        {
            Padding = 12,
            Margin = new Thickness(0, 8, 0, 0),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            BackgroundColor = WarningBg,
            Stroke = WarningStroke,
            Content = new VerticalStackLayout { Spacing = 6, Children = { titleLabel, bodyLabel } }
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  EMPTY STATES
    // ═══════════════════════════════════════════════════════════

    /// <summary>Centred empty-state placeholder with icon, title, and message.</summary>
    public static View EmptyState(string icon, string title, string message)
    {
        return new VerticalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 32),
            Children =
            {
                new Label { Text = icon, FontSize = 40, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, TextColor = BodyText },
                new Label { Text = message, FontSize = 13, TextColor = SubtleText, HorizontalTextAlignment = TextAlignment.Center, MaximumWidthRequest = 300 }
            }
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  UTILITY
    // ═══════════════════════════════════════════════════════════

    /// <summary>Parse int from string with clamped fallback.</summary>
    public static int ParseInt(string? text, int fallback, int min, int max)
    {
        if (int.TryParse(text, out var val))
            return Math.Clamp(val, min, max);
        return fallback;
    }

    /// <summary>Parse double from string with fallback.</summary>
    public static double ParseDouble(string? text, double fallback)
    {
        if (double.TryParse(text, out var val))
            return val;
        return fallback;
    }
}
