using System;
using System.Reflection;
using System.Threading.Tasks;
using Wdpl2.Services;
using Xunit;

namespace wdpl2.Tests.Features.Import.Access
{
    public class ActualDatabaseImporterTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithDatabasePath_SetsConnectionStringCorrectly()
        {
            // Arrange
            var databasePath = @"C:\TestDatabase.accdb";

            // Act
            var importer = new ActualDatabaseImporterV2(databasePath);

            // Assert
            var connectionStringField = typeof(ActualDatabaseImporterV2).GetField("_connectionString", BindingFlags.NonPublic | BindingFlags.Instance);
            var connectionString = connectionStringField?.GetValue(importer) as string;

            Assert.NotNull(connectionString);
            Assert.Contains("Microsoft.ACE.OLEDB.12.0", connectionString);
            Assert.Contains(databasePath, connectionString);
            Assert.Equal($"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};", connectionString);
        }

        #endregion

        #region ImportAllAsync Tests

        [Fact]
        public async Task ImportAllAsync_WithInvalidDatabasePath_ReturnsFailureSummary()
        {
            // Arrange
            var importer = new ActualDatabaseImporterV2(@"C:\NonExistentDatabase.accdb");

            // Act
            var (data, summary) = await importer.ImportAllAsync();

            // Assert
            Assert.NotNull(data);
            Assert.NotNull(summary);
            Assert.False(summary.Success);
            Assert.Contains("Import failed:", summary.Message);
            Assert.Contains("❌ ERROR:", summary.Errors[0]);
        }

        [Fact]
        public async Task ImportAllAsync_WithEmptyPath_ReturnsFailureSummary()
        {
            // Arrange
            var importer = new ActualDatabaseImporterV2(string.Empty);

            // Act
            var (data, summary) = await importer.ImportAllAsync();

            // Assert
            Assert.NotNull(data);
            Assert.NotNull(summary);
            Assert.False(summary.Success);
            Assert.StartsWith("Import failed:", summary.Message);
        }

        [Fact]
        public async Task ImportAllAsync_WithInvalidPath_CreatesNewLeagueDataAndSummary()
        {
            // Arrange
            var importer = new ActualDatabaseImporterV2(@"C:\Invalid\Path\Database.accdb");

            // Act
            var (data, summary) = await importer.ImportAllAsync();

            // Assert - Verify new instances are created (lines 34-35)
            Assert.NotNull(data);
            Assert.NotNull(summary);
            Assert.NotNull(summary.Errors);
            Assert.False(summary.Success);
        }

        [Fact]
        public async Task ImportAllAsync_OnException_AddsErrorToSummary()
        {
            // Arrange
            var importer = new ActualDatabaseImporterV2(@"C:\NonExistent\Database.accdb");

            // Act
            var (data, summary) = await importer.ImportAllAsync();

            // Assert - Verify exception handling (lines 57-62)
            Assert.False(summary.Success);
            Assert.NotEmpty(summary.Message);
            Assert.NotEmpty(summary.Errors);
            Assert.Single(summary.Errors);
            Assert.StartsWith("❌ ERROR:", summary.Errors[0]);
        }

        [Fact]
        public async Task ImportAllAsync_ReturnsTuple_WithDataAndSummary()
        {
            // Arrange
            var importer = new ActualDatabaseImporterV2(@"C:\TestDatabase.accdb");

            // Act
            var result = await importer.ImportAllAsync();

            // Assert - Verify return tuple (line 64)
            Assert.NotNull(result.data);
            Assert.NotNull(result.summary);
        }

        #endregion
    }
}
