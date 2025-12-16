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
            
            // Imported counts
            public int TeamsImported { get; set; }
            public int PlayersImported { get; set; }
            public int FixturesImported { get; set; }
            public int ResultsImported { get; set; }
            public int FramesImported { get; set; }
            public int VenuesImported { get; set; }
            
            // Skipped counts (duplicates)
            public int TeamsSkipped { get; set; }
            public int PlayersSkipped { get; set; }
            public int FixturesSkipped { get; set; }
            public int ResultsSkipped { get; set; }
            public int VenuesSkipped { get; set; }
            
            // Updated counts (re-imports)
            public int FixturesUpdated { get; set; }
            
            public List<Guid> ImportedSeasonIds { get; set; } = new();
            public List<Guid> ImportedDivisionIds { get; set; } = new();
            public List<Guid> ImportedTeamIds { get; } = new();
            public List<Guid> ImportedPlayerIds { get; } = new();
            public List<Guid> ImportedFixtureIds { get; } = new();
            public List<Guid> ImportedVenueIds { get; } = new();

            // VBA ID mappings for reference
            public Dictionary<int, Guid> VbaTeamIdToGuid { get; set; } = new();
            public Dictionary<int, Guid> VbaPlayerIdToGuid { get; set; } = new();
            public Dictionary<string, Guid> VenueNameToGuid { get; set; } = new();
            
            // Name mappings from SQL data
            public Dictionary<int, string> VbaPlayerIdToName { get; set; } = new();
            public Dictionary<int, string> VbaTeamIdToName { get; set; } = new();

            public string Summary =>
                $"?? Season: {DetectedSeason?.Name ?? "None"}\n" +
                $"?? Teams: {TeamsImported} imported" + (TeamsSkipped > 0 ? $", {TeamsSkipped} skipped" : "") + "\n" +
                $"?? Players: {PlayersImported} imported" + (PlayersSkipped > 0 ? $", {PlayersSkipped} skipped" : "") + "\n" +
                $"?? Fixtures: {FixturesImported} imported" + (FixturesSkipped > 0 ? $", {FixturesSkipped} existing" : "") + "\n" +
                $"?? Venues: {VenuesImported} imported" + (VenuesSkipped > 0 ? $", {VenuesSkipped} skipped" : "") + "\n" +
                $"?? Results: {ResultsImported} matches" + (FixturesUpdated > 0 ? $" ({FixturesUpdated} updated)" : "") + "\n" +
                $"?? Frames: {FramesImported}\n" +
                $"?? Warnings: {Warnings.Count}\n" +
                $"? Errors: {Errors.Count}";
        }

        public class ParsedSqlData
        {
            public Dictionary<string, List<Dictionary<string, string>>> Tables { get; set; } = new();
            public string DetectedDialect { get; set; } = "Unknown";
            
            // Pre-built lookups from tblplayers
            public Dictionary<int, string> PlayerIdToName { get; set; } = new();
            public Dictionary<int, int> PlayerIdToTeamId { get; set; } = new();
            
            // Pre-built lookups from tblteams (if available)
            public Dictionary<int, string> TeamIdToName { get; set; } = new();
            public Dictionary<int, string> TeamIdToVenue { get; set; } = new();
            public Dictionary<int, int> TeamIdToDivision { get; set; } = new();
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
                
                // Build player ID-to-name lookup from tblplayers
                BuildPlayerLookups(result);
                
                // Build team ID-to-name lookup from tblteams (if available in SQL)
                BuildTeamLookups(result);
                
                // If no team names found in SQL, try loading from VBA_Data files
                if (!result.TeamIdToName.Any())
                {
                    LoadVbaTeamData(result, Path.GetDirectoryName(sqlFilePath));
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse SQL file: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// Build player ID-to-name and team lookups from tblplayers data
        /// </summary>
        private static void BuildPlayerLookups(ParsedSqlData data)
        {
            if (!data.Tables.ContainsKey("tblplayers"))
                return;

            foreach (var row in data.Tables["tblplayers"])
            {
                var playerId = GetIntValue(row, "PlayerID", 0);
                if (playerId <= 0) continue;

                // Get player name
                var playerName = GetStringValue(row, "PlayerName", "");
                if (!string.IsNullOrWhiteSpace(playerName))
                {
                    data.PlayerIdToName[playerId] = playerName.Trim();
                }

                // Get team ID
                var teamId = GetIntValue(row, "Team", 0);
                if (teamId > 0)
                {
                    data.PlayerIdToTeamId[playerId] = teamId;
                }
            }
        }

        /// <summary>
        /// Build team ID-to-name and venue lookups from tblteams data (if available in SQL)
        /// </summary>
        private static void BuildTeamLookups(ParsedSqlData data)
        {
            if (!data.Tables.ContainsKey("tblteams"))
                return;

            foreach (var row in data.Tables["tblteams"])
            {
                var teamId = GetIntValue(row, "TeamID", 0);
                if (teamId <= 0) continue;

                // Get team name
                var teamName = GetStringValue(row, "TeamName", "");
                if (!string.IsNullOrWhiteSpace(teamName))
                {
                    data.TeamIdToName[teamId] = teamName.Trim();
                }

                // Get venue name
                var venueName = GetStringValue(row, "VenueName", "");
                if (!string.IsNullOrWhiteSpace(venueName))
                {
                    data.TeamIdToVenue[teamId] = venueName.Trim();
                }

                // Get division
                var divisionId = GetIntValue(row, "Division", 0);
                if (divisionId > 0)
                {
                    data.TeamIdToDivision[teamId] = divisionId;
                }
            }
        }

        /// <summary>
        /// Load supplementary team data from VBA_Data/tblteams.txt file if it exists
        /// </summary>
        public static void LoadVbaTeamData(ParsedSqlData data, string? vbaDataDir = null)
        {
            // Try to find tblteams.txt in common locations
            var possiblePaths = new List<string>();
            
            if (!string.IsNullOrEmpty(vbaDataDir))
            {
                possiblePaths.Add(Path.Combine(vbaDataDir, "tblteams.txt"));
            }
            
            // Try common development paths
            possiblePaths.AddRange(new[]
            {
                @"C:\Users\bobgc\source\repos\gazlappy\Wdplapp\wdpl2\VBA_Data\tblteams.txt",
                Path.Combine(FileSystem.AppDataDirectory, "VBA_Data", "tblteams.txt"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WDPL", "tblteams.txt")
            });

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        ParseVbaTeamsFile(path, data);
                        return;
                    }
                    catch
                    {
                        // Continue to next path
                    }
                }
            }
        }

        /// <summary>
        /// Parse tab-delimited VBA teams file
        /// </summary>
        private static void ParseVbaTeamsFile(string filePath, ParsedSqlData data)
        {
            var lines = File.ReadAllLines(filePath);
            string[]? headers = null;

            foreach (var line in lines)
            {
                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split('\t');

                // First non-comment line is headers
                if (headers == null)
                {
                    headers = parts;
                    continue;
                }

                // Parse data row
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < Math.Min(headers.Length, parts.Length); i++)
                {
                    row[headers[i].Trim()] = parts[i].Trim();
                }

                var teamId = GetIntValue(row, "TeamID", 0);
                if (teamId <= 0) continue;

                // Get team name
                var teamName = GetStringValue(row, "TeamName", "");
                if (!string.IsNullOrWhiteSpace(teamName) && !data.TeamIdToName.ContainsKey(teamId))
                {
                    data.TeamIdToName[teamId] = teamName.Trim();
                }

                // Get venue name
                var venueName = GetStringValue(row, "VenueName", "");
                if (!string.IsNullOrWhiteSpace(venueName) && !data.TeamIdToVenue.ContainsKey(teamId))
                {
                    data.TeamIdToVenue[teamId] = venueName.Trim();
                }

                // Get division
                var divisionId = GetIntValue(row, "Division", 0);
                if (divisionId > 0 && !data.TeamIdToDivision.ContainsKey(teamId))
                {
                    data.TeamIdToDivision[teamId] = divisionId;
                }
            }
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
                
                // Store name lookups in result for reference
                result.VbaPlayerIdToName = new Dictionary<int, string>(parsed.PlayerIdToName);
                result.VbaTeamIdToName = new Dictionary<int, string>(parsed.TeamIdToName);

                // Import in order of dependencies
                await ImportSeasonData(parsed.Tables, importedData, existingData, replaceExisting, result);
                
                if (result.DetectedSeason != null)
                {
                    await ImportDivisions(parsed.Tables, importedData, existingData, result);
                    await ImportVenues(parsed, importedData, existingData, result);
                    await ImportTeams(parsed, importedData, existingData, result);
                    await ImportPlayers(parsed, importedData, existingData, result);
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
                result.Warnings.Add($"Season '{fullSeasonName}' already exists - using existing season");
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
            LeagueData existingData,
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
                
                // Check if division already exists in this season
                var existingDivision = existingData.Divisions.FirstOrDefault(d =>
                    d.SeasonId == result.DetectedSeason.Id &&
                    !string.IsNullOrWhiteSpace(d.Name) &&
                    d.Name.Equals(divisionName, StringComparison.OrdinalIgnoreCase));

                if (existingDivision != null)
                {
                    // Use existing division - don't create duplicate
                    continue;
                }

                var division = new Division
                {
                    Id = Guid.NewGuid(),
                    Name = divisionName,
                    SeasonId = result.DetectedSeason.Id
                };

                importedData.Divisions.Add(division);
                existingData.Divisions.Add(division);
                result.ImportedDivisionIds.Add(division.Id);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Import venues extracted from tblteams data
        /// </summary>
        private static Task ImportVenues(
            ParsedSqlData parsed,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            if (result.DetectedSeason == null)
            {
                result.Errors.Add("Cannot import venues without a season");
                return Task.CompletedTask;
            }

            // Extract unique venue names from tblteams
            var venueNames = parsed.TeamIdToVenue.Values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!venueNames.Any())
            {
                result.Warnings.Add("No venue data found in tblteams");
                return Task.CompletedTask;
            }

            foreach (var venueName in venueNames)
            {
                // Check if venue already exists
                var existingVenue = existingData.Venues.FirstOrDefault(v =>
                    v.SeasonId == result.DetectedSeason.Id &&
                    !string.IsNullOrWhiteSpace(v.Name) &&
                    v.Name.Equals(venueName, StringComparison.OrdinalIgnoreCase));

                if (existingVenue != null)
                {
                    result.VenueNameToGuid[venueName.ToLower()] = existingVenue.Id;
                    result.VenuesSkipped++;
                    continue;
                }

                var venue = new Venue
                {
                    Id = Guid.NewGuid(),
                    Name = venueName,
                    SeasonId = result.DetectedSeason.Id
                };

                importedData.Venues.Add(venue);
                existingData.Venues.Add(venue);
                result.ImportedVenueIds.Add(venue.Id);
                result.VenueNameToGuid[venueName.ToLower()] = venue.Id;
                result.VenuesImported++;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Import teams using actual names from tblteams when available
        /// </summary>
        private static Task ImportTeams(
            ParsedSqlData parsed,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            var tableData = parsed.Tables;
            
            // Get team IDs from fixtures (to know which teams we need)
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

            // Use first division if available - check both importedData and existingData for re-imports
            var divisionId = importedData.Divisions.FirstOrDefault()?.Id 
                          ?? existingData.Divisions.FirstOrDefault(d => d.SeasonId == result.DetectedSeason?.Id)?.Id 
                          ?? Guid.Empty;
            
            if (divisionId == Guid.Empty)
            {
                result.Errors.Add("No division available for teams");
                return Task.CompletedTask;
            }

            foreach (var vbaTeamId in teamIds.OrderBy(id => id))
            {
                // Try to get actual team name from tblteams lookup, fallback to placeholder
                var teamName = parsed.TeamIdToName.TryGetValue(vbaTeamId, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : $"Team {vbaTeamId}";
                
                result.VbaTeamIdToName[vbaTeamId] = teamName;
                
                // Check if team already exists in this season
                var existingTeam = existingData.Teams.FirstOrDefault(t =>
                    t.SeasonId == result.DetectedSeason?.Id &&
                    t.Name != null &&
                    t.Name.Equals(teamName, StringComparison.OrdinalIgnoreCase));

                if (existingTeam != null)
                {
                    result.VbaTeamIdToGuid[vbaTeamId] = existingTeam.Id;
                    result.TeamsSkipped++;
                    continue;
                }

                // Get venue if available
                Guid? venueId = null;
                if (parsed.TeamIdToVenue.TryGetValue(vbaTeamId, out var venueName) && 
                    !string.IsNullOrWhiteSpace(venueName) &&
                    result.VenueNameToGuid.TryGetValue(venueName.ToLower(), out var vGuid))
                {
                    venueId = vGuid;
                }

                var team = new Team
                {
                    Id = Guid.NewGuid(),
                    Name = teamName,
                    DivisionId = divisionId,
                    SeasonId = result.DetectedSeason?.Id,
                    VenueId = venueId
                };

                importedData.Teams.Add(team);
                existingData.Teams.Add(team);
                result.ImportedTeamIds.Add(team.Id);
                result.VbaTeamIdToGuid[vbaTeamId] = team.Id;
                result.TeamsImported++;
            }

            // Only show warning if we had to use placeholder names
            var placeholderCount = teamIds.Count(id => !parsed.TeamIdToName.ContainsKey(id));
            if (placeholderCount > 0)
                result.Warnings.Add($"{placeholderCount} teams imported with placeholder names (tblteams not found) - update names manually");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Import players using actual names from tblplayers when available
        /// </summary>
        private static Task ImportPlayers(
            ParsedSqlData parsed,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            var tableData = parsed.Tables;
            
            // First, try to get players from tblplayers (has actual names)
            if (tableData.ContainsKey("tblplayers") && tableData["tblplayers"].Any())
            {
                foreach (var row in tableData["tblplayers"])
                {
                    var vbaPlayerId = GetIntValue(row, "PlayerID", 0);
                    if (vbaPlayerId <= 0) continue;
                    
                    // Skip VOID player (ID 0)
                    var playerName = GetStringValue(row, "PlayerName", "").Trim();
                    if (string.IsNullOrWhiteSpace(playerName) || playerName.Equals("VOID", StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    var vbaTeamId = GetIntValue(row, "Team", 0);
                    
                    // Check if player already exists by name (case-insensitive)
                    var existingPlayer = existingData.Players.FirstOrDefault(p =>
                        p.SeasonId == result.DetectedSeason?.Id &&
                        !string.IsNullOrWhiteSpace(p.Name) &&
                        p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));

                    if (existingPlayer != null)
                    {
                        result.VbaPlayerIdToGuid[vbaPlayerId] = existingPlayer.Id;
                        result.VbaPlayerIdToName[vbaPlayerId] = playerName;
                        result.PlayersSkipped++;
                        continue;
                    }

                    // Get team GUID - check VbaTeamIdToGuid mapping first (works for both new and existing teams)
                    Guid? teamId = null;
                    if (vbaTeamId > 0 && result.VbaTeamIdToGuid.TryGetValue(vbaTeamId, out var tGuid))
                    {
                        teamId = tGuid;
                    }
                    else
                    {
                        // Fallback to first team in season
                        teamId = importedData.Teams.FirstOrDefault()?.Id 
                              ?? existingData.Teams.FirstOrDefault(t => t.SeasonId == result.DetectedSeason?.Id)?.Id;
                    }

                    // Parse name into first/last
                    var nameParts = playerName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    var firstName = nameParts.Length > 0 ? nameParts[0] : playerName;
                    var lastName = nameParts.Length > 1 ? nameParts[1] : "";

                    var player = new Player
                    {
                        Id = Guid.NewGuid(),
                        Name = playerName,
                        FirstName = firstName,
                        LastName = lastName,
                        TeamId = teamId,
                        SeasonId = result.DetectedSeason?.Id
                    };

                    importedData.Players.Add(player);
                    existingData.Players.Add(player);
                    result.ImportedPlayerIds.Add(player.Id);
                    result.VbaPlayerIdToGuid[vbaPlayerId] = player.Id;
                    result.VbaPlayerIdToName[vbaPlayerId] = playerName;
                    result.PlayersImported++;
                }
                
                return Task.CompletedTask;
            }

            // Fallback: Extract player IDs from match detail data (creates placeholder names)
            if (!tableData.ContainsKey("tblmatchdetail") && !tableData.ContainsKey("tblplayerresult"))
            {
                result.Warnings.Add("No player data found in match details or tblplayers");
                return Task.CompletedTask;
            }

            var tableName = tableData.ContainsKey("tblmatchdetail") ? "tblmatchdetail" : "tblplayerresult";
            var playerIds = new HashSet<int>();

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

            // Get default team for fallback
            var defaultTeamId = importedData.Teams.FirstOrDefault()?.Id 
                             ?? existingData.Teams.FirstOrDefault(t => t.SeasonId == result.DetectedSeason?.Id)?.Id;

            foreach (var vbaPlayerId in playerIds.OrderBy(id => id))
            {
                // Try to get name from lookup, fallback to placeholder
                var playerName = parsed.PlayerIdToName.TryGetValue(vbaPlayerId, out var name) 
                    ? name 
                    : $"Player {vbaPlayerId}";

                // Check if player already exists
                var existingPlayer = existingData.Players.FirstOrDefault(p =>
                    p.SeasonId == result.DetectedSeason?.Id &&
                    !string.IsNullOrWhiteSpace(p.Name) &&
                    p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));

                if (existingPlayer != null)
                {
                    result.VbaPlayerIdToGuid[vbaPlayerId] = existingPlayer.Id;
                    result.PlayersSkipped++;
                    continue;
                }

                var nameParts = playerName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var firstName = nameParts.Length > 0 ? nameParts[0] : playerName;
                var lastName = nameParts.Length > 1 ? nameParts[1] : "";

                var player = new Player
                {
                    Id = Guid.NewGuid(),
                    Name = playerName,
                    FirstName = firstName,
                    LastName = lastName,
                    TeamId = defaultTeamId,
                    SeasonId = result.DetectedSeason?.Id
                };

                importedData.Players.Add(player);
                existingData.Players.Add(player);
                result.ImportedPlayerIds.Add(player.Id);
                result.VbaPlayerIdToGuid[vbaPlayerId] = player.Id;
                result.PlayersImported++;
            }

            if (result.PlayersImported > 0 && !tableData.ContainsKey("tblplayers"))
                result.Warnings.Add($"Created {result.PlayersImported} placeholder players - names should be updated manually");

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

            // Get first division for fixtures - check existingData too for re-imports
            var divisionId = importedData.Divisions.FirstOrDefault()?.Id 
                          ?? existingData.Divisions.FirstOrDefault(d => d.SeasonId == result.DetectedSeason.Id)?.Id;

            foreach (var fixtureRow in tableData["tblfixtures"])
            {
                var vbaHomeTeamId = GetIntValue(fixtureRow, "HomeTeam", 0);
                var vbaAwayTeamId = GetIntValue(fixtureRow, "AwayTeam", 0);
                var weekNo = GetIntValue(fixtureRow, "WeekNo", 0);
                var matchDate = GetDateTimeValue(fixtureRow, "MatchDate", result.DetectedSeason.StartDate);

                if (!result.VbaTeamIdToGuid.ContainsKey(vbaHomeTeamId) || 
                    !result.VbaTeamIdToGuid.ContainsKey(vbaAwayTeamId))
                {
                    var homeTeamName = result.VbaTeamIdToName.TryGetValue(vbaHomeTeamId, out var h) ? h : $"Team {vbaHomeTeamId}";
                    var awayTeamName = result.VbaTeamIdToName.TryGetValue(vbaAwayTeamId, out var a) ? a : $"Team {vbaAwayTeamId}";
                    result.Warnings.Add($"Skipping fixture with unknown teams: {homeTeamName} vs {awayTeamName}");
                    continue;
                }

                var homeTeamId = result.VbaTeamIdToGuid[vbaHomeTeamId];
                var awayTeamId = result.VbaTeamIdToGuid[vbaAwayTeamId];

                // Check for existing fixture (same date + teams)
                var existingFixture = existingData.Fixtures.FirstOrDefault(f =>
                    f.SeasonId == result.DetectedSeason.Id &&
                    f.Date.Date == matchDate.Date &&
                    f.HomeTeamId == homeTeamId &&
                    f.AwayTeamId == awayTeamId);

                if (existingFixture != null)
                {
                    // Fixture exists - add to importedData for result processing
                    // This allows results to be updated on existing fixtures
                    if (!importedData.Fixtures.Contains(existingFixture))
                    {
                        importedData.Fixtures.Add(existingFixture);
                    }
                    result.FixturesSkipped++;
                    continue;
                }

                // Get venue from home team
                var homeTeam = importedData.Teams.FirstOrDefault(t => t.Id == homeTeamId) ??
                               existingData.Teams.FirstOrDefault(t => t.Id == homeTeamId);

                var fixture = new Fixture
                {
                    Id = Guid.NewGuid(),
                    SeasonId = result.DetectedSeason.Id,
                    DivisionId = divisionId,
                    HomeTeamId = homeTeamId,
                    AwayTeamId = awayTeamId,
                    VenueId = homeTeam?.VenueId,
                    Date = matchDate
                };

                importedData.Fixtures.Add(fixture);
                existingData.Fixtures.Add(fixture);
                result.ImportedFixtureIds.Add(fixture.Id);
                result.FixturesImported++;
            }

            return Task.CompletedTask;
        }

        private static Task ImportResults(
            Dictionary<string, List<Dictionary<string, string>>> tableData,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            // First, build a lookup from MatchNo -> Fixture using tblmatchheader
            var matchNoToFixture = new Dictionary<int, Fixture>();
            var matchNoToWeekNo = new Dictionary<int, int>();
            
            if (tableData.ContainsKey("tblmatchheader"))
            {
                foreach (var header in tableData["tblmatchheader"])
                {
                    var matchNo = GetIntValue(header, "MatchNo", 0);
                    if (matchNo <= 0) continue;
                    
                    var weekNo = GetIntValue(header, "WeekNo", 0);
                    var homeTeamVbaId = GetIntValue(header, "TeamHome", 0);
                    var awayTeamVbaId = GetIntValue(header, "TeamAway", 0);
                    
                    matchNoToWeekNo[matchNo] = weekNo;
                    
                    // Find matching fixture by home/away team GUIDs
                    if (result.VbaTeamIdToGuid.TryGetValue(homeTeamVbaId, out var homeGuid) &&
                        result.VbaTeamIdToGuid.TryGetValue(awayTeamVbaId, out var awayGuid))
                    {
                        var fixture = importedData.Fixtures.FirstOrDefault(f =>
                            f.HomeTeamId == homeGuid && f.AwayTeamId == awayGuid);
                        
                        if (fixture == null)
                        {
                            fixture = existingData.Fixtures.FirstOrDefault(f =>
                                f.SeasonId == result.DetectedSeason?.Id &&
                                f.HomeTeamId == homeGuid && f.AwayTeamId == awayGuid);
                        }
                        
                        if (fixture != null)
                        {
                            matchNoToFixture[matchNo] = fixture;
                        }
                    }
                }
            }
            
            // Build lookup for VBA pre-calculated ratings from tblplayerresult
            // Key: (MatchNo, FrameNo, VbaPlayerId) -> (OppRating, PlayerRating, WeekNo)
            var playerResultLookup = new Dictionary<(int matchNo, int frameNo, int playerId), (int oppRating, int playerRating, int weekNo)>();
            
            if (tableData.ContainsKey("tblplayerresult"))
            {
                foreach (var row in tableData["tblplayerresult"])
                {
                    var matchNo = GetIntValue(row, "MatchNo", 0);
                    var frameNo = GetIntValue(row, "FrameNo", 0);
                    var playerId = GetIntValue(row, "PlayerID", 0);
                    var oppRating = GetIntValue(row, "OppRating", 0);
                    var playerRating = GetIntValue(row, "PlayerRating", 0);
                    var weekNo = GetIntValue(row, "WeekNo", 0);
                    
                    if (matchNo > 0 && frameNo > 0 && playerId > 0)
                    {
                        playerResultLookup[(matchNo, frameNo, playerId)] = (oppRating, playerRating, weekNo);
                    }
                }
                
                if (playerResultLookup.Any())
                {
                    result.Warnings.Add($"Imported {playerResultLookup.Count} pre-calculated VBA ratings from tblplayerresult");
                }
            }
            
            // If no match headers, try to map by fixture order (fallback)
            if (!matchNoToFixture.Any() && importedData.Fixtures.Any())
            {
                result.Warnings.Add("No tblmatchheader found - mapping results by fixture order (may be inaccurate)");
                for (int i = 0; i < importedData.Fixtures.Count; i++)
                {
                    matchNoToFixture[i + 1] = importedData.Fixtures[i];
                }
            }

            // Now parse the match details
            var tableName = tableData.ContainsKey("tblmatchdetail") ? "tblmatchdetail" : 
                           tableData.ContainsKey("tblplayerresult") ? "tblplayerresult" : null;

            if (tableName == null)
            {
                result.Warnings.Add("No match detail data found (tblmatchdetail or tblplayerresult)");
                return Task.CompletedTask;
            }

            // Group by MatchNo
            var matchGroups = tableData[tableName]
                .GroupBy(row => GetIntValue(row, "MatchNo", 0))
                .Where(g => g.Key > 0)
                .ToList();

            foreach (var matchGroup in matchGroups)
            {
                var matchNo = matchGroup.Key;
                
                if (!matchNoToFixture.TryGetValue(matchNo, out var fixture))
                {
                    if (matchNo <= 10 || matchNo % 10 == 0)
                        result.Warnings.Add($"Could not find fixture for match {matchNo}");
                    continue;
                }

                var hadExistingFrames = fixture.Frames.Any();
                fixture.Frames.Clear();
                
                // Get week number for this match
                var matchWeekNo = matchNoToWeekNo.TryGetValue(matchNo, out var wk) ? wk : 0;

                foreach (var frameRow in matchGroup.OrderBy(r => GetIntValue(r, "FrameNo", 0)))
                {
                    var frameNo = GetIntValue(frameRow, "FrameNo", 0);
                    if (frameNo <= 0) continue;
                    
                    // Get player IDs
                    var vbaPlayer1Id = GetIntValue(frameRow, "Player1", GetIntValue(frameRow, "PlayerID", 0));
                    var vbaPlayer2Id = GetIntValue(frameRow, "Player2", GetIntValue(frameRow, "Played", 0));
                    
                    // Skip void frames (player ID 0)
                    if (vbaPlayer1Id <= 0 || vbaPlayer2Id <= 0)
                    {
                        var homeScore = GetIntValue(frameRow, "HomeScore", 0);
                        var awayScore = GetIntValue(frameRow, "AwayScore", 0);
                        
                        var voidFrame = new FrameResult
                        {
                            Number = frameNo,
                            HomePlayerId = null,
                            AwayPlayerId = null,
                            Winner = homeScore > awayScore ? FrameWinner.Home : 
                                     awayScore > homeScore ? FrameWinner.Away : FrameWinner.None,
                            EightBall = false,
                            WeekNo = matchWeekNo > 0 ? matchWeekNo : null
                        };
                        fixture.Frames.Add(voidFrame);
                        result.FramesImported++;
                        continue;
                    }

                    // Get player GUIDs
                    Guid? player1Guid = result.VbaPlayerIdToGuid.TryGetValue(vbaPlayer1Id, out var p1) ? p1 : null;
                    Guid? player2Guid = result.VbaPlayerIdToGuid.TryGetValue(vbaPlayer2Id, out var p2) ? p2 : null;

                    // Get scores
                    var homeScore2 = GetIntValue(frameRow, "HomeScore", 0);
                    var awayScore2 = GetIntValue(frameRow, "AwayScore", 0);

                    // Determine winner
                    var winner = homeScore2 > awayScore2 ? FrameWinner.Home : 
                                 awayScore2 > homeScore2 ? FrameWinner.Away : FrameWinner.None;

                    // Check for 8-ball
                    var eightBall = false;
                    if (frameRow.TryGetValue("Achived8Ball", out var eb1) && (eb1 == "1" || eb1.ToLower() == "true"))
                        eightBall = true;
                    else if (frameRow.TryGetValue("EightBall", out var eb2) && (eb2 == "1" || eb2.ToLower() == "true"))
                        eightBall = true;

                    // Look up pre-calculated VBA ratings for both players
                    int? homeOppRating = null, homePlayerRating = null;
                    int? awayOppRating = null, awayPlayerRating = null;
                    int? frameWeekNo = matchWeekNo > 0 ? matchWeekNo : null;
                    
                    // Home player (Player1) rating data
                    if (playerResultLookup.TryGetValue((matchNo, frameNo, vbaPlayer1Id), out var homeRatings))
                    {
                        homeOppRating = homeRatings.oppRating;
                        homePlayerRating = homeRatings.playerRating;
                        if (homeRatings.weekNo > 0) frameWeekNo = homeRatings.weekNo;
                    }
                    
                    // Away player (Player2) rating data
                    if (playerResultLookup.TryGetValue((matchNo, frameNo, vbaPlayer2Id), out var awayRatings))
                    {
                        awayOppRating = awayRatings.oppRating;
                        awayPlayerRating = awayRatings.playerRating;
                        if (awayRatings.weekNo > 0) frameWeekNo = awayRatings.weekNo;
                    }

                    var frame = new FrameResult
                    {
                        Number = frameNo,
                        HomePlayerId = player1Guid,
                        AwayPlayerId = player2Guid,
                        Winner = winner,
                        EightBall = eightBall,
                        // VBA pre-calculated rating data
                        HomeOppRating = homeOppRating,
                        HomePlayerRating = homePlayerRating,
                        AwayOppRating = awayOppRating,
                        AwayPlayerRating = awayPlayerRating,
                        WeekNo = frameWeekNo
                    };

                    fixture.Frames.Add(frame);
                    result.FramesImported++;
                }

                if (hadExistingFrames)
                {
                    result.FixturesUpdated++;
                }

                result.ResultsImported++;
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
            data.Venues.RemoveAll(v => result.ImportedVenueIds.Contains(v.Id));
            data.Divisions.RemoveAll(d => result.ImportedDivisionIds.Contains(d.Id));
            data.Seasons.RemoveAll(s => result.ImportedSeasonIds.Contains(s.Id));
        }
    }
}
