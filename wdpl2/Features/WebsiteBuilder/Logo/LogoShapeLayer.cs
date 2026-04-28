namespace Wdpl2.Features.WebsiteBuilder.Logo;

/// <summary>
/// One stacked decorative shape layered on top of the background but behind (or above) the text.
/// All position/size values are normalised 0..1 against the design canvas (512×512),
/// so layers scale cleanly when the logo is rendered at any size.
/// </summary>
public sealed class LogoShapeLayer
{
    public string Shape { get; set; } = "circle";

    /// <summary>Center X (0..1).</summary>
    public float CenterX { get; set; } = 0.5f;
    /// <summary>Center Y (0..1).</summary>
    public float CenterY { get; set; } = 0.5f;

    /// <summary>Width as fraction of canvas (0..1).</summary>
    public float Width { get; set; } = 0.4f;
    /// <summary>Height as fraction of canvas (0..1).</summary>
    public float Height { get; set; } = 0.4f;

    public float Rotation { get; set; } = 0f;
    public float Opacity { get; set; } = 1f;     // 0..1

    public string FillColor { get; set; } = "#FFFFFF";
    public bool UseGradient { get; set; } = false;
    public string FillColor2 { get; set; } = "#3B82F6";
    public string GradientDirection { get; set; } = "diagonal"; // diagonal/vertical/horizontal/radial

    public bool Stroke { get; set; } = false;
    public string StrokeColor { get; set; } = "#000000";
    public float StrokeWidth { get; set; } = 4f;

    /// <summary>If true, this layer is drawn AFTER the text (i.e. covers it).</summary>
    public bool AboveText { get; set; } = false;

    public LogoShapeLayer Clone() => new()
    {
        Shape = Shape, CenterX = CenterX, CenterY = CenterY,
        Width = Width, Height = Height, Rotation = Rotation, Opacity = Opacity,
        FillColor = FillColor, UseGradient = UseGradient, FillColor2 = FillColor2,
        GradientDirection = GradientDirection,
        Stroke = Stroke, StrokeColor = StrokeColor, StrokeWidth = StrokeWidth,
        AboveText = AboveText
    };
}
