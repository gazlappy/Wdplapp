using Wdpl2.Models;

namespace Wdpl2.Domain.Fixtures;

/// <summary>
/// How many frames a match has, and which of them are doubles.
/// </summary>
/// <remarks>
/// The answer comes from three places that override one another, and getting
/// the precedence wrong produces a scorecard with the wrong number of frames -
/// which is only noticed on a match night. This is the one place that decides.
/// </remarks>
public sealed record MatchFormat(int TotalFrames, int SinglesFrames, int DoublesFrames)
{
    /// <summary>The league's standard when nothing else is configured.</summary>
    public const int WdplStandardFrames = 15;

    public bool HasDoubles => DoublesFrames > 0;

    /// <summary>
    /// Resolves the format for a season.
    /// </summary>
    /// <remarks>
    /// Precedence, most specific first:
    /// <list type="number">
    ///   <item>the season's doubles split, when doubles are enabled;</item>
    ///   <item>the season's own frame count, when set;</item>
    ///   <item>the app-wide default;</item>
    ///   <item>the league standard.</item>
    /// </list>
    /// </remarks>
    public static MatchFormat For(Season? season, AppSettings? settings)
    {
        var total = WdplStandardFrames;

        if (settings is not null && settings.DefaultFramesPerMatch > 0)
            total = settings.DefaultFramesPerMatch;

        if (season is not null && season.FramesPerMatch > 0)
            total = season.FramesPerMatch;

        // A doubles season describes itself as a split rather than a total, and
        // that split wins - it says both how many frames there are and how many
        // of them are doubles.
        if (season is not null && season.IncludeDoubles
            && (season.SinglesFrameCount > 0 || season.DoublesFrameCount > 0))
        {
            var singles = Math.Max(0, season.SinglesFrameCount);
            var doubles = Math.Max(0, season.DoublesFrameCount);
            return new MatchFormat(singles + doubles, singles, doubles);
        }

        return new MatchFormat(total, total, 0);
    }

    /// <summary>
    /// The frame numbers that are doubles, by convention the closing frames.
    /// </summary>
    /// <remarks>
    /// Doubles are played at the end of the night, so they take the last
    /// numbers on the card.
    /// </remarks>
    public IReadOnlyList<int> DoublesFrameNumbers()
    {
        if (DoublesFrames <= 0) return Array.Empty<int>();

        var numbers = new List<int>(DoublesFrames);
        for (var n = TotalFrames - DoublesFrames + 1; n <= TotalFrames; n++)
            numbers.Add(n);
        return numbers;
    }

    public override string ToString() =>
        HasDoubles
            ? $"{TotalFrames} frames ({SinglesFrames} singles, {DoublesFrames} doubles)"
            : $"{TotalFrames} frames";
}
