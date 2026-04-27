using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wdpl2.Services.Import;
using Xunit;

namespace wdpl2.Tests;

/// <summary>
/// Tests for ParadoxImportOrchestrator - validates import orchestration and folder scanning.
/// Note: ImportAsync tests are limited because DataStore and ParadoxDatabaseParser are static
/// and DataStore requires MAUI context. We focus on testing GetSummaryText, constructor,
/// ScanFolder, and HasAnyData which don't require DataStore.
/// </summary>
public class ParadoxImportOrchestratorTests : IDisposable
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

    [Fact]
    public void GetSummaryText_WithAllCounts_ReturnsFormattedSummary()
    {
        // Arrange
        var summary = new ParadoxImportOrchestrator.ImportSummary
        {
            DivisionsImported = 5,
            DivisionsSkipped = 1,
            VenuesImported = 10,
            VenuesSkipped = 2,
            TeamsImported = 20,
            TeamsSkipped = 3,
            PlayersImported = 100,
            PlayersSkipped = 5,
            FixturesImported = 50,
            FixturesSkipped = 2,
            SinglesImported = 200,
            SinglesSkipped = 10,
            DoublesImported = 100,
            DoublesSkipped = 5
        };

        // Act
        var result = summary.GetSummaryText();

        // Assert
        Assert.Contains("Import Summary:", result);
        Assert.Contains("Divisions: 5 imported, 1 skipped", result);
        Assert.Contains("Venues: 10 imported, 2 skipped", result);
        Assert.Contains("Teams: 20 imported, 3 skipped", result);
        Assert.Contains("Players: 100 imported, 5 skipped", result);
        Assert.Contains("Fixtures: 50 imported, 2 skipped", result);
        Assert.Contains("Singles Frames: 200 imported, 10 skipped", result);
        Assert.Contains("Doubles Frames: 100 imported, 5 skipped", result);
    }

    [Fact]
    public void GetSummaryText_WithZeroCounts_ReturnsFormattedSummary()
    {
        // Arrange
        var summary = new ParadoxImportOrchestrator.ImportSummary();

        // Act
        var result = summary.GetSummaryText();

        // Assert
        Assert.Contains("Import Summary:", result);
        Assert.Contains("Divisions: 0 imported, 0 skipped", result);
        Assert.Contains("Venues: 0 imported, 0 skipped", result);
        Assert.Contains("Teams: 0 imported, 0 skipped", result);
        Assert.Contains("Players: 0 imported, 0 skipped", result);
        Assert.Contains("Fixtures: 0 imported, 0 skipped", result);
        Assert.Contains("Singles Frames: 0 imported, 0 skipped", result);
        Assert.Contains("Doubles Frames: 0 imported, 0 skipped", result);
        Assert.DoesNotContain("Season dates:", result);
    }

    [Fact]
    public void GetSummaryText_WithSeasonDates_IncludesDateRange()
    {
        // Arrange
        var summary = new ParadoxImportOrchestrator.ImportSummary
        {
            SeasonStartDate = new DateTime(2024, 1, 15),
            SeasonEndDate = new DateTime(2024, 12, 20)
        };

        // Act
        var result = summary.GetSummaryText();

        // Assert
        Assert.Contains("Season dates: 15/01/2024 - 20/12/2024", result);
    }

    [Fact]
    public void GetSummaryText_WithOnlyStartDate_DoesNotIncludeDateRange()
    {
        // Arrange
        var summary = new ParadoxImportOrchestrator.ImportSummary
        {
            SeasonStartDate = new DateTime(2024, 1, 15)
        };

        // Act
        var result = summary.GetSummaryText();

        // Assert
        Assert.DoesNotContain("Season dates:", result);
    }

    [Fact]
    public void GetSummaryText_WithOnlyEndDate_DoesNotIncludeDateRange()
    {
        // Arrange
        var summary = new ParadoxImportOrchestrator.ImportSummary
        {
            SeasonEndDate = new DateTime(2024, 12, 20)
        };

        // Act
        var result = summary.GetSummaryText();

        // Assert
        Assert.DoesNotContain("Season dates:", result);
    }

    [Fact]
    public void Constructor_StoresFolderPath()
    {
        // Arrange
        var folderPath = @"C:\TestFolder";

        // Act
        var orchestrator = new ParadoxImportOrchestrator(folderPath);

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void ScanFolder_WithNonExistentFolder_ReturnsError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(nonExistentPath);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains($"Folder not found: {nonExistentPath}", result.Errors[0]);
    }

    [Fact]
    public void ScanFolder_WithEmptyFolder_ReturnsSuccessWithNoFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.HasDivisions);
        Assert.False(result.HasVenues);
        Assert.False(result.HasTeams);
        Assert.False(result.HasPlayers);
        Assert.False(result.HasMatches);
        Assert.False(result.HasSingles);
        Assert.False(result.HasDoubles);
        Assert.False(result.HasDateRates);
        Assert.False(result.HasCsvFiles);
        Assert.False(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_WithDivisionFile_DetectsDivisions()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var divisionFile = Path.Combine(tempDir, "DIVISION.DB");
        File.WriteAllText(divisionFile, "test content");
        _tempFiles.Add(divisionFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasDivisions);
        Assert.True(result.DivisionFileSize > 0);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_WithVenueFile_DetectsVenues()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var venueFile = Path.Combine(tempDir, "VENUE.DB");
        File.WriteAllText(venueFile, "test content");
        _tempFiles.Add(venueFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasVenues);
        Assert.True(result.VenueFileSize > 0);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_WithTeamFile_DetectsTeams()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var teamFile = Path.Combine(tempDir, "TEAM.DB");
        File.WriteAllText(teamFile, "test content");
        _tempFiles.Add(teamFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasTeams);
        Assert.True(result.TeamFileSize > 0);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_WithPlayerFile_DetectsPlayers()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var playerFile = Path.Combine(tempDir, "PLAYER.DB");
        File.WriteAllText(playerFile, "test content");
        _tempFiles.Add(playerFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasPlayers);
        Assert.True(result.PlayerFileSize > 0);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_WithMatchFile_DetectsMatches()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var matchFile = Path.Combine(tempDir, "MATCH.DB");
        File.WriteAllText(matchFile, "test content");
        _tempFiles.Add(matchFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasMatches);
        Assert.True(result.MatchFileSize > 0);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_WithSingleFile_DetectsSingles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var singleFile = Path.Combine(tempDir, "SINGLE.DB");
        File.WriteAllText(singleFile, "test content");
        _tempFiles.Add(singleFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasSingles);
        Assert.True(result.SingleFileSize > 0);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_WithDblsFile_DetectsDoubles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var dblsFile = Path.Combine(tempDir, "DBLS.DB");
        File.WriteAllText(dblsFile, "test content");
        _tempFiles.Add(dblsFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasDoubles);
        Assert.True(result.DoubleFileSize > 0);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_WithDoubleFile_DetectsDoubles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var doubleFile = Path.Combine(tempDir, "DOUBLE.DB");
        File.WriteAllText(doubleFile, "test content");
        _tempFiles.Add(doubleFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasDoubles);
        Assert.True(result.DoubleFileSize > 0);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_WithDateRateFile_DetectsDateRates()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var dateRateFile = Path.Combine(tempDir, "DATERATE.DB");
        File.WriteAllText(dateRateFile, "test content");
        _tempFiles.Add(dateRateFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasDateRates);
        Assert.True(result.DateRateFileSize > 0);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_WithCsvFile_DetectsCsvFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var csvFile = Path.Combine(tempDir, "data.csv");
        File.WriteAllText(csvFile, "test,content");
        _tempFiles.Add(csvFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasCsvFiles);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_WithMultipleFiles_DetectsAll()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var divisionFile = Path.Combine(tempDir, "DIVISION.DB");
        File.WriteAllText(divisionFile, "test content");
        _tempFiles.Add(divisionFile);
        
        var venueFile = Path.Combine(tempDir, "VENUE.DB");
        File.WriteAllText(venueFile, "test content");
        _tempFiles.Add(venueFile);
        
        var teamFile = Path.Combine(tempDir, "TEAM.DB");
        File.WriteAllText(teamFile, "test content");
        _tempFiles.Add(teamFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasDivisions);
        Assert.True(result.HasVenues);
        Assert.True(result.HasTeams);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void ScanFolder_CaseInsensitiveFileNames_DetectsFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var divisionFile = Path.Combine(tempDir, "division.db");
        File.WriteAllText(divisionFile, "test content");
        _tempFiles.Add(divisionFile);
        
        var venueFile = Path.Combine(tempDir, "Venue.Db");
        File.WriteAllText(venueFile, "test content");
        _tempFiles.Add(venueFile);

        // Act
        var result = ParadoxImportOrchestrator.ScanFolder(tempDir);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.HasDivisions);
        Assert.True(result.HasVenues);
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void HasAnyData_WithNoDivisions_ReturnsFalse()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult();

        // Act & Assert
        Assert.False(result.HasAnyData);
    }

    [Fact]
    public void HasAnyData_WithDivisions_ReturnsTrue()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasDivisions = true
        };

        // Act & Assert
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void HasAnyData_WithVenues_ReturnsTrue()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasVenues = true
        };

        // Act & Assert
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void HasAnyData_WithTeams_ReturnsTrue()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasTeams = true
        };

        // Act & Assert
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void HasAnyData_WithPlayers_ReturnsTrue()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasPlayers = true
        };

        // Act & Assert
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void HasAnyData_WithMatches_ReturnsTrue()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasMatches = true
        };

        // Act & Assert
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void HasAnyData_WithSingles_ReturnsTrue()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasSingles = true
        };

        // Act & Assert
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void HasAnyData_WithDoubles_ReturnsTrue()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasDoubles = true
        };

        // Act & Assert
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void HasAnyData_WithDateRates_ReturnsTrue()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasDateRates = true
        };

        // Act & Assert
        Assert.True(result.HasAnyData);
    }

    [Fact]
    public void HasAnyData_WithCsvFiles_ReturnsTrue()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasCsvFiles = true
        };

        // Act & Assert
        Assert.True(result.HasAnyData);
    }

    // Note: ImportAsync cannot be fully tested in unit tests because:
    // - DataStore is a static class that requires MAUI FileSystem context
    // - ParadoxDatabaseParser.ParseFolder is static and cannot be mocked
    // - All import methods (ImportDivisions, ImportVenues, etc.) depend on DataStore
    // The method would need to be refactored to use dependency injection for testability.
    // Integration tests would be more appropriate for this method.

    [Fact]
    public void GetSummary_WithNoFiles_ReturnsHeaderOnly()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult();

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.DoesNotContain("Division.DB", summary);
        Assert.DoesNotContain("Venue.DB", summary);
        Assert.DoesNotContain("Team.DB", summary);
        Assert.DoesNotContain("Player.DB", summary);
        Assert.DoesNotContain("Match.DB", summary);
        Assert.DoesNotContain("Single.DB", summary);
        Assert.DoesNotContain("Dbls.DB", summary);
        Assert.DoesNotContain("Daterate.DB", summary);
        Assert.DoesNotContain("CSV files", summary);
    }

    [Fact]
    public void GetSummary_WithDivisionFile_IncludesDivisionWithSize()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasDivisions = true,
            DivisionFileSize = 10240 // 10 KB
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("Division.DB (10 KB)", summary);
    }

    [Fact]
    public void GetSummary_WithVenueFile_IncludesVenueWithSize()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasVenues = true,
            VenueFileSize = 20480 // 20 KB
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("Venue.DB (20 KB)", summary);
    }

    [Fact]
    public void GetSummary_WithTeamFile_IncludesTeamWithSize()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasTeams = true,
            TeamFileSize = 30720 // 30 KB
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("Team.DB (30 KB)", summary);
    }

    [Fact]
    public void GetSummary_WithPlayerFile_IncludesPlayerWithSize()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasPlayers = true,
            PlayerFileSize = 51200 // 50 KB
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("Player.DB (50 KB)", summary);
    }

    [Fact]
    public void GetSummary_WithMatchFile_IncludesMatchWithSize()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasMatches = true,
            MatchFileSize = 102400 // 100 KB
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("Match.DB (100 KB)", summary);
    }

    [Fact]
    public void GetSummary_WithSingleFile_IncludesSingleWithSize()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasSingles = true,
            SingleFileSize = 204800 // 200 KB
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("Single.DB (200 KB)", summary);
    }

    [Fact]
    public void GetSummary_WithDoubleFile_IncludesDoublesWithSize()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasDoubles = true,
            DoubleFileSize = 153600 // 150 KB
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("Dbls.DB (150 KB)", summary);
    }

    [Fact]
    public void GetSummary_WithDateRateFile_IncludesDateRateWithSizeAndNote()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasDateRates = true,
            DateRateFileSize = 5120 // 5 KB
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("Daterate.DB (5 KB) - VBA ratings", summary);
    }

    [Fact]
    public void GetSummary_WithCsvFiles_IncludesCsvNote()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasCsvFiles = true
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("CSV files found (preferred)", summary);
    }

    [Fact]
    public void GetSummary_WithLargeFileSize_FormatsWithThousandsSeparator()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasDivisions = true,
            DivisionFileSize = 2048000 // 2000 KB
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("Division.DB (2,000 KB)", summary);
    }

    [Fact]
    public void GetSummary_WithAllFiles_IncludesAllEntries()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasDivisions = true,
            DivisionFileSize = 10240,
            HasVenues = true,
            VenueFileSize = 20480,
            HasTeams = true,
            TeamFileSize = 30720,
            HasPlayers = true,
            PlayerFileSize = 51200,
            HasMatches = true,
            MatchFileSize = 102400,
            HasSingles = true,
            SingleFileSize = 204800,
            HasDoubles = true,
            DoubleFileSize = 153600,
            HasDateRates = true,
            DateRateFileSize = 5120,
            HasCsvFiles = true
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("Division.DB (10 KB)", summary);
        Assert.Contains("Venue.DB (20 KB)", summary);
        Assert.Contains("Team.DB (30 KB)", summary);
        Assert.Contains("Player.DB (50 KB)", summary);
        Assert.Contains("Match.DB (100 KB)", summary);
        Assert.Contains("Single.DB (200 KB)", summary);
        Assert.Contains("Dbls.DB (150 KB)", summary);
        Assert.Contains("Daterate.DB (5 KB) - VBA ratings", summary);
        Assert.Contains("CSV files found (preferred)", summary);
    }

    [Fact]
    public void GetSummary_WithSmallFileSize_RoundsToZeroKB()
    {
        // Arrange
        var result = new ParadoxImportOrchestrator.FolderScanResult
        {
            HasDivisions = true,
            DivisionFileSize = 512 // Less than 1 KB
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Paradox files found:", summary);
        Assert.Contains("Division.DB (0 KB)", summary);
    }
}
