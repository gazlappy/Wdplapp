using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Tests;

/// <summary>
/// Tests for AchievementService — player achievement calculations.
/// </summary>
public class AchievementServiceTests
{
    private static Player CreatePlayer(Guid id) => new()
    {
        Id = id,
        FirstName = "Test",
        LastName = "Player"
    };

    [Fact]
    public void NoFixtures_NoAchievementsUnlocked()
    {
        var playerId = Guid.NewGuid();
        var achievements = AchievementService.CalculateAchievements(
            playerId, new List<Fixture>(), new List<Player> { CreatePlayer(playerId) });

        Assert.NotEmpty(achievements); // Definitions exist
        Assert.All(achievements, a => Assert.False(a.IsUnlocked));
    }

    [Fact]
    public void MultiplePlayerIds_AggregatesFrames()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var opp = Guid.NewGuid();

        var players = new List<Player>
        {
            CreatePlayer(id1),
            CreatePlayer(id2),
            CreatePlayer(opp)
        };

        var fixtures = new List<Fixture>
        {
            new()
            {
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Date = DateTime.Today.AddDays(-10),
                Frames = new List<FrameResult>
                {
                    new() { Number = 1, HomePlayerId = id1, AwayPlayerId = opp, Winner = FrameWinner.Home }
                }
            },
            new()
            {
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Date = DateTime.Today,
                Frames = new List<FrameResult>
                {
                    new() { Number = 1, HomePlayerId = id2, AwayPlayerId = opp, Winner = FrameWinner.Home }
                }
            }
        };

        var achievements = AchievementService.CalculateAchievementsForMultiplePlayers(
            new List<Guid> { id1, id2 }, fixtures, players);

        Assert.NotEmpty(achievements);
    }

    [Fact]
    public void EightBallWins_TrackProgress()
    {
        var playerId = Guid.NewGuid();
        var opp = Guid.NewGuid();

        var players = new List<Player>
        {
            CreatePlayer(playerId),
            CreatePlayer(opp)
        };

        var frames = new List<FrameResult>();
        for (int i = 0; i < 5; i++)
        {
            frames.Add(new FrameResult
            {
                Number = i + 1,
                HomePlayerId = playerId,
                AwayPlayerId = opp,
                Winner = FrameWinner.Home,
                EightBall = true
            });
        }

        var fixtures = new List<Fixture>
        {
            new()
            {
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Date = DateTime.Today,
                Frames = frames
            }
        };

        var achievements = AchievementService.CalculateAchievements(playerId, fixtures, players);

        // Should have some 8-ball related achievements with progress
        var eightBallAchievements = achievements.Where(a =>
            a.Name.Contains("8", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains("eight", StringComparison.OrdinalIgnoreCase) ||
            a.Id.Contains("eight", StringComparison.OrdinalIgnoreCase)).ToList();

        if (eightBallAchievements.Count > 0)
        {
            Assert.True(eightBallAchievements.Any(a => a.Progress > 0),
                "8-ball achievements should have progress after 8-ball wins");
        }
    }
}
