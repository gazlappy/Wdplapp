using Microsoft.Maui.Storage;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using Wdpl2.Features.WebsiteBuilder.Logo;
using Wdpl2.Models;

namespace Wdpl2.Features.WebsiteBuilder.Views;

public partial class LogoDesignerPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private LogoDesignRecipe _recipe = new();
    private string? _editingId;
    private bool _suppress;

    private enum SelKind { None, Background, Text, Subtitle, Icon, Layer, Image }
    private SelKind _selKind = SelKind.None;
    private int _selLayer = -1;

    // Drag state
    private bool _dragging;
    private SKPoint _dragStartCanvas;
    private float _dragStartCx, _dragStartCy;
    private float _dragStartTextOffsetY;
    private float _dragStartImgX, _dragStartImgY;
    private float _dragStartIconX, _dragStartIconY;

    // Last canvas size used to draw, for hit-testing in canvas pixels
    private SKSize _lastSize = new(480, 480);

    public LogoDesignerPage() : this(null, null) { }

    public LogoDesignerPage(string? existingCatalogId, LogoDesignRecipe? recipe)
    {
        InitializeComponent();

        _editingId = existingCatalogId;
        _recipe = recipe?.Clone() ?? LogoDesignRecipe.Random();

        if (_editingId != null)
        {
            var item = League.WebsiteSettings.LogoCatalog.Find(l => l.Id == _editingId);
            if (item != null) NameEntry.Text = item.Name;
            Title = "Edit Logo";
        }
        else
        {
            NameEntry.Text = "New Logo";
        }

        // Populate shape pickers
        var shapeKeys = ShapeCatalog.All.Select(s => s.Key).ToList();
        BgShapePicker.ItemsSource = shapeKeys;
        LayerShapePicker.ItemsSource = shapeKeys;

        // Populate font picker with fonts actually installed on this machine.
        // (Web-only families like "Inter" silently fall back to the default — looks like nothing changes.)
        FontPicker.ItemsSource = GetInstalledFontFamilies();

        // Populate icon category picker
        var cats = new List<string> { "All" };
        cats.AddRange(IconCatalog.Categories);
        IconCategoryPicker.ItemsSource = cats;
        IconCategoryPicker.SelectedIndex = 0;

        PopulateTemplatesRow();
        PopulateShapesRow();
        PopulateIconRow("All");
        PopulateImageGallery();

        // Enable mouse-wheel horizontal scrolling on every ribbon panel (Windows).
        foreach (var sv in new[] { TemplatesPanel, ShapesPanel, QuickPanel, BgPanel, TextPanel,
                                    SubtitlePanel, LayerPanel, IconControlsScroll, IconGridScroll,
                                    ImageControlsScroll, ImageGalleryScroll })
        {
            EnableHorizontalWheelScroll(sv);
        }

        SyncRibbonFromRecipe();
        SetActiveTab("templates");
        UpdateSelectionUI();
    }

    private static void EnableHorizontalWheelScroll(ScrollView sv)
    {
#if WINDOWS
        Microsoft.UI.Xaml.Controls.ScrollViewer? hooked = null;
        var handler = new Microsoft.UI.Xaml.Input.PointerEventHandler(OnWheel);

        void Hook()
        {
            if (sv.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ScrollViewer native && native != hooked)
            {
                hooked = native;
                // ScrollViewer marks wheel as Handled internally before bubble — must subscribe with handledEventsToo.
                native.AddHandler(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent, handler, true);
                // Subtle, auto-hide overlay scrollbars (mouse-indicator style instead of chunky always-on).
                native.HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto;
                native.HorizontalScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode.Enabled;
                native.VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Disabled;
            }
        }
        sv.HandlerChanged += (_, __) => Hook();
        Hook();

        static void OnWheel(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (s is not Microsoft.UI.Xaml.Controls.ScrollViewer sv2) return;
            var delta = e.GetCurrentPoint(sv2).Properties.MouseWheelDelta;
            if (delta == 0) return;
            sv2.ChangeView(sv2.HorizontalOffset - delta, null, null, false);
            e.Handled = true;
        }
#endif
    }

    private static List<string> GetInstalledFontFamilies()
    {
        var preferred = new[]
        {
            "Segoe UI", "Segoe UI Black", "Arial", "Arial Black", "Calibri", "Cambria",
            "Georgia", "Times New Roman", "Verdana", "Tahoma", "Trebuchet MS",
            "Courier New", "Consolas", "Lucida Console", "Comic Sans MS", "Impact",
            "Franklin Gothic Medium", "Bahnschrift", "Constantia", "Corbel", "Candara",
            "Palatino Linotype", "Book Antiqua", "Garamond", "Century Gothic", "Rockwell",
            "Copperplate Gothic Bold", "Bauhaus 93", "Broadway", "Stencil", "Showcard Gothic",
            "Brush Script MT", "Lucida Handwriting", "Monotype Corsiva", "Papyrus",
            "Inter", "Roboto", "Open Sans", "Poppins", "Lato", "Montserrat", "Nunito", "Raleway"
        };

        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var name in SKFontManager.Default.GetFontFamilies())
                installed.Add(name);
        }
        catch { /* ignore — fall back to preferred list */ }

        var result = new List<string>();
        foreach (var p in preferred)
            if (installed.Count == 0 || installed.Contains(p))
                result.Add(p);

        // Append any other installed family not already in the preferred list (alphabetical)
        foreach (var n in installed.OrderBy(n => n))
            if (!result.Contains(n, StringComparer.OrdinalIgnoreCase))
                result.Add(n);

        return result;
    }

    private void PopulateIconRow(string category)
    {
        IconRow.Children.Clear();
        var items = string.Equals(category, "All", StringComparison.OrdinalIgnoreCase)
            ? IconCatalog.All
            : IconCatalog.All.Where(i => i.Category == category).ToList();
        foreach (var ic in items)
        {
            var glyph = ic.Glyph;
            var btn = new Button
            {
                Text = glyph,
                FontSize = 22,
                WidthRequest = 44,
                HeightRequest = 44,
                Padding = 0,
                BackgroundColor = Color.FromArgb("#FFFFFF"),
                TextColor = Color.FromArgb("#0F172A"),
                BorderColor = Color.FromArgb("#CBD5E1"),
                BorderWidth = 1,
                CornerRadius = 6
            };
            ToolTipProperties.SetText(btn, ic.Name);
            btn.Clicked += (_, __) =>
            {
                _recipe.Icon = glyph;
                _suppress = true;
                try { IconEntry.Text = glyph; } finally { _suppress = false; }
                Select(SelKind.Icon);
                Preview.InvalidateSurface();
            };
            IconRow.Children.Add(btn);
        }
    }

    private void OnIconCategoryChanged(object sender, EventArgs e)
    {
        if (IconCategoryPicker.SelectedItem is string cat) PopulateIconRow(cat);
    }

    private void OnIconClearClicked(object sender, EventArgs e)
    {
        _recipe.Icon = "";
        _suppress = true;
        try { IconEntry.Text = ""; } finally { _suppress = false; }
        Preview.InvalidateSurface();
    }

    // =========================================================================
    // TAB SYSTEM
    // =========================================================================
    private string _activeTab = "templates";

    private void OnTabClicked(object sender, EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is string name) SetActiveTab(name);
    }

    private void SetActiveTab(string name)
    {
        _activeTab = name;
        TemplatesPanel.IsVisible = name == "templates";
        ShapesPanel.IsVisible    = name == "shapes";
        BgPanel.IsVisible        = name == "background";
        TextPanel.IsVisible      = name == "text";
        SubtitlePanel.IsVisible  = name == "subtitle";
        IconPanel.IsVisible      = name == "icon";
        ImagePanel.IsVisible     = name == "image";
        LayerPanel.IsVisible     = name == "layer";
        QuickPanel.IsVisible     = name == "quick";

        var active = (Style)Resources["TabBtnActive"];
        var idle   = (Style)Resources["TabBtn"];
        TabTemplatesBtn.Style = name == "templates"  ? active : idle;
        TabShapesBtn.Style    = name == "shapes"     ? active : idle;
        TabBgBtn.Style        = name == "background" ? active : idle;
        TabTextBtn.Style      = name == "text"       ? active : idle;
        TabSubBtn.Style       = name == "subtitle"   ? active : idle;
        TabIconBtn.Style      = name == "icon"       ? active : idle;
        TabImageBtn.Style     = name == "image"      ? active : idle;
        TabLayerBtn.Style     = name == "layer"      ? active : idle;
        TabQuickBtn.Style     = name == "quick"      ? active : idle;
    }

    private void PopulateTemplatesRow()
    {
        TemplatesRow.Children.Clear();
        foreach (var t in LogoDesignRecipe.Templates)
        {
            var template = t;
            var btn = new Button
            {
                Text = $"{t.Emoji}  {t.Name}",
                FontSize = 12,
                Padding = new Thickness(10, 6),
                BackgroundColor = Color.FromArgb("#FFFFFF"),
                TextColor = Color.FromArgb("#0F172A"),
                BorderColor = Color.FromArgb("#CBD5E1"),
                BorderWidth = 1,
                CornerRadius = 6
            };
            btn.Clicked += (_, __) =>
            {
                _recipe = template.Build();
                Select(SelKind.None);
                SyncRibbonFromRecipe();
                Preview.InvalidateSurface();
            };
            TemplatesRow.Children.Add(btn);
        }
    }

    private void PopulateShapesRow()
    {
        ShapesRow.Children.Clear();
        foreach (var s in ShapeCatalog.All.Where(x => x.Key != "none"))
        {
            var info = s;
            var btn = new Button
            {
                Text = s.Emoji,
                FontSize = 20,
                WidthRequest = 48,
                HeightRequest = 48,
                Padding = 0,
                BackgroundColor = Color.FromArgb("#FFFFFF"),
                TextColor = Color.FromArgb("#0F172A"),
                BorderColor = Color.FromArgb("#CBD5E1"),
                BorderWidth = 1,
                CornerRadius = 6
            };
            ToolTipProperties.SetText(btn, s.DisplayName);
            btn.Clicked += (_, __) =>
            {
                var layer = new LogoShapeLayer
                {
                    Shape = info.Key,
                    CenterX = 0.5f, CenterY = 0.5f,
                    Width = 0.4f, Height = 0.4f,
                    FillColor = "#FFFFFF", Opacity = 0.85f,
                };
                _recipe.Layers.Add(layer);
                Select(SelKind.Layer, _recipe.Layers.Count - 1);
                SetActiveTab("layer");
                Preview.InvalidateSurface();
            };
            ShapesRow.Children.Add(btn);
        }
    }

    // =========================================================================
    // PAINT
    // =========================================================================
    private void OnPaintPreview(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var size = new SKSize(e.Info.Width, e.Info.Height);
        _lastSize = size;
        LogoRenderer.Draw(canvas, size, _recipe);
        DrawSelectionOverlay(canvas, size);
    }

    private void DrawSelectionOverlay(SKCanvas canvas, SKSize size)
    {
        var rect = GetSelectionBounds(size);
        if (rect == null) return;

        using var dash = SKPathEffect.CreateDash(new[] { 8f, 6f }, 0);
        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            Color = new SKColor(0x38, 0xBD, 0xF8),
            PathEffect = dash,
            IsAntialias = true
        };
        canvas.DrawRect(rect.Value, stroke);

        // Corner handles
        using var handle = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.White, IsAntialias = true };
        using var handleBorder = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x38, 0xBD, 0xF8), StrokeWidth = 2, IsAntialias = true };
        const float h = 6;
        foreach (var p in new[] {
            new SKPoint(rect.Value.Left, rect.Value.Top),
            new SKPoint(rect.Value.Right, rect.Value.Top),
            new SKPoint(rect.Value.Left, rect.Value.Bottom),
            new SKPoint(rect.Value.Right, rect.Value.Bottom),
        })
        {
            var hr = new SKRect(p.X - h, p.Y - h, p.X + h, p.Y + h);
            canvas.DrawRect(hr, handle);
            canvas.DrawRect(hr, handleBorder);
        }
    }

    private SKRect? GetSelectionBounds(SKSize size)
    {
        var w = size.Width; var h = size.Height; var scale = w / 512f;

        switch (_selKind)
        {
            case SelKind.Background:
                {
                    var pad = _recipe.Padding * scale;
                    return new SKRect(pad, pad, w - pad, h - pad);
                }
            case SelKind.Text:
                {
                    if (string.IsNullOrEmpty(_recipe.Text)) return null;
                    var fs = _recipe.FontSize * scale;
                    var cy = h / 2f + _recipe.TextOffsetY * scale;
                    var tw = MathF.Max(fs * 0.6f, EstimateTextWidth(_recipe.Text, fs));
                    return new SKRect(w / 2 - tw / 2 - 8, cy - fs * 0.6f - 8, w / 2 + tw / 2 + 8, cy + fs * 0.4f + 8);
                }
            case SelKind.Subtitle:
                {
                    if (string.IsNullOrEmpty(_recipe.Subtitle)) return null;
                    var fs = _recipe.SubtitleSize * scale;
                    var cy = h / 2f + _recipe.TextOffsetY * scale + _recipe.FontSize * scale * 0.7f + fs;
                    var tw = MathF.Max(fs * 0.6f, EstimateTextWidth(_recipe.Subtitle, fs));
                    return new SKRect(w / 2 - tw / 2 - 8, cy - fs * 0.7f - 6, w / 2 + tw / 2 + 8, cy + fs * 0.4f + 6);
                }
            case SelKind.Icon:
                {
                    if (string.IsNullOrEmpty(_recipe.Icon)) return null;
                    var s = _recipe.IconSize * scale;
                    var (cx, cy) = GetIconCenter(size);
                    return new SKRect(cx - s / 2 - 6, cy - s / 2 - 6, cx + s / 2 + 6, cy + s / 2 + 6);
                }
            case SelKind.Layer:
                {
                    if (_selLayer < 0 || _selLayer >= _recipe.Layers.Count) return null;
                    var l = _recipe.Layers[_selLayer];
                    var lw = MathF.Max(2f, l.Width * w);
                    var lh = MathF.Max(2f, l.Height * h);
                    var cx = l.CenterX * w;
                    var cy = l.CenterY * h;
                    return new SKRect(cx - lw / 2, cy - lh / 2, cx + lw / 2, cy + lh / 2);
                }
            case SelKind.Image:
                return GetImageBounds(size);
        }
        return null;
    }

    private SKRect? GetImageBounds(SKSize size)
    {
        if (string.IsNullOrEmpty(_recipe.ImageData)) return null;
        var w = size.Width; var h = size.Height; var scale = w / 512f;
        var pos = (_recipe.ImagePosition ?? "center").ToLowerInvariant();
        if (pos == "fill" || pos == "behind")
        {
            var pad = _recipe.Padding * scale;
            return new SKRect(pad, pad, w - pad, h - pad);
        }
        var s = MathF.Max(8f, _recipe.ImageSize * scale);
        float cx = w / 2f + _recipe.ImageOffsetX * scale;
        float cy = h / 2f + _recipe.ImageOffsetY * scale;
        if (pos == "above") cy = h * 0.30f + _recipe.ImageOffsetY * scale;
        else if (pos == "below") cy = h * 0.72f + _recipe.ImageOffsetY * scale;
        return new SKRect(cx - s / 2, cy - s / 2, cx + s / 2, cy + s / 2);
    }

    private static float EstimateTextWidth(string text, float fontSize) => text.Length * fontSize * 0.55f;

    private (float cx, float cy) GetIconCenter(SKSize size)
    {
        var w = size.Width; var h = size.Height; var scale = w / 512f;
        var fs = _recipe.FontSize * scale;
        var iconSize = _recipe.IconSize * scale;
        var pos = (_recipe.IconPosition ?? "above").ToLowerInvariant();
        var cy = h / 2f + _recipe.TextOffsetY * scale;
        return pos switch
        {
            "above"  => (w / 2f, cy - fs * 0.7f - iconSize / 2f - 8),
            "below"  => (w / 2f, cy + fs * 0.5f + iconSize / 2f + 8),
            "left"   => (w / 2f - EstimateTextWidth(_recipe.Text, fs) / 2 - iconSize / 2 - 16, cy),
            "right"  => (w / 2f + EstimateTextWidth(_recipe.Text, fs) / 2 + iconSize / 2 + 16, cy),
            "custom" => (w / 2f + _recipe.IconOffsetX * scale, h / 2f + _recipe.IconOffsetY * scale),
            _        => (w / 2f, cy),
        };
    }

    // =========================================================================
    // TOUCH / SELECTION
    // =========================================================================
    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        e.Handled = true;
        var p = e.Location;

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                HitTestAndSelect(p);

                // Right-click → show context menu for the selection.
                if (e.MouseButton == SKMouseButton.Right)
                {
                    Preview.InvalidateSurface();
                    _ = ShowSelectionContextMenuAsync();
                    break;
                }

                if (_selKind != SelKind.None)
                {
                    _dragging = true;
                    _dragStartCanvas = p;
                    if (_selKind == SelKind.Layer && _selLayer >= 0 && _selLayer < _recipe.Layers.Count)
                    {
                        _dragStartCx = _recipe.Layers[_selLayer].CenterX;
                        _dragStartCy = _recipe.Layers[_selLayer].CenterY;
                    }
                    else if (_selKind == SelKind.Text || _selKind == SelKind.Subtitle)
                    {
                        _dragStartTextOffsetY = _recipe.TextOffsetY;
                    }
                    else if (_selKind == SelKind.Image)
                    {
                        _dragStartImgX = _recipe.ImageOffsetX;
                        _dragStartImgY = _recipe.ImageOffsetY;
                    }
                    else if (_selKind == SelKind.Icon)
                    {
                        // Switch to custom positioning so the drag actually persists.
                        if (!string.Equals(_recipe.IconPosition, "custom", StringComparison.OrdinalIgnoreCase))
                        {
                            // Seed the custom offset to wherever the icon currently sits.
                            var (icx, icy) = GetIconCenter(_lastSize);
                            var iconScale = _lastSize.Width / 512f;
                            _recipe.IconOffsetX = (icx - _lastSize.Width / 2f) / iconScale;
                            _recipe.IconOffsetY = (icy - _lastSize.Height / 2f) / iconScale;
                            _recipe.IconPosition = "custom";
                            _suppress = true;
                            try { IconPosPicker.SelectedItem = "custom"; }
                            finally { _suppress = false; }
                        }
                        _dragStartIconX = _recipe.IconOffsetX;
                        _dragStartIconY = _recipe.IconOffsetY;
                    }
                }
                Preview.InvalidateSurface();
                break;

            case SKTouchAction.Moved:
                if (_dragging)
                {
                    var dx = p.X - _dragStartCanvas.X;
                    var dy = p.Y - _dragStartCanvas.Y;
                    if (_selKind == SelKind.Layer && _selLayer >= 0 && _selLayer < _recipe.Layers.Count)
                    {
                        var l = _recipe.Layers[_selLayer];
                        l.CenterX = Clamp01(_dragStartCx + dx / _lastSize.Width);
                        l.CenterY = Clamp01(_dragStartCy + dy / _lastSize.Height);
                    }
                    else if (_selKind == SelKind.Text || _selKind == SelKind.Subtitle)
                    {
                        var scale = _lastSize.Width / 512f;
                        _recipe.TextOffsetY = _dragStartTextOffsetY + dy / scale;
                    }
                    else if (_selKind == SelKind.Image)
                    {
                        var scale = _lastSize.Width / 512f;
                        _recipe.ImageOffsetX = _dragStartImgX + dx / scale;
                        _recipe.ImageOffsetY = _dragStartImgY + dy / scale;
                        if (!_suppress)
                        {
                            _suppress = true;
                            try { ImageOffsetSlider.Value = _recipe.ImageOffsetY; ImageOffsetLabel.Text = $"Y offset {(int)_recipe.ImageOffsetY}"; }
                            finally { _suppress = false; }
                        }
                    }
                    else if (_selKind == SelKind.Icon && string.Equals(_recipe.IconPosition, "custom", StringComparison.OrdinalIgnoreCase))
                    {
                        var scale = _lastSize.Width / 512f;
                        _recipe.IconOffsetX = _dragStartIconX + dx / scale;
                        _recipe.IconOffsetY = _dragStartIconY + dy / scale;
                    }
                    Preview.InvalidateSurface();
                }
                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                _dragging = false;
                break;
        }
    }

    private static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

    private void HitTestAndSelect(SKPoint p)
    {
        // Top-down: layers above text first, then image (if drawn on top), then text/subtitle/icon,
        // then layers below text, then image (if drawn behind), then background.
        var imgPos = (_recipe.ImagePosition ?? "center").ToLowerInvariant();
        var imageOnTop = !string.IsNullOrEmpty(_recipe.ImageData) && imgPos != "behind" && imgPos != "fill";

        for (int i = _recipe.Layers.Count - 1; i >= 0; i--)
        {
            var l = _recipe.Layers[i];
            if (!l.AboveText) continue;
            if (LayerBounds(l).Contains(p.X, p.Y)) { Select(SelKind.Layer, i); return; }
        }

        if (imageOnTop && TryRect(SelKind.Image) is { } imgR && imgR.Contains(p.X, p.Y)) { Select(SelKind.Image); return; }

        if (TryRect(SelKind.Text) is { } tr && tr.Contains(p.X, p.Y)) { Select(SelKind.Text); return; }
        if (TryRect(SelKind.Subtitle) is { } sr && sr.Contains(p.X, p.Y)) { Select(SelKind.Subtitle); return; }
        if (TryRect(SelKind.Icon) is { } ir && ir.Contains(p.X, p.Y)) { Select(SelKind.Icon); return; }

        for (int i = _recipe.Layers.Count - 1; i >= 0; i--)
        {
            var l = _recipe.Layers[i];
            if (l.AboveText) continue;
            if (LayerBounds(l).Contains(p.X, p.Y)) { Select(SelKind.Layer, i); return; }
        }

        if (!imageOnTop && !string.IsNullOrEmpty(_recipe.ImageData)
            && TryRect(SelKind.Image) is { } imgR2 && imgR2.Contains(p.X, p.Y)) { Select(SelKind.Image); return; }

        if (TryRect(SelKind.Background) is { } br && br.Contains(p.X, p.Y)) { Select(SelKind.Background); return; }

        Select(SelKind.None);
    }

    private SKRect LayerBounds(LogoShapeLayer l)
    {
        var w = _lastSize.Width; var h = _lastSize.Height;
        var lw = MathF.Max(2f, l.Width * w);
        var lh = MathF.Max(2f, l.Height * h);
        var cx = l.CenterX * w; var cy = l.CenterY * h;
        return new SKRect(cx - lw / 2, cy - lh / 2, cx + lw / 2, cy + lh / 2);
    }

    private SKRect? TryRect(SelKind kind)
    {
        var prev = _selKind;
        _selKind = kind;
        var r = GetSelectionBounds(_lastSize);
        _selKind = prev;
        return r;
    }

    private void Select(SelKind kind, int layerIndex = -1)
    {
        _selKind = kind;
        _selLayer = layerIndex;
        UpdateSelectionUI();
    }

    private void UpdateSelectionUI()
    {
        // Show the Layer tab when a layer is selected or any layer exists.
        TabLayerBtn.IsVisible = _selKind == SelKind.Layer || _recipe.Layers.Count > 0;

        // Auto-switch ribbon to the matching tab when an element is selected.
        var target = _selKind switch
        {
            SelKind.Background => "background",
            SelKind.Text       => "text",
            SelKind.Subtitle   => "subtitle",
            SelKind.Icon       => "icon",
            SelKind.Layer      => "layer",
            SelKind.Image      => "image",
            _                  => _activeTab
        };
        if (target != _activeTab) SetActiveTab(target);

        SelectionLabel.Text = _selKind switch
        {
            SelKind.Background => "Selected: Background — drag isn't supported, use the ribbon",
            SelKind.Text => "Selected: Main text — drag to move vertically",
            SelKind.Subtitle => "Selected: Subtitle",
            SelKind.Icon => "Selected: Icon — drag to place freely (switches Position to Custom)",
            SelKind.Layer => $"Selected: Shape layer #{_selLayer + 1} — drag to move",
            SelKind.Image => "Selected: Image — drag to move (Delete/right-click to remove)",
            _ => "No selection — tap an element on the canvas (right-click for menu)",
        };

        // Floating action bar above the canvas
        SelectionActionBar.IsVisible = _selKind != SelKind.None;
        ActionBarLabel.Text = _selKind switch
        {
            SelKind.Background => "Background",
            SelKind.Text => "Text",
            SelKind.Subtitle => "Subtitle",
            SelKind.Icon => "Icon",
            SelKind.Layer => $"Layer #{_selLayer + 1}",
            SelKind.Image => "Image",
            _ => ""
        };

        if (_selKind == SelKind.Layer && _selLayer >= 0 && _selLayer < _recipe.Layers.Count)
            SyncLayerPanel(_recipe.Layers[_selLayer]);

        Preview.InvalidateSurface();
    }

    // =========================================================================
    // CONTEXT MENU / WORK AREA ACTIONS
    // =========================================================================
    private async Task ShowSelectionContextMenuAsync()
    {
        if (_selKind == SelKind.None)
        {
            var pick = await DisplayActionSheet("Canvas", "Cancel", null, "🎲 Random design", "↺ Reset");
            if (pick == "🎲 Random design") OnRandomClicked(this, EventArgs.Empty);
            else if (pick == "↺ Reset") OnResetClicked(this, EventArgs.Empty);
            return;
        }

        var actions = new List<string> { "✏ Edit in ribbon" };
        if (_selKind == SelKind.Layer)
        {
            actions.AddRange(new[] { "⧉ Duplicate", "⬆ Bring to front", "⬇ Send to back", "🔄 Toggle above text" });
        }
        if (_selKind == SelKind.Icon)
        {
            actions.Add("✕ Clear icon");
        }
        if (_selKind == SelKind.Image)
        {
            actions.Add("⇧ Recenter");
        }
        if (_selKind == SelKind.Icon && string.Equals(_recipe.IconPosition, "custom", StringComparison.OrdinalIgnoreCase))
        {
            actions.Add("⇧ Recenter");
        }
        if (_selKind == SelKind.Background)
        {
            actions.Add("✕ Remove shape (blank canvas)");
        }
        if (_selKind == SelKind.Layer || _selKind == SelKind.Icon || _selKind == SelKind.Image || _selKind == SelKind.Background)
        {
            actions.Add("🗑 Delete");
        }
        if (_selKind == SelKind.Text || _selKind == SelKind.Subtitle)
        {
            actions.Add("⇧ Recenter");
        }

        var result = await DisplayActionSheet(ActionBarLabel.Text, "Cancel", null, actions.ToArray());
        switch (result)
        {
            case "✏ Edit in ribbon":
                SetActiveTab(_selKind switch
                {
                    SelKind.Background => "background",
                    SelKind.Text => "text",
                    SelKind.Subtitle => "subtitle",
                    SelKind.Icon => "icon",
                    SelKind.Layer => "layer",
                    SelKind.Image => "image",
                    _ => _activeTab
                });
                break;
            case "⧉ Duplicate":
                OnLayerDuplicateClicked(this, EventArgs.Empty); break;
            case "⬆ Bring to front":
                if (_selKind == SelKind.Layer && _selLayer >= 0 && _selLayer < _recipe.Layers.Count - 1)
                {
                    var l = _recipe.Layers[_selLayer];
                    _recipe.Layers.RemoveAt(_selLayer);
                    _recipe.Layers.Add(l);
                    Select(SelKind.Layer, _recipe.Layers.Count - 1);
                }
                break;
            case "⬇ Send to back":
                if (_selKind == SelKind.Layer && _selLayer > 0)
                {
                    var l = _recipe.Layers[_selLayer];
                    _recipe.Layers.RemoveAt(_selLayer);
                    _recipe.Layers.Insert(0, l);
                    Select(SelKind.Layer, 0);
                }
                break;
            case "🔄 Toggle above text":
                if (_selKind == SelKind.Layer && _selLayer >= 0 && _selLayer < _recipe.Layers.Count)
                {
                    _recipe.Layers[_selLayer].AboveText = !_recipe.Layers[_selLayer].AboveText;
                    UpdateSelectionUI();
                }
                break;
            case "✕ Clear icon":
                OnIconClearClicked(this, EventArgs.Empty); break;
            case "✕ Remove shape (blank canvas)":
                _recipe.BgShape = "none";
                _suppress = true;
                try
                {
                    var keys2 = ShapeCatalog.All.Select(s => s.Key).ToList();
                    BgShapePicker.SelectedIndex = Math.Max(0, keys2.IndexOf("none"));
                }
                finally { _suppress = false; }
                Select(SelKind.None);
                Preview.InvalidateSurface();
                break;
            case "🗑 Delete":
                OnDeleteSelectionClicked(this, EventArgs.Empty); break;
            case "⇧ Recenter":
                if (_selKind == SelKind.Image) { _recipe.ImageOffsetX = 0; _recipe.ImageOffsetY = 0; SyncRibbonFromRecipe(); }
                else if (_selKind == SelKind.Icon) { _recipe.IconOffsetX = 0; _recipe.IconOffsetY = 0; }
                else _recipe.TextOffsetY = 0;
                Preview.InvalidateSurface();
                break;
        }
    }

    private void OnSelectionMoreClicked(object sender, EventArgs e) => _ = ShowSelectionContextMenuAsync();

    private void OnDeleteSelectionClicked(object sender, EventArgs e)
    {
        switch (_selKind)
        {
            case SelKind.Layer:
                if (_selLayer >= 0 && _selLayer < _recipe.Layers.Count)
                {
                    _recipe.Layers.RemoveAt(_selLayer);
                    Select(SelKind.None);
                    Preview.InvalidateSurface();
                }
                break;
            case SelKind.Icon:
                _recipe.Icon = "";
                _suppress = true; try { IconEntry.Text = ""; } finally { _suppress = false; }
                Select(SelKind.None);
                Preview.InvalidateSurface();
                break;
            case SelKind.Text:
                _recipe.Text = "";
                _suppress = true; try { TextEntry.Text = ""; } finally { _suppress = false; }
                Select(SelKind.None);
                Preview.InvalidateSurface();
                break;
            case SelKind.Subtitle:
                _recipe.Subtitle = "";
                _suppress = true; try { SubtitleEntry.Text = ""; } finally { _suppress = false; }
                Select(SelKind.None);
                Preview.InvalidateSurface();
                break;
            case SelKind.Image:
                _recipe.ImageData = "";
                Select(SelKind.None);
                Preview.InvalidateSurface();
                break;
            case SelKind.Background:
                _recipe.BgShape = "none";
                _suppress = true;
                try
                {
                    var keys = ShapeCatalog.All.Select(s => s.Key).ToList();
                    BgShapePicker.SelectedIndex = Math.Max(0, keys.IndexOf("none"));
                }
                finally { _suppress = false; }
                Select(SelKind.None);
                Preview.InvalidateSurface();
                break;
        }
    }

    // =========================================================================
    // SYNC RIBBON FROM RECIPE
    // =========================================================================
    private void SyncRibbonFromRecipe()
    {
        _suppress = true;
        try
        {
            var keys = ShapeCatalog.All.Select(s => s.Key).ToList();
            BgShapePicker.SelectedIndex = Math.Max(0, keys.IndexOf(_recipe.BgShape));
            BgColor1Picker.HexColor = _recipe.BgColor1;
            BgColor2Picker.HexColor = _recipe.BgColor2;
            GradDirPicker.SelectedItem = _recipe.UseGradient ? _recipe.GradientDirection : "none";
            BorderSwitch.IsToggled = _recipe.ShowBorder;
            BorderColorPicker.HexColor = _recipe.BorderColor;
            PatternPicker.SelectedItem = _recipe.Pattern;
            ShapeShadowSwitch.IsToggled = _recipe.ShapeShadow;

            TextEntry.Text = _recipe.Text;
            var fonts = (FontPicker.ItemsSource as System.Collections.IEnumerable)?
                .Cast<object>().Select(o => o?.ToString() ?? "").ToList() ?? new List<string>();
            var fIdx = fonts.FindIndex(f => string.Equals(f, _recipe.FontFamily, StringComparison.OrdinalIgnoreCase));
            if (fIdx < 0 && fonts.Count > 0) fIdx = 0;
            FontPicker.SelectedIndex = fIdx;
            WeightPicker.SelectedItem = _recipe.FontWeight;
            FontSizeSlider.Value = _recipe.FontSize;
            FontSizeLabel.Text = $"Size {(int)_recipe.FontSize}";
            LetterSpacingSlider.Value = _recipe.LetterSpacing;
            LetterSpacingLabel.Text = $"Spacing {(int)_recipe.LetterSpacing}";
            TextColorPicker.HexColor = _recipe.TextColor;
            UppercaseSwitch.IsToggled = _recipe.TextUppercase;
            StrokeSwitch.IsToggled = _recipe.TextStroke;
            StrokeColorPicker.HexColor = _recipe.TextStrokeColor;
            ShadowSwitch.IsToggled = _recipe.TextShadow;

            SubtitleEntry.Text = _recipe.Subtitle;
            SubtitleSizeSlider.Value = _recipe.SubtitleSize;
            SubtitleSizeLabel.Text = $"Size {(int)_recipe.SubtitleSize}";
            SubtitleColorPicker.HexColor = _recipe.SubtitleColor;

            IconEntry.Text = _recipe.Icon;
            IconSizeSlider.Value = _recipe.IconSize;
            IconSizeLabel.Text = $"Size {(int)_recipe.IconSize}";
            IconRotSlider.Value = _recipe.IconRotation;
            IconRotLabel.Text = $"Rotation {(int)_recipe.IconRotation}°";
            IconColorPicker.HexColor = _recipe.IconColor;
            IconPosPicker.SelectedItem = _recipe.IconPosition;

            ImagePosPicker.SelectedItem = _recipe.ImagePosition;
            ImageSizeSlider.Value = _recipe.ImageSize;
            ImageSizeLabel.Text = $"Size {(int)_recipe.ImageSize}";
            ImageOpacitySlider.Value = _recipe.ImageOpacity;
            ImageOpacityLabel.Text = $"Opacity {(int)(_recipe.ImageOpacity * 100)}%";
            ImageRotSlider.Value = _recipe.ImageRotation;
            ImageRotLabel.Text = $"Rotation {(int)_recipe.ImageRotation}°";
            ImageOffsetSlider.Value = _recipe.ImageOffsetY;
            ImageOffsetLabel.Text = $"Y offset {(int)_recipe.ImageOffsetY}";
        }
        finally { _suppress = false; }
    }

    private void SyncLayerPanel(LogoShapeLayer l)
    {
        _suppress = true;
        try
        {
            var keys = ShapeCatalog.All.Select(s => s.Key).ToList();
            LayerShapePicker.SelectedIndex = Math.Max(0, keys.IndexOf(l.Shape));
            LayerWidthSlider.Value = l.Width;
            LayerWidthLabel.Text = $"Width {(int)(l.Width * 100)}%";
            LayerHeightSlider.Value = l.Height;
            LayerHeightLabel.Text = $"Height {(int)(l.Height * 100)}%";
            LayerRotSlider.Value = l.Rotation;
            LayerRotLabel.Text = $"Rotation {(int)l.Rotation}°";
            LayerOpacitySlider.Value = l.Opacity;
            LayerOpacityLabel.Text = $"Opacity {(int)(l.Opacity * 100)}%";
            LayerFillPicker.HexColor = l.FillColor;
            LayerGradSwitch.IsToggled = l.UseGradient;
            LayerFill2Picker.HexColor = l.FillColor2;
            LayerStrokeSwitch.IsToggled = l.Stroke;
            LayerStrokePicker.HexColor = l.StrokeColor;
            LayerAboveSwitch.IsToggled = l.AboveText;
        }
        finally { _suppress = false; }
    }

    // =========================================================================
    // ACTION RIBBON HANDLERS
    // =========================================================================
    private async void OnTemplatesClicked(object sender, EventArgs e)
    {
        var labels = LogoDesignRecipe.Templates.Select(t => $"{t.Emoji}  {t.Name}").ToArray();
        var pick = await DisplayActionSheet("Choose a template", "Cancel", null, labels);
        if (string.IsNullOrEmpty(pick) || pick == "Cancel") return;
        var idx = Array.IndexOf(labels, pick);
        if (idx < 0) return;
        _recipe = LogoDesignRecipe.Templates[idx].Build();
        Select(SelKind.None);
        SyncRibbonFromRecipe();
        Preview.InvalidateSurface();
    }

    private void OnRandomClicked(object sender, EventArgs e)
    {
        _recipe = LogoDesignRecipe.Random();
        Select(SelKind.None);
        SyncRibbonFromRecipe();
        Preview.InvalidateSurface();
    }

    private void OnResetClicked(object sender, EventArgs e)
    {
        _recipe = new LogoDesignRecipe();
        Select(SelKind.None);
        SyncRibbonFromRecipe();
        Preview.InvalidateSurface();
    }

    private async void OnAddLayerClicked(object sender, EventArgs e)
    {
        var shapes = ShapeCatalog.All.Where(s => s.Key != "none").ToList();
        var labels = shapes.Select(s => $"{s.Emoji}  {s.DisplayName}").ToArray();
        var pick = await DisplayActionSheet("Add shape layer", "Cancel", null, labels);
        if (string.IsNullOrEmpty(pick) || pick == "Cancel") return;
        var idx = Array.IndexOf(labels, pick);
        if (idx < 0) return;
        var layer = new LogoShapeLayer
        {
            Shape = shapes[idx].Key,
            CenterX = 0.5f, CenterY = 0.5f,
            Width = 0.4f, Height = 0.4f,
            FillColor = "#FFFFFF", Opacity = 0.85f,
        };
        _recipe.Layers.Add(layer);
        Select(SelKind.Layer, _recipe.Layers.Count - 1);
        Preview.InvalidateSurface();
    }

    private void OnDeleteLayerClicked(object sender, EventArgs e)
    {
        if (_selKind != SelKind.Layer || _selLayer < 0 || _selLayer >= _recipe.Layers.Count) return;
        _recipe.Layers.RemoveAt(_selLayer);
        Select(SelKind.None);
        Preview.InvalidateSurface();
    }

    private void OnLayerUpClicked(object sender, EventArgs e)
    {
        if (_selKind != SelKind.Layer || _selLayer <= 0) return;
        (_recipe.Layers[_selLayer - 1], _recipe.Layers[_selLayer]) = (_recipe.Layers[_selLayer], _recipe.Layers[_selLayer - 1]);
        _selLayer--;
        UpdateSelectionUI();
    }

    private void OnLayerDownClicked(object sender, EventArgs e)
    {
        if (_selKind != SelKind.Layer || _selLayer < 0 || _selLayer >= _recipe.Layers.Count - 1) return;
        (_recipe.Layers[_selLayer + 1], _recipe.Layers[_selLayer]) = (_recipe.Layers[_selLayer], _recipe.Layers[_selLayer + 1]);
        _selLayer++;
        UpdateSelectionUI();
    }

    private void OnLayerDuplicateClicked(object sender, EventArgs e)
    {
        if (_selKind != SelKind.Layer || _selLayer < 0 || _selLayer >= _recipe.Layers.Count) return;
        var copy = _recipe.Layers[_selLayer].Clone();
        copy.CenterX = Clamp01(copy.CenterX + 0.05f);
        copy.CenterY = Clamp01(copy.CenterY + 0.05f);
        _recipe.Layers.Insert(_selLayer + 1, copy);
        Select(SelKind.Layer, _selLayer + 1);
    }

    // =========================================================================
    // BACKGROUND HANDLERS
    // =========================================================================
    private void OnBgShapeChanged(object sender, EventArgs e)
    {
        if (_suppress) return;
        if (BgShapePicker.SelectedItem is string s) { _recipe.BgShape = s; Preview.InvalidateSurface(); }
    }
    private void OnBgColor1Changed(object? sender, EventArgs e) { if (_suppress) return; _recipe.BgColor1 = BgColor1Picker.HexColor; Preview.InvalidateSurface(); }
    private void OnBgColor2Changed(object? sender, EventArgs e) { if (_suppress) return; _recipe.BgColor2 = BgColor2Picker.HexColor; Preview.InvalidateSurface(); }
    private void OnGradDirChanged(object sender, EventArgs e)
    {
        if (_suppress) return;
        if (GradDirPicker.SelectedItem is string s)
        {
            if (s == "none") _recipe.UseGradient = false;
            else { _recipe.UseGradient = true; _recipe.GradientDirection = s; }
            Preview.InvalidateSurface();
        }
    }
    private void OnBorderToggled(object sender, ToggledEventArgs e) { if (_suppress) return; _recipe.ShowBorder = e.Value; Preview.InvalidateSurface(); }
    private void OnBorderColorChanged(object? sender, EventArgs e) { if (_suppress) return; _recipe.BorderColor = BorderColorPicker.HexColor; Preview.InvalidateSurface(); }
    private void OnPatternChanged(object sender, EventArgs e)
    {
        if (_suppress) return;
        if (PatternPicker.SelectedItem is string s) { _recipe.Pattern = s; Preview.InvalidateSurface(); }
    }
    private void OnShapeShadowToggled(object sender, ToggledEventArgs e) { if (_suppress) return; _recipe.ShapeShadow = e.Value; Preview.InvalidateSurface(); }

    // =========================================================================
    // TEXT HANDLERS
    // =========================================================================
    private void OnTextChanged(object sender, TextChangedEventArgs e) { if (_suppress) return; _recipe.Text = e.NewTextValue ?? ""; Preview.InvalidateSurface(); }
    private void OnFontChanged(object sender, EventArgs e)
    {
        if (_suppress) return;
        if (FontPicker.SelectedItem is string s) { _recipe.FontFamily = s; Preview.InvalidateSurface(); }
    }
    private void OnWeightChanged(object sender, EventArgs e)
    {
        if (_suppress) return;
        if (WeightPicker.SelectedItem is string s) { _recipe.FontWeight = s; Preview.InvalidateSurface(); }
    }
    private void OnFontSizeChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return;
        _recipe.FontSize = (float)e.NewValue; FontSizeLabel.Text = $"Size {(int)e.NewValue}"; Preview.InvalidateSurface();
    }
    private void OnLetterSpacingChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return;
        _recipe.LetterSpacing = (float)e.NewValue; LetterSpacingLabel.Text = $"Spacing {(int)e.NewValue}"; Preview.InvalidateSurface();
    }
    private void OnTextColorChanged(object? sender, EventArgs e) { if (_suppress) return; _recipe.TextColor = TextColorPicker.HexColor; Preview.InvalidateSurface(); }
    private void OnUppercaseToggled(object sender, ToggledEventArgs e) { if (_suppress) return; _recipe.TextUppercase = e.Value; Preview.InvalidateSurface(); }
    private void OnStrokeToggled(object sender, ToggledEventArgs e) { if (_suppress) return; _recipe.TextStroke = e.Value; Preview.InvalidateSurface(); }
    private void OnStrokeColorChanged(object? sender, EventArgs e) { if (_suppress) return; _recipe.TextStrokeColor = StrokeColorPicker.HexColor; Preview.InvalidateSurface(); }
    private void OnShadowToggled(object sender, ToggledEventArgs e) { if (_suppress) return; _recipe.TextShadow = e.Value; Preview.InvalidateSurface(); }

    // =========================================================================
    // SUBTITLE HANDLERS
    // =========================================================================
    private void OnSubtitleChanged(object sender, TextChangedEventArgs e) { if (_suppress) return; _recipe.Subtitle = e.NewTextValue ?? ""; Preview.InvalidateSurface(); }
    private void OnSubtitleSizeChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return;
        _recipe.SubtitleSize = (float)e.NewValue; SubtitleSizeLabel.Text = $"Size {(int)e.NewValue}"; Preview.InvalidateSurface();
    }
    private void OnSubtitleColorChanged(object? sender, EventArgs e) { if (_suppress) return; _recipe.SubtitleColor = SubtitleColorPicker.HexColor; Preview.InvalidateSurface(); }

    // =========================================================================
    // ICON HANDLERS
    // =========================================================================
    private void OnIconChanged(object sender, TextChangedEventArgs e) { if (_suppress) return; _recipe.Icon = e.NewTextValue ?? ""; Preview.InvalidateSurface(); }
    private void OnIconSizeChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return;
        _recipe.IconSize = (float)e.NewValue; IconSizeLabel.Text = $"Size {(int)e.NewValue}"; Preview.InvalidateSurface();
    }
    private void OnIconRotChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return;
        _recipe.IconRotation = (float)e.NewValue; IconRotLabel.Text = $"Rotation {(int)e.NewValue}°"; Preview.InvalidateSurface();
    }
    private void OnIconColorChanged(object? sender, EventArgs e) { if (_suppress) return; _recipe.IconColor = IconColorPicker.HexColor; Preview.InvalidateSurface(); }
    private void OnIconPosChanged(object sender, EventArgs e)
    {
        if (_suppress) return;
        if (IconPosPicker.SelectedItem is string s)
        {
            // When switching to custom for the first time, seed the offset to the icon's current
            // on-canvas centre so it doesn't visually jump to (0,0).
            if (string.Equals(s, "custom", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(_recipe.IconPosition, "custom", StringComparison.OrdinalIgnoreCase))
            {
                var (icx, icy) = GetIconCenter(_lastSize);
                var scale = _lastSize.Width / 512f;
                if (scale > 0)
                {
                    _recipe.IconOffsetX = (icx - _lastSize.Width / 2f) / scale;
                    _recipe.IconOffsetY = (icy - _lastSize.Height / 2f) / scale;
                }
            }
            _recipe.IconPosition = s;
            Preview.InvalidateSurface();
        }
    }

    // =========================================================================
    // LAYER HANDLERS
    // =========================================================================
    private LogoShapeLayer? CurrentLayer =>
        (_selKind == SelKind.Layer && _selLayer >= 0 && _selLayer < _recipe.Layers.Count) ? _recipe.Layers[_selLayer] : null;

    private void OnLayerShapeChanged(object sender, EventArgs e)
    {
        if (_suppress) return;
        var l = CurrentLayer; if (l == null) return;
        if (LayerShapePicker.SelectedItem is string s) { l.Shape = s; Preview.InvalidateSurface(); }
    }
    private void OnLayerWidthChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return; var l = CurrentLayer; if (l == null) return;
        l.Width = (float)e.NewValue; LayerWidthLabel.Text = $"Width {(int)(l.Width * 100)}%"; Preview.InvalidateSurface();
    }
    private void OnLayerHeightChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return; var l = CurrentLayer; if (l == null) return;
        l.Height = (float)e.NewValue; LayerHeightLabel.Text = $"Height {(int)(l.Height * 100)}%"; Preview.InvalidateSurface();
    }
    private void OnLayerRotChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return; var l = CurrentLayer; if (l == null) return;
        l.Rotation = (float)e.NewValue; LayerRotLabel.Text = $"Rotation {(int)l.Rotation}°"; Preview.InvalidateSurface();
    }
    private void OnLayerOpacityChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return; var l = CurrentLayer; if (l == null) return;
        l.Opacity = (float)e.NewValue; LayerOpacityLabel.Text = $"Opacity {(int)(l.Opacity * 100)}%"; Preview.InvalidateSurface();
    }
    private void OnLayerFillChanged(object? sender, EventArgs e)
    {
        if (_suppress) return; var l = CurrentLayer; if (l == null) return;
        l.FillColor = LayerFillPicker.HexColor; Preview.InvalidateSurface();
    }
    private void OnLayerGradToggled(object sender, ToggledEventArgs e)
    {
        if (_suppress) return; var l = CurrentLayer; if (l == null) return;
        l.UseGradient = e.Value; Preview.InvalidateSurface();
    }
    private void OnLayerFill2Changed(object? sender, EventArgs e)
    {
        if (_suppress) return; var l = CurrentLayer; if (l == null) return;
        l.FillColor2 = LayerFill2Picker.HexColor; Preview.InvalidateSurface();
    }
    private void OnLayerStrokeToggled(object sender, ToggledEventArgs e)
    {
        if (_suppress) return; var l = CurrentLayer; if (l == null) return;
        l.Stroke = e.Value; Preview.InvalidateSurface();
    }
    private void OnLayerStrokeColorChanged(object? sender, EventArgs e)
    {
        if (_suppress) return; var l = CurrentLayer; if (l == null) return;
        l.StrokeColor = LayerStrokePicker.HexColor; Preview.InvalidateSurface();
    }
    private void OnLayerAboveToggled(object sender, ToggledEventArgs e)
    {
        if (_suppress) return; var l = CurrentLayer; if (l == null) return;
        l.AboveText = e.Value; Preview.InvalidateSurface();
    }

    // =========================================================================
    // SAVE / CANCEL
    // =========================================================================
    private async void OnCancelClicked(object sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var name = (NameEntry.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlert("Name Required", "Please enter a name for this logo.", "OK");
            return;
        }

        try
        {
            var png = LogoRenderer.RenderPng(_recipe, 512);
            var json = _recipe.ToJson();

            if (!string.IsNullOrEmpty(_editingId))
                League.WebsiteSettings.UpdateDesignedLogoCatalogItem(_editingId, name, png, json);
            else
                League.WebsiteSettings.AddDesignedLogoCatalogItem(name, png, json);

            DataStore.Save();
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save Failed", ex.Message, "OK");
        }
    }

    // =========================================================================
    // IMAGE TAB
    // =========================================================================
    private async void OnPickImageClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Pick an image",
            });
            if (result == null) return;
            using var s = await result.OpenReadAsync();
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms);
            SetImageBytes(ms.ToArray());
        }
        catch (Exception ex)
        {
            await DisplayAlert("Pick failed", ex.Message, "OK");
        }
    }

    private void SetImageBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return;
        _recipe.ImageData = Convert.ToBase64String(bytes);
        Preview.InvalidateSurface();
    }

    private void OnImageClearClicked(object sender, EventArgs e)
    {
        _recipe.ImageData = "";
        Preview.InvalidateSurface();
    }

    private void OnImagePosChanged(object sender, EventArgs e)
    {
        if (_suppress) return;
        if (ImagePosPicker.SelectedItem is string s) _recipe.ImagePosition = s;
        Preview.InvalidateSurface();
    }

    private void OnImageSizeChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return;
        _recipe.ImageSize = (float)e.NewValue;
        ImageSizeLabel.Text = $"Size {(int)_recipe.ImageSize}";
        Preview.InvalidateSurface();
    }

    private void OnImageOpacityChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return;
        _recipe.ImageOpacity = (float)e.NewValue;
        ImageOpacityLabel.Text = $"Opacity {(int)(_recipe.ImageOpacity * 100)}%";
        Preview.InvalidateSurface();
    }

    private void OnImageRotChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return;
        _recipe.ImageRotation = (float)e.NewValue;
        ImageRotLabel.Text = $"Rotation {(int)_recipe.ImageRotation}°";
        Preview.InvalidateSurface();
    }

    private void OnImageOffsetChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppress) return;
        _recipe.ImageOffsetY = (float)e.NewValue;
        ImageOffsetLabel.Text = $"Y offset {(int)_recipe.ImageOffsetY}";
        Preview.InvalidateSurface();
    }

    /// <summary>
    /// Gallery: shows previously-saved logos from the catalog plus a few generated swatches.
    /// Click a thumbnail to use that PNG as the embedded image.
    /// </summary>
    private void PopulateImageGallery()
    {
        ImageGalleryRow.Children.Clear();

        // 1) Saved catalog logos as ready-to-use images
        foreach (var item in League.WebsiteSettings.LogoCatalog)
        {
            var bytes = item.ImageData;
            if (bytes == null || bytes.Length == 0) continue;
            ImageGalleryRow.Children.Add(BuildGalleryThumb(item.Name, bytes));
        }

        // 2) A handful of generated swatches from templates so the gallery never feels empty
        foreach (var t in LogoDesignRecipe.Templates.Take(12))
        {
            try
            {
                var preview = LogoRenderer.RenderPng(t.Build(), 128);
                ImageGalleryRow.Children.Add(BuildGalleryThumb(t.Name, preview));
            }
            catch { /* skip bad templates */ }
        }
    }

    private View BuildGalleryThumb(string name, byte[] bytes)
    {
        var img = new Image
        {
            Source = ImageSource.FromStream(() => new MemoryStream(bytes)),
            WidthRequest = 60,
            HeightRequest = 60,
            Aspect = Aspect.AspectFit,
            BackgroundColor = Color.FromArgb("#FFFFFF"),
        };
        var border = new Border
        {
            Stroke = Color.FromArgb("#CBD5E1"),
            StrokeThickness = 1,
            Padding = 2,
            BackgroundColor = Color.FromArgb("#FFFFFF"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            Content = img,
        };
        ToolTipProperties.SetText(border, name);
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, __) =>
        {
            SetImageBytes(bytes);
        };
        border.GestureRecognizers.Add(tap);
        return border;
    }
}
