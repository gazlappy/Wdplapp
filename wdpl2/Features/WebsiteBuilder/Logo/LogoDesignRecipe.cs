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
    public string IconPosition { get; set; } = "above"; // above, below, behind, left, right, custom
    public float IconOffsetX { get; set; } = 0;         // px nudge at 512 canvas (used when IconPosition == "custom")
    public float IconOffsetY { get; set; } = 0;         // px nudge at 512 canvas (used when IconPosition == "custom")

    // --- Optional embedded image ----------------------------------------
    /// <summary>Base-64 encoded PNG/JPG bytes (so it round-trips through JSON).</summary>
    public string ImageData { get; set; } = "";
    /// <summary>"behind" | "center" | "above" | "below" | "fill"</summary>
    public string ImagePosition { get; set; } = "center";
    public float ImageSize { get; set; } = 240;         // px at 512 canvas
    public float ImageOpacity { get; set; } = 1f;       // 0..1
    public float ImageRotation { get; set; } = 0;       // degrees
    public float ImageOffsetX { get; set; } = 0;       // px nudge at 512 canvas
    public float ImageOffsetY { get; set; } = 0;       // px nudge at 512 canvas

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

    // --- Stacked decorative shape layers --------------------------------
    public System.Collections.Generic.List<LogoShapeLayer> Layers { get; set; } = new();

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
        new("Ocean Wave", "🌊", () => new LogoDesignRecipe
        {
            Text = "WAVE", BgShape = "circle",
            BgColor1 = "#0EA5E9", BgColor2 = "#0C4A6E", UseGradient = true, GradientDirection = "vertical",
            Icon = "🌊", IconPosition = "above", IconSize = 110,
            FontSize = 110, FontWeight = "Black", TextShadow = true, TextShadowOffsetY = 2
        }),
        new("Forest Crest", "🌲", () => new LogoDesignRecipe
        {
            Text = "WILD", Subtitle = "OUTDOORS", BgShape = "shield",
            BgColor1 = "#064E3B", BgColor2 = "#10B981", UseGradient = true, GradientDirection = "diagonal",
            ShowBorder = true, BorderColor = "#FDE68A", BorderWidth = 6,
            Icon = "🌲", IconPosition = "above", IconSize = 80,
            FontSize = 130, FontWeight = "Black", SubtitleSize = 28, SubtitleLetterSpacing = 6
        }),
        new("Royal Crown", "👑", () => new LogoDesignRecipe
        {
            Text = "ROYAL", BgShape = "circle",
            BgColor1 = "#7C2D12", BgColor2 = "#FBBF24", UseGradient = true, GradientDirection = "radial",
            ShowBorder = true, BorderColor = "#FBBF24", BorderWidth = 8,
            Icon = "👑", IconPosition = "above", IconSize = 110,
            FontSize = 110, FontWeight = "Black", TextColor = "#FFFBEB"
        }),
        new("Cyber Hex", "⬢", () => new LogoDesignRecipe
        {
            Text = "CYBR", BgShape = "hexagon",
            BgColor1 = "#020617", BgColor2 = "#1E1B4B", UseGradient = true, GradientDirection = "diagonal",
            TextColor = "#22D3EE", TextStroke = true, TextStrokeColor = "#A78BFA", TextStrokeWidth = 2,
            TextShadow = true, TextShadowColor = "#22D3EE", TextShadowBlur = 14,
            Pattern = "grid", PatternColor = "#22D3EE", PatternOpacity = 0.15f, PatternScale = 28,
            FontSize = 140, FontWeight = "Black"
        }),
        new("Coffee Stamp", "☕", () => new LogoDesignRecipe
        {
            Text = "BREW", Subtitle = "CO.", BgShape = "circle",
            BgColor1 = "#3F2410", BgColor2 = "#78350F", UseGradient = true, GradientDirection = "radial",
            ShowBorder = true, BorderColor = "#FBBF24", BorderWidth = 4,
            Icon = "☕", IconPosition = "above", IconSize = 90,
            FontSize = 130, FontWeight = "Black", SubtitleSize = 28, TextColor = "#FDE68A"
        }),
        new("Pop Burst", "💥", () => new LogoDesignRecipe
        {
            Text = "POP!", BgShape = "burst",
            BgColor1 = "#FACC15", BgColor2 = "#F97316", UseGradient = true, GradientDirection = "radial",
            TextColor = "#7F1D1D", FontSize = 120, FontWeight = "Black",
            TextStroke = true, TextStrokeColor = "#7F1D1D", TextStrokeWidth = 4
        }),
        new("Mountain Peak", "🏔", () => new LogoDesignRecipe
        {
            Text = "ASCEND", BgShape = "triangle",
            BgColor1 = "#1E293B", BgColor2 = "#475569", UseGradient = true, GradientDirection = "vertical",
            FontSize = 70, TextOffsetY = 40, FontWeight = "Black",
            ShowBorder = true, BorderColor = "#E2E8F0", BorderWidth = 4
        }),
        new("Sunset Beach", "🏖", () => new LogoDesignRecipe
        {
            Text = "SUN", BgShape = "circle",
            BgColor1 = "#FB923C", BgColor2 = "#BE185D", UseGradient = true, GradientDirection = "vertical",
            UseThreeColorGradient = true, BgColor3 = "#7C3AED",
            Icon = "🌅", IconPosition = "above", IconSize = 130,
            FontSize = 140, FontWeight = "Black", TextShadow = true
        }),
        new("Lightning Strike", "⚡", () => new LogoDesignRecipe
        {
            Text = "BOLT", BgShape = "rounded-square",
            BgColor1 = "#0F172A", BgColor2 = "#1E1B4B", UseGradient = true, GradientDirection = "diagonal",
            Icon = "⚡", IconPosition = "above", IconSize = 130, IconColor = "#FACC15",
            FontSize = 130, FontWeight = "Black", TextColor = "#FACC15"
        }),
        new("Soft Pastel", "🎀", () => new LogoDesignRecipe
        {
            Text = "Bloom", BgShape = "circle",
            BgColor1 = "#FBCFE8", BgColor2 = "#DDD6FE", UseGradient = true, GradientDirection = "diagonal",
            TextColor = "#831843", TextUppercase = false, FontSize = 160, FontWeight = "Bold"
        }),
        new("Eight-Ball", "8", () => new LogoDesignRecipe
        {
            Text = "8", BgShape = "circle",
            BgColor1 = "#000000", BgColor2 = "#1F2937", UseGradient = true, GradientDirection = "radial",
            FontSize = 320, FontWeight = "Black", TextColor = "#FFFFFF",
            ShowBorder = true, BorderColor = "#FFFFFF", BorderWidth = 4
        }),
        new("Cue Sport", "🎱", () => new LogoDesignRecipe
        {
            Text = "CUE", Subtitle = "CLUB", BgShape = "shield",
            BgColor1 = "#064E3B", BgColor2 = "#022C22", UseGradient = true, GradientDirection = "vertical",
            ShowBorder = true, BorderColor = "#FBBF24", BorderWidth = 8,
            Icon = "🎱", IconPosition = "above", IconSize = 90,
            FontSize = 120, FontWeight = "Black", SubtitleSize = 30, SubtitleLetterSpacing = 8
        }),
        new("Esports Tag", "🎮", () => new LogoDesignRecipe
        {
            Text = "[GG]", BgShape = "rounded-square",
            BgColor1 = "#7C3AED", BgColor2 = "#DB2777", UseGradient = true, GradientDirection = "diagonal",
            FontSize = 150, FontWeight = "Black",
            TextStroke = true, TextStrokeColor = "#FFFFFF", TextStrokeWidth = 3
        }),
        new("Rocket Launch", "🚀", () => new LogoDesignRecipe
        {
            Text = "LIFT", BgShape = "circle",
            BgColor1 = "#1E1B4B", BgColor2 = "#0F172A", UseGradient = true, GradientDirection = "radial",
            Icon = "🚀", IconPosition = "above", IconSize = 130,
            FontSize = 130, FontWeight = "Black", TextColor = "#F8FAFC"
        }),
        new("Wild Cat", "🐯", () => new LogoDesignRecipe
        {
            Text = "TIGERS", BgShape = "shield",
            BgColor1 = "#7C2D12", BgColor2 = "#000000", UseGradient = true, GradientDirection = "vertical",
            Icon = "🐯", IconPosition = "above", IconSize = 100,
            FontSize = 90, FontWeight = "Black", TextColor = "#F59E0B",
            ShowBorder = true, BorderColor = "#F59E0B", BorderWidth = 6
        }),
        new("Sky Wing", "🦅", () => new LogoDesignRecipe
        {
            Text = "EAGLES", BgShape = "shield",
            BgColor1 = "#1E40AF", BgColor2 = "#0F172A", UseGradient = true, GradientDirection = "vertical",
            Icon = "🦅", IconPosition = "above", IconSize = 100,
            FontSize = 95, FontWeight = "Black",
            ShowBorder = true, BorderColor = "#FFFFFF", BorderWidth = 5
        }),
        new("Diamond Tech", "💎", () => new LogoDesignRecipe
        {
            Text = "GEM", BgShape = "diamond",
            BgColor1 = "#06B6D4", BgColor2 = "#3B82F6", UseGradient = true, GradientDirection = "diagonal",
            FontSize = 150, FontWeight = "Black",
            Pattern = "diagonal-lines", PatternColor = "#FFFFFF", PatternOpacity = 0.10f
        }),
        new("Retro 80s", "🌴", () => new LogoDesignRecipe
        {
            Text = "RETRO", BgShape = "rectangle",
            BgColor1 = "#DB2777", BgColor2 = "#7C3AED", UseGradient = true, GradientDirection = "vertical",
            UseThreeColorGradient = true, BgColor3 = "#F59E0B",
            FontSize = 130, FontWeight = "Black", TextColor = "#FFFFFF",
            TextStroke = true, TextStrokeColor = "#7C3AED", TextStrokeWidth = 3
        }),
        new("Minimal Mono", "·", () => new LogoDesignRecipe
        {
            Text = "wd", BgShape = "circle",
            BgColor1 = "#F8FAFC", BgColor2 = "#E2E8F0", UseGradient = true, GradientDirection = "diagonal",
            TextColor = "#0F172A", TextUppercase = false, FontSize = 240, FontWeight = "Bold"
        }),
        new("Heart Mark", "❤", () => new LogoDesignRecipe
        {
            Text = "LOVE", BgShape = "heart",
            BgColor1 = "#EF4444", BgColor2 = "#7F1D1D", UseGradient = true, GradientDirection = "vertical",
            FontSize = 90, FontWeight = "Black", TextOffsetY = 30
        }),
        new("Star Burst", "✨", () => new LogoDesignRecipe
        {
            Text = "WIN", BgShape = "star",
            BgColor1 = "#FACC15", BgColor2 = "#EA580C", UseGradient = true, GradientDirection = "radial",
            FontSize = 110, FontWeight = "Black",
            TextStroke = true, TextStrokeColor = "#7C2D12", TextStrokeWidth = 3
        }),
        new("Octagon Stop", "🛑", () => new LogoDesignRecipe
        {
            Text = "STOP", BgShape = "octagon",
            BgColor1 = "#DC2626", BgColor2 = "#7F1D1D", UseGradient = true, GradientDirection = "vertical",
            ShowBorder = true, BorderColor = "#FFFFFF", BorderWidth = 6,
            FontSize = 130, FontWeight = "Black"
        }),
        new("Pentagon Pro", "⬠", () => new LogoDesignRecipe
        {
            Text = "PRO", BgShape = "pentagon",
            BgColor1 = "#0F766E", BgColor2 = "#134E4A", UseGradient = true, GradientDirection = "vertical",
            FontSize = 150, FontWeight = "Black"
        }),
        new("Speech POW", "💬", () => new LogoDesignRecipe
        {
            Text = "POW!", BgShape = "speech",
            BgColor1 = "#FACC15", BgColor2 = "#F59E0B", UseGradient = true, GradientDirection = "vertical",
            TextColor = "#7F1D1D", FontSize = 130, FontWeight = "Black",
            TextStroke = true, TextStrokeColor = "#7F1D1D", TextStrokeWidth = 3
        }),
        new("Lightning Esports", "⚡", () => new LogoDesignRecipe
        {
            Text = "VOLT", BgShape = "lightning",
            BgColor1 = "#FACC15", BgColor2 = "#F97316", UseGradient = true, GradientDirection = "vertical",
            FontSize = 70, FontWeight = "Black", TextColor = "#0F172A", TextOffsetY = 30
        }),
        new("Gear Mech", "⚙", () => new LogoDesignRecipe
        {
            Text = "MECH", BgShape = "gear",
            BgColor1 = "#475569", BgColor2 = "#0F172A", UseGradient = true, GradientDirection = "radial",
            FontSize = 90, FontWeight = "Black", TextColor = "#F8FAFC"
        }),
        new("Crescent Night", "🌙", () => new LogoDesignRecipe
        {
            Text = "NOX", BgShape = "circle",
            BgColor1 = "#0F172A", BgColor2 = "#1E1B4B", UseGradient = true, GradientDirection = "radial",
            Icon = "🌙", IconPosition = "above", IconSize = 130, IconColor = "#FACC15",
            FontSize = 150, FontWeight = "Black", TextColor = "#FACC15"
        }),
        new("Ribbon Award", "🎖", () => new LogoDesignRecipe
        {
            Text = "WINNER", BgShape = "ribbon",
            BgColor1 = "#7C2D12", BgColor2 = "#DC2626", UseGradient = true, GradientDirection = "vertical",
            ShowBorder = true, BorderColor = "#FBBF24", BorderWidth = 4,
            FontSize = 70, FontWeight = "Black", TextColor = "#FBBF24"
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
