using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Wdpl2;
using Wdpl2.Models;
using Wdpl2.Services.Import;
using Xunit;

namespace wdpl2.Tests;

/// <summary>
/// Tests for ParadoxDoubleImporter - validates parsing and importing of doubles from Paradox DB files.
/// Note: ImportToFixtures tests are limited because DataStore requires MAUI context.
/// We focus on testing error paths and ParseDoublesDb which doesn't require DataStore.
/// </summary>
public class ParadoxDoubleImporterTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public ParadoxDoubleImporterTests()
    {
        // No DataStore initialization - it requires MAUI context
    }

    public void Dispose()
    {
        // Clean up temporary test files
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    private void InitializeDataStore()
    {
        // Cannot initialize DataStore in unit tests - requires MAUI FileSystem
        // This method is kept for future reference if DataStore becomes testable
    }

    private string CreateTempFile(string content = "")
    {
        var tempFile = Path.GetTempFileName();
        _tempFiles.Add(tempFile);
        if (!string.IsNullOrEmpty(content))
        {
            File.WriteAllText(tempFile, content);
        }
        return tempFile;
    }

    [Fact]
    public void ParseDoublesDb_FileNotFound_ReturnsError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".db");

        // Act
        var result = ParadoxDoubleImporter.ParseDoublesDb(nonExistentPath);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("Doubles DB file not found", result.Errors[0]);
        Assert.Contains(nonExistentPath, result.Errors[0]);
    }



    [Fact]
    public void ParseDoublesDb_ExceptionDuringParsing_ReturnsError()
    {
        // Arrange
        var tempFile = CreateTempFile();
        // Write invalid/corrupted data that will cause parsing to fail
        File.WriteAllBytes(tempFile, new byte[] { 1, 2, 3 });

        // Act
        var result = ParadoxDoubleImporter.ParseDoublesDb(tempFile);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("Error parsing Dbls.DB", result.Errors[0]);
    }

    [Fact]
    public void ImportToFixtures_EmptyDoublesList_ReturnsSuccess()
    {
        // Arrange
        InitializeDataStore();
        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>();
        var matchMap = new Dictionary<int, Guid>();
        var playerMap = new Dictionary<int, Guid>();

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
    }

    [Fact]
    public void ImportToFixtures_MatchNotInMap_SkipsFrames()
    {
        // Arrange
        InitializeDataStore();
        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble
            {
                MatchNo = 1,
                FrameNumber = 1,
                Winner = "Home"
            }
        };
        var matchMap = new Dictionary<int, Guid>(); // Empty - no match mapping
        var playerMap = new Dictionary<int, Guid>();

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains("Match 1: Fixture not found", result.Warnings[0]);
    }

    [Fact]
    public void ImportToFixtures_FixtureNotInDataStore_SkipsFrames()
    {
        // Arrange
        InitializeDataStore();
        var fixtureId = Guid.NewGuid();
        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble
            {
                MatchNo = 1,
                FrameNumber = 1,
                Winner = "Home"
            }
        };
        var matchMap = new Dictionary<int, Guid> { { 1, fixtureId } };
        var playerMap = new Dictionary<int, Guid>();

        // Setup DataStore with empty fixtures
        DataStore.Data.Fixtures.Clear();

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains($"Match 1: Fixture GUID {fixtureId} not in database", result.Warnings[0]);
    }

    [Fact]
    public void ImportToFixtures_FrameAlreadyExists_SkipsFrame()
    {
        // Arrange
        InitializeDataStore();
        var fixtureId = Guid.NewGuid();
        var fixture = new Fixture
        {
            Id = fixtureId,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Frames = new List<FrameResult>
            {
                new FrameResult { Number = 9 } // Frame 9 already exists (8 singles + 1 double)
            }
        };
        DataStore.Data.Fixtures.Clear();
        DataStore.Data.Fixtures.Add(fixture);

        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble
            {
                MatchNo = 1,
                FrameNumber = 1, // Will become frame 9 with offset
                Winner = "Home"
            }
        };
        var matchMap = new Dictionary<int, Guid> { { 1, fixtureId } };
        var playerMap = new Dictionary<int, Guid>();

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap, singlesFrameCount: 8);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public void ImportToFixtures_ValidDouble_ImportsSuccessfully()
    {
        // Arrange
        InitializeDataStore();
        var fixtureId = Guid.NewGuid();
        var hp1Id = Guid.NewGuid();
        var hp2Id = Guid.NewGuid();
        var ap1Id = Guid.NewGuid();
        var ap2Id = Guid.NewGuid();

        var fixture = new Fixture
        {
            Id = fixtureId,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Frames = new List<FrameResult>()
        };
        DataStore.Data.Fixtures.Clear();
        DataStore.Data.Fixtures.Add(fixture);

        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble
            {
                MatchNo = 1,
                FrameNumber = 1,
                HomePlayer1No = 10,
                HomePlayer2No = 11,
                AwayPlayer1No = 20,
                AwayPlayer2No = 21,
                Winner = "Home",
                EightBall1 = true,
                EightBall2 = false
            }
        };
        var matchMap = new Dictionary<int, Guid> { { 1, fixtureId } };
        var playerMap = new Dictionary<int, Guid>
        {
            { 10, hp1Id },
            { 11, hp2Id },
            { 20, ap1Id },
            { 21, ap2Id }
        };

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap, singlesFrameCount: 8);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Single(fixture.Frames);

        var frame = fixture.Frames[0];
        Assert.Equal(9, frame.Number); // 8 + 1
        Assert.Equal(hp1Id, frame.HomePlayerId);
        Assert.Equal(ap1Id, frame.AwayPlayerId);
        Assert.Equal(hp2Id, frame.HomePlayer2Id);
        Assert.Equal(ap2Id, frame.AwayPlayer2Id);
        Assert.Equal(FrameWinner.Home, frame.Winner);
        Assert.True(frame.EightBall);
        Assert.True(frame.IsDoubles);
    }

    [Fact]
    public void ImportToFixtures_WinnerAway_SetsCorrectWinner()
    {
        // Arrange
        InitializeDataStore();
        var fixtureId = Guid.NewGuid();
        var fixture = new Fixture
        {
            Id = fixtureId,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Frames = new List<FrameResult>()
        };
        DataStore.Data.Fixtures.Clear();
        DataStore.Data.Fixtures.Add(fixture);

        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble
            {
                MatchNo = 1,
                FrameNumber = 1,
                Winner = "Away"
            }
        };
        var matchMap = new Dictionary<int, Guid> { { 1, fixtureId } };
        var playerMap = new Dictionary<int, Guid>();

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap);

        // Assert
        Assert.True(result.Success);
        Assert.Single(fixture.Frames);
        Assert.Equal(FrameWinner.Away, fixture.Frames[0].Winner);
    }

    [Fact]
    public void ImportToFixtures_WinnerUnknown_SetsNone()
    {
        // Arrange
        InitializeDataStore();
        var fixtureId = Guid.NewGuid();
        var fixture = new Fixture
        {
            Id = fixtureId,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Frames = new List<FrameResult>()
        };
        DataStore.Data.Fixtures.Clear();
        DataStore.Data.Fixtures.Add(fixture);

        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble
            {
                MatchNo = 1,
                FrameNumber = 1,
                Winner = "Unknown"
            }
        };
        var matchMap = new Dictionary<int, Guid> { { 1, fixtureId } };
        var playerMap = new Dictionary<int, Guid>();

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap);

        // Assert
        Assert.True(result.Success);
        Assert.Single(fixture.Frames);
        Assert.Equal(FrameWinner.None, fixture.Frames[0].Winner);
    }

    [Fact]
    public void ImportToFixtures_EightBall2True_SetsEightBallTrue()
    {
        // Arrange
        InitializeDataStore();
        var fixtureId = Guid.NewGuid();
        var fixture = new Fixture
        {
            Id = fixtureId,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Frames = new List<FrameResult>()
        };
        DataStore.Data.Fixtures.Clear();
        DataStore.Data.Fixtures.Add(fixture);

        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble
            {
                MatchNo = 1,
                FrameNumber = 1,
                Winner = "Home",
                EightBall1 = false,
                EightBall2 = true
            }
        };
        var matchMap = new Dictionary<int, Guid> { { 1, fixtureId } };
        var playerMap = new Dictionary<int, Guid>();

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap);

        // Assert
        Assert.True(result.Success);
        Assert.Single(fixture.Frames);
        Assert.True(fixture.Frames[0].EightBall);
    }

    [Fact]
    public void ImportToFixtures_PartialPlayerMapping_MapsAvailablePlayers()
    {
        // Arrange
        InitializeDataStore();
        var fixtureId = Guid.NewGuid();
        var hp1Id = Guid.NewGuid();
        var ap2Id = Guid.NewGuid();

        var fixture = new Fixture
        {
            Id = fixtureId,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Frames = new List<FrameResult>()
        };
        DataStore.Data.Fixtures.Clear();
        DataStore.Data.Fixtures.Add(fixture);

        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble
            {
                MatchNo = 1,
                FrameNumber = 1,
                HomePlayer1No = 10,
                HomePlayer2No = 11, // Not in map
                AwayPlayer1No = 20, // Not in map
                AwayPlayer2No = 21,
                Winner = "Home"
            }
        };
        var matchMap = new Dictionary<int, Guid> { { 1, fixtureId } };
        var playerMap = new Dictionary<int, Guid>
        {
            { 10, hp1Id },
            { 21, ap2Id }
        };

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap);

        // Assert
        Assert.True(result.Success);
        Assert.Single(fixture.Frames);
        var frame = fixture.Frames[0];
        Assert.Equal(hp1Id, frame.HomePlayerId);
        Assert.Null(frame.AwayPlayerId); // AwayPlayer1 not mapped
        Assert.Null(frame.HomePlayer2Id); // HomePlayer2 not mapped
        Assert.Equal(ap2Id, frame.AwayPlayer2Id);

        var importedDouble = doubles[0];
        Assert.Equal(hp1Id, importedDouble.MappedHomePlayer1Id);
        Assert.Null(importedDouble.MappedHomePlayer2Id);
        Assert.Null(importedDouble.MappedAwayPlayer1Id);
        Assert.Equal(ap2Id, importedDouble.MappedAwayPlayer2Id);
    }

    [Fact]
    public void ImportToFixtures_MultipleDoublesInMatch_ImportsAll()
    {
        // Arrange
        InitializeDataStore();
        var fixtureId = Guid.NewGuid();
        var fixture = new Fixture
        {
            Id = fixtureId,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Frames = new List<FrameResult>()
        };
        DataStore.Data.Fixtures.Clear();
        DataStore.Data.Fixtures.Add(fixture);

        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble { MatchNo = 1, FrameNumber = 1, Winner = "Home" },
            new ParadoxDoubleImporter.ImportedDouble { MatchNo = 1, FrameNumber = 2, Winner = "Away" },
            new ParadoxDoubleImporter.ImportedDouble { MatchNo = 1, FrameNumber = 3, Winner = "Home" }
        };
        var matchMap = new Dictionary<int, Guid> { { 1, fixtureId } };
        var playerMap = new Dictionary<int, Guid>();

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.ImportedCount);
        Assert.Equal(3, fixture.Frames.Count);
        Assert.Equal(9, fixture.Frames[0].Number);
        Assert.Equal(10, fixture.Frames[1].Number);
        Assert.Equal(11, fixture.Frames[2].Number);
    }

    [Fact]
    public void ImportToFixtures_CustomSinglesFrameCount_OffsetsCorrectly()
    {
        // Arrange
        InitializeDataStore();
        var fixtureId = Guid.NewGuid();
        var fixture = new Fixture
        {
            Id = fixtureId,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Frames = new List<FrameResult>()
        };
        DataStore.Data.Fixtures.Clear();
        DataStore.Data.Fixtures.Add(fixture);

        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble { MatchNo = 1, FrameNumber = 1, Winner = "Home" }
        };
        var matchMap = new Dictionary<int, Guid> { { 1, fixtureId } };
        var playerMap = new Dictionary<int, Guid>();

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap, singlesFrameCount: 10);

        // Assert
        Assert.True(result.Success);
        Assert.Single(fixture.Frames);
        Assert.Equal(11, fixture.Frames[0].Number); // 10 + 1
    }

    [Fact]
    public void ImportToFixtures_SetsMappedFixtureId()
    {
        // Arrange
        InitializeDataStore();
        var fixtureId = Guid.NewGuid();
        var fixture = new Fixture
        {
            Id = fixtureId,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Frames = new List<FrameResult>()
        };
        DataStore.Data.Fixtures.Clear();
        DataStore.Data.Fixtures.Add(fixture);

        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble { MatchNo = 1, FrameNumber = 1, Winner = "Home" }
        };
        var matchMap = new Dictionary<int, Guid> { { 1, fixtureId } };
        var playerMap = new Dictionary<int, Guid>();

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(fixtureId, doubles[0].MappedFixtureId);
    }

    [Fact]
    public void ImportToFixtures_ExceptionDuringImport_ReturnsError()
    {
        // Arrange - pass null doubles list to trigger exception
        List<ParadoxDoubleImporter.ImportedDouble> doubles = null!;
        var matchMap = new Dictionary<int, Guid>();
        var playerMap = new Dictionary<int, Guid>();

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("Error importing doubles", result.Errors[0]);
    }

    [Fact]
    public void ImportToFixtures_PlayerNoZero_SkipsMapping()
    {
        // Arrange
        InitializeDataStore();
        var fixtureId = Guid.NewGuid();
        var fixture = new Fixture
        {
            Id = fixtureId,
            HomeTeamId = Guid.NewGuid(),
            AwayTeamId = Guid.NewGuid(),
            Frames = new List<FrameResult>()
        };
        DataStore.Data.Fixtures.Clear();
        DataStore.Data.Fixtures.Add(fixture);

        var doubles = new List<ParadoxDoubleImporter.ImportedDouble>
        {
            new ParadoxDoubleImporter.ImportedDouble
            {
                MatchNo = 1,
                FrameNumber = 1,
                HomePlayer1No = 0, // Should be skipped
                HomePlayer2No = 0,
                AwayPlayer1No = 0,
                AwayPlayer2No = 0,
                Winner = "Home"
            }
        };
        var matchMap = new Dictionary<int, Guid> { { 1, fixtureId } };
        var playerMap = new Dictionary<int, Guid>
        {
            { 0, Guid.NewGuid() } // Even if 0 is in map, it should be skipped
        };

        // Act
        var result = ParadoxDoubleImporter.ImportToFixtures(doubles, matchMap, playerMap);

        // Assert
        Assert.True(result.Success);
        Assert.Single(fixture.Frames);
        var frame = fixture.Frames[0];
        Assert.Null(frame.HomePlayerId);
        Assert.Null(frame.AwayPlayerId);
        Assert.Null(frame.HomePlayer2Id);
        Assert.Null(frame.AwayPlayer2Id);
    }

    // Helper method to remove mock file creation since we removed those tests
}
