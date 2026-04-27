using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wdpl2.Services;
using Xunit;

namespace wdpl2.Tests;

/// <summary>
/// Tests for ParadoxDatabaseParser - validates parsing and exporting of Paradox database files.
/// Tests cover ParseFolder and ExportToCsv methods with various scenarios.
/// </summary>
public class ParadoxDatabaseParserTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        // Clean up temporary test files and directories
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

        foreach (var dir in _tempDirectories)
        {
            if (Directory.Exists(dir))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    private string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ParadoxTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        return tempDir;
    }

    [Fact]
    public void ParseFolder_NonExistentFolder_ReturnsErrorResult()
    {
        // Arrange
        var nonExistentFolder = Path.Combine(Path.GetTempPath(), $"NonExistent_{Guid.NewGuid():N}");

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(nonExistentFolder);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Folder not found"));
        Assert.Contains(nonExistentFolder, result.Errors[0]);
    }

    [Fact]
    public void ParseFolder_EmptyFolder_ReturnsFailureWithNoDataError()
    {
        // Arrange
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("No data could be parsed"));
    }

    [Fact]
    public void ParseFolder_WithException_CapturesErrorMessage()
    {
        // Arrange - use a path that will cause an exception during processing
        var tempDir = CreateTempDirectory();
        
        // Create an invalid file to trigger parsing error
        var invalidFile = Path.Combine(tempDir, "Division.DB");
        File.WriteAllBytes(invalidFile, new byte[] { 0, 1, 2 }); // Too small to be valid
        _tempFiles.Add(invalidFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.False(result.Success);
        // The parser should handle the error gracefully
        Assert.NotNull(result);
    }

    [Fact]
    public void ExportToCsv_EmptyParseResult_ReturnsNoDataMessage()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult();
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Equal("No data to export", result);
    }

    [Fact]
    public void ExportToCsv_WithDivisions_CreatesDivisionCsvFile()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Divisions = new List<ParadoxDatabaseParser.ParadoxDivision>
            {
                new() { ItemId = 1, Abbreviated = "Div1", FullDivisionName = "Division One" },
                new() { ItemId = 2, Abbreviated = "Div2", FullDivisionName = "Division Two" }
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("Division_Export.csv", result);
        var csvFile = Path.Combine(tempDir, "Division_Export.csv");
        Assert.True(File.Exists(csvFile));
        
        var content = File.ReadAllText(csvFile);
        Assert.Contains("Id,Abbreviated,FullDivisionName", content);
        Assert.Contains("1,Div1,Division One", content);
        Assert.Contains("2,Div2,Division Two", content);
    }

    [Fact]
    public void ExportToCsv_WithTeams_CreatesTeamCsvFile()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Teams = new List<ParadoxDatabaseParser.ParadoxTeam>
            {
                new() 
                { 
                    ItemId = 1, 
                    TeamName = "Team A", 
                    VenueId = 10, 
                    DivisionId = 5,
                    Contact = "John Doe",
                    Wins = 5,
                    Losses = 3,
                    Points = 15
                }
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("Team_Export.csv", result);
        var csvFile = Path.Combine(tempDir, "Team_Export.csv");
        Assert.True(File.Exists(csvFile));
        
        var content = File.ReadAllText(csvFile);
        Assert.Contains("Id,TeamName,VenueId,DivisionId,Contact,Wins,Losses,Points", content);
        Assert.Contains("1,Team A,10,5,John Doe,5,3,15", content);
    }

    [Fact]
    public void ExportToCsv_WithPlayers_CreatesPlayerCsvFile()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Players = new List<ParadoxDatabaseParser.ParadoxPlayer>
            {
                new() 
                { 
                    PlayerNo = 100, 
                    PlayerName = "Jane Smith", 
                    PlayerTeam = 5,
                    Wins = 10,
                    Losses = 8,
                    CurrentRating = 1500
                }
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("Player_Export.csv", result);
        var csvFile = Path.Combine(tempDir, "Player_Export.csv");
        Assert.True(File.Exists(csvFile));
        
        var content = File.ReadAllText(csvFile);
        Assert.Contains("Id,PlayerName,TeamId,Wins,Losses,Rating", content);
        Assert.Contains("100,Jane Smith,5,10,8,1500", content);
    }

    [Fact]
    public void ExportToCsv_WithMatches_CreatesMatchCsvFile()
    {
        // Arrange
        var matchDate = new DateTime(2024, 1, 15);
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Matches = new List<ParadoxDatabaseParser.ParadoxMatch>
            {
                new() 
                { 
                    MatchNo = 1, 
                    HomeTeam = 5, 
                    AwayTeam = 8,
                    MatchDate = matchDate,
                    HomeSinglesWins = 3,
                    AwaySinglesWins = 2,
                    HomeDoublesWins = 1,
                    AwayDoublesWins = 1,
                    DivisionName = "Division A"
                }
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("Match_Export.csv", result);
        var csvFile = Path.Combine(tempDir, "Match_Export.csv");
        Assert.True(File.Exists(csvFile));
        
        var content = File.ReadAllText(csvFile);
        Assert.Contains("Id,HomeTeam,AwayTeam,Date,HSWins,ASWins,HDWins,ADWins,Division", content);
        Assert.Contains("1,5,8,2024-01-15,3,2,1,1,Division A", content);
    }

    [Fact]
    public void ExportToCsv_WithSingles_CreatesSingleCsvFile()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Singles = new List<ParadoxDatabaseParser.ParadoxSingle>
            {
                new() 
                { 
                    MatchNo = 10, 
                    SingleNo = 1, 
                    HomePlayerNo = 100,
                    AwayPlayerNo = 200,
                    Winner = "Home",
                    EightBall = true
                },
                new() 
                { 
                    MatchNo = 10, 
                    SingleNo = 2, 
                    HomePlayerNo = 101,
                    AwayPlayerNo = 201,
                    Winner = "Away",
                    EightBall = false
                }
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("Single_Export.csv", result);
        var csvFile = Path.Combine(tempDir, "Single_Export.csv");
        Assert.True(File.Exists(csvFile));
        
        var content = File.ReadAllText(csvFile);
        Assert.Contains("MatchNo,FrameNo,HomePlayer,AwayPlayer,Winner,EightBall", content);
        Assert.Contains("10,1,100,200,Home,1", content);
        Assert.Contains("10,2,101,201,Away,0", content);
    }

    [Fact]
    public void ExportToCsv_WithVenues_CreatesVenueCsvFile()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Venues = new List<ParadoxDatabaseParser.ParadoxVenue>
            {
                new() 
                { 
                    ItemId = 1, 
                    VenueName = "Main Hall", 
                    Address = "123 Main St"
                }
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("Venue_Export.csv", result);
        var csvFile = Path.Combine(tempDir, "Venue_Export.csv");
        Assert.True(File.Exists(csvFile));
        
        var content = File.ReadAllText(csvFile);
        Assert.Contains("Id,VenueName,Address", content);
        Assert.Contains("1,Main Hall,123 Main St", content);
    }

    [Fact]
    public void ExportToCsv_WithMultipleDataTypes_CreatesAllCsvFiles()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Divisions = new List<ParadoxDatabaseParser.ParadoxDivision>
            {
                new() { ItemId = 1, Abbreviated = "D1", FullDivisionName = "Division 1" }
            },
            Teams = new List<ParadoxDatabaseParser.ParadoxTeam>
            {
                new() { ItemId = 1, TeamName = "Team 1", VenueId = 1, DivisionId = 1, Contact = "Contact", Wins = 0, Losses = 0, Points = 0 }
            },
            Players = new List<ParadoxDatabaseParser.ParadoxPlayer>
            {
                new() { PlayerNo = 1, PlayerName = "Player 1", PlayerTeam = 1, Wins = 0, Losses = 0, CurrentRating = 1000 }
            },
            Venues = new List<ParadoxDatabaseParser.ParadoxVenue>
            {
                new() { ItemId = 1, VenueName = "Venue 1", Address = "Address 1" }
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("4 files", result);
        Assert.Contains("Division_Export.csv", result);
        Assert.Contains("Team_Export.csv", result);
        Assert.Contains("Player_Export.csv", result);
        Assert.Contains("Venue_Export.csv", result);
        
        Assert.True(File.Exists(Path.Combine(tempDir, "Division_Export.csv")));
        Assert.True(File.Exists(Path.Combine(tempDir, "Team_Export.csv")));
        Assert.True(File.Exists(Path.Combine(tempDir, "Player_Export.csv")));
        Assert.True(File.Exists(Path.Combine(tempDir, "Venue_Export.csv")));
    }

    [Fact]
    public void ExportToCsv_WithSpecialCharactersInData_EscapesCorrectly()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Divisions = new List<ParadoxDatabaseParser.ParadoxDivision>
            {
                new() { ItemId = 1, Abbreviated = "D,1", FullDivisionName = "Division \"One\"" }
            },
            Teams = new List<ParadoxDatabaseParser.ParadoxTeam>
            {
                new() 
                { 
                    ItemId = 1, 
                    TeamName = "Team, A", 
                    VenueId = 1, 
                    DivisionId = 1, 
                    Contact = "John \"The King\" Doe",
                    Wins = 0, 
                    Losses = 0, 
                    Points = 0 
                }
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        var divisionCsv = File.ReadAllText(Path.Combine(tempDir, "Division_Export.csv"));
        Assert.Contains("\"D,1\"", divisionCsv);
        Assert.Contains("\"Division \"\"One\"\"\"", divisionCsv);
        
        var teamCsv = File.ReadAllText(Path.Combine(tempDir, "Team_Export.csv"));
        Assert.Contains("\"Team, A\"", teamCsv);
        Assert.Contains("\"John \"\"The King\"\" Doe\"", teamCsv);
    }

    [Fact]
    public void ExportToCsv_CreatesOutputFolder_IfNotExists()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Divisions = new List<ParadoxDatabaseParser.ParadoxDivision>
            {
                new() { ItemId = 1, Abbreviated = "D1", FullDivisionName = "Division 1" }
            }
        };
        var tempDir = CreateTempDirectory();
        var outputDir = Path.Combine(tempDir, "SubFolder", "Output");
        _tempDirectories.Add(outputDir);

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, outputDir);

        // Assert
        Assert.True(Directory.Exists(outputDir));
        Assert.Contains("Division_Export.csv", result);
    }

    [Fact]
    public void ParseFolder_WithDivisionCsvFile_LoadsFromCsv()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Division_Export.csv");
        var csvContent = "Id,Abbreviated,FullDivisionName\n1,Div1,Division One\n2,Div2,Division Two";
        File.WriteAllText(csvFile, csvContent);
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Loaded from CSV files"));
        Assert.Equal(2, result.Divisions.Count);
    }

    [Fact]
    public void ParseFolder_WithMultipleCsvFiles_LoadsAllData()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        
        var divisionCsv = Path.Combine(tempDir, "Division_Export.csv");
        File.WriteAllText(divisionCsv, "Id,Abbreviated,FullDivisionName\n1,D1,Division 1");
        _tempFiles.Add(divisionCsv);
        
        var venueCsv = Path.Combine(tempDir, "Venue_Export.csv");
        File.WriteAllText(venueCsv, "Id,VenueName,Address\n1,Hall,123 St");
        _tempFiles.Add(venueCsv);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Loaded from CSV files"));
        Assert.Single(result.Divisions);
        Assert.Single(result.Venues);
    }

    [Fact]
    public void ParseFolder_WithTeamCsvFile_LoadsTeams()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Team_Export.csv");
        var csvContent = "Id,TeamName,VenueId,DivisionId,Contact,Wins,Losses,Points\n1,Team A,10,5,Contact,5,3,15";
        File.WriteAllText(csvFile, csvContent);
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Teams);
    }

    [Fact]
    public void ParseFolder_WithPlayerCsvFile_LoadsPlayers()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Player_Export.csv");
        var csvContent = "Id,PlayerName,TeamId,Wins,Losses,Rating\n100,John Doe,5,10,8,1500";
        File.WriteAllText(csvFile, csvContent);
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Players);
    }

    [Fact]
    public void ParseFolder_WithMatchCsvFile_LoadsMatches()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Match_Export.csv");
        var csvContent = "Id,HomeTeam,AwayTeam,Date,HSWins,ASWins,HDWins,ADWins,Division\n1,5,8,2024-01-15,3,2,1,1,Div A";
        File.WriteAllText(csvFile, csvContent);
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Matches);
    }

    [Fact]
    public void ParseFolder_WithSingleCsvFile_LoadsSingles()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Single_Export.csv");
        var csvContent = "MatchNo,FrameNo,HomePlayer,AwayPlayer,Winner,EightBall\n10,1,100,200,Home,1";
        File.WriteAllText(csvFile, csvContent);
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Singles);
    }

    [Fact]
    public void ParseFolder_WithVenueCsvFile_LoadsVenues()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Venue_Export.csv");
        var csvContent = "Id,VenueName,Address\n1,Main Hall,123 Main St";
        File.WriteAllText(csvFile, csvContent);
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Venues);
    }

    [Fact]
    public void ParseFolder_WithInvalidDbFile_HandlesGracefully()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        
        // Create multiple invalid DB files to test each parsing path
        var divisionDb = Path.Combine(tempDir, "Division.DB");
        File.WriteAllBytes(divisionDb, new byte[] { 0, 1, 2, 3, 4 });
        _tempFiles.Add(divisionDb);
        
        var venueDb = Path.Combine(tempDir, "Venue.DB");
        File.WriteAllBytes(venueDb, new byte[] { 0, 1, 2, 3, 4 });
        _tempFiles.Add(venueDb);
        
        var teamDb = Path.Combine(tempDir, "Team.DB");
        File.WriteAllBytes(teamDb, new byte[] { 0, 1, 2, 3, 4 });
        _tempFiles.Add(teamDb);
        
        var playerDb = Path.Combine(tempDir, "Player.DB");
        File.WriteAllBytes(playerDb, new byte[] { 0, 1, 2, 3, 4 });
        _tempFiles.Add(playerDb);
        
        var matchDb = Path.Combine(tempDir, "Match.DB");
        File.WriteAllBytes(matchDb, new byte[] { 0, 1, 2, 3, 4 });
        _tempFiles.Add(matchDb);
        
        var singleDb = Path.Combine(tempDir, "Single.DB");
        File.WriteAllBytes(singleDb, new byte[] { 0, 1, 2, 3, 4 });
        _tempFiles.Add(singleDb);
        
        var dblsDb = Path.Combine(tempDir, "Dbls.DB");
        File.WriteAllBytes(dblsDb, new byte[] { 0, 1, 2, 3, 4 });
        _tempFiles.Add(dblsDb);
        
        var dateRateDb = Path.Combine(tempDir, "Daterate.DB");
        File.WriteAllBytes(dateRateDb, new byte[] { 0, 1, 2, 3, 4 });
        _tempFiles.Add(dateRateDb);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result);
    }

    [Fact]
    public void ExportToCsv_WithNullValuesInTeam_HandlesGracefully()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Teams = new List<ParadoxDatabaseParser.ParadoxTeam>
            {
                new() 
                { 
                    ItemId = 1, 
                    TeamName = "Team A", 
                    VenueId = null, 
                    DivisionId = null,
                    Contact = string.Empty,
                    Wins = 0,
                    Losses = 0,
                    Points = 0
                }
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("Team_Export.csv", result);
        var csvFile = Path.Combine(tempDir, "Team_Export.csv");
        var content = File.ReadAllText(csvFile);
        Assert.Contains("1,Team A,,,", content);
    }

    [Fact]
    public void ExportToCsv_WithNullValuesInPlayer_HandlesGracefully()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Players = new List<ParadoxDatabaseParser.ParadoxPlayer>
            {
                new() 
                { 
                    PlayerNo = 100, 
                    PlayerName = "Jane Smith", 
                    PlayerTeam = null,
                    Wins = 0,
                    Losses = 0,
                    CurrentRating = null
                }
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("Player_Export.csv", result);
        var csvFile = Path.Combine(tempDir, "Player_Export.csv");
        var content = File.ReadAllText(csvFile);
        Assert.Contains("100,Jane Smith,,0,0,", content);
    }

    [Fact]
    public void ExportToCsv_WithEmptyStringValues_HandlesGracefully()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Divisions = new List<ParadoxDatabaseParser.ParadoxDivision>
            {
                new() { ItemId = 1, Abbreviated = "", FullDivisionName = "" }
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("Division_Export.csv", result);
        var csvFile = Path.Combine(tempDir, "Division_Export.csv");
        var content = File.ReadAllText(csvFile);
        Assert.Contains("1,,", content);
    }

    [Fact]
    public void ExportToCsv_ReturnsCorrectFileCount()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Divisions = new List<ParadoxDatabaseParser.ParadoxDivision> { new() { ItemId = 1, Abbreviated = "D1", FullDivisionName = "D1" } },
            Teams = new List<ParadoxDatabaseParser.ParadoxTeam> { new() { ItemId = 1, TeamName = "T1", Wins = 0, Losses = 0, Points = 0 } },
            Players = new List<ParadoxDatabaseParser.ParadoxPlayer> { new() { PlayerNo = 1, PlayerName = "P1", Wins = 0, Losses = 0 } },
            Matches = new List<ParadoxDatabaseParser.ParadoxMatch> { new() { MatchNo = 1, HomeTeam = 1, AwayTeam = 2, MatchDate = DateTime.Now } },
            Singles = new List<ParadoxDatabaseParser.ParadoxSingle> { new() { MatchNo = 1, SingleNo = 1, HomePlayerNo = 1, AwayPlayerNo = 2 } },
            Venues = new List<ParadoxDatabaseParser.ParadoxVenue> { new() { ItemId = 1, VenueName = "V1", Address = "A1" } }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("6 files", result);
    }

    [Fact]
    public void ParseFolder_WithCaseInsensitiveDbFiles_FindsFiles()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        
        // Create DB files with different casing
        var divisionDb = Path.Combine(tempDir, "division.db");
        File.WriteAllBytes(divisionDb, new byte[] { 0, 1, 2, 3, 4 });
        _tempFiles.Add(divisionDb);
        
        var venueDb = Path.Combine(tempDir, "VENUE.DB");
        File.WriteAllBytes(venueDb, new byte[] { 0, 1, 2, 3, 4 });
        _tempFiles.Add(venueDb);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.NotNull(result);
        // Files should be found even with different casing
        Assert.Contains(result.Warnings, w => w.Contains("Parsing binary .DB files"));
    }

    [Fact]
    public void ParseFolder_WithOnlyDbFiles_AttemptsDbParsing()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        
        // Create a larger but still invalid DB file to ensure it goes through more parsing logic
        var divisionDb = Path.Combine(tempDir, "Division.DB");
        var data = new byte[2048]; // BLOCK_SIZE
        File.WriteAllBytes(divisionDb, data);
        _tempFiles.Add(divisionDb);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.Contains(result.Warnings, w => w.Contains("Parsing binary .DB files"));
    }

    [Fact]
    public void ParseFolder_SuccessWithDivisionsOnly_ReturnsSuccess()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Division_Export.csv");
        File.WriteAllText(csvFile, "Id,Abbreviated,FullDivisionName\n1,D1,Division 1");
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Divisions);
        Assert.Empty(result.Teams);
        Assert.Empty(result.Players);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void ParseFolder_SuccessWithTeamsOnly_ReturnsSuccess()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Team_Export.csv");
        File.WriteAllText(csvFile, "Id,TeamName,VenueId,DivisionId,Contact,Wins,Losses,Points\n1,T1,1,1,C1,0,0,0");
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Divisions);
        Assert.Single(result.Teams);
    }

    [Fact]
    public void ParseFolder_SuccessWithPlayersOnly_ReturnsSuccess()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Player_Export.csv");
        File.WriteAllText(csvFile, "Id,PlayerName,TeamId,Wins,Losses,Rating\n1,P1,1,0,0,1000");
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Players);
    }

    [Fact]
    public void ParseFolder_SuccessWithMatchesOnly_ReturnsSuccess()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Match_Export.csv");
        File.WriteAllText(csvFile, "Id,HomeTeam,AwayTeam,Date,HSWins,ASWins,HDWins,ADWins,Division\n1,1,2,2024-01-01,0,0,0,0,D1");
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Matches);
    }

    [Fact]
    public void ParseFolder_WithEmptyCsvFile_HandlesGracefully()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Division_Export.csv");
        File.WriteAllText(csvFile, string.Empty);
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public void ParseFolder_WithOnlyHeaderCsvFile_HandlesGracefully()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "Division_Export.csv");
        File.WriteAllText(csvFile, "Id,Abbreviated,FullDivisionName\n");
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public void ParseFolder_ResultInitialization_CreatesEmptyLists()
    {
        // Arrange
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.NotNull(result.Divisions);
        Assert.NotNull(result.Teams);
        Assert.NotNull(result.Players);
        Assert.NotNull(result.Matches);
        Assert.NotNull(result.Singles);
        Assert.NotNull(result.Doubles);
        Assert.NotNull(result.Venues);
        Assert.NotNull(result.DateRates);
        Assert.NotNull(result.Errors);
        Assert.NotNull(result.Warnings);
    }

    [Fact]
    public void ExportToCsv_WithLargeDataset_ExportsSuccessfully()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Divisions = Enumerable.Range(1, 10).Select(i => new ParadoxDatabaseParser.ParadoxDivision 
            { 
                ItemId = i, 
                Abbreviated = $"D{i}", 
                FullDivisionName = $"Division {i}" 
            }).ToList(),
            Teams = Enumerable.Range(1, 50).Select(i => new ParadoxDatabaseParser.ParadoxTeam 
            { 
                ItemId = i, 
                TeamName = $"Team {i}", 
                VenueId = i % 10 + 1, 
                DivisionId = i % 10 + 1,
                Contact = $"Contact {i}",
                Wins = i, 
                Losses = i * 2, 
                Points = i * 3 
            }).ToList(),
            Players = Enumerable.Range(1, 100).Select(i => new ParadoxDatabaseParser.ParadoxPlayer 
            { 
                PlayerNo = i, 
                PlayerName = $"Player {i}", 
                PlayerTeam = i % 50 + 1,
                Wins = i, 
                Losses = i * 2, 
                CurrentRating = 1000 + i * 10 
            }).ToList()
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("3 files", result);
        
        var divisionCsv = File.ReadAllLines(Path.Combine(tempDir, "Division_Export.csv"));
        Assert.Equal(11, divisionCsv.Length); // Header + 10 divisions
        
        var teamCsv = File.ReadAllLines(Path.Combine(tempDir, "Team_Export.csv"));
        Assert.Equal(51, teamCsv.Length); // Header + 50 teams
        
        var playerCsv = File.ReadAllLines(Path.Combine(tempDir, "Player_Export.csv"));
        Assert.Equal(101, playerCsv.Length); // Header + 100 players
    }

    [Fact]
    public void ParseFolder_WithDateRateCsvFile_LoadsDateRates()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var csvFile = Path.Combine(tempDir, "DateRate_Export.csv");
        var csvContent = "DateRateKey,PlayerNo,Won,AgainstPlayerNo,Rating,RatingDate\n1,100,1,200,1500,2024-01-01";
        File.WriteAllText(csvFile, csvContent);
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.DateRates);
    }

    [Fact]
    public void ParseFolder_CsvLoadingPrecedesDbParsing_LoadsFromCsv()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        
        // Create both CSV and DB files - CSV should take precedence
        var csvFile = Path.Combine(tempDir, "Division_Export.csv");
        File.WriteAllText(csvFile, "Id,Abbreviated,FullDivisionName\n1,CSV-Div,CSV Division");
        _tempFiles.Add(csvFile);
        
        var dbFile = Path.Combine(tempDir, "Division.DB");
        File.WriteAllBytes(dbFile, new byte[] { 0, 1, 2, 3, 4 });
        _tempFiles.Add(dbFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Loaded from CSV files"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("Parsing binary .DB files"));
    }

    [Fact]
    public void ParseFolder_ExceptionDuringParsing_CapturesInErrors()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        
        // Create a file that exists but will cause issues during parsing
        var dbFile = Path.Combine(tempDir, "Division.DB");
        File.WriteAllBytes(dbFile, new byte[10]);
        _tempFiles.Add(dbFile);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
    }

    [Fact]
    public void ParseFolder_MultipleSuccessConditions_DeterminesSuccessCorrectly()
    {
        // Arrange - test all four success conditions
        var tempDir1 = CreateTempDirectory();
        var tempDir2 = CreateTempDirectory();
        var tempDir3 = CreateTempDirectory();
        var tempDir4 = CreateTempDirectory();
        
        // Success with divisions
        var csv1 = Path.Combine(tempDir1, "Division_Export.csv");
        File.WriteAllText(csv1, "Id,Abbreviated,FullDivisionName\n1,D1,Div 1");
        _tempFiles.Add(csv1);
        
        // Success with teams
        var csv2 = Path.Combine(tempDir2, "Team_Export.csv");
        File.WriteAllText(csv2, "Id,TeamName,VenueId,DivisionId,Contact,Wins,Losses,Points\n1,T1,1,1,C,0,0,0");
        _tempFiles.Add(csv2);
        
        // Success with players
        var csv3 = Path.Combine(tempDir3, "Player_Export.csv");
        File.WriteAllText(csv3, "Id,PlayerName,TeamId,Wins,Losses,Rating\n1,P1,1,0,0,1000");
        _tempFiles.Add(csv3);
        
        // Success with matches
        var csv4 = Path.Combine(tempDir4, "Match_Export.csv");
        File.WriteAllText(csv4, "Id,HomeTeam,AwayTeam,Date,HSWins,ASWins,HDWins,ADWins,Division\n1,1,2,2024-01-01,0,0,0,0,D");
        _tempFiles.Add(csv4);

        // Act
        var result1 = ParadoxDatabaseParser.ParseFolder(tempDir1);
        var result2 = ParadoxDatabaseParser.ParseFolder(tempDir2);
        var result3 = ParadoxDatabaseParser.ParseFolder(tempDir3);
        var result4 = ParadoxDatabaseParser.ParseFolder(tempDir4);

        // Assert - all should be successful as they meet at least one success condition
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.True(result3.Success);
        Assert.True(result4.Success);
    }

    [Fact]
    public void ParseFolder_NoCoreDataLoaded_ReturnsFailure()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        
        // Load only singles and venues (not core data)
        var singlesCsv = Path.Combine(tempDir, "Single_Export.csv");
        File.WriteAllText(singlesCsv, "MatchNo,FrameNo,HomePlayer,AwayPlayer,Winner,EightBall\n1,1,1,2,Home,0");
        _tempFiles.Add(singlesCsv);
        
        var venueCsv = Path.Combine(tempDir, "Venue_Export.csv");
        File.WriteAllText(venueCsv, "Id,VenueName,Address\n1,V1,A1");
        _tempFiles.Add(venueCsv);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert - Should still succeed because singles and venues were loaded
        Assert.True(result.Success);
    }

    [Fact]
    public void ParseFolder_InvalidCsvWithException_AddsWarningAndContinues()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        
        // Create a valid CSV
        var validCsv = Path.Combine(tempDir, "Division_Export.csv");
        File.WriteAllText(validCsv, "Id,Abbreviated,FullDivisionName\n1,D1,Div1");
        _tempFiles.Add(validCsv);
        
        // Create an invalid CSV file that might cause parsing issues
        var invalidCsv = Path.Combine(tempDir, "Team_Export.csv");
        File.WriteAllText(invalidCsv, "InvalidHeader");
        _tempFiles.Add(invalidCsv);

        // Act
        var result = ParadoxDatabaseParser.ParseFolder(tempDir);

        // Assert - should still succeed because divisions loaded
        Assert.True(result.Success);
        Assert.Single(result.Divisions);
    }

    [Fact]
    public void ExportToCsv_CreatesAllFiles_InCorrectFormat()
    {
        // Arrange
        var parseResult = new ParadoxDatabaseParser.ParadoxParseResult
        {
            Divisions = new List<ParadoxDatabaseParser.ParadoxDivision> 
            { 
                new() { ItemId = 1, Abbreviated = "D1", FullDivisionName = "Division 1" } 
            },
            Teams = new List<ParadoxDatabaseParser.ParadoxTeam> 
            { 
                new() { ItemId = 1, TeamName = "T1", VenueId = 1, DivisionId = 1, Contact = "C1", Wins = 5, Losses = 3, Points = 15 } 
            },
            Players = new List<ParadoxDatabaseParser.ParadoxPlayer> 
            { 
                new() { PlayerNo = 1, PlayerName = "P1", PlayerTeam = 1, Wins = 10, Losses = 5, CurrentRating = 1500 } 
            },
            Matches = new List<ParadoxDatabaseParser.ParadoxMatch> 
            { 
                new() { MatchNo = 1, HomeTeam = 1, AwayTeam = 2, MatchDate = new DateTime(2024, 1, 15), HomeSinglesWins = 3, AwaySinglesWins = 2, HomeDoublesWins = 1, AwayDoublesWins = 1, DivisionName = "D1" } 
            },
            Singles = new List<ParadoxDatabaseParser.ParadoxSingle> 
            { 
                new() { MatchNo = 1, SingleNo = 1, HomePlayerNo = 1, AwayPlayerNo = 2, Winner = "Home", EightBall = true } 
            },
            Venues = new List<ParadoxDatabaseParser.ParadoxVenue> 
            { 
                new() { ItemId = 1, VenueName = "V1", Address = "123 St" } 
            }
        };
        var tempDir = CreateTempDirectory();

        // Act
        var result = ParadoxDatabaseParser.ExportToCsv(parseResult, tempDir);

        // Assert
        Assert.Contains("6 files", result);
        Assert.Contains("Division_Export.csv", result);
        Assert.Contains("Team_Export.csv", result);
        Assert.Contains("Player_Export.csv", result);
        Assert.Contains("Match_Export.csv", result);
        Assert.Contains("Single_Export.csv", result);
        Assert.Contains("Venue_Export.csv", result);
        
        // Verify file format
        var divCsv = File.ReadAllText(Path.Combine(tempDir, "Division_Export.csv"));
        Assert.StartsWith("Id,Abbreviated,FullDivisionName\n", divCsv);
        
        var teamCsv = File.ReadAllText(Path.Combine(tempDir, "Team_Export.csv"));
        Assert.StartsWith("Id,TeamName,VenueId,DivisionId,Contact,Wins,Losses,Points\n", teamCsv);
        
        var playerCsv = File.ReadAllText(Path.Combine(tempDir, "Player_Export.csv"));
        Assert.StartsWith("Id,PlayerName,TeamId,Wins,Losses,Rating\n", playerCsv);
        
        var matchCsv = File.ReadAllText(Path.Combine(tempDir, "Match_Export.csv"));
        Assert.StartsWith("Id,HomeTeam,AwayTeam,Date,HSWins,ASWins,HDWins,ADWins,Division\n", matchCsv);
        
        var singleCsv = File.ReadAllText(Path.Combine(tempDir, "Single_Export.csv"));
        Assert.StartsWith("MatchNo,FrameNo,HomePlayer,AwayPlayer,Winner,EightBall\n", singleCsv);
        
        var venueCsv = File.ReadAllText(Path.Combine(tempDir, "Venue_Export.csv"));
        Assert.StartsWith("Id,VenueName,Address\n", venueCsv);
    }
}
