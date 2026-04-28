using SkiaSharp;

namespace Wdpl2.Features.WebsiteBuilder.Logo;

/// <summary>
/// A library of named shapes that can be used both as the main background and as
/// stacked overlay layers in <see cref="LogoDesignRecipe.Layers"/>.
/// </summary>
public static class ShapeCatalog
{
    public sealed record ShapeInfo(string Key, string DisplayName, string Emoji);

    public static IReadOnlyList<ShapeInfo> All { get; } = new ShapeInfo[]
    {
        new("circle",          "Circle",          "●"),
        new("rounded-square",  "Rounded square",  "◼"),
        new("square",          "Square",          "■"),
        new("rectangle",       "Rectangle (wide)","▬"),
        new("ellipse",         "Ellipse (wide)",  "⬭"),
        new("triangle",        "Triangle",        "▲"),
        new("triangle-down",   "Triangle ▼",      "▼"),
        new("diamond",         "Diamond",         "◆"),
        new("pentagon",        "Pentagon",        "⬠"),
        new("hexagon",         "Hexagon",         "⬢"),
        new("octagon",         "Octagon",         "⯃"),
        new("star",            "Star (5)",        "★"),
        new("star6",           "Star (6)",        "✶"),
        new("burst",           "Burst (12)",      "✺"),
        new("shield",          "Shield",          "🛡"),
        new("banner",          "Banner",          "🎀"),
        new("ribbon",          "Ribbon",          "🏷"),
        new("heart",           "Heart",           "♥"),
        new("plus",            "Plus / Cross",    "✚"),
        new("arrow-right",     "Arrow ►",         "►"),
        new("arrow-up",        "Arrow ▲",         "⬆"),
        new("lightning",       "Lightning bolt",  "⚡"),
        new("crown",           "Crown",           "👑"),
        new("gear",            "Gear / Cog",      "⚙"),
        new("speech",          "Speech bubble",   "💬"),
        new("teardrop",        "Teardrop",        "💧"),
        new("none",            "(none)",          "∅"),
    };

    /// <summary>Build a path for the given shape key, fitted into <paramref name="rect"/>.</summary>
    public static SKPath Build(string key, SKRect rect)
    {
        var path = new SKPath();
        var k = (key ?? "circle").ToLowerInvariant();
        switch (k)
        {
            case "none":
            case "circle":
                path.AddCircle(rect.MidX, rect.MidY, System.Math.Min(rect.Width, rect.Height) / 2f);
                break;
            case "rounded-square":
            {
                var r = System.Math.Min(rect.Width, rect.Height) * 0.18f;
                path.AddRoundRect(rect, r, r);
                break;
            }
            case "square":
            {
                var s = System.Math.Min(rect.Width, rect.Height);
                var sq = new SKRect(rect.MidX - s/2, rect.MidY - s/2, rect.MidX + s/2, rect.MidY + s/2);
                path.AddRect(sq);
                break;
            }
            case "rectangle":
                path.AddRect(rect);
                break;
            case "ellipse":
                path.AddOval(rect);
                break;
            case "triangle":      Polygon(path, rect, 3, -90); break;
            case "triangle-down": Polygon(path, rect, 3, 90);  break;
            case "diamond":       Polygon(path, rect, 4, -90); break;
            case "pentagon":      Polygon(path, rect, 5, -90); break;
            case "hexagon":       Polygon(path, rect, 6, -90); break;
            case "octagon":       Polygon(path, rect, 8, -22.5f); break;
            case "star":   Star(path, rect, 5,  0.5f);  break;
            case "star6":  Star(path, rect, 6,  0.55f); break;
            case "burst":  Star(path, rect, 12, 0.72f); break;
            case "shield": Shield(path, rect); break;
            case "banner": Banner(path, rect); break;
            case "ribbon": Ribbon(path, rect); break;
            case "heart":  Heart(path, rect);  break;
            case "plus":   Plus(path, rect);   break;
            case "arrow-right": ArrowRight(path, rect); break;
            case "arrow-up":    ArrowUp(path, rect);    break;
            case "lightning": Lightning(path, rect);    break;
            case "crown":     Crown(path, rect);        break;
            case "gear":      Gear(path, rect, 10);     break;
            case "speech":    Speech(path, rect);       break;
            case "teardrop":  Teardrop(path, rect);     break;
            default:
                path.AddCircle(rect.MidX, rect.MidY, System.Math.Min(rect.Width, rect.Height) / 2f);
                break;
        }
        return path;
    }

    private static void Polygon(SKPath p, SKRect rect, int sides, float startDeg)
    {
        var cx = rect.MidX; var cy = rect.MidY;
        var rad = System.Math.Min(rect.Width, rect.Height) / 2f;
        var s = startDeg * System.Math.PI / 180.0;
        for (int i = 0; i < sides; i++)
        {
            var a = s + 2 * System.Math.PI * i / sides;
            var x = cx + rad * (float)System.Math.Cos(a);
            var y = cy + rad * (float)System.Math.Sin(a);
            if (i == 0) p.MoveTo(x, y); else p.LineTo(x, y);
        }
        p.Close();
    }

    private static void Star(SKPath p, SKRect rect, int points, float innerRatio)
    {
        var cx = rect.MidX; var cy = rect.MidY;
        var outerR = System.Math.Min(rect.Width, rect.Height) / 2f;
        var innerR = outerR * innerRatio;
        var step = System.Math.PI / points;
        var a = -System.Math.PI / 2;
        for (int i = 0; i < points * 2; i++)
        {
            var r = (i % 2 == 0) ? outerR : innerR;
            var x = cx + r * (float)System.Math.Cos(a);
            var y = cy + r * (float)System.Math.Sin(a);
            if (i == 0) p.MoveTo(x, y); else p.LineTo(x, y);
            a += step;
        }
        p.Close();
    }

    private static void Shield(SKPath p, SKRect rect)
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

    private static void Banner(SKPath p, SKRect rect)
    {
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

    private static void Ribbon(SKPath p, SKRect rect)
    {
        var top = rect.Top + rect.Height * 0.34f;
        var bot = rect.Bottom - rect.Height * 0.10f;
        var midY = (top + bot) / 2f;
        var notch = rect.Width * 0.07f;
        // Main band
        p.MoveTo(rect.Left + notch, top);
        p.LineTo(rect.Right - notch, top);
        p.LineTo(rect.Right, midY);
        p.LineTo(rect.Right - notch, bot);
        p.LineTo(rect.Left + notch, bot);
        p.LineTo(rect.Left, midY);
        p.Close();
    }

    private static void Heart(SKPath p, SKRect rect)
    {
        // Classic two-arc heart inside the rect
        var w = rect.Width;
        var h = rect.Height;
        var cx = rect.MidX;
        var cy = rect.MidY;
        var topY = rect.Top + h * 0.30f;
        var lobeR = w * 0.27f;
        p.MoveTo(cx, rect.Bottom - h * 0.05f);
        p.CubicTo(rect.Left, cy, rect.Left + w * 0.08f, rect.Top, cx, topY);
        p.CubicTo(rect.Right - w * 0.08f, rect.Top, rect.Right, cy, cx, rect.Bottom - h * 0.05f);
        p.Close();
        _ = lobeR;
    }

    private static void Plus(SKPath p, SKRect rect)
    {
        var cx = rect.MidX; var cy = rect.MidY;
        var s = System.Math.Min(rect.Width, rect.Height);
        var arm = s * 0.32f;     // half arm length
        var thick = s * 0.18f;   // half-thickness
        p.MoveTo(cx - thick, cy - arm);
        p.LineTo(cx + thick, cy - arm);
        p.LineTo(cx + thick, cy - thick);
        p.LineTo(cx + arm,   cy - thick);
        p.LineTo(cx + arm,   cy + thick);
        p.LineTo(cx + thick, cy + thick);
        p.LineTo(cx + thick, cy + arm);
        p.LineTo(cx - thick, cy + arm);
        p.LineTo(cx - thick, cy + thick);
        p.LineTo(cx - arm,   cy + thick);
        p.LineTo(cx - arm,   cy - thick);
        p.LineTo(cx - thick, cy - thick);
        p.Close();
    }

    private static void ArrowRight(SKPath p, SKRect rect)
    {
        var cy = rect.MidY;
        var t = rect.Height * 0.22f;     // tail half-thickness
        var headW = rect.Width * 0.40f;  // head length
        p.MoveTo(rect.Left,            cy - t);
        p.LineTo(rect.Right - headW,   cy - t);
        p.LineTo(rect.Right - headW,   rect.Top);
        p.LineTo(rect.Right,           cy);
        p.LineTo(rect.Right - headW,   rect.Bottom);
        p.LineTo(rect.Right - headW,   cy + t);
        p.LineTo(rect.Left,            cy + t);
        p.Close();
    }

    private static void ArrowUp(SKPath p, SKRect rect)
    {
        var cx = rect.MidX;
        var t = rect.Width * 0.22f;
        var headH = rect.Height * 0.40f;
        p.MoveTo(cx - t, rect.Bottom);
        p.LineTo(cx - t, rect.Top + headH);
        p.LineTo(rect.Left, rect.Top + headH);
        p.LineTo(cx, rect.Top);
        p.LineTo(rect.Right, rect.Top + headH);
        p.LineTo(cx + t, rect.Top + headH);
        p.LineTo(cx + t, rect.Bottom);
        p.Close();
    }

    private static void Lightning(SKPath p, SKRect rect)
    {
        // Stylised bolt
        var l = rect.Left; var r = rect.Right; var t = rect.Top; var b = rect.Bottom;
        var w = rect.Width; var h = rect.Height;
        p.MoveTo(l + w * 0.55f, t);
        p.LineTo(l + w * 0.10f, t + h * 0.55f);
        p.LineTo(l + w * 0.42f, t + h * 0.55f);
        p.LineTo(l + w * 0.30f, b);
        p.LineTo(l + w * 0.85f, t + h * 0.45f);
        p.LineTo(l + w * 0.55f, t + h * 0.45f);
        p.Close();
    }

    private static void Crown(SKPath p, SKRect rect)
    {
        var l = rect.Left; var r = rect.Right; var t = rect.Top; var b = rect.Bottom;
        var w = rect.Width; var h = rect.Height;
        var bandTop = t + h * 0.55f;
        p.MoveTo(l, b);
        p.LineTo(l, bandTop);
        p.LineTo(l + w * 0.18f, t + h * 0.10f);
        p.LineTo(l + w * 0.32f, bandTop);
        p.LineTo(l + w * 0.50f, t + h * 0.00f);
        p.LineTo(l + w * 0.68f, bandTop);
        p.LineTo(l + w * 0.82f, t + h * 0.10f);
        p.LineTo(r, bandTop);
        p.LineTo(r, b);
        p.Close();
    }

    private static void Gear(SKPath p, SKRect rect, int teeth)
    {
        var cx = rect.MidX; var cy = rect.MidY;
        var outerR = System.Math.Min(rect.Width, rect.Height) / 2f;
        var innerR = outerR * 0.78f;
        var perTooth = (System.Math.PI * 2) / teeth;
        for (int i = 0; i < teeth; i++)
        {
            var a0 = perTooth * i;
            var a1 = a0 + perTooth * 0.25;
            var a2 = a0 + perTooth * 0.50;
            var a3 = a0 + perTooth * 0.75;
            void Pt(double a, float r, bool first)
            {
                var x = cx + r * (float)System.Math.Cos(a);
                var y = cy + r * (float)System.Math.Sin(a);
                if (first && i == 0) p.MoveTo(x, y); else p.LineTo(x, y);
            }
            Pt(a0, outerR, true);
            Pt(a1, outerR, false);
            Pt(a2, innerR, false);
            Pt(a3, innerR, false);
        }
        p.Close();
        // Inner hub hole
        p.AddCircle(cx, cy, outerR * 0.30f, SKPathDirection.CounterClockwise);
        p.FillType = SKPathFillType.EvenOdd;
    }

    private static void Speech(SKPath p, SKRect rect)
    {
        var bodyBottom = rect.Top + rect.Height * 0.78f;
        var body = new SKRect(rect.Left, rect.Top, rect.Right, bodyBottom);
        var rr = System.Math.Min(body.Width, body.Height) * 0.18f;
        p.AddRoundRect(body, rr, rr);
        // Tail
        var tail = new SKPath();
        tail.MoveTo(rect.Left + rect.Width * 0.30f, bodyBottom - 2);
        tail.LineTo(rect.Left + rect.Width * 0.22f, rect.Bottom);
        tail.LineTo(rect.Left + rect.Width * 0.46f, bodyBottom - 2);
        tail.Close();
        p.AddPath(tail);
    }

    private static void Teardrop(SKPath p, SKRect rect)
    {
        var cx = rect.MidX;
        var w = rect.Width; var h = rect.Height;
        p.MoveTo(cx, rect.Top);
        p.CubicTo(cx + w * 0.55f, rect.Top + h * 0.30f,
                  cx + w * 0.45f, rect.Bottom,
                  cx,             rect.Bottom);
        p.CubicTo(cx - w * 0.45f, rect.Bottom,
                  cx - w * 0.55f, rect.Top + h * 0.30f,
                  cx,             rect.Top);
        p.Close();
    }
}
