using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for SqliteDataStore — SQLite-based implementation of IDataStore using Entity Framework Core.
/// </summary>
public class SqliteDataStoreTests
{
    [Fact]
    public void Constructor_SetsContext()
    {
        // Arrange
        var mockContext = new Mock<LeagueContext>();

        // Act
        var store = new SqliteDataStore(mockContext.Object);

        // Assert
        Assert.NotNull(store);
    }

    [Fact]
    public async Task GetCompetitionsAsync_NullSeasonId_ReturnsEmptyList()
    {
        // Arrange
        var mockContext = new Mock<LeagueContext>();
        var store = new SqliteDataStore(mockContext.Object);

        // Act
        var result = await store.GetCompetitionsAsync(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCompetitionsAsync_ValidSeasonId_ReturnsFilteredCompetitions()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var competitions = new List<Competition>
        {
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Competition 1", CreatedDate = DateTime.UtcNow.AddDays(-2) },
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Competition 2", CreatedDate = DateTime.UtcNow.AddDays(-1) },
            new() { Id = Guid.NewGuid(), SeasonId = Guid.NewGuid(), Name = "Competition 3", CreatedDate = DateTime.UtcNow }
        };

        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Competitions.AddRangeAsync(competitions);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        var result = await store.GetCompetitionsAsync(seasonId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal(seasonId, c.SeasonId));
        Assert.Equal("Competition 2", result[0].Name);
        Assert.Equal("Competition 1", result[1].Name);
    }

    [Fact]
    public async Task GetCompetitionsAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetCompetitionsAsync(seasonId, cts.Token));
    }

    [Fact]
    public async Task AddCompetitionAsync_AddsAndSavesCompetition()
    {
        // Arrange
        var competition = new Competition { Id = Guid.NewGuid(), Name = "Test Competition", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);

        // Act
        await store.AddCompetitionAsync(competition);

        // Assert
        var saved = await context.Competitions.FindAsync(competition.Id);
        Assert.NotNull(saved);
        Assert.Equal(competition.Name, saved.Name);
    }

    [Fact]
    public async Task UpdateCompetitionAsync_DeletesAndReinserts()
    {
        // Arrange
        var competition = new Competition { Id = Guid.NewGuid(), Name = "Original", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new LeagueContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        await context.Competitions.AddAsync(competition);
        await context.SaveChangesAsync();

        competition.Name = "Updated";

        var store = new SqliteDataStore(context);

        // Act
        await store.UpdateCompetitionAsync(competition);

        // Assert
        var updated = await context.Competitions.FindAsync(competition.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
    }

    [Fact]
    public async Task UpdateCompetitionAsync_ExceptionDuringUpdate_RollsBackTransaction()
    {
        // Arrange - Use SQLite in-memory database with forced constraint violation
        var competition = new Competition { Id = Guid.NewGuid(), Name = "Updated Competition", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new LeagueContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        // Add initial competition
        await context.Competitions.AddAsync(new Competition { Id = competition.Id, Name = "Original", SeasonId = competition.SeasonId });
        await context.SaveChangesAsync();

        // Create a store and simulate an error by disposing the connection mid-operation
        var store = new SqliteDataStore(context);
        
        // Close connection to force an error during the transaction
        await context.Database.CloseConnectionAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await store.UpdateCompetitionAsync(competition));

        // Verify original data is still there (rollback happened)
        await context.Database.OpenConnectionAsync();
        var existing = await context.Competitions.FindAsync(competition.Id);
        Assert.NotNull(existing);
        Assert.Equal("Original", existing.Name);
    }

    [Fact]
    public async Task DeleteCompetitionAsync_CompetitionExists_RemovesAndSaves()
    {
        // Arrange
        var competition = new Competition { Id = Guid.NewGuid(), Name = "To Delete", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Competitions.AddAsync(competition);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        await store.DeleteCompetitionAsync(competition);

        // Assert
        var deleted = await context.Competitions.FindAsync(competition.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteCompetitionAsync_CompetitionNotFound_DoesNotThrow()
    {
        // Arrange
        var competition = new Competition { Id = Guid.NewGuid(), Name = "Non-existent", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);

        // Act
        await store.DeleteCompetitionAsync(competition);

        // Assert
        var result = await context.Competitions.FindAsync(competition.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPlayersAsync_NullSeasonId_ReturnsEmptyList()
    {
        // Arrange
        var mockContext = new Mock<LeagueContext>();
        var store = new SqliteDataStore(mockContext.Object);

        // Act
        var result = await store.GetPlayersAsync(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPlayersAsync_ValidSeasonId_ReturnsFilteredPlayers()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var players = new List<Player>
        {
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, FirstName = "John", LastName = "Smith" },
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, FirstName = "Jane", LastName = "Doe" },
            new() { Id = Guid.NewGuid(), SeasonId = Guid.NewGuid(), FirstName = "Bob", LastName = "Jones" }
        };

        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Players.AddRangeAsync(players);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        var result = await store.GetPlayersAsync(seasonId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Equal(seasonId, p.SeasonId));
        Assert.Equal("Doe", result[0].LastName);
        Assert.Equal("Smith", result[1].LastName);
    }

    [Fact]
    public async Task GetPlayersAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetPlayersAsync(seasonId, cts.Token));
    }

    [Fact]
    public async Task AddPlayerAsync_AddsAndSavesPlayer()
    {
        // Arrange
        var player = new Player { Id = Guid.NewGuid(), FirstName = "Test", LastName = "Player", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);

        // Act
        await store.AddPlayerAsync(player);

        // Assert
        var saved = await context.Players.FindAsync(player.Id);
        Assert.NotNull(saved);
        Assert.Equal(player.FirstName, saved.FirstName);
        Assert.Equal(player.LastName, saved.LastName);
    }

    [Fact]
    public async Task UpdatePlayerAsync_UpdatesAndSavesPlayer()
    {
        // Arrange
        var player = new Player { Id = Guid.NewGuid(), FirstName = "Original", LastName = "Name", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Players.AddAsync(player);
        await context.SaveChangesAsync();

        player.FirstName = "Updated";

        var store = new SqliteDataStore(context);

        // Act
        await store.UpdatePlayerAsync(player);

        // Assert
        var updated = await context.Players.FindAsync(player.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.FirstName);
    }

    [Fact]
    public async Task DeletePlayerAsync_PlayerExists_RemovesAndSaves()
    {
        // Arrange
        var player = new Player { Id = Guid.NewGuid(), FirstName = "To", LastName = "Delete", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Players.AddAsync(player);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        await store.DeletePlayerAsync(player);

        // Assert
        var deleted = await context.Players.FindAsync(player.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetTeamsAsync_NullSeasonId_ReturnsEmptyList()
    {
        // Arrange
        var mockContext = new Mock<LeagueContext>();
        var store = new SqliteDataStore(mockContext.Object);

        // Act
        var result = await store.GetTeamsAsync(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTeamsAsync_ValidSeasonId_ReturnsFilteredTeams()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var teams = new List<Team>
        {
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Team B" },
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Team A" },
            new() { Id = Guid.NewGuid(), SeasonId = Guid.NewGuid(), Name = "Team C" }
        };

        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Teams.AddRangeAsync(teams);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        var result = await store.GetTeamsAsync(seasonId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(seasonId, t.SeasonId));
        Assert.Equal("Team A", result[0].Name);
        Assert.Equal("Team B", result[1].Name);
    }

    [Fact]
    public async Task GetTeamsAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetTeamsAsync(seasonId, cts.Token));
    }

    [Fact]
    public async Task AddTeamAsync_AddsAndSavesTeam()
    {
        // Arrange
        var team = new Team { Id = Guid.NewGuid(), Name = "Test Team", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);

        // Act
        await store.AddTeamAsync(team);

        // Assert
        var saved = await context.Teams.FindAsync(team.Id);
        Assert.NotNull(saved);
        Assert.Equal(team.Name, saved.Name);
    }

    [Fact]
    public async Task UpdateTeamAsync_UpdatesAndSavesTeam()
    {
        // Arrange
        var team = new Team { Id = Guid.NewGuid(), Name = "Original", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Teams.AddAsync(team);
        await context.SaveChangesAsync();

        team.Name = "Updated";

        var store = new SqliteDataStore(context);

        // Act
        await store.UpdateTeamAsync(team);

        // Assert
        var updated = await context.Teams.FindAsync(team.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
    }

    [Fact]
    public async Task DeleteTeamAsync_TeamExists_RemovesAndSaves()
    {
        // Arrange
        var team = new Team { Id = Guid.NewGuid(), Name = "To Delete", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Teams.AddAsync(team);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        await store.DeleteTeamAsync(team);

        // Assert
        var deleted = await context.Teams.FindAsync(team.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetVenuesAsync_NullSeasonId_ReturnsEmptyList()
    {
        // Arrange
        var mockContext = new Mock<LeagueContext>();
        var store = new SqliteDataStore(mockContext.Object);

        // Act
        var result = await store.GetVenuesAsync(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetVenuesAsync_ValidSeasonId_ReturnsFilteredVenues()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var venues = new List<Venue>
        {
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Venue B" },
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Venue A" },
            new() { Id = Guid.NewGuid(), SeasonId = Guid.NewGuid(), Name = "Venue C" }
        };

        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Venues.AddRangeAsync(venues);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        var result = await store.GetVenuesAsync(seasonId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, v => Assert.Equal(seasonId, v.SeasonId));
        Assert.Equal("Venue A", result[0].Name);
        Assert.Equal("Venue B", result[1].Name);
    }

    [Fact]
    public async Task GetVenuesAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetVenuesAsync(seasonId, cts.Token));
    }

    [Fact]
    public async Task AddVenueAsync_AddsAndSavesVenue()
    {
        // Arrange
        var venue = new Venue { Id = Guid.NewGuid(), Name = "Test Venue", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);

        // Act
        await store.AddVenueAsync(venue);

        // Assert
        var saved = await context.Venues.FindAsync(venue.Id);
        Assert.NotNull(saved);
        Assert.Equal(venue.Name, saved.Name);
    }

    [Fact]
    public async Task UpdateVenueAsync_UpdatesAndSavesVenue()
    {
        // Arrange — UpdateVenueAsync uses raw SQL + a transaction (JSON-owned
        // collection workaround), so it needs a relational provider.
        var season = new Season { Id = Guid.NewGuid(), Name = "S1" };
        var venue = new Venue { Id = Guid.NewGuid(), Name = "Original", SeasonId = season.Id };
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new LeagueContext(options);
        await context.Database.EnsureCreatedAsync();
        await context.Seasons.AddAsync(season);
        await context.Venues.AddAsync(venue);
        await context.SaveChangesAsync();

        venue.Name = "Updated";

        var store = new SqliteDataStore(context);

        // Act
        await store.UpdateVenueAsync(venue);

        // Assert
        var updated = await context.Venues.FindAsync(venue.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
    }

    [Fact]
    public async Task DeleteVenueAsync_VenueExists_RemovesAndSaves()
    {
        // Arrange
        var venue = new Venue { Id = Guid.NewGuid(), Name = "To Delete", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Venues.AddAsync(venue);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        await store.DeleteVenueAsync(venue);

        // Assert
        var deleted = await context.Venues.FindAsync(venue.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetDivisionsAsync_NullSeasonId_ReturnsEmptyList()
    {
        // Arrange
        var mockContext = new Mock<LeagueContext>();
        var store = new SqliteDataStore(mockContext.Object);

        // Act
        var result = await store.GetDivisionsAsync(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDivisionsAsync_ValidSeasonId_ReturnsFilteredDivisions()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var divisions = new List<Division>
        {
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Division B" },
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Division A" },
            new() { Id = Guid.NewGuid(), SeasonId = Guid.NewGuid(), Name = "Division C" }
        };

        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Divisions.AddRangeAsync(divisions);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        var result = await store.GetDivisionsAsync(seasonId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, d => Assert.Equal(seasonId, d.SeasonId));
        Assert.Equal("Division A", result[0].Name);
        Assert.Equal("Division B", result[1].Name);
    }

    [Fact]
    public async Task GetDivisionsAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetDivisionsAsync(seasonId, cts.Token));
    }

    [Fact]
    public async Task AddDivisionAsync_AddsAndSavesDivision()
    {
        // Arrange
        var division = new Division { Id = Guid.NewGuid(), Name = "Test Division", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);

        // Act
        await store.AddDivisionAsync(division);

        // Assert
        var saved = await context.Divisions.FindAsync(division.Id);
        Assert.NotNull(saved);
        Assert.Equal(division.Name, saved.Name);
    }

    [Fact]
    public async Task UpdateDivisionAsync_UpdatesAndSavesDivision()
    {
        // Arrange
        var division = new Division { Id = Guid.NewGuid(), Name = "Original", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Divisions.AddAsync(division);
        await context.SaveChangesAsync();

        division.Name = "Updated";

        var store = new SqliteDataStore(context);

        // Act
        await store.UpdateDivisionAsync(division);

        // Assert
        var updated = await context.Divisions.FindAsync(division.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
    }

    [Fact]
    public async Task DeleteDivisionAsync_DivisionExists_RemovesAndSaves()
    {
        // Arrange
        var division = new Division { Id = Guid.NewGuid(), Name = "To Delete", SeasonId = Guid.NewGuid() };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Divisions.AddAsync(division);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        await store.DeleteDivisionAsync(division);

        // Assert
        var deleted = await context.Divisions.FindAsync(division.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetFixturesAsync_NullSeasonId_ReturnsEmptyList()
    {
        // Arrange
        var mockContext = new Mock<LeagueContext>();
        var store = new SqliteDataStore(mockContext.Object);

        // Act
        var result = await store.GetFixturesAsync(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFixturesAsync_ValidSeasonId_ReturnsFilteredFixtures()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var fixtures = new List<Fixture>
        {
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, Date = DateTime.UtcNow.AddDays(2) },
            new() { Id = Guid.NewGuid(), SeasonId = seasonId, Date = DateTime.UtcNow.AddDays(1) },
            new() { Id = Guid.NewGuid(), SeasonId = Guid.NewGuid(), Date = DateTime.UtcNow }
        };

        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Fixtures.AddRangeAsync(fixtures);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        var result = await store.GetFixturesAsync(seasonId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.Equal(seasonId, f.SeasonId));
        Assert.True(result[0].Date < result[1].Date);
    }

    [Fact]
    public async Task GetFixturesAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetFixturesAsync(seasonId, cts.Token));
    }

    [Fact]
    public async Task AddFixtureAsync_AddsAndSavesFixture()
    {
        // Arrange
        var fixture = new Fixture { Id = Guid.NewGuid(), SeasonId = Guid.NewGuid(), Date = DateTime.UtcNow };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);

        // Act
        await store.AddFixtureAsync(fixture);

        // Assert
        var saved = await context.Fixtures.FindAsync(fixture.Id);
        Assert.NotNull(saved);
        Assert.Equal(fixture.SeasonId, saved.SeasonId);
    }

    [Fact]
    public async Task UpdateFixtureAsync_UpdatesAndSavesFixture()
    {
        // Arrange — UpdateFixtureAsync uses raw SQL + a transaction (JSON-owned
        // collection workaround), so it needs a relational provider.
        var season = new Season { Id = Guid.NewGuid(), Name = "S1" };
        var home = new Team { Id = Guid.NewGuid(), SeasonId = season.Id, Name = "Home" };
        var away = new Team { Id = Guid.NewGuid(), SeasonId = season.Id, Name = "Away" };
        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            Date = DateTime.UtcNow
        };
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new LeagueContext(options);
        await context.Database.EnsureCreatedAsync();
        await context.Seasons.AddAsync(season);
        await context.Teams.AddRangeAsync(home, away);
        await context.Fixtures.AddAsync(fixture);
        await context.SaveChangesAsync();

        fixture.Date = DateTime.UtcNow.AddDays(1);

        var store = new SqliteDataStore(context);

        // Act
        await store.UpdateFixtureAsync(fixture);

        // Assert
        var updated = await context.Fixtures.FindAsync(fixture.Id);
        Assert.NotNull(updated);
        Assert.Equal(fixture.Date, updated.Date);
    }

    [Fact]
    public async Task DeleteFixtureAsync_FixtureExists_RemovesAndSaves()
    {
        // Arrange
        var fixture = new Fixture { Id = Guid.NewGuid(), SeasonId = Guid.NewGuid(), Date = DateTime.UtcNow };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Fixtures.AddAsync(fixture);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        await store.DeleteFixtureAsync(fixture);

        // Assert
        var deleted = await context.Fixtures.FindAsync(fixture.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetSeasonsAsync_NoSeasons_ReturnsEmptyList()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);

        // Act
        var result = await store.GetSeasonsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSeasonsAsync_HasSeasons_ReturnsOrderedByStartDateDescending()
    {
        // Arrange
        var seasons = new List<Season>
        {
            new() { Id = Guid.NewGuid(), Name = "Season 1", StartDate = DateTime.UtcNow.AddDays(-30) },
            new() { Id = Guid.NewGuid(), Name = "Season 2", StartDate = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Season 3", StartDate = DateTime.UtcNow.AddDays(-60) }
        };

        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Seasons.AddRangeAsync(seasons);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        var result = await store.GetSeasonsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("Season 2", result[0].Name);
        Assert.Equal("Season 1", result[1].Name);
        Assert.Equal("Season 3", result[2].Name);
    }

    [Fact]
    public async Task GetSeasonsAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetSeasonsAsync(cts.Token));
    }

    [Fact]
    public async Task AddSeasonAsync_AddsAndSavesSeason()
    {
        // Arrange
        var season = new Season { Id = Guid.NewGuid(), Name = "Test Season", StartDate = DateTime.UtcNow };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);

        // Act
        await store.AddSeasonAsync(season);

        // Assert
        var saved = await context.Seasons.FindAsync(season.Id);
        Assert.NotNull(saved);
        Assert.Equal(season.Name, saved.Name);
    }

    [Fact]
    public async Task UpdateSeasonAsync_UpdatesAndSavesSeason()
    {
        // Arrange
        var season = new Season { Id = Guid.NewGuid(), Name = "Original", StartDate = DateTime.UtcNow };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Seasons.AddAsync(season);
        await context.SaveChangesAsync();

        season.Name = "Updated";

        var store = new SqliteDataStore(context);

        // Act
        await store.UpdateSeasonAsync(season);

        // Assert
        var updated = await context.Seasons.FindAsync(season.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
    }

    [Fact]
    public async Task DeleteSeasonAsync_SeasonExists_RemovesSeasonAndCascadeDeletesRelatedEntities()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var season = new Season { Id = seasonId, Name = "To Delete", StartDate = DateTime.UtcNow };
        var competition = new Competition { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Competition 1" };
        var division = new Division { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Division 1" };
        var venue = new Venue { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Venue 1" };
        var team = new Team { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "Team 1" };
        var player = new Player { Id = Guid.NewGuid(), SeasonId = seasonId, FirstName = "John", LastName = "Doe" };
        var fixture = new Fixture { Id = Guid.NewGuid(), SeasonId = seasonId, Date = DateTime.UtcNow };

        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Seasons.AddAsync(season);
        await context.Competitions.AddAsync(competition);
        await context.Divisions.AddAsync(division);
        await context.Venues.AddAsync(venue);
        await context.Teams.AddAsync(team);
        await context.Players.AddAsync(player);
        await context.Fixtures.AddAsync(fixture);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        await store.DeleteSeasonAsync(season);

        // Assert
        var deletedSeason = await context.Seasons.FindAsync(seasonId);
        Assert.Null(deletedSeason);
        var deletedCompetition = await context.Competitions.FindAsync(competition.Id);
        Assert.Null(deletedCompetition);
        var deletedDivision = await context.Divisions.FindAsync(division.Id);
        Assert.Null(deletedDivision);
        var deletedVenue = await context.Venues.FindAsync(venue.Id);
        Assert.Null(deletedVenue);
        var deletedTeam = await context.Teams.FindAsync(team.Id);
        Assert.Null(deletedTeam);
        var deletedPlayer = await context.Players.FindAsync(player.Id);
        Assert.Null(deletedPlayer);
        var deletedFixture = await context.Fixtures.FindAsync(fixture.Id);
        Assert.Null(deletedFixture);
    }

    [Fact]
    public async Task DeleteSeasonAsync_SeasonWithNoRelatedEntities_RemovesSeason()
    {
        // Arrange
        var season = new Season { Id = Guid.NewGuid(), Name = "To Delete", StartDate = DateTime.UtcNow };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        await context.Seasons.AddAsync(season);
        await context.SaveChangesAsync();

        var store = new SqliteDataStore(context);

        // Act
        await store.DeleteSeasonAsync(season);

        // Assert
        var deleted = await context.Seasons.FindAsync(season.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task SaveAsync_SavesChangesToContext()
    {
        // Arrange
        var season = new Season { Id = Guid.NewGuid(), Name = "Test Season", StartDate = DateTime.UtcNow };
        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        context.Seasons.Add(season);

        var store = new SqliteDataStore(context);

        // Act
        await store.SaveAsync();

        // Assert
        var saved = await context.Seasons.FindAsync(season.Id);
        Assert.NotNull(saved);
        Assert.Equal(season.Name, saved.Name);
    }

    [Fact]
    public void GetData_ReturnsAllEntitiesFromContext()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var divisionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var fixtureId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();

        var season = new Season { Id = seasonId, Name = "Test Season", StartDate = DateTime.UtcNow };
        var division = new Division { Id = divisionId, Name = "Test Division", SeasonId = seasonId };
        var team = new Team { Id = teamId, Name = "Test Team", SeasonId = seasonId };
        var player = new Player { Id = playerId, FirstName = "Test", LastName = "Player", SeasonId = seasonId };
        var venue = new Venue { Id = venueId, Name = "Test Venue", SeasonId = seasonId };
        var fixture = new Fixture { Id = fixtureId, SeasonId = seasonId };
        var competition = new Competition { Id = competitionId, Name = "Test Competition", SeasonId = seasonId };

        var options = new DbContextOptionsBuilder<LeagueContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new LeagueContext(options);
        context.Seasons.Add(season);
        context.Divisions.Add(division);
        context.Teams.Add(team);
        context.Players.Add(player);
        context.Venues.Add(venue);
        context.Fixtures.Add(fixture);
        context.Competitions.Add(competition);
        context.SaveChanges();

        var store = new SqliteDataStore(context);

        // Act
        var result = store.GetData();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Seasons);
        Assert.Equal(seasonId, result.Seasons[0].Id);
        Assert.Single(result.Divisions);
        Assert.Equal(divisionId, result.Divisions[0].Id);
        Assert.Single(result.Teams);
        Assert.Equal(teamId, result.Teams[0].Id);
        Assert.Single(result.Players);
        Assert.Equal(playerId, result.Players[0].Id);
        Assert.Single(result.Venues);
        Assert.Equal(venueId, result.Venues[0].Id);
        Assert.Single(result.Fixtures);
        Assert.Equal(fixtureId, result.Fixtures[0].Id);
        Assert.Single(result.Competitions);
        Assert.Equal(competitionId, result.Competitions[0].Id);
    }
}
