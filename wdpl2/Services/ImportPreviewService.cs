using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Service to extract and preview data from Word documents before importing
/// Handles season winners, division results, and competition data
/// </summary>
public static class ImportPreviewService
{
    /// <summary>
    /// Extract preview data from Word document
    /// </summary>
    public static async Task<ImportPreview> ExtractPreviewAsync(
        string filePath,
        LeagueData existingData)
    {
        var preview = new ImportPreview
        {
            FileName = System.IO.Path.GetFileName(filePath),
            FileType = System.IO.Path.GetExtension(filePath)
        };

        try
        {
            // Parse the Word document
            var wordResult = await WordDocumentParser.ParseWordDocumentAsync(filePath);

            if (!wordResult.Success)
            {
                preview.Errors.AddRange(wordResult.Errors);
                return preview;
            }

            // Extract season information
            preview.DetectedSeason = ExtractSeasonInfo(wordResult, existingData);

            // Extract divisions and winners
            ExtractDivisions(wordResult, preview, existingData);

            // Extract competitions
            ExtractCompetitions(wordResult, preview, existingData);

            // Validate and detect conflicts
            await ValidatePreviewAsync(preview, existingData);

            return preview;
        }
        catch (Exception ex)
        {
            preview.Errors.Add($"Extraction error: {ex.Message}");
            return preview;
        }
    }

    /// <summary>
    /// Extract season information from document content
    /// </summary>
    private static SeasonInfo? ExtractSeasonInfo(
        WordDocumentParser.WordParseResult wordResult,
        LeagueData existingData)
    {
        var seasonInfo = new SeasonInfo();

        // Look for year in text
        var allText = string.Join(" ", wordResult.Paragraphs);
        
        // Pattern: "1994 Winners", "2023/24 Season", "Season 2023-2024", etc.
        var yearPatterns = new[]
        {
            @"(\d{4})\s+(Winners|Season|League)",
            @"(\d{4})/(\d{2,4})",
            @"(\d{4})-(\d{4})",
            @"Season\s+(\d{4})"
        };

        foreach (var pattern in yearPatterns)
        {
            var match = Regex.Match(allText, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out var year))
                {
                    seasonInfo.Year = year;
                    seasonInfo.Name = match.Value;
                    
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
                    
                    break;
                }
            }
        }

        // If no year found, suggest creating new season
        if (!seasonInfo.Year.HasValue)
        {
            seasonInfo.Name = $"Imported Season {DateTime.Now.Year}";
            seasonInfo.Year = DateTime.Now.Year;
        }

        return seasonInfo;
    }

    /// <summary>
    /// Extract division information and winners/runners-up
    /// </summary>
    private static void ExtractDivisions(
        WordDocumentParser.WordParseResult wordResult,
        ImportPreview preview,
        LeagueData existingData)
    {
        // Look for division headers: "Premier division", "First division", etc.
        var divisionPattern = @"(Premier|First|Second|Third|Fourth|Division\s+\d+)\s+division";
        
        for (int i = 0; i < wordResult.Paragraphs.Count; i++)
        {
            var paragraph = wordResult.Paragraphs[i];
            var match = Regex.Match(paragraph, divisionPattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var divisionName = match.Value.Trim();
                var division = new DivisionPreview
                {
                    Name = divisionName
                };

                // Look for winner and runner-up in next few lines
                string? winnerTeam = null;
                string? runnerUpTeam = null;

                for (int j = i + 1; j < Math.Min(i + 5, wordResult.Paragraphs.Count); j++)
                {
                    var line = wordResult.Paragraphs[j];
                    
                    if (line.Contains("Winners:", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Winner:", StringComparison.OrdinalIgnoreCase))
                    {
                        winnerTeam = ExtractTeamName(line);
                    }
                    else if (line.Contains("Runners up:", StringComparison.OrdinalIgnoreCase) ||
                             line.Contains("Runner up:", StringComparison.OrdinalIgnoreCase))
                    {
                        runnerUpTeam = ExtractTeamName(line);
                    }
                }

                division.WinnerTeam = winnerTeam ?? "";
                division.RunnerUpTeam = runnerUpTeam ?? "";

                // Check if division exists
                var existingDivision = existingData.Divisions
                    .FirstOrDefault(d => string.Equals(d.Name, divisionName, StringComparison.OrdinalIgnoreCase));
                
                if (existingDivision != null)
                {
                    division.IsExisting = true;
                    division.ExistingDivisionId = existingDivision.Id;
                    division.Status = ImportStatus.Existing;
                }

                preview.Divisions.Add(division);

                // Add teams
                if (!string.IsNullOrEmpty(winnerTeam))
                {
                    preview.Teams.Add(new TeamPreview
                    {
                        Name = winnerTeam,
                        DivisionName = divisionName,
                        DivisionId = division.Id,
                        IsWinner = true
                    });
                }

                if (!string.IsNullOrEmpty(runnerUpTeam))
                {
                    preview.Teams.Add(new TeamPreview
                    {
                        Name = runnerUpTeam,
                        DivisionName = divisionName,
                        DivisionId = division.Id,
                        IsRunnerUp = true
                    });
                }
            }
        }
    }

    /// <summary>
    /// Extract competition information (singles, doubles, etc.)
    /// </summary>
    private static void ExtractCompetitions(
        WordDocumentParser.WordParseResult wordResult,
        ImportPreview preview,
        LeagueData existingData)
    {
        // Look for competition headers
        var competitionPatterns = new Dictionary<string, CompetitionType>
        {
            { @"Chairman'?s?\s+Cup", CompetitionType.Team },
            { @"Ladies\s+Singles", CompetitionType.Singles },
            { @"Men'?s?\s+Singles|Gents\s+Singles", CompetitionType.Singles },
            { @"Mixed\s+Doubles", CompetitionType.MixedDoubles },
            { @"Ladies\s+Doubles", CompetitionType.Doubles },
            { @"Men'?s?\s+Doubles|Gents\s+Doubles", CompetitionType.Doubles },
            { @"Singles\s+Champion", CompetitionType.Singles },
            { @"Singles\s+Runner\s+up", CompetitionType.Singles },
            { @"Doubles\s+Winners", CompetitionType.Doubles },
            { @"8\s+BALLER\s+TROPHY", CompetitionType.Singles }
        };

        for (int i = 0; i < wordResult.Paragraphs.Count; i++)
        {
            var paragraph = wordResult.Paragraphs[i];
            
            foreach (var pattern in competitionPatterns)
            {
                var match = Regex.Match(paragraph, pattern.Key, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var competition = new CompetitionWinner
                    {
                        CompetitionName = match.Value.Trim(),
                        Type = pattern.Value
                    };

                    // Look for winner/runner-up in nearby lines
                    for (int j = i; j < Math.Min(i + 10, wordResult.Paragraphs.Count); j++)
                    {
                        var line = wordResult.Paragraphs[j];
                        
                        if ((line.Contains("Winner", StringComparison.OrdinalIgnoreCase) ||
                             line.Contains("Champion", StringComparison.OrdinalIgnoreCase)) &&
                            !line.Contains("Runner", StringComparison.OrdinalIgnoreCase))
                        {
                            competition.WinnerName = ExtractPlayerName(line);
                        }
                        else if (line.Contains("Runner", StringComparison.OrdinalIgnoreCase) ||
                                 line.Contains("Runners", StringComparison.OrdinalIgnoreCase))
                        {
                            competition.RunnerUpName = ExtractPlayerName(line);
                        }
                    }

                    if (!string.IsNullOrEmpty(competition.WinnerName))
                    {
                        preview.Competitions.Add(competition);

                        // Add players
                        if (!string.IsNullOrEmpty(competition.WinnerName))
                        {
                            var names = ParsePlayerName(competition.WinnerName);
                            foreach (var name in names)
                            {
                                preview.Players.Add(new PlayerPreview
                                {
                                    FirstName = name.firstName,
                                    LastName = name.lastName,
                                    TeamName = ExtractTeamFromParentheses(competition.WinnerName),
                                    CompetitionName = competition.CompetitionName,
                                    IsWinner = true
                                });
                            }
                        }

                        if (!string.IsNullOrEmpty(competition.RunnerUpName))
                        {
                            var names = ParsePlayerName(competition.RunnerUpName);
                            foreach (var name in names)
                            {
                                preview.Players.Add(new PlayerPreview
                                {
                                    FirstName = name.firstName,
                                    LastName = name.lastName,
                                    TeamName = ExtractTeamFromParentheses(competition.RunnerUpName),
                                    CompetitionName = competition.CompetitionName,
                                    IsRunnerUp = true
                                });
                            }
                        }
                    }
                    
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Validate preview data and detect conflicts
    /// </summary>
    internal static async Task ValidatePreviewAsync(
        ImportPreview preview,
        LeagueData existingData)
    {
        // Check for duplicate teams in preview
        var duplicateTeams = preview.Teams
            .GroupBy(t => t.Name.ToLower())
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicateTeams.Any())
        {
            preview.Warnings.Add($"Found {duplicateTeams.Count} duplicate team name(s) in document");
        }

        // Check teams against existing data
        foreach (var team in preview.Teams)
        {
            var existing = existingData.Teams
                .FirstOrDefault(t => string.Equals(t.Name, team.Name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                team.IsExisting = true;
                team.ExistingTeamId = existing.Id;
                team.Status = ImportStatus.Existing;
                
                // Check if team is in different division
                var existingDiv = existingData.Divisions.FirstOrDefault(d => d.Id == existing.DivisionId);
                if (existingDiv != null)
                {
                    team.ExistingDivisionName = existingDiv.Name;
                    if (!string.Equals(existingDiv.Name, team.DivisionName, StringComparison.OrdinalIgnoreCase))
                    {
                        team.ConflictMessage = $"Exists in '{existingDiv.Name}', will move to '{team.DivisionName}'";
                        team.Status = ImportStatus.Conflict;
                    }
                }
            }
        }

        // Check players against existing data
        foreach (var player in preview.Players)
        {
            var existing = existingData.Players
                .FirstOrDefault(p => 
                    string.Equals(p.FirstName, player.FirstName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.LastName, player.LastName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                player.IsExisting = true;
                player.ExistingPlayerId = existing.Id;
                player.Status = ImportStatus.Existing;
                
                // Check if player is on different team
                var existingTeam = existingData.Teams.FirstOrDefault(t => t.Id == existing.TeamId);
                if (existingTeam != null)
                {
                    player.ExistingTeamName = existingTeam.Name;
                    if (!string.Equals(existingTeam.Name, player.TeamName, StringComparison.OrdinalIgnoreCase))
                    {
                        player.ConflictMessage = $"Exists on '{existingTeam.Name}', will move to '{player.TeamName}'";
                        player.Status = ImportStatus.Conflict;
                    }
                }
            }
        }

        // Add summary warnings
        var newTeams = preview.Teams.Count(t => !t.IsExisting);
        var newPlayers = preview.Players.Count(p => !p.IsExisting);
        
        if (newTeams > 0)
            preview.Warnings.Add($"{newTeams} new team(s) will be created");
        if (newPlayers > 0)
            preview.Warnings.Add($"{newPlayers} new player(s) will be created");
    }

    /// <summary>
    /// Apply the preview to the database
    /// </summary>
    public static async Task<ImportApplyResult> ApplyPreviewAsync(
        ImportPreview preview,
        Guid seasonId,
        LeagueData data)
    {
        var result = new ImportApplyResult();

        try
        {
            // Create divisions
            foreach (var divPreview in preview.Divisions.Where(d => d.Include))
            {
                if (divPreview.Status == ImportStatus.New || !divPreview.IsExisting)
                {
                    var division = new Division
                    {
                        Id = divPreview.Id,
                        SeasonId = seasonId,
                        Name = divPreview.Name
                    };
                    data.Divisions.Add(division);
                    result.DivisionsCreated++;
                }
                else
                {
                    result.RecordsSkipped++;
                }
            }

            // Create teams
            foreach (var teamPreview in preview.Teams.Where(t => t.Include))
            {
                if (teamPreview.Status == ImportStatus.New || !teamPreview.IsExisting)
                {
                    var team = new Team
                    {
                        Id = teamPreview.Id,
                        SeasonId = seasonId,
                        Name = teamPreview.Name,
                        DivisionId = teamPreview.DivisionId
                    };
                    data.Teams.Add(team);
                    result.TeamsCreated++;
                }
                else if (teamPreview.Status == ImportStatus.Conflict && teamPreview.ExistingTeamId.HasValue)
                {
                    // Update existing team's division
                    var existing = data.Teams.FirstOrDefault(t => t.Id == teamPreview.ExistingTeamId);
                    if (existing != null)
                    {
                        existing.DivisionId = teamPreview.DivisionId;
                        result.RecordsUpdated++;
                    }
                }
                else
                {
                    result.RecordsSkipped++;
                }
            }

            // Create players
            foreach (var playerPreview in preview.Players.Where(p => p.Include))
            {
                if (playerPreview.Status == ImportStatus.New || !playerPreview.IsExisting)
                {
                    var player = new Player
                    {
                        Id = playerPreview.Id,
                        SeasonId = seasonId,
                        FirstName = playerPreview.FirstName,
                        LastName = playerPreview.LastName,
                        TeamId = playerPreview.TeamId
                    };
                    data.Players.Add(player);
                    result.PlayersCreated++;
                }
                else if (playerPreview.Status == ImportStatus.Conflict && playerPreview.ExistingPlayerId.HasValue)
                {
                    // Update existing player's team
                    var existing = data.Players.FirstOrDefault(p => p.Id == playerPreview.ExistingPlayerId);
                    if (existing != null)
                    {
                        existing.TeamId = playerPreview.TeamId;
                        result.RecordsUpdated++;
                    }
                }
                else
                {
                    result.RecordsSkipped++;
                }
            }

            // Create competitions
            foreach (var compPreview in preview.Competitions.Where(c => c.Include))
            {
                var competition = new Competition
                {
                    Id = compPreview.Id,
                    SeasonId = seasonId,
                    Name = compPreview.CompetitionName,
                    // Note: Competition model doesn't have Type/WinnerName/RunnerUpName fields
                    // These would need to be added to the Competition model if needed
                    // For now, we store the basic competition data
                };
                data.Competitions.Add(competition);
                result.CompetitionsCreated++;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Import failed: {ex.Message}");
        }

        return result;
    }

    // ========== HELPER METHODS ==========

    private static string ExtractTeamName(string line)
    {
        // Pattern: "Winners: Team Name" or "Winners: Team Name (Venue)"
        var match = Regex.Match(line, @":\s*([^(]+)");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        return "";
    }

    private static string ExtractPlayerName(string line)
    {
        // Extract player name from various formats
        // "Winner Richard Forward"
        // "Alison Brooks (Reprobates)"
        // "Ian Oliver & Trudie Townsend (Haywain Hustlers)"
        
        var match = Regex.Match(line, @"(?:Winner|Champion|Runner\s+up)[:\s]+(.+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        
        // Try to extract everything before line end or newline
        return line.Trim();
    }

    private static List<(string firstName, string lastName)> ParsePlayerName(string fullName)
    {
        var players = new List<(string, string)>();
        
        // Remove team name in parentheses
        fullName = Regex.Replace(fullName, @"\([^)]+\)", "").Trim();
        
        // Check for multiple players (doubles): "Name1 & Name2" or "Name1 and Name2"
        if (fullName.Contains('&') || fullName.Contains(" and ", StringComparison.OrdinalIgnoreCase))
        {
            var names = Regex.Split(fullName, @"\s+&\s+|\s+and\s+", RegexOptions.IgnoreCase);
            foreach (var name in names)
            {
                players.Add(SplitName(name.Trim()));
            }
        }
        else
        {
            players.Add(SplitName(fullName));
        }
        
        return players;
    }

    private static (string firstName, string lastName) SplitName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return ("", "");
        if (parts.Length == 1)
            return (parts[0], "");
        
        var firstName = parts[0];
        var lastName = string.Join(" ", parts.Skip(1));
        return (firstName, lastName);
    }

    private static string ExtractTeamFromParentheses(string text)
    {
        var match = Regex.Match(text, @"\(([^)]+)\)");
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }
}
