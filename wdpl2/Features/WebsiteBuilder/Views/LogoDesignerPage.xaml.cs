using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using Wdpl2.Features.WebsiteBuilder.Logo;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Features.WebsiteBuilder.Views;

public partial class LogoDesignerPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private readonly LogoDesignRecipe _recipe;
    private readonly string? _editingId;
    private bool _loading = true;

    private static readonly string[] s_fontFamilyKeys =
        WebsiteSettings.FontFamilies.Keys.ToArray();

    public LogoDesignerPage() : this(null, null) { }

    /// <summary>
    /// Open the designer either fresh, or to edit an existing catalog item by id.
    /// </summary>
    public LogoDesignerPage(string? existingCatalogId, LogoDesignRecipe? recipe)
    {
        InitializeComponent();

        _editingId = existingCatalogId;
        _recipe = recipe?.Clone() ?? new LogoDesignRecipe();

        // Populate font family picker from the shared FontFamilies dictionary
        FontFamilyPicker.ItemsSource = s_fontFamilyKeys;

        BuildTemplateButtons();

        LoadFromRecipe();
        _loading = false;

        if (!string.IsNullOrEmpty(_editingId))
        {
            ModeLabel.Text = "Editing existing catalog logo";
            var existing = League.WebsiteSettings.LogoCatalog.Find(l => l.Id == _editingId);
            if (existing != null) LogoNameEntry.Text = existing.Name;
        }

        PreviewCanvas.InvalidateSurface();
    }

    private void LoadFromRecipe()
    {
        TextEntry.Text = _recipe.Text;
        UppercaseCheck.IsChecked = _recipe.TextUppercase;

        var familyIdx = System.Array.IndexOf(s_fontFamilyKeys, _recipe.FontFamily);
        FontFamilyPicker.SelectedIndex = familyIdx >= 0 ? familyIdx : 0;

        FontWeightPicker.SelectedIndex = _recipe.FontWeight switch
        {
            "Regular" => 0,
            "Black"   => 2,
            _         => 1,
        };

        FontSizeSlider.Value = _recipe.FontSize;
        FontSizeLabel.Text = ((int)_recipe.FontSize).ToString();
        LetterSpacingSlider.Value = _recipe.LetterSpacing;
        LetterSpacingLabel.Text = ((int)_recipe.LetterSpacing).ToString();
        TextRotationSlider.Value = _recipe.TextRotation;
        TextRotationLabel.Text = ((int)_recipe.TextRotation) + "°";
        TextOffsetYSlider.Value = _recipe.TextOffsetY;
        TextOffsetYLabel.Text = ((int)_recipe.TextOffsetY).ToString();
        TextColorEntry.Text = _recipe.TextColor;

        TextStrokeCheck.IsChecked = _recipe.TextStroke;
        TextStrokeColorEntry.Text = _recipe.TextStrokeColor;
        TextStrokeWidthSlider.Value = _recipe.TextStrokeWidth;
        TextStrokeWidthLabel.Text = ((int)_recipe.TextStrokeWidth).ToString();

        TextShadowCheck.IsChecked = _recipe.TextShadow;
        TextShadowColorEntry.Text = _recipe.TextShadowColor;
        TextShadowBlurSlider.Value = _recipe.TextShadowBlur;
        TextShadowBlurLabel.Text = ((int)_recipe.TextShadowBlur).ToString();
        TextShadowOffsetYSlider.Value = _recipe.TextShadowOffsetY;
        TextShadowOffsetYLabel.Text = ((int)_recipe.TextShadowOffsetY).ToString();

        SubtitleEntry.Text = _recipe.Subtitle;
        SubtitleSizeSlider.Value = _recipe.SubtitleSize;
        SubtitleSizeLabel.Text = ((int)_recipe.SubtitleSize).ToString();
        SubtitleSpacingSlider.Value = _recipe.SubtitleLetterSpacing;
        SubtitleSpacingLabel.Text = ((int)_recipe.SubtitleLetterSpacing).ToString();
        SubtitleColorEntry.Text = _recipe.SubtitleColor;

        IconEntry.Text = _recipe.Icon;
        IconSizeSlider.Value = _recipe.IconSize;
        IconSizeLabel.Text = ((int)_recipe.IconSize).ToString();
        IconRotationSlider.Value = _recipe.IconRotation;
        IconRotationLabel.Text = ((int)_recipe.IconRotation) + "°";
        IconPositionPicker.SelectedIndex = _recipe.IconPosition switch
        {
            "below"  => 1,
            "left"   => 2,
            "right"  => 3,
            "behind" => 4,
            _        => 0,
        };

        ShapePicker.SelectedIndex = _recipe.BgShape switch
        {
            "rounded-square" => 1,
            "square"         => 2,
            "shield"         => 3,
            "hexagon"        => 4,
            "octagon"        => 5,
            "triangle"       => 6,
            "diamond"        => 7,
            "star"           => 8,
            "banner"         => 9,
            "none"           => 10,
            _                => 0,
        };

        BgColor1Entry.Text = _recipe.BgColor1;
        BgColor2Entry.Text = _recipe.BgColor2;
        BgColor3Entry.Text = _recipe.BgColor3;
        GradientCheck.IsChecked = _recipe.UseGradient;
        ThreeColorCheck.IsChecked = _recipe.UseThreeColorGradient;
        GradientDirPicker.SelectedIndex = _recipe.GradientDirection switch
        {
            "vertical"   => 1,
            "horizontal" => 2,
            "radial"     => 3,
            "angle"      => 4,
            _            => 0,
        };
        GradientAngleSlider.Value = _recipe.GradientAngle;
        GradientAngleLabel.Text = ((int)_recipe.GradientAngle) + "°";

        PaddingSlider.Value = _recipe.Padding;
        PaddingLabel.Text = ((int)_recipe.Padding).ToString();

        PatternPicker.SelectedIndex = _recipe.Pattern switch
        {
            "stripes"        => 1,
            "dots"           => 2,
            "grid"           => 3,
            "diagonal-lines" => 4,
            "chevron"        => 5,
            _                => 0,
        };
        PatternColorEntry.Text = _recipe.PatternColor;
        PatternOpacitySlider.Value = _recipe.PatternOpacity;
        PatternOpacityLabel.Text = ((int)(_recipe.PatternOpacity * 100)) + "%";
        PatternScaleSlider.Value = _recipe.PatternScale;
        PatternScaleLabel.Text = ((int)_recipe.PatternScale).ToString();

        BorderCheck.IsChecked = _recipe.ShowBorder;
        BorderColorEntry.Text = _recipe.BorderColor;
        BorderWidthSlider.Value = _recipe.BorderWidth;
        BorderWidthLabel.Text = ((int)_recipe.BorderWidth).ToString();
        BorderStylePicker.SelectedIndex = _recipe.BorderStyle switch
        {
            "dashed" => 1,
            "dotted" => 2,
            "double" => 3,
            _        => 0,
        };

        ShapeShadowCheck.IsChecked = _recipe.ShapeShadow;
        ShapeShadowColorEntry.Text = _recipe.ShapeShadowColor;
        ShapeShadowBlurSlider.Value = _recipe.ShapeShadowBlur;
        ShapeShadowBlurLabel.Text = ((int)_recipe.ShapeShadowBlur).ToString();
        ShapeShadowOffsetYSlider.Value = _recipe.ShapeShadowOffsetY;
        ShapeShadowOffsetYLabel.Text = ((int)_recipe.ShapeShadowOffsetY).ToString();
    }

    private void ApplyControlsToRecipe()
    {
        _recipe.Text = TextEntry.Text ?? "";
        _recipe.TextUppercase = UppercaseCheck.IsChecked;

        if (FontFamilyPicker.SelectedIndex >= 0 && FontFamilyPicker.SelectedIndex < s_fontFamilyKeys.Length)
            _recipe.FontFamily = s_fontFamilyKeys[FontFamilyPicker.SelectedIndex];

        _recipe.FontWeight = FontWeightPicker.SelectedIndex switch
        {
            0 => "Regular",
            2 => "Black",
            _ => "Bold",
        };

        _recipe.FontSize = (float)FontSizeSlider.Value;
        FontSizeLabel.Text = ((int)_recipe.FontSize).ToString();
        _recipe.LetterSpacing = (float)LetterSpacingSlider.Value;
        LetterSpacingLabel.Text = ((int)_recipe.LetterSpacing).ToString();
        _recipe.TextRotation = (float)TextRotationSlider.Value;
        TextRotationLabel.Text = ((int)_recipe.TextRotation) + "°";
        _recipe.TextOffsetY = (float)TextOffsetYSlider.Value;
        TextOffsetYLabel.Text = ((int)_recipe.TextOffsetY).ToString();
        _recipe.TextColor = NormalizeHex(TextColorEntry.Text, _recipe.TextColor);

        _recipe.TextStroke = TextStrokeCheck.IsChecked;
        _recipe.TextStrokeColor = NormalizeHex(TextStrokeColorEntry.Text, _recipe.TextStrokeColor);
        _recipe.TextStrokeWidth = (float)TextStrokeWidthSlider.Value;
        TextStrokeWidthLabel.Text = ((int)_recipe.TextStrokeWidth).ToString();

        _recipe.TextShadow = TextShadowCheck.IsChecked;
        _recipe.TextShadowColor = NormalizeHex(TextShadowColorEntry.Text, _recipe.TextShadowColor);
        _recipe.TextShadowBlur = (float)TextShadowBlurSlider.Value;
        TextShadowBlurLabel.Text = ((int)_recipe.TextShadowBlur).ToString();
        _recipe.TextShadowOffsetY = (float)TextShadowOffsetYSlider.Value;
        TextShadowOffsetYLabel.Text = ((int)_recipe.TextShadowOffsetY).ToString();

        _recipe.Subtitle = SubtitleEntry.Text ?? "";
        _recipe.SubtitleSize = (float)SubtitleSizeSlider.Value;
        SubtitleSizeLabel.Text = ((int)_recipe.SubtitleSize).ToString();
        _recipe.SubtitleLetterSpacing = (float)SubtitleSpacingSlider.Value;
        SubtitleSpacingLabel.Text = ((int)_recipe.SubtitleLetterSpacing).ToString();
        _recipe.SubtitleColor = NormalizeHex(SubtitleColorEntry.Text, _recipe.SubtitleColor);

        _recipe.Icon = IconEntry.Text ?? "";
        _recipe.IconSize = (float)IconSizeSlider.Value;
        IconSizeLabel.Text = ((int)_recipe.IconSize).ToString();
        _recipe.IconRotation = (float)IconRotationSlider.Value;
        IconRotationLabel.Text = ((int)_recipe.IconRotation) + "°";
        _recipe.IconPosition = IconPositionPicker.SelectedIndex switch
        {
            1 => "below",
            2 => "left",
            3 => "right",
            4 => "behind",
            _ => "above",
        };

        _recipe.BgShape = ShapePicker.SelectedIndex switch
        {
            1 => "rounded-square",
            2 => "square",
            3 => "shield",
            4 => "hexagon",
            5 => "octagon",
            6 => "triangle",
            7 => "diamond",
            8 => "star",
            9 => "banner",
            10 => "none",
            _ => "circle",
        };
        _recipe.BgColor1 = NormalizeHex(BgColor1Entry.Text, _recipe.BgColor1);
        _recipe.BgColor2 = NormalizeHex(BgColor2Entry.Text, _recipe.BgColor2);
        _recipe.BgColor3 = NormalizeHex(BgColor3Entry.Text, _recipe.BgColor3);
        _recipe.UseGradient = GradientCheck.IsChecked;
        _recipe.UseThreeColorGradient = ThreeColorCheck.IsChecked;
        _recipe.GradientDirection = GradientDirPicker.SelectedIndex switch
        {
            1 => "vertical",
            2 => "horizontal",
            3 => "radial",
            4 => "angle",
            _ => "diagonal",
        };
        _recipe.GradientAngle = (float)GradientAngleSlider.Value;
        GradientAngleLabel.Text = ((int)_recipe.GradientAngle) + "°";

        _recipe.Padding = (float)PaddingSlider.Value;
        PaddingLabel.Text = ((int)_recipe.Padding).ToString();

        _recipe.Pattern = PatternPicker.SelectedIndex switch
        {
            1 => "stripes",
            2 => "dots",
            3 => "grid",
            4 => "diagonal-lines",
            5 => "chevron",
            _ => "none",
        };
        _recipe.PatternColor = NormalizeHex(PatternColorEntry.Text, _recipe.PatternColor);
        _recipe.PatternOpacity = (float)PatternOpacitySlider.Value;
        PatternOpacityLabel.Text = ((int)(_recipe.PatternOpacity * 100)) + "%";
        _recipe.PatternScale = (float)PatternScaleSlider.Value;
        PatternScaleLabel.Text = ((int)_recipe.PatternScale).ToString();

        _recipe.ShowBorder = BorderCheck.IsChecked;
        _recipe.BorderColor = NormalizeHex(BorderColorEntry.Text, _recipe.BorderColor);
        _recipe.BorderWidth = (float)BorderWidthSlider.Value;
        BorderWidthLabel.Text = ((int)_recipe.BorderWidth).ToString();
        _recipe.BorderStyle = BorderStylePicker.SelectedIndex switch
        {
            1 => "dashed",
            2 => "dotted",
            3 => "double",
            _ => "solid",
        };

        _recipe.ShapeShadow = ShapeShadowCheck.IsChecked;
        _recipe.ShapeShadowColor = NormalizeHex(ShapeShadowColorEntry.Text, _recipe.ShapeShadowColor);
        _recipe.ShapeShadowBlur = (float)ShapeShadowBlurSlider.Value;
        ShapeShadowBlurLabel.Text = ((int)_recipe.ShapeShadowBlur).ToString();
        _recipe.ShapeShadowOffsetY = (float)ShapeShadowOffsetYSlider.Value;
        ShapeShadowOffsetYLabel.Text = ((int)_recipe.ShapeShadowOffsetY).ToString();
    }

    private void BuildTemplateButtons()
    {
        TemplatesFlex.Children.Clear();
        foreach (var t in LogoDesignRecipe.Templates)
        {
            var b = new Button
            {
                Text = $"{t.Emoji} {t.Name}",
                FontSize = 11,
                Padding = new Thickness(10, 4),
                Margin = new Thickness(0, 0, 4, 4),
                BackgroundColor = Color.FromArgb("#E0E7FF"),
                TextColor = Color.FromArgb("#1E3A8A"),
                CommandParameter = t
            };
            b.Clicked += OnTemplateClicked;
            TemplatesFlex.Children.Add(b);
        }
    }

    private void OnTemplateClicked(object? sender, System.EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is LogoDesignRecipe.Template t)
        {
            ApplyRecipe(t.Build());
        }
    }

    private void OnRandomClicked(object? sender, System.EventArgs e) => ApplyRecipe(LogoDesignRecipe.Random());

    private void OnResetClicked(object? sender, System.EventArgs e) => ApplyRecipe(new LogoDesignRecipe());

    private void ApplyRecipe(LogoDesignRecipe recipe)
    {
        // Preserve user-entered text/icon if they've customized it
        var preservedText = string.IsNullOrWhiteSpace(TextEntry.Text) ? recipe.Text : TextEntry.Text;
        CopyRecipe(recipe, _recipe);
        _recipe.Text = preservedText;

        _loading = true;
        LoadFromRecipe();
        _loading = false;
        PreviewCanvas.InvalidateSurface();
    }

    private static void CopyRecipe(LogoDesignRecipe from, LogoDesignRecipe to)
    {
        var json = from.ToJson();
        var src = LogoDesignRecipe.FromJson(json) ?? new LogoDesignRecipe();
        foreach (var p in typeof(LogoDesignRecipe).GetProperties())
        {
            if (p.CanRead && p.CanWrite)
                p.SetValue(to, p.GetValue(src));
        }
    }

    private static string NormalizeHex(string? input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;
        var v = input.Trim();
        if (!v.StartsWith('#')) v = "#" + v;
        return SKColor.TryParse(v, out _) ? v : fallback;
    }

    private void OnAnyChanged(object? sender, System.EventArgs e)
    {
        if (_loading) return;
        ApplyControlsToRecipe();
        PreviewCanvas.InvalidateSurface();
    }

    private void OnIconQuickPick(object? sender, System.EventArgs e)
    {
        if (sender is Button b && !string.IsNullOrEmpty(b.Text))
        {
            IconEntry.Text = b.Text;
        }
    }

    private void OnIconClear(object? sender, System.EventArgs e)
    {
        IconEntry.Text = "";
    }

    private void OnPreset(object? sender, System.EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is string param)
        {
            var parts = param.Split('|');
            if (parts.Length == 2)
            {
                BgColor1Entry.Text = parts[0];
                BgColor2Entry.Text = parts[1];
                GradientCheck.IsChecked = true;
            }
        }
    }

    private void OnPaintPreview(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Draw a soft checkerboard so transparent backgrounds are visible
        DrawCheckerboard(canvas, e.Info.Width, e.Info.Height);

        LogoRenderer.Draw(canvas, new SKSize(e.Info.Width, e.Info.Height), _recipe);
    }

    private static void DrawCheckerboard(SKCanvas canvas, int w, int h)
    {
        const int tile = 16;
        using var light = new SKPaint { Color = new SKColor(0xF8, 0xFA, 0xFC), Style = SKPaintStyle.Fill };
        using var dark = new SKPaint { Color = new SKColor(0xE2, 0xE8, 0xF0), Style = SKPaintStyle.Fill };
        for (int y = 0; y < h; y += tile)
        {
            for (int x = 0; x < w; x += tile)
            {
                var paint = ((x / tile + y / tile) % 2 == 0) ? light : dark;
                canvas.DrawRect(new SKRect(x, y, x + tile, y + tile), paint);
            }
        }
    }

    private async void OnCancelClicked(object? sender, System.EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnSaveClicked(object? sender, System.EventArgs e)
    {
        try
        {
            ApplyControlsToRecipe();

            var name = (LogoNameEntry.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                name = string.IsNullOrWhiteSpace(_recipe.Text) ? "Designed Logo" : _recipe.Text.Trim();
            }

            var png = LogoRenderer.RenderPng(_recipe, 512);
            var json = _recipe.ToJson();

            if (!string.IsNullOrEmpty(_editingId))
            {
                League.WebsiteSettings.UpdateDesignedLogoCatalogItem(_editingId, name, png, json);
            }
            else
            {
                League.WebsiteSettings.AddDesignedLogoCatalogItem(name, png, json, "", "Designed");
            }

            DataStore.Save();
            await DisplayAlert("Saved", $"Logo '{name}' saved to catalog.", "OK");
            await Navigation.PopAsync();
        }
        catch (System.Exception ex)
        {
            await DisplayAlert("Error", $"Could not save logo: {ex.Message}", "OK");
        }
    }
}
