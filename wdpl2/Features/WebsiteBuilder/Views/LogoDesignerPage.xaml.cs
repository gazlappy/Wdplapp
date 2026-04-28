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
        TextColorPicker.HexColor = _recipe.TextColor;

        TextStrokeCheck.IsChecked = _recipe.TextStroke;
        TextStrokeColorPicker.HexColor = _recipe.TextStrokeColor;
        TextStrokeWidthSlider.Value = _recipe.TextStrokeWidth;
        TextStrokeWidthLabel.Text = ((int)_recipe.TextStrokeWidth).ToString();

        TextShadowCheck.IsChecked = _recipe.TextShadow;
        TextShadowColorPicker.HexColor = _recipe.TextShadowColor;
        TextShadowBlurSlider.Value = _recipe.TextShadowBlur;
        TextShadowBlurLabel.Text = ((int)_recipe.TextShadowBlur).ToString();
        TextShadowOffsetYSlider.Value = _recipe.TextShadowOffsetY;
        TextShadowOffsetYLabel.Text = ((int)_recipe.TextShadowOffsetY).ToString();

        SubtitleEntry.Text = _recipe.Subtitle;
        SubtitleSizeSlider.Value = _recipe.SubtitleSize;
        SubtitleSizeLabel.Text = ((int)_recipe.SubtitleSize).ToString();
        SubtitleSpacingSlider.Value = _recipe.SubtitleLetterSpacing;
        SubtitleSpacingLabel.Text = ((int)_recipe.SubtitleLetterSpacing).ToString();
        SubtitleColorPicker.HexColor = _recipe.SubtitleColor;

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

        BgColor1Picker.HexColor = _recipe.BgColor1;
        BgColor2Picker.HexColor = _recipe.BgColor2;
        BgColor3Picker.HexColor = _recipe.BgColor3;
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
        PatternColorPicker.HexColor = _recipe.PatternColor;
        PatternOpacitySlider.Value = _recipe.PatternOpacity;
        PatternOpacityLabel.Text = ((int)(_recipe.PatternOpacity * 100)) + "%";
        PatternScaleSlider.Value = _recipe.PatternScale;
        PatternScaleLabel.Text = ((int)_recipe.PatternScale).ToString();

        BorderCheck.IsChecked = _recipe.ShowBorder;
        BorderColorPicker.HexColor = _recipe.BorderColor;
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
        ShapeShadowColorPicker.HexColor = _recipe.ShapeShadowColor;
        ShapeShadowBlurSlider.Value = _recipe.ShapeShadowBlur;
        ShapeShadowBlurLabel.Text = ((int)_recipe.ShapeShadowBlur).ToString();
        ShapeShadowOffsetYSlider.Value = _recipe.ShapeShadowOffsetY;
        ShapeShadowOffsetYLabel.Text = ((int)_recipe.ShapeShadowOffsetY).ToString();

        RebuildLayersUI();
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
        _recipe.TextColor = NormalizeHex(TextColorPicker.HexColor, _recipe.TextColor);

        _recipe.TextStroke = TextStrokeCheck.IsChecked;
        _recipe.TextStrokeColor = NormalizeHex(TextStrokeColorPicker.HexColor, _recipe.TextStrokeColor);
        _recipe.TextStrokeWidth = (float)TextStrokeWidthSlider.Value;
        TextStrokeWidthLabel.Text = ((int)_recipe.TextStrokeWidth).ToString();

        _recipe.TextShadow = TextShadowCheck.IsChecked;
        _recipe.TextShadowColor = NormalizeHex(TextShadowColorPicker.HexColor, _recipe.TextShadowColor);
        _recipe.TextShadowBlur = (float)TextShadowBlurSlider.Value;
        TextShadowBlurLabel.Text = ((int)_recipe.TextShadowBlur).ToString();
        _recipe.TextShadowOffsetY = (float)TextShadowOffsetYSlider.Value;
        TextShadowOffsetYLabel.Text = ((int)_recipe.TextShadowOffsetY).ToString();

        _recipe.Subtitle = SubtitleEntry.Text ?? "";
        _recipe.SubtitleSize = (float)SubtitleSizeSlider.Value;
        SubtitleSizeLabel.Text = ((int)_recipe.SubtitleSize).ToString();
        _recipe.SubtitleLetterSpacing = (float)SubtitleSpacingSlider.Value;
        SubtitleSpacingLabel.Text = ((int)_recipe.SubtitleLetterSpacing).ToString();
        _recipe.SubtitleColor = NormalizeHex(SubtitleColorPicker.HexColor, _recipe.SubtitleColor);

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
        _recipe.BgColor1 = NormalizeHex(BgColor1Picker.HexColor, _recipe.BgColor1);
        _recipe.BgColor2 = NormalizeHex(BgColor2Picker.HexColor, _recipe.BgColor2);
        _recipe.BgColor3 = NormalizeHex(BgColor3Picker.HexColor, _recipe.BgColor3);
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
        _recipe.PatternColor = NormalizeHex(PatternColorPicker.HexColor, _recipe.PatternColor);
        _recipe.PatternOpacity = (float)PatternOpacitySlider.Value;
        PatternOpacityLabel.Text = ((int)(_recipe.PatternOpacity * 100)) + "%";
        _recipe.PatternScale = (float)PatternScaleSlider.Value;
        PatternScaleLabel.Text = ((int)_recipe.PatternScale).ToString();

        _recipe.ShowBorder = BorderCheck.IsChecked;
        _recipe.BorderColor = NormalizeHex(BorderColorPicker.HexColor, _recipe.BorderColor);
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
        _recipe.ShapeShadowColor = NormalizeHex(ShapeShadowColorPicker.HexColor, _recipe.ShapeShadowColor);
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
                BgColor1Picker.HexColor = parts[0];
                BgColor2Picker.HexColor = parts[1];
                GradientCheck.IsChecked = true;
            }
        }
    }

    // ===== Shape layers UI =================================================

    private void RebuildLayersUI()
    {
        LayersStack.Children.Clear();
        for (int i = 0; i < _recipe.Layers.Count; i++)
        {
            LayersStack.Children.Add(BuildLayerEditor(_recipe.Layers[i], i));
        }
    }

    private Frame BuildLayerEditor(LogoShapeLayer layer, int index)
    {
        var shapeKeys = Wdpl2.Features.WebsiteBuilder.Logo.ShapeCatalog.All.Select(s => s.Key).ToArray();
        var shapeLabels = Wdpl2.Features.WebsiteBuilder.Logo.ShapeCatalog.All.Select(s => $"{s.Emoji} {s.DisplayName}").ToArray();

        var shapePicker = new Picker { Title = "Shape", FontSize = 12, ItemsSource = shapeLabels };
        var idx = System.Array.IndexOf(shapeKeys, layer.Shape);
        shapePicker.SelectedIndex = idx >= 0 ? idx : 0;
        shapePicker.SelectedIndexChanged += (_, __) =>
        {
            if (shapePicker.SelectedIndex >= 0 && shapePicker.SelectedIndex < shapeKeys.Length)
            {
                layer.Shape = shapeKeys[shapePicker.SelectedIndex];
                PreviewCanvas.InvalidateSurface();
            }
        };

        var fillPicker = new Wdpl2.Features.WebsiteBuilder.Views.Controls.ColorPickerView { HexColor = layer.FillColor };
        fillPicker.ColorChanged += (_, __) => { layer.FillColor = fillPicker.HexColor; PreviewCanvas.InvalidateSurface(); };

        var gradCheck = new CheckBox { IsChecked = layer.UseGradient };
        gradCheck.CheckedChanged += (_, __) => { layer.UseGradient = gradCheck.IsChecked; PreviewCanvas.InvalidateSurface(); };

        var fill2Picker = new Wdpl2.Features.WebsiteBuilder.Views.Controls.ColorPickerView { HexColor = layer.FillColor2 };
        fill2Picker.ColorChanged += (_, __) => { layer.FillColor2 = fill2Picker.HexColor; PreviewCanvas.InvalidateSurface(); };

        var strokeCheck = new CheckBox { IsChecked = layer.Stroke };
        strokeCheck.CheckedChanged += (_, __) => { layer.Stroke = strokeCheck.IsChecked; PreviewCanvas.InvalidateSurface(); };
        var strokePicker = new Wdpl2.Features.WebsiteBuilder.Views.Controls.ColorPickerView { HexColor = layer.StrokeColor };
        strokePicker.ColorChanged += (_, __) => { layer.StrokeColor = strokePicker.HexColor; PreviewCanvas.InvalidateSurface(); };
        var strokeWidth = MakeSlider(1, 30, layer.StrokeWidth, v => { layer.StrokeWidth = (float)v; PreviewCanvas.InvalidateSurface(); });

        var x = MakeSlider(0, 1, layer.CenterX, v => { layer.CenterX = (float)v; PreviewCanvas.InvalidateSurface(); });
        var y = MakeSlider(0, 1, layer.CenterY, v => { layer.CenterY = (float)v; PreviewCanvas.InvalidateSurface(); });
        var w = MakeSlider(0.05, 1.5, layer.Width,  v => { layer.Width  = (float)v; PreviewCanvas.InvalidateSurface(); });
        var hSl = MakeSlider(0.05, 1.5, layer.Height, v => { layer.Height = (float)v; PreviewCanvas.InvalidateSurface(); });
        var rot = MakeSlider(-180, 180, layer.Rotation, v => { layer.Rotation = (float)v; PreviewCanvas.InvalidateSurface(); });
        var op  = MakeSlider(0, 1, layer.Opacity, v => { layer.Opacity = (float)v; PreviewCanvas.InvalidateSurface(); });

        var aboveCheck = new CheckBox { IsChecked = layer.AboveText };
        aboveCheck.CheckedChanged += (_, __) => { layer.AboveText = aboveCheck.IsChecked; PreviewCanvas.InvalidateSurface(); };

        var upBtn   = new Button { Text = "↑",  FontSize = 12, Padding = new Thickness(8, 2), BackgroundColor = Color.FromArgb("#E5E7EB"), TextColor = Colors.Black };
        var downBtn = new Button { Text = "↓",  FontSize = 12, Padding = new Thickness(8, 2), BackgroundColor = Color.FromArgb("#E5E7EB"), TextColor = Colors.Black };
        var dupBtn  = new Button { Text = "⧉",  FontSize = 12, Padding = new Thickness(8, 2), BackgroundColor = Color.FromArgb("#DBEAFE"), TextColor = Color.FromArgb("#1E3A8A") };
        var delBtn  = new Button { Text = "✕",  FontSize = 12, Padding = new Thickness(8, 2), BackgroundColor = Color.FromArgb("#FEE2E2"), TextColor = Color.FromArgb("#B91C1C") };
        upBtn.Clicked   += (_, __) => MoveLayer(index, -1);
        downBtn.Clicked += (_, __) => MoveLayer(index, +1);
        dupBtn.Clicked  += (_, __) => DuplicateLayer(index);
        delBtn.Clicked  += (_, __) => RemoveLayer(index);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = GridLength.Auto }), ColumnSpacing = 4 };
        var title = new Label { Text = $"Layer {index + 1}", FontAttributes = FontAttributes.Bold, FontSize = 12, VerticalOptions = LayoutOptions.Center };
        header.Children.Add(title);   Microsoft.Maui.Controls.Grid.SetColumn(title,   0);
        header.Children.Add(upBtn);   Microsoft.Maui.Controls.Grid.SetColumn(upBtn,   1);
        header.Children.Add(downBtn); Microsoft.Maui.Controls.Grid.SetColumn(downBtn, 2);
        header.Children.Add(dupBtn);  Microsoft.Maui.Controls.Grid.SetColumn(dupBtn,  3);
        header.Children.Add(delBtn);  Microsoft.Maui.Controls.Grid.SetColumn(delBtn,  4);

        var body = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                header,
                new Label { Text = "Shape", FontSize = 11, TextColor = Color.FromArgb("#6B7280") },
                shapePicker,
                LabelledRow("X", x),
                LabelledRow("Y", y),
                LabelledRow("W", w),
                LabelledRow("H", hSl),
                LabelledRow("Rot", rot),
                LabelledRow("Opacity", op),
                new Label { Text = "Fill", FontSize = 11, TextColor = Color.FromArgb("#6B7280") },
                fillPicker,
                CheckRow(gradCheck, "Use gradient → second color:"),
                fill2Picker,
                CheckRow(strokeCheck, "Outline"),
                strokePicker,
                LabelledRow("Stroke W", strokeWidth),
                CheckRow(aboveCheck, "Draw above text (in front)")
            }
        };

        return new Frame
        {
            Padding = 8,
            CornerRadius = 6,
            BorderColor = Color.FromArgb("#E5E7EB"),
            BackgroundColor = Colors.White,
            HasShadow = false,
            Content = body
        };
    }

    private static Slider MakeSlider(double min, double max, double value, System.Action<double> onChanged)
    {
        var s = new Slider { Minimum = min, Maximum = max, Value = System.Math.Clamp(value, min, max) };
        s.ValueChanged += (_, e) => onChanged(e.NewValue);
        return s;
    }

    private static Grid LabelledRow(string label, View control)
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition { Width = new GridLength(60) },
                new ColumnDefinition { Width = GridLength.Star }),
            ColumnSpacing = 6
        };
        var l = new Label { Text = label, FontSize = 11, VerticalOptions = LayoutOptions.Center };
        g.Children.Add(l);       Microsoft.Maui.Controls.Grid.SetColumn(l,       0);
        g.Children.Add(control); Microsoft.Maui.Controls.Grid.SetColumn(control, 1);
        return g;
    }

    private static HorizontalStackLayout CheckRow(CheckBox check, string text) =>
        new()
        {
            Spacing = 4,
            Children = { check, new Label { Text = text, FontSize = 11, VerticalOptions = LayoutOptions.Center } }
        };

    private async void OnAddLayerClicked(object? sender, System.EventArgs e)
    {
        var labels = Wdpl2.Features.WebsiteBuilder.Logo.ShapeCatalog.All
            .Select(s => $"{s.Emoji} {s.DisplayName}").ToArray();
        var choice = await DisplayActionSheet("Pick a shape to add", "Cancel", null, labels);
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        var idx = System.Array.IndexOf(labels, choice);
        if (idx < 0) return;
        var info = Wdpl2.Features.WebsiteBuilder.Logo.ShapeCatalog.All[idx];

        _recipe.Layers.Add(new LogoShapeLayer
        {
            Shape = info.Key,
            CenterX = 0.5f, CenterY = 0.5f,
            Width = 0.35f, Height = 0.35f,
            FillColor = "#FBBF24",
            Opacity = 0.9f
        });
        RebuildLayersUI();
        PreviewCanvas.InvalidateSurface();
    }

    private void MoveLayer(int index, int delta)
    {
        var to = index + delta;
        if (index < 0 || index >= _recipe.Layers.Count || to < 0 || to >= _recipe.Layers.Count) return;
        (_recipe.Layers[index], _recipe.Layers[to]) = (_recipe.Layers[to], _recipe.Layers[index]);
        RebuildLayersUI();
        PreviewCanvas.InvalidateSurface();
    }

    private void DuplicateLayer(int index)
    {
        if (index < 0 || index >= _recipe.Layers.Count) return;
        _recipe.Layers.Insert(index + 1, _recipe.Layers[index].Clone());
        RebuildLayersUI();
        PreviewCanvas.InvalidateSurface();
    }

    private void RemoveLayer(int index)
    {
        if (index < 0 || index >= _recipe.Layers.Count) return;
        _recipe.Layers.RemoveAt(index);
        RebuildLayersUI();
        PreviewCanvas.InvalidateSurface();
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
