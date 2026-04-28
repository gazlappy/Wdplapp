using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Wdpl2.Features.WebsiteBuilder.Views.Controls;

/// <summary>
/// A compact color picker: swatch + hex entry + 🎨 button that opens an action sheet of presets.
/// Two-way bindable via <see cref="HexColor"/>. Raises <see cref="ColorChanged"/> when the user changes it.
/// </summary>
public sealed class ColorPickerView : Grid
{
    public static readonly BindableProperty HexColorProperty = BindableProperty.Create(
        nameof(HexColor),
        typeof(string),
        typeof(ColorPickerView),
        defaultValue: "#FFFFFF",
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnHexColorPropertyChanged);

    public string HexColor
    {
        get => (string)GetValue(HexColorProperty);
        set => SetValue(HexColorProperty, value);
    }

    public event EventHandler? ColorChanged;

    private readonly BoxView _swatch;
    private readonly Entry _entry;
    private readonly Button _picker;
    private bool _suppress;

    private static readonly (string Name, string Hex)[] s_palette =
    [
        ("White",   "#FFFFFF"), ("Black",   "#000000"), ("Slate",   "#0F172A"),
        ("Gray",    "#475569"), ("Silver",  "#CBD5E1"), ("Red",     "#EF4444"),
        ("Rose",    "#F43F5E"), ("Orange",  "#F97316"), ("Amber",   "#F59E0B"),
        ("Gold",    "#FBBF24"), ("Yellow",  "#FACC15"), ("Lime",    "#84CC16"),
        ("Green",   "#10B981"), ("Emerald", "#059669"), ("Teal",    "#14B8A6"),
        ("Cyan",    "#06B6D4"), ("Sky",     "#0EA5E9"), ("Blue",    "#3B82F6"),
        ("Navy",    "#1E40AF"), ("Indigo",  "#6366F1"), ("Violet",  "#8B5CF6"),
        ("Purple",  "#9333EA"), ("Fuchsia", "#D946EF"), ("Pink",    "#EC4899"),
        ("Brown",   "#92400E"), ("Cream",   "#FEF3C7"), ("Mint",    "#A7F3D0"),
    ];

    public ColorPickerView()
    {
        ColumnDefinitions = new ColumnDefinitionCollection(
            new ColumnDefinition { Width = new GridLength(28) },
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Auto });
        ColumnSpacing = 6;

        _swatch = new BoxView
        {
            CornerRadius = 6,
            HeightRequest = 28,
            WidthRequest = 28,
            Color = Colors.White,
            BackgroundColor = Colors.White
        };
        var swatchBorder = new Frame
        {
            Padding = 0,
            HasShadow = false,
            BorderColor = Color.FromArgb("#CBD5E1"),
            CornerRadius = 6,
            HeightRequest = 28,
            WidthRequest = 28,
            Content = _swatch,
            BackgroundColor = Colors.Transparent
        };

        _entry = new Entry
        {
            Placeholder = "#RRGGBB",
            FontSize = 12,
            Keyboard = Keyboard.Plain
        };
        _entry.TextChanged += OnEntryTextChanged;
        _entry.Unfocused += (_, __) => SyncFromEntry(force: true);

        _picker = new Button
        {
            Text = "🎨",
            FontSize = 16,
            Padding = new Thickness(8, 2),
            BackgroundColor = Color.FromArgb("#E5E7EB"),
            TextColor = Colors.Black
        };
        _picker.Clicked += OnPickerClicked;

        Children.Add(swatchBorder);
        Microsoft.Maui.Controls.Grid.SetColumn(swatchBorder, 0);

        Children.Add(_entry);
        Microsoft.Maui.Controls.Grid.SetColumn(_entry, 1);

        Children.Add(_picker);
        Microsoft.Maui.Controls.Grid.SetColumn(_picker, 2);

        ApplyHexToControls(HexColor);
    }

    private static void OnHexColorPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ColorPickerView v)
            v.ApplyHexToControls(newValue as string ?? "");
    }

    private void ApplyHexToControls(string hex)
    {
        _suppress = true;
        try
        {
            _entry.Text = hex;
            _swatch.Color = TryParseColor(hex, out var c) ? c : Colors.Transparent;
            _swatch.BackgroundColor = _swatch.Color;
        }
        finally { _suppress = false; }
    }

    private void OnEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        SyncFromEntry(force: false);
    }

    private void SyncFromEntry(bool force)
    {
        var raw = (_entry.Text ?? "").Trim();
        if (!raw.StartsWith('#')) raw = "#" + raw;
        if (TryParseColor(raw, out var c))
        {
            _swatch.Color = c;
            _swatch.BackgroundColor = c;
            if (!string.Equals(HexColor, raw, StringComparison.OrdinalIgnoreCase))
            {
                HexColor = raw;
                ColorChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        else if (force)
        {
            // Revert to last valid
            ApplyHexToControls(HexColor);
        }
    }

    private async void OnPickerClicked(object? sender, EventArgs e)
    {
        var page = GetParentPage();
        if (page == null) return;

        var labels = new string[s_palette.Length];
        for (int i = 0; i < s_palette.Length; i++)
            labels[i] = $"{s_palette[i].Name}  {s_palette[i].Hex}";

        var choice = await page.DisplayActionSheet("Pick a color", "Cancel", null, labels);
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == choice)
            {
                HexColor = s_palette[i].Hex;
                ApplyHexToControls(HexColor);
                ColorChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
    }

    private Page? GetParentPage()
    {
        Element? cur = this;
        while (cur != null)
        {
            if (cur is Page p) return p;
            cur = cur.Parent;
        }
        return null;
    }

    private static bool TryParseColor(string? hex, out Color color)
    {
        color = Colors.White;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var v = hex.Trim();
        if (!v.StartsWith('#')) v = "#" + v;
        // Accept #RGB / #RRGGBB / #AARRGGBB / #RRGGBBAA-style — Color.FromArgb handles most
        try
        {
            var parsed = Color.FromArgb(v);
            if (parsed != null)
            {
                color = parsed;
                return true;
            }
        }
        catch { }

        // Manual fallback for #RRGGBB
        if (v.Length == 7
            && byte.TryParse(v.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && byte.TryParse(v.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && byte.TryParse(v.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            color = Color.FromRgb(r, g, b);
            return true;
        }
        return false;
    }
}
