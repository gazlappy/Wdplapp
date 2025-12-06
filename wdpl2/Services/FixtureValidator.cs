using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Validates fixture results to prevent data entry errors
/// </summary>
public static class FixtureValidator
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Validates a fixture before saving
    /// </summary>
    public static ValidationResult ValidateFixture(Fixture fixture, AppSettings settings)
    {
        var result = new ValidationResult { IsValid = true };

        if (fixture == null)
        {
            result.IsValid = false;
            result.Errors.Add("Fixture cannot be null");
            return result;
        }

        // Check required fields
        if (fixture.HomeTeamId == Guid.Empty)
        {
            result.IsValid = false;
            result.Errors.Add("Home team is required");
        }

        if (fixture.AwayTeamId == Guid.Empty)
        {
            result.IsValid = false;
            result.Errors.Add("Away team is required");
        }

        if (fixture.HomeTeamId == fixture.AwayTeamId)
        {
            result.IsValid = false;
            result.Errors.Add("Home and away teams cannot be the same");
        }

        // Validate frames if any exist
        if (fixture.Frames.Any())
        {
            ValidateFrames(fixture, settings, result);
        }

        // Validate scores match frame results
        if (fixture.Frames.Any())
        {
            ValidateScores(fixture, result);
        }

        // Check for future dates with results
        if (fixture.Date > DateTime.Now && fixture.Frames.Any())
        {
            result.Warnings.Add("Future fixture has results - verify date is correct");
        }

        return result;
    }

    private static void ValidateFrames(Fixture fixture, AppSettings settings, ValidationResult result)
    {
        var frames = fixture.Frames.OrderBy(f => f.Number).ToList();

        // Check frame numbers are sequential
        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i].Number != i + 1)
            {
                result.Warnings.Add($"Frame numbers are not sequential (expected {i + 1}, got {frames[i].Number})");
            }
        }

        // Check total frames doesn't exceed maximum
        int maxFrames = settings.DefaultFramesPerMatch * 2; // Allow double for flexibility
        if (frames.Count > maxFrames)
        {
            result.Warnings.Add($"Fixture has {frames.Count} frames (typical max: {settings.DefaultFramesPerMatch})");
        }

        // Validate each frame
        foreach (var frame in frames)
        {
            // Check frame has valid players
            if (!frame.HomePlayerId.HasValue && !frame.AwayPlayerId.HasValue)
            {
                result.Warnings.Add($"Frame {frame.Number} has no players assigned");
            }

            // Check frame has a winner
            if (frame.Winner == FrameWinner.None)
            {
                result.Warnings.Add($"Frame {frame.Number} has no winner");
            }

            // Check 8-ball frame has a winner
            if (frame.EightBall && frame.Winner == FrameWinner.None)
            {
                result.Errors.Add($"Frame {frame.Number} marked as 8-ball but has no winner");
                result.IsValid = false;
            }

            // Check winner matches player assignment
            if (frame.Winner == FrameWinner.Home && !frame.HomePlayerId.HasValue)
            {
                result.Errors.Add($"Frame {frame.Number} won by home but no home player assigned");
                result.IsValid = false;
            }

            if (frame.Winner == FrameWinner.Away && !frame.AwayPlayerId.HasValue)
            {
                result.Errors.Add($"Frame {frame.Number} won by away but no away player assigned");
                result.IsValid = false;
            }
        }
    }

    private static void ValidateScores(Fixture fixture, ValidationResult result)
    {
        // Calculate expected scores from frames
        int expectedHomeScore = fixture.Frames.Count(f => f.Winner == FrameWinner.Home);
        int expectedAwayScore = fixture.Frames.Count(f => f.Winner == FrameWinner.Away);

        // Check if scores match
        if (fixture.HomeScore != expectedHomeScore || fixture.AwayScore != expectedAwayScore)
        {
            result.Errors.Add($"Scores don't match frame results (Expected: {expectedHomeScore}-{expectedAwayScore}, Got: {fixture.HomeScore}-{fixture.AwayScore})");
            result.IsValid = false;
        }

        // Check for impossible scores
        if (fixture.HomeScore < 0 || fixture.AwayScore < 0)
        {
            result.Errors.Add("Scores cannot be negative");
            result.IsValid = false;
        }

        // Check total score isn't impossibly high
        int totalScore = fixture.HomeScore + fixture.AwayScore;
        if (totalScore > 50) // Reasonable maximum
        {
            result.Warnings.Add($"Total score ({totalScore}) is unusually high - verify this is correct");
        }
    }

    /// <summary>
    /// Quick validation for score entry (before frames are created)
    /// </summary>
    public static ValidationResult ValidateScoreEntry(int homeScore, int awayScore, int maxFrames)
    {
        var result = new ValidationResult { IsValid = true };

        if (homeScore < 0)
        {
            result.IsValid = false;
            result.Errors.Add("Home score cannot be negative");
        }

        if (awayScore < 0)
        {
            result.IsValid = false;
            result.Errors.Add("Away score cannot be negative");
        }

        if (homeScore == 0 && awayScore == 0)
        {
            result.Warnings.Add("Both scores are zero - is this correct?");
        }

        int totalFrames = homeScore + awayScore;
        if (totalFrames > maxFrames * 2)
        {
            result.IsValid = false;
            result.Errors.Add($"Total frames ({totalFrames}) exceeds maximum possible ({maxFrames * 2})");
        }

        if (totalFrames < maxFrames && homeScore != awayScore)
        {
            result.Warnings.Add($"Match appears incomplete ({totalFrames} of {maxFrames} possible frames)");
        }

        return result;
    }

    /// <summary>
    /// Validates bulk fixture generation
    /// </summary>
    public static ValidationResult ValidateFixtureGeneration(List<Team> teams, DateTime startDate, int rounds)
    {
        var result = new ValidationResult { IsValid = true };

        if (teams == null || teams.Count < 2)
        {
            result.IsValid = false;
            result.Errors.Add("At least 2 teams are required to generate fixtures");
            return result;
        }

        if (startDate < DateTime.Today.AddMonths(-1))
        {
            result.Warnings.Add("Start date is in the past - fixtures will be created for past dates");
        }

        if (rounds < 1)
        {
            result.IsValid = false;
            result.Errors.Add("Number of rounds must be at least 1");
        }

        if (rounds > 10)
        {
            result.Warnings.Add($"Generating {rounds} rounds will create many fixtures - this may take time");
        }

        // Check for venue conflicts
        var teamsWithoutVenues = teams.Where(t => !t.VenueId.HasValue).ToList();
        if (teamsWithoutVenues.Any())
        {
            result.Warnings.Add($"{teamsWithoutVenues.Count} team(s) have no venue assigned - fixtures may not be scheduled optimally");
        }

        return result;
    }

    /// <summary>
    /// Validates player assignment to frame
    /// </summary>
    public static ValidationResult ValidatePlayerAssignment(Guid playerId, Guid teamId, List<Player> allPlayers)
    {
        var result = new ValidationResult { IsValid = true };

        var player = allPlayers.FirstOrDefault(p => p.Id == playerId);
        if (player == null)
        {
            result.IsValid = false;
            result.Errors.Add("Player not found");
            return result;
        }

        if (player.TeamId != teamId)
        {
            result.Warnings.Add($"{player.FullName} is not registered for this team - verify this is a guest player");
        }

        return result;
    }

    /// <summary>
    /// Validates a match result before submission
    /// </summary>
    public static ValidationResult ValidateMatchResult(Fixture fixture, AppSettings settings, List<Player> allPlayers)
    {
        var basicValidation = ValidateFixture(fixture, settings);
        
        if (!basicValidation.IsValid)
            return basicValidation;

        // Additional match result validations
        var result = new ValidationResult { IsValid = true };
        result.Errors.AddRange(basicValidation.Errors);
        result.Warnings.AddRange(basicValidation.Warnings);

        // Check all frames have players assigned
        var framesWithoutPlayers = fixture.Frames.Where(f => !f.HomePlayerId.HasValue || !f.AwayPlayerId.HasValue).ToList();
        if (framesWithoutPlayers.Any())
        {
            result.Warnings.Add($"{framesWithoutPlayers.Count} frame(s) missing player assignments");
        }

        // Check for duplicate player assignments in same frame
        foreach (var frame in fixture.Frames)
        {
            if (frame.HomePlayerId.HasValue && frame.HomePlayerId == frame.AwayPlayerId)
            {
                result.Errors.Add($"Frame {frame.Number}: Same player assigned to both home and away");
                result.IsValid = false;
            }
        }

        // Check player eligibility
        foreach (var frame in fixture.Frames)
        {
            if (frame.HomePlayerId.HasValue)
            {
                var playerCheck = ValidatePlayerAssignment(frame.HomePlayerId.Value, fixture.HomeTeamId, allPlayers);
                result.Warnings.AddRange(playerCheck.Warnings);
            }

            if (frame.AwayPlayerId.HasValue)
            {
                var playerCheck = ValidatePlayerAssignment(frame.AwayPlayerId.Value, fixture.AwayTeamId, allPlayers);
                result.Warnings.AddRange(playerCheck.Warnings);
            }
        }

        return result;
    }
}
