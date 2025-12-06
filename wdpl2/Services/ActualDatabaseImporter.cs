using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// Specialized importer for your actual Access database structure - VERSION 2
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class ActualDatabaseImporterV2  // Changed class name
    {
        private readonly string _connectionString;
        private readonly Dictionary<int, Guid> _divisionMap = new();
        private readonly Dictionary<int, Guid> _venueMap = new();
        private readonly Dictionary<int, (Guid venueId, string? tableLabel)> _venueTableMap = new();
        private Dictionary<string, (Guid venueId, string? tableLabel)> _venueNameMap = new(); // NEW: Map venue names to venue+table
        private readonly Dictionary<int, Guid> _teamMap = new();
        private readonly Dictionary<int, Guid> _playerMap = new();
        private Guid _seasonId = Guid.NewGuid();

        public ActualDatabaseImporterV2(string databasePath)  // Changed constructor
        {
            var provider = "Microsoft.ACE.OLEDB.12.0";
            _connectionString = $"Provider={provider};Data Source={databasePath};";
            
            // FORCE RECOMPILE - DO NOT REMOVE
            var forceRecompile = DateTime.Now.Ticks;
        }

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
                    summary.Errors.Add("✓ Connected successfully");
                    summary.Errors.Add("🔥🔥🔥 NEW CODE VERSION - BUILD TIME: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 🔥🔥🔥");

                    // Import in dependency order
                    ImportLeagueAndSeason(connection, data, summary);
                    ImportDivisions(connection, data, summary);
                    ImportVenues(connection, data, summary); // Extract from teams
                    ImportTeams(connection, data, summary);
                    ImportPlayers(connection, data, summary);
                    ImportFixtures(connection, data, summary);
                });

                summary.Success = true;
                summary.Message = "Import completed!";
            }
            catch (Exception ex)
            {
                summary.Success = false;
                summary.Message = $"Import failed: {ex.Message}";
                summary.Errors.Add($"❌ ERROR: {ex}");
            }

            return (data, summary);
        }

        private void ImportLeagueAndSeason(OleDbConnection conn, LeagueData data, ImportSummary summary)
        {
            try
            {
                using var cmd = new OleDbCommand("SELECT * FROM tblLeague", conn);
                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    var leagueName = reader["LeagueName"]?.ToString() ?? "League";
                    var seasonName = reader["SeasonName"]?.ToString() ?? "Season";
                    var seasonYear = reader["SeasonYear"] != DBNull.Value ? Convert.ToInt32(reader["SeasonYear"]) : DateTime.Now.Year;
                    var frames = reader["Frames"] != DBNull.Value ? Convert.ToInt32(reader["Frames"]) : 10;
                    var firstMatchDate = reader["FirstMatchDate"] != DBNull.Value ? Convert.ToDateTime(reader["FirstMatchDate"]) : DateTime.Today;

                    var season = new Season
                    {
                        Id = _seasonId,
                        Name = $"[IMPORTED] {seasonName} {seasonYear}", // Mark as imported
                        StartDate = firstMatchDate,
                        EndDate = firstMatchDate.AddMonths(6), // Estimate
                        MatchDayOfWeek = DayOfWeek.Tuesday, // Default
                        MatchStartTime = new TimeSpan(19, 30, 0),
                        FramesPerMatch = frames,
                        IsActive = false // Make sure imported season is not active
                    };

                    season.NormaliseDates();
                    data.Seasons.Add(season);
                    data.ActiveSeasonId = _seasonId;
                    summary.SeasonsImported = 1;
                    summary.Errors.Add($"✓ Imported season: {season.Name}");
                }
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Season import error: {ex.Message}");
            }
        }

        private void ImportDivisions(OleDbConnection conn, LeagueData data, ImportSummary summary)
        {
            try
            {
                using var cmd = new OleDbCommand("SELECT * FROM tblDivisions", conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var id = Convert.ToInt32(reader["ID"]);
                    var name = reader["DivisionName"]?.ToString() ?? "Unknown";
                    var bandColour = reader["BandColour"]?.ToString();

                    var division = new Division
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = _seasonId,  // ADD THIS
                        Name = name,
                        Notes = $"[IMPORTED] {bandColour}" // Mark as imported
                    };

                    data.Divisions.Add(division);
                    _divisionMap[id] = division.Id;
                    summary.DivisionsImported++;
                }

                summary.Errors.Add($"✓ Imported {summary.DivisionsImported} divisions");
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Division error: {ex.Message}");
            }
        }

        private void ImportVenues(OleDbConnection conn, LeagueData data, ImportSummary summary)
        {
            try
            {
                summary.Errors.Add("=== VENUE IMPORT SKIPPED ===");
                summary.Errors.Add("Venues will be extracted from team data instead.");
                summary.Errors.Add("After import, use 'Fix Missing Season IDs' to assign venues to the season.");
                summary.VenuesImported = 0;
                
                // Initialize empty mapping so team import doesn't fail
                _venueNameMap = new Dictionary<string, (Guid venueId, string? tableLabel)>();
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Venue import error: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void ImportTeams(OleDbConnection conn, LeagueData data, ImportSummary summary)
        {
            try
            {
                using var cmd = new OleDbCommand("SELECT * FROM tblTeams WHERE Withdrawn = False OR Withdrawn IS NULL", conn);
                using var reader = cmd.ExecuteReader();

                // Track venues we create on the fly
                var venuesByName = new Dictionary<string, Venue>();

                while (reader.Read())
                {
                    var teamId = Convert.ToInt32(reader["TeamID"]);
                    var teamName = reader["TeamName"]?.ToString() ?? "Unknown Team";
                    var divisionId = reader["Division"] != DBNull.Value ? Convert.ToInt32(reader["Division"]) : 0;
                    
                    // Try to get venue name - use safe conversion
                    string? fullVenueName = null;
                    try
                    {
                        var venueNameObj = reader["VenueName"];
                        if (venueNameObj != null && venueNameObj != DBNull.Value)
                        {
                            fullVenueName = Convert.ToString(venueNameObj)?.Trim();
                        }
                    }
                    catch
                    {
                        // If we can't read VenueName, just skip venue assignment
                        fullVenueName = null;
                    }
                    
                    var captain = reader["CaptainName"]?.ToString();
                    var contact = reader["ContactNo"]?.ToString();

                    // Create venue on the fly if needed
                    Guid? assignedVenueId = null;
                    Guid? assignedTableId = null;
                    
                    if (!string.IsNullOrEmpty(fullVenueName))
                    {
                        // Parse venue name to extract base and table
                        string baseName;
                        string? tableLabel = null;
                        
                        var parts = fullVenueName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        if (parts.Length > 1)
                        {
                            var lastPart = parts.Last().ToUpper();
                            
                            if (lastPart.StartsWith("TB") || lastPart == "BAR" || lastPart.Length <= 3)
                            {
                                baseName = string.Join(" ", parts.Take(parts.Length - 1));
                                tableLabel = lastPart;
                            }
                            else
                            {
                                baseName = fullVenueName;
                            }
                        }
                        else
                        {
                            baseName = fullVenueName;
                        }

                        // Get or create venue
                        if (!venuesByName.ContainsKey(baseName))
                        {
                            var newVenue = new Venue
                            {
                                Id = Guid.NewGuid(),
                                SeasonId = _seasonId,
                                Name = baseName,
                                Address = null,
                                Notes = "[IMPORTED FROM TEAM DATA]",
                                Tables = new List<VenueTable>()
                            };
                            
                            venuesByName[baseName] = newVenue;
                            data.Venues.Add(newVenue);
                        }
                        
                        var targetVenue = venuesByName[baseName];
                        assignedVenueId = targetVenue.Id;
                        
                        // Add table if needed
                        if (tableLabel != null)
                        {
                            var table = targetVenue.Tables.FirstOrDefault(t => t.Label == tableLabel);
                            if (table == null)
                            {
                                table = new VenueTable
                                {
                                    Id = Guid.NewGuid(),
                                    Label = tableLabel,
                                    MaxTeams = 2
                                };
                                targetVenue.Tables.Add(table);
                            }
                            assignedTableId = table.Id;
                        }
                        else
                        {
                            // Single table venue
                            if (!targetVenue.Tables.Any())
                            {
                                var table = new VenueTable
                                {
                                    Id = Guid.NewGuid(),
                                    Label = "Main",
                                    MaxTeams = 2
                                };
                                targetVenue.Tables.Add(table);
                            }
                            assignedTableId = targetVenue.Tables.First().Id;
                        }
                    }

                    var team = new Team
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = _seasonId,
                        Name = teamName,
                        DivisionId = _divisionMap.ContainsKey(divisionId) ? _divisionMap[divisionId] : null,
                        VenueId = assignedVenueId,
                        TableId = assignedTableId,
                        Captain = captain,
                        Notes = $"[IMPORTED] {contact}",
                        ProvidesFood = false
                    };

                    data.Teams.Add(team);
                    _teamMap[teamId] = team.Id;
                    summary.TeamsImported++;
                }

                // Update venues imported count
                summary.VenuesImported = venuesByName.Count;
                if (venuesByName.Count > 0)
                {
                    summary.Errors.Add($"✓ Created {venuesByName.Count} venues from team data with {data.Venues.Sum(v => v.Tables.Count)} tables");
                }
                
                summary.Errors.Add($"✓ Imported {summary.TeamsImported} teams");
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Team error: {ex.Message}");
                summary.Errors.Add($"   Stack: {ex.StackTrace}");
            }
        }

        private void ImportPlayers(OleDbConnection conn, LeagueData data, ImportSummary summary
)
        {
            try
            {
                using var cmd = new OleDbCommand("SELECT * FROM tblPlayers WHERE Active = True OR Active IS NULL", conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var playerId = Convert.ToInt32(reader["PlayerID"]);
                    var fullName = reader["PlayerName"]?.ToString() ?? "Unknown";
                    var teamId = reader["Team"] != DBNull.Value ? Convert.ToInt32(reader["Team"]) : 0;

                    // Split name into first/last (simple split on last space)
                    var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var firstName = parts.Length > 0 ? string.Join(" ", parts.Take(parts.Length - 1)) : fullName;
                    var lastName = parts.Length > 0 ? parts.Last() : "";

                    if (string.IsNullOrEmpty(firstName))
                    {
                        firstName = fullName;
                        lastName = "";
                    }

                    var player = new Player
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = _seasonId,  // ADD THIS (already there)
                        FirstName = firstName,
                        LastName = lastName,
                        TeamId = _teamMap.ContainsKey(teamId) ? _teamMap[teamId] : null,
                        Notes = "[IMPORTED]" // Mark as imported
                    };

                    data.Players.Add(player);
                    _playerMap[playerId] = player.Id;
                    summary.PlayersImported++;
                }

                summary.Errors.Add($"✓ Imported {summary.PlayersImported} players");
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Player error: {ex.Message}");
            }
        }

        private void ImportFixtures(OleDbConnection conn, LeagueData data, ImportSummary summary)
        {
            try
            {
                // Join tblMatchHeader with tblFixtures to get the date
                var query = @"
                    SELECT h.MatchNo, h.Division, h.TeamHome, h.TeamAway, f.MatchDate
                    FROM tblMatchHeader h
                    LEFT JOIN tblFixtures f ON h.MatchNo = f.MatchNo
                    ORDER BY f.MatchDate, h.MatchNo";

                using var cmd = new OleDbCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                var fixtures = new Dictionary<int, Fixture>();

                while (reader.Read())
                {
                    var matchNo = Convert.ToInt32(reader["MatchNo"]);
                    var divisionId = reader["Division"] != DBNull.Value ? Convert.ToInt32(reader["Division"]) : 0;
                    var homeTeamId = Convert.ToInt32(reader["TeamHome"]);
                    var awayTeamId = Convert.ToInt32(reader["TeamAway"]);
                    var matchDate = reader["MatchDate"] != DBNull.Value ? Convert.ToDateTime(reader["MatchDate"]) : DateTime.Today;

                    var fixture = new Fixture
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = _seasonId,
                        DivisionId = _divisionMap.ContainsKey(divisionId) ? _divisionMap[divisionId] : null,
                        HomeTeamId = _teamMap.ContainsKey(homeTeamId) ? _teamMap[homeTeamId] : Guid.Empty,
                        AwayTeamId = _teamMap.ContainsKey(awayTeamId) ? _teamMap[awayTeamId] : Guid.Empty,
                        Date = matchDate,
                        VenueId = null // Could look up home team's venue
                    };

                    fixtures[matchNo] = fixture;
                    data.Fixtures.Add(fixture);
                    summary.FixturesImported++;
                }

                summary.Errors.Add($"✓ Imported {summary.FixturesImported} fixtures");

                // Import frames
                ImportFrames(conn, fixtures, summary);
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"❌ Fixture error: {ex.Message}");
            }
        }

        private void ImportFrames(OleDbConnection conn, Dictionary<int, Fixture> fixtures, ImportSummary summary)
        {
            try
            {
                using var cmd = new OleDbCommand("SELECT * FROM tblMatchDetail ORDER BY MatchNo, FrameNo", conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var matchNo = Convert.ToInt32(reader["MatchNo"]);
                    if (!fixtures.ContainsKey(matchNo)) continue;

                    var frameNo = Convert.ToInt32(reader["FrameNo"]);
                    var player1Id = reader["Player1"] != DBNull.Value ? Convert.ToInt32(reader["Player1"]) : 0;
                    var player2Id = reader["Player2"] != DBNull.Value ? Convert.ToInt32(reader["Player2"]) : 0;
                    var homeScore = reader["HomeScore"] != DBNull.Value ? Convert.ToInt32(reader["HomeScore"]) : 0;
                    var awayScore = reader["AwayScore"] != DBNull.Value ? Convert.ToInt32(reader["AwayScore"]) : 0;
                    var eightBall = reader["Achived8Ball"] != DBNull.Value && Convert.ToBoolean(reader["Achived8Ball"]);

                    var frame = new FrameResult
                    {
                        Number = frameNo,
                        HomePlayerId = _playerMap.ContainsKey(player1Id) ? _playerMap[player1Id] : null,
                        AwayPlayerId = _playerMap.ContainsKey(player2Id) ? _playerMap[player2Id] : null,
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
                summary.Errors.Add($"❌ Frame error: {ex.Message}");
            }
        }
    }
}