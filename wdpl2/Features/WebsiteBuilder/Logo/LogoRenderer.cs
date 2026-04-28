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
        using var shapePath = !string.Equals(recipe.BgShape, "none", System.StringComparison.OrdinalIgnoreCase)
            ? ShapeCatalog.Build(recipe.BgShape, rect)
            : null;

        if (shapePath != null)
        {
            // Drop shadow under the shape
            if (recipe.ShapeShadow)
            {
                using var shadow = new SKPaint
                {
                    Color = ParseColor(recipe.ShapeShadowColor, SKColors.Black).WithAlpha(140),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true,
                    ImageFilter = SKImageFilter.CreateBlur(recipe.ShapeShadowBlur * scale, recipe.ShapeShadowBlur * scale)
                };
                canvas.Save();
                canvas.Translate(0, recipe.ShapeShadowOffsetY * scale);
                canvas.DrawPath(shapePath, shadow);
                canvas.Restore();
            }

            using var bgPaint = BuildBackgroundPaint(recipe, rect);
            canvas.DrawPath(shapePath, bgPaint);

            // Pattern overlay (clipped to shape)
            if (!string.Equals(recipe.Pattern, "none", System.StringComparison.OrdinalIgnoreCase))
            {
                canvas.Save();
                canvas.ClipPath(shapePath, antialias: true);
                DrawPattern(canvas, rect, recipe, scale);
                canvas.Restore();
            }

            if (recipe.ShowBorder && recipe.BorderWidth > 0)
            {
                using var borderPaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = recipe.BorderWidth * scale,
                    Color = ParseColor(recipe.BorderColor, SKColors.White),
                    IsAntialias = true
                };
                ApplyBorderStyle(borderPaint, recipe.BorderStyle, recipe.BorderWidth * scale);
                canvas.DrawPath(shapePath, borderPaint);

                if (string.Equals(recipe.BorderStyle, "double", System.StringComparison.OrdinalIgnoreCase))
                {
                    using var inner = ShapeCatalog.Build(recipe.BgShape, Inset(rect, recipe.BorderWidth * scale * 1.8f));
                    canvas.DrawPath(inner, borderPaint);
                }
            }
        }

        // ---- Stacked decorative layers (below text) -------------------
        DrawLayers(canvas, size, recipe, aboveText: false);

        // ---- Icon and text --------------------------------------------
        var hasIcon = !string.IsNullOrWhiteSpace(recipe.Icon);
        var hasText = !string.IsNullOrWhiteSpace(recipe.Text);
        var hasSub = !string.IsNullOrWhiteSpace(recipe.Subtitle);

        var centerX = w / 2f;
        var centerY = h / 2f + recipe.TextOffsetY * scale;

        SKTypeface iconTypeface = SKTypeface.Default;
        SKTypeface textTypeface = ResolveTypeface(recipe.FontFamily, recipe.FontWeight);

        var iconPos = (recipe.IconPosition ?? "above").ToLowerInvariant();

        // "behind" icon — draw faded large icon centered behind text
        if (hasIcon && iconPos == "behind")
        {
            DrawIcon(canvas, recipe.Icon, iconTypeface,
                recipe.IconSize * scale * 1.6f,
                ParseColor(recipe.IconColor, SKColors.White).WithAlpha(60),
                centerX, centerY + recipe.IconSize * scale * 0.6f, recipe.IconRotation);
        }

        if (hasText || hasSub)
        {
            var textSize = recipe.FontSize * scale;
            var subSize = recipe.SubtitleSize * scale;
            var iconSize = hasIcon && (iconPos == "above" || iconPos == "below") ? recipe.IconSize * scale : 0;
            var stackGap = 12 * scale;
            var subGap = 8 * scale;

            float textHeight = hasText ? textSize : 0;
            float subHeight = hasSub ? subSize : 0;
            float totalH = (iconSize > 0 ? iconSize + stackGap : 0) + textHeight + (hasSub ? subGap + subHeight : 0);
            var top = centerY - totalH / 2f;

            float y = top;

            if (hasIcon && iconPos == "above")
            {
                DrawIcon(canvas, recipe.Icon, iconTypeface, iconSize,
                    ParseColor(recipe.IconColor, SKColors.White),
                    centerX, y + iconSize * 0.85f, recipe.IconRotation);
                y += iconSize + stackGap;
            }

            if (hasText)
            {
                DrawTextWithEffects(canvas, ApplyCase(recipe.Text, recipe.TextUppercase), textTypeface, textSize,
                    centerX, y + textSize * 0.85f, recipe, scale, isSubtitle: false);
                y += textHeight;
            }

            if (hasSub)
            {
                y += subGap;
                DrawTextWithEffects(canvas, ApplyCase(recipe.Subtitle, recipe.TextUppercase), textTypeface, subSize,
                    centerX, y + subSize * 0.85f, recipe, scale, isSubtitle: true);
                y += subHeight;
            }

            if (hasIcon && iconPos == "below")
            {
                y += stackGap;
                DrawIcon(canvas, recipe.Icon, iconTypeface, iconSize,
                    ParseColor(recipe.IconColor, SKColors.White),
                    centerX, y + iconSize * 0.85f, recipe.IconRotation);
            }
        }
        else if (hasIcon && iconPos != "behind")
        {
            var iconSize = recipe.IconSize * scale;
            DrawIcon(canvas, recipe.Icon, iconTypeface, iconSize,
                ParseColor(recipe.IconColor, SKColors.White), centerX, centerY + iconSize * 0.35f, recipe.IconRotation);
        }

        // Side-positioned icons (left/right of text)
        if (hasIcon && (iconPos == "left" || iconPos == "right") && hasText)
        {
            var textSize = recipe.FontSize * scale;
            var iconSize = recipe.IconSize * scale;
            using var font = new SKFont(textTypeface, textSize);
            var tw = font.MeasureText(ApplyCase(recipe.Text, recipe.TextUppercase));
            float gap = 16 * scale;
            float ix = iconPos == "left" ? centerX - tw / 2f - iconSize / 2f - gap : centerX + tw / 2f + iconSize / 2f + gap;
            DrawIcon(canvas, recipe.Icon, iconTypeface, iconSize,
                ParseColor(recipe.IconColor, SKColors.White), ix, centerY + iconSize * 0.35f, recipe.IconRotation);
        }

        // ---- Stacked decorative layers (above text) -------------------
        DrawLayers(canvas, size, recipe, aboveText: true);
    }

    private static void DrawLayers(SKCanvas canvas, SKSize size, LogoDesignRecipe recipe, bool aboveText)
    {
        if (recipe.Layers == null || recipe.Layers.Count == 0) return;
        var w = size.Width; var h = size.Height;
        foreach (var layer in recipe.Layers)
        {
            if (layer == null) continue;
            if (layer.AboveText != aboveText) continue;
            if (string.IsNullOrEmpty(layer.Shape) || string.Equals(layer.Shape, "none", System.StringComparison.OrdinalIgnoreCase)) continue;

            var lw = System.Math.Max(2f, layer.Width * w);
            var lh = System.Math.Max(2f, layer.Height * h);
            var cx = layer.CenterX * w;
            var cy = layer.CenterY * h;
            var rect = new SKRect(cx - lw / 2f, cy - lh / 2f, cx + lw / 2f, cy + lh / 2f);

            using var path = ShapeCatalog.Build(layer.Shape, rect);
            var alpha = (byte)System.Math.Clamp(layer.Opacity * 255f, 0, 255);

            canvas.Save();
            if (System.Math.Abs(layer.Rotation) > 0.01f)
                canvas.RotateDegrees(layer.Rotation, cx, cy);

            using (var fill = BuildLayerFill(layer, rect, alpha))
                canvas.DrawPath(path, fill);

            if (layer.Stroke && layer.StrokeWidth > 0)
            {
                using var stroke = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = layer.StrokeWidth * (w / 512f),
                    Color = ParseColor(layer.StrokeColor, SKColors.Black).WithAlpha(alpha),
                    IsAntialias = true,
                    StrokeJoin = SKStrokeJoin.Round
                };
                canvas.DrawPath(path, stroke);
            }
            canvas.Restore();
        }
    }

    private static SKPaint BuildLayerFill(LogoShapeLayer layer, SKRect rect, byte alpha)
    {
        var c1 = ParseColor(layer.FillColor, SKColors.White).WithAlpha(alpha);
        var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        if (!layer.UseGradient)
        {
            paint.Color = c1;
            return paint;
        }
        var c2 = ParseColor(layer.FillColor2, SKColors.Black).WithAlpha(alpha);
        SKColor[] colors = [c1, c2];
        paint.Shader = (layer.GradientDirection ?? "diagonal").ToLowerInvariant() switch
        {
            "vertical"   => SKShader.CreateLinearGradient(new SKPoint(rect.MidX, rect.Top), new SKPoint(rect.MidX, rect.Bottom), colors, null, SKShaderTileMode.Clamp),
            "horizontal" => SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.MidY), new SKPoint(rect.Right, rect.MidY), colors, null, SKShaderTileMode.Clamp),
            "radial"     => SKShader.CreateRadialGradient(new SKPoint(rect.MidX, rect.MidY), System.Math.Max(rect.Width, rect.Height) / 2f, colors, null, SKShaderTileMode.Clamp),
            _            => SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.Top), new SKPoint(rect.Right, rect.Bottom), colors, null, SKShaderTileMode.Clamp),
        };
        return paint;
    }

    // --- helpers ------------------------------------------------------

    private static string ApplyCase(string text, bool upper) => upper ? text.ToUpperInvariant() : text;

    private static SKRect Inset(SKRect r, float by) => new(r.Left + by, r.Top + by, r.Right - by, r.Bottom - by);

    private static void ApplyBorderStyle(SKPaint paint, string style, float w)
    {
        switch ((style ?? "solid").ToLowerInvariant())
        {
            case "dashed": paint.PathEffect = SKPathEffect.CreateDash([w * 2.5f, w * 1.8f], 0); break;
            case "dotted": paint.PathEffect = SKPathEffect.CreateDash([w * 0.1f, w * 1.8f], 0); paint.StrokeCap = SKStrokeCap.Round; break;
            default: break;
        }
    }

    private static SKPaint BuildBackgroundPaint(LogoDesignRecipe r, SKRect rect)
    {
        var c1 = ParseColor(r.BgColor1, SKColors.RoyalBlue);
        var c2 = ParseColor(r.BgColor2, SKColors.DarkBlue);
        var c3 = ParseColor(r.BgColor3, SKColors.Purple);

        var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

        if (!r.UseGradient)
        {
            paint.Color = c1;
            return paint;
        }

        SKColor[] colors = r.UseThreeColorGradient ? [c1, c2, c3] : [c1, c2];

        SKShader shader = (r.GradientDirection?.ToLowerInvariant()) switch
        {
            "vertical"   => SKShader.CreateLinearGradient(new SKPoint(rect.MidX, rect.Top), new SKPoint(rect.MidX, rect.Bottom), colors, null, SKShaderTileMode.Clamp),
            "horizontal" => SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.MidY), new SKPoint(rect.Right, rect.MidY), colors, null, SKShaderTileMode.Clamp),
            "radial"     => SKShader.CreateRadialGradient(new SKPoint(rect.MidX, rect.MidY), System.Math.Max(rect.Width, rect.Height) / 2f, colors, null, SKShaderTileMode.Clamp),
            "angle"      => CreateAngleGradient(rect, r.GradientAngle, colors),
            _            => SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.Top), new SKPoint(rect.Right, rect.Bottom), colors, null, SKShaderTileMode.Clamp),
        };
        paint.Shader = shader;
        return paint;
    }

    private static SKShader CreateAngleGradient(SKRect rect, float angleDeg, SKColor[] colors)
    {
        var rad = angleDeg * System.Math.PI / 180.0;
        var dx = (float)System.Math.Cos(rad);
        var dy = (float)System.Math.Sin(rad);
        var cx = rect.MidX; var cy = rect.MidY;
        var len = (System.Math.Max(rect.Width, rect.Height)) / 2f;
        var p1 = new SKPoint(cx - dx * len, cy - dy * len);
        var p2 = new SKPoint(cx + dx * len, cy + dy * len);
        return SKShader.CreateLinearGradient(p1, p2, colors, null, SKShaderTileMode.Clamp);
    }

    private static SKPath BuildShapePath(string shape, SKRect rect)
    {
        var path = new SKPath();
        switch ((shape ?? "circle").ToLowerInvariant())
        {
            case "square":
                path.AddRect(rect); break;
            case "rounded-square":
                var r = System.Math.Min(rect.Width, rect.Height) * 0.18f;
                path.AddRoundRect(rect, r, r); break;
            case "shield":
                AppendShield(path, rect); break;
            case "hexagon":
                AppendPolygon(path, rect, 6, -90); break;
            case "octagon":
                AppendPolygon(path, rect, 8, -22.5f); break;
            case "triangle":
                AppendPolygon(path, rect, 3, -90); break;
            case "diamond":
                AppendPolygon(path, rect, 4, -90); break;
            case "star":
                AppendStar(path, rect, 5, 0.5f); break;
            case "banner":
                AppendBanner(path, rect); break;
            case "circle":
            default:
                var radius = System.Math.Min(rect.Width, rect.Height) / 2f;
                path.AddCircle(rect.MidX, rect.MidY, radius); break;
        }
        return path;
    }

    private static void DrawShape(SKCanvas canvas, string shape, SKRect rect, SKPaint paint)
    {
        using var path = BuildShapePath(shape, rect);
        canvas.DrawPath(path, paint);
    }

    private static void AppendShield(SKPath p, SKRect rect)
    {
        var h = rect.Height;
        p.MoveTo(rect.MidX, rect.Top);
        p.LineTo(rect.Right, rect.Top + h * 0.15f);
        p.LineTo(rect.Right, rect.Top + h * 0.55f);
        p.QuadTo(rect.Right, rect.Bottom, rect.MidX, rect.Bottom);
        p.QuadTo(rect.Left, rect.Bottom, rect.Left, rect.Top + h * 0.55f);
        p.LineTo(rect.Left, rect.Top + h * 0.15f);
        p.Close();
    }

    private static void AppendPolygon(SKPath p, SKRect rect, int sides, float startAngleDeg)
    {
        var cx = rect.MidX; var cy = rect.MidY;
        var rad = System.Math.Min(rect.Width, rect.Height) / 2f;
        var startRad = startAngleDeg * System.Math.PI / 180.0;
        for (int i = 0; i < sides; i++)
        {
            var angle = startRad + 2 * System.Math.PI * i / sides;
            var x = cx + rad * (float)System.Math.Cos(angle);
            var y = cy + rad * (float)System.Math.Sin(angle);
            if (i == 0) p.MoveTo(x, y); else p.LineTo(x, y);
        }
        p.Close();
    }

    private static void AppendStar(SKPath p, SKRect rect, int points, float innerRatio)
    {
        var cx = rect.MidX; var cy = rect.MidY;
        var outerR = System.Math.Min(rect.Width, rect.Height) / 2f;
        var innerR = outerR * innerRatio;
        var step = System.Math.PI / points;
        var angle = -System.Math.PI / 2;
        for (int i = 0; i < points * 2; i++)
        {
            var rad = (i % 2 == 0) ? outerR : innerR;
            var x = cx + rad * (float)System.Math.Cos(angle);
            var y = cy + rad * (float)System.Math.Sin(angle);
            if (i == 0) p.MoveTo(x, y); else p.LineTo(x, y);
            angle += step;
        }
        p.Close();
    }

    private static void AppendBanner(SKPath p, SKRect rect)
    {
        // Ribbon-style banner with notched ends
        var notch = rect.Width * 0.10f;
        var top = rect.Top + rect.Height * 0.28f;
        var bot = rect.Bottom - rect.Height * 0.28f;
        p.MoveTo(rect.Left, top);
        p.LineTo(rect.Right, top);
        p.LineTo(rect.Right - notch, (top + bot) / 2f);
        p.LineTo(rect.Right, bot);
        p.LineTo(rect.Left, bot);
        p.LineTo(rect.Left + notch, (top + bot) / 2f);
        p.Close();
    }

    private static void DrawPattern(SKCanvas canvas, SKRect rect, LogoDesignRecipe r, float scale)
    {
        var color = ParseColor(r.PatternColor, SKColors.White)
            .WithAlpha((byte)System.Math.Clamp(r.PatternOpacity * 255f, 0, 255));
        var step = System.Math.Max(4f, r.PatternScale * scale);
        using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var stroke = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = System.Math.Max(1f, step / 12f) };

        switch ((r.Pattern ?? "none").ToLowerInvariant())
        {
            case "stripes":
                for (var y = rect.Top; y < rect.Bottom; y += step * 2)
                    canvas.DrawRect(new SKRect(rect.Left, y, rect.Right, y + step), paint);
                break;
            case "dots":
                var dotR = step / 4f;
                for (var y = rect.Top + step / 2; y < rect.Bottom; y += step)
                    for (var x = rect.Left + step / 2; x < rect.Right; x += step)
                        canvas.DrawCircle(x, y, dotR, paint);
                break;
            case "grid":
                for (var x = rect.Left; x < rect.Right; x += step) canvas.DrawLine(x, rect.Top, x, rect.Bottom, stroke);
                for (var y = rect.Top; y < rect.Bottom; y += step) canvas.DrawLine(rect.Left, y, rect.Right, y, stroke);
                break;
            case "diagonal-lines":
                for (var d = -rect.Height; d < rect.Width; d += step)
                    canvas.DrawLine(rect.Left + d, rect.Top, rect.Left + d + rect.Height, rect.Bottom, stroke);
                break;
            case "chevron":
                for (var y = rect.Top; y < rect.Bottom; y += step)
                {
                    var path = new SKPath();
                    path.MoveTo(rect.Left, y);
                    path.LineTo(rect.MidX, y + step / 2);
                    path.LineTo(rect.Right, y);
                    canvas.DrawPath(path, stroke);
                    path.Dispose();
                }
                break;
        }
    }

    private static void DrawIcon(SKCanvas canvas, string text, SKTypeface typeface, float size, SKColor color, float cx, float baselineY, float rotation)
    {
        if (string.IsNullOrEmpty(text) || size <= 0) return;
        if (System.Math.Abs(rotation) > 0.01f)
        {
            canvas.Save();
            canvas.RotateDegrees(rotation, cx, baselineY - size * 0.35f);
        }

        // Emojis are not in standard typefaces — use SKTextBlob.CreateFromText with a typeface
        // resolved per-character so we get the system emoji font (Segoe UI Emoji, Apple Color Emoji, etc.)
        DrawEmojiCapableText(canvas, text, size, color, cx, baselineY);

        if (System.Math.Abs(rotation) > 0.01f) canvas.Restore();
    }

    private static SKTypeface ResolveTypefaceForCharacter(int codepoint)
    {
        return SKFontManager.Default.MatchCharacter(codepoint) ?? SKTypeface.Default;
    }

    private static void DrawEmojiCapableText(SKCanvas canvas, string text, float size, SKColor color, float cx, float baselineY)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Group consecutive runes that share the same matched typeface, then draw each run.
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        // First pass — measure total width
        float total = 0f;
        var runs = new System.Collections.Generic.List<(string Text, SKTypeface Typeface, float Width)>();
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        SKTypeface? currentTf = null;
        var sb = new System.Text.StringBuilder();
        while (enumerator.MoveNext())
        {
            var element = (string)enumerator.Current!;
            int cp = char.ConvertToUtf32(element, 0);
            var tf = ResolveTypefaceForCharacter(cp) ?? SKTypeface.Default;
            if (currentTf == null || ReferenceEquals(tf, currentTf) || tf.FamilyName == currentTf.FamilyName)
            {
                currentTf ??= tf;
                sb.Append(element);
            }
            else
            {
                var run = sb.ToString();
                using var f0 = new SKFont(currentTf!, size);
                runs.Add((run, currentTf!, f0.MeasureText(run)));
                total += runs[^1].Width;
                sb.Clear();
                sb.Append(element);
                currentTf = tf;
            }
        }
        if (sb.Length > 0 && currentTf != null)
        {
            var run = sb.ToString();
            using var f0 = new SKFont(currentTf, size);
            runs.Add((run, currentTf, f0.MeasureText(run)));
            total += runs[^1].Width;
        }

        float x = cx - total / 2f;
        foreach (var run in runs)
        {
            using var font = new SKFont(run.Typeface, size);
            canvas.DrawText(run.Text, x, baselineY, SKTextAlign.Left, font, paint);
            x += run.Width;
        }
    }

    private static void DrawTextWithEffects(SKCanvas canvas, string text, SKTypeface typeface, float size,
        float cx, float baselineY, LogoDesignRecipe r, float scale, bool isSubtitle)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var font = new SKFont(typeface, size);

        var color = ParseColor(isSubtitle ? r.SubtitleColor : r.TextColor, SKColors.White);
        var spacing = (isSubtitle ? r.SubtitleLetterSpacing : r.LetterSpacing) * scale;
        var rotation = isSubtitle ? 0 : r.TextRotation;

        if (System.Math.Abs(rotation) > 0.01f)
        {
            canvas.Save();
            canvas.RotateDegrees(rotation, cx, baselineY - size * 0.35f);
        }

        // Drop shadow (only for main text)
        if (!isSubtitle && r.TextShadow)
        {
            using var shadow = new SKPaint
            {
                Color = ParseColor(r.TextShadowColor, SKColors.Black).WithAlpha(180),
                IsAntialias = true,
                ImageFilter = SKImageFilter.CreateBlur(System.Math.Max(0.5f, r.TextShadowBlur * scale), System.Math.Max(0.5f, r.TextShadowBlur * scale))
            };
            DrawSpacedText(canvas, text, font, shadow, cx + r.TextShadowOffsetX * scale, baselineY + r.TextShadowOffsetY * scale, spacing);
        }

        // Stroke
        if (!isSubtitle && r.TextStroke && r.TextStrokeWidth > 0)
        {
            using var stroke = new SKPaint
            {
                Color = ParseColor(r.TextStrokeColor, SKColors.Black),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = r.TextStrokeWidth * scale,
                StrokeJoin = SKStrokeJoin.Round
            };
            DrawSpacedText(canvas, text, font, stroke, cx, baselineY, spacing);
        }

        using var fill = new SKPaint { Color = color, IsAntialias = true };
        DrawSpacedText(canvas, text, font, fill, cx, baselineY, spacing);

        if (System.Math.Abs(rotation) > 0.01f) canvas.Restore();
    }

    private static void DrawSpacedText(SKCanvas canvas, string text, SKFont font, SKPaint paint, float cx, float baselineY, float spacing)
    {
        if (System.Math.Abs(spacing) < 0.01f)
        {
            var w = font.MeasureText(text);
            canvas.DrawText(text, cx - w / 2f, baselineY, SKTextAlign.Left, font, paint);
            return;
        }
        // Manually lay out with spacing
        float total = 0;
        var widths = new float[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            widths[i] = font.MeasureText(text[i].ToString());
            total += widths[i];
        }
        total += spacing * (text.Length - 1);
        float x = cx - total / 2f;
        for (int i = 0; i < text.Length; i++)
        {
            canvas.DrawText(text[i].ToString(), x, baselineY, SKTextAlign.Left, font, paint);
            x += widths[i] + spacing;
        }
    }

    private static void DrawCenteredText(SKCanvas canvas, string text, SKTypeface typeface, float size, SKColor color, float cx, float baselineY)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var font = new SKFont(typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
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
