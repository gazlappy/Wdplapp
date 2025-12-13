using System.Text;
using System.Text.RegularExpressions;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    public class SqlFileImporter
    {
        public class SqlImportResult
        {
            public bool Success { get; set; }
            public string DetectedDialect { get; set; } = "Unknown";
            public Season? DetectedSeason { get; set; }
            public List<string> Warnings { get; set; } = new();
            public List<string> Errors { get; set; } = new();
            public int TeamsImported { get; set; }
            public int PlayersImported { get; set; }
            public int FixturesImported { get; set; }
            public int ResultsImported { get; set; }
            public int FramesImported { get; set; }
            public int TeamsSkipped { get; set; }
            public int PlayersSkipped { get; set; }
            public int FixturesSkipped { get; set; }
            public int ResultsSkipped { get; set; }
            public int FramesSkipped { get; set; }
            public List<Guid> ImportedSeasonIds { get; set; } = new();
            public List<Guid> ImportedDivisionIds { get; set; } = new();
            public List<Guid> ImportedTeamIds { get; set; } = new();
            public List<Guid> ImportedPlayerIds { get; set; } = new();
            public List<Guid> ImportedFixtureIds { get; set; } = new();

            // VBA ID mappings for reference
            public Dictionary<int, Guid> VbaTeamIdToGuid { get; set; } = new();
            public Dictionary<int, Guid> VbaPlayerIdToGuid { get; set; } = new();
            
            // ID-to-Name mappings from SQL file
            public Dictionary<int, string> VbaPlayerIdToName { get; set; } = new();
            public Dictionary<int, string> VbaTeamIdToName { get; set; } = new();

            public string Summary =>
                $"? Season: {DetectedSeason?.Name ?? "None"}\n" +
                $"? Teams: {TeamsImported} imported, {TeamsSkipped} skipped\n" +
                $"? Players: {PlayersImported} imported, {PlayersSkipped} skipped\n" +
                $"? Fixtures: {FixturesImported} imported, {FixturesSkipped} skipped\n" +
                $"? Results: {ResultsImported} matches imported, {ResultsSkipped} skipped\n" +
                $"? Frames: {FramesImported} imported, {FramesSkipped} skipped\n" +
                $"? Warnings: {Warnings.Count}\n" +
                $"? Errors: {Errors.Count}";
        }

        public class ParsedSqlData
        {
            public Dictionary<string, List<Dictionary<string, string>>> Tables { get; set; } = new();
            public string DetectedDialect { get; set; } = "Unknown";
        }

        /// <summary>
        /// Parse SQL file without importing - for preview
        /// </summary>
        public static async Task<ParsedSqlData> ParseSqlFileAsync(string sqlFilePath)
        {
            var result = new ParsedSqlData();
            var tempResult = new SqlImportResult();

            try
            {
                var sqlContent = await File.ReadAllTextAsync(sqlFilePath);
                result.DetectedDialect = DetectSqlDialect(sqlContent);
                sqlContent = CleanSqlContent(sqlContent);
                result.Tables = ParseSqlContent(sqlContent, tempResult);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse SQL file: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// Full import with all data types
        /// </summary>
        public static async Task<(LeagueData importedData, SqlImportResult result)> ImportFromSqlFileAsync(
            string sqlFilePath,
            LeagueData existingData,
            bool replaceExisting)
        {
            var result = new SqlImportResult();
            var importedData = new LeagueData();

            try
            {
                // Parse SQL
                var parsed = await ParseSqlFileAsync(sqlFilePath);
                result.DetectedDialect = parsed.DetectedDialect;

                // Step 1: Build ID-to-Name mappings from lookup tables
                BuildIdToNameMappings(parsed.Tables, result);

                // Step 2: Import in order of dependencies
                await ImportSeasonData(parsed.Tables, importedData, existingData, replaceExisting, result);
                
                if (result.DetectedSeason != null)
                {
                    await ImportDivisions(parsed.Tables, importedData, result);
                    await ImportTeams(parsed.Tables, importedData, existingData, result);
                    await ImportPlayers(parsed.Tables, importedData, existingData, result);
                    await ImportFixtures(parsed.Tables, importedData, existingData, result);
                    await ImportResults(parsed.Tables, importedData, existingData, result);
                }
                else
                {
                    result.Errors.Add("No season detected - cannot import other data");
                }

                result.Success = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Fatal error: {ex.Message}");
                result.Success = false;
            }

            return (importedData, result);
        }

        private static string DetectSqlDialect(string sqlContent)
        {
            if (sqlContent.Contains("ENGINE=") || sqlContent.Contains("CHARSET="))
                return "MySQL/phpMyAdmin";
            if (sqlContent.Contains("bit(1)"))
                return "MySQL";
            return "Standard SQL";
        }

        private static string CleanSqlContent(string sqlContent)
        {
            var lines = sqlContent.Split('\n');
            var cleanedLines = new List<string>();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;
                
                if (trimmedLine.StartsWith("--"))
                    continue;
                
                if (trimmedLine.StartsWith("/*!") || trimmedLine.Contains("/*!"))
                    continue;
                
                if (trimmedLine.StartsWith("SET ", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("START TRANSACTION", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("COMMIT", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("DROP ", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("CREATE ", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                cleanedLines.Add(line);
            }

            var cleaned = string.Join("\n", cleanedLines);
            cleaned = Regex.Replace(cleaned, @"b'([01])'", m => m.Groups[1].Value);
            
            return cleaned;
        }

        private static Dictionary<string, List<Dictionary<string, string>>> ParseSqlContent(
            string sqlContent,
            SqlImportResult result)
        {
            var tables = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            var currentStatement = new StringBuilder();
            bool inInsertStatement = false;

            var lines = sqlContent.Split('\n');

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Check if this is an INSERT statement
                if (trimmedLine.StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentStatement.Length > 0)
                    {
                        ParseInsertStatement(currentStatement.ToString(), tables, result);
                        currentStatement.Clear();
                    }
                    inInsertStatement = true;
                }

                if (inInsertStatement)
                {
                    currentStatement.AppendLine(line);

                    // Statement is complete when we hit a semicolon
                    if (trimmedLine.EndsWith(";"))
                    {
                        ParseInsertStatement(currentStatement.ToString(), tables, result);
                        currentStatement.Clear();
                        inInsertStatement = false;
                    }
                }
            }

            // Parse any remaining statement
            if (currentStatement.Length > 0)
            {
                ParseInsertStatement(currentStatement.ToString(), tables, result);
            }

            return tables;
        }

        private static void ParseInsertStatement(
            string statement,
            Dictionary<string, List<Dictionary<string, string>>> tables,
            SqlImportResult result)
        {
            try
            {
                // Extract table name
                var tableMatch = Regex.Match(statement, @"INSERT\s+INTO\s+[`]?(\w+)[`]?\s*\(", RegexOptions.IgnoreCase);
                if (!tableMatch.Success)
                {
                    result.Warnings.Add($"Could not parse table name from INSERT statement");
                    return;
                }

                var tableName = tableMatch.Groups[1].Value;

                // Extract column names
                var columnsMatch = Regex.Match(statement, @"\(([^)]+)\)\s+VALUES", RegexOptions.IgnoreCase);
                if (!columnsMatch.Success)
                {
                    result.Warnings.Add($"Could not parse columns for table {tableName}");
                    return;
                }

                var columns = columnsMatch.Groups[1].Value
                    .Split(',')
                    .Select(c => c.Trim().Trim('`', '\'', '"'))
                    .ToList();

                // Extract VALUES
                var valuesPattern = @"VALUES\s*(\(.+\))";
                var valuesMatch = Regex.Match(statement, valuesPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!valuesMatch.Success)
                {
                    result.Warnings.Add($"Could not parse VALUES for table {tableName}");
                    return;
                }

                var valuesSection = valuesMatch.Groups[1].Value;
                
                // Parse each row
                var rowPattern = @"\(([^)]+)\)";
                var rowMatches = Regex.Matches(valuesSection, rowPattern);

                if (!tables.ContainsKey(tableName))
                {
                    tables[tableName] = new List<Dictionary<string, string>>();
                }

                foreach (Match rowMatch in rowMatches)
                {
                    var values = SplitValues(rowMatch.Groups[1].Value);
                    
                    if (values.Count != columns.Count)
                    {
                        result.Warnings.Add($"Column/value count mismatch in table {tableName}: {columns.Count} columns vs {values.Count} values");
                        continue;
                    }

                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < columns.Count; i++)
                    {
                        row[columns[i]] = CleanValue(values[i]);
                    }

                    tables[tableName].Add(row);
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Error parsing INSERT statement: {ex.Message}");
            }
        }

        private static List<string> SplitValues(string valuesString)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            char quoteChar = '\0';

            for (int i = 0; i < valuesString.Length; i++)
            {
                char c = valuesString[i];

                if ((c == '\'' || c == '"') && (i == 0 || valuesString[i - 1] != '\\'))
                {
                    if (!inQuotes)
                    {
                        inQuotes = true;
                        quoteChar = c;
                    }
                    else if (c == quoteChar)
                    {
                        inQuotes = false;
                        quoteChar = '\0';
                    }
                    current.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                values.Add(current.ToString().Trim());
            }

            return values;
        }

        private static string CleanValue(string value)
        {
            value = value.Trim();
            
            // Handle NULL
            if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                return "";
            
            // Remove quotes
            if ((value.StartsWith("'") && value.EndsWith("'")) ||
                (value.StartsWith("\"") && value.EndsWith("\"")))
            {
                value = value.Substring(1, value.Length - 2);
            }
            
            // Unescape characters
            value = value.Replace("\\'", "'");
            value = value.Replace("\\\"", "\"");
            value = value.Replace("\\n", "\n");
            value = value.Replace("\\r", "\r");
            value = value.Replace("\\t", "\t");
            value = value.Replace("\\\\", "\\");
            
            return value;
        }

        /// <summary>
        /// Build ID-to-Name mappings from tblplayers and other lookup tables
        /// This allows us to use actual names instead of just IDs
        /// </summary>
        private static void BuildIdToNameMappings(
            Dictionary<string, List<Dictionary<string, string>>> tableData,
            SqlImportResult result)
        {
            // Build PlayerID -> PlayerName mapping from tblplayers
            if (tableData.ContainsKey("tblplayers"))
            {
                foreach (var playerRow in tableData["tblplayers"])
                {
                    var playerId = GetIntValue(playerRow, "PlayerID", -1);
                    var playerName = GetStringValue(playerRow, "PlayerName", "");
                    
                    if (playerId >= 0 && !string.IsNullOrWhiteSpace(playerName))
                    {
                        result.VbaPlayerIdToName[playerId] = playerName;
                    }
                }
                
                if (result.VbaPlayerIdToName.Count > 0)
                {
                    result.Warnings.Add($"Loaded {result.VbaPlayerIdToName.Count} player names from tblplayers");
                }
            }

            // Build TeamID -> TeamName mapping from tblplayers Team field
            // (VBA system doesn't have a separate teams table, team info comes from players)
            if (tableData.ContainsKey("tblplayers"))
            {
                var teamNames = new Dictionary<int, HashSet<string>>();
                
                foreach (var playerRow in tableData["tblplayers"])
                {
                    var teamId = GetIntValue(playerRow, "Team", -1);
                    if (teamId > 0)
                    {
                        if (!teamNames.ContainsKey(teamId))
                            teamNames[teamId] = new HashSet<string>();
                    }
                }

                // For now, just note which teams exist - we'll use "Team X" as names
                foreach (var teamId in teamNames.Keys)
                {
                    result.VbaTeamIdToName[teamId] = $"Team {teamId}";
                }
            }

            // Try to get division names from tbldivisions
            if (tableData.ContainsKey("tbldivisions"))
            {
                result.Warnings.Add($"Found {tableData["tbldivisions"].Count} divisions in tbldivisions table");
            }
        }

        private static Task ImportSeasonData(
            Dictionary<string, List<Dictionary<string, string>>> tableData,
            LeagueData importedData,
            LeagueData existingData,
            bool replaceExisting,
            SqlImportResult result)
        {
            if (!tableData.ContainsKey("tblleague") || !tableData["tblleague"].Any())
            {
                result.Warnings.Add("No tblleague data found - season information not imported");
                return Task.CompletedTask;
            }

            var leagueRow = tableData["tblleague"].First();
            
            var seasonName = GetStringValue(leagueRow, "SeasonName", "Unknown Season");
            var seasonYear = GetIntValue(leagueRow, "SeasonYear", DateTime.Now.Year);
            var fullSeasonName = $"{seasonName} {seasonYear}";
            
            // Check if season already exists
            var existingSeason = existingData.Seasons.FirstOrDefault(s => 
                s.Name.Equals(fullSeasonName, StringComparison.OrdinalIgnoreCase));

            if (existingSeason != null && !replaceExisting)
            {
                result.Warnings.Add($"Season '{fullSeasonName}' already exists - skipping import");
                result.DetectedSeason = existingSeason;
                return Task.CompletedTask;
            }

            var season = new Season
            {
                Id = Guid.NewGuid(),
                Name = fullSeasonName,
                StartDate = GetDateTimeValue(leagueRow, "FirstMatchDate", DateTime.Now),
                EndDate = GetDateTimeValue(leagueRow, "FirstMatchDate", DateTime.Now).AddMonths(6),
                IsActive = false
            };

            importedData.Seasons.Add(season);
            existingData.Seasons.Add(season);
            result.DetectedSeason = season;
            result.ImportedSeasonIds.Add(season.Id);

            return Task.CompletedTask;
        }

        private static Task ImportDivisions(
            Dictionary<string, List<Dictionary<string, string>>> tableData,
            LeagueData importedData,
            SqlImportResult result)
        {
            if (!tableData.ContainsKey("tbldivisions"))
            {
                result.Warnings.Add("No tbldivisions data found");
                return Task.CompletedTask;
            }

            if (result.DetectedSeason == null)
            {
                result.Errors.Add("Cannot import divisions without a season");
                return Task.CompletedTask;
            }

            foreach (var divRow in tableData["tbldivisions"])
            {
                var divisionName = GetStringValue(divRow, "DivisionName", "Unknown Division");
                
                var division = new Division
                {
                    Id = Guid.NewGuid(),
                    Name = divisionName,
                    SeasonId = result.DetectedSeason.Id
                };

                importedData.Divisions.Add(division);
                result.ImportedDivisionIds.Add(division.Id);
            }

            return Task.CompletedTask;
        }

        private static Task ImportTeams(
            Dictionary<string, List<Dictionary<string, string>>> tableData,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            // Teams need to be extracted from fixture data (HomeTeam/AwayTeam IDs)
            if (!tableData.ContainsKey("tblfixtures"))
            {
                result.Warnings.Add("No tblfixtures data found - cannot determine teams");
                return Task.CompletedTask;
            }

            var teamIds = new HashSet<int>();
            foreach (var fixtureRow in tableData["tblfixtures"])
            {
                var homeTeamId = GetIntValue(fixtureRow, "HomeTeam", 0);
                var awayTeamId = GetIntValue(fixtureRow, "AwayTeam", 0);
                
                if (homeTeamId > 0) teamIds.Add(homeTeamId);
                if (awayTeamId > 0) teamIds.Add(awayTeamId);
            }

            // Use first division if available
            var divisionId = importedData.Divisions.FirstOrDefault()?.Id;
            if (divisionId == null)
            {
                result.Errors.Add("No division available for teams");
                return Task.CompletedTask;
            }

            foreach (var vbaTeamId in teamIds.OrderBy(id => id))
            {
                // Get team name from mapping or use default
                var teamName = result.VbaTeamIdToName.ContainsKey(vbaTeamId) 
                    ? result.VbaTeamIdToName[vbaTeamId] 
                    : $"Team {vbaTeamId}";

                // Check for duplicate team in existing data
                var existingTeam = existingData.Teams.FirstOrDefault(t => 
                    t.SeasonId == result.DetectedSeason?.Id &&
                    t.Name.Equals(teamName, StringComparison.OrdinalIgnoreCase));

                if (existingTeam != null)
                {
                    // Team already exists - skip and use existing GUID
                    result.VbaTeamIdToGuid[vbaTeamId] = existingTeam.Id;
                    result.TeamsSkipped++;
                    continue;
                }

                // Create new team
                var team = new Team
                {
                    Id = Guid.NewGuid(),
                    Name = teamName,
                    DivisionId = divisionId,
                    SeasonId = result.DetectedSeason?.Id
                };

                importedData.Teams.Add(team);
                result.ImportedTeamIds.Add(team.Id);
                result.VbaTeamIdToGuid[vbaTeamId] = team.Id;
                result.TeamsImported++;
            }

            if (result.TeamsImported > 0 || result.TeamsSkipped > 0)
            {
                result.Warnings.Add($"Teams: {result.TeamsImported} created, {result.TeamsSkipped} already exist");
            }

            return Task.CompletedTask;
        }

        private static Task ImportPlayers(
            Dictionary<string, List<Dictionary<string, string>>> tableData,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            // First, try to get players from tblplayers table if it exists
            var playerIds = new HashSet<int>();
            var playerTeamMapping = new Dictionary<int, int>(); // PlayerID -> TeamID

            if (tableData.ContainsKey("tblplayers"))
            {
                // Use tblplayers as the primary source
                foreach (var playerRow in tableData["tblplayers"])
                {
                    var playerId = GetIntValue(playerRow, "PlayerID", 0);
                    var teamId = GetIntValue(playerRow, "Team", 0);
                    
                    if (playerId > 0)
                    {
                        playerIds.Add(playerId);
                        if (teamId > 0)
                        {
                            playerTeamMapping[playerId] = teamId;
                        }
                    }
                }
            }
            else if (tableData.ContainsKey("tblmatchdetail") || tableData.ContainsKey("tblplayerresult"))
            {
                // Fallback: Extract player IDs from match detail data
                var tableName = tableData.ContainsKey("tblmatchdetail") ? "tblmatchdetail" : "tblplayerresult";

                foreach (var row in tableData[tableName])
                {
                    var player1Id = GetIntValue(row, "Player1", 0);
                    var player2Id = GetIntValue(row, "Player2", 0);
                    var playerId = GetIntValue(row, "PlayerID", 0);
                    var played = GetIntValue(row, "Played", 0);
                    
                    if (player1Id > 0) playerIds.Add(player1Id);
                    if (player2Id > 0) playerIds.Add(player2Id);
                    if (playerId > 0) playerIds.Add(playerId);
                    if (played > 0) playerIds.Add(played);
                }
            }
            else
            {
                result.Warnings.Add("No player data found in tblplayers or match details");
                return Task.CompletedTask;
            }

            foreach (var vbaPlayerId in playerIds.OrderBy(id => id))
            {
                // Get player name from mapping or use default
                var playerName = result.VbaPlayerIdToName.ContainsKey(vbaPlayerId)
                    ? result.VbaPlayerIdToName[vbaPlayerId]
                    : $"Player {vbaPlayerId}";

                // Check if player already exists by name or ID
                var existingPlayer = existingData.Players.FirstOrDefault(p => 
                    (p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase) ||
                     p.Name.EndsWith($"({vbaPlayerId})")) &&
                    p.SeasonId == result.DetectedSeason?.Id);

                if (existingPlayer != null)
                {
                    // Player already exists - skip and use existing GUID
                    result.VbaPlayerIdToGuid[vbaPlayerId] = existingPlayer.Id;
                    result.PlayersSkipped++;
                    continue;
                }

                // Get the team for this player
                Guid? teamId = null;
                if (playerTeamMapping.ContainsKey(vbaPlayerId))
                {
                    var vbaTeamId = playerTeamMapping[vbaPlayerId];
                    if (result.VbaTeamIdToGuid.ContainsKey(vbaTeamId))
                    {
                        teamId = result.VbaTeamIdToGuid[vbaTeamId];
                    }
                }

                // If no team found, use first available team (or null if no teams)
                if (teamId == null)
                {
                    teamId = importedData.Teams.FirstOrDefault()?.Id;
                }

                var player = new Player
                {
                    Id = Guid.NewGuid(),
                    Name = playerName,
                    TeamId = teamId,
                    SeasonId = result.DetectedSeason?.Id
                };

                importedData.Players.Add(player);
                existingData.Players.Add(player);
                result.ImportedPlayerIds.Add(player.Id);
                result.VbaPlayerIdToGuid[vbaPlayerId] = player.Id;
                result.PlayersImported++;
            }

            if (result.PlayersImported > 0 || result.PlayersSkipped > 0)
            {
                var hasNames = result.VbaPlayerIdToName.Count > 0;
                var message = hasNames 
                    ? $"Players: {result.PlayersImported} imported with names, {result.PlayersSkipped} already exist"
                    : $"Players: {result.PlayersImported} created with placeholder names, {result.PlayersSkipped} already exist";
                result.Warnings.Add(message);
            }

            return Task.CompletedTask;
        }

        private static Task ImportFixtures(
            Dictionary<string, List<Dictionary<string, string>>> tableData,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            if (!tableData.ContainsKey("tblfixtures"))
            {
                result.Warnings.Add("No tblfixtures data found");
                return Task.CompletedTask;
            }

            if (result.DetectedSeason == null)
            {
                result.Errors.Add("Cannot import fixtures without a season");
                return Task.CompletedTask;
            }

            foreach (var fixtureRow in tableData["tblfixtures"])
            {
                var vbaHomeTeamId = GetIntValue(fixtureRow, "HomeTeam", 0);
                var vbaAwayTeamId = GetIntValue(fixtureRow, "AwayTeam", 0);
                var weekNo = GetIntValue(fixtureRow, "WeekNo", 0);
                var matchDate = GetDateTimeValue(fixtureRow, "MatchDate", result.DetectedSeason.StartDate);

                if (!result.VbaTeamIdToGuid.ContainsKey(vbaHomeTeamId) || 
                    !result.VbaTeamIdToGuid.ContainsKey(vbaAwayTeamId))
                {
                    result.Warnings.Add($"Skipping fixture with unknown team IDs: {vbaHomeTeamId} vs {vbaAwayTeamId}");
                    result.FixturesSkipped++;
                    continue;
                }

                var homeTeamId = result.VbaTeamIdToGuid[vbaHomeTeamId];
                var awayTeamId = result.VbaTeamIdToGuid[vbaAwayTeamId];

                // Check for duplicate fixture (same date + teams)
                var existingFixture = existingData.Fixtures.FirstOrDefault(f =>
                    f.SeasonId == result.DetectedSeason?.Id &&
                    f.Date.Date == matchDate.Date &&
                    f.HomeTeamId == homeTeamId &&
                    f.AwayTeamId == awayTeamId);

                if (existingFixture != null)
                {
                    // Fixture already exists - skip
                    result.FixturesSkipped++;
                    continue;
                }

                var fixture = new Fixture
                {
                    Id = Guid.NewGuid(),
                    SeasonId = result.DetectedSeason.Id,
                    HomeTeamId = homeTeamId,
                    AwayTeamId = awayTeamId,
                    Date = matchDate
                };

                importedData.Fixtures.Add(fixture);
                result.ImportedFixtureIds.Add(fixture.Id);
                result.FixturesImported++;
            }

            if (result.FixturesSkipped > 0)
            {
                result.Warnings.Add($"Fixtures: {result.FixturesImported} imported, {result.FixturesSkipped} already exist");
            }

            return Task.CompletedTask;
        }

        private static Task ImportResults(
            Dictionary<string, List<Dictionary<string, string>>> tableData,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            // Import match results from tblmatchdetail or tblplayerresult
            var tableName = tableData.ContainsKey("tblmatchdetail") ? "tblmatchdetail" : 
                           tableData.ContainsKey("tblplayerresult") ? "tblplayerresult" : null;

            if (tableName == null)
            {
                result.Warnings.Add("No match detail data found");
                return Task.CompletedTask;
            }

            // Group by match number
            var matchGroups = tableData[tableName]
                .GroupBy(row => GetIntValue(row, "MatchNo", 0))
                .Where(g => g.Key > 0)
                .ToList();

            foreach (var matchGroup in matchGroups)
            {
                var matchNo = matchGroup.Key;
                
                // Find corresponding fixture (by match number or sequence)
                // This is approximate - in real VBA system, we'd need match header data
                Fixture? fixture = null;
                if (matchNo <= importedData.Fixtures.Count)
                {
                    fixture = importedData.Fixtures[matchNo - 1];
                }

                if (fixture == null)
                {
                    result.Warnings.Add($"Could not find fixture for match {matchNo}");
                    result.ResultsSkipped++;
                    continue;
                }

                // Check if this fixture already has results
                if (fixture.Frames.Count > 0)
                {
                    result.ResultsSkipped++;
                    result.FramesSkipped += matchGroup.Count();
                    continue;
                }

                // Import frames for this match
                foreach (var frameRow in matchGroup)
                {
                    var vbaPlayer1Id = GetIntValue(frameRow, "Player1", GetIntValue(frameRow, "PlayerID", 0));
                    var vbaPlayer2Id = GetIntValue(frameRow, "Player2", GetIntValue(frameRow, "Played", 0));
                    var homeScore = GetIntValue(frameRow, "HomeScore", 0);
                    var awayScore = GetIntValue(frameRow, "AwayScore", 0);
                    var frameNo = GetIntValue(frameRow, "FrameNo", 0);

                    if (!result.VbaPlayerIdToGuid.ContainsKey(vbaPlayer1Id) || 
                        !result.VbaPlayerIdToGuid.ContainsKey(vbaPlayer2Id))
                    {
                        result.FramesSkipped++;
                        continue;
                    }

                    var player1Id = result.VbaPlayerIdToGuid[vbaPlayer1Id];
                    var player2Id = result.VbaPlayerIdToGuid[vbaPlayer2Id];

                    // Check for duplicate frame (same players + frame number in this fixture)
                    var existingFrame = fixture.Frames.FirstOrDefault(f =>
                        f.Number == frameNo &&
                        f.HomePlayerId == player1Id &&
                        f.AwayPlayerId == player2Id);

                    if (existingFrame != null)
                    {
                        result.FramesSkipped++;
                        continue;
                    }

                    // Determine winner
                    var winner = homeScore > awayScore ? FrameWinner.Home : 
                                 awayScore > homeScore ? FrameWinner.Away : FrameWinner.None;

                    var frame = new FrameResult
                    {
                        Number = frameNo,
                        HomePlayerId = player1Id,
                        AwayPlayerId = player2Id,
                        Winner = winner,
                        EightBall = false // Will be set from EightBall column if available
                    };

                    // Check for 8-ball
                    if (frameRow.ContainsKey("Achived8Ball") && frameRow["Achived8Ball"] == "1")
                    {
                        frame.EightBall = true;
                    }
                    else if (frameRow.ContainsKey("EightBall") && frameRow["EightBall"] == "1")
                    {
                        frame.EightBall = true;
                    }

                    fixture.Frames.Add(frame);
                    result.FramesImported++;
                }

                result.ResultsImported++;
            }

            if (result.ResultsSkipped > 0 || result.FramesSkipped > 0)
            {
                result.Warnings.Add($"Results: {result.ResultsImported} matches imported, {result.ResultsSkipped} skipped; {result.FramesImported} frames imported, {result.FramesSkipped} skipped");
            }

            return Task.CompletedTask;
        }

        // Helper methods
        private static int GetIntValue(Dictionary<string, string> row, string key, int defaultValue = 0)
        {
            if (row.TryGetValue(key, out var value) && int.TryParse(value, out var result))
                return result;
            return defaultValue;
        }

        private static string GetStringValue(Dictionary<string, string> row, string key, string defaultValue = "")
        {
            if (row.TryGetValue(key, out var value))
                return value;
            return defaultValue;
        }

        private static DateTime GetDateTimeValue(Dictionary<string, string> row, string key, DateTime defaultValue)
        {
            if (row.TryGetValue(key, out var value) && DateTime.TryParse(value, out var result))
                return result;
            return defaultValue;
        }

        public static void RollbackImport(LeagueData data, SqlImportResult result)
        {
            // Remove in reverse order to maintain referential integrity
            // Note: Frames are part of fixtures, so they're removed when fixtures are removed
            data.Fixtures.RemoveAll(f => result.ImportedFixtureIds.Contains(f.Id));
            data.Players.RemoveAll(p => result.ImportedPlayerIds.Contains(p.Id));
            data.Teams.RemoveAll(t => result.ImportedTeamIds.Contains(t.Id));
            data.Divisions.RemoveAll(d => result.ImportedDivisionIds.Contains(d.Id));
            data.Seasons.RemoveAll(s => result.ImportedSeasonIds.Contains(s.Id));
        }
    }
}
