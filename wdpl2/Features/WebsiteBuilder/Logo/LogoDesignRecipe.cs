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

    // --- Optional icon / emoji above the text ----------------------------
    public string Icon { get; set; } = "";              // emoji or short string
    public float IconSize { get; set; } = 120;
    public string IconColor { get; set; } = "#FFFFFF";

    // --- Background shape ------------------------------------------------
    public string BgShape { get; set; } = "circle";     // none, circle, rounded-square, square, shield, hexagon
    public string BgColor1 { get; set; } = "#3B82F6";
    public string BgColor2 { get; set; } = "#1E40AF";
    public bool UseGradient { get; set; } = true;
    public string GradientDirection { get; set; } = "diagonal"; // vertical, horizontal, diagonal, radial
    public float Padding { get; set; } = 24;

    // --- Border ----------------------------------------------------------
    public bool ShowBorder { get; set; } = false;
    public string BorderColor { get; set; } = "#FFFFFF";
    public float BorderWidth { get; set; } = 6;

    private static readonly JsonSerializerOptions s_json = new() { WriteIndented = false };

    public string ToJson() => JsonSerializer.Serialize(this, s_json);

    public static LogoDesignRecipe? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<LogoDesignRecipe>(json); }
        catch { return null; }
    }

    public LogoDesignRecipe Clone() => FromJson(ToJson()) ?? new LogoDesignRecipe();
}
