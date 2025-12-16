using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Manages player achievements and badges
/// </summary>
public static class AchievementService
{
    public class Achievement
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "trophy";
        public AchievementTier Tier { get; set; }
        public bool IsUnlocked { get; set; }
        public DateTime? UnlockedDate { get; set; }
        public int Progress { get; set; }
        public int Target { get; set; }
    }

    public enum AchievementTier
    {
        Bronze,
        Silver,
        Gold,
        Platinum
    }

    /// <summary>
    /// Calculate all achievements for a player (single player ID)
    /// </summary>
    public static List<Achievement> CalculateAchievements(Guid playerId, List<Fixture> allFixtures, List<Player> allPlayers)
    {
        return CalculateAchievementsForMultiplePlayers(new List<Guid> { playerId }, allFixtures, allPlayers);
    }

    /// <summary>
    /// Calculate all achievements for a player across multiple seasons (multiple player IDs for same person)
    /// </summary>
    public static List<Achievement> CalculateAchievementsForMultiplePlayers(List<Guid> playerIds, List<Fixture> allFixtures, List<Player> allPlayers)
    {
        var achievements = new List<Achievement>();
        var playerIdSet = new HashSet<Guid>(playerIds);
        var playerFrames = new List<(DateTime date, bool won, bool eightBall, Guid? oppId, int oppRating)>();

        // Build frame history across all seasons
        foreach (var fixture in allFixtures.OrderBy(f => f.Date))
        {
            foreach (var frame in fixture.Frames)
            {
                if (frame.HomePlayerId.HasValue && playerIdSet.Contains(frame.HomePlayerId.Value))
                {
                    var oppRating = frame.AwayOppRating ?? 1000;
                    playerFrames.Add((fixture.Date, frame.Winner == FrameWinner.Home, 
                                     frame.EightBall && frame.Winner == FrameWinner.Home, 
                                     frame.AwayPlayerId, oppRating));
                }
                else if (frame.AwayPlayerId.HasValue && playerIdSet.Contains(frame.AwayPlayerId.Value))
                {
                    var oppRating = frame.HomeOppRating ?? 1000;
                    playerFrames.Add((fixture.Date, frame.Winner == FrameWinner.Away,
                                     frame.EightBall && frame.Winner == FrameWinner.Away,
                                     frame.HomePlayerId, oppRating));
                }
            }
        }

        if (!playerFrames.Any())
            return GetAllAchievements(); // Return locked achievements

        // Win Streaks
        achievements.AddRange(CalculateWinStreakAchievements(playerFrames));

        // 8-Ball Achievements
        achievements.AddRange(Calculate8BallAchievements(playerFrames));

        // Milestone Achievements
        achievements.AddRange(CalculateMilestoneAchievements(playerFrames));

        // Special Achievements
        achievements.AddRange(CalculateSpecialAchievements(playerFrames, allPlayers));

        // Career Achievements (for multi-season players)
        achievements.AddRange(CalculateCareerAchievements(playerFrames, playerIds.Count));

        return achievements;
    }

    private static List<Achievement> CalculateWinStreakAchievements(List<(DateTime date, bool won, bool eightBall, Guid? oppId, int oppRating)> frames)
    {
        var achievements = new List<Achievement>();
        int longestStreak = 0;
        int currentStreak = 0;

        foreach (var frame in frames)
        {
            if (frame.won)
            {
                currentStreak++;
                if (currentStreak > longestStreak)
                    longestStreak = currentStreak;
            }
            else
            {
                currentStreak = 0;
            }
        }

        // Hot Streak (5 wins in a row)
        achievements.Add(new Achievement
        {
            Id = "hot_streak",
            Name = "Hot Streak",
            Description = "Win 5 frames in a row",
            Icon = "fire",
            Tier = AchievementTier.Bronze,
            IsUnlocked = longestStreak >= 5,
            UnlockedDate = longestStreak >= 5 ? DateTime.Now : null,
            Progress = Math.Min(longestStreak, 5),
            Target = 5
        });

        // Unbeatable (10 wins in a row)
        achievements.Add(new Achievement
        {
            Id = "unbeatable",
            Name = "Unbeatable",
            Description = "Win 10 frames in a row",
            Icon = "star",
            Tier = AchievementTier.Gold,
            IsUnlocked = longestStreak >= 10,
            UnlockedDate = longestStreak >= 10 ? DateTime.Now : null,
            Progress = Math.Min(longestStreak, 10),
            Target = 10
        });

        // Legendary Streak (15 wins in a row)
        achievements.Add(new Achievement
        {
            Id = "legendary_streak",
            Name = "Legendary Streak",
            Description = "Win 15 frames in a row",
            Icon = "crown",
            Tier = AchievementTier.Platinum,
            IsUnlocked = longestStreak >= 15,
            UnlockedDate = longestStreak >= 15 ? DateTime.Now : null,
            Progress = Math.Min(longestStreak, 15),
            Target = 15
        });

        return achievements;
    }

    private static List<Achievement> Calculate8BallAchievements(List<(DateTime date, bool won, bool eightBall, Guid? oppId, int oppRating)> frames)
    {
        var achievements = new List<Achievement>();
        int total8Balls = frames.Count(f => f.eightBall);

        // 8-Ball Apprentice
        achievements.Add(new Achievement
        {
            Id = "eightball_apprentice",
            Name = "8-Ball Apprentice",
            Description = "Clear 5 frames on the 8-ball",
            Icon = "8ball",
            Tier = AchievementTier.Bronze,
            IsUnlocked = total8Balls >= 5,
            UnlockedDate = total8Balls >= 5 ? DateTime.Now : null,
            Progress = Math.Min(total8Balls, 5),
            Target = 5
        });

        // 8-Ball Master
        achievements.Add(new Achievement
        {
            Id = "eightball_master",
            Name = "8-Ball Master",
            Description = "Clear 25 frames on the 8-ball",
            Icon = "target",
            Tier = AchievementTier.Gold,
            IsUnlocked = total8Balls >= 25,
            UnlockedDate = total8Balls >= 25 ? DateTime.Now : null,
            Progress = Math.Min(total8Balls, 25),
            Target = 25
        });

        // 8-Ball Legend
        achievements.Add(new Achievement
        {
            Id = "eightball_legend",
            Name = "8-Ball Legend",
            Description = "Clear 50 frames on the 8-ball",
            Icon = "sparkle",
            Tier = AchievementTier.Platinum,
            IsUnlocked = total8Balls >= 50,
            UnlockedDate = total8Balls >= 50 ? DateTime.Now : null,
            Progress = Math.Min(total8Balls, 50),
            Target = 50
        });

        return achievements;
    }

    private static List<Achievement> CalculateMilestoneAchievements(List<(DateTime date, bool won, bool eightBall, Guid? oppId, int oppRating)> frames)
    {
        var achievements = new List<Achievement>();
        int totalFrames = frames.Count;
        int totalWins = frames.Count(f => f.won);

        // First Steps (Play 10 frames)
        achievements.Add(new Achievement
        {
            Id = "first_steps",
            Name = "First Steps",
            Description = "Play 10 competitive frames",
            Icon = "footprints",
            Tier = AchievementTier.Bronze,
            IsUnlocked = totalFrames >= 10,
            UnlockedDate = totalFrames >= 10 ? DateTime.Now : null,
            Progress = Math.Min(totalFrames, 10),
            Target = 10
        });

        // Century (Play 100 frames)
        achievements.Add(new Achievement
        {
            Id = "century",
            Name = "Century",
            Description = "Play 100 competitive frames",
            Icon = "hundred",
            Tier = AchievementTier.Silver,
            IsUnlocked = totalFrames >= 100,
            UnlockedDate = totalFrames >= 100 ? DateTime.Now : null,
            Progress = Math.Min(totalFrames, 100),
            Target = 100
        });

        // 500 Club (Play 500 frames)
        achievements.Add(new Achievement
        {
            Id = "500_club",
            Name = "500 Club",
            Description = "Play 500 competitive frames",
            Icon = "medal",
            Tier = AchievementTier.Gold,
            IsUnlocked = totalFrames >= 500,
            UnlockedDate = totalFrames >= 500 ? DateTime.Now : null,
            Progress = Math.Min(totalFrames, 500),
            Target = 500
        });

        // Half Century Wins (50 wins)
        achievements.Add(new Achievement
        {
            Id = "half_century_wins",
            Name = "Half Century Wins",
            Description = "Win 50 frames",
            Icon = "trophy",
            Tier = AchievementTier.Silver,
            IsUnlocked = totalWins >= 50,
            UnlockedDate = totalWins >= 50 ? DateTime.Now : null,
            Progress = Math.Min(totalWins, 50),
            Target = 50
        });

        // Century Wins (100 wins)
        achievements.Add(new Achievement
        {
            Id = "century_wins",
            Name = "Century Wins",
            Description = "Win 100 frames",
            Icon = "star_gold",
            Tier = AchievementTier.Gold,
            IsUnlocked = totalWins >= 100,
            UnlockedDate = totalWins >= 100 ? DateTime.Now : null,
            Progress = Math.Min(totalWins, 100),
            Target = 100
        });

        // 250 Wins
        achievements.Add(new Achievement
        {
            Id = "250_wins",
            Name = "Win Machine",
            Description = "Win 250 frames",
            Icon = "diamond",
            Tier = AchievementTier.Platinum,
            IsUnlocked = totalWins >= 250,
            UnlockedDate = totalWins >= 250 ? DateTime.Now : null,
            Progress = Math.Min(totalWins, 250),
            Target = 250
        });

        return achievements;
    }

    private static List<Achievement> CalculateSpecialAchievements(
        List<(DateTime date, bool won, bool eightBall, Guid? oppId, int oppRating)> frames,
        List<Player> allPlayers)
    {
        var achievements = new List<Achievement>();

        // Comeback King (win after losing first 3 frames of a match)
        var matchGroups = frames.GroupBy(f => f.date);
        int comebacks = 0;
        foreach (var match in matchGroups)
        {
            var matchFrames = match.OrderBy(f => f.date).ToList();
            if (matchFrames.Count >= 4)
            {
                var first3 = matchFrames.Take(3);
                if (first3.All(f => !f.won))
                {
                    if (matchFrames.Skip(3).Count(f => f.won) > matchFrames.Skip(3).Count(f => !f.won))
                        comebacks++;
                }
            }
        }

        achievements.Add(new Achievement
        {
            Id = "comeback_king",
            Name = "Comeback King",
            Description = "Win a match after losing first 3 frames",
            Icon = "superhero",
            Tier = AchievementTier.Silver,
            IsUnlocked = comebacks > 0,
            UnlockedDate = comebacks > 0 ? DateTime.Now : null,
            Progress = Math.Min(comebacks, 1),
            Target = 1
        });

        // Giant Killer (beat player rated 200+ higher)
        int giantKills = frames.Count(f => f.won && f.oppRating - 1000 > 200);
        achievements.Add(new Achievement
        {
            Id = "giant_killer",
            Name = "Giant Killer",
            Description = "Beat a player rated 200+ points higher",
            Icon = "sword",
            Tier = AchievementTier.Gold,
            IsUnlocked = giantKills > 0,
            UnlockedDate = giantKills > 0 ? DateTime.Now : null,
            Progress = Math.Min(giantKills, 1),
            Target = 1
        });

        // Perfect Week (win all frames in one week)
        var weekGroups = frames.GroupBy(f => System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
            f.date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday));
        bool perfectWeek = weekGroups.Any(g => g.All(f => f.won) && g.Count() >= 3);

        achievements.Add(new Achievement
        {
            Id = "perfect_week",
            Name = "Perfect Week",
            Description = "Win all frames played in a single week (min 3)",
            Icon = "sparkles",
            Tier = AchievementTier.Platinum,
            IsUnlocked = perfectWeek,
            UnlockedDate = perfectWeek ? DateTime.Now : null,
            Progress = perfectWeek ? 1 : 0,
            Target = 1
        });

        return achievements;
    }

    private static List<Achievement> CalculateCareerAchievements(
        List<(DateTime date, bool won, bool eightBall, Guid? oppId, int oppRating)> frames,
        int seasonCount)
    {
        var achievements = new List<Achievement>();

        // Veteran (play in 3+ seasons)
        achievements.Add(new Achievement
        {
            Id = "veteran",
            Name = "League Veteran",
            Description = "Play in 3 different seasons",
            Icon = "ribbon",
            Tier = AchievementTier.Silver,
            IsUnlocked = seasonCount >= 3,
            UnlockedDate = seasonCount >= 3 ? DateTime.Now : null,
            Progress = Math.Min(seasonCount, 3),
            Target = 3
        });

        // Legend (play in 5+ seasons)
        achievements.Add(new Achievement
        {
            Id = "legend",
            Name = "League Legend",
            Description = "Play in 5 different seasons",
            Icon = "star_shine",
            Tier = AchievementTier.Gold,
            IsUnlocked = seasonCount >= 5,
            UnlockedDate = seasonCount >= 5 ? DateTime.Now : null,
            Progress = Math.Min(seasonCount, 5),
            Target = 5
        });

        // Icon (play in 10+ seasons)
        achievements.Add(new Achievement
        {
            Id = "icon",
            Name = "League Icon",
            Description = "Play in 10 different seasons",
            Icon = "crown_gold",
            Tier = AchievementTier.Platinum,
            IsUnlocked = seasonCount >= 10,
            UnlockedDate = seasonCount >= 10 ? DateTime.Now : null,
            Progress = Math.Min(seasonCount, 10),
            Target = 10
        });

        // Long-term consistency: 60%+ win rate over career (min 100 frames)
        double winRate = frames.Any() ? (double)frames.Count(f => f.won) / frames.Count * 100 : 0;
        bool isConsistent = frames.Count >= 100 && winRate >= 60;

        achievements.Add(new Achievement
        {
            Id = "consistent_performer",
            Name = "Consistent Performer",
            Description = "Maintain 60%+ win rate over 100+ frames",
            Icon = "chart_up",
            Tier = AchievementTier.Gold,
            IsUnlocked = isConsistent,
            UnlockedDate = isConsistent ? DateTime.Now : null,
            Progress = frames.Count >= 100 ? (int)winRate : 0,
            Target = 60
        });

        return achievements;
    }

    /// <summary>
    /// Get all possible achievements (locked and unlocked)
    /// </summary>
    public static List<Achievement> GetAllAchievements()
    {
        return new List<Achievement>
        {
            // Win Streaks
            new Achievement { Id = "hot_streak", Name = "Hot Streak", Description = "Win 5 frames in a row", Icon = "fire", Tier = AchievementTier.Bronze, Target = 5 },
            new Achievement { Id = "unbeatable", Name = "Unbeatable", Description = "Win 10 frames in a row", Icon = "star", Tier = AchievementTier.Gold, Target = 10 },
            new Achievement { Id = "legendary_streak", Name = "Legendary Streak", Description = "Win 15 frames in a row", Icon = "crown", Tier = AchievementTier.Platinum, Target = 15 },
            
            // 8-Ball
            new Achievement { Id = "eightball_apprentice", Name = "8-Ball Apprentice", Description = "Clear 5 frames on the 8-ball", Icon = "8ball", Tier = AchievementTier.Bronze, Target = 5 },
            new Achievement { Id = "eightball_master", Name = "8-Ball Master", Description = "Clear 25 frames on the 8-ball", Icon = "target", Tier = AchievementTier.Gold, Target = 25 },
            new Achievement { Id = "eightball_legend", Name = "8-Ball Legend", Description = "Clear 50 frames on the 8-ball", Icon = "sparkle", Tier = AchievementTier.Platinum, Target = 50 },
            
            // Milestones
            new Achievement { Id = "first_steps", Name = "First Steps", Description = "Play 10 competitive frames", Icon = "footprints", Tier = AchievementTier.Bronze, Target = 10 },
            new Achievement { Id = "century", Name = "Century", Description = "Play 100 competitive frames", Icon = "hundred", Tier = AchievementTier.Silver, Target = 100 },
            new Achievement { Id = "500_club", Name = "500 Club", Description = "Play 500 competitive frames", Icon = "medal", Tier = AchievementTier.Gold, Target = 500 },
            new Achievement { Id = "half_century_wins", Name = "Half Century Wins", Description = "Win 50 frames", Icon = "trophy", Tier = AchievementTier.Silver, Target = 50 },
            new Achievement { Id = "century_wins", Name = "Century Wins", Description = "Win 100 frames", Icon = "star_gold", Tier = AchievementTier.Gold, Target = 100 },
            new Achievement { Id = "250_wins", Name = "Win Machine", Description = "Win 250 frames", Icon = "diamond", Tier = AchievementTier.Platinum, Target = 250 },
            
            // Special
            new Achievement { Id = "comeback_king", Name = "Comeback King", Description = "Win a match after losing first 3 frames", Icon = "superhero", Tier = AchievementTier.Silver, Target = 1 },
            new Achievement { Id = "giant_killer", Name = "Giant Killer", Description = "Beat a player rated 200+ points higher", Icon = "sword", Tier = AchievementTier.Gold, Target = 1 },
            new Achievement { Id = "perfect_week", Name = "Perfect Week", Description = "Win all frames played in a single week (min 3)", Icon = "sparkles", Tier = AchievementTier.Platinum, Target = 1 },
            
            // Career
            new Achievement { Id = "veteran", Name = "League Veteran", Description = "Play in 3 different seasons", Icon = "ribbon", Tier = AchievementTier.Silver, Target = 3 },
            new Achievement { Id = "legend", Name = "League Legend", Description = "Play in 5 different seasons", Icon = "star_shine", Tier = AchievementTier.Gold, Target = 5 },
            new Achievement { Id = "icon", Name = "League Icon", Description = "Play in 10 different seasons", Icon = "crown_gold", Tier = AchievementTier.Platinum, Target = 10 },
            new Achievement { Id = "consistent_performer", Name = "Consistent Performer", Description = "Maintain 60%+ win rate over 100+ frames", Icon = "chart_up", Tier = AchievementTier.Gold, Target = 60 }
        };
    }

    /// <summary>
    /// Get the display icon for an achievement (emoji-based)
    /// </summary>
    public static string GetIconDisplay(string iconId)
    {
        // Use Unicode escape sequences for emojis to ensure proper rendering
        return iconId switch
        {
            "fire" => "\U0001F525",        // ??
            "star" => "\u2B50",            // ?
            "crown" => "\U0001F451",       // ??
            "8ball" => "\U0001F3B1",       // ??
            "target" => "\U0001F3AF",      // ??
            "sparkle" => "\u2728",         // ?
            "footprints" => "\U0001F463",  // ??
            "hundred" => "\U0001F4AF",     // ??
            "medal" => "\U0001F3C5",       // ??
            "trophy" => "\U0001F3C6",      // ??
            "star_gold" => "\U0001F31F",   // ??
            "diamond" => "\U0001F48E",     // ??
            "superhero" => "\U0001F9B8",   // ??
            "sword" => "\u2694\uFE0F",     // ??
            "sparkles" => "\U0001F320",    // ??
            "ribbon" => "\U0001F396\uFE0F", // ???
            "star_shine" => "\U0001F31F",  // ??
            "crown_gold" => "\U0001F451",  // ??
            "chart_up" => "\U0001F4C8",    // ??
            _ => "\U0001F3C5"              // ??
        };
    }

    /// <summary>
    /// Get achievement tier color
    /// </summary>
    public static Microsoft.Maui.Graphics.Color GetTierColor(AchievementTier tier)
    {
        return tier switch
        {
            AchievementTier.Bronze => Microsoft.Maui.Graphics.Color.FromArgb("#CD7F32"),
            AchievementTier.Silver => Microsoft.Maui.Graphics.Color.FromArgb("#C0C0C0"),
            AchievementTier.Gold => Microsoft.Maui.Graphics.Color.FromArgb("#FFD700"),
            AchievementTier.Platinum => Microsoft.Maui.Graphics.Color.FromArgb("#E5E4E2"),
            _ => Microsoft.Maui.Graphics.Colors.Gray
        };
    }
}
