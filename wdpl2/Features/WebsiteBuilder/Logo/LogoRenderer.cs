using SkiaSharp;

namespace Wdpl2.Features.WebsiteBuilder.Logo;

/// <summary>
/// Renders a <see cref="LogoDesignRecipe"/> to an <see cref="SKCanvas"/> (live preview)
/// or to a PNG byte array (final export).
/// </summary>
public static class LogoRenderer
{
    /// <summary>Render the recipe to a square PNG of the given pixel size.</summary>
    public static byte[] RenderPng(LogoDesignRecipe recipe, int size = 512)
    {
        using var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            Draw(canvas, new SKSize(size, size), recipe);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Draw the recipe onto an existing canvas, scaled to <paramref name="size"/>.</summary>
    public static void Draw(SKCanvas canvas, SKSize size, LogoDesignRecipe recipe)
    {
        var w = size.Width;
        var h = size.Height;

        // Scale font sizes etc. against the reference 512px design canvas
        var scale = w / 512f;
        var pad = recipe.Padding * scale;
        var rect = new SKRect(pad, pad, w - pad, h - pad);

        // ---- Background shape -----------------------------------------
        if (!string.Equals(recipe.BgShape, "none", System.StringComparison.OrdinalIgnoreCase))
        {
            using var bgPaint = BuildBackgroundPaint(recipe, rect);
            DrawShape(canvas, recipe.BgShape, rect, bgPaint);

            if (recipe.ShowBorder && recipe.BorderWidth > 0)
            {
                using var borderPaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = recipe.BorderWidth * scale,
                    Color = ParseColor(recipe.BorderColor, SKColors.White),
                    IsAntialias = true
                };
                DrawShape(canvas, recipe.BgShape, rect, borderPaint);
            }
        }

        // ---- Icon (emoji/text) above main text ------------------------
        var hasIcon = !string.IsNullOrWhiteSpace(recipe.Icon);
        var hasText = !string.IsNullOrWhiteSpace(recipe.Text);

        var centerX = w / 2f;
        var centerY = h / 2f + recipe.TextOffsetY * scale;

        SKTypeface iconTypeface = SKTypeface.Default;
        SKTypeface textTypeface = ResolveTypeface(recipe.FontFamily, recipe.FontWeight);

        if (hasIcon && hasText)
        {
            // Stack: icon above, text below
            var iconSize = recipe.IconSize * scale;
            var textSize = recipe.FontSize * scale;
            var stackGap = 12 * scale;
            var totalH = iconSize + stackGap + textSize;
            var top = centerY - totalH / 2f;

            DrawCenteredText(canvas, recipe.Icon, iconTypeface, iconSize,
                ParseColor(recipe.IconColor, SKColors.White), centerX, top + iconSize * 0.85f);

            DrawCenteredText(canvas, ApplyCase(recipe.Text, recipe.TextUppercase), textTypeface, textSize,
                ParseColor(recipe.TextColor, SKColors.White), centerX, top + iconSize + stackGap + textSize * 0.85f);
        }
        else if (hasIcon)
        {
            var iconSize = recipe.IconSize * scale;
            DrawCenteredText(canvas, recipe.Icon, iconTypeface, iconSize,
                ParseColor(recipe.IconColor, SKColors.White), centerX, centerY + iconSize * 0.35f);
        }
        else if (hasText)
        {
            var textSize = recipe.FontSize * scale;
            DrawCenteredText(canvas, ApplyCase(recipe.Text, recipe.TextUppercase), textTypeface, textSize,
                ParseColor(recipe.TextColor, SKColors.White), centerX, centerY + textSize * 0.35f);
        }
    }

    // --- helpers ------------------------------------------------------

    private static string ApplyCase(string text, bool upper) => upper ? text.ToUpperInvariant() : text;

    private static SKPaint BuildBackgroundPaint(LogoDesignRecipe r, SKRect rect)
    {
        var c1 = ParseColor(r.BgColor1, SKColors.RoyalBlue);
        var c2 = ParseColor(r.BgColor2, SKColors.DarkBlue);

        var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

        if (!r.UseGradient)
        {
            paint.Color = c1;
            return paint;
        }

        SKShader shader = (r.GradientDirection?.ToLowerInvariant()) switch
        {
            "vertical"   => SKShader.CreateLinearGradient(new SKPoint(rect.MidX, rect.Top), new SKPoint(rect.MidX, rect.Bottom), new[] { c1, c2 }, null, SKShaderTileMode.Clamp),
            "horizontal" => SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.MidY), new SKPoint(rect.Right, rect.MidY), new[] { c1, c2 }, null, SKShaderTileMode.Clamp),
            "radial"     => SKShader.CreateRadialGradient(new SKPoint(rect.MidX, rect.MidY), System.Math.Max(rect.Width, rect.Height) / 2f, new[] { c1, c2 }, null, SKShaderTileMode.Clamp),
            _            => SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.Top), new SKPoint(rect.Right, rect.Bottom), new[] { c1, c2 }, null, SKShaderTileMode.Clamp),
        };
        paint.Shader = shader;
        return paint;
    }

    private static void DrawShape(SKCanvas canvas, string shape, SKRect rect, SKPaint paint)
    {
        switch ((shape ?? "circle").ToLowerInvariant())
        {
            case "square":
                canvas.DrawRect(rect, paint);
                break;
            case "rounded-square":
                var r = System.Math.Min(rect.Width, rect.Height) * 0.18f;
                canvas.DrawRoundRect(rect, r, r, paint);
                break;
            case "shield":
                using (var path = BuildShieldPath(rect))
                    canvas.DrawPath(path, paint);
                break;
            case "hexagon":
                using (var path = BuildHexagonPath(rect))
                    canvas.DrawPath(path, paint);
                break;
            case "circle":
            default:
                var cx = rect.MidX; var cy = rect.MidY;
                var radius = System.Math.Min(rect.Width, rect.Height) / 2f;
                canvas.DrawCircle(cx, cy, radius, paint);
                break;
        }
    }

    private static SKPath BuildShieldPath(SKRect rect)
    {
        var p = new SKPath();
        var w = rect.Width; var h = rect.Height;
        var top = new SKPoint(rect.MidX, rect.Top);
        p.MoveTo(top);
        p.LineTo(rect.Right, rect.Top + h * 0.15f);
        p.LineTo(rect.Right, rect.Top + h * 0.55f);
        p.QuadTo(rect.Right, rect.Bottom, rect.MidX, rect.Bottom);
        p.QuadTo(rect.Left, rect.Bottom, rect.Left, rect.Top + h * 0.55f);
        p.LineTo(rect.Left, rect.Top + h * 0.15f);
        p.Close();
        return p;
    }

    private static SKPath BuildHexagonPath(SKRect rect)
    {
        var p = new SKPath();
        var cx = rect.MidX; var cy = rect.MidY;
        var rad = System.Math.Min(rect.Width, rect.Height) / 2f;
        for (int i = 0; i < 6; i++)
        {
            var angle = System.Math.PI / 3 * i - System.Math.PI / 2;
            var x = cx + rad * (float)System.Math.Cos(angle);
            var y = cy + rad * (float)System.Math.Sin(angle);
            if (i == 0) p.MoveTo(x, y); else p.LineTo(x, y);
        }
        p.Close();
        return p;
    }

    private static void DrawCenteredText(SKCanvas canvas, string text, SKTypeface typeface, float size, SKColor color, float cx, float baselineY)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var font = new SKFont(typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        // Measure to center horizontally
        var width = font.MeasureText(text);
        canvas.DrawText(text, cx - width / 2f, baselineY, SKTextAlign.Left, font, paint);
    }

    private static SKTypeface ResolveTypeface(string family, string weight)
    {
        var style = (weight ?? "Bold").ToLowerInvariant() switch
        {
            "regular" => SKFontStyle.Normal,
            "black"   => new SKFontStyle(SKFontStyleWeight.Black, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            _         => SKFontStyle.Bold,
        };
        return SKTypeface.FromFamilyName(family, style) ?? SKTypeface.Default;
    }

    private static SKColor ParseColor(string hex, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        return SKColor.TryParse(hex, out var c) ? c : fallback;
    }
}
