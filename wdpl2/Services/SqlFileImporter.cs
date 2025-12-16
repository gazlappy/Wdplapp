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
            public int CompetitionsImported { get; set; }
            
            // Skipped counts (duplicates)
            public int TeamsSkipped { get; set; }
            public int PlayersSkipped { get; set; }
            public int FixturesSkipped { get; set; }
            public int ResultsSkipped { get; set; }
            public int VenuesSkipped { get; set; }
            public int CompetitionsSkipped { get; set; }
            
            // Updated counts (re-imports)
            public int FixturesUpdated { get; set; }
            
            public List<Guid> ImportedSeasonIds { get; set; } = new();
            public List<Guid> ImportedDivisionIds { get; set; } = new();
            public List<Guid> ImportedTeamIds { get; } = new();
            public List<Guid> ImportedPlayerIds { get; } = new();
            public List<Guid> ImportedFixtureIds { get; } = new();
            public List<Guid> ImportedVenueIds { get; } = new();
            public List<Guid> ImportedCompetitionIds { get; } = new();

            // VBA ID mappings for reference
            public Dictionary<int, Guid> VbaTeamIdToGuid { get; set; } = new();
            public Dictionary<int, Guid> VbaPlayerIdToGuid { get; } = new();
            public Dictionary<int, Guid> VbaDivisionIdToGuid { get; } = new();
            public Dictionary<string, Guid> VenueNameToGuid { get; set; } = new();
            public Dictionary<int, Guid> VbaCompetitionIdToGuid { get; } = new();
            
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
                $"?? Competitions: {CompetitionsImported} imported" + (CompetitionsSkipped > 0 ? $", {CompetitionsSkipped} skipped" : "") + "\n" +
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

                // Get venue name - try multiple column names
                var venueName = GetStringValue(row, "VenueName", "");
                if (string.IsNullOrWhiteSpace(venueName))
                    venueName = GetStringValue(row, "Venue", "");
                if (!string.IsNullOrWhiteSpace(venueName))
                {
                    data.TeamIdToVenue[teamId] = venueName.Trim();
                }

                // Get division - try multiple column names
                var divisionId = GetIntValue(row, "Division", 0);
                if (divisionId <= 0)
                    divisionId = GetIntValue(row, "DivisionID", 0);
                if (divisionId <= 0)
                    divisionId = GetIntValue(row, "Div", 0);
                    
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
                    
                    // Import competitions (after players/teams are imported so we can link participants)
                    await ImportCompetitions(parsed.Tables, importedData, existingData, result);
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
            
            // Check if season already exists - try multiple matching strategies
            var existingSeason = existingData.Seasons.FirstOrDefault(s => 
                s.Name.Equals(fullSeasonName, StringComparison.OrdinalIgnoreCase));
            
            // Also try matching just by year (common pattern for WDPL seasons)
            if (existingSeason == null)
            {
                existingSeason = existingData.Seasons.FirstOrDefault(s => 
                    s.Name.Contains(seasonYear.ToString()) && 
                    (s.Name.Contains("WDPL", StringComparison.OrdinalIgnoreCase) || 
                     s.Name.Contains(seasonName, StringComparison.OrdinalIgnoreCase)));
            }

            if (existingSeason != null)
            {
                // Use existing season - don't create a new one
                result.Warnings.Add($"Season '{existingSeason.Name}' already exists - adding data to existing season");
                result.DetectedSeason = existingSeason;
                
                // Don't add to importedData.Seasons or ImportedSeasonIds since we're using existing
                return Task.CompletedTask;
            }

            // Only create new season if it doesn't exist
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

            // Clear and rebuild mapping from VBA division ID to GUID
            result.VbaDivisionIdToGuid.Clear();
            
            // Debug: show columns in tbldivisions
            if (tableData["tbldivisions"].Any())
            {
                var columns = string.Join(", ", tableData["tbldivisions"].First().Keys);
                result.Warnings.Add($"tbldivisions columns: {columns}");
                result.Warnings.Add($"Found {tableData["tbldivisions"].Count} divisions in SQL");
            }

            foreach (var divRow in tableData["tbldivisions"])
            {
                // Try multiple column names for division ID
                var vbaDivisionId = GetIntValue(divRow, "DivisionID", 0);
                if (vbaDivisionId <= 0)
                    vbaDivisionId = GetIntValue(divRow, "Division", 0);
                if (vbaDivisionId <= 0)
                    vbaDivisionId = GetIntValue(divRow, "ID", 0);
                    
                var divisionName = GetStringValue(divRow, "DivisionName", "");
                if (string.IsNullOrWhiteSpace(divisionName))
                    divisionName = GetStringValue(divRow, "Name", "Unknown Division");
                
                result.Warnings.Add($"Processing division: ID={vbaDivisionId}, Name='{divisionName}'");
                
                // Check if division already exists in this season
                var existingDivision = existingData.Divisions.FirstOrDefault(d =>
                    d.SeasonId == result.DetectedSeason.Id &&
                    !string.IsNullOrWhiteSpace(d.Name) &&
                    d.Name.Equals(divisionName, StringComparison.OrdinalIgnoreCase));

                if (existingDivision != null)
                {
                    // Use existing division - map VBA ID to its GUID
                    if (vbaDivisionId > 0)
                    {
                        result.VbaDivisionIdToGuid[vbaDivisionId] = existingDivision.Id;
                        result.Warnings.Add($"Mapped existing division: VBA ID {vbaDivisionId} ? '{existingDivision.Name}' ({existingDivision.Id})");
                    }
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
                
                // Map VBA ID to new GUID
                if (vbaDivisionId > 0)
                {
                    result.VbaDivisionIdToGuid[vbaDivisionId] = division.Id;
                    result.Warnings.Add($"Created new division: VBA ID {vbaDivisionId} ? '{divisionName}' ({division.Id})");
                }
                else
                {
                    result.Warnings.Add($"WARNING: Division '{divisionName}' has no VBA ID - teams cannot be mapped to it!");
                }
            }
            
            result.Warnings.Add($"Division mapping complete: {result.VbaDivisionIdToGuid.Count} VBA IDs mapped");

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
            
            // Collect team IDs from multiple sources
            var teamIds = new HashSet<int>();
            
            // Source 1: Get teams directly from tblteams (most reliable - has all teams with their divisions)
            if (tableData.ContainsKey("tblteams"))
            {
                foreach (var teamRow in tableData["tblteams"])
                {
                    var teamId = GetIntValue(teamRow, "TeamID", 0);
                    if (teamId > 0) teamIds.Add(teamId);
                }
                result.Warnings.Add($"Found {teamIds.Count} teams in tblteams table");
                
                // Debug: show columns in tblteams
                if (tableData["tblteams"].Any())
                {
                    var columns = string.Join(", ", tableData["tblteams"].First().Keys);
                    result.Warnings.Add($"tblteams columns: {columns}");
                }
            }
            
            // Source 2: Also get team IDs from fixtures (in case tblteams doesn't have all teams)
            if (tableData.ContainsKey("tblfixtures"))
            {
                var fixtureTeamCount = 0;
                foreach (var fixtureRow in tableData["tblfixtures"])
                {
                    var homeTeamId = GetIntValue(fixtureRow, "HomeTeam", 0);
                    var awayTeamId = GetIntValue(fixtureRow, "AwayTeam", 0);
                    
                    if (homeTeamId > 0 && teamIds.Add(homeTeamId)) fixtureTeamCount++;
                    if (awayTeamId > 0 && teamIds.Add(awayTeamId)) fixtureTeamCount++;
                }
                if (fixtureTeamCount > 0)
                    result.Warnings.Add($"Found {fixtureTeamCount} additional teams from fixtures");
            }
            
            // Source 3: Get team IDs from tblplayers (teams that have players but maybe no fixtures yet)
            if (tableData.ContainsKey("tblplayers"))
            {
                var playerTeamCount = 0;
                foreach (var playerRow in tableData["tblplayers"])
                {
                    var teamId = GetIntValue(playerRow, "Team", 0);
                    if (teamId > 0 && teamIds.Add(teamId)) playerTeamCount++;
                }
                if (playerTeamCount > 0)
                    result.Warnings.Add($"Found {playerTeamCount} additional teams from players");
            }

            if (!teamIds.Any())
            {
                result.Warnings.Add("No teams found in SQL data");
                return Task.CompletedTask;
            }

            // Debug: Show TeamIdToDivision mapping
            if (parsed.TeamIdToDivision.Any())
            {
                var divMappings = string.Join(", ", parsed.TeamIdToDivision.Select(kvp => $"Team{kvp.Key}?Div{kvp.Value}"));
                result.Warnings.Add($"Team-Division mappings from SQL: {divMappings}");
            }
            else
            {
                result.Warnings.Add("WARNING: No team-to-division mappings found in tblteams! All teams will use default division.");
            }
            
            // Debug: Show VbaDivisionIdToGuid mapping
            if (result.VbaDivisionIdToGuid.Any())
            {
                var vbaDivs = string.Join(", ", result.VbaDivisionIdToGuid.Keys.OrderBy(k => k));
                result.Warnings.Add($"VBA Division IDs mapped: {vbaDivs}");
            }
            else
            {
                result.Warnings.Add("WARNING: No VBA division ID mappings available!");
            }

            // Get default division (fallback if team doesn't have a division assigned)
            var defaultDivisionId = importedData.Divisions.FirstOrDefault()?.Id 
                          ?? existingData.Divisions.FirstOrDefault(d => d.SeasonId == result.DetectedSeason?.Id)?.Id 
                          ?? Guid.Empty;
            
            if (defaultDivisionId == Guid.Empty && !result.VbaDivisionIdToGuid.Any())
            {
                result.Errors.Add("No division available for teams");
                return Task.CompletedTask;
            }

            // Track teams per division for reporting
            var teamsPerDivision = new Dictionary<Guid, int>();
            var teamsWithoutDivision = 0;

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

                // Get correct division for this team
                Guid? teamDivisionId = null;
                if (parsed.TeamIdToDivision.TryGetValue(vbaTeamId, out var vbaDivId))
                {
                    if (result.VbaDivisionIdToGuid.TryGetValue(vbaDivId, out var divGuid))
                    {
                        teamDivisionId = divGuid;
                    }
                    else
                    {
                        result.Warnings.Add($"Team {teamName} (ID {vbaTeamId}) has division {vbaDivId} but no GUID mapping found");
                    }
                }
                
                // Fallback to default division if no division found
                if (!teamDivisionId.HasValue)
                {
                    teamDivisionId = defaultDivisionId != Guid.Empty ? defaultDivisionId : null;
                    teamsWithoutDivision++;
                }

                var team = new Team
                {
                    Id = Guid.NewGuid(),
                    Name = teamName,
                    DivisionId = teamDivisionId,
                    SeasonId = result.DetectedSeason?.Id,
                    VenueId = venueId
                };

                importedData.Teams.Add(team);
                existingData.Teams.Add(team);
                result.ImportedTeamIds.Add(team.Id);
                result.VbaTeamIdToGuid[vbaTeamId] = team.Id;
                result.TeamsImported++;
                
                // Track division count
                if (teamDivisionId.HasValue)
                {
                    if (!teamsPerDivision.ContainsKey(teamDivisionId.Value))
                        teamsPerDivision[teamDivisionId.Value] = 0;
                    teamsPerDivision[teamDivisionId.Value]++;
                }
            }

            // Report teams per division
            if (teamsPerDivision.Any())
            {
                var divisionReport = string.Join(", ", teamsPerDivision.Select(kvp =>
                {
                    var divName = importedData.Divisions.FirstOrDefault(d => d.Id == kvp.Key)?.Name
                               ?? existingData.Divisions.FirstOrDefault(d => d.Id == kvp.Key)?.Name
                               ?? "Unknown";
                    return $"{divName}: {kvp.Value}";
                }));
                result.Warnings.Add($"Teams by division: {divisionReport}");
            }
            
            if (teamsWithoutDivision > 0)
            {
                result.Warnings.Add($"WARNING: {teamsWithoutDivision} teams had no division mapping and used default division");
            }

            // Only show warning if we had to use placeholder names
            var placeholderCount = teamIds.Count(id => !parsed.TeamIdToName.ContainsKey(id));
            if (placeholderCount > 0)
                result.Warnings.Add($"{placeholderCount} teams imported with placeholder names (tblteams data missing) - update names manually");

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
            if (result.DetectedSeason == null)
            {
                result.Errors.Add("Cannot import fixtures without a season");
                return Task.CompletedTask;
            }

            // Build a lookup from tblmatchheader for division info (since tblfixtures doesn't have it)
            var matchHeaderLookup = new Dictionary<int, (int weekNo, int division, int homeTeam, int awayTeam)>();
            if (tableData.ContainsKey("tblmatchheader"))
            {
                foreach (var header in tableData["tblmatchheader"])
                {
                    var matchNo = GetIntValue(header, "MatchNo", 0);
                    var weekNo = GetIntValue(header, "WeekNo", 0);
                    var division = GetIntValue(header, "Division", 0);
                    var homeTeam = GetIntValue(header, "TeamHome", 0);
                    var awayTeam = GetIntValue(header, "TeamAway", 0);
                    
                    if (matchNo > 0)
                    {
                        matchHeaderLookup[matchNo] = (weekNo, division, homeTeam, awayTeam);
                    }
                }
                result.Warnings.Add($"Found {matchHeaderLookup.Count} match headers with division info");
            }

            // Try tblfixtures first (for scheduled matches)
            if (tableData.ContainsKey("tblfixtures"))
            {
                foreach (var fixtureRow in tableData["tblfixtures"])
                {
                    var fixtureId = GetIntValue(fixtureRow, "FixtureID", 0);
                    var vbaHomeTeamId = GetIntValue(fixtureRow, "HomeTeam", 0);
                    var vbaAwayTeamId = GetIntValue(fixtureRow, "AwayTeam", 0);
                    var weekNo = GetIntValue(fixtureRow, "WeekNo", 0);
                    var matchNo = GetIntValue(fixtureRow, "MatchNo", fixtureId); // MatchNo often same as FixtureID
                    var matchDate = GetDateTimeValue(fixtureRow, "MatchDate", result.DetectedSeason.StartDate);
                    
                    // Get division from fixture row first (if it has it)
                    var vbaDivisionId = GetIntValue(fixtureRow, "Division", 0);
                    
                    // If no division in fixture, try to find it from tblmatchheader using MatchNo or by team matching
                    if (vbaDivisionId <= 0)
                    {
                        // First try direct MatchNo lookup
                        if (matchHeaderLookup.TryGetValue(matchNo, out var headerInfo))
                        {
                            vbaDivisionId = headerInfo.division;
                        }
                        else
                        {
                            // Try to find by team matching
                            var matchingHeader = matchHeaderLookup.Values
                                .FirstOrDefault(h => h.homeTeam == vbaHomeTeamId && h.awayTeam == vbaAwayTeamId);
                            if (matchingHeader != default)
                            {
                                vbaDivisionId = matchingHeader.division;
                            }
                        }
                    }

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

                    // Get the correct division GUID for this fixture
                    Guid? divisionId = null;
                    if (vbaDivisionId > 0 && result.VbaDivisionIdToGuid.TryGetValue(vbaDivisionId, out var divGuid))
                    {
                        divisionId = divGuid;
                    }
                    else
                    {
                        // Fallback: get division from home team
                        var homeTeam = importedData.Teams.FirstOrDefault(t => t.Id == homeTeamId) ??
                                       existingData.Teams.FirstOrDefault(t => t.Id == homeTeamId);
                        divisionId = homeTeam?.DivisionId;
                    }

                    // Final fallback to first division if still null
                    if (!divisionId.HasValue)
                    {
                        divisionId = importedData.Divisions.FirstOrDefault()?.Id 
                                  ?? existingData.Divisions.FirstOrDefault(d => d.SeasonId == result.DetectedSeason.Id)?.Id;
                    }

                    // Check for existing fixture (same date + teams)
                    var existingFixture = existingData.Fixtures.FirstOrDefault(f =>
                        f.SeasonId == result.DetectedSeason.Id &&
                        f.Date.Date == matchDate.Date &&
                        f.HomeTeamId == homeTeamId &&
                        f.AwayTeamId == awayTeamId);

                    if (existingFixture != null)
                    {
                        // Fixture exists - add to importedData for result processing
                        if (!importedData.Fixtures.Contains(existingFixture))
                        {
                            importedData.Fixtures.Add(existingFixture);
                        }
                        result.FixturesSkipped++;
                        continue;
                    }

                    // Get venue from home team
                    var homeTeamForVenue = importedData.Teams.FirstOrDefault(t => t.Id == homeTeamId) ??
                                   existingData.Teams.FirstOrDefault(t => t.Id == homeTeamId);

                    var fixture = new Fixture
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = result.DetectedSeason.Id,
                        DivisionId = divisionId,
                        HomeTeamId = homeTeamId,
                        AwayTeamId = awayTeamId,
                        VenueId = homeTeamForVenue?.VenueId,
                        Date = matchDate
                    };

                    importedData.Fixtures.Add(fixture);
                    existingData.Fixtures.Add(fixture);
                    result.ImportedFixtureIds.Add(fixture.Id);
                    result.FixturesImported++;
                }
            }
            
            // Also create fixtures from tblmatchheader if they don't exist yet
            // This handles cases where tblmatchheader has matches that aren't in tblfixtures
            if (tableData.ContainsKey("tblmatchheader"))
            {
                foreach (var header in tableData["tblmatchheader"])
                {
                    var matchNo = GetIntValue(header, "MatchNo", 0);
                    var weekNo = GetIntValue(header, "WeekNo", 0);
                    var vbaDivisionId = GetIntValue(header, "Division", 0);
                    var vbaHomeTeamId = GetIntValue(header, "TeamHome", 0);
                    var vbaAwayTeamId = GetIntValue(header, "TeamAway", 0);
                    
                    if (!result.VbaTeamIdToGuid.ContainsKey(vbaHomeTeamId) || 
                        !result.VbaTeamIdToGuid.ContainsKey(vbaAwayTeamId))
                    {
                        continue; // Skip unknown teams
                    }

                    var homeTeamId = result.VbaTeamIdToGuid[vbaHomeTeamId];
                    var awayTeamId = result.VbaTeamIdToGuid[vbaAwayTeamId];
                    
                    // Check if fixture already exists (from tblfixtures or previous import)
                    var existingFixture = existingData.Fixtures.FirstOrDefault(f =>
                        f.SeasonId == result.DetectedSeason.Id &&
                        f.HomeTeamId == homeTeamId &&
                        f.AwayTeamId == awayTeamId);
                    
                    if (existingFixture != null)
                    {
                        // Make sure it's in importedData for result processing
                        if (!importedData.Fixtures.Contains(existingFixture))
                        {
                            importedData.Fixtures.Add(existingFixture);
                        }
                        continue; // Already have this fixture
                    }
                    
                    // Get division GUID
                    Guid? divisionId = null;
                    if (vbaDivisionId > 0 && result.VbaDivisionIdToGuid.TryGetValue(vbaDivisionId, out var divGuid))
                    {
                        divisionId = divGuid;
                    }
                    else
                    {
                        var homeTeam = importedData.Teams.FirstOrDefault(t => t.Id == homeTeamId) ??
                                       existingData.Teams.FirstOrDefault(t => t.Id == homeTeamId);
                        divisionId = homeTeam?.DivisionId;
                    }
                    
                    if (!divisionId.HasValue)
                    {
                        divisionId = importedData.Divisions.FirstOrDefault()?.Id 
                                  ?? existingData.Divisions.FirstOrDefault(d => d.SeasonId == result.DetectedSeason.Id)?.Id;
                    }
                    
                    // Try to get match date from tblmatchdates by week number
                    var matchDate = result.DetectedSeason.StartDate;
                    if (tableData.ContainsKey("tblmatchdates"))
                    {
                        var matchDateRow = tableData["tblmatchdates"].FirstOrDefault(md => 
                            GetIntValue(md, "week", 0) == weekNo || 
                            GetIntValue(md, "WeekNo", 0) == weekNo);
                        if (matchDateRow != null)
                        {
                            matchDate = GetDateTimeValue(matchDateRow, "MatchDate", matchDate);
                        }
                    }
                    
                    var homeTeamForVenue = importedData.Teams.FirstOrDefault(t => t.Id == homeTeamId) ??
                                   existingData.Teams.FirstOrDefault(t => t.Id == homeTeamId);

                    var fixture = new Fixture
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = result.DetectedSeason.Id,
                        DivisionId = divisionId,
                        HomeTeamId = homeTeamId,
                        AwayTeamId = awayTeamId,
                        VenueId = homeTeamForVenue?.VenueId,
                        Date = matchDate
                    };

                    importedData.Fixtures.Add(fixture);
                    existingData.Fixtures.Add(fixture);
                    result.ImportedFixtureIds.Add(fixture.Id);
                    result.FixturesImported++;
                }
            }

            if (!tableData.ContainsKey("tblfixtures") && !tableData.ContainsKey("tblmatchheader"))
            {
                result.Warnings.Add("No tblfixtures or tblmatchheader data found");
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
                    var vbaPlayer1Id = GetIntValue(frameRow, "Player1", 0);
                    var vbaPlayer2Id = GetIntValue(frameRow, "Player2", 0);
                    
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

        /// <summary>
        /// Import competitions from SQL data
        /// Looks for tables like: tblcompetitions, competitions, tblcups, cups, tournaments
        /// </summary>
        private static Task ImportCompetitions(
            Dictionary<string, List<Dictionary<string, string>>> tableData,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            if (result.DetectedSeason == null)
            {
                return Task.CompletedTask;
            }

            // Try to find competition table (various naming conventions)
            var competitionTableNames = new[] { "tblcompetitions", "competitions", "tblcups", "cups", "tournaments", "tblcup", "tbltournament" };
            string? foundTableName = null;
            
            foreach (var tableName in competitionTableNames)
            {
                if (tableData.ContainsKey(tableName))
                {
                    foundTableName = tableName;
                    break;
                }
            }

            if (foundTableName == null)
            {
                // Also try to detect competitions from result text patterns
                DetectCompetitionsFromText(tableData, importedData, existingData, result);
                return Task.CompletedTask;
            }

            result.Warnings.Add($"Found competition data in table: {foundTableName}");

            foreach (var compRow in tableData[foundTableName])
            {
                try
                {
                    // Try to get competition ID
                    var vbaCompId = GetIntValue(compRow, "CompetitionID", 0);
                    if (vbaCompId <= 0)
                        vbaCompId = GetIntValue(compRow, "CupID", 0);
                    if (vbaCompId <= 0)
                        vbaCompId = GetIntValue(compRow, "TournamentID", 0);
                    if (vbaCompId <= 0)
                        vbaCompId = GetIntValue(compRow, "ID", 0);

                    // Get competition name
                    var compName = GetStringValue(compRow, "CompetitionName", "");
                    if (string.IsNullOrWhiteSpace(compName))
                        compName = GetStringValue(compRow, "CupName", "");
                    if (string.IsNullOrWhiteSpace(compName))
                        compName = GetStringValue(compRow, "TournamentName", "");
                    if (string.IsNullOrWhiteSpace(compName))
                        compName = GetStringValue(compRow, "Name", "");
                    if (string.IsNullOrWhiteSpace(compName))
                        compName = $"Competition {vbaCompId}";

                    // Check if competition already exists
                    var existingComp = existingData.Competitions?.FirstOrDefault(c =>
                        c.SeasonId == result.DetectedSeason.Id &&
                        !string.IsNullOrWhiteSpace(c.Name) &&
                        c.Name.Equals(compName, StringComparison.OrdinalIgnoreCase));

                    if (existingComp != null)
                    {
                        if (vbaCompId > 0)
                            result.VbaCompetitionIdToGuid[vbaCompId] = existingComp.Id;
                        result.CompetitionsSkipped++;
                        continue;
                    }

                    // Determine competition format
                    var formatStr = GetStringValue(compRow, "Format", "").ToLower();
                    var typeStr = GetStringValue(compRow, "Type", "").ToLower();
                    var competitionFormat = DetermineCompetitionFormat(formatStr, typeStr, compName);

                    // Get competition date
                    var compDate = GetDateTimeValue(compRow, "Date", DateTime.MinValue);
                    if (compDate == DateTime.MinValue)
                        compDate = GetDateTimeValue(compRow, "StartDate", DateTime.MinValue);

                    // Get winner information
                    var winnerId = GetIntValue(compRow, "WinnerID", 0);
                    if (winnerId <= 0)
                        winnerId = GetIntValue(compRow, "Winner", 0);
                    var winnerName = GetStringValue(compRow, "WinnerName", "");
                    
                    var runnerUpId = GetIntValue(compRow, "RunnerUpID", 0);
                    if (runnerUpId <= 0)
                        runnerUpId = GetIntValue(compRow, "RunnerUp", 0);
                    var runnerUpName = GetStringValue(compRow, "RunnerUpName", "");

                    // Create competition
                    var competition = new Competition
                    {
                        Id = Guid.NewGuid(),
                        Name = compName,
                        SeasonId = result.DetectedSeason.Id,
                        Format = competitionFormat,
                        Status = CompetitionStatus.Completed, // Historical data is always completed
                        StartDate = compDate != DateTime.MinValue ? compDate : null,
                        CreatedDate = DateTime.Now,
                        Notes = BuildCompetitionNotes(compRow, winnerName, runnerUpName)
                    };

                    // Try to find winner/runner-up GUIDs
                    Guid? winnerGuid = null;
                    Guid? runnerUpGuid = null;

                    if (winnerId > 0 && result.VbaPlayerIdToGuid.TryGetValue(winnerId, out var wGuid))
                    {
                        winnerGuid = wGuid;
                        competition.ParticipantIds.Add(wGuid);
                    }

                    if (runnerUpId > 0 && result.VbaPlayerIdToGuid.TryGetValue(runnerUpId, out var rGuid))
                    {
                        runnerUpGuid = rGuid;
                        competition.ParticipantIds.Add(rGuid);
                    }

                    // Create final round with result if we have winner info
                    if (winnerGuid.HasValue || !string.IsNullOrEmpty(winnerName))
                    {
                        var finalRound = new CompetitionRound
                        {
                            Id = Guid.NewGuid(),
                            Name = "Final",
                            RoundNumber = 1
                        };

                        var finalMatch = new CompetitionMatch
                        {
                            Id = Guid.NewGuid(),
                            Participant1Id = winnerGuid,
                            Participant2Id = runnerUpGuid,
                            WinnerId = winnerGuid,
                            IsComplete = true,
                            ScheduledDate = compDate != DateTime.MinValue ? compDate : null,
                            Notes = !string.IsNullOrEmpty(winnerName) ? $"Winner: {winnerName}" : null
                        };

                        // Try to get final score
                        var winnerScore = GetIntValue(compRow, "WinnerScore", 0);
                        var runnerUpScore = GetIntValue(compRow, "RunnerUpScore", 0);
                        if (winnerScore > 0 || runnerUpScore > 0)
                        {
                            finalMatch.Participant1Score = winnerScore;
                            finalMatch.Participant2Score = runnerUpScore;
                        }

                        finalRound.Matches.Add(finalMatch);
                        competition.Rounds.Add(finalRound);
                    }

                    // Add to data
                    importedData.Competitions ??= new List<Competition>();
                    existingData.Competitions ??= new List<Competition>();
                    
                    importedData.Competitions.Add(competition);
                    existingData.Competitions.Add(competition);
                    result.ImportedCompetitionIds.Add(competition.Id);
                    
                    if (vbaCompId > 0)
                        result.VbaCompetitionIdToGuid[vbaCompId] = competition.Id;
                    
                    result.CompetitionsImported++;
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Error importing competition: {ex.Message}");
                }
            }

            // Also try to import competition results from separate results table
            ImportCompetitionResults(tableData, importedData, existingData, result);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Import competition results from tables like tblcupresults, cupresults, etc.
        /// </summary>
        private static void ImportCompetitionResults(
            Dictionary<string, List<Dictionary<string, string>>> tableData,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            var resultTableNames = new[] { "tblcupresults", "cupresults", "tblcompetitionresults", "competitionresults", "tblcupmatch", "cupmatch" };
            string? foundTableName = null;
            
            foreach (var tableName in resultTableNames)
            {
                if (tableData.ContainsKey(tableName))
                {
                    foundTableName = tableName;
                    break;
                }
            }

            if (foundTableName == null)
                return;

            result.Warnings.Add($"Found competition results in table: {foundTableName}");

            // Group results by competition
            var resultsByComp = tableData[foundTableName]
                .GroupBy(r => GetIntValue(r, "CompetitionID", GetIntValue(r, "CupID", 0)))
                .Where(g => g.Key > 0)
                .ToList();

            foreach (var compGroup in resultsByComp)
            {
                var vbaCompId = compGroup.Key;
                
                // Find the competition
                Competition? competition = null;
                if (result.VbaCompetitionIdToGuid.TryGetValue(vbaCompId, out var compGuid))
                {
                    competition = existingData.Competitions?.FirstOrDefault(c => c.Id == compGuid);
                }

                if (competition == null)
                    continue;

                // Process rounds
                var roundGroups = compGroup
                    .GroupBy(r => GetIntValue(r, "Round", GetIntValue(r, "RoundNo", 1)))
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var roundGroup in roundGroups)
                {
                    var roundNo = roundGroup.Key;
                    var roundName = GetRoundName(roundNo, roundGroups.Count);

                    var round = new CompetitionRound
                    {
                        Id = Guid.NewGuid(),
                        Name = roundName,
                        RoundNumber = roundNo
                    };

                    foreach (var matchRow in roundGroup)
                    {
                        var player1VbaId = GetIntValue(matchRow, "Player1", GetIntValue(matchRow, "HomePlayer", 0));
                        var player2VbaId = GetIntValue(matchRow, "Player2", GetIntValue(matchRow, "AwayPlayer", 0));
                        var winnerVbaId = GetIntValue(matchRow, "Winner", GetIntValue(matchRow, "WinnerID", 0));
                        var score1 = GetIntValue(matchRow, "Score1", GetIntValue(matchRow, "HomeScore", 0));
                        var score2 = GetIntValue(matchRow, "Score2", GetIntValue(matchRow, "AwayScore", 0));

                        Guid? p1Guid = player1VbaId > 0 && result.VbaPlayerIdToGuid.TryGetValue(player1VbaId, out var p1) ? p1 : null;
                        Guid? p2Guid = player2VbaId > 0 && result.VbaPlayerIdToGuid.TryGetValue(player2VbaId, out var p2) ? p2 : null;
                        Guid? winnerGuid = winnerVbaId > 0 && result.VbaPlayerIdToGuid.TryGetValue(winnerVbaId, out var w) ? w : null;

                        // If no explicit winner, determine from score
                        if (!winnerGuid.HasValue && (score1 > 0 || score2 > 0))
                        {
                            winnerGuid = score1 > score2 ? p1Guid : (score2 > score1 ? p2Guid : null);
                        }

                        var match = new CompetitionMatch
                        {
                            Id = Guid.NewGuid(),
                            Participant1Id = p1Guid,
                            Participant2Id = p2Guid,
                            WinnerId = winnerGuid,
                            Participant1Score = score1,
                            Participant2Score = score2,
                            IsComplete = winnerGuid.HasValue || score1 > 0 || score2 > 0
                        };

                        round.Matches.Add(match);

                        // Add participants to competition
                        if (p1Guid.HasValue && !competition.ParticipantIds.Contains(p1Guid.Value))
                            competition.ParticipantIds.Add(p1Guid.Value);
                        if (p2Guid.HasValue && !competition.ParticipantIds.Contains(p2Guid.Value))
                            competition.ParticipantIds.Add(p2Guid.Value);
                    }

                    if (round.Matches.Any())
                    {
                        competition.Rounds.Add(round);
                    }
                }
            }
        }

        /// <summary>
        /// Try to detect competitions from text patterns in other tables (like tblleague notes)
        /// </summary>
        private static void DetectCompetitionsFromText(
            Dictionary<string, List<Dictionary<string, string>>> tableData,
            LeagueData importedData,
            LeagueData existingData,
            SqlImportResult result)
        {
            // Common competition names to look for
            var competitionPatterns = new Dictionary<string, CompetitionFormat>
            {
                { @"singles\s+(champion|knockout|cup|trophy)", CompetitionFormat.SinglesKnockout },
                { @"doubles\s+(champion|knockout|cup|trophy)", CompetitionFormat.DoublesKnockout },
                { @"team\s+(knockout|cup)", CompetitionFormat.TeamKnockout },
                { @"chairman'?s?\s+cup", CompetitionFormat.TeamKnockout },
                { @"8\s*ball(er)?\s+(trophy|champion)", CompetitionFormat.SinglesKnockout },
                { @"plate\s+(competition|trophy|cup)", CompetitionFormat.SinglesKnockout },
                { @"pairs\s+(competition|trophy|cup)", CompetitionFormat.DoublesKnockout }
            };

            // Look through all text fields in all tables
            foreach (var table in tableData)
            {
                foreach (var row in table.Value)
                {
                    foreach (var field in row.Values)
                    {
                        if (string.IsNullOrWhiteSpace(field)) continue;

                        foreach (var pattern in competitionPatterns)
                        {
                            var match = Regex.Match(field, pattern.Key, RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                var compName = match.Value.Trim();
                                
                                // Check if we already have this competition
                                var existing = existingData.Competitions?.Any(c =>
                                    c.SeasonId == result.DetectedSeason?.Id &&
                                    c.Name?.Contains(compName, StringComparison.OrdinalIgnoreCase) == true);

                                if (existing != true)
                                {
                                    result.Warnings.Add($"Detected competition reference: '{compName}' - consider adding manually via Historical Competitions import");
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determine competition format from string hints
        /// </summary>
        private static CompetitionFormat DetermineCompetitionFormat(string formatStr, string typeStr, string name)
        {
            var combined = $"{formatStr} {typeStr} {name}".ToLower();

            if (combined.Contains("double") || combined.Contains("pairs"))
                return CompetitionFormat.DoublesKnockout;
            
            if (combined.Contains("team") || combined.Contains("chairman"))
                return CompetitionFormat.TeamKnockout;
            
            if (combined.Contains("round robin") || combined.Contains("league"))
                return CompetitionFormat.RoundRobin;
            
            if (combined.Contains("group"))
                return CompetitionFormat.SinglesGroupStage;
            
            // Default to singles knockout
            return CompetitionFormat.SinglesKnockout;
        }

        /// <summary>
        /// Build notes string for imported competition
        /// </summary>
        private static string BuildCompetitionNotes(Dictionary<string, string> row, string winnerName, string runnerUpName)
        {
            var notes = new StringBuilder();
            notes.AppendLine($"[Historical Import: {DateTime.Now:yyyy-MM-dd HH:mm}]");
            
            if (!string.IsNullOrEmpty(winnerName))
                notes.AppendLine($"Winner: {winnerName}");
            
            if (!string.IsNullOrEmpty(runnerUpName))
                notes.AppendLine($"Runner-up: {runnerUpName}");

            // Add any other relevant fields
            var venue = GetStringValue(row, "Venue", "");
            if (!string.IsNullOrEmpty(venue))
                notes.AppendLine($"Venue: {venue}");

            return notes.ToString().Trim();
        }

        /// <summary>
        /// Get round name from round number
        /// </summary>
        private static string GetRoundName(int roundNo, int totalRounds)
        {
            var roundsFromEnd = totalRounds - roundNo + 1;
            
            return roundsFromEnd switch
            {
                1 => "Final",
                2 => "Semi-Finals",
                3 => "Quarter-Finals",
                4 => "Round of 16",
                5 => "Round of 32",
                _ => $"Round {roundNo}"
            };
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
            data.Competitions?.RemoveAll(c => result.ImportedCompetitionIds.Contains(c.Id));
            data.Fixtures.RemoveAll(f => result.ImportedFixtureIds.Contains(f.Id));
            data.Players.RemoveAll(p => result.ImportedPlayerIds.Contains(p.Id));
            data.Teams.RemoveAll(t => result.ImportedTeamIds.Contains(t.Id));
            data.Venues.RemoveAll(v => result.ImportedVenueIds.Contains(v.Id));
            data.Divisions.RemoveAll(d => result.ImportedDivisionIds.Contains(d.Id));
            data.Seasons.RemoveAll(s => result.ImportedSeasonIds.Contains(s.Id));
        }
    }
}
