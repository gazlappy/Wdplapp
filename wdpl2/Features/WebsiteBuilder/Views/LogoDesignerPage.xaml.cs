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
        TextColorEntry.Text = _recipe.TextColor;

        IconEntry.Text = _recipe.Icon;
        IconSizeSlider.Value = _recipe.IconSize;
        IconSizeLabel.Text = ((int)_recipe.IconSize).ToString();

        ShapePicker.SelectedIndex = _recipe.BgShape switch
        {
            "rounded-square" => 1,
            "square"         => 2,
            "shield"         => 3,
            "hexagon"        => 4,
            "none"           => 5,
            _                => 0,
        };

        BgColor1Entry.Text = _recipe.BgColor1;
        BgColor2Entry.Text = _recipe.BgColor2;
        GradientCheck.IsChecked = _recipe.UseGradient;
        GradientDirPicker.SelectedIndex = _recipe.GradientDirection switch
        {
            "vertical"   => 1,
            "horizontal" => 2,
            "radial"     => 3,
            _            => 0,
        };

        PaddingSlider.Value = _recipe.Padding;
        PaddingLabel.Text = ((int)_recipe.Padding).ToString();

        BorderCheck.IsChecked = _recipe.ShowBorder;
        BorderColorEntry.Text = _recipe.BorderColor;
        BorderWidthSlider.Value = _recipe.BorderWidth;
        BorderWidthLabel.Text = ((int)_recipe.BorderWidth).ToString();
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
        _recipe.TextColor = NormalizeHex(TextColorEntry.Text, _recipe.TextColor);

        _recipe.Icon = IconEntry.Text ?? "";
        _recipe.IconSize = (float)IconSizeSlider.Value;
        IconSizeLabel.Text = ((int)_recipe.IconSize).ToString();

        _recipe.BgShape = ShapePicker.SelectedIndex switch
        {
            1 => "rounded-square",
            2 => "square",
            3 => "shield",
            4 => "hexagon",
            5 => "none",
            _ => "circle",
        };
        _recipe.BgColor1 = NormalizeHex(BgColor1Entry.Text, _recipe.BgColor1);
        _recipe.BgColor2 = NormalizeHex(BgColor2Entry.Text, _recipe.BgColor2);
        _recipe.UseGradient = GradientCheck.IsChecked;
        _recipe.GradientDirection = GradientDirPicker.SelectedIndex switch
        {
            1 => "vertical",
            2 => "horizontal",
            3 => "radial",
            _ => "diagonal",
        };

        _recipe.Padding = (float)PaddingSlider.Value;
        PaddingLabel.Text = ((int)_recipe.Padding).ToString();

        _recipe.ShowBorder = BorderCheck.IsChecked;
        _recipe.BorderColor = NormalizeHex(BorderColorEntry.Text, _recipe.BorderColor);
        _recipe.BorderWidth = (float)BorderWidthSlider.Value;
        BorderWidthLabel.Text = ((int)_recipe.BorderWidth).ToString();
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
