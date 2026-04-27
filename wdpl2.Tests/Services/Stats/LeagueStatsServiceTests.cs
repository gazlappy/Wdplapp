using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for LeagueStatsService — player of month, venue stats, season recap calculations.
/// </summary>
public class LeagueStatsServiceTests
{
    private static Player CreatePlayer(Guid id, string firstName = "Test", string lastName = "Player", Guid? teamId = null) => new()
    {
        Id = id,
        FirstName = firstName,
        LastName = lastName,
        TeamId = teamId
    };

    private static Team CreateTeam(Guid id, string name = "Test Team") => new()
    {
        Id = id,
        Name = name
    };

    private static Venue CreateVenue(Guid id, string name = "Test Venue") => new()
    {
        Id = id,
        Name = name
    };

    private static FrameResult CreateFrame(Guid? homePlayerId, Guid? awayPlayerId, FrameWinner winner, bool eightBall = false) => new()
    {
        HomePlayerId = homePlayerId,
        AwayPlayerId = awayPlayerId,
        Winner = winner,
        EightBall = eightBall
    };

    // ========== CalculatePlayersOfMonth Tests ==========

    [Fact]
    public void CalculatePlayersOfMonth_EmptyFixtures_ReturnsEmptyList()
    {
        var fixtures = new List<Fixture>();
        var players = new List<Player>();
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculatePlayersOfMonth_FixturesWithNoFrames_ReturnsEmptyList()
    {
        var fixtures = new List<Fixture>
        {
            new() { Date = new DateTime(2024, 1, 15) }
        };
        var players = new List<Player>();
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculatePlayersOfMonth_PlayerWithLessThanThreeFrames_NotIncluded()
    {
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(playerId) };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculatePlayersOfMonth_PlayerWithThreeFrames_Included()
    {
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(playerId, "John", "Doe") };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal(playerId, result[0].PlayerId);
        Assert.Equal("John Doe", result[0].PlayerName);
        Assert.Equal(3, result[0].FramesPlayed);
        Assert.Equal(3, result[0].FramesWon);
        Assert.Equal(100, result[0].WinPercentage);
    }

    [Fact]
    public void CalculatePlayersOfMonth_MultipleMonths_CreatesMultipleWinners()
    {
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home)
                }
            },
            new()
            {
                Date = new DateTime(2024, 2, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player>
        {
            CreatePlayer(player1, "John", "Doe"),
            CreatePlayer(player2, "Jane", "Smith")
        };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Month);
        Assert.Equal(2024, result[0].Year);
        Assert.Equal(2, result[1].Month);
        Assert.Equal(2024, result[1].Year);
    }

    [Fact]
    public void CalculatePlayersOfMonth_VoidPlayer_Excluded()
    {
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, FrameResult.VoidPlayerId, FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(playerId) };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal(3, result[0].FramesPlayed);
    }

    [Fact]
    public void CalculatePlayersOfMonth_EightBallWins_CountedCorrectly()
    {
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home, true),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home, true),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home, false)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(playerId) };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal(2, result[0].EightBalls);
    }

    [Fact]
    public void CalculatePlayersOfMonth_EightBallButLost_NotCounted()
    {
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home, false),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Away, true),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home, false)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(playerId) };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal(0, result[0].EightBalls);
    }

    [Fact]
    public void CalculatePlayersOfMonth_AwayPlayerStats_CalculatedCorrectly()
    {
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), playerId, FrameWinner.Away),
                    CreateFrame(Guid.NewGuid(), playerId, FrameWinner.Away),
                    CreateFrame(Guid.NewGuid(), playerId, FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(playerId) };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal(3, result[0].FramesPlayed);
        Assert.Equal(2, result[0].FramesWon);
    }

    [Fact]
    public void CalculatePlayersOfMonth_AwayPlayerEightBall_CountedCorrectly()
    {
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), playerId, FrameWinner.Away, true),
                    CreateFrame(Guid.NewGuid(), playerId, FrameWinner.Away, false),
                    CreateFrame(Guid.NewGuid(), playerId, FrameWinner.Away, true)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(playerId) };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal(2, result[0].EightBalls);
    }

    [Fact]
    public void CalculatePlayersOfMonth_BestPlayerByWinPercentage_Selected()
    {
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Away)
                }
            }
        };
        var players = new List<Player>
        {
            CreatePlayer(player1, "Winner", "Best"),
            CreatePlayer(player2, "Second", "Place")
        };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal(player1, result[0].PlayerId);
        Assert.Equal(100, result[0].WinPercentage);
    }

    [Fact]
    public void CalculatePlayersOfMonth_TieOnWinPercentage_BreaksByTotalWins()
    {
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player>
        {
            CreatePlayer(player1, "More", "Wins"),
            CreatePlayer(player2, "Fewer", "Wins")
        };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal(player1, result[0].PlayerId);
        Assert.Equal(4, result[0].FramesWon);
    }

    [Fact]
    public void CalculatePlayersOfMonth_TieOnWinsAndPercentage_BreaksByEightBalls()
    {
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home, true),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home, true),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home, false),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home, false),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home, false),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home, false)
                }
            }
        };
        var players = new List<Player>
        {
            CreatePlayer(player1, "More", "EightBalls"),
            CreatePlayer(player2, "Fewer", "EightBalls")
        };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal(player1, result[0].PlayerId);
        Assert.Equal(2, result[0].EightBalls);
    }

    [Fact]
    public void CalculatePlayersOfMonth_PlayerWithTeam_IncludesTeamName()
    {
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(playerId, "John", "Doe", teamId) };
        var teams = new List<Team> { CreateTeam(teamId, "Dream Team") };

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal("Dream Team", result[0].TeamName);
    }

    [Fact]
    public void CalculatePlayersOfMonth_PlayerWithoutTeam_EmptyTeamName()
    {
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(playerId) };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal("", result[0].TeamName);
    }

    [Fact]
    public void CalculatePlayersOfMonth_PlayerNotInLookup_NotIncluded()
    {
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player>();
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculatePlayersOfMonth_MonthName_FormattedCorrectly()
    {
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 3, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(playerId) };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal("March 2024", result[0].MonthName);
    }

    [Fact]
    public void CalculatePlayersOfMonth_MonthsOrderedChronologically_OldestFirst()
    {
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 5, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home)
                }
            },
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player>
        {
            CreatePlayer(player1),
            CreatePlayer(player2)
        };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Month);
        Assert.Equal(5, result[1].Month);
    }

    // ========== VenueStats.HomeWinPercentage Tests ==========

    [Fact]
    public void HomeWinPercentage_NoMatches_ReturnsZero()
    {
        var stats = new LeagueStatsService.VenueStats
        {
            TotalMatches = 0,
            HomeWins = 0
        };

        var result = stats.HomeWinPercentage;

        Assert.Equal(0, result);
    }

    [Fact]
    public void HomeWinPercentage_AllHomeWins_Returns100()
    {
        var stats = new LeagueStatsService.VenueStats
        {
            TotalMatches = 10,
            HomeWins = 10
        };

        var result = stats.HomeWinPercentage;

        Assert.Equal(100, result);
    }

    [Fact]
    public void HomeWinPercentage_HalfHomeWins_Returns50()
    {
        var stats = new LeagueStatsService.VenueStats
        {
            TotalMatches = 10,
            HomeWins = 5
        };

        var result = stats.HomeWinPercentage;

        Assert.Equal(50, result);
    }

    [Fact]
    public void HomeWinPercentage_CalculatesCorrectPercentage()
    {
        var stats = new LeagueStatsService.VenueStats
        {
            TotalMatches = 20,
            HomeWins = 15
        };

        var result = stats.HomeWinPercentage;

        Assert.Equal(75, result);
    }

    // ========== CalculateVenueStats Tests ==========

    [Fact]
    public void CalculateVenueStats_EmptyFixtures_ReturnsEmptyList()
    {
        var fixtures = new List<Fixture>();
        var venues = new List<Venue>();

        var result = LeagueStatsService.CalculateVenueStats(fixtures, venues);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculateVenueStats_FixturesWithoutVenue_Excluded()
    {
        var fixtures = new List<Fixture>
        {
            new()
            {
                VenueId = null,
                Frames = new List<FrameResult> { CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home) }
            }
        };
        var venues = new List<Venue>();

        var result = LeagueStatsService.CalculateVenueStats(fixtures, venues);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculateVenueStats_FixturesWithoutFrames_Excluded()
    {
        var venueId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                VenueId = venueId,
                Frames = new List<FrameResult>()
            }
        };
        var venues = new List<Venue> { CreateVenue(venueId) };

        var result = LeagueStatsService.CalculateVenueStats(fixtures, venues);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculateVenueStats_SingleMatch_CalculatesCorrectly()
    {
        var venueId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                VenueId = venueId,
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Away)
                }
            }
        };
        var venues = new List<Venue> { CreateVenue(venueId, "Main Hall") };

        var result = LeagueStatsService.CalculateVenueStats(fixtures, venues);

        Assert.Single(result);
        Assert.Equal(venueId, result[0].VenueId);
        Assert.Equal("Main Hall", result[0].VenueName);
        Assert.Equal(1, result[0].TotalMatches);
        Assert.Equal(3, result[0].TotalFrames);
        Assert.Equal(2, result[0].HomeFrames);
        Assert.Equal(1, result[0].AwayFrames);
        Assert.Equal(1, result[0].HomeWins);
        Assert.Equal(0, result[0].AwayWins);
        Assert.Equal(0, result[0].Draws);
    }

    [Fact]
    public void CalculateVenueStats_HomeWin_CountedCorrectly()
    {
        var venueId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                VenueId = venueId,
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Away)
                }
            }
        };
        var venues = new List<Venue> { CreateVenue(venueId) };

        var result = LeagueStatsService.CalculateVenueStats(fixtures, venues);

        Assert.Single(result);
        Assert.Equal(1, result[0].HomeWins);
    }

    [Fact]
    public void CalculateVenueStats_AwayWin_CountedCorrectly()
    {
        var venueId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                VenueId = venueId,
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Away),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Away),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var venues = new List<Venue> { CreateVenue(venueId) };

        var result = LeagueStatsService.CalculateVenueStats(fixtures, venues);

        Assert.Single(result);
        Assert.Equal(1, result[0].AwayWins);
    }

    [Fact]
    public void CalculateVenueStats_Draw_CountedCorrectly()
    {
        var venueId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                VenueId = venueId,
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Away)
                }
            }
        };
        var venues = new List<Venue> { CreateVenue(venueId) };

        var result = LeagueStatsService.CalculateVenueStats(fixtures, venues);

        Assert.Single(result);
        Assert.Equal(1, result[0].Draws);
    }

    [Fact]
    public void CalculateVenueStats_MultipleFixtures_Aggregated()
    {
        var venueId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                VenueId = venueId,
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home)
                }
            },
            new()
            {
                VenueId = venueId,
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Away),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Away)
                }
            }
        };
        var venues = new List<Venue> { CreateVenue(venueId) };

        var result = LeagueStatsService.CalculateVenueStats(fixtures, venues);

        Assert.Single(result);
        Assert.Equal(2, result[0].TotalMatches);
        Assert.Equal(4, result[0].TotalFrames);
        Assert.Equal(1, result[0].HomeWins);
        Assert.Equal(1, result[0].AwayWins);
    }

    [Fact]
    public void CalculateVenueStats_MultipleVenues_AllIncluded()
    {
        var venue1 = Guid.NewGuid();
        var venue2 = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                VenueId = venue1,
                Frames = new List<FrameResult> { CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home) }
            },
            new()
            {
                VenueId = venue2,
                Frames = new List<FrameResult> { CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Away) }
            }
        };
        var venues = new List<Venue>
        {
            CreateVenue(venue1, "Venue A"),
            CreateVenue(venue2, "Venue B")
        };

        var result = LeagueStatsService.CalculateVenueStats(fixtures, venues);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void CalculateVenueStats_VenueNotInLookup_UsesUnknown()
    {
        var venueId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                VenueId = venueId,
                Frames = new List<FrameResult> { CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home) }
            }
        };
        var venues = new List<Venue>();

        var result = LeagueStatsService.CalculateVenueStats(fixtures, venues);

        Assert.Single(result);
        Assert.Equal("Unknown", result[0].VenueName);
    }

    [Fact]
    public void CalculateVenueStats_OrderedByTotalMatches_Descending()
    {
        var venue1 = Guid.NewGuid();
        var venue2 = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                VenueId = venue1,
                Frames = new List<FrameResult> { CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home) }
            },
            new()
            {
                VenueId = venue2,
                Frames = new List<FrameResult> { CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home) }
            },
            new()
            {
                VenueId = venue2,
                Frames = new List<FrameResult> { CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home) }
            }
        };
        var venues = new List<Venue>
        {
            CreateVenue(venue1, "Less Busy"),
            CreateVenue(venue2, "More Busy")
        };

        var result = LeagueStatsService.CalculateVenueStats(fixtures, venues);

        Assert.Equal(2, result.Count);
        Assert.Equal("More Busy", result[0].VenueName);
        Assert.Equal(2, result[0].TotalMatches);
        Assert.Equal("Less Busy", result[1].VenueName);
        Assert.Equal(1, result[1].TotalMatches);
    }

    // ========== GenerateSeasonRecap Tests ==========

    [Fact]
    public void GenerateSeasonRecap_EmptyFixtures_ReturnsBasicRecap()
    {
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>();
        var players = new List<Player>();
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal("Test Season", result.SeasonName);
        Assert.Equal(0, result.TotalFixtures);
        Assert.Equal(0, result.TotalFrames);
        Assert.Equal(0, result.TotalEightBalls);
        Assert.Empty(result.MonthlyWinners);
    }

    [Fact]
    public void GenerateSeasonRecap_FixturesWithoutFrames_NotCounted()
    {
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new() { Date = new DateTime(2024, 1, 15) }
        };
        var players = new List<Player>();
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal(0, result.TotalFixtures);
    }

    [Fact]
    public void GenerateSeasonRecap_TotalFixtures_CountedCorrectly()
    {
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult> { CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home) }
            },
            new()
            {
                Date = new DateTime(2024, 1, 22),
                Frames = new List<FrameResult> { CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home) }
            }
        };
        var players = new List<Player>();
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal(2, result.TotalFixtures);
    }

    [Fact]
    public void GenerateSeasonRecap_TotalFrames_CountedCorrectly()
    {
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Away),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player>();
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal(3, result.TotalFrames);
    }

    [Fact]
    public void GenerateSeasonRecap_TotalEightBalls_CountedCorrectly()
    {
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home, true),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Away, false),
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home, true)
                }
            }
        };
        var players = new List<Player>();
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal(2, result.TotalEightBalls);
    }

    [Fact]
    public void GenerateSeasonRecap_TopScorer_IdentifiedCorrectly()
    {
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home),
                    CreateFrame(player1, player2, FrameWinner.Home),
                    CreateFrame(player1, player2, FrameWinner.Home),
                    CreateFrame(player1, player2, FrameWinner.Away)
                }
            }
        };
        var players = new List<Player>
        {
            CreatePlayer(player1, "Top", "Scorer"),
            CreatePlayer(player2, "Second", "Place")
        };
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal("Top Scorer", result.TopScorer);
        Assert.Equal(3, result.TopScorerWins);
    }

    [Fact]
    public void GenerateSeasonRecap_MostEightBalls_IdentifiedCorrectly()
    {
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home, true),
                    CreateFrame(player1, player2, FrameWinner.Home, true),
                    CreateFrame(player1, player2, FrameWinner.Home, true),
                    CreateFrame(player1, player2, FrameWinner.Away, true)
                }
            }
        };
        var players = new List<Player>
        {
            CreatePlayer(player1, "Most", "EightBalls"),
            CreatePlayer(player2, "Fewer", "EightBalls")
        };
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal("Most EightBalls", result.MostEightBalls);
        Assert.Equal(3, result.MostEightBallCount);
    }

    [Fact]
    public void GenerateSeasonRecap_LongestWinStreak_IdentifiedCorrectly()
    {
        var player1 = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Away),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(player1, "Streak", "Master") };
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal("Streak Master", result.LongestWinStreak);
        Assert.Equal(3, result.LongestWinStreakCount);
    }

    [Fact]
    public void GenerateSeasonRecap_WinStreakReset_OnLoss()
    {
        var player1 = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Away),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(player1, "Player", "One") };
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal(2, result.LongestWinStreakCount);
    }

    [Fact]
    public void GenerateSeasonRecap_VoidPlayer_Excluded()
    {
        var player1 = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, FrameResult.VoidPlayerId, FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(player1, "Real", "Player") };
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal("Real Player", result.TopScorer);
        Assert.Equal(2, result.TopScorerWins);
    }

    [Fact]
    public void GenerateSeasonRecap_AwayPlayerStats_Counted()
    {
        var player1 = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), player1, FrameWinner.Away),
                    CreateFrame(Guid.NewGuid(), player1, FrameWinner.Away)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(player1, "Away", "Player") };
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal("Away Player", result.TopScorer);
        Assert.Equal(2, result.TopScorerWins);
    }

    [Fact]
    public void GenerateSeasonRecap_NoPlayers_NoStatistics()
    {
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(Guid.NewGuid(), Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player>();
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal("", result.TopScorer);
        Assert.Equal("", result.MostEightBalls);
        Assert.Equal("", result.LongestWinStreak);
    }

    [Fact]
    public void GenerateSeasonRecap_MonthlyWinners_Populated()
    {
        var player1 = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(player1, "Monthly", "Winner") };
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Single(result.MonthlyWinners);
        Assert.Equal("Monthly Winner", result.MonthlyWinners[0].PlayerName);
    }

    [Fact]
    public void GenerateSeasonRecap_FixturesOrderedByDate_ForStreakCalculation()
    {
        var player1 = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 22),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home)
                }
            },
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(player1, "Test", "Player") };
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal(3, result.LongestWinStreakCount);
    }

    [Fact]
    public void GenerateSeasonRecap_MostImproved_IdentifiedCorrectly()
    {
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>();
        
        for (int i = 0; i < 10; i++)
        {
            fixtures.Add(new()
            {
                Date = new DateTime(2024, 1, 1).AddDays(i),
                HomeTeamId = teamId,
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home),
                    CreateFrame(player1, player2, FrameWinner.Home)
                }
            });
        }

        var players = new List<Player>
        {
            CreatePlayer(player1, "Improved", "Player", teamId),
            CreatePlayer(player2, "Other", "Player")
        };
        var teams = new List<Team> { CreateTeam(teamId, "Test Team") };
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.NotEmpty(result.MostImproved);
        Assert.True(result.MostImprovedGain > 0);
    }

    [Fact]
    public void GenerateSeasonRecap_MostImprovedFewerThanFiveFrames_NotEligible()
    {
        var player1 = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(player1, "Test", "Player") };
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal("", result.MostImproved);
        Assert.Equal(0, result.MostImprovedGain);
    }

    [Fact]
    public void GenerateSeasonRecap_PlayerInitializedWithoutResults_HasDefaultStats()
    {
        var player1 = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(player1, "New", "Player") };
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.Equal("New Player", result.TopScorer);
        Assert.Equal(1, result.TopScorerWins);
    }

    [Fact]
    public void CalculatePlayersOfMonth_WinPercentageZeroFrames_HandledCorrectly()
    {
        var playerId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Away),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Away),
                    CreateFrame(playerId, Guid.NewGuid(), FrameWinner.Away)
                }
            }
        };
        var players = new List<Player> { CreatePlayer(playerId) };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal(0, result[0].FramesWon);
        Assert.Equal(0, result[0].WinPercentage);
    }

    [Fact]
    public void CalculatePlayersOfMonth_MultiplePlayersInSameMonth_BestWinnerSelected()
    {
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player3, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player3, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player3, Guid.NewGuid(), FrameWinner.Away)
                }
            }
        };
        var players = new List<Player>
        {
            CreatePlayer(player1, "Best", "Player"),
            CreatePlayer(player2, "Second", "Player"),
            CreatePlayer(player3, "Third", "Player")
        };
        var teams = new List<Team>();

        var result = LeagueStatsService.CalculatePlayersOfMonth(fixtures, players, teams);

        Assert.Single(result);
        Assert.Equal(player1, result[0].PlayerId);
    }

    [Fact]
    public void GenerateSeasonRecap_MultiplePlayersWithSameWins_FirstIsSelected()
    {
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var season = new Season { Name = "Test Season", StartDate = new DateTime(2024, 1, 1) };
        var fixtures = new List<Fixture>
        {
            new()
            {
                Date = new DateTime(2024, 1, 15),
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, Guid.NewGuid(), FrameWinner.Home),
                    CreateFrame(player2, Guid.NewGuid(), FrameWinner.Home)
                }
            }
        };
        var players = new List<Player>
        {
            CreatePlayer(player1, "First", "Player"),
            CreatePlayer(player2, "Second", "Player")
        };
        var teams = new List<Team>();
        var settings = new AppSettings { RatingStartValue = 1000 };

        var result = LeagueStatsService.GenerateSeasonRecap(season, fixtures, players, teams, settings);

        Assert.NotEmpty(result.TopScorer);
        Assert.Equal(1, result.TopScorerWins);
    }

    // ========== FormatRecapAsText Tests ==========

    [Fact]
    public void FormatRecapAsText_BasicRecap_FormatsCorrectly()
    {
        var recap = new LeagueStatsService.SeasonRecap
        {
            SeasonName = "Winter 2024",
            TotalFixtures = 10,
            TotalFrames = 100,
            TotalEightBalls = 25
        };

        var result = LeagueStatsService.FormatRecapAsText(recap);

        Assert.Contains("Winter 2024", result);
        Assert.Contains("10 matches", result);
        Assert.Contains("100 frames", result);
        Assert.Contains("25 8-balls", result);
    }

    [Fact]
    public void FormatRecapAsText_WithTopScorer_IncludesTopScorer()
    {
        var recap = new LeagueStatsService.SeasonRecap
        {
            SeasonName = "Test Season",
            TotalFixtures = 1,
            TotalFrames = 1,
            TotalEightBalls = 0,
            TopScorer = "John Doe",
            TopScorerWins = 50
        };

        var result = LeagueStatsService.FormatRecapAsText(recap);

        Assert.Contains("Top Scorer: John Doe (50 wins)", result);
    }

    [Fact]
    public void FormatRecapAsText_WithMostImproved_IncludesMostImproved()
    {
        var recap = new LeagueStatsService.SeasonRecap
        {
            SeasonName = "Test Season",
            TotalFixtures = 1,
            TotalFrames = 1,
            TotalEightBalls = 0,
            MostImproved = "Jane Smith",
            MostImprovedGain = 150
        };

        var result = LeagueStatsService.FormatRecapAsText(recap);

        Assert.Contains("Most Improved: Jane Smith (+150 rating)", result);
    }

    [Fact]
    public void FormatRecapAsText_WithMostEightBalls_IncludesMostEightBalls()
    {
        var recap = new LeagueStatsService.SeasonRecap
        {
            SeasonName = "Test Season",
            TotalFixtures = 1,
            TotalFrames = 1,
            TotalEightBalls = 0,
            MostEightBalls = "Bob Wilson",
            MostEightBallCount = 30
        };

        var result = LeagueStatsService.FormatRecapAsText(recap);

        Assert.Contains("Most 8-Balls: Bob Wilson (30)", result);
    }

    [Fact]
    public void FormatRecapAsText_WithLongestWinStreak_IncludesStreak()
    {
        var recap = new LeagueStatsService.SeasonRecap
        {
            SeasonName = "Test Season",
            TotalFixtures = 1,
            TotalFrames = 1,
            TotalEightBalls = 0,
            LongestWinStreak = "Alice Brown",
            LongestWinStreakCount = 15
        };

        var result = LeagueStatsService.FormatRecapAsText(recap);

        Assert.Contains("Longest Win Streak: Alice Brown (15 in a row)", result);
    }

    [Fact]
    public void FormatRecapAsText_WithMonthlyWinners_IncludesMonthlyWinners()
    {
        var recap = new LeagueStatsService.SeasonRecap
        {
            SeasonName = "Test Season",
            TotalFixtures = 1,
            TotalFrames = 1,
            TotalEightBalls = 0,
            MonthlyWinners = new List<LeagueStatsService.PlayerOfMonth>
            {
                new()
                {
                    MonthName = "January 2024",
                    PlayerName = "Winner One",
                    WinPercentage = 75.5
                },
                new()
                {
                    MonthName = "February 2024",
                    PlayerName = "Winner Two",
                    WinPercentage = 80.0
                }
            }
        };

        var result = LeagueStatsService.FormatRecapAsText(recap);

        Assert.Contains("Players of the Month:", result);
        Assert.Contains("January 2024: Winner One (76% win rate)", result);
        Assert.Contains("February 2024: Winner Two (80% win rate)", result);
    }

    [Fact]
    public void FormatRecapAsText_NoMonthlyWinners_OmitsSection()
    {
        var recap = new LeagueStatsService.SeasonRecap
        {
            SeasonName = "Test Season",
            TotalFixtures = 1,
            TotalFrames = 1,
            TotalEightBalls = 0,
            MonthlyWinners = new List<LeagueStatsService.PlayerOfMonth>()
        };

        var result = LeagueStatsService.FormatRecapAsText(recap);

        Assert.DoesNotContain("Players of the Month:", result);
    }

    [Fact]
    public void FormatRecapAsText_EmptyStrings_NotIncluded()
    {
        var recap = new LeagueStatsService.SeasonRecap
        {
            SeasonName = "Test Season",
            TotalFixtures = 1,
            TotalFrames = 1,
            TotalEightBalls = 0,
            TopScorer = "",
            MostImproved = "",
            MostEightBalls = "",
            LongestWinStreak = ""
        };

        var result = LeagueStatsService.FormatRecapAsText(recap);

        Assert.DoesNotContain("Top Scorer:", result);
        Assert.DoesNotContain("Most Improved:", result);
        Assert.DoesNotContain("Most 8-Balls:", result);
        Assert.DoesNotContain("Longest Win Streak:", result);
    }

    [Fact]
    public void FormatRecapAsText_CompleteRecap_FormatsAll()
    {
        var recap = new LeagueStatsService.SeasonRecap
        {
            SeasonName = "Complete Season",
            TotalFixtures = 50,
            TotalFrames = 500,
            TotalEightBalls = 75,
            TopScorer = "John Doe",
            TopScorerWins = 100,
            MostImproved = "Jane Smith",
            MostImprovedGain = 200,
            MostEightBalls = "Bob Wilson",
            MostEightBallCount = 50,
            LongestWinStreak = "Alice Brown",
            LongestWinStreakCount = 20,
            MonthlyWinners = new List<LeagueStatsService.PlayerOfMonth>
            {
                new()
                {
                    MonthName = "January 2024",
                    PlayerName = "Monthly Winner",
                    WinPercentage = 90.0
                }
            }
        };

        var result = LeagueStatsService.FormatRecapAsText(recap);

        Assert.Contains("Complete Season", result);
        Assert.Contains("50 matches", result);
        Assert.Contains("500 frames", result);
        Assert.Contains("75 8-balls", result);
        Assert.Contains("Top Scorer: John Doe (100 wins)", result);
        Assert.Contains("Most Improved: Jane Smith (+200 rating)", result);
        Assert.Contains("Most 8-Balls: Bob Wilson (50)", result);
        Assert.Contains("Longest Win Streak: Alice Brown (20 in a row)", result);
        Assert.Contains("Players of the Month:", result);
        Assert.Contains("January 2024: Monthly Winner (90% win rate)", result);
    }

    // ========== CompareSeasons Tests ==========

    [Fact]
    public void CompareSeasons_EmptyFixtures_ReturnsZeroStatistics()
    {
        // Arrange
        var season1 = new Season { Name = "Season 1" };
        var season2 = new Season { Name = "Season 2" };
        var fixtures1 = new List<Fixture>();
        var fixtures2 = new List<Fixture>();
        var teams1 = new List<Team>();
        var teams2 = new List<Team>();
        var players1 = new List<Player>();
        var players2 = new List<Player>();

        // Act
        var result = LeagueStatsService.CompareSeasons(
            season1, fixtures1, teams1, players1,
            season2, fixtures2, teams2, players2);

        // Assert
        Assert.Equal("Season 1", result.Season1Name);
        Assert.Equal("Season 2", result.Season2Name);
        Assert.Equal(0, result.Season1Fixtures);
        Assert.Equal(0, result.Season2Fixtures);
        Assert.Equal(0, result.Season1Frames);
        Assert.Equal(0, result.Season2Frames);
        Assert.Equal(0, result.Season1EightBalls);
        Assert.Equal(0, result.Season2EightBalls);
        Assert.Equal(0, result.Season1Teams);
        Assert.Equal(0, result.Season2Teams);
        Assert.Equal(0, result.Season1Players);
        Assert.Equal(0, result.Season2Players);
        Assert.Equal(0, result.Season1AvgFramesPerMatch);
        Assert.Equal(0, result.Season2AvgFramesPerMatch);
        Assert.Equal(0, result.Season1HomeWinPct);
        Assert.Equal(0, result.Season2HomeWinPct);
    }

    [Fact]
    public void CompareSeasons_FixturesWithoutFrames_ExcludesFromCounts()
    {
        // Arrange
        var season1 = new Season { Name = "Season 1" };
        var season2 = new Season { Name = "Season 2" };
        var fixtures1 = new List<Fixture>
        {
            new() { Frames = new List<FrameResult>() }, // Empty frames - should be excluded
            new() { Frames = new List<FrameResult>() }
        };
        var fixtures2 = new List<Fixture>
        {
            new() { Frames = new List<FrameResult>() }
        };
        var teams1 = new List<Team> { CreateTeam(Guid.NewGuid()) };
        var teams2 = new List<Team> { CreateTeam(Guid.NewGuid()), CreateTeam(Guid.NewGuid()) };
        var players1 = new List<Player> { CreatePlayer(Guid.NewGuid()) };
        var players2 = new List<Player> { CreatePlayer(Guid.NewGuid()) };

        // Act
        var result = LeagueStatsService.CompareSeasons(
            season1, fixtures1, teams1, players1,
            season2, fixtures2, teams2, players2);

        // Assert
        Assert.Equal(0, result.Season1Fixtures); // Excluded because no frames
        Assert.Equal(0, result.Season2Fixtures);
        Assert.Equal(1, result.Season1Teams);
        Assert.Equal(2, result.Season2Teams);
        Assert.Equal(1, result.Season1Players);
        Assert.Equal(1, result.Season2Players);
    }

    [Fact]
    public void CompareSeasons_WithFrameData_CalculatesCorrectStatistics()
    {
        // Arrange
        var season1 = new Season { Name = "Winter 2023" };
        var season2 = new Season { Name = "Summer 2024" };

        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();
        var player4 = Guid.NewGuid();

        var fixtures1 = new List<Fixture>
        {
            new()
            {
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home, eightBall: true),
                    CreateFrame(player1, player2, FrameWinner.Home),
                    CreateFrame(player1, player2, FrameWinner.Away)
                }
            },
            new()
            {
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player3, player4, FrameWinner.Home),
                    CreateFrame(player3, player4, FrameWinner.Away, eightBall: true)
                }
            }
        };

        var fixtures2 = new List<Fixture>
        {
            new()
            {
                HomeTeamId = Guid.NewGuid(),
                AwayTeamId = Guid.NewGuid(),
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Away),
                    CreateFrame(player1, player2, FrameWinner.Away),
                    CreateFrame(player1, player2, FrameWinner.Away),
                    CreateFrame(player1, player2, FrameWinner.Away, eightBall: true)
                }
            }
        };

        var teams1 = new List<Team> { CreateTeam(Guid.NewGuid()), CreateTeam(Guid.NewGuid()) };
        var teams2 = new List<Team> { CreateTeam(Guid.NewGuid()) };
        var players1 = new List<Player> { CreatePlayer(player1), CreatePlayer(player2), CreatePlayer(player3) };
        var players2 = new List<Player> { CreatePlayer(player1), CreatePlayer(player2) };

        // Act
        var result = LeagueStatsService.CompareSeasons(
            season1, fixtures1, teams1, players1,
            season2, fixtures2, teams2, players2);

        // Assert
        Assert.Equal("Winter 2023", result.Season1Name);
        Assert.Equal("Summer 2024", result.Season2Name);
        Assert.Equal(2, result.Season1Fixtures);
        Assert.Equal(1, result.Season2Fixtures);
        Assert.Equal(5, result.Season1Frames); // 3 + 2
        Assert.Equal(4, result.Season2Frames);
        Assert.Equal(2, result.Season1EightBalls); // 1 home + 1 away
        Assert.Equal(1, result.Season2EightBalls);
        Assert.Equal(2, result.Season1Teams);
        Assert.Equal(1, result.Season2Teams);
        Assert.Equal(3, result.Season1Players);
        Assert.Equal(2, result.Season2Players);
        Assert.Equal(2.5, result.Season1AvgFramesPerMatch); // 5 frames / 2 fixtures
        Assert.Equal(4.0, result.Season2AvgFramesPerMatch); // 4 frames / 1 fixture
        Assert.Equal(50.0, result.Season1HomeWinPct); // 1 out of 2 fixtures (first fixture home won 2-1, second fixture home won 1-1 but we count fixtures where home score > away score)
        Assert.Equal(0.0, result.Season2HomeWinPct); // 0 out of 1 fixture (home score 0, away score 4)
    }

    [Fact]
    public void CompareSeasons_AllHomeWins_Returns100PercentHomeWinRate()
    {
        // Arrange
        var season1 = new Season { Name = "Season A" };
        var season2 = new Season { Name = "Season B" };

        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();

        var fixtures1 = new List<Fixture>
        {
            new()
            {
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home),
                    CreateFrame(player1, player2, FrameWinner.Home)
                }
            },
            new()
            {
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home)
                }
            }
        };

        var fixtures2 = new List<Fixture>();
        var teams1 = new List<Team>();
        var teams2 = new List<Team>();
        var players1 = new List<Player>();
        var players2 = new List<Player>();

        // Act
        var result = LeagueStatsService.CompareSeasons(
            season1, fixtures1, teams1, players1,
            season2, fixtures2, teams2, players2);

        // Assert
        Assert.Equal(100.0, result.Season1HomeWinPct);
    }

    [Fact]
    public void CompareSeasons_AllAwayWins_ReturnsZeroPercentHomeWinRate()
    {
        // Arrange
        var season1 = new Season { Name = "Season A" };
        var season2 = new Season { Name = "Season B" };

        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();

        var fixtures1 = new List<Fixture>
        {
            new()
            {
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Away),
                    CreateFrame(player1, player2, FrameWinner.Away)
                }
            }
        };

        var fixtures2 = new List<Fixture>();
        var teams1 = new List<Team>();
        var teams2 = new List<Team>();
        var players1 = new List<Player>();
        var players2 = new List<Player>();

        // Act
        var result = LeagueStatsService.CompareSeasons(
            season1, fixtures1, teams1, players1,
            season2, fixtures2, teams2, players2);

        // Assert
        Assert.Equal(0.0, result.Season1HomeWinPct);
    }

    [Fact]
    public void CompareSeasons_MixedResults_CalculatesCorrectHomeWinPercentage()
    {
        // Arrange
        var season1 = new Season { Name = "Season A" };
        var season2 = new Season { Name = "Season B" };

        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();

        // 3 home wins out of 4 fixtures = 75%
        var fixtures1 = new List<Fixture>
        {
            new()
            {
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home),
                    CreateFrame(player1, player2, FrameWinner.Home)
                }
            },
            new()
            {
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home),
                    CreateFrame(player1, player2, FrameWinner.Home),
                    CreateFrame(player1, player2, FrameWinner.Away)
                }
            },
            new()
            {
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Away),
                    CreateFrame(player1, player2, FrameWinner.Away)
                }
            },
            new()
            {
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home),
                    CreateFrame(player1, player2, FrameWinner.Home)
                }
            }
        };

        var fixtures2 = new List<Fixture>();
        var teams1 = new List<Team>();
        var teams2 = new List<Team>();
        var players1 = new List<Player>();
        var players2 = new List<Player>();

        // Act
        var result = LeagueStatsService.CompareSeasons(
            season1, fixtures1, teams1, players1,
            season2, fixtures2, teams2, players2);

        // Assert
        Assert.Equal(75.0, result.Season1HomeWinPct);
    }

    [Fact]
    public void CompareSeasons_EightBallFrames_CountedCorrectly()
    {
        // Arrange
        var season1 = new Season { Name = "Season A" };
        var season2 = new Season { Name = "Season B" };

        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();

        var fixtures1 = new List<Fixture>
        {
            new()
            {
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home, eightBall: true),
                    CreateFrame(player1, player2, FrameWinner.Away, eightBall: true),
                    CreateFrame(player1, player2, FrameWinner.Home, eightBall: false)
                }
            }
        };

        var fixtures2 = new List<Fixture>
        {
            new()
            {
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home, eightBall: true),
                    CreateFrame(player1, player2, FrameWinner.Home, eightBall: true),
                    CreateFrame(player1, player2, FrameWinner.Home, eightBall: true)
                }
            }
        };

        var teams1 = new List<Team>();
        var teams2 = new List<Team>();
        var players1 = new List<Player>();
        var players2 = new List<Player>();

        // Act
        var result = LeagueStatsService.CompareSeasons(
            season1, fixtures1, teams1, players1,
            season2, fixtures2, teams2, players2);

        // Assert
        Assert.Equal(2, result.Season1EightBalls);
        Assert.Equal(3, result.Season2EightBalls);
    }

    [Fact]
    public void CompareSeasons_DrawFixture_NotCountedAsHomeWin()
    {
        // Arrange
        var season1 = new Season { Name = "Season A" };
        var season2 = new Season { Name = "Season B" };

        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();

        var fixtures1 = new List<Fixture>
        {
            new()
            {
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home),
                    CreateFrame(player1, player2, FrameWinner.Away)
                }
            }
        };

        var fixtures2 = new List<Fixture>();
        var teams1 = new List<Team>();
        var teams2 = new List<Team>();
        var players1 = new List<Player>();
        var players2 = new List<Player>();

        // Act
        var result = LeagueStatsService.CompareSeasons(
            season1, fixtures1, teams1, players1,
            season2, fixtures2, teams2, players2);

        // Assert
        Assert.Equal(0.0, result.Season1HomeWinPct); // Draw (1-1) should not count as home win
    }

    [Fact]
    public void CompareSeasons_Season2HasDataSeason1Empty_CalculatesCorrectly()
    {
        // Arrange
        var season1 = new Season { Name = "Empty Season" };
        var season2 = new Season { Name = "Active Season" };

        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();

        var fixtures1 = new List<Fixture>();
        var fixtures2 = new List<Fixture>
        {
            new()
            {
                Frames = new List<FrameResult>
                {
                    CreateFrame(player1, player2, FrameWinner.Home, eightBall: true),
                    CreateFrame(player1, player2, FrameWinner.Home)
                }
            }
        };

        var teams1 = new List<Team>();
        var teams2 = new List<Team> { CreateTeam(Guid.NewGuid()), CreateTeam(Guid.NewGuid()) };
        var players1 = new List<Player>();
        var players2 = new List<Player> { CreatePlayer(player1), CreatePlayer(player2) };

        // Act
        var result = LeagueStatsService.CompareSeasons(
            season1, fixtures1, teams1, players1,
            season2, fixtures2, teams2, players2);

        // Assert
        Assert.Equal("Empty Season", result.Season1Name);
        Assert.Equal("Active Season", result.Season2Name);
        Assert.Equal(0, result.Season1Fixtures);
        Assert.Equal(1, result.Season2Fixtures);
        Assert.Equal(0, result.Season1Frames);
        Assert.Equal(2, result.Season2Frames);
        Assert.Equal(0, result.Season1EightBalls);
        Assert.Equal(1, result.Season2EightBalls);
        Assert.Equal(0, result.Season1Teams);
        Assert.Equal(2, result.Season2Teams);
        Assert.Equal(0, result.Season1Players);
        Assert.Equal(2, result.Season2Players);
        Assert.Equal(0.0, result.Season1AvgFramesPerMatch);
        Assert.Equal(2.0, result.Season2AvgFramesPerMatch);
        Assert.Equal(0.0, result.Season1HomeWinPct);
        Assert.Equal(100.0, result.Season2HomeWinPct);
    }
}

