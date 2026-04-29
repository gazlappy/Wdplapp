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
        new("none",            "(blank canvas)",  "∅"),
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
        // --- Extended shape library ---
        new("heptagon",        "Heptagon (7)",    "⬡"),
        new("nonagon",         "Nonagon (9)",     "⬢"),
        new("decagon",         "Decagon (10)",    "⬣"),
        new("dodecagon",       "Dodecagon (12)",  "⬢"),
        new("star4",           "Star (4)",        "✦"),
        new("star7",           "Star (7)",        "✷"),
        new("star8",           "Star (8)",        "✴"),
        new("star10",          "Star (10)",       "✺"),
        new("sparkle",         "Sparkle",         "✧"),
        new("pill",            "Pill / Capsule",  "⬭"),
        new("parallelogram",   "Parallelogram",   "▰"),
        new("trapezoid",       "Trapezoid",       "⏢"),
        new("kite",            "Kite",            "◇"),
        new("chevron",         "Chevron ►",       "❯"),
        new("arrow-left",      "Arrow ◄",         "◄"),
        new("arrow-down",      "Arrow ▼",         "⬇"),
        new("double-arrow",    "Double arrow ↔",  "⇆"),
        new("cloud",           "Cloud",           "☁"),
        new("blob",            "Blob",            "⬬"),
        new("leaf",            "Leaf",            "🍃"),
        new("flame",           "Flame",           "🔥"),
        new("crescent",        "Crescent moon",   "🌙"),
        new("sun-rays",        "Sun / rays",      "☀"),
        new("ring",            "Ring (annulus)",  "◯"),
        new("semicircle",      "Semicircle",      "◖"),
        new("quarter-circle",  "Quarter circle",  "◜"),
        new("pie-slice",       "Pie slice",       "⌔"),
        new("badge",           "Badge",           "🏅"),
        new("tag",             "Tag",             "🏷"),
        new("chat-round",      "Chat (round)",    "💭"),
        new("x-mark",          "X mark",          "✖"),
        new("checkmark",       "Checkmark",       "✓"),
        new("cross-thick",     "Cross (thick)",   "✚"),
        new("scroll",          "Scroll",          "📜"),
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
            case "heptagon":  Polygon(path, rect, 7, -90); break;
            case "nonagon":   Polygon(path, rect, 9, -90); break;
            case "decagon":   Polygon(path, rect, 10, -90); break;
            case "dodecagon": Polygon(path, rect, 12, -90); break;
            case "star4":     Star(path, rect, 4, 0.45f); break;
            case "star7":     Star(path, rect, 7, 0.5f);  break;
            case "star8":     Star(path, rect, 8, 0.5f);  break;
            case "star10":    Star(path, rect, 10, 0.55f); break;
            case "sparkle":   Star(path, rect, 4, 0.22f); break;
            case "pill":      Pill(path, rect); break;
            case "parallelogram": Parallelogram(path, rect); break;
            case "trapezoid": Trapezoid(path, rect); break;
            case "kite":      Kite(path, rect); break;
            case "chevron":   Chevron(path, rect); break;
            case "arrow-left":   ArrowLeft(path, rect); break;
            case "arrow-down":   ArrowDown(path, rect); break;
            case "double-arrow": DoubleArrow(path, rect); break;
            case "cloud":     Cloud(path, rect); break;
            case "blob":      Blob(path, rect); break;
            case "leaf":      Leaf(path, rect); break;
            case "flame":     Flame(path, rect); break;
            case "crescent":  Crescent(path, rect); break;
            case "sun-rays":  SunRays(path, rect, 12); break;
            case "ring":      Ring(path, rect); break;
            case "semicircle":     Semicircle(path, rect); break;
            case "quarter-circle": QuarterCircle(path, rect); break;
            case "pie-slice": PieSlice(path, rect); break;
            case "badge":     Badge(path, rect); break;
            case "tag":       Tag(path, rect); break;
            case "chat-round": ChatRound(path, rect); break;
            case "x-mark":    XMark(path, rect); break;
            case "checkmark": Checkmark(path, rect); break;
            case "cross-thick": CrossThick(path, rect); break;
            case "scroll":    Scroll(path, rect); break;
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

    private static void Pill(SKPath p, SKRect rect)
    {
        var r = rect.Height / 2f;
        p.AddRoundRect(rect, r, r);
    }

    private static void Parallelogram(SKPath p, SKRect rect)
    {
        var slant = rect.Width * 0.20f;
        p.MoveTo(rect.Left + slant, rect.Top);
        p.LineTo(rect.Right, rect.Top);
        p.LineTo(rect.Right - slant, rect.Bottom);
        p.LineTo(rect.Left, rect.Bottom);
        p.Close();
    }

    private static void Trapezoid(SKPath p, SKRect rect)
    {
        var inset = rect.Width * 0.18f;
        p.MoveTo(rect.Left + inset, rect.Top);
        p.LineTo(rect.Right - inset, rect.Top);
        p.LineTo(rect.Right, rect.Bottom);
        p.LineTo(rect.Left, rect.Bottom);
        p.Close();
    }

    private static void Kite(SKPath p, SKRect rect)
    {
        var cx = rect.MidX;
        p.MoveTo(cx, rect.Top);
        p.LineTo(rect.Right, rect.Top + rect.Height * 0.38f);
        p.LineTo(cx, rect.Bottom);
        p.LineTo(rect.Left, rect.Top + rect.Height * 0.38f);
        p.Close();
    }

    private static void Chevron(SKPath p, SKRect rect)
    {
        var notch = rect.Width * 0.30f;
        var cy = rect.MidY;
        p.MoveTo(rect.Left, rect.Top);
        p.LineTo(rect.Right - notch, rect.Top);
        p.LineTo(rect.Right, cy);
        p.LineTo(rect.Right - notch, rect.Bottom);
        p.LineTo(rect.Left, rect.Bottom);
        p.LineTo(rect.Left + notch, cy);
        p.Close();
    }

    private static void ArrowLeft(SKPath p, SKRect rect)
    {
        var cy = rect.MidY;
        var t = rect.Height * 0.22f;
        var headW = rect.Width * 0.40f;
        p.MoveTo(rect.Right,           cy - t);
        p.LineTo(rect.Left + headW,    cy - t);
        p.LineTo(rect.Left + headW,    rect.Top);
        p.LineTo(rect.Left,            cy);
        p.LineTo(rect.Left + headW,    rect.Bottom);
        p.LineTo(rect.Left + headW,    cy + t);
        p.LineTo(rect.Right,           cy + t);
        p.Close();
    }

    private static void ArrowDown(SKPath p, SKRect rect)
    {
        var cx = rect.MidX;
        var t = rect.Width * 0.22f;
        var headH = rect.Height * 0.40f;
        p.MoveTo(cx - t, rect.Top);
        p.LineTo(cx - t, rect.Bottom - headH);
        p.LineTo(rect.Left, rect.Bottom - headH);
        p.LineTo(cx, rect.Bottom);
        p.LineTo(rect.Right, rect.Bottom - headH);
        p.LineTo(cx + t, rect.Bottom - headH);
        p.LineTo(cx + t, rect.Top);
        p.Close();
    }

    private static void DoubleArrow(SKPath p, SKRect rect)
    {
        var cy = rect.MidY;
        var t = rect.Height * 0.20f;
        var headW = rect.Width * 0.28f;
        p.MoveTo(rect.Left, cy);
        p.LineTo(rect.Left + headW, rect.Top);
        p.LineTo(rect.Left + headW, cy - t);
        p.LineTo(rect.Right - headW, cy - t);
        p.LineTo(rect.Right - headW, rect.Top);
        p.LineTo(rect.Right, cy);
        p.LineTo(rect.Right - headW, rect.Bottom);
        p.LineTo(rect.Right - headW, cy + t);
        p.LineTo(rect.Left + headW, cy + t);
        p.LineTo(rect.Left + headW, rect.Bottom);
        p.Close();
    }

    private static void Cloud(SKPath p, SKRect rect)
    {
        var w = rect.Width; var h = rect.Height;
        var b = rect.Bottom - h * 0.05f;
        p.AddCircle(rect.Left + w * 0.28f, b - h * 0.30f, h * 0.30f);
        p.AddCircle(rect.Left + w * 0.50f, b - h * 0.45f, h * 0.36f);
        p.AddCircle(rect.Left + w * 0.72f, b - h * 0.30f, h * 0.30f);
        p.AddRoundRect(new SKRect(rect.Left + w * 0.10f, b - h * 0.30f,
                                  rect.Right - w * 0.10f, b),
                       h * 0.18f, h * 0.18f);
    }

    private static void Blob(SKPath p, SKRect rect)
    {
        var cx = rect.MidX; var cy = rect.MidY;
        var rad = System.Math.Min(rect.Width, rect.Height) / 2f;
        // Wobbly closed curve, 8 control points with alternating radii
        const int pts = 8;
        var rng = new System.Random(rect.Width.GetHashCode() ^ rect.Height.GetHashCode());
        var radii = new float[pts];
        for (int i = 0; i < pts; i++)
            radii[i] = rad * (0.78f + (float)rng.NextDouble() * 0.22f);
        for (int i = 0; i < pts; i++)
        {
            var a = 2 * System.Math.PI * i / pts;
            var x = cx + radii[i] * (float)System.Math.Cos(a);
            var y = cy + radii[i] * (float)System.Math.Sin(a);
            if (i == 0) p.MoveTo(x, y);
            else
            {
                var aPrev = 2 * System.Math.PI * (i - 0.5) / pts;
                var rMid = (radii[i] + radii[(i - 1 + pts) % pts]) / 2f * 1.10f;
                var ctrlX = cx + rMid * (float)System.Math.Cos(aPrev);
                var ctrlY = cy + rMid * (float)System.Math.Sin(aPrev);
                p.QuadTo(ctrlX, ctrlY, x, y);
            }
        }
        p.Close();
    }

    private static void Leaf(SKPath p, SKRect rect)
    {
        // Almond / leaf via two arcs
        p.MoveTo(rect.Left, rect.Bottom);
        p.QuadTo(rect.Left, rect.Top, rect.Right, rect.Top);
        p.QuadTo(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
        p.Close();
    }

    private static void Flame(SKPath p, SKRect rect)
    {
        var cx = rect.MidX;
        var w = rect.Width; var h = rect.Height;
        p.MoveTo(cx, rect.Top);
        p.CubicTo(cx + w * 0.50f, rect.Top + h * 0.35f,
                  cx + w * 0.10f, rect.Top + h * 0.45f,
                  cx + w * 0.25f, rect.Top + h * 0.65f);
        p.CubicTo(cx + w * 0.55f, rect.Top + h * 0.55f,
                  cx + w * 0.55f, rect.Bottom,
                  cx,             rect.Bottom);
        p.CubicTo(cx - w * 0.55f, rect.Bottom,
                  cx - w * 0.55f, rect.Top + h * 0.55f,
                  cx - w * 0.25f, rect.Top + h * 0.65f);
        p.CubicTo(cx - w * 0.10f, rect.Top + h * 0.45f,
                  cx - w * 0.50f, rect.Top + h * 0.35f,
                  cx,             rect.Top);
        p.Close();
    }

    private static void Crescent(SKPath p, SKRect rect)
    {
        var rad = System.Math.Min(rect.Width, rect.Height) / 2f;
        var cx = rect.MidX; var cy = rect.MidY;
        p.AddCircle(cx, cy, rad);
        p.AddCircle(cx + rad * 0.45f, cy, rad * 0.85f, SKPathDirection.CounterClockwise);
        p.FillType = SKPathFillType.EvenOdd;
    }

    private static void SunRays(SKPath p, SKRect rect, int rays)
    {
        var cx = rect.MidX; var cy = rect.MidY;
        var outerR = System.Math.Min(rect.Width, rect.Height) / 2f;
        var innerR = outerR * 0.55f;
        var coreR  = outerR * 0.40f;
        var step = System.Math.PI / rays;
        var a = -System.Math.PI / 2;
        for (int i = 0; i < rays * 2; i++)
        {
            var r = (i % 2 == 0) ? outerR : innerR;
            var x = cx + r * (float)System.Math.Cos(a);
            var y = cy + r * (float)System.Math.Sin(a);
            if (i == 0) p.MoveTo(x, y); else p.LineTo(x, y);
            a += step;
        }
        p.Close();
        p.AddCircle(cx, cy, coreR, SKPathDirection.CounterClockwise);
        p.FillType = SKPathFillType.EvenOdd;
    }

    private static void Ring(SKPath p, SKRect rect)
    {
        var rad = System.Math.Min(rect.Width, rect.Height) / 2f;
        p.AddCircle(rect.MidX, rect.MidY, rad);
        p.AddCircle(rect.MidX, rect.MidY, rad * 0.62f, SKPathDirection.CounterClockwise);
        p.FillType = SKPathFillType.EvenOdd;
    }

    private static void Semicircle(SKPath p, SKRect rect)
    {
        var rad = System.Math.Min(rect.Width, rect.Height / 2f);
        var box = new SKRect(rect.MidX - rad, rect.Bottom - rad * 2, rect.MidX + rad, rect.Bottom);
        p.MoveTo(box.Left, box.Bottom);
        p.ArcTo(box, 180, 180, false);
        p.Close();
    }

    private static void QuarterCircle(SKPath p, SKRect rect)
    {
        var s = System.Math.Min(rect.Width, rect.Height);
        var box = new SKRect(rect.Left, rect.Top, rect.Left + s * 2, rect.Top + s * 2);
        p.MoveTo(rect.Left, rect.Top);
        p.ArcTo(box, 180, 90, false);
        p.LineTo(rect.Left, rect.Top);
        p.Close();
    }

    private static void PieSlice(SKPath p, SKRect rect)
    {
        var rad = System.Math.Min(rect.Width, rect.Height) / 2f;
        var cx = rect.MidX; var cy = rect.MidY;
        var box = new SKRect(cx - rad, cy - rad, cx + rad, cy + rad);
        p.MoveTo(cx, cy);
        p.ArcTo(box, -90, 270, false);
        p.LineTo(cx, cy);
        p.Close();
    }

    private static void Badge(SKPath p, SKRect rect)
    {
        var w = rect.Width; var h = rect.Height;
        var rad = System.Math.Min(w, h) * 0.35f;
        var cx = rect.MidX;
        var medalCY = rect.Top + h * 0.55f;
        // Ribbon V at top
        p.MoveTo(cx - w * 0.18f, rect.Top);
        p.LineTo(cx, rect.Top + h * 0.30f);
        p.LineTo(cx + w * 0.18f, rect.Top);
        p.LineTo(cx + w * 0.05f, rect.Top + h * 0.40f);
        p.LineTo(cx - w * 0.05f, rect.Top + h * 0.40f);
        p.Close();
        // Medal
        p.AddCircle(cx, medalCY, rad);
    }

    private static void Tag(SKPath p, SKRect rect)
    {
        var notch = rect.Width * 0.18f;
        var holeR = rect.Height * 0.10f;
        p.MoveTo(rect.Left + notch, rect.Top);
        p.LineTo(rect.Right, rect.Top);
        p.LineTo(rect.Right, rect.Bottom);
        p.LineTo(rect.Left + notch, rect.Bottom);
        p.LineTo(rect.Left, rect.MidY);
        p.Close();
        p.AddCircle(rect.Left + notch + holeR * 1.6f, rect.MidY, holeR, SKPathDirection.CounterClockwise);
        p.FillType = SKPathFillType.EvenOdd;
    }

    private static void ChatRound(SKPath p, SKRect rect)
    {
        var bodyBottom = rect.Top + rect.Height * 0.78f;
        var body = new SKRect(rect.Left, rect.Top, rect.Right, bodyBottom);
        var rr = System.Math.Min(body.Width, body.Height) * 0.45f;
        p.AddRoundRect(body, rr, rr);
        var tail = new SKPath();
        tail.MoveTo(rect.Left + rect.Width * 0.40f, bodyBottom - 2);
        tail.LineTo(rect.Left + rect.Width * 0.30f, rect.Bottom);
        tail.LineTo(rect.Left + rect.Width * 0.55f, bodyBottom - 2);
        tail.Close();
        p.AddPath(tail);
    }

    private static void XMark(SKPath p, SKRect rect)
    {
        var cx = rect.MidX; var cy = rect.MidY;
        var s = System.Math.Min(rect.Width, rect.Height);
        var arm = s * 0.42f;
        var t = s * 0.13f;
        // Build a rotated plus by computing two thick diagonal bars (rectangles rotated 45°)
        var diag = (float)(arm * System.Math.Sqrt(2));
        var th   = (float)(t   * System.Math.Sqrt(2));
        // Bar 1: top-left to bottom-right
        p.MoveTo(cx - diag,        cy - diag + th);
        p.LineTo(cx - diag + th,   cy - diag);
        p.LineTo(cx + diag,        cy + diag - th);
        p.LineTo(cx + diag - th,   cy + diag);
        p.Close();
        // Bar 2: top-right to bottom-left
        p.MoveTo(cx + diag - th,   cy - diag);
        p.LineTo(cx + diag,        cy - diag + th);
        p.LineTo(cx - diag + th,   cy + diag);
        p.LineTo(cx - diag,        cy + diag - th);
        p.Close();
    }

    private static void Checkmark(SKPath p, SKRect rect)
    {
        var w = rect.Width; var h = rect.Height;
        var t = System.Math.Min(w, h) * 0.16f;
        var x1 = rect.Left + w * 0.10f;  var y1 = rect.Top + h * 0.55f;
        var x2 = rect.Left + w * 0.40f;  var y2 = rect.Top + h * 0.85f;
        var x3 = rect.Left + w * 0.92f;  var y3 = rect.Top + h * 0.20f;
        // Approximate thick polyline as a 6-point polygon
        p.MoveTo(x1,        y1);
        p.LineTo(x2,        y2 - t * 0.2f);
        p.LineTo(x3 - t,    y3);
        p.LineTo(x3,        y3 + t);
        p.LineTo(x2,        y2 + t);
        p.LineTo(x1 - t * 0.2f, y1 + t);
        p.Close();
    }

    private static void CrossThick(SKPath p, SKRect rect)
    {
        var cx = rect.MidX; var cy = rect.MidY;
        var s = System.Math.Min(rect.Width, rect.Height);
        var arm = s * 0.45f;
        var thick = s * 0.28f;
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

    private static void Scroll(SKPath p, SKRect rect)
    {
        var w = rect.Width; var h = rect.Height;
        var roll = w * 0.10f;
        var body = new SKRect(rect.Left + roll, rect.Top + h * 0.10f,
                              rect.Right - roll, rect.Bottom - h * 0.10f);
        p.AddRect(body);
        // Rolls (ovals at each end)
        p.AddOval(new SKRect(rect.Left, rect.Top + h * 0.10f,
                             rect.Left + roll * 2, rect.Bottom - h * 0.10f));
        p.AddOval(new SKRect(rect.Right - roll * 2, rect.Top + h * 0.10f,
                             rect.Right, rect.Bottom - h * 0.10f));
    }
}
