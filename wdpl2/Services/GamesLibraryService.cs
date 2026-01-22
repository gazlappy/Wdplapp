using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Service that manages the games library catalog
/// </summary>
public class GamesLibraryService
{
    private readonly List<GameInfo> _games = [];

    public GamesLibraryService()
    {
        InitializeGames();
    }

    private void InitializeGames()
    {
        // Pool Game - the flagship game
        _games.Add(new GameInfo
        {
            Id = "pool",
            Name = "UK 8-Ball Pool",
            Description = "Play a realistic game of UK 8-ball pool with full physics, spin control, and EPA rules.",
            Category = GameCategory.Sports,
            Icon = "??",
            ThumbnailColor = "#1a7f37",
            PageType = typeof(Views.PoolGamePage),
            IsFeatured = true,
            PlayerCount = 2,
            Difficulty = "Medium"
        });

        // Snake Game
        _games.Add(new GameInfo
        {
            Id = "snake",
            Name = "Snake",
            Description = "Classic snake game. Eat food, grow longer, and avoid hitting walls or yourself!",
            Category = GameCategory.Arcade,
            Icon = "??",
            ThumbnailColor = "#22c55e",
            PageType = typeof(Views.SnakeGamePage),
            IsNew = true,
            PlayerCount = 1,
            Difficulty = "Easy"
        });

        // Memory Card Game
        _games.Add(new GameInfo
        {
            Id = "memory",
            Name = "Memory Match",
            Description = "Test your memory by matching pairs of cards. Beat your best time!",
            Category = GameCategory.Puzzle,
            Icon = "??",
            ThumbnailColor = "#8b5cf6",
            PageType = typeof(Views.MemoryGamePage),
            IsNew = true,
            PlayerCount = 1,
            Difficulty = "Easy"
        });

        // Breakout/Brick Breaker
        _games.Add(new GameInfo
        {
            Id = "breakout",
            Name = "Brick Breaker",
            Description = "Classic brick breaker arcade game. Break all the bricks with your ball and paddle!",
            Category = GameCategory.Arcade,
            Icon = "??",
            ThumbnailColor = "#ef4444",
            PageType = typeof(Views.BreakoutGamePage),
            IsNew = true,
            PlayerCount = 1,
            Difficulty = "Medium"
        });

        // Retro FPS - Doom-style shooter
        _games.Add(new GameInfo
        {
            Id = "retro-fps",
            Name = "Dungeon Blaster",
            Description = "Doom-style retro FPS! Navigate dungeons and blast demons in this raycasting shooter.",
            Category = GameCategory.Arcade,
            Icon = "??",
            ThumbnailColor = "#8b0000",
            PageType = typeof(Views.RetroFpsGamePage),
            IsFeatured = true,
            IsNew = true,
            PlayerCount = 1,
            Difficulty = "Hard"
        });
    }

    public IReadOnlyList<GameInfo> GetAllGames() => _games.AsReadOnly();

    public IReadOnlyList<GameInfo> GetGamesByCategory(string category) =>
        _games.Where(g => g.Category == category).ToList().AsReadOnly();

    public IReadOnlyList<GameInfo> GetFeaturedGames() =>
        _games.Where(g => g.IsFeatured).ToList().AsReadOnly();

    public IReadOnlyList<GameInfo> GetNewGames() =>
        _games.Where(g => g.IsNew).ToList().AsReadOnly();

    public GameInfo? GetGameById(string id) =>
        _games.FirstOrDefault(g => g.Id == id);

    public IReadOnlyList<string> GetCategories() =>
        _games.Select(g => g.Category).Distinct().OrderBy(c => c).ToList().AsReadOnly();
}
