using Wdpl2.Models;

namespace Wdpl2.Domain.Fixtures;

/// <summary>
/// How many frames a match has, and how many a player may play.
/// </summary>
/// <remarks>
/// <para>
/// One source, deliberately: <see cref="AppSettings"/>. The league runs one
/// format, so the count is set once under Settings and everything follows it.
/// </para>
/// <para>
/// Seasons carry their own <c>FramesPerMatch</c> and doubles fields, and
/// fixtures can carry pre-built frames. Both are ignored here. A number that
/// can be set in three places is a number nobody can be sure of, and the cost
/// of being wrong lands on a match night with two captains waiting.
/// </para>
/// </remarks>
public sealed record MatchFormat(int TotalFrames, int MaxFramesPerPlayer)
{
    /// <summary>Used only if settings are missing entirely.</summary>
    public const int WdplStandardFrames = 15;

    /// <summary>Used only if settings are missing entirely.</summary>
    public const int WdplStandardMaxPerPlayer = 3;

    /// <summary>Reads the format from the app's settings.</summary>
    public static MatchFormat From(AppSettings? settings)
    {
        var frames = settings is { DefaultFramesPerMatch: > 0 }
            ? settings.DefaultFramesPerMatch
            : WdplStandardFrames;

        var maxPerPlayer = settings is { MaxFramesPerPlayer: > 0 }
            ? settings.MaxFramesPerPlayer
            : WdplStandardMaxPerPlayer;

        return new MatchFormat(frames, maxPerPlayer);
    }

    public override string ToString() =>
        $"{TotalFrames} frames, max {MaxFramesPerPlayer} per player";
}
