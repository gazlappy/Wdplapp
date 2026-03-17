using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Tests;

/// <summary>
/// Tests for RatingCalculator — the VBA-style player rating algorithm.
/// </summary>
public class RatingCalculatorTests
{
    private static AppSettings DefaultSettings => new()
    {
        RatingStartValue = 1000,
        RatingWeighting = 220,
        RatingsBias = 4,
        WinFactor = 1.25,
        LossFactor = 0.75,
        EightBallFactor = 1.35,
        UseEightBallFactor = true
    };

    private static Player CreatePlayer(Guid id, string first, string last, Guid teamId) => new()
    {
        Id = id,
        FirstName = first,
        LastName = last,
        TeamId = teamId
    };

    private static Team CreateTeam(Guid id, string name) => new()
    {
        Id = id,
        Name = name
    };

    [Fact]
    public void EmptyFixtures_ReturnsEmptyRatings()
    {
        var result = RatingCalculator.CalculateAllRatings(
            new List<Fixture>(),
            new List<Player>(),
            new List<Team>(),
            DefaultSettings,
            DateTime.Today);

        Assert.Empty(result);
    }

    [Fact]
    public void SingleFrame_Win_RatingAboveStart()
    {
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();

        var players = new List<Player>
        {
            CreatePlayer(playerA, "Alice", "Smith", teamA),
            CreatePlayer(playerB, "Bob", "Jones", teamB)
        };

        var teams = new List<Team>
        {
            CreateTeam(teamA, "Team A"),
            CreateTeam(teamB, "Team B")
        };

        var fixture = new Fixture
        {
            HomeTeamId = teamA,
            AwayTeamId = teamB,
            Date = DateTime.Today,
            Frames = new List<FrameResult>
            {
                new() { Number = 1, HomePlayerId = playerA, AwayPlayerId = playerB, Winner = FrameWinner.Home }
            }
        };

        var result = RatingCalculator.CalculateAllRatings(
            new List<Fixture> { fixture }, players, teams, DefaultSettings, DateTime.Today);

        Assert.Equal(2, result.Count);
        Assert.True(result[playerA].Rating > DefaultSettings.RatingStartValue,
            "Winner should have rating above start");
        Assert.True(result[playerB].Rating < DefaultSettings.RatingStartValue,
            "Loser should have rating below start");
    }

    [Fact]
    public void WinAndLoss_StatsAreCorrect()
    {
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var pA = Guid.NewGuid();
        var pB = Guid.NewGuid();

        var players = new List<Player>
        {
            CreatePlayer(pA, "A", "Player", teamA),
            CreatePlayer(pB, "B", "Player", teamB)
        };

        var teams = new List<Team>
        {
            CreateTeam(teamA, "Team A"),
            CreateTeam(teamB, "Team B")
        };

        var fixture = new Fixture
        {
            HomeTeamId = teamA,
            AwayTeamId = teamB,
            Date = DateTime.Today,
            Frames = new List<FrameResult>
            {
                new() { Number = 1, HomePlayerId = pA, AwayPlayerId = pB, Winner = FrameWinner.Home },
                new() { Number = 2, HomePlayerId = pA, AwayPlayerId = pB, Winner = FrameWinner.Away },
                new() { Number = 3, HomePlayerId = pA, AwayPlayerId = pB, Winner = FrameWinner.Home }
            }
        };

        var result = RatingCalculator.CalculateAllRatings(
            new List<Fixture> { fixture }, players, teams, DefaultSettings, DateTime.Today);

        Assert.Equal(3, result[pA].Played);
        Assert.Equal(2, result[pA].Wins);
        Assert.Equal(1, result[pA].Losses);
        Assert.Equal(3, result[pB].Played);
        Assert.Equal(1, result[pB].Wins);
        Assert.Equal(2, result[pB].Losses);
    }

    [Fact]
    public void EightBall_IncreasesWinRating()
    {
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var pNormal = Guid.NewGuid();
        var pEight = Guid.NewGuid();
        var pOpp1 = Guid.NewGuid();
        var pOpp2 = Guid.NewGuid();

        var players = new List<Player>
        {
            CreatePlayer(pNormal, "Normal", "Win", teamA),
            CreatePlayer(pEight, "Eight", "Ball", teamA),
            CreatePlayer(pOpp1, "Opp", "One", teamB),
            CreatePlayer(pOpp2, "Opp", "Two", teamB)
        };

        var teams = new List<Team>
        {
            CreateTeam(teamA, "Team A"),
            CreateTeam(teamB, "Team B")
        };

        var fixtureNormal = new Fixture
        {
            HomeTeamId = teamA,
            AwayTeamId = teamB,
            Date = DateTime.Today,
            Frames = new List<FrameResult>
            {
                new() { Number = 1, HomePlayerId = pNormal, AwayPlayerId = pOpp1, Winner = FrameWinner.Home, EightBall = false }
            }
        };

        var fixtureEight = new Fixture
        {
            HomeTeamId = teamA,
            AwayTeamId = teamB,
            Date = DateTime.Today,
            Frames = new List<FrameResult>
            {
                new() { Number = 1, HomePlayerId = pEight, AwayPlayerId = pOpp2, Winner = FrameWinner.Home, EightBall = true }
            }
        };

        var result = RatingCalculator.CalculateAllRatings(
            new List<Fixture> { fixtureNormal, fixtureEight }, players, teams, DefaultSettings, DateTime.Today);

        Assert.True(result[pEight].Rating > result[pNormal].Rating,
            "8-ball win should produce higher rating than normal win");
        Assert.Equal(1, result[pEight].EightBalls);
        Assert.Equal(0, result[pNormal].EightBalls);
    }

    [Fact]
    public void VoidPlayer_FramesAreSkipped()
    {
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var pA = Guid.NewGuid();

        var players = new List<Player>
        {
            CreatePlayer(pA, "Real", "Player", teamA)
        };

        var teams = new List<Team>
        {
            CreateTeam(teamA, "Team A"),
            CreateTeam(teamB, "Team B")
        };

        var fixture = new Fixture
        {
            HomeTeamId = teamA,
            AwayTeamId = teamB,
            Date = DateTime.Today,
            Frames = new List<FrameResult>
            {
                new() { Number = 1, HomePlayerId = pA, AwayPlayerId = FrameResult.VoidPlayerId, Winner = FrameWinner.Home }
            }
        };

        var result = RatingCalculator.CalculateAllRatings(
            new List<Fixture> { fixture }, players, teams, DefaultSettings, DateTime.Today);

        // Void frames should be skipped entirely
        Assert.False(result.ContainsKey(pA) && result[pA].Played > 0,
            "Frames with void players should not count");
    }

    [Fact]
    public void DivisionFilter_OnlyReturnsFilteredPlayers()
    {
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var divA = Guid.NewGuid();
        var divB = Guid.NewGuid();
        var pA = Guid.NewGuid();
        var pB = Guid.NewGuid();

        var players = new List<Player>
        {
            CreatePlayer(pA, "A", "P", teamA),
            CreatePlayer(pB, "B", "P", teamB)
        };

        var teams = new List<Team>
        {
            new() { Id = teamA, Name = "Team A", DivisionId = divA },
            new() { Id = teamB, Name = "Team B", DivisionId = divB }
        };

        var fixture = new Fixture
        {
            HomeTeamId = teamA,
            AwayTeamId = teamB,
            Date = DateTime.Today,
            Frames = new List<FrameResult>
            {
                new() { Number = 1, HomePlayerId = pA, AwayPlayerId = pB, Winner = FrameWinner.Home }
            }
        };

        var result = RatingCalculator.CalculateAllRatings(
            new List<Fixture> { fixture }, players, teams, DefaultSettings, DateTime.Today,
            divisionFilter: new HashSet<Guid> { divA });

        Assert.True(result.ContainsKey(pA));
        Assert.False(result.ContainsKey(pB), "Player from filtered-out division should not appear");
    }
}
