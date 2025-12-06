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
        public string Icon { get; set; } = "??";
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
    /// Calculate all achievements for a player
    /// </summary>
    public static List<Achievement> CalculateAchievements(Guid playerId, List<Fixture> allFixtures, List<Player> allPlayers)
    {
        var achievements = new List<Achievement>();
        var playerFrames = new List<(DateTime date, bool won, bool eightBall, Guid? oppId, int oppRating)>();

        // Build frame history
        foreach (var fixture in allFixtures.OrderBy(f => f.Date))
        {
            foreach (var frame in fixture.Frames)
            {
                if (frame.HomePlayerId == playerId)
                {
                    var oppRating = 1000; // Simplified
                    playerFrames.Add((fixture.Date, frame.Winner == FrameWinner.Home, 
                                     frame.EightBall && frame.Winner == FrameWinner.Home, 
                                     frame.AwayPlayerId, oppRating));
                }
                else if (frame.AwayPlayerId == playerId)
                {
                    var oppRating = 1000;
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
            Icon = "??",
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
            Icon = "?",
            Tier = AchievementTier.Gold,
            IsUnlocked = longestStreak >= 10,
            UnlockedDate = longestStreak >= 10 ? DateTime.Now : null,
            Progress = Math.Min(longestStreak, 10),
            Target = 10
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
            Icon = "??",
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
            Icon = "??",
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
            Icon = "??",
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
            Icon = "??",
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
            Icon = "??",
            Tier = AchievementTier.Silver,
            IsUnlocked = totalFrames >= 100,
            UnlockedDate = totalFrames >= 100 ? DateTime.Now : null,
            Progress = Math.Min(totalFrames, 100),
            Target = 100
        });

        // Half Century Wins (50 wins)
        achievements.Add(new Achievement
        {
            Id = "half_century_wins",
            Name = "Half Century Wins",
            Description = "Win 50 frames",
            Icon = "??",
            Tier = AchievementTier.Gold,
            IsUnlocked = totalWins >= 50,
            UnlockedDate = totalWins >= 50 ? DateTime.Now : null,
            Progress = Math.Min(totalWins, 50),
            Target = 50
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
            Icon = "??",
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
            Icon = "??",
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
            Description = "Win all frames played in a single week",
            Icon = "?",
            Tier = AchievementTier.Platinum,
            IsUnlocked = perfectWeek,
            UnlockedDate = perfectWeek ? DateTime.Now : null,
            Progress = perfectWeek ? 1 : 0,
            Target = 1
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
            new Achievement { Id = "hot_streak", Name = "Hot Streak", Description = "Win 5 frames in a row", Icon = "??", Tier = AchievementTier.Bronze, Target = 5 },
            new Achievement { Id = "unbeatable", Name = "Unbeatable", Description = "Win 10 frames in a row", Icon = "?", Tier = AchievementTier.Gold, Target = 10 },
            
            // 8-Ball
            new Achievement { Id = "eightball_apprentice", Name = "8-Ball Apprentice", Description = "Clear 5 frames on the 8-ball", Icon = "??", Tier = AchievementTier.Bronze, Target = 5 },
            new Achievement { Id = "eightball_master", Name = "8-Ball Master", Description = "Clear 25 frames on the 8-ball", Icon = "??", Tier = AchievementTier.Gold, Target = 25 },
            new Achievement { Id = "eightball_legend", Name = "8-Ball Legend", Description = "Clear 50 frames on the 8-ball", Icon = "??", Tier = AchievementTier.Platinum, Target = 50 },
            
            // Milestones
            new Achievement { Id = "first_steps", Name = "First Steps", Description = "Play 10 competitive frames", Icon = "??", Tier = AchievementTier.Bronze, Target = 10 },
            new Achievement { Id = "century", Name = "Century", Description = "Play 100 competitive frames", Icon = "??", Tier = AchievementTier.Silver, Target = 100 },
            new Achievement { Id = "half_century_wins", Name = "Half Century Wins", Description = "Win 50 frames", Icon = "??", Tier = AchievementTier.Gold, Target = 50 },
            
            // Special
            new Achievement { Id = "comeback_king", Name = "Comeback King", Description = "Win a match after losing first 3 frames", Icon = "??", Tier = AchievementTier.Silver, Target = 1 },
            new Achievement { Id = "giant_killer", Name = "Giant Killer", Description = "Beat a player rated 200+ points higher", Icon = "??", Tier = AchievementTier.Gold, Target = 1 },
            new Achievement { Id = "perfect_week", Name = "Perfect Week", Description = "Win all frames played in a single week", Icon = "?", Tier = AchievementTier.Platinum, Target = 1 }
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
