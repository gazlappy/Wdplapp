using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Models;
using Wdpl2.Services;
using Xunit;

namespace wdpl2.Tests.Features.Import.Documents
{
    public class VenueTableParserTests
    {
        #region ParseResult.HasTable Tests

        [Fact]
        public void ParseResult_HasTable_ReturnsFalse_WhenTableLabelIsNull()
        {
            // Arrange
            var result = new VenueTableParser.ParseResult
            {
                TableLabel = null
            };

            // Act
            var hasTable = result.HasTable;

            // Assert
            Assert.False(hasTable);
        }

        [Fact]
        public void ParseResult_HasTable_ReturnsFalse_WhenTableLabelIsEmpty()
        {
            // Arrange
            var result = new VenueTableParser.ParseResult
            {
                TableLabel = ""
            };

            // Act
            var hasTable = result.HasTable;

            // Assert
            Assert.False(hasTable);
        }

        [Fact]
        public void ParseResult_HasTable_ReturnsTrue_WhenTableLabelHasValue()
        {
            // Arrange
            var result = new VenueTableParser.ParseResult
            {
                TableLabel = "T1"
            };

            // Act
            var hasTable = result.HasTable;

            // Assert
            Assert.True(hasTable);
        }

        #endregion

        #region Parse Tests

        [Fact]
        public void Parse_WithNull_ReturnsEmptyResult()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse(null);

            // Assert
            Assert.Equal("", result.OriginalName);
            Assert.Equal("", result.BaseName);
            Assert.Null(result.TableLabel);
            Assert.False(result.HasTable);
        }

        [Fact]
        public void Parse_WithEmptyString_ReturnsEmptyResult()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("");

            // Assert
            Assert.Equal("", result.OriginalName);
            Assert.Equal("", result.BaseName);
            Assert.Null(result.TableLabel);
            Assert.False(result.HasTable);
        }

        [Fact]
        public void Parse_WithWhitespace_ReturnsEmptyResult()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("   ");

            // Assert
            Assert.Equal("   ", result.OriginalName);
            Assert.Equal("", result.BaseName);
            Assert.Null(result.TableLabel);
            Assert.False(result.HasTable);
        }

        [Fact]
        public void Parse_SimpleVenueName_ReturnsBaseNameOnly()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("R.G.W.M.C");

            // Assert
            Assert.Equal("R.G.W.M.C", result.OriginalName);
            Assert.Equal("R.G.W.M.C", result.BaseName);
            Assert.Null(result.TableLabel);
            Assert.False(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithT1_ParsesTableLabel()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("R.G.W.M.C T1");

            // Assert
            Assert.Equal("R.G.W.M.C T1", result.OriginalName);
            Assert.Equal("R.G.W.M.C", result.BaseName);
            Assert.Equal("T1", result.TableLabel);
            Assert.True(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithT2_ParsesTableLabel()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("Foo Bar T2");

            // Assert
            Assert.Equal("Foo Bar T2", result.OriginalName);
            Assert.Equal("Foo Bar", result.BaseName);
            Assert.Equal("T2", result.TableLabel);
            Assert.True(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithTB1_ParsesTableLabel()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("Test Venue TB1");

            // Assert
            Assert.Equal("Test Venue TB1", result.OriginalName);
            Assert.Equal("Test Venue", result.BaseName);
            Assert.Equal("TB1", result.TableLabel);
            Assert.True(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithTable1_ParsesTableLabel()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("Test Venue TABLE 1");

            // Assert
            Assert.Equal("Test Venue TABLE 1", result.OriginalName);
            Assert.Equal("Test Venue", result.BaseName);
            Assert.Equal("TABLE 1", result.TableLabel);
            Assert.True(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithTable1NoSpace_ParsesTableLabel()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("Test Venue TABLE1");

            // Assert
            Assert.Equal("Test Venue TABLE1", result.OriginalName);
            Assert.Equal("Test Venue", result.BaseName);
            Assert.Equal("TABLE1", result.TableLabel);
            Assert.True(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithBar_ParsesTableLabel()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("Test Venue BAR");

            // Assert
            Assert.Equal("Test Venue BAR", result.OriginalName);
            Assert.Equal("Test Venue", result.BaseName);
            Assert.Equal("BAR", result.TableLabel);
            Assert.True(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithLounge_ParsesTableLabel()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("Test Venue LOUNGE");

            // Assert
            Assert.Equal("Test Venue LOUNGE", result.OriginalName);
            Assert.Equal("Test Venue", result.BaseName);
            Assert.Equal("LOUNGE", result.TableLabel);
            Assert.True(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithClub_ParsesTableLabel_WhenBaseNameLongEnough()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("Test Venue Name CLUB 1");

            // Assert
            Assert.Equal("Test Venue Name CLUB 1", result.OriginalName);
            Assert.Equal("Test Venue Name", result.BaseName);
            Assert.Equal("CLUB 1", result.TableLabel);
            Assert.True(result.HasTable);
        }

        [Fact]
        public void Parse_ShortVenueWithClub_DoesNotParseTable()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("Con CLUB 1");

            // Assert
            Assert.Equal("Con CLUB 1", result.OriginalName);
            Assert.Equal("Con CLUB 1", result.BaseName);
            Assert.Null(result.TableLabel);
            Assert.False(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithTableButBaseNameTooShort_DoesNotParseTable()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("A T1");

            // Assert
            Assert.Equal("A T1", result.OriginalName);
            Assert.Equal("A T1", result.BaseName);
            Assert.Null(result.TableLabel);
            Assert.False(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithLowercaseTable_NormalizesToUpperCase()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("Test Venue t1");

            // Assert
            Assert.Equal("Test Venue t1", result.OriginalName);
            Assert.Equal("Test Venue", result.BaseName);
            Assert.Equal("T1", result.TableLabel);
            Assert.True(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithExtraWhitespace_TrimsCorrectly()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("  Test Venue T1  ");

            // Assert
            Assert.Equal("Test Venue T1", result.OriginalName);
            Assert.Equal("Test Venue", result.BaseName);
            Assert.Equal("T1", result.TableLabel);
            Assert.True(result.HasTable);
        }

        [Fact]
        public void Parse_VenueWithMixedCase_PreservesBaseName()
        {
            // Arrange & Act
            var result = VenueTableParser.Parse("TeSt VeNuE bar");

            // Assert
            Assert.Equal("TeSt VeNuE bar", result.OriginalName);
            Assert.Equal("TeSt VeNuE", result.BaseName);
            Assert.Equal("BAR", result.TableLabel);
            Assert.True(result.HasTable);
        }

        #endregion

        #region ConsolidateVenues Tests

        [Fact]
        public void ConsolidateVenues_WithEmptyList_ReturnsEmptyResults()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string>();

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Empty(venues);
            Assert.Empty(mapping);
        }

        [Fact]
        public void ConsolidateVenues_WithNullEntries_SkipsThem()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { null!, "  ", "Valid Venue" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Single(venues);
            Assert.Equal("VALID VENUE", venues[0].Name);
            Assert.Single(mapping);
        }

        [Fact]
        public void ConsolidateVenues_WithSingleVenue_CreatesVenueWithDefaultTable()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Test Venue" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Single(venues);
            var venue = venues[0];
            Assert.Equal("TEST VENUE", venue.Name);
            Assert.Equal(seasonId, venue.SeasonId);
            Assert.Equal("[IMPORTED]", venue.Notes);
            Assert.Single(venue.Tables);
            Assert.Equal(VenueTableParser.DefaultTableLabel, venue.Tables[0].Label);
            Assert.Equal(2, venue.Tables[0].MaxTeams);

            Assert.Single(mapping);
            Assert.True(mapping.ContainsKey("Test Venue"));
            Assert.Equal(venue.Id, mapping["Test Venue"].venueId);
            Assert.Null(mapping["Test Venue"].tableId);
        }

        [Fact]
        public void ConsolidateVenues_WithSingleVenueWithTable_CreatesVenueWithSpecificTable()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Test Venue T1" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Single(venues);
            var venue = venues[0];
            Assert.Equal("TEST VENUE", venue.Name);
            Assert.Single(venue.Tables);
            Assert.Equal("T1", venue.Tables[0].Label);

            Assert.Single(mapping);
            Assert.True(mapping.ContainsKey("Test Venue T1"));
            Assert.Equal(venue.Id, mapping["Test Venue T1"].venueId);
            Assert.Equal(venue.Tables[0].Id, mapping["Test Venue T1"].tableId);
        }

        [Fact]
        public void ConsolidateVenues_WithMultipleTables_ConsolidatesIntoOneVenue()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Test Venue T1", "Test Venue T2", "Test Venue BAR" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Single(venues);
            var venue = venues[0];
            Assert.Equal("TEST VENUE", venue.Name);
            Assert.Equal(3, venue.Tables.Count);
            Assert.Contains(venue.Tables, t => t.Label == "T1");
            Assert.Contains(venue.Tables, t => t.Label == "T2");
            Assert.Contains(venue.Tables, t => t.Label == "BAR");

            Assert.Equal(3, mapping.Count);
            Assert.True(mapping.ContainsKey("Test Venue T1"));
            Assert.True(mapping.ContainsKey("Test Venue T2"));
            Assert.True(mapping.ContainsKey("Test Venue BAR"));
            Assert.All(mapping.Values, m => Assert.Equal(venue.Id, m.venueId));
            Assert.All(mapping.Values, m => Assert.NotNull(m.tableId));
        }

        [Fact]
        public void ConsolidateVenues_WithDuplicateTable_ReusesSameTable()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Test Venue T1", "Test Venue T1" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Single(venues);
            var venue = venues[0];
            Assert.Single(venue.Tables);
            Assert.Equal("T1", venue.Tables[0].Label);

            Assert.Single(mapping);
            Assert.Equal(mapping["Test Venue T1"].tableId, venue.Tables[0].Id);
        }

        [Fact]
        public void ConsolidateVenues_WithDifferentVenues_CreatesMultipleVenues()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Venue One", "Venue Two" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Equal(2, venues.Count);
            Assert.Contains(venues, v => v.Name == "VENUE ONE");
            Assert.Contains(venues, v => v.Name == "VENUE TWO");
            Assert.All(venues, v => Assert.Single(v.Tables));
            Assert.All(venues, v => Assert.Equal(VenueTableParser.DefaultTableLabel, v.Tables[0].Label));

            Assert.Equal(2, mapping.Count);
        }

        [Fact]
        public void ConsolidateVenues_WithCreateDefaultTableFalse_DoesNotAddDefaultTable()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Test Venue" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId, createDefaultTable: false);

            // Assert
            Assert.Single(venues);
            var venue = venues[0];
            Assert.Empty(venue.Tables);

            Assert.Single(mapping);
            Assert.Null(mapping["Test Venue"].tableId);
        }

        [Fact]
        public void ConsolidateVenues_WithCreateDefaultTableFalse_KeepsExplicitTables()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Test Venue T1", "Test Venue T2" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId, createDefaultTable: false);

            // Assert
            Assert.Single(venues);
            var venue = venues[0];
            Assert.Equal(2, venue.Tables.Count);
        }

        [Fact]
        public void ConsolidateVenues_WithInvalidShortName_SkipsIt()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "A", "Valid Venue" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Single(venues);
            Assert.Equal("VALID VENUE", venues[0].Name);
            Assert.Single(mapping);
            Assert.False(mapping.ContainsKey("A"));
        }

        [Fact]
        public void ConsolidateVenues_CaseInsensitiveMatching_ConsolidatesCorrectly()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Test Venue T1", "test venue T2", "TEST VENUE BAR" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Single(venues);
            var venue = venues[0];
            Assert.Equal(3, venue.Tables.Count);

            Assert.Equal(3, mapping.Count);
            Assert.All(mapping.Values, m => Assert.Equal(venue.Id, m.venueId));
        }

        [Fact]
        public void ConsolidateVenues_MixedVenuesWithAndWithoutTables_HandlesCorrectly()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Venue One T1", "Venue One", "Venue Two" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Equal(2, venues.Count);
            
            var venueOne = venues.First(v => v.Name == "VENUE ONE");
            Assert.Single(venueOne.Tables);
            Assert.Equal("T1", venueOne.Tables[0].Label);

            var venueTwo = venues.First(v => v.Name == "VENUE TWO");
            Assert.Single(venueTwo.Tables);
            Assert.Equal(VenueTableParser.DefaultTableLabel, venueTwo.Tables[0].Label);
        }

        [Fact]
        public void ConsolidateVenues_DuplicateRawName_OnlyMapsOnce()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Test Venue", "Test Venue" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Single(venues);
            Assert.Single(mapping);
        }

        [Fact]
        public void ConsolidateVenues_VenueWithTableAndSameVenueWithoutTable_CreatesOnlyExplicitTable()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Test Venue T1", "Test Venue" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Single(venues);
            var venue = venues[0];
            Assert.Single(venue.Tables);
            Assert.Equal("T1", venue.Tables[0].Label);
            Assert.Equal(2, mapping.Count);
        }

        [Fact]
        public void ConsolidateVenues_TableLabelCaseVariation_UsesSameTable()
        {
            // Arrange
            var seasonId = Guid.NewGuid();
            var venueNames = new List<string> { "Test Venue t1", "Test Venue T1" };

            // Act
            var (venues, mapping) = VenueTableParser.ConsolidateVenues(venueNames, seasonId);

            // Assert
            Assert.Single(venues);
            var venue = venues[0];
            Assert.Single(venue.Tables);
            Assert.Single(mapping);
            Assert.NotNull(mapping.Values.First().tableId);
        }

        #endregion

        #region GetOrAddTable Tests

        [Fact]
        public void GetOrAddTable_WithNewTable_AddsAndReturnsTable()
        {
            // Arrange
            var venue = new Venue
            {
                Id = Guid.NewGuid(),
                Name = "Test Venue",
                Tables = new List<VenueTable>()
            };

            // Act
            var table = VenueTableParser.GetOrAddTable(venue, "T1");

            // Assert
            Assert.Single(venue.Tables);
            Assert.Equal("T1", table.Label);
            Assert.Equal(2, table.MaxTeams);
            Assert.NotEqual(Guid.Empty, table.Id);
            Assert.Same(venue.Tables[0], table);
        }

        [Fact]
        public void GetOrAddTable_WithExistingTable_ReturnsExisting()
        {
            // Arrange
            var existingTable = new VenueTable
            {
                Id = Guid.NewGuid(),
                Label = "T1",
                MaxTeams = 2
            };
            var venue = new Venue
            {
                Id = Guid.NewGuid(),
                Name = "Test Venue",
                Tables = new List<VenueTable> { existingTable }
            };

            // Act
            var table = VenueTableParser.GetOrAddTable(venue, "T1");

            // Assert
            Assert.Single(venue.Tables);
            Assert.Same(existingTable, table);
        }

        [Fact]
        public void GetOrAddTable_CaseInsensitive_ReturnsExisting()
        {
            // Arrange
            var existingTable = new VenueTable
            {
                Id = Guid.NewGuid(),
                Label = "T1",
                MaxTeams = 2
            };
            var venue = new Venue
            {
                Id = Guid.NewGuid(),
                Name = "Test Venue",
                Tables = new List<VenueTable> { existingTable }
            };

            // Act
            var table = VenueTableParser.GetOrAddTable(venue, "t1");

            // Assert
            Assert.Single(venue.Tables);
            Assert.Same(existingTable, table);
        }

        [Fact]
        public void GetOrAddTable_MultipleTablesExist_AddsNewOne()
        {
            // Arrange
            var venue = new Venue
            {
                Id = Guid.NewGuid(),
                Name = "Test Venue",
                Tables = new List<VenueTable>
                {
                    new VenueTable { Id = Guid.NewGuid(), Label = "T1", MaxTeams = 2 },
                    new VenueTable { Id = Guid.NewGuid(), Label = "T2", MaxTeams = 2 }
                }
            };

            // Act
            var table = VenueTableParser.GetOrAddTable(venue, "T3");

            // Assert
            Assert.Equal(3, venue.Tables.Count);
            Assert.Equal("T3", table.Label);
        }

        [Fact]
        public void GetOrAddTable_NormalizesToUpperCase()
        {
            // Arrange
            var venue = new Venue
            {
                Id = Guid.NewGuid(),
                Name = "Test Venue",
                Tables = new List<VenueTable>()
            };

            // Act
            var table = VenueTableParser.GetOrAddTable(venue, "bar");

            // Assert
            Assert.Equal("BAR", table.Label);
        }

        #endregion

        #region EnsureHasTable Tests

        [Fact]
        public void EnsureHasTable_WithNoTables_AddsDefaultTable()
        {
            // Arrange
            var venue = new Venue
            {
                Id = Guid.NewGuid(),
                Name = "Test Venue",
                Tables = new List<VenueTable>()
            };

            // Act
            VenueTableParser.EnsureHasTable(venue);

            // Assert
            Assert.Single(venue.Tables);
            Assert.Equal(VenueTableParser.DefaultTableLabel, venue.Tables[0].Label);
            Assert.Equal(2, venue.Tables[0].MaxTeams);
            Assert.NotEqual(Guid.Empty, venue.Tables[0].Id);
        }

        [Fact]
        public void EnsureHasTable_WithExistingTable_DoesNotAddAnother()
        {
            // Arrange
            var existingTable = new VenueTable
            {
                Id = Guid.NewGuid(),
                Label = "T1",
                MaxTeams = 2
            };
            var venue = new Venue
            {
                Id = Guid.NewGuid(),
                Name = "Test Venue",
                Tables = new List<VenueTable> { existingTable }
            };

            // Act
            VenueTableParser.EnsureHasTable(venue);

            // Assert
            Assert.Single(venue.Tables);
            Assert.Same(existingTable, venue.Tables[0]);
        }

        [Fact]
        public void EnsureHasTable_WithMultipleTables_DoesNotAddAnother()
        {
            // Arrange
            var venue = new Venue
            {
                Id = Guid.NewGuid(),
                Name = "Test Venue",
                Tables = new List<VenueTable>
                {
                    new VenueTable { Id = Guid.NewGuid(), Label = "T1", MaxTeams = 2 },
                    new VenueTable { Id = Guid.NewGuid(), Label = "T2", MaxTeams = 2 }
                }
            };

            // Act
            VenueTableParser.EnsureHasTable(venue);

            // Assert
            Assert.Equal(2, venue.Tables.Count);
        }

        #endregion
    }
}
