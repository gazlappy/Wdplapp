using System.Text.Json;

namespace Wdpl2.Features.WebsiteBuilder.Logo;

/// <summary>
/// Serializable recipe describing a custom logo designed in the Logo Designer.
/// Persisted as JSON on <see cref="Wdpl2.Models.WebsiteLogoCatalogItem.DesignJson"/>
/// so designs can be re-edited later.
/// </summary>
public sealed class LogoDesignRecipe
{
    // --- Text / Monogram -------------------------------------------------
    public string Text { get; set; } = "WDPL";
    public string FontFamily { get; set; } = "Inter";
    public string FontWeight { get; set; } = "Bold";   // Regular, Bold, Black
    public bool TextUppercase { get; set; } = true;
    public float FontSize { get; set; } = 180;          // px at 512x512 canvas
    public string TextColor { get; set; } = "#FFFFFF";
    public float TextOffsetY { get; set; } = 0;         // px nudge
    public float LetterSpacing { get; set; } = 0;       // px (at 512 canvas)
    public float TextRotation { get; set; } = 0;        // degrees

    // Text outline / stroke
    public bool TextStroke { get; set; } = false;
    public string TextStrokeColor { get; set; } = "#0F172A";
    public float TextStrokeWidth { get; set; } = 4;

    // Drop shadow on text
    public bool TextShadow { get; set; } = false;
    public string TextShadowColor { get; set; } = "#000000";
    public float TextShadowBlur { get; set; } = 6;
    public float TextShadowOffsetX { get; set; } = 0;
    public float TextShadowOffsetY { get; set; } = 4;

    // Subtitle (small text under main text)
    public string Subtitle { get; set; } = "";
    public float SubtitleSize { get; set; } = 36;
    public string SubtitleColor { get; set; } = "#FFFFFF";
    public float SubtitleLetterSpacing { get; set; } = 6;

    // --- Optional icon / emoji above the text ----------------------------
    public string Icon { get; set; } = "";              // emoji or short string
    public float IconSize { get; set; } = 120;
    public string IconColor { get; set; } = "#FFFFFF";
    public float IconRotation { get; set; } = 0;        // degrees
    public string IconPosition { get; set; } = "above"; // above, below, behind, left, right

    // --- Background shape ------------------------------------------------
    // none, circle, rounded-square, square, shield, hexagon, star, diamond, octagon, triangle, banner
    public string BgShape { get; set; } = "circle";
    public string BgColor1 { get; set; } = "#3B82F6";
    public string BgColor2 { get; set; } = "#1E40AF";
    public bool UseGradient { get; set; } = true;
    public bool UseThreeColorGradient { get; set; } = false;
    public string BgColor3 { get; set; } = "#9333EA";
    public string GradientDirection { get; set; } = "diagonal"; // vertical, horizontal, diagonal, radial, angle
    public float GradientAngle { get; set; } = 45;      // degrees, used when direction == angle
    public float Padding { get; set; } = 24;

    // Drop shadow on the shape
    public bool ShapeShadow { get; set; } = false;
    public string ShapeShadowColor { get; set; } = "#000000";
    public float ShapeShadowBlur { get; set; } = 16;
    public float ShapeShadowOffsetY { get; set; } = 8;

    // Pattern overlay inside shape
    public string Pattern { get; set; } = "none";       // none, stripes, dots, grid, diagonal-lines, chevron
    public string PatternColor { get; set; } = "#FFFFFF";
    public float PatternOpacity { get; set; } = 0.12f;
    public float PatternScale { get; set; } = 24;

    // --- Border ----------------------------------------------------------
    public bool ShowBorder { get; set; } = false;
    public string BorderColor { get; set; } = "#FFFFFF";
    public float BorderWidth { get; set; } = 6;
    public string BorderStyle { get; set; } = "solid";  // solid, dashed, dotted, double

    private static readonly JsonSerializerOptions s_json = new() { WriteIndented = false };

    public string ToJson() => JsonSerializer.Serialize(this, s_json);

    public static LogoDesignRecipe? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<LogoDesignRecipe>(json); }
        catch { return null; }
    }

    public LogoDesignRecipe Clone() => FromJson(ToJson()) ?? new LogoDesignRecipe();

    // --- Templates -------------------------------------------------------
    public sealed record Template(string Name, string Emoji, System.Func<LogoDesignRecipe> Build);

    public static IReadOnlyList<Template> Templates { get; } = new Template[]
    {
        new("Classic Badge", "🛡", () => new LogoDesignRecipe
        {
            Text = "WDPL", BgShape = "shield", BgColor1 = "#1E3A8A", BgColor2 = "#3B82F6",
            UseGradient = true, GradientDirection = "vertical",
            ShowBorder = true, BorderColor = "#FBBF24", BorderWidth = 8,
            FontWeight = "Black", FontSize = 150
        }),
        new("Sport Crest", "🏆", () => new LogoDesignRecipe
        {
            Text = "FC", Subtitle = "EST. 1985", BgShape = "shield",
            BgColor1 = "#7F1D1D", BgColor2 = "#DC2626", GradientDirection = "diagonal",
            ShowBorder = true, BorderColor = "#FBBF24", BorderWidth = 10,
            Icon = "⚽", IconPosition = "above", IconSize = 90,
            FontWeight = "Black", FontSize = 200, SubtitleSize = 32, SubtitleLetterSpacing = 8
        }),
        new("Pool 8-Ball", "🎱", () => new LogoDesignRecipe
        {
            Text = "POOL", Icon = "🎱", BgShape = "circle",
            BgColor1 = "#0F172A", BgColor2 = "#1E293B", UseGradient = true, GradientDirection = "radial",
            ShowBorder = true, BorderColor = "#FBBF24", BorderWidth = 6,
            FontSize = 110, IconSize = 160, IconPosition = "above"
        }),
        new("Modern Mono", "M", () => new LogoDesignRecipe
        {
            Text = "M", BgShape = "rounded-square",
            BgColor1 = "#0EA5E9", BgColor2 = "#6366F1", GradientDirection = "diagonal",
            FontSize = 320, FontWeight = "Black"
        }),
        new("Hex Tech", "⬡", () => new LogoDesignRecipe
        {
            Text = "TECH", BgShape = "hexagon",
            BgColor1 = "#10B981", BgColor2 = "#0EA5E9", UseGradient = true, GradientDirection = "diagonal",
            Pattern = "grid", PatternColor = "#FFFFFF", PatternOpacity = 0.10f, PatternScale = 32,
            FontSize = 130
        }),
        new("Sunset Star", "⭐", () => new LogoDesignRecipe
        {
            Text = "STAR", BgShape = "star",
            BgColor1 = "#F59E0B", BgColor2 = "#DC2626", UseGradient = true, GradientDirection = "vertical",
            FontSize = 100, FontWeight = "Black", TextShadow = true
        }),
        new("Neon Glow", "✨", () => new LogoDesignRecipe
        {
            Text = "NEON", BgShape = "rounded-square",
            BgColor1 = "#0F172A", BgColor2 = "#1E1B4B", UseGradient = true, GradientDirection = "radial",
            TextColor = "#22D3EE", TextStroke = true, TextStrokeColor = "#0E7490", TextStrokeWidth = 3,
            TextShadow = true, TextShadowColor = "#22D3EE", TextShadowBlur = 18, TextShadowOffsetY = 0,
            FontSize = 160, FontWeight = "Black"
        }),
        new("Vintage Banner", "📜", () => new LogoDesignRecipe
        {
            Text = "EST 1985", BgShape = "banner",
            BgColor1 = "#7C2D12", BgColor2 = "#92400E", UseGradient = true, GradientDirection = "vertical",
            ShowBorder = true, BorderColor = "#FBBF24", BorderWidth = 4,
            FontSize = 90, FontWeight = "Black", TextUppercase = true
        }),
        new("Diamond Edge", "◆", () => new LogoDesignRecipe
        {
            Text = "D", BgShape = "diamond",
            BgColor1 = "#8B5CF6", BgColor2 = "#3B82F6", UseGradient = true, GradientDirection = "diagonal",
            FontSize = 240, FontWeight = "Black"
        }),
        new("Triangle Peak", "▲", () => new LogoDesignRecipe
        {
            Text = "PEAK", BgShape = "triangle",
            BgColor1 = "#059669", BgColor2 = "#0F766E", UseGradient = true, GradientDirection = "vertical",
            FontSize = 90, TextOffsetY = 30
        }),
        new("Pastel Cloud", "☁", () => new LogoDesignRecipe
        {
            Text = "soft", BgShape = "circle",
            BgColor1 = "#FCE7F3", BgColor2 = "#DBEAFE", UseGradient = true, GradientDirection = "diagonal",
            TextColor = "#1E293B", TextUppercase = false, FontSize = 170, FontWeight = "Bold"
        }),
        new("Stripes Sport", "🏁", () => new LogoDesignRecipe
        {
            Text = "GO", BgShape = "circle",
            BgColor1 = "#DC2626", BgColor2 = "#7F1D1D", UseGradient = false,
            Pattern = "stripes", PatternColor = "#FFFFFF", PatternOpacity = 0.18f, PatternScale = 28,
            FontSize = 240, FontWeight = "Black"
        }),
    };

    private static readonly System.Random s_rng = new();
    private static readonly string[] s_paletteA = ["#3B82F6","#10B981","#F59E0B","#EF4444","#8B5CF6","#0EA5E9","#22C55E","#F97316","#EC4899","#14B8A6"];
    private static readonly string[] s_paletteB = ["#1E40AF","#047857","#D97706","#7F1D1D","#6D28D9","#0369A1","#15803D","#9A3412","#9D174D","#0F766E"];
    private static readonly string[] s_shapes = ["circle","rounded-square","shield","hexagon","star","diamond","octagon"];

    public static LogoDesignRecipe Random()
    {
        int i = s_rng.Next(s_paletteA.Length);
        return new LogoDesignRecipe
        {
            Text = "WDPL",
            BgColor1 = s_paletteA[i],
            BgColor2 = s_paletteB[i],
            BgShape = s_shapes[s_rng.Next(s_shapes.Length)],
            GradientDirection = new[] { "diagonal","vertical","horizontal","radial","angle" }[s_rng.Next(5)],
            GradientAngle = s_rng.Next(0, 360),
            UseGradient = s_rng.NextDouble() > 0.15,
            ShowBorder = s_rng.NextDouble() > 0.5,
            BorderColor = "#FFFFFF",
            BorderWidth = s_rng.Next(2, 12),
            Pattern = s_rng.NextDouble() > 0.7 ? new[] { "stripes","dots","grid","diagonal-lines" }[s_rng.Next(4)] : "none",
        };
    }
}
