using System;
using System.Collections.Generic;
using System.Linq;

namespace Wdpl2.Models;

/// <summary>
/// Preview of data extracted from a document before importing
/// Allows user to review, edit, and confirm before adding to database
/// </summary>
public class ImportPreview
{
    public string FileName { get; set; } = "";
    public string FileType { get; set; } = "";
    public DateTime ExtractedAt { get; set; } = DateTime.Now;
    
    public SeasonInfo? DetectedSeason { get; set; }
    public List<DivisionPreview> Divisions { get; set; } = new();
    public List<TeamPreview> Teams { get; set; } = new();
    public List<PlayerPreview> Players { get; set; } = new();
    public List<CompetitionWinner> Competitions { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    
    public int TotalRecords => Divisions.Count + Teams.Count + Players.Count + Competitions.Count;
    public bool HasErrors => Errors.Any();
    public bool HasWarnings => Warnings.Any();
    public bool IsEmpty => TotalRecords == 0;
}

/// <summary>
/// Season information detected from document
/// </summary>
public class SeasonInfo
{
    public string Name { get; set; } = "";
    public int? Year { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsExisting { get; set; }
    public Guid? ExistingSeasonId { get; set; }
    public string? ExistingSeasonName { get; set; }
}

/// <summary>
/// Division preview with winner/runner-up information
/// </summary>
public class DivisionPreview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string WinnerTeam { get; set; } = "";
    public string RunnerUpTeam { get; set; } = "";
    
    public bool IsExisting { get; set; }
    public Guid? ExistingDivisionId { get; set; }
    public string? ConflictMessage { get; set; }
    
    public bool Include { get; set; } = true;
    public ImportStatus Status { get; set; } = ImportStatus.New;
}

/// <summary>
/// Team preview with division assignment
/// </summary>
public class TeamPreview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string DivisionName { get; set; } = "";
    public Guid? DivisionId { get; set; }
    public bool IsWinner { get; set; }
    public bool IsRunnerUp { get; set; }
    
    public bool IsExisting { get; set; }
    public Guid? ExistingTeamId { get; set; }
    public string? ExistingDivisionName { get; set; }
    public string? ConflictMessage { get; set; }
    
    public bool Include { get; set; } = true;
    public ImportStatus Status { get; set; } = ImportStatus.New;
}

/// <summary>
/// Player preview with team assignment
/// </summary>
public class PlayerPreview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string TeamName { get; set; } = "";
    public Guid? TeamId { get; set; }
    public string CompetitionName { get; set; } = "";
    public bool IsWinner { get; set; }
    public bool IsRunnerUp { get; set; }
    
    public bool IsExisting { get; set; }
    public Guid? ExistingPlayerId { get; set; }
    public string? ExistingTeamName { get; set; }
    public string? ConflictMessage { get; set; }
    
    public bool Include { get; set; } = true;
    public ImportStatus Status { get; set; } = ImportStatus.New;
}

/// <summary>
/// Competition winner information
/// </summary>
public class CompetitionWinner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CompetitionName { get; set; } = "";
    public string WinnerName { get; set; } = "";
    public string RunnerUpName { get; set; } = "";
    public CompetitionType Type { get; set; }
    
    public Guid? WinnerPlayerId { get; set; }
    public Guid? RunnerUpPlayerId { get; set; }
    
    public bool IsExisting { get; set; }
    public Guid? ExistingCompetitionId { get; set; }
    public string? ConflictMessage { get; set; }
    
    public bool Include { get; set; } = true;
    public ImportStatus Status { get; set; } = ImportStatus.New;
}

public enum CompetitionType
{
    Singles,
    Doubles,
    MixedDoubles,
    Team,
    Other
}

public enum ImportStatus
{
    New,              // Will be created
    Existing,         // Already exists, will skip or update
    Conflict,         // Conflict detected, needs resolution
    Modified,         // User modified the data
    Excluded          // User excluded from import
}

/// <summary>
/// Result of applying an import preview to the database
/// </summary>
public class ImportApplyResult
{
    public bool Success { get; set; }
    public int DivisionsCreated { get; set; }
    public int TeamsCreated { get; set; }
    public int PlayersCreated { get; set; }
    public int CompetitionsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    
    public string Summary => $"Created: {TotalCreated} | Updated: {RecordsUpdated} | Skipped: {RecordsSkipped}";
    public int TotalCreated => DivisionsCreated + TeamsCreated + PlayersCreated + CompetitionsCreated;
}
