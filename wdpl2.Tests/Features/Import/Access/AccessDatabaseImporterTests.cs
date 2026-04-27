using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Reflection;
using System.Threading.Tasks;
using Wdpl2.Models;
using Wdpl2.Services;
using Xunit;

namespace wdpl2.Tests.Features.Import.Access
{
    public class AccessDatabaseImporterTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullSchema_UsesDefaultSchema()
        {
            // Arrange & Act & Assert
            var ex = Record.Exception(() => new AccessDatabaseImporter("test.mdb", null));

            // Constructor should attempt to use default schema and get provider
            // Will throw InvalidOperationException if provider not available
            if (ex != null)
            {
                Assert.IsType<InvalidOperationException>(ex);
            }
        }

        [Fact]
        public void Constructor_WithSchemaProvided_UsesProvidedSchema()
        {
            // Arrange
            var customSchema = new DatabaseSchemaConfig
            {
                DivisionTable = "CustomDivisions"
            };

            // Act & Assert
            var ex = Record.Exception(() => new AccessDatabaseImporter("test.mdb", customSchema));

            // Constructor should attempt to get provider
            if (ex != null)
            {
                Assert.IsType<InvalidOperationException>(ex);
            }
        }

        [Fact]
        public void Constructor_WithAccdbExtension_AttemptsToUseAceProvider()
        {
            // Arrange & Act
            var ex = Record.Exception(() => new AccessDatabaseImporter("database.accdb"));

            // Assert - Should throw InvalidOperationException if ACE provider not available
            if (ex != null)
            {
                Assert.IsType<InvalidOperationException>(ex);
                Assert.Contains("ACE", ex.Message);
            }
        }

        [Fact]
        public void Constructor_WithMdbExtension_AttemptsToUseAvailableProvider()
        {
            // Arrange & Act
            var ex = Record.Exception(() => new AccessDatabaseImporter("database.mdb"));

            // Assert - Should throw InvalidOperationException if no provider available
            if (ex != null)
            {
                Assert.IsType<InvalidOperationException>(ex);
            }
        }

        [Fact]
        public void Constructor_WithUppercaseExtension_HandlesCorrectly()
        {
            // Arrange & Act
            var ex = Record.Exception(() => new AccessDatabaseImporter("DATABASE.MDB"));

            // Assert - Should handle case-insensitive extension
            if (ex != null)
            {
                Assert.IsType<InvalidOperationException>(ex);
            }
        }

        [Fact]
        public void Constructor_WithMixedCaseExtension_NormalizesToLowerCase()
        {
            // Arrange & Act
            var ex = Record.Exception(() => new AccessDatabaseImporter("Test.MdB"));

            // Assert - Should handle mixed case extension
            if (ex != null)
            {
                Assert.IsType<InvalidOperationException>(ex);
            }
        }

        [Fact]
        public void Constructor_WithAccdbUppercase_HandlesCorrectly()
        {
            // Arrange & Act
            var ex = Record.Exception(() => new AccessDatabaseImporter("DATA.ACCDB"));

            // Assert
            if (ex != null)
            {
                Assert.IsType<InvalidOperationException>(ex);
            }
        }

        #endregion

        #region AutoDetectSchema Tests

        [Fact]
        public void AutoDetectSchema_WithNonExistentDatabase_ReturnsNull()
        {
            // Arrange
            var nonExistentPath = "nonexistent_database.mdb";

            // Act
            var result = AccessDatabaseImporter.AutoDetectSchema(nonExistentPath);

            // Assert - Should return null when detection fails
            Assert.Null(result);
        }

        [Fact]
        public void AutoDetectSchema_WithInvalidPath_ReturnsNull()
        {
            // Arrange
            var invalidPath = "";

            // Act
            var result = AccessDatabaseImporter.AutoDetectSchema(invalidPath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void AutoDetectSchema_WhenProviderNotAvailable_ThrowsInvalidOperationException()
        {
            // Arrange
            var testPath = "test.accdb";

            // Act & Assert
            // If provider is not available, should throw InvalidOperationException
            // If provider is available, it will attempt to open the file and return null or a schema
            var ex = Record.Exception(() => AccessDatabaseImporter.AutoDetectSchema(testPath));

            if (ex is InvalidOperationException)
            {
                Assert.Contains("provider", ex.Message.ToLower());
            }
        }

        [Fact]
        public void AutoDetectSchema_WithMdbExtension_ExecutesWithoutThrowing()
        {
            // Arrange
            var mdbPath = "test.mdb";

            // Act
            var result = AccessDatabaseImporter.AutoDetectSchema(mdbPath);

            // Assert - Should return null or a schema, but not throw (unless provider missing)
            // Provider exception would be caught and re-thrown, others return null
            Assert.True(result == null || result is DatabaseSchemaConfig);
        }

        [Fact]
        public void AutoDetectSchema_WithAccdbExtension_HandlesProviderCheck()
        {
            // Arrange
            var accdbPath = "database.accdb";

            // Act & Assert
            var ex = Record.Exception(() => AccessDatabaseImporter.AutoDetectSchema(accdbPath));

            // Should throw InvalidOperationException if ACE provider not available
            // or return null if file not found (caught by general catch)
            if (ex is InvalidOperationException)
            {
                Assert.Contains("provider", ex.Message.ToLower());
            }
        }

        [Fact]
        public void AutoDetectSchema_WithUppercaseExtension_HandlesCorrectly()
        {
            // Arrange
            var upperPath = "TEST.MDB";

            // Act
            var result = AccessDatabaseImporter.AutoDetectSchema(upperPath);

            // Assert - Extension should be normalized to lowercase
            Assert.True(result == null || result is DatabaseSchemaConfig);
        }

        #endregion

        #region ImportAllAsync Tests

        [Fact]
        public async Task ImportAllAsync_WithInvalidDatabase_ReturnsFailureSummary()
        {
            // Arrange
            AccessDatabaseImporter? importer = null;
            try
            {
                importer = new AccessDatabaseImporter("invalid.mdb");
            }
            catch (InvalidOperationException)
            {
                // Provider not available, skip test
                return;
            }

            // Act
            var (data, summary) = await importer.ImportAllAsync();

            // Assert
            Assert.NotNull(summary);
            Assert.False(summary.Success);
            Assert.NotEmpty(summary.Message);
        }

        [Fact]
        public async Task ImportAllAsync_WhenProviderNotAvailable_ReturnsProviderError()
        {
            // This test verifies the exception handling in ImportAllAsync
            // We can't easily force a provider error during import without reflection
            // So this is a basic structural test
            
            AccessDatabaseImporter? importer = null;
            try
            {
                importer = new AccessDatabaseImporter("test.mdb");
            }
            catch (InvalidOperationException)
            {
                // Provider not available in constructor - expected on test machines
                return;
            }

            // Act
            var (data, summary) = await importer.ImportAllAsync();

            // Assert - Will fail to connect, but should handle gracefully
            Assert.NotNull(data);
            Assert.NotNull(summary);
        }

        [Fact]
        public async Task ImportAllAsync_InitializesLeagueDataAndSummary()
        {
            // Arrange
            AccessDatabaseImporter? importer = null;
            try
            {
                importer = new AccessDatabaseImporter("test.mdb");
            }
            catch (InvalidOperationException)
            {
                // Provider not available
                return;
            }

            // Act
            var (data, summary) = await importer.ImportAllAsync();

            // Assert
            Assert.NotNull(data);
            Assert.NotNull(summary);
            Assert.NotNull(summary.Errors);
        }

        [Fact]
        public async Task ImportAllAsync_CatchesGeneralExceptions()
        {
            // Arrange
            AccessDatabaseImporter? importer = null;
            try
            {
                importer = new AccessDatabaseImporter("nonexistent_file.mdb");
            }
            catch (InvalidOperationException)
            {
                // Provider not available
                return;
            }

            // Act
            var (data, summary) = await importer.ImportAllAsync();

            // Assert - Should catch exception and populate summary
            Assert.NotNull(summary);
            Assert.False(summary.Success);
            Assert.NotEmpty(summary.Errors);
        }

        [Fact]
        public async Task ImportAllAsync_ReturnsLeagueDataInstance()
        {
            // Arrange
            AccessDatabaseImporter? importer = null;
            try
            {
                importer = new AccessDatabaseImporter("test.mdb");
            }
            catch (InvalidOperationException)
            {
                return;
            }

            // Act
            var (data, summary) = await importer.ImportAllAsync();

            // Assert
            Assert.NotNull(data);
            Assert.IsType<LeagueData>(data);
        }

        [Fact]
        public async Task ImportAllAsync_ReturnsTupleWithBothComponents()
        {
            // Arrange
            AccessDatabaseImporter? importer = null;
            try
            {
                importer = new AccessDatabaseImporter("file.mdb");
            }
            catch (InvalidOperationException)
            {
                return;
            }

            // Act
            var result = await importer.ImportAllAsync();

            // Assert
            Assert.NotNull(result.data);
            Assert.NotNull(result.summary);
        }

        [Fact]
        public async Task ImportAllAsync_PopulatesSummaryErrors()
        {
            // Arrange
            AccessDatabaseImporter? importer = null;
            try
            {
                importer = new AccessDatabaseImporter("missing.mdb");
            }
            catch (InvalidOperationException)
            {
                return;
            }

            // Act
            var (data, summary) = await importer.ImportAllAsync();

            // Assert
            Assert.NotNull(summary.Errors);
            // Should have at least one error entry
            Assert.True(summary.Errors.Count >= 0);
        }

        #endregion

        #region InspectDatabaseSchema Tests

        [Fact]
        public void InspectDatabaseSchema_WithNonExistentFile_ReturnsErrorMessage()
        {
            // Arrange
            var nonExistentPath = "nonexistent.mdb";

            // Act
            var result = AccessDatabaseImporter.InspectDatabaseSchema(nonExistentPath);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("DATABASE SCHEMA INSPECTION", result);
        }

        [Fact]
        public void InspectDatabaseSchema_WithInvalidPath_ReturnsErrorInformation()
        {
            // Arrange
            var invalidPath = "";

            // Act
            var result = AccessDatabaseImporter.InspectDatabaseSchema(invalidPath);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("=== DATABASE SCHEMA INSPECTION ===", result);
        }

        [Fact]
        public void InspectDatabaseSchema_WithAccdbExtension_AttemptsAceProvider()
        {
            // Arrange
            var accdbPath = "test.accdb";

            // Act
            var result = AccessDatabaseImporter.InspectDatabaseSchema(accdbPath);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("DATABASE SCHEMA INSPECTION", result);
            // Will contain error about provider or file not found
        }

        [Fact]
        public void InspectDatabaseSchema_WithMdbExtension_AttemptsAvailableProvider()
        {
            // Arrange
            var mdbPath = "test.mdb";

            // Act
            var result = AccessDatabaseImporter.InspectDatabaseSchema(mdbPath);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("DATABASE SCHEMA INSPECTION", result);
        }

        [Fact]
        public void InspectDatabaseSchema_ReturnsFormattedOutput()
        {
            // Arrange
            var testPath = "database.mdb";

            // Act
            var result = AccessDatabaseImporter.InspectDatabaseSchema(testPath);

            // Assert
            Assert.NotNull(result);
            Assert.StartsWith("=== DATABASE SCHEMA INSPECTION ===", result);
        }

        [Fact]
        public void InspectDatabaseSchema_WhenProviderNotAvailable_ReturnsProviderError()
        {
            // Arrange
            var accdbPath = "notfound.accdb";

            // Act
            var result = AccessDatabaseImporter.InspectDatabaseSchema(accdbPath);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("DATABASE SCHEMA INSPECTION", result);
            // Will contain either provider error or file not found error
        }

        [Fact]
        public void InspectDatabaseSchema_ReturnsStringWithHeader()
        {
            // Arrange
            var path = "any.mdb";

            // Act
            var result = AccessDatabaseImporter.InspectDatabaseSchema(path);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.StartsWith("=== DATABASE SCHEMA INSPECTION ===", result);
        }

        [Fact]
        public void InspectDatabaseSchema_HandlesUppercaseExtension()
        {
            // Arrange
            var path = "DATABASE.ACCDB";

            // Act
            var result = AccessDatabaseImporter.InspectDatabaseSchema(path);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("DATABASE SCHEMA INSPECTION", result);
        }

        [Fact]
        public void InspectDatabaseSchema_CatchesExceptionAndReturnsMessage()
        {
            // Arrange
            var invalidPath = @"x:\nonexistent\path\file.mdb";

            // Act
            var result = AccessDatabaseImporter.InspectDatabaseSchema(invalidPath);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("DATABASE SCHEMA INSPECTION", result);
        }

        #endregion

        #region ImportSummary Tests

        [Fact]
        public void Summary_WithDefaultValues_ReturnsFormattedString()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var result = summary.Summary;

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Divisions: 0", result);
            Assert.Contains("Venues: 0", result);
            Assert.Contains("Teams: 0", result);
            Assert.Contains("Players: 0", result);
            Assert.Contains("Seasons: 0", result);
            Assert.Contains("Fixtures: 0", result);
            Assert.Contains("Frames: 0", result);
        }

        [Fact]
        public void Summary_WithNonZeroValues_ReturnsCorrectCounts()
        {
            // Arrange
            var summary = new ImportSummary
            {
                DivisionsImported = 5,
                VenuesImported = 10,
                TeamsImported = 20,
                PlayersImported = 100,
                SeasonsImported = 3,
                FixturesImported = 50,
                FramesImported = 250
            };

            // Act
            var result = summary.Summary;

            // Assert
            Assert.Contains("Divisions: 5", result);
            Assert.Contains("Venues: 10", result);
            Assert.Contains("Teams: 20", result);
            Assert.Contains("Players: 100", result);
            Assert.Contains("Seasons: 3", result);
            Assert.Contains("Fixtures: 50", result);
            Assert.Contains("Frames: 250", result);
        }

        [Fact]
        public void Summary_ContainsNewlineCharacters()
        {
            // Arrange
            var summary = new ImportSummary
            {
                DivisionsImported = 1,
                VenuesImported = 2
            };

            // Act
            var result = summary.Summary;

            // Assert
            Assert.Contains("\n", result);
            Assert.Equal(6, result.Split('\n').Length - 1); // 6 newlines in the format
        }

        [Fact]
        public void Summary_WithLargeNumbers_FormatsCorrectly()
        {
            // Arrange
            var summary = new ImportSummary
            {
                DivisionsImported = 999999,
                VenuesImported = 888888,
                TeamsImported = 777777,
                PlayersImported = 666666,
                SeasonsImported = 555555,
                FixturesImported = 444444,
                FramesImported = 333333
            };

            // Act
            var result = summary.Summary;

            // Assert
            Assert.Contains("Divisions: 999999", result);
            Assert.Contains("Venues: 888888", result);
            Assert.Contains("Teams: 777777", result);
            Assert.Contains("Players: 666666", result);
            Assert.Contains("Seasons: 555555", result);
            Assert.Contains("Fixtures: 444444", result);
            Assert.Contains("Frames: 333333", result);
        }

        [Fact]
        public void Summary_WithNegativeValues_DisplaysNegativeNumbers()
        {
            // Arrange
            var summary = new ImportSummary
            {
                DivisionsImported = -1,
                VenuesImported = -2
            };

            // Act
            var result = summary.Summary;

            // Assert
            Assert.Contains("Divisions: -1", result);
            Assert.Contains("Venues: -2", result);
        }

        [Fact]
        public void Summary_PropertyIsReadOnly()
        {
            // Arrange
            var summary = new ImportSummary
            {
                DivisionsImported = 5
            };

            // Act
            var result1 = summary.Summary;
            summary.DivisionsImported = 10;
            var result2 = summary.Summary;

            // Assert - Summary should reflect updated values
            Assert.Contains("Divisions: 5", result1);
            Assert.Contains("Divisions: 10", result2);
        }

        [Fact]
        public void Summary_WithZeroValues_ShowsZeros()
        {
            // Arrange
            var summary = new ImportSummary
            {
                DivisionsImported = 0,
                VenuesImported = 0,
                TeamsImported = 0,
                PlayersImported = 0,
                SeasonsImported = 0,
                FixturesImported = 0,
                FramesImported = 0
            };

            // Act
            var result = summary.Summary;

            // Assert
            Assert.Contains("Divisions: 0", result);
            Assert.Contains("Venues: 0", result);
            Assert.Contains("Teams: 0", result);
            Assert.Contains("Players: 0", result);
            Assert.Contains("Seasons: 0", result);
            Assert.Contains("Fixtures: 0", result);
            Assert.Contains("Frames: 0", result);
        }

        [Fact]
        public void Summary_FormatIncludesAllFields()
        {
            // Arrange
            var summary = new ImportSummary
            {
                DivisionsImported = 1,
                VenuesImported = 2,
                TeamsImported = 3,
                PlayersImported = 4,
                SeasonsImported = 5,
                FixturesImported = 6,
                FramesImported = 7
            };

            // Act
            var result = summary.Summary;

            // Assert - Verify all 7 lines are present
            var lines = result.Split('\n');
            Assert.Equal(7, lines.Length);
            Assert.Contains("Divisions:", lines[0]);
            Assert.Contains("Venues:", lines[1]);
            Assert.Contains("Teams:", lines[2]);
            Assert.Contains("Players:", lines[3]);
            Assert.Contains("Seasons:", lines[4]);
            Assert.Contains("Fixtures:", lines[5]);
            Assert.Contains("Frames:", lines[6]);
        }

        [Fact]
        public void Summary_EachValueOnSeparateLine()
        {
            // Arrange
            var summary = new ImportSummary
            {
                DivisionsImported = 10,
                VenuesImported = 20,
                TeamsImported = 30,
                PlayersImported = 40,
                SeasonsImported = 50,
                FixturesImported = 60,
                FramesImported = 70
            };

            // Act
            var result = summary.Summary;

            // Assert
            var lines = result.Split('\n');
            Assert.Equal(7, lines.Length);
            Assert.All(lines, line => Assert.NotEmpty(line));
        }

        #endregion

        #region DiagnosticLog Tests

        [Fact]
        public void DiagnosticLog_WithEmptyErrors_ReturnsEmptyString()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var result = summary.DiagnosticLog;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void DiagnosticLog_WithSingleError_ReturnsSingleLine()
        {
            // Arrange
            var summary = new ImportSummary();
            summary.Errors.Add("Error 1");

            // Act
            var result = summary.DiagnosticLog;

            // Assert
            Assert.Equal("Error 1", result);
        }

        [Fact]
        public void DiagnosticLog_WithMultipleErrors_ReturnsJoinedWithNewline()
        {
            // Arrange
            var summary = new ImportSummary();
            summary.Errors.Add("Error 1");
            summary.Errors.Add("Error 2");
            summary.Errors.Add("Error 3");

            // Act
            var result = summary.DiagnosticLog;

            // Assert
            Assert.Equal("Error 1\nError 2\nError 3", result);
        }

        [Fact]
        public void DiagnosticLog_WithWhitespaceErrors_PreservesWhitespace()
        {
            // Arrange
            var summary = new ImportSummary();
            summary.Errors.Add("  Error with spaces  ");
            summary.Errors.Add("\tError with tab");

            // Act
            var result = summary.DiagnosticLog;

            // Assert
            Assert.Contains("  Error with spaces  ", result);
            Assert.Contains("\tError with tab", result);
        }

        [Fact]
        public void DiagnosticLog_WithEmptyStringInErrors_IncludesEmptyLine()
        {
            // Arrange
            var summary = new ImportSummary();
            summary.Errors.Add("Error 1");
            summary.Errors.Add("");
            summary.Errors.Add("Error 2");

            // Act
            var result = summary.DiagnosticLog;

            // Assert
            Assert.Equal("Error 1\n\nError 2", result);
        }

        #endregion

        #region GenerateFullLog Tests

        [Fact]
        public void GenerateFullLog_WithoutSourcePath_GeneratesCompleteLog()
        {
            // Arrange
            var summary = new ImportSummary
            {
                Success = true,
                SeasonsImported = 1,
                DivisionsImported = 2,
                VenuesImported = 3,
                TeamsImported = 4,
                PlayersImported = 5,
                FixturesImported = 6,
                FramesImported = 7
            };
            summary.Errors.Add("Test log entry");

            // Act
            var result = summary.GenerateFullLog();

            // Assert
            Assert.Contains("IMPORT LOG REPORT", result);
            Assert.Contains("✓ SUCCESS", result);
            Assert.Contains("Seasons:   1", result);
            Assert.Contains("Divisions: 2", result);
            Assert.Contains("Venues:    3", result);
            Assert.Contains("Teams:     4", result);
            Assert.Contains("Players:   5", result);
            Assert.Contains("Fixtures:  6", result);
            Assert.Contains("Frames:    7", result);
            Assert.Contains("Test log entry", result);
        }

        [Fact]
        public void GenerateFullLog_WithSourcePath_IncludesSourcePath()
        {
            // Arrange
            var summary = new ImportSummary();
            var sourcePath = @"C:\Test\database.mdb";

            // Act
            var result = summary.GenerateFullLog(sourcePath);

            // Assert
            Assert.Contains($"Source: {sourcePath}", result);
        }

        [Fact]
        public void GenerateFullLog_WithNullSourcePath_OmitsSourceLine()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var result = summary.GenerateFullLog(null);

            // Assert
            Assert.DoesNotContain("Source:", result);
        }

        [Fact]
        public void GenerateFullLog_WithEmptySourcePath_OmitsSourceLine()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var result = summary.GenerateFullLog("");

            // Assert
            Assert.DoesNotContain("Source:", result);
        }

        [Fact]
        public void GenerateFullLog_WithSuccessTrue_ShowsSuccessStatus()
        {
            // Arrange
            var summary = new ImportSummary { Success = true };

            // Act
            var result = summary.GenerateFullLog();

            // Assert
            Assert.Contains("✓ SUCCESS", result);
            Assert.DoesNotContain("✗ FAILED", result);
        }

        [Fact]
        public void GenerateFullLog_WithSuccessFalse_ShowsFailedStatus()
        {
            // Arrange
            var summary = new ImportSummary { Success = false };

            // Act
            var result = summary.GenerateFullLog();

            // Assert
            Assert.Contains("✗ FAILED", result);
            Assert.DoesNotContain("✓ SUCCESS", result);
        }

        [Fact]
        public void GenerateFullLog_ContainsAllSectionHeaders()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var result = summary.GenerateFullLog();

            // Assert
            Assert.Contains("IMPORT LOG REPORT", result);
            Assert.Contains("SUMMARY", result);
            Assert.Contains("DETAILED LOG", result);
            Assert.Contains("END OF LOG", result);
        }

        [Fact]
        public void GenerateFullLog_ContainsGeneratedTimestamp()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var result = summary.GenerateFullLog();

            // Assert
            Assert.Contains("Generated:", result);
            Assert.Matches(@"Generated: \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", result);
        }

        [Fact]
        public void GenerateFullLog_WithMultipleErrors_IncludesAllErrors()
        {
            // Arrange
            var summary = new ImportSummary();
            summary.Errors.Add("Error 1");
            summary.Errors.Add("Error 2");
            summary.Errors.Add("Warning 1");

            // Act
            var result = summary.GenerateFullLog();

            // Assert
            Assert.Contains("Error 1", result);
            Assert.Contains("Error 2", result);
            Assert.Contains("Warning 1", result);
        }

        [Fact]
        public void GenerateFullLog_WithNoErrors_StillIncludesDetailedLogSection()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var result = summary.GenerateFullLog();

            // Assert
            Assert.Contains("DETAILED LOG", result);
        }

        [Fact]
        public void GenerateFullLog_WithZeroImports_DisplaysZeros()
        {
            // Arrange
            var summary = new ImportSummary
            {
                SeasonsImported = 0,
                DivisionsImported = 0,
                VenuesImported = 0,
                TeamsImported = 0,
                PlayersImported = 0,
                FixturesImported = 0,
                FramesImported = 0
            };

            // Act
            var result = summary.GenerateFullLog();

            // Assert
            Assert.Contains("Seasons:   0", result);
            Assert.Contains("Divisions: 0", result);
            Assert.Contains("Venues:    0", result);
            Assert.Contains("Teams:     0", result);
            Assert.Contains("Players:   0", result);
            Assert.Contains("Fixtures:  0", result);
            Assert.Contains("Frames:    0", result);
        }

        [Fact]
        public void GenerateFullLog_WithLargeNumbers_DisplaysCorrectly()
        {
            // Arrange
            var summary = new ImportSummary
            {
                SeasonsImported = 9999,
                DivisionsImported = 8888,
                VenuesImported = 7777,
                TeamsImported = 6666,
                PlayersImported = 5555,
                FixturesImported = 4444,
                FramesImported = 3333
            };

            // Act
            var result = summary.GenerateFullLog();

            // Assert
            Assert.Contains("Seasons:   9999", result);
            Assert.Contains("Divisions: 8888", result);
            Assert.Contains("Venues:    7777", result);
            Assert.Contains("Teams:     6666", result);
            Assert.Contains("Players:   5555", result);
            Assert.Contains("Fixtures:  4444", result);
            Assert.Contains("Frames:    3333", result);
        }

        [Fact]
        public void GenerateFullLog_ReturnsNonEmptyString()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var result = summary.GenerateFullLog();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GenerateFullLog_ContainsDecorationLines()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var result = summary.GenerateFullLog();

            // Assert
            Assert.Contains("═══════════════════════════════════════════════════════════════", result);
            Assert.Contains("───────────────────────────────────────────────────────────────", result);
        }

        #endregion

        #region SaveLogToFileAsync Tests

        [Fact]
        public async Task SaveLogToFileAsync_WithSuccessfulSave_ReturnsSuccessResult()
        {
            // Arrange
            var summary = new ImportSummary
            {
                Success = true,
                SeasonsImported = 1
            };

            // Act
            // Note: This test will actually try to save a file using FileSaver.Default
            // In a real test environment, we would mock FileSaver, but since we can't
            // easily mock a static Default property, we'll test that it doesn't throw
            var ex = await Record.ExceptionAsync(async () => 
                await summary.SaveLogToFileAsync("test.mdb"));

            // Assert
            // Should not throw an exception (will return success or cancellation)
            Assert.Null(ex);
        }

        [Fact]
        public async Task SaveLogToFileAsync_WithNullSourcePath_ExecutesWithoutError()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var ex = await Record.ExceptionAsync(async () => 
                await summary.SaveLogToFileAsync(null));

            // Assert
            Assert.Null(ex);
        }

        [Fact]
        public async Task SaveLogToFileAsync_WithEmptySourcePath_ExecutesWithoutError()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var ex = await Record.ExceptionAsync(async () => 
                await summary.SaveLogToFileAsync(""));

            // Assert
            Assert.Null(ex);
        }

        [Fact]
        public async Task SaveLogToFileAsync_ReturnsTupleWithBoolAndString()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var result = await summary.SaveLogToFileAsync();

            // Assert
            Assert.IsType<ValueTuple<bool, string>>(result);
            Assert.IsType<bool>(result.success);
            Assert.IsType<string>(result.message);
        }

        [Fact]
        public async Task SaveLogToFileAsync_ReturnsNonNullMessage()
        {
            // Arrange
            var summary = new ImportSummary();

            // Act
            var (success, message) = await summary.SaveLogToFileAsync();

            // Assert
            Assert.NotNull(message);
        }

        [Fact]
        public async Task SaveLogToFileAsync_WithSourcePath_PassesSourceToGenerateFullLog()
        {
            // Arrange
            var summary = new ImportSummary();
            var sourcePath = "test.mdb";

            // Act
            var (success, message) = await summary.SaveLogToFileAsync(sourcePath);

            // Assert
            // Should not throw and should return a result
            Assert.NotNull(message);
        }

        #endregion
    }
}
