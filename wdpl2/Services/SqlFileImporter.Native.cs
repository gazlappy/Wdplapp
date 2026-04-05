using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// Native-format import support for SQL files exported by <see cref="SqlExportService"/>.
    /// Handles tables named Seasons, Divisions, Venues, VenueTables, Teams, Players,
    /// PlayerTransfers, Fixtures, FrameResults, Competitions, and DoublesPairings with GUID IDs.
    /// </summary>
    public partial class SqlFileImporter
    {
        /// <summary>
        /// Returns true when the parsed SQL contains native-format tables
        /// (exported by SqlExportService) rather than VBA/Access tables.
        /// </summary>
        private static bool IsNativeFormat(ParsedSqlData parsed)
        {
            return parsed.Tables.ContainsKey("Seasons") ||
                   parsed.Tables.ContainsKey("Players") ||
                   parsed.Tables.ContainsKey("Teams");
        }

        /// <summary>
        /// Import all data from a native-format SQL export.
        /// </summary>
        private static Task ImportNativeFormatAsync(
            ParsedSqlData parsed,
            LeagueData importedData,
            LeagueData existingData,
            bool replaceExisting,
            SqlImportResult result,
            Guid? targetSeasonId)
        {
            var tables = parsed.Tables;

            // 1. Season
            ImportNativeSeasons(tables, importedData, existingData, result, targetSeasonId);

            if (result.DetectedSeason == null)
            {
                result.Errors.Add("No season detected from native SQL — cannot import other data");
                return Task.CompletedTask;
            }

            var seasonId = result.DetectedSeason.Id;

            // 2. Divisions
            ImportNativeDivisions(tables, importedData, existingData, result, seasonId);

            // 3. Venues + VenueTables
            ImportNativeVenues(tables, importedData, existingData, result, seasonId);
            ImportNativeVenueTables(tables, importedData, existingData, result);

            // 4. Teams
            ImportNativeTeams(tables, importedData, existingData, result, seasonId);

            // 5. Players + Transfers
            ImportNativePlayers(tables, importedData, existingData, result, seasonId);
            ImportNativePlayerTransfers(tables, importedData, existingData, result);

            // 6. Fixtures + Frames
            ImportNativeFixtures(tables, importedData, existingData, result, seasonId);
            ImportNativeFrameResults(tables, importedData, existingData, result);

            // 7. Competitions
            ImportNativeCompetitions(tables, importedData, existingData, result, seasonId);

            // 8. Doubles Pairings
            ImportNativeDoublesPairings(tables, importedData, existingData, result, seasonId);

            result.DetectedDialect = "WDPL Native";
            return Task.CompletedTask;
        }

        // -------------------------------------------------------
        //  Individual entity importers
        // -------------------------------------------------------

        private static void ImportNativeSeasons(
            Dictionary<string, List<Dictionary<string, string>>> tables,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result,
            Guid? targetSeasonId)
        {
            // If a target season was provided, use it
            if (targetSeasonId.HasValue)
            {
                var target = existingData.Seasons.FirstOrDefault(s => s.Id == targetSeasonId.Value);
                if (target != null)
                {
                    result.DetectedSeason = target;
                    result.Warnings.Add($"Using target season '{target.Name}' (provided by caller)");
                    return;
                }
            }

            if (!tables.TryGetValue("Seasons", out var rows) || rows.Count == 0)
            {
                result.Warnings.Add("No Seasons table found in native SQL");
                return;
            }

            var row = rows[0]; // Use the first (and usually only) season row
            var id = ParseGuid(GetStringValue(row, "Id"));

            if (id == Guid.Empty)
            {
                result.Errors.Add("Season row has no valid Id");
                return;
            }

            // Check if season already exists
            var existing = existingData.Seasons.FirstOrDefault(s => s.Id == id);
            if (existing != null)
            {
                result.DetectedSeason = existing;
                result.Warnings.Add($"Season '{existing.Name}' already exists — adding data to existing season");
                return;
            }

            // Also check by name
            var name = GetStringValue(row, "Name", "Unknown Season");
            existing = existingData.Seasons.FirstOrDefault(s =>
                s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                result.DetectedSeason = existing;
                result.Warnings.Add($"Season '{existing.Name}' already exists (matched by name) — adding data to existing season");
                return;
            }

            var season = new Season
            {
                Id = id,
                Name = name,
                StartDate = GetDateTimeValue(row, "StartDate", DateTime.Now),
                EndDate = GetDateTimeValue(row, "EndDate", DateTime.Now.AddMonths(6)),
                MatchDayOfWeek = (DayOfWeek)GetIntValue(row, "MatchDayOfWeek", 0),
                MatchStartTime = ParseTimeSpan(GetStringValue(row, "MatchStartTime")),
                FramesPerMatch = GetIntValue(row, "FramesPerMatch", 0),
                IsActive = false, // Imported seasons are inactive
                IsLocked = GetIntValue(row, "IsLocked", 0) == 1,
                IncludeDoubles = GetIntValue(row, "IncludeDoubles", 0) == 1,
                SinglesFrameCount = GetIntValue(row, "SinglesFrameCount", 0),
                DoublesFrameCount = GetIntValue(row, "DoublesFrameCount", 0),
                TransferWindowStart = ParseNullableDateTime(GetStringValue(row, "TransferWindowStart")),
                TransferWindowEnd = ParseNullableDateTime(GetStringValue(row, "TransferWindowEnd")),
                CreatedDate = GetDateTimeValue(row, "CreatedDate", DateTime.Now),
                ModifiedDate = GetDateTimeValue(row, "ModifiedDate", DateTime.Now)
            };

            importedData.Seasons.Add(season);
            existingData.Seasons.Add(season);
            result.DetectedSeason = season;
            result.ImportedSeasonIds.Add(season.Id);
        }

        private static void ImportNativeDivisions(
            Dictionary<string, List<Dictionary<string, string>>> tables,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result,
            Guid seasonId)
        {
            if (!tables.TryGetValue("Divisions", out var rows)) return;

            foreach (var row in rows)
            {
                var id = ParseGuid(GetStringValue(row, "Id"));
                if (id == Guid.Empty) continue;

                if (existingData.Divisions.Any(d => d.Id == id)) continue;

                var division = new Division
                {
                    Id = id,
                    SeasonId = seasonId,
                    Name = GetStringValue(row, "Name", "Unknown Division"),
                    Notes = NullIfEmpty(GetStringValue(row, "Notes")),
                    CreatedDate = GetDateTimeValue(row, "CreatedDate", DateTime.Now),
                    ModifiedDate = GetDateTimeValue(row, "ModifiedDate", DateTime.Now)
                };

                importedData.Divisions.Add(division);
                existingData.Divisions.Add(division);
                result.ImportedDivisionIds.Add(division.Id);
            }
        }

        private static void ImportNativeVenues(
            Dictionary<string, List<Dictionary<string, string>>> tables,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result,
            Guid seasonId)
        {
            if (!tables.TryGetValue("Venues", out var rows)) return;

            foreach (var row in rows)
            {
                var id = ParseGuid(GetStringValue(row, "Id"));
                if (id == Guid.Empty) continue;

                if (existingData.Venues.Any(v => v.Id == id)) continue;

                var venue = new Venue
                {
                    Id = id,
                    SeasonId = seasonId,
                    Name = GetStringValue(row, "Name", "Unknown Venue"),
                    Address = NullIfEmpty(GetStringValue(row, "Address")),
                    Notes = NullIfEmpty(GetStringValue(row, "Notes")),
                    CreatedDate = GetDateTimeValue(row, "CreatedDate", DateTime.Now),
                    ModifiedDate = GetDateTimeValue(row, "ModifiedDate", DateTime.Now),
                    Tables = new List<VenueTable>()
                };

                importedData.Venues.Add(venue);
                existingData.Venues.Add(venue);
                result.ImportedVenueIds.Add(venue.Id);
                result.VenuesImported++;
            }
        }

        private static void ImportNativeVenueTables(
            Dictionary<string, List<Dictionary<string, string>>> tables,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            if (!tables.TryGetValue("VenueTables", out var rows)) return;

            foreach (var row in rows)
            {
                var id = ParseGuid(GetStringValue(row, "Id"));
                var venueId = ParseGuid(GetStringValue(row, "VenueId"));
                if (id == Guid.Empty || venueId == Guid.Empty) continue;

                var venue = existingData.Venues.FirstOrDefault(v => v.Id == venueId);
                if (venue == null) continue;
                if (venue.Tables.Any(t => t.Id == id)) continue;

                venue.Tables.Add(new VenueTable
                {
                    Id = id,
                    Label = GetStringValue(row, "Label", "Table 1"),
                    MaxTeams = GetIntValue(row, "MaxTeams", 2)
                });
            }
        }

        private static void ImportNativeTeams(
            Dictionary<string, List<Dictionary<string, string>>> tables,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result,
            Guid seasonId)
        {
            if (!tables.TryGetValue("Teams", out var rows)) return;

            foreach (var row in rows)
            {
                var id = ParseGuid(GetStringValue(row, "Id"));
                if (id == Guid.Empty) continue;

                if (existingData.Teams.Any(t => t.Id == id))
                {
                    result.TeamsSkipped++;
                    continue;
                }

                var team = new Team
                {
                    Id = id,
                    SeasonId = seasonId,
                    GlobalTeamId = ParseNullableGuid(GetStringValue(row, "GlobalTeamId")),
                    Name = GetStringValue(row, "Name"),
                    DivisionId = ParseNullableGuid(GetStringValue(row, "DivisionId")),
                    VenueId = ParseNullableGuid(GetStringValue(row, "VenueId")),
                    TableId = ParseNullableGuid(GetStringValue(row, "TableId")),
                    ProvidesFood = GetIntValue(row, "ProvidesFood", 0) == 1,
                    CaptainPlayerId = ParseNullableGuid(GetStringValue(row, "CaptainPlayerId")),
                    Captain = NullIfEmpty(GetStringValue(row, "Captain")),
                    CaptainPlayed = GetIntValue(row, "CaptainPlayed", 0) == 1,
                    Notes = NullIfEmpty(GetStringValue(row, "Notes")),
                    CreatedDate = GetDateTimeValue(row, "CreatedDate", DateTime.Now),
                    ModifiedDate = GetDateTimeValue(row, "ModifiedDate", DateTime.Now)
                };

                importedData.Teams.Add(team);
                existingData.Teams.Add(team);
                result.ImportedTeamIds.Add(team.Id);
                result.TeamsImported++;
            }
        }

        private static void ImportNativePlayers(
            Dictionary<string, List<Dictionary<string, string>>> tables,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result,
            Guid seasonId)
        {
            if (!tables.TryGetValue("Players", out var rows)) return;

            foreach (var row in rows)
            {
                var id = ParseGuid(GetStringValue(row, "Id"));
                if (id == Guid.Empty) continue;

                if (existingData.Players.Any(p => p.Id == id))
                {
                    result.PlayersSkipped++;
                    continue;
                }

                var player = new Player
                {
                    Id = id,
                    SeasonId = seasonId,
                    GlobalPlayerId = ParseNullableGuid(GetStringValue(row, "GlobalPlayerId")),
                    FirstName = NullIfEmpty(GetStringValue(row, "FirstName")),
                    LastName = NullIfEmpty(GetStringValue(row, "LastName")),
                    TeamId = ParseNullableGuid(GetStringValue(row, "TeamId")),
                    IsActive = GetIntValue(row, "IsActive", 1) == 1,
                    DeactivatedDate = ParseNullableDateTime(GetStringValue(row, "DeactivatedDate")),
                    DeactivationReason = NullIfEmpty(GetStringValue(row, "DeactivationReason")),
                    Notes = NullIfEmpty(GetStringValue(row, "Notes")),
                    CreatedDate = GetDateTimeValue(row, "CreatedDate", DateTime.Now),
                    ModifiedDate = GetDateTimeValue(row, "ModifiedDate", DateTime.Now)
                };

                importedData.Players.Add(player);
                existingData.Players.Add(player);
                result.ImportedPlayerIds.Add(player.Id);
                result.PlayersImported++;
            }
        }

        private static void ImportNativePlayerTransfers(
            Dictionary<string, List<Dictionary<string, string>>> tables,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            if (!tables.TryGetValue("PlayerTransfers", out var rows)) return;

            foreach (var row in rows)
            {
                var playerId = ParseGuid(GetStringValue(row, "PlayerId"));
                if (playerId == Guid.Empty) continue;

                var player = existingData.Players.FirstOrDefault(p => p.Id == playerId);
                if (player == null) continue;

                player.TransferHistory ??= new List<PlayerTransfer>();

                var transferId = ParseGuid(GetStringValue(row, "Id"));
                if (transferId != Guid.Empty && player.TransferHistory.Any(t => t.Id == transferId))
                    continue;

                player.TransferHistory.Add(new PlayerTransfer
                {
                    Id = transferId != Guid.Empty ? transferId : Guid.NewGuid(),
                    FromTeamId = ParseGuid(GetStringValue(row, "FromTeamId")),
                    FromTeamName = GetStringValue(row, "FromTeamName"),
                    ToTeamId = ParseGuid(GetStringValue(row, "ToTeamId")),
                    ToTeamName = GetStringValue(row, "ToTeamName"),
                    TransferDate = GetDateTimeValue(row, "TransferDate", DateTime.Now),
                    RatingAtTransfer = GetIntValue(row, "RatingAtTransfer", 0),
                    FramesPlayedAtTransfer = GetIntValue(row, "FramesPlayedAtTransfer", 0),
                    WinsAtTransfer = GetIntValue(row, "WinsAtTransfer", 0),
                    LossesAtTransfer = GetIntValue(row, "LossesAtTransfer", 0),
                    Notes = NullIfEmpty(GetStringValue(row, "Notes"))
                });
            }
        }

        private static void ImportNativeFixtures(
            Dictionary<string, List<Dictionary<string, string>>> tables,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result,
            Guid seasonId)
        {
            if (!tables.TryGetValue("Fixtures", out var rows)) return;

            foreach (var row in rows)
            {
                var id = ParseGuid(GetStringValue(row, "Id"));
                if (id == Guid.Empty) continue;

                if (existingData.Fixtures.Any(f => f.Id == id))
                {
                    result.FixturesSkipped++;
                    continue;
                }

                var fixture = new Fixture
                {
                    Id = id,
                    SeasonId = seasonId,
                    DivisionId = ParseNullableGuid(GetStringValue(row, "DivisionId")),
                    HomeTeamId = ParseGuid(GetStringValue(row, "HomeTeamId")),
                    AwayTeamId = ParseGuid(GetStringValue(row, "AwayTeamId")),
                    VenueId = ParseNullableGuid(GetStringValue(row, "VenueId")),
                    TableId = ParseNullableGuid(GetStringValue(row, "TableId")),
                    Date = GetDateTimeValue(row, "Date", DateTime.Now),
                    HomeLatePenalty = GetIntValue(row, "HomeLatePenalty", 0),
                    AwayLatePenalty = GetIntValue(row, "AwayLatePenalty", 0),
                    CancelledByTeam = (FrameWinner)GetIntValue(row, "CancelledByTeam", 0),
                    CancellationPenalty = GetIntValue(row, "CancellationPenalty", 0),
                    CreatedDate = GetDateTimeValue(row, "CreatedDate", DateTime.Now),
                    ModifiedDate = GetDateTimeValue(row, "ModifiedDate", DateTime.Now)
                };

                importedData.Fixtures.Add(fixture);
                existingData.Fixtures.Add(fixture);
                result.ImportedFixtureIds.Add(fixture.Id);
                result.FixturesImported++;
            }
        }

        private static void ImportNativeFrameResults(
            Dictionary<string, List<Dictionary<string, string>>> tables,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            if (!tables.TryGetValue("FrameResults", out var rows)) return;

            foreach (var row in rows)
            {
                var fixtureId = ParseGuid(GetStringValue(row, "FixtureId"));
                if (fixtureId == Guid.Empty) continue;

                var fixture = existingData.Fixtures.FirstOrDefault(f => f.Id == fixtureId);
                if (fixture == null) continue;

                var frameNo = GetIntValue(row, "Number", 0);
                if (frameNo <= 0) continue;
                if (fixture.Frames.Any(f => f.Number == frameNo)) continue;

                var frame = new FrameResult
                {
                    Number = frameNo,
                    HomePlayerId = ParseNullableGuid(GetStringValue(row, "HomePlayerId")),
                    AwayPlayerId = ParseNullableGuid(GetStringValue(row, "AwayPlayerId")),
                    HomePlayer2Id = ParseNullableGuid(GetStringValue(row, "HomePlayer2Id")),
                    AwayPlayer2Id = ParseNullableGuid(GetStringValue(row, "AwayPlayer2Id")),
                    Winner = (FrameWinner)GetIntValue(row, "Winner", 0),
                    EightBall = GetIntValue(row, "EightBall", 0) == 1,
                    IsDoubles = GetIntValue(row, "IsDoubles", 0) == 1,
                    HomeOppRating = ParseNullableInt(GetStringValue(row, "HomeOppRating")),
                    HomePlayerRating = ParseNullableInt(GetStringValue(row, "HomePlayerRating")),
                    AwayOppRating = ParseNullableInt(GetStringValue(row, "AwayOppRating")),
                    AwayPlayerRating = ParseNullableInt(GetStringValue(row, "AwayPlayerRating")),
                    WeekNo = ParseNullableInt(GetStringValue(row, "WeekNo"))
                };

                fixture.Frames.Add(frame);
                result.FramesImported++;
            }

            // Count results (fixtures that have frames)
            result.ResultsImported = existingData.Fixtures
                .Count(f => result.ImportedFixtureIds.Contains(f.Id) && f.Frames.Count > 0);
        }

        private static void ImportNativeCompetitions(
            Dictionary<string, List<Dictionary<string, string>>> tables,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result,
            Guid seasonId)
        {
            if (!tables.TryGetValue("Competitions", out var rows)) return;

            existingData.Competitions ??= new List<Competition>();
            importedData.Competitions ??= new List<Competition>();

            foreach (var row in rows)
            {
                var id = ParseGuid(GetStringValue(row, "Id"));
                if (id == Guid.Empty) continue;

                if (existingData.Competitions.Any(c => c.Id == id))
                {
                    result.CompetitionsSkipped++;
                    continue;
                }

                var competition = new Competition
                {
                    Id = id,
                    SeasonId = seasonId,
                    Name = GetStringValue(row, "Name", "Unknown Competition"),
                    Format = (CompetitionFormat)GetIntValue(row, "Format", 0),
                    Status = (CompetitionStatus)GetIntValue(row, "Status", 0),
                    StartDate = ParseNullableDateTime(GetStringValue(row, "StartDate")),
                    Notes = NullIfEmpty(GetStringValue(row, "Notes")),
                    BestOf = GetIntValue(row, "BestOf", 0),
                    RandomDraw = GetIntValue(row, "RandomDraw", 1) == 1,
                    CreatedDate = GetDateTimeValue(row, "CreatedDate", DateTime.Now),
                    PlateCompetitionId = ParseNullableGuid(GetStringValue(row, "PlateCompetitionId")),
                    ParentCompetitionId = ParseNullableGuid(GetStringValue(row, "ParentCompetitionId"))
                };

                importedData.Competitions.Add(competition);
                existingData.Competitions.Add(competition);
                result.ImportedCompetitionIds.Add(competition.Id);
                result.CompetitionsImported++;
            }
        }

        private static void ImportNativeDoublesPairings(
            Dictionary<string, List<Dictionary<string, string>>> tables,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result,
            Guid seasonId)
        {
            if (!tables.TryGetValue("DoublesPairings", out var rows)) return;

            foreach (var row in rows)
            {
                var id = ParseGuid(GetStringValue(row, "Id"));
                if (id == Guid.Empty) continue;

                if (existingData.DoublesPairings.Any(dp => dp.Id == id)) continue;

                var pairing = new DoublesPairing
                {
                    Id = id,
                    SeasonId = seasonId,
                    DivisionId = ParseNullableGuid(GetStringValue(row, "DivisionId")),
                    TeamId = ParseNullableGuid(GetStringValue(row, "TeamId")),
                    Player1Id = ParseNullableGuid(GetStringValue(row, "Player1Id")),
                    Player2Id = ParseNullableGuid(GetStringValue(row, "Player2Id")),
                    Player1Name = NullIfEmpty(GetStringValue(row, "Player1Name")),
                    Player2Name = NullIfEmpty(GetStringValue(row, "Player2Name")),
                    TeamName = NullIfEmpty(GetStringValue(row, "TeamName")),
                    Played = GetIntValue(row, "Played", 0),
                    Won = GetIntValue(row, "Won", 0),
                    Lost = GetIntValue(row, "Lost", 0),
                    BestRating = GetIntValue(row, "BestRating", 0),
                    BestRatingDate = ParseNullableDateTime(GetStringValue(row, "BestRatingDate")),
                    CurrentRating = GetIntValue(row, "CurrentRating", 0)
                };

                importedData.DoublesPairings.Add(pairing);
                existingData.DoublesPairings.Add(pairing);
            }
        }

        // -------------------------------------------------------
        //  Parsing helpers for native format
        // -------------------------------------------------------

        private static Guid ParseGuid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Guid.Empty;
            return Guid.TryParse(value, out var g) ? g : Guid.Empty;
        }

        private static Guid? ParseNullableGuid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Guid.TryParse(value, out var g) ? g : null;
        }

        private static int? ParseNullableInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return int.TryParse(value, out var i) ? i : null;
        }

        private static DateTime? ParseNullableDateTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, out var dt) ? dt : null;
        }

        private static TimeSpan ParseTimeSpan(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return TimeSpan.Zero;
            return TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero;
        }

        private static string? NullIfEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
