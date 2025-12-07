using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Service for batch importing multiple HTML files with preview
/// </summary>
public static class BatchHtmlImportService
{
    /// <summary>
    /// Process multiple HTML files and create batch preview
    /// </summary>
    public static async Task<BatchImportPreview> CreateBatchPreviewAsync(
        List<string> filePaths,
        LeagueData existingData)
    {
        var batchPreview = new BatchImportPreview();

        foreach (var filePath in filePaths)
        {
            var filePreview = await ProcessSingleFileAsync(filePath, existingData);
            batchPreview.Files.Add(filePreview);
        }

        return batchPreview;
    }

    /// <summary>
    /// Process a single HTML file and create preview
    /// </summary>
    private static async Task<ImportFilePreview> ProcessSingleFileAsync(
        string filePath,
        LeagueData existingData)
    {
        var filePreview = new ImportFilePreview
        {
            FileName = Path.GetFileName(filePath),
            FilePath = filePath
        };

        try
        {
            // Get file info
            var fileInfo = new FileInfo(filePath);
            filePreview.FileSizeBytes = fileInfo.Length;

            // Parse HTML
            var htmlResult = await HtmlLeagueParser.ParseHtmlFileAsync(filePath);
            
            filePreview.PageTitle = htmlResult.PageTitle;
            filePreview.TablesFound = htmlResult.Tables.Count;
            filePreview.HasLeagueTable = htmlResult.HasLeagueTable;
            filePreview.HasResults = htmlResult.HasResults;
            filePreview.HasPlayerStats = htmlResult.HasPlayerStats;
            filePreview.HasFixtures = htmlResult.HasFixtures;

            if (!htmlResult.Success)
            {
                filePreview.Errors.AddRange(htmlResult.Errors);
                filePreview.Status = FileImportStatus.Failed;
                return filePreview;
            }

            // Convert HTML tables to ImportPreview
            filePreview.Preview = await ConvertHtmlToImportPreviewAsync(
                htmlResult,
                existingData);

            filePreview.Warnings.AddRange(htmlResult.Warnings);
            filePreview.Status = FileImportStatus.Pending;
        }
        catch (Exception ex)
        {
            filePreview.Errors.Add($"Error processing file: {ex.Message}");
            filePreview.Status = FileImportStatus.Failed;
        }

        return filePreview;
    }

    /// <summary>
    /// Convert HTML parse result to ImportPreview format
    /// </summary>
    private static async Task<ImportPreview> ConvertHtmlToImportPreviewAsync(
        HtmlLeagueParser.HtmlParseResult htmlResult,
        LeagueData existingData)
    {
        var preview = new ImportPreview
        {
            FileName = htmlResult.FileName,
            FileType = ".html"
        };

        // Extract season info from page title
        preview.DetectedSeason = ExtractSeasonFromTitle(htmlResult.PageTitle, existingData);

        // Process each table
        foreach (var table in htmlResult.Tables)
        {
            switch (table.DetectedType)
            {
                case HtmlLeagueParser.TableType.LeagueStandings:
                    await ProcessLeagueStandingsAsync(table, preview, existingData);
                    break;

                case HtmlLeagueParser.TableType.MatchResults:
                    await ProcessMatchResultsAsync(table, preview, existingData);
                    break;

                case HtmlLeagueParser.TableType.PlayerStatistics:
                case HtmlLeagueParser.TableType.TopScorers:
                    await ProcessPlayerStatsAsync(table, preview, existingData);
                    break;
            }
        }

        // Validate
        await ImportPreviewService.ValidatePreviewAsync(preview, existingData);

        return preview;
    }

    /// <summary>
    /// Process league standings table
    /// </summary>
    private static async Task ProcessLeagueStandingsAsync(
        HtmlLeagueParser.HtmlTable table,
        ImportPreview preview,
        LeagueData existingData)
    {
        var standings = HtmlLeagueParser.ParseLeagueStandings(table);

        if (!standings.Any())
            return;

        // Create division from table caption or default
        var divisionName = !string.IsNullOrWhiteSpace(table.Caption) 
            ? table.Caption 
            : "Imported Division";

        var division = new DivisionPreview
        {
            Name = divisionName
        };

        // Check if division exists
        var existingDiv = existingData.Divisions
            .FirstOrDefault(d => string.Equals(d.Name, divisionName, StringComparison.OrdinalIgnoreCase));

        if (existingDiv != null)
        {
            division.IsExisting = true;
            division.ExistingDivisionId = existingDiv.Id;
            division.Status = ImportStatus.Existing;
        }

        // Get top 2 teams for winner/runner-up
        if (standings.Count >= 1)
            division.WinnerTeam = standings[0].TeamName;
        if (standings.Count >= 2)
            division.RunnerUpTeam = standings[1].TeamName;

        preview.Divisions.Add(division);

        // Add all teams
        foreach (var standing in standings)
        {
            var team = new TeamPreview
            {
                Name = standing.TeamName,
                DivisionName = divisionName,
                DivisionId = division.Id,
                IsWinner = standings.IndexOf(standing) == 0,
                IsRunnerUp = standings.IndexOf(standing) == 1
            };

            // Check if team exists
            var existingTeam = existingData.Teams
                .FirstOrDefault(t => string.Equals(t.Name, team.Name, StringComparison.OrdinalIgnoreCase));

            if (existingTeam != null)
            {
                team.IsExisting = true;
                team.ExistingTeamId = existingTeam.Id;
                team.Status = ImportStatus.Existing;

                // Check division conflict
                var existingTeamDiv = existingData.Divisions.FirstOrDefault(d => d.Id == existingTeam.DivisionId);
                if (existingTeamDiv != null && !string.Equals(existingTeamDiv.Name, divisionName, StringComparison.OrdinalIgnoreCase))
                {
                    team.ExistingDivisionName = existingTeamDiv.Name;
                    team.ConflictMessage = $"Exists in '{existingTeamDiv.Name}', will move to '{divisionName}'";
                    team.Status = ImportStatus.Conflict;
                }
            }

            preview.Teams.Add(team);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Process match results table
    /// </summary>
    private static async Task ProcessMatchResultsAsync(
        HtmlLeagueParser.HtmlTable table,
        ImportPreview preview,
        LeagueData existingData)
    {
        var results = HtmlLeagueParser.ParseMatchResults(table);

        // For now, just add teams from results
        // Future: Create fixtures
        foreach (var result in results)
        {
            // Add home team if not already added
            if (!preview.Teams.Any(t => string.Equals(t.Name, result.HomeTeam, StringComparison.OrdinalIgnoreCase)))
            {
                preview.Teams.Add(new TeamPreview
                {
                    Name = result.HomeTeam,
                    DivisionName = table.Caption ?? "Unknown Division"
                });
            }

            // Add away team if not already added
            if (!preview.Teams.Any(t => string.Equals(t.Name, result.AwayTeam, StringComparison.OrdinalIgnoreCase)))
            {
                preview.Teams.Add(new TeamPreview
                {
                    Name = result.AwayTeam,
                    DivisionName = table.Caption ?? "Unknown Division"
                });
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Process player statistics table
    /// </summary>
    private static async Task ProcessPlayerStatsAsync(
        HtmlLeagueParser.HtmlTable table,
        ImportPreview preview,
        LeagueData existingData)
    {
        // Extract player names from table
        // Skip header row if present
        var dataRows = table.HasHeaders ? table.Rows.Skip(1) : table.Rows;

        foreach (var row in dataRows)
        {
            if (row.Count < 2)
                continue;

            // First non-numeric cell is likely the player name
            var playerName = row.FirstOrDefault(cell => 
                !string.IsNullOrWhiteSpace(cell) && 
                !int.TryParse(cell, out _));

            if (string.IsNullOrWhiteSpace(playerName))
                continue;

            // Try to split name
            var nameParts = playerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstName = nameParts.Length > 0 ? nameParts[0] : "";
            var lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";

            // Try to find team name in row
            var teamName = row.Skip(1).FirstOrDefault(cell => 
                !string.IsNullOrWhiteSpace(cell) &&
                !cell.Equals(playerName, StringComparison.OrdinalIgnoreCase) &&
                !int.TryParse(cell, out _)) ?? "";

            preview.Players.Add(new PlayerPreview
            {
                FirstName = firstName,
                LastName = lastName,
                TeamName = teamName
            });
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Extract season info from page title
    /// </summary>
    private static SeasonInfo? ExtractSeasonFromTitle(string title, LeagueData existingData)
    {
        var seasonInfo = new SeasonInfo();

        // Look for year patterns
        var yearMatch = System.Text.RegularExpressions.Regex.Match(
            title, 
            @"(\d{4})(?:/(\d{2,4})|[-\s](\d{4}))?");

        if (yearMatch.Success)
        {
            if (int.TryParse(yearMatch.Groups[1].Value, out var year))
            {
                seasonInfo.Year = year;
                seasonInfo.Name = yearMatch.Value;

                // Try to find existing season
                var existingSeason = existingData.Seasons
                    .FirstOrDefault(s => s.Name != null && 
                        (s.Name.Contains(year.ToString()) ||
                         s.StartDate.Year == year ||
                         s.EndDate.Year == year));

                if (existingSeason != null)
                {
                    seasonInfo.IsExisting = true;
                    seasonInfo.ExistingSeasonId = existingSeason.Id;
                    seasonInfo.ExistingSeasonName = existingSeason.Name;
                }
            }
        }

        // If no year found, use page title
        if (!seasonInfo.Year.HasValue)
        {
            seasonInfo.Name = title;
            seasonInfo.Year = DateTime.Now.Year;
        }

        return seasonInfo;
    }

    /// <summary>
    /// Apply batch import - process all selected files
    /// </summary>
    public static async Task<BatchImportResult> ApplyBatchImportAsync(
        BatchImportPreview batchPreview,
        Guid seasonId,
        LeagueData data,
        IProgress<BatchImportProgress>? progress = null)
    {
        var result = new BatchImportResult();
        var startTime = DateTime.Now;

        var selectedFiles = batchPreview.Files.Where(f => f.Include).ToList();
        result.FilesProcessed = selectedFiles.Count;

        int fileIndex = 0;
        foreach (var filePreview in selectedFiles)
        {
            fileIndex++;
            
            // Report progress
            progress?.Report(new BatchImportProgress
            {
                TotalFiles = selectedFiles.Count,
                FilesProcessed = fileIndex - 1,
                CurrentFile = filePreview.FileName,
                CurrentOperation = "Processing..."
            });

            try
            {
                filePreview.Status = FileImportStatus.Processing;

                if (filePreview.Preview == null)
                {
                    filePreview.Status = FileImportStatus.Failed;
                    filePreview.Errors.Add("No preview data available");
                    result.FilesFailed++;
                    continue;
                }

                // Apply import for this file
                var fileResult = await ImportPreviewService.ApplyPreviewAsync(
                    filePreview.Preview,
                    seasonId,
                    data);

                if (fileResult.Success)
                {
                    filePreview.Status = FileImportStatus.Completed;
                    result.FilesSucceeded++;

                    // Accumulate totals
                    result.TotalDivisionsCreated += fileResult.DivisionsCreated;
                    result.TotalTeamsCreated += fileResult.TeamsCreated;
                    result.TotalPlayersCreated += fileResult.PlayersCreated;
                    result.TotalCompetitionsCreated += fileResult.CompetitionsCreated;
                    result.TotalRecordsUpdated += fileResult.RecordsUpdated;
                }
                else
                {
                    filePreview.Status = FileImportStatus.Failed;
                    filePreview.Errors.AddRange(fileResult.Errors);
                    result.FilesFailed++;
                    result.Errors.AddRange(fileResult.Errors);
                }
            }
            catch (Exception ex)
            {
                filePreview.Status = FileImportStatus.Failed;
                filePreview.Errors.Add($"Import error: {ex.Message}");
                result.FilesFailed++;
                result.Errors.Add($"{filePreview.FileName}: {ex.Message}");
            }
        }

        result.Duration = DateTime.Now - startTime;
        result.Success = result.FilesFailed == 0;

        // Final progress update
        progress?.Report(new BatchImportProgress
        {
            TotalFiles = selectedFiles.Count,
            FilesProcessed = selectedFiles.Count,
            CurrentFile = "",
            CurrentOperation = "Complete"
        });

        return result;
    }
}
