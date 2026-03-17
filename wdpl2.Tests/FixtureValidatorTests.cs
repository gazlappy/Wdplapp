using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Tests;

/// <summary>
/// Tests for FixtureValidator — validates fixture data before saving.
/// </summary>
public class FixtureValidatorTests
{
    private static AppSettings DefaultSettings => new()
    {
        DefaultFramesPerMatch = 15,
        MaxFramesPerPlayer = 3
    };

    [Fact]
    public void NullFixture_IsInvalid()
    {
        var result = FixtureValidator.ValidateFixture(null!, DefaultSettings);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("null"));
    }

    [Fact]
    public void ValidFixture_NoFrames_IsValid()
    {
        var fixture = new Fixture
        {
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Date = DateTime.Today
        };

        var result = FixtureValidator.ValidateFixture(fixture, DefaultSettings);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void MissingHomeTeam_IsInvalid()
    {
        var fixture = new Fixture
        {
            HomeTeamId = Guid.Empty,
            AwayTeamId = Guid.NewGuid()
        };

        var result = FixtureValidator.ValidateFixture(fixture, DefaultSettings);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Home team"));
    }

    [Fact]
    public void MissingAwayTeam_IsInvalid()
    {
        var fixture = new Fixture
        {
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.Empty
        };

        var result = FixtureValidator.ValidateFixture(fixture, DefaultSettings);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Away team"));
    }

    [Fact]
    public void SameTeam_IsInvalid()
    {
        var teamId = Guid.NewGuid();
        var fixture = new Fixture
        {
            HomeTeamId = teamId,
            AwayTeamId = teamId
        };

        var result = FixtureValidator.ValidateFixture(fixture, DefaultSettings);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("same"));
    }

    [Fact]
    public void ValidFrames_NoWarnings()
    {
        var pHome = Guid.NewGuid();
        var pAway = Guid.NewGuid();

        var fixture = new Fixture
        {
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Date = DateTime.Today.AddDays(-1),
            Frames = new List<FrameResult>
            {
                new() { Number = 1, HomePlayerId = pHome, AwayPlayerId = pAway, Winner = FrameWinner.Home },
                new() { Number = 2, HomePlayerId = pHome, AwayPlayerId = pAway, Winner = FrameWinner.Away },
                new() { Number = 3, HomePlayerId = pHome, AwayPlayerId = pAway, Winner = FrameWinner.Home }
            }
        };

        var result = FixtureValidator.ValidateFixture(fixture, DefaultSettings);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void FutureDate_WithResults_HasWarning()
    {
        var fixture = new Fixture
        {
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Date = DateTime.Now.AddDays(7),
            Frames = new List<FrameResult>
            {
                new() { Number = 1, HomePlayerId = Guid.NewGuid(), AwayPlayerId = Guid.NewGuid(), Winner = FrameWinner.Home }
            }
        };

        var result = FixtureValidator.ValidateFixture(fixture, DefaultSettings);
        Assert.Contains(result.Warnings, w => w.Contains("Future"));
    }
}
