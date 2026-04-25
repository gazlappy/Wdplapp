namespace Wdpl2.Models;

/// <summary>
/// Represents a game in the games library
/// </summary>
public class GameInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string Icon { get; init; }
    public required string ThumbnailColor { get; init; }
    public required Type PageType { get; init; }
    public bool IsNew { get; init; }
    public bool IsFeatured { get; init; }
    public int PlayerCount { get; init; } = 1;
    public string Difficulty { get; init; } = "Medium";
}

/// <summary>
/// Game categories for filtering
/// </summary>
public static class GameCategory
{
    public const string Sports = "Sports";
    public const string Puzzle = "Puzzle";
    public const string Arcade = "Arcade";
    public const string Card = "Card";
    public const string Strategy = "Strategy";
}
