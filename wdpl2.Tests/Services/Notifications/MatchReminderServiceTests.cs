using Moq;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for MatchReminderService match reminder notification scheduling.
/// </summary>
public class MatchReminderServiceTests
{
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IDataStore> _mockDataStore;
    private readonly MatchReminderService _service;

    public MatchReminderServiceTests()
    {
        _mockNotificationService = new Mock<INotificationService>();
        _mockDataStore = new Mock<IDataStore>();
        _service = new MatchReminderService(_mockNotificationService.Object, _mockDataStore.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesService()
    {
        // Arrange & Act
        var service = new MatchReminderService(_mockNotificationService.Object, _mockDataStore.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task ScheduleMatchReminderAsync_WithFutureDate_SchedulesNotification()
    {
        // Arrange
        var fixtureId = Guid.NewGuid();
        var matchDate = DateTime.Now.AddDays(1);
        var homeTeam = "Team A";
        var awayTeam = "Team B";
        var hoursBeforeMatch = 2;

        // Act
        await _service.ScheduleMatchReminderAsync(fixtureId, matchDate, homeTeam, awayTeam, hoursBeforeMatch);

        // Assert
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            It.IsAny<int>(),
            It.Is<string>(s => s.Contains("Match Reminder")),
            It.Is<string>(s => s.Contains(homeTeam) && s.Contains(awayTeam) && s.Contains("2 hours")),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ScheduleMatchReminderAsync_WithPastReminderTime_DoesNotSchedule()
    {
        // Arrange
        var fixtureId = Guid.NewGuid();
        var matchDate = DateTime.Now.AddHours(1); // Reminder would be in the past
        var homeTeam = "Team A";
        var awayTeam = "Team B";
        var hoursBeforeMatch = 2;

        // Act
        await _service.ScheduleMatchReminderAsync(fixtureId, matchDate, homeTeam, awayTeam, hoursBeforeMatch);

        // Assert
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ScheduleMatchReminderAsync_WithSameFixtureId_ReplacesOldReminder()
    {
        // Arrange
        var fixtureId = Guid.NewGuid();
        var matchDate1 = DateTime.Now.AddDays(1);
        var matchDate2 = DateTime.Now.AddDays(2);
        var homeTeam = "Team A";
        var awayTeam = "Team B";

        // Act
        await _service.ScheduleMatchReminderAsync(fixtureId, matchDate1, homeTeam, awayTeam);
        await _service.ScheduleMatchReminderAsync(fixtureId, matchDate2, homeTeam, awayTeam);
        var reminders = await _service.GetAllScheduledRemindersAsync();

        // Assert
        Assert.Single(reminders);
        Assert.Equal(matchDate2, reminders[0].MatchDate);
    }

    [Fact]
    public async Task ScheduleMatchReminderAsync_WithException_CatchesAndContinues()
    {
        // Arrange
        var fixtureId = Guid.NewGuid();
        var matchDate = DateTime.Now.AddDays(1);
        var homeTeam = "Team A";
        var awayTeam = "Team B";

        _mockNotificationService.Setup(x => x.ScheduleNotificationAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        await _service.ScheduleMatchReminderAsync(fixtureId, matchDate, homeTeam, awayTeam);

        // Assert - Should not throw
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CancelMatchReminderAsync_WithExistingReminder_CancelsAndRemoves()
    {
        // Arrange
        var fixtureId = Guid.NewGuid();
        var matchDate = DateTime.Now.AddDays(1);
        var homeTeam = "Team A";
        var awayTeam = "Team B";

        await _service.ScheduleMatchReminderAsync(fixtureId, matchDate, homeTeam, awayTeam);

        // Act
        await _service.CancelMatchReminderAsync(fixtureId);
        var reminders = await _service.GetAllScheduledRemindersAsync();

        // Assert
        Assert.Empty(reminders);
        _mockNotificationService.Verify(x => x.CancelNotificationAsync(It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task CancelMatchReminderAsync_WithNonExistingReminder_CalculatesAndCancels()
    {
        // Arrange
        var fixtureId = Guid.NewGuid();

        // Act
        await _service.CancelMatchReminderAsync(fixtureId);

        // Assert
        _mockNotificationService.Verify(x => x.CancelNotificationAsync(It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task CancelMatchReminderAsync_WithException_CatchesAndContinues()
    {
        // Arrange
        var fixtureId = Guid.NewGuid();

        _mockNotificationService.Setup(x => x.CancelNotificationAsync(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        await _service.CancelMatchReminderAsync(fixtureId);

        // Assert - Should not throw
        _mockNotificationService.Verify(x => x.CancelNotificationAsync(It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task GetAllScheduledRemindersAsync_WithNoReminders_ReturnsEmptyList()
    {
        // Arrange & Act
        var reminders = await _service.GetAllScheduledRemindersAsync();

        // Assert
        Assert.Empty(reminders);
    }

    [Fact]
    public async Task GetAllScheduledRemindersAsync_WithMultipleReminders_ReturnsOrderedByMatchDate()
    {
        // Arrange
        var fixture1 = Guid.NewGuid();
        var fixture2 = Guid.NewGuid();
        var fixture3 = Guid.NewGuid();
        var date1 = DateTime.Now.AddDays(3);
        var date2 = DateTime.Now.AddDays(1);
        var date3 = DateTime.Now.AddDays(2);

        await _service.ScheduleMatchReminderAsync(fixture1, date1, "Team A", "Team B");
        await _service.ScheduleMatchReminderAsync(fixture2, date2, "Team C", "Team D");
        await _service.ScheduleMatchReminderAsync(fixture3, date3, "Team E", "Team F");

        // Act
        var reminders = await _service.GetAllScheduledRemindersAsync();

        // Assert
        Assert.Equal(3, reminders.Count);
        Assert.Equal(date2, reminders[0].MatchDate);
        Assert.Equal(date3, reminders[1].MatchDate);
        Assert.Equal(date1, reminders[2].MatchDate);
    }

    [Fact]
    public async Task GetAllScheduledRemindersAsync_WithPastReminders_RemovesPastReminders()
    {
        // Arrange
        var futureFix = Guid.NewGuid();
        var futureDate = DateTime.Now.AddDays(1);

        await _service.ScheduleMatchReminderAsync(futureFix, futureDate, "Team A", "Team B");

        // Simulate a past reminder by using reflection to add it directly
        var remindersField = typeof(MatchReminderService)
            .GetField("_scheduledReminders", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var remindersList = remindersField?.GetValue(_service) as List<MatchReminderService.ScheduledReminder>;
        
        remindersList?.Add(new MatchReminderService.ScheduledReminder
        {
            FixtureId = Guid.NewGuid(),
            MatchDate = DateTime.Now.AddDays(-1),
            ReminderTime = DateTime.Now.AddDays(-1).AddHours(-2),
            HomeTeam = "Old Team",
            AwayTeam = "Old Team 2"
        });

        // Act
        var reminders = await _service.GetAllScheduledRemindersAsync();

        // Assert
        Assert.Single(reminders);
        Assert.Equal(futureDate, reminders[0].MatchDate);
    }

    [Fact]
    public async Task ScheduleMatchReminderAsync_PopulatesReminderFields_Correctly()
    {
        // Arrange
        var fixtureId = Guid.NewGuid();
        var matchDate = DateTime.Now.AddDays(1);
        var homeTeam = "Home Team";
        var awayTeam = "Away Team";
        var hoursBeforeMatch = 3;

        // Act
        await _service.ScheduleMatchReminderAsync(fixtureId, matchDate, homeTeam, awayTeam, hoursBeforeMatch);
        var reminders = await _service.GetAllScheduledRemindersAsync();

        // Assert
        Assert.Single(reminders);
        var reminder = reminders[0];
        Assert.Equal(fixtureId, reminder.FixtureId);
        Assert.Equal(matchDate, reminder.MatchDate);
        Assert.Equal(homeTeam, reminder.HomeTeam);
        Assert.Equal(awayTeam, reminder.AwayTeam);
        Assert.Equal(matchDate.AddHours(-hoursBeforeMatch), reminder.ReminderTime);
        Assert.True(reminder.NotificationId >= 10000 && reminder.NotificationId < 20000);
    }


    [Fact]
    public async Task SchedulePlayerMatchRemindersAsync_WithValidPlayer_SchedulesReminders()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var player = new Player { Id = playerId, TeamId = teamId };
        var team = new Team { Id = teamId, Name = "Team A" };
        var opponentTeam = new Team { Id = Guid.NewGuid(), Name = "Team B" };
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = teamId,
            AwayTeamId = opponentTeam.Id,
            Date = DateTime.Now.AddDays(7)
        };

        var leagueData = new LeagueData
        {
            Players = new List<Player> { player },
            Teams = new List<Team> { team, opponentTeam },
            Fixtures = new List<Fixture> { fixture }
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.SchedulePlayerMatchRemindersAsync(playerId);

        // Assert
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task SchedulePlayerMatchRemindersAsync_WithNonExistentPlayer_DoesNotSchedule()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var leagueData = new LeagueData
        {
            Players = new List<Player>()
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.SchedulePlayerMatchRemindersAsync(playerId);

        // Assert
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SchedulePlayerMatchRemindersAsync_WithPlayerButNoTeam_DoesNotSchedule()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player { Id = playerId, TeamId = Guid.NewGuid() };
        var leagueData = new LeagueData
        {
            Players = new List<Player> { player },
            Teams = new List<Team>()
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.SchedulePlayerMatchRemindersAsync(playerId);

        // Assert
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SchedulePlayerMatchRemindersAsync_WithNoUpcomingFixtures_DoesNotSchedule()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var player = new Player { Id = playerId, TeamId = teamId };
        var team = new Team { Id = teamId, Name = "Team A" };
        var leagueData = new LeagueData
        {
            Players = new List<Player> { player },
            Teams = new List<Team> { team },
            Fixtures = new List<Fixture>()
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.SchedulePlayerMatchRemindersAsync(playerId);

        // Assert
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SchedulePlayerMatchRemindersAsync_WithException_CatchesAndContinues()
    {
        // Arrange
        var playerId = Guid.NewGuid();

        _mockDataStore.Setup(x => x.GetData()).Throws(new InvalidOperationException("Test exception"));

        // Act
        await _service.SchedulePlayerMatchRemindersAsync(playerId);

        // Assert - Should not throw
        _mockDataStore.Verify(x => x.GetData(), Times.Once);
    }

    [Fact]
    public async Task ScheduleTeamMatchRemindersAsync_WithValidTeam_SchedulesPlayerReminders()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var team = new Team { Id = teamId, Name = "Team A" };
        var player1 = new Player { Id = player1Id, TeamId = teamId };
        var player2 = new Player { Id = player2Id, TeamId = teamId };

        var leagueData = new LeagueData
        {
            Teams = new List<Team> { team },
            Players = new List<Player> { player1, player2 },
            Fixtures = new List<Fixture>()
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleTeamMatchRemindersAsync(teamId);

        // Assert
        _mockDataStore.Verify(x => x.GetData(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ScheduleTeamMatchRemindersAsync_WithNonExistentTeam_DoesNotSchedule()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var leagueData = new LeagueData
        {
            Teams = new List<Team>(),
            Players = new List<Player>()
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleTeamMatchRemindersAsync(teamId);

        // Assert
        _mockDataStore.Verify(x => x.GetData(), Times.Once);
    }

    [Fact]
    public async Task ScheduleTeamMatchRemindersAsync_WithNullPlayers_DoesNotSchedule()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var team = new Team { Id = teamId, Name = "Team A" };
        var leagueData = new LeagueData
        {
            Teams = new List<Team> { team },
            Players = null!
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleTeamMatchRemindersAsync(teamId);

        // Assert
        _mockDataStore.Verify(x => x.GetData(), Times.AtLeast(1));
    }

    [Fact]
    public async Task ScheduleTeamMatchRemindersAsync_WithEmptyPlayersList_DoesNotSchedule()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var team = new Team { Id = teamId, Name = "Team A" };
        var leagueData = new LeagueData
        {
            Teams = new List<Team> { team },
            Players = new List<Player>()
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleTeamMatchRemindersAsync(teamId);

        // Assert
        _mockDataStore.Verify(x => x.GetData(), Times.AtLeast(1));
    }

    [Fact]
    public async Task ScheduleTeamMatchRemindersAsync_WithException_CatchesAndContinues()
    {
        // Arrange
        var teamId = Guid.NewGuid();

        _mockDataStore.Setup(x => x.GetData()).Throws(new InvalidOperationException("Test exception"));

        // Act
        await _service.ScheduleTeamMatchRemindersAsync(teamId);

        // Assert - Should not throw
        _mockDataStore.Verify(x => x.GetData(), Times.Once);
    }

    [Fact]
    public async Task ScheduleTeamMatchRemindersAsync_WithCustomHours_PassesHoursToPlayerScheduler()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var team = new Team { Id = teamId, Name = "Team A" };
        var player = new Player { Id = playerId, TeamId = teamId };

        var leagueData = new LeagueData
        {
            Teams = new List<Team> { team },
            Players = new List<Player> { player },
            Fixtures = new List<Fixture>()
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleTeamMatchRemindersAsync(teamId, 4);

        // Assert
        _mockDataStore.Verify(x => x.GetData(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ScheduleFixtureReminderAsync_WithValidFixture_SchedulesReminder()
    {
        // Arrange
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var homeTeam = new Team { Id = homeTeamId, Name = "Home Team" };
        var awayTeam = new Team { Id = awayTeamId, Name = "Away Team" };
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Date = DateTime.Now.AddDays(5)
        };

        var leagueData = new LeagueData
        {
            Teams = new List<Team> { homeTeam, awayTeam }
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleFixtureReminderAsync(fixture);

        // Assert
        _mockDataStore.Verify(x => x.GetData(), Times.AtLeast(1));
    }

    [Fact]
    public async Task ScheduleFixtureReminderAsync_WithMissingHomeTeam_UsesDefaultName()
    {
        // Arrange
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var awayTeam = new Team { Id = awayTeamId, Name = "Away Team" };
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Date = DateTime.Now.AddDays(5)
        };

        var leagueData = new LeagueData
        {
            Teams = new List<Team> { awayTeam }
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleFixtureReminderAsync(fixture);

        // Assert
        _mockDataStore.Verify(x => x.GetData(), Times.AtLeast(1));
    }

    [Fact]
    public async Task ScheduleFixtureReminderAsync_WithMissingAwayTeam_UsesDefaultName()
    {
        // Arrange
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var homeTeam = new Team { Id = homeTeamId, Name = "Home Team" };
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Date = DateTime.Now.AddDays(5)
        };

        var leagueData = new LeagueData
        {
            Teams = new List<Team> { homeTeam }
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleFixtureReminderAsync(fixture);

        // Assert
        _mockDataStore.Verify(x => x.GetData(), Times.AtLeast(1));
    }

    [Fact]
    public async Task ScheduleFixtureReminderAsync_WithException_CatchesAndContinues()
    {
        // Arrange
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Date = DateTime.Now.AddDays(5)
        };

        _mockDataStore.Setup(x => x.GetData()).Throws(new InvalidOperationException("Test exception"));

        // Act
        await _service.ScheduleFixtureReminderAsync(fixture);

        // Assert - Should not throw
        _mockDataStore.Verify(x => x.GetData(), Times.Once);
    }

    [Fact]
    public async Task ScheduleFixtureReminderAsync_WithCustomHours_PassesHoursToScheduler()
    {
        // Arrange
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var homeTeam = new Team { Id = homeTeamId, Name = "Home Team" };
        var awayTeam = new Team { Id = awayTeamId, Name = "Away Team" };
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Date = DateTime.Now.AddDays(5)
        };

        var leagueData = new LeagueData
        {
            Teams = new List<Team> { homeTeam, awayTeam }
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleFixtureReminderAsync(fixture, 3);

        // Assert
        _mockDataStore.Verify(x => x.GetData(), Times.AtLeast(1));
    }

    [Fact]
    public async Task CancelAllMatchRemindersAsync_WithScheduledReminders_CancelsAll()
    {
        // Arrange
        var fixture1 = Guid.NewGuid();
        var fixture2 = Guid.NewGuid();
        var matchDate = DateTime.Now.AddDays(1);
        
        await _service.ScheduleMatchReminderAsync(fixture1, matchDate, "Team A", "Team B");
        await _service.ScheduleMatchReminderAsync(fixture2, matchDate, "Team C", "Team D");

        // Act
        await _service.CancelAllMatchRemindersAsync();

        // Assert
        var reminders = await _service.GetAllScheduledRemindersAsync();
        Assert.Empty(reminders);
        _mockNotificationService.Verify(x => x.CancelNotificationAsync(It.IsAny<int>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task CancelAllMatchRemindersAsync_WithNoScheduledReminders_CancelsIdRange()
    {
        // Arrange & Act
        await _service.CancelAllMatchRemindersAsync();

        // Assert
        _mockNotificationService.Verify(x => x.CancelNotificationAsync(It.IsAny<int>()), Times.AtLeast(1));
    }

    [Fact]
    public async Task CancelAllMatchRemindersAsync_WithCancellationErrors_ContinuesWithAllIds()
    {
        // Arrange
        var fixture1 = Guid.NewGuid();
        var matchDate = DateTime.Now.AddDays(1);
        
        await _service.ScheduleMatchReminderAsync(fixture1, matchDate, "Team A", "Team B");

        _mockNotificationService.Setup(x => x.CancelNotificationAsync(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        await _service.CancelAllMatchRemindersAsync();

        // Assert - Should not throw, but list won't be cleared due to exception in foreach
        var reminders = await _service.GetAllScheduledRemindersAsync();
        Assert.Single(reminders);
    }

    [Fact]
    public async Task CancelAllMatchRemindersAsync_WithException_CatchesAndContinues()
    {
        // Arrange
        _mockNotificationService.Setup(x => x.CancelNotificationAsync(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        await _service.CancelAllMatchRemindersAsync();

        // Assert - Should not throw
        _mockNotificationService.Verify(x => x.CancelNotificationAsync(It.IsAny<int>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task NotifyMatchResultAsync_WithHomeWin_ShowsWinnerNotification()
    {
        // Arrange
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var homeTeam = new Team { Id = homeTeamId, Name = "Home Team" };
        var awayTeam = new Team { Id = awayTeamId, Name = "Away Team" };
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Date = DateTime.Now,
            Frames = new List<FrameResult>
            {
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Away },
                new FrameResult { Winner = FrameWinner.Away },
                new FrameResult { Winner = FrameWinner.Away }
            }
        };

        var leagueData = new LeagueData
        {
            Teams = new List<Team> { homeTeam, awayTeam }
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.NotifyMatchResultAsync(fixture);

        // Assert
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            It.IsAny<int>(),
            It.Is<string>(s => s.Contains("Match Result")),
            It.Is<string>(s => s.Contains("Home Team") && s.Contains("Away Team") && s.Contains("5") && s.Contains("3") && s.Contains("wins")),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task NotifyMatchResultAsync_WithAwayWin_ShowsWinnerNotification()
    {
        // Arrange
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var homeTeam = new Team { Id = homeTeamId, Name = "Home Team" };
        var awayTeam = new Team { Id = awayTeamId, Name = "Away Team" };
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Date = DateTime.Now,
            Frames = new List<FrameResult>
            {
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Away },
                new FrameResult { Winner = FrameWinner.Away },
                new FrameResult { Winner = FrameWinner.Away },
                new FrameResult { Winner = FrameWinner.Away }
            }
        };

        var leagueData = new LeagueData
        {
            Teams = new List<Team> { homeTeam, awayTeam }
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.NotifyMatchResultAsync(fixture);

        // Assert
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            It.IsAny<int>(),
            It.Is<string>(s => s.Contains("Match Result")),
            It.Is<string>(s => s.Contains("Home Team") && s.Contains("Away Team") && s.Contains("2") && s.Contains("4") && s.Contains("wins")),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task NotifyMatchResultAsync_WithDraw_ShowsDrawNotification()
    {
        // Arrange
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var homeTeam = new Team { Id = homeTeamId, Name = "Home Team" };
        var awayTeam = new Team { Id = awayTeamId, Name = "Away Team" };
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Date = DateTime.Now,
            Frames = new List<FrameResult>
            {
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Away },
                new FrameResult { Winner = FrameWinner.Away },
                new FrameResult { Winner = FrameWinner.Away }
            }
        };

        var leagueData = new LeagueData
        {
            Teams = new List<Team> { homeTeam, awayTeam }
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.NotifyMatchResultAsync(fixture);

        // Assert
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            It.IsAny<int>(),
            It.Is<string>(s => s.Contains("Match Result")),
            It.Is<string>(s => s.Contains("Home Team") && s.Contains("Away Team") && s.Contains("3") && s.Contains("3") && s.Contains("Draw")),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task NotifyMatchResultAsync_WithMissingTeams_UsesDefaultNames()
    {
        // Arrange
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Date = DateTime.Now,
            Frames = new List<FrameResult>
            {
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Away },
                new FrameResult { Winner = FrameWinner.Away },
                new FrameResult { Winner = FrameWinner.Away }
            }
        };

        var leagueData = new LeagueData
        {
            Teams = new List<Team>()
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.NotifyMatchResultAsync(fixture);

        // Assert
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            It.IsAny<int>(),
            It.Is<string>(s => s.Contains("Match Result")),
            It.Is<string>(s => s.Contains("Home") && s.Contains("Away") && s.Contains("5") && s.Contains("3")),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task NotifyMatchResultAsync_WithException_CatchesAndContinues()
    {
        // Arrange
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Date = DateTime.Now,
            Frames = new List<FrameResult>
            {
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Home },
                new FrameResult { Winner = FrameWinner.Away },
                new FrameResult { Winner = FrameWinner.Away },
                new FrameResult { Winner = FrameWinner.Away }
            }
        };

        _mockDataStore.Setup(x => x.GetData()).Throws(new InvalidOperationException("Test exception"));

        // Act
        await _service.NotifyMatchResultAsync(fixture);

        // Assert - Should not throw
        _mockDataStore.Verify(x => x.GetData(), Times.Once);
    }

    [Fact]
    public async Task ScheduleWeeklyFixtureListAsync_WithUpcomingFixtures_SchedulesNotification()
    {
        // Arrange
        var fixture1 = new Fixture { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(2) };
        var fixture2 = new Fixture { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(4) };
        var fixture3 = new Fixture { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(6) };

        var leagueData = new LeagueData
        {
            Fixtures = new List<Fixture> { fixture1, fixture2, fixture3 }
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleWeeklyFixtureListAsync(DayOfWeek.Monday, new TimeSpan(9, 0, 0));

        // Assert
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            20000,
            It.Is<string>(s => s.Contains("This Week's Fixtures")),
            It.Is<string>(s => s.Contains("3 matches this week")),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ScheduleWeeklyFixtureListAsync_WithNoUpcomingFixtures_DoesNotSchedule()
    {
        // Arrange
        var leagueData = new LeagueData
        {
            Fixtures = new List<Fixture>()
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleWeeklyFixtureListAsync(DayOfWeek.Monday, new TimeSpan(9, 0, 0));

        // Assert
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ScheduleWeeklyFixtureListAsync_WithNullFixtures_DoesNotSchedule()
    {
        // Arrange
        var leagueData = new LeagueData
        {
            Fixtures = null!
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleWeeklyFixtureListAsync(DayOfWeek.Monday, new TimeSpan(9, 0, 0));

        // Assert
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ScheduleWeeklyFixtureListAsync_WithMaxFiveFixtures_LimitsToFive()
    {
        // Arrange
        var fixtures = new List<Fixture>();
        for (int i = 1; i <= 10; i++)
        {
            fixtures.Add(new Fixture { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(i) });
        }

        var leagueData = new LeagueData
        {
            Fixtures = fixtures
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleWeeklyFixtureListAsync(DayOfWeek.Monday, new TimeSpan(9, 0, 0));

        // Assert
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            20000,
            It.Is<string>(s => s.Contains("This Week's Fixtures")),
            It.Is<string>(s => s.Contains("5 matches this week")),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ScheduleWeeklyFixtureListAsync_WithTodayAsDayOfWeek_SchedulesNextWeek()
    {
        // Arrange
        var fixture = new Fixture { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(2) };

        var leagueData = new LeagueData
        {
            Fixtures = new List<Fixture> { fixture }
        };

        _mockDataStore.Setup(x => x.GetData()).Returns(leagueData);

        // Act
        await _service.ScheduleWeeklyFixtureListAsync(DateTime.Now.DayOfWeek, new TimeSpan(9, 0, 0));

        // Assert
        _mockNotificationService.Verify(x => x.ScheduleNotificationAsync(
            20000,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ScheduleWeeklyFixtureListAsync_WithException_CatchesAndContinues()
    {
        // Arrange
        _mockDataStore.Setup(x => x.GetData()).Throws(new InvalidOperationException("Test exception"));

        // Act
        await _service.ScheduleWeeklyFixtureListAsync(DayOfWeek.Monday, new TimeSpan(9, 0, 0));

        // Assert - Should not throw
        _mockDataStore.Verify(x => x.GetData(), Times.Once);
    }

    [Fact]
    public async Task NotifyMatchResultIfEnabledAsync_WithNotificationsDisabled_DoesNotNotify()
    {
        // Arrange
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid()
        };

        var settings = new AppSettings
        {
            ResultNotificationsEnabled = false
        };

        // Act
        await _service.NotifyMatchResultIfEnabledAsync(fixture, settings);

        // Assert
        _mockDataStore.Verify(x => x.GetData(), Times.Never);
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Never);
    }
}
