using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// Service to import data from Access database (.accdb or .mdb) into the MAUI app.
    /// Supports flexible schema mapping for different database versions.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class AccessDatabaseImporter
    {
        private readonly string _connectionString;
        private readonly DatabaseSchemaConfig _schema;
        private readonly Dictionary<string, Guid> _divisionMap = new();
        private readonly Dictionary<string, Guid> _venueMap = new();
        private readonly Dictionary<string, Guid> _teamMap = new();
        private readonly Dictionary<string, Guid> _playerMap = new();
        private readonly Dictionary<string, Guid> _seasonMap = new();

        public AccessDatabaseImporter(string databasePath, DatabaseSchemaConfig? schema = null)
        {
            // Use provided schema or default
            _schema = schema ?? DatabaseSchemaConfig.KnownSchemas["Current"];

            // Determine provider based on file extension and availability
            var fileExtension = System.IO.Path.GetExtension(databasePath).ToLowerInvariant();
            var provider = GetAvailableProvider(fileExtension);

            _connectionString = $"Provider={provider};Data Source={databasePath};";
        }

        /// <summary>
        /// Gets the best available OLE DB provider for Access databases.
        /// </summary>
        private static string GetAvailableProvider(string fileExtension)
        {
            // For .accdb files, we must use ACE
            if (fileExtension == ".accdb")
            {
                if (!IsProviderAvailable("Microsoft.ACE.OLEDB.12.0"))
                {
                    throw new InvalidOperationException(
                        "The Microsoft Access Database Engine (ACE) is not installed.\n\n" +
                        "To import .accdb files, please install the Microsoft Access Database Engine 2016 Redistributable:\n" +
                        "- Download from: https://www.microsoft.com/en-us/download/details.aspx?id=54920\n" +
                        "- Install the version (32-bit or 64-bit) that matches your application's platform.\n" +
                        "- If you have 64-bit Office installed, install the 64-bit redistributable.\n" +
                        "- If you have 32-bit Office or no Office, you can install either version (64-bit recommended for Windows).");
                }
                return "Microsoft.ACE.OLEDB.12.0";
            }

            // For .mdb files, try ACE first, then fall back to Jet
            if (IsProviderAvailable("Microsoft.ACE.OLEDB.12.0"))
            {
                return "Microsoft.ACE.OLEDB.12.0";
            }

            if (IsProviderAvailable("Microsoft.Jet.OLEDB.4.0"))
            {
                return "Microsoft.Jet.OLEDB.4.0";
            }

            throw new InvalidOperationException(
                "No Access database provider is available.\n\n" +
                "To import Access database files, please install the Microsoft Access Database Engine 2016 Redistributable:\n" +
                "- Download from: https://www.microsoft.com/en-us/download/details.aspx?id=54920\n" +
                "- Install the version (32-bit or 64-bit) that matches your application's platform.\n" +
                "- If you have 64-bit Office installed, install the 64-bit redistributable.\n" +
                "- If you have 32-bit Office or no Office, you can install either version (64-bit recommended for Windows).");
        }

        /// <summary>
        /// Checks if a specific OLE DB provider is registered on the system.
        /// </summary>
        private static bool IsProviderAvailable(string providerName)
        {
            try
            {
                using var connection = new OleDbConnection($"Provider={providerName};");
                // Just creating the connection with a valid provider name is enough
                // We don't need to open it to verify the provider exists
                var factory = System.Data.Common.DbProviderFactories.GetFactory(connection);
                return factory != null;
            }
            catch
            {
                // If we can't create the connection or get the factory, provider is not available
                return false;
            }
        }

        /// <summary>
        /// Detect schema by inspecting database tables and columns.
        /// </summary>
        public static DatabaseSchemaConfig? AutoDetectSchema(string databasePath)
        {
            try
            {
                var fileExtension = System.IO.Path.GetExtension(databasePath).ToLowerInvariant();
                var provider = GetAvailableProvider(fileExtension);
                var connectionString = $"Provider={provider};Data Source={databasePath};";

                using var connection = new OleDbConnection(connectionString);
                connection.Open();

                var tables = connection.GetSchema("Tables");
                var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (DataRow row in tables.Rows)
                {
                    var tableName = row["TABLE_NAME"].ToString();
                    if (!string.IsNullOrEmpty(tableName))
                        tableNames.Add(tableName);
                }

                // Try to match known schemas
                foreach (var kvp in DatabaseSchemaConfig.KnownSchemas)
                {
                    var schema = kvp.Value;
                    if (tableNames.Contains(schema.DivisionTable) &&
                        tableNames.Contains(schema.TeamTable) &&
                        tableNames.Contains(schema.PlayerTable))
                    {
                        return schema;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Re-throw provider not available exceptions
                throw;
            }
            catch
            {
                // Return null if detection fails for other reasons
            }

            return null;
        }

        /// <summary>
        /// Import all data from Access database into LeagueData.
        /// </summary>
        public async Task<(LeagueData data, ImportSummary summary)> ImportAllAsync()
        {
            var data = new LeagueData();
            var summary = new ImportSummary();

            try
            {
                await Task.Run(() =>
                {
                    using var connection = new OleDbConnection(_connectionString);
                    connection.Open();

                    // Log connection success
                    summary.Errors.Add($"✓ Connected to database successfully");

                    // Import in dependency order
                    ImportDivisions(connection, data, summary);
                    ImportVenues(connection, data, summary);
                    ImportTeams(connection, data, summary);
                    ImportPlayers(connection, data, summary);
                    ImportSeasons(connection, data, summary);
                    ImportFixtures(connection, data, summary);
                });

                summary.Success = true;
                summary.Message = "Import completed successfully!";
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("provider") || ex.Message.Contains("registered"))
            {
                summary.Success = false;
                summary.Message = "Database provider not available";
                summary.Errors.Add($"❌ {ex.Message}");
            }
            catch (Exception ex)
            {
                summary.Success = false;
                summary.Message = $"Import failed: {ex.Message}";
                summary.Errors.Add(ex.ToString());
            }

            return (data, summary);
        }

        private void ImportDivisions(OleDbConnection conn, LeagueData data, ImportSummary summary)
        {
            try
            {
                var query = $"SELECT {_schema.DivisionIdColumn}, {_schema.DivisionNameColumn}, {_schema.DivisionNotesColumn} FROM {_schema.DivisionTable}";
                summary.Errors.Add($"Division Query: {query}");

                using var cmd = new OleDbCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                // Log column names found
                var columns = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }
                summary.Errors.Add($"Division Columns: {string.Join(", ", columns)}");

                while (reader.Read())
                {
                    var oldId = SafeGetString(reader, _schema.DivisionIdColumn);
                    var name = SafeGetString(reader, _schema.DivisionNameColumn) ?? "Unknown Division";
                    var notes = SafeGetString(reader, _schema.DivisionNotesColumn);

                    if (string.IsNullOrEmpty(oldId))
                    {
                        summary.Errors.Add($"⚠️ Warning: Division '{name}' has no ID, skipping");
                        continue;
                    }

                    var division = new Division
                    {
                        Id = Guid.NewGuid(),
                        Name = name,
                        Notes = notes
                    };

                    data.Divisions.Add(division);
                    _divisionMap[oldId] = division.Id;
                    summary.DivisionsImported++;
                }

                summary.Errors.Add($"✓ Imported {summary.DivisionsImported} divisions");
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Division import error: {ex.Message}");
            }
        }

        private void ImportVenues(OleDbConnection conn, LeagueData data, ImportSummary summary)
        {
            try
            {
                var query = $"SELECT {_schema.VenueIdColumn}, {_schema.VenueNameColumn}, {_schema.VenueAddressColumn}, {_schema.VenueNotesColumn} FROM {_schema.VenueTable}";
                summary.Errors.Add($"Venue Query: {query}");

                using var cmd = new OleDbCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                // Collect raw venue names for consolidation
                var rawVenues = new List<(string oldId, string name, string? address, string? notes)>();

                while (reader.Read())
                {
                    var oldId = SafeGetString(reader, _schema.VenueIdColumn);
                    var name = SafeGetString(reader, _schema.VenueNameColumn) ?? "Unknown Venue";
                    var address = SafeGetString(reader, _schema.VenueAddressColumn);
                    var notes = SafeGetString(reader, _schema.VenueNotesColumn);

                    if (string.IsNullOrEmpty(oldId))
                    {
                        summary.Errors.Add($"⚠️ Warning: Venue '{name}' has no ID, skipping");
                        continue;
                    }

                    rawVenues.Add((oldId, name, address, notes));
                }

                // Use VenueTableParser to consolidate venues with table suffixes
                var venuesByBaseName = new Dictionary<string, Venue>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var (oldId, rawName, address, notes) in rawVenues)
                {
                    var parsed = VenueTableParser.Parse(rawName);
                    
                    // Get or create venue by base name
                    if (!venuesByBaseName.TryGetValue(parsed.BaseName, out var venue))
                    {
                        venue = new Venue
                        {
                            Id = Guid.NewGuid(),
                            Name = parsed.BaseName,
                            Address = address,
                            Notes = notes,
                            Tables = new List<VenueTable>()
                        };
                        venuesByBaseName[parsed.BaseName] = venue;
                        data.Venues.Add(venue);
                        summary.VenuesImported++;
                        summary.Errors.Add($"  Venue: {parsed.BaseName}");
                    }
                    
                    // Add table if detected
                    if (parsed.HasTable)
                    {
                        var existingTable = venue.Tables.FirstOrDefault(t => 
                            t.Label.Equals(parsed.TableLabel, StringComparison.OrdinalIgnoreCase));
                        
                        if (existingTable == null)
                        {
                            var newTable = new VenueTable
                            {
                                Id = Guid.NewGuid(),
                                Label = parsed.TableLabel!,
                                MaxTeams = 2
                            };
                            venue.Tables.Add(newTable);
                            summary.Errors.Add($"    + Table: {parsed.TableLabel}");
                        }
                    }
                    
                    // Map the raw ID to the venue GUID
                    _venueMap[oldId] = venue.Id;
                }

                // After processing all venues, add default table to venues without any tables
                foreach (var venue in venuesByBaseName.Values)
                {
                    if (!venue.Tables.Any())
                    {
                        var defaultTable = new VenueTable
                        {
                            Id = Guid.NewGuid(),
                            Label = VenueTableParser.DefaultTableLabel,
                            MaxTeams = 2
                        };
                        venue.Tables.Add(defaultTable);
                        summary.Errors.Add($"    + Default table '{VenueTableParser.DefaultTableLabel}' for '{venue.Name}'");
                    }
                }

                var totalTables = data.Venues.Sum(v => v.Tables.Count);
                summary.Errors.Add($"✓ Imported {summary.VenuesImported} venues with {totalTables} tables");
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Venue import error: {ex.Message}");
            }
        }

        private void ImportTeams(OleDbConnection conn, LeagueData data, ImportSummary summary)
        {
            try
            {
                var query = $@"SELECT {_schema.TeamIdColumn}, {_schema.TeamNameColumn}, 
                              {_schema.TeamDivisionIdColumn}, {_schema.TeamVenueIdColumn}, 
                              {_schema.TeamCaptainColumn}, {_schema.TeamNotesColumn}, 
                              {_schema.TeamProvidesFoodColumn} 
                              FROM {_schema.TeamTable}";
                summary.Errors.Add($"Team Query: {query}");

                using var cmd = new OleDbCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var oldId = SafeGetString(reader, _schema.TeamIdColumn);
                    var name = SafeGetString(reader, _schema.TeamNameColumn) ?? "Unknown Team";
                    var divisionId = SafeGetString(reader, _schema.TeamDivisionIdColumn);
                    var venueId = SafeGetString(reader, _schema.TeamVenueIdColumn);
                    var captain = SafeGetString(reader, _schema.TeamCaptainColumn);
                    var notes = SafeGetString(reader, _schema.TeamNotesColumn);
                    var providesFood = SafeGetBool(reader, _schema.TeamProvidesFoodColumn);

                    if (string.IsNullOrEmpty(oldId))
                    {
                        summary.Errors.Add($"⚠️ Warning: Team '{name}' has no ID, skipping");
                        continue;
                    }

                    var team = new Team
                    {
                        Id = Guid.NewGuid(),
                        Name = name,
                        DivisionId = divisionId != null && _divisionMap.ContainsKey(divisionId) ? _divisionMap[divisionId] : null,
                        VenueId = venueId != null && _venueMap.ContainsKey(venueId) ? _venueMap[venueId] : null,
                        Captain = captain,
                        Notes = notes,
                        ProvidesFood = providesFood
                    };

                    data.Teams.Add(team);
                    _teamMap[oldId] = team.Id;
                    summary.TeamsImported++;
                }

                summary.Errors.Add($"✓ Imported {summary.TeamsImported} teams");
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Team import error: {ex.Message}");
            }
        }

        private void ImportPlayers(OleDbConnection conn, LeagueData data, ImportSummary summary)
        {
            try
            {
                var query = $@"SELECT {_schema.PlayerIdColumn}, {_schema.PlayerFirstNameColumn}, 
                              {_schema.PlayerLastNameColumn}, {_schema.PlayerTeamIdColumn}, 
                              {_schema.PlayerNotesColumn} 
                              FROM {_schema.PlayerTable}";
                summary.Errors.Add($"Player Query: {query}");

                using var cmd = new OleDbCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var oldId = SafeGetString(reader, _schema.PlayerIdColumn);
                    var firstName = SafeGetString(reader, _schema.PlayerFirstNameColumn) ?? "";
                    var lastName = SafeGetString(reader, _schema.PlayerLastNameColumn) ?? "";
                    var teamId = SafeGetString(reader, _schema.PlayerTeamIdColumn);
                    var notes = SafeGetString(reader, _schema.PlayerNotesColumn);

                    if (string.IsNullOrEmpty(oldId))
                    {
                        summary.Errors.Add($"⚠️ Warning: Player '{firstName} {lastName}' has no ID, skipping");
                        continue;
                    }

                    var player = new Player
                    {
                        Id = Guid.NewGuid(),
                        FirstName = firstName,
                        LastName = lastName,
                        TeamId = teamId != null && _teamMap.ContainsKey(teamId) ? _teamMap[teamId] : null,
                        Notes = notes
                    };

                    data.Players.Add(player);
                    _playerMap[oldId] = player.Id;
                    summary.PlayersImported++;
                }

                summary.Errors.Add($"✓ Imported {summary.PlayersImported} players");
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Player import error: {ex.Message}");
            }
        }

        private void ImportSeasons(OleDbConnection conn, LeagueData data, ImportSummary summary)
        {
            try
            {
                var query = $@"SELECT {_schema.SeasonIdColumn}, {_schema.SeasonNameColumn}, 
                              {_schema.SeasonStartDateColumn}, {_schema.SeasonEndDateColumn}, 
                              {_schema.SeasonMatchDayColumn}, {_schema.SeasonMatchTimeColumn}, 
                              {_schema.SeasonFramesPerMatchColumn}, {_schema.SeasonIsActiveColumn} 
                              FROM {_schema.SeasonTable}";
                summary.Errors.Add($"Season Query: {query}");

                using var cmd = new OleDbCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var oldId = SafeGetString(reader, _schema.SeasonIdColumn);
                    var name = SafeGetString(reader, _schema.SeasonNameColumn) ?? "Unknown Season";
                    var startDate = SafeGetDateTime(reader, _schema.SeasonStartDateColumn) ?? DateTime.Today;
                    var endDate = SafeGetDateTime(reader, _schema.SeasonEndDateColumn) ?? DateTime.Today.AddMonths(3);
                    var matchDay = SafeGetInt(reader, _schema.SeasonMatchDayColumn) ?? 2;
                    var matchTime = SafeGetTimeSpan(reader, _schema.SeasonMatchTimeColumn) ?? new TimeSpan(20, 0, 0);
                    var framesPerMatch = SafeGetInt(reader, _schema.SeasonFramesPerMatchColumn) ?? 10;
                    var isActive = SafeGetBool(reader, _schema.SeasonIsActiveColumn);

                    if (string.IsNullOrEmpty(oldId))
                    {
                        summary.Errors.Add($"⚠️ Warning: Season '{name}' has no ID, skipping");
                        continue;
                    }

                    var season = new Season
                    {
                        Id = Guid.NewGuid(),
                        Name = name,
                        StartDate = startDate,
                        EndDate = endDate,
                        MatchDayOfWeek = (DayOfWeek)matchDay,
                        MatchStartTime = matchTime,
                        FramesPerMatch = framesPerMatch,
                        IsActive = isActive
                    };

                    season.NormaliseDates();
                    data.Seasons.Add(season);
                    _seasonMap[oldId] = season.Id;
                    summary.SeasonsImported++;
                }

                summary.Errors.Add($"✓ Imported {summary.SeasonsImported} seasons");
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Season import error: {ex.Message}");
            }
        }

        private void ImportFixtures(OleDbConnection conn, LeagueData data, ImportSummary summary
)
        {
            try
            {
                var query = $@"SELECT {_schema.MatchNumberColumn}, {_schema.MatchSeasonIdColumn}, 
                              {_schema.MatchDivisionIdColumn}, {_schema.MatchHomeTeamIdColumn}, 
                              {_schema.MatchAwayTeamIdColumn}, {_schema.MatchDateColumn}, 
                              {_schema.MatchVenueIdColumn} 
                              FROM {_schema.MatchHeaderTable} 
                              ORDER BY {_schema.MatchDateColumn}";
                summary.Errors.Add($"Fixture Query: {query}");

                using var cmd = new OleDbCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                var fixtures = new Dictionary<string, Fixture>();

                while (reader.Read())
                {
                    var matchNo = SafeGetString(reader, _schema.MatchNumberColumn);
                    var seasonId = SafeGetString(reader, _schema.MatchSeasonIdColumn);
                    var divisionId = SafeGetString(reader, _schema.MatchDivisionIdColumn);
                    var homeTeamId = SafeGetString(reader, _schema.MatchHomeTeamIdColumn);
                    var awayTeamId = SafeGetString(reader, _schema.MatchAwayTeamIdColumn);
                    var matchDate = SafeGetDateTime(reader, _schema.MatchDateColumn) ?? DateTime.Today;
                    var venueId = SafeGetString(reader, _schema.MatchVenueIdColumn);

                    if (string.IsNullOrEmpty(matchNo))
                    {
                        summary.Errors.Add($"⚠️ Warning: Fixture has no match number, skipping");
                        continue;
                    }

                    var fixture = new Fixture
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = seasonId != null && _seasonMap.ContainsKey(seasonId) ? _seasonMap[seasonId] : null,
                        DivisionId = divisionId != null && _divisionMap.ContainsKey(divisionId) ? _divisionMap[divisionId] : null,
                        HomeTeamId = homeTeamId != null && _teamMap.ContainsKey(homeTeamId) ? _teamMap[homeTeamId] : Guid.Empty,
                        AwayTeamId = awayTeamId != null && _teamMap.ContainsKey(awayTeamId) ? _teamMap[awayTeamId] : Guid.Empty,
                        Date = matchDate,
                        VenueId = venueId != null && _venueMap.ContainsKey(venueId) ? _venueMap[venueId] : null
                    };

                    fixtures[matchNo] = fixture;
                    data.Fixtures.Add(fixture);
                    summary.FixturesImported++;
                }

                summary.Errors.Add($"✓ Imported {summary.FixturesImported} fixtures");

                // Now import frame details
                ImportFrameDetails(conn, fixtures, summary);
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Fixture import error: {ex.Message}");
            }
        }

        private void ImportFrameDetails(OleDbConnection conn, Dictionary<string, Fixture> fixtures, ImportSummary summary)
        {
            try
            {
                var query = $@"SELECT {_schema.FrameMatchNumberColumn}, {_schema.FrameNumberColumn}, 
                              {_schema.FramePlayer1Column}, {_schema.FramePlayer2Column}, 
                              {_schema.FrameHomeScoreColumn}, {_schema.FrameAwayScoreColumn}, 
                              {_schema.FrameEightBallColumn} 
                              FROM {_schema.MatchDetailTable} 
                              ORDER BY {_schema.FrameMatchNumberColumn}, {_schema.FrameNumberColumn}";
                summary.Errors.Add($"Frame Query: {query}");

                using var cmd = new OleDbCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var matchNo = SafeGetString(reader, _schema.FrameMatchNumberColumn);
                    if (matchNo == null || !fixtures.ContainsKey(matchNo)) continue;

                    var frameNo = SafeGetInt(reader, _schema.FrameNumberColumn) ?? 1;
                    var player1 = SafeGetString(reader, _schema.FramePlayer1Column);
                    var player2 = SafeGetString(reader, _schema.FramePlayer2Column);
                    var homeScore = SafeGetInt(reader, _schema.FrameHomeScoreColumn) ?? 0;
                    var awayScore = SafeGetInt(reader, _schema.FrameAwayScoreColumn) ?? 0;
                    var eightBall = SafeGetBool(reader, _schema.FrameEightBallColumn);

                    var frame = new FrameResult
                    {
                        Number = frameNo,
                        HomePlayerId = player1 != null && _playerMap.ContainsKey(player1) ? _playerMap[player1] : null,
                        AwayPlayerId = player2 != null && _playerMap.ContainsKey(player2) ? _playerMap[player2] : null,
                        Winner = homeScore > awayScore ? FrameWinner.Home : awayScore > homeScore ? FrameWinner.Away : FrameWinner.None,
                        EightBall = eightBall
                    };

                    fixtures[matchNo].Frames.Add(frame);
                    summary.FramesImported++;
                }

                summary.Errors.Add($"✓ Imported {summary.FramesImported} frames");
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Frame details import error: {ex.Message}");
            }
        }

        /// <summary>
        /// Inspect Access database structure to help configure schema mapping.
        /// </summary>
        public static string InspectDatabaseSchema(string databasePath)
        {
            var result = new System.Text.StringBuilder();
            result.AppendLine("=== DATABASE SCHEMA INSPECTION ===\n");

            try
            {
                var fileExtension = System.IO.Path.GetExtension(databasePath).ToLowerInvariant();
                var provider = GetAvailableProvider(fileExtension);
                var connectionString = $"Provider={provider};Data Source={databasePath};";

                using var connection = new OleDbConnection(connectionString);
                connection.Open();

                result.AppendLine($"✓ Connection successful using {provider}\n");

                // Get all tables
                var tables = connection.GetSchema("Tables");
                result.AppendLine($"Found {tables.Rows.Count} tables:\n");

                foreach (DataRow tableRow in tables.Rows)
                {
                    var tableName = tableRow["TABLE_NAME"].ToString();
                    var tableType = tableRow["TABLE_TYPE"].ToString();

                    // Skip system tables
                    if (tableType != "TABLE" || tableName?.StartsWith("MSys") == true)
                        continue;

                    result.AppendLine($"📋 Table: {tableName}");

                    // Get columns for this table
                    try
                    {
                        using var cmd = new OleDbCommand($"SELECT TOP 1 * FROM [{tableName}]", connection);
                        using var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly);
                        var schemaTable = reader.GetSchemaTable();

                        if (schemaTable != null)
                        {
                            result.AppendLine("   Columns:");
                            foreach (DataRow row in schemaTable.Rows)
                            {
                                var columnName = row["ColumnName"].ToString();
                                var dataType = row["DataType"].ToString();
                                result.AppendLine($"   - {columnName} ({dataType})");
                            }

                            // Get row count
                            using var countCmd = new OleDbCommand($"SELECT COUNT(*) FROM [{tableName}]", connection);
                            var count = countCmd.ExecuteScalar();
                            result.AppendLine($"   📊 Row count: {count}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine($"   ⚠️ Error reading table: {ex.Message}");
                    }

                    result.AppendLine();
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("provider") || ex.Message.Contains("registered"))
            {
                result.AppendLine($"❌ DATABASE PROVIDER ERROR:\n");
                result.AppendLine(ex.Message);
            }
            catch (Exception ex)
            {
                result.AppendLine($"❌ ERROR: {ex.Message}");
                result.AppendLine(ex.ToString());
            }

            return result.ToString();
        }

        // Safe data extraction helpers - improved with better error reporting
        private static string? SafeGetString(OleDbDataReader reader, string columnName)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal))
                    return null;

                var value = reader.GetValue(ordinal);
                return value?.ToString()?.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static int? SafeGetInt(OleDbDataReader reader, string columnName)
        {
            try
            {
// Skip to next column on error
                try
                {
                    var ordinal = reader.GetOrdinal(columnName);
                    if (reader.IsDBNull(ordinal))
                        return null;

                    return Convert.ToInt32(reader.GetValue(ordinal));
                }
                catch
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool SafeGetBool(OleDbDataReader reader, string columnName)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal))
                    return false;

                var value = reader.GetValue(ordinal);
                return Convert.ToBoolean(value);
            }
            catch
            {
                return false;
            }
        }

        private static DateTime? SafeGetDateTime(OleDbDataReader reader, string columnName)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal))
                    return null;

                return reader.GetDateTime(ordinal);
            }
            catch
            {
                return null;
            }
        }

        private static TimeSpan? SafeGetTimeSpan(OleDbDataReader reader, string columnName)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal))
                    return null;

                var value = reader.GetValue(ordinal);
                if (value is TimeSpan ts) return ts;
                if (value is DateTime dt) return dt.TimeOfDay;
                if (TimeSpan.TryParse(value.ToString(), out var parsed)) return parsed;

                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Summary of what was imported.
    /// </summary>
    public class ImportSummary
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int DivisionsImported { get; set; }
        public int VenuesImported { get; set; }
        public int TeamsImported { get; set; }
        public int PlayersImported { get; set; }
        public int SeasonsImported { get; set; }
        public int FixturesImported { get; set; }
        public int FramesImported { get; set; }
        public List<string> Errors { get; set; } = new(); // Now also contains diagnostic logs

        public string Summary =>
            $"Divisions: {DivisionsImported}\n" +
            $"Venues: {VenuesImported}\n" +
            $"Teams: {TeamsImported}\n" +
            $"Players: {PlayersImported}\n" +
            $"Seasons: {SeasonsImported}\n" +
            $"Fixtures: {FixturesImported}\n" +
            $"Frames: {FramesImported}";

        public string DiagnosticLog => string.Join("\n", Errors);
        
        /// <summary>
        /// Generate a complete import log for export/saving
        /// </summary>
        public string GenerateFullLog(string? sourcePath = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("                    IMPORT LOG REPORT");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (!string.IsNullOrEmpty(sourcePath))
                sb.AppendLine($"Source: {sourcePath}");
            sb.AppendLine($"Status: {(Success ? "✓ SUCCESS" : "✗ FAILED")}");
            sb.AppendLine();

            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("                        SUMMARY");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine();
            sb.AppendLine($"  Seasons:   {SeasonsImported}");
            sb.AppendLine($"  Divisions: {DivisionsImported}");
            sb.AppendLine($"  Venues:    {VenuesImported}");
            sb.AppendLine($"  Teams:     {TeamsImported}");
            sb.AppendLine($"  Players:   {PlayersImported}");
            sb.AppendLine($"  Fixtures:  {FixturesImported}");
            sb.AppendLine($"  Frames:    {FramesImported}");
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("                     DETAILED LOG");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine();
            foreach (var line in Errors)
            {
                sb.AppendLine(line);
            }
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("                      END OF LOG");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Save the import log to a file
        /// </summary>
        public async Task<(bool success, string message)> SaveLogToFileAsync(string? sourcePath = null)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"import_log_{timestamp}.txt";
                
                // Use file saver to let user choose location
                var result = await FileSaver.Default.SaveAsync(fileName, 
                    new MemoryStream(Encoding.UTF8.GetBytes(GenerateFullLog(sourcePath))), 
                    CancellationToken.None);
                
                if (result.IsSuccessful)
                {
                    return (true, $"Log saved to: {result.FilePath}");
                }
                else
                {
                    return (false, result.Exception?.Message ?? "Save cancelled");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to save log: {ex.Message}");
            }
        }
    }
}