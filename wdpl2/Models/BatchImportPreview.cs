using System;
using System.Collections.Generic;
using System.Linq;

namespace Wdpl2.Models;

/// <summary>
/// Batch import for multiple HTML files with preview
/// </summary>
public class BatchImportPreview
{
    public List<ImportFilePreview> Files { get; set; } = new();
    public DateTime ImportedAt { get; set; } = DateTime.Now;
    public Guid? TargetSeasonId { get; set; }
    public string? TargetSeasonName { get; set; }
    
    public int TotalFiles => Files.Count;
    public int SelectedFiles => Files.Count(f => f.Include);
    public int TotalRecords => Files.Where(f => f.Include).Sum(f => f.TotalRecords);
    public bool HasErrors => Files.Any(f => f.HasErrors);
    public bool HasWarnings => Files.Any(f => f.HasWarnings);
    public bool IsEmpty => !Files.Any();
}

/// <summary>
/// Preview of a single HTML file in a batch import
/// </summary>
public class ImportFilePreview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string PageTitle { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.Now;
    
    // Preview data
    public ImportPreview? Preview { get; set; }
    
    // Import options
    public bool Include { get; set; } = true;
    public bool IsExpanded { get; set; } = false;
    
    // Status
    public FileImportStatus Status { get; set; } = FileImportStatus.Pending;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    
    // Statistics
    public int TablesFound { get; set; }
    public bool HasLeagueTable { get; set; }
    public bool HasResults { get; set; }
    public bool HasPlayerStats { get; set; }
    public bool HasFixtures { get; set; }
    
    public int TotalRecords => (Preview?.TotalRecords ?? 0);
    public bool HasErrors => Errors.Any() || (Preview?.HasErrors ?? false);
    public bool HasWarnings => Warnings.Any() || (Preview?.HasWarnings ?? false);
    
    public string FileSizeFormatted
    {
        get
        {
            if (FileSizeBytes < 1024)
                return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024)
                return $"{FileSizeBytes / 1024.0:F1} KB";
            return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }
    
    public string StatusIcon => Status switch
    {
        FileImportStatus.Pending => "?",
        FileImportStatus.Processing => "?",
        FileImportStatus.Completed => "?",
        FileImportStatus.Failed => "?",
        FileImportStatus.Skipped => "?",
        _ => "?"
    };
    
    public string ContentSummary
    {
        get
        {
            var items = new List<string>();
            if (HasLeagueTable) items.Add("League Table");
            if (HasResults) items.Add("Results");
            if (HasPlayerStats) items.Add("Player Stats");
            if (HasFixtures) items.Add("Fixtures");
            
            return items.Any() ? string.Join(", ", items) : "Unknown Content";
        }
    }
}

public enum FileImportStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Skipped
}

/// <summary>
/// Result of batch import operation
/// </summary>
public class BatchImportResult
{
    public bool Success { get; set; }
    public int FilesProcessed { get; set; }
    public int FilesSucceeded { get; set; }
    public int FilesFailed { get; set; }
    public int FilesSkipped { get; set; }
    
    public int TotalDivisionsCreated { get; set; }
    public int TotalTeamsCreated { get; set; }
    public int TotalPlayersCreated { get; set; }
    public int TotalCompetitionsCreated { get; set; }
    public int TotalFixturesCreated { get; set; }
    public int TotalRecordsUpdated { get; set; }
    
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    
    public string Summary => $"Processed: {FilesProcessed} | Success: {FilesSucceeded} | Failed: {FilesFailed} | Skipped: {FilesSkipped}";
    
    public string DetailedSummary => 
        $"Files: {FilesProcessed} processed ({FilesSucceeded} succeeded, {FilesFailed} failed, {FilesSkipped} skipped)\n\n" +
        $"Records Created:\n" +
        $"  • Divisions: {TotalDivisionsCreated}\n" +
        $"  • Teams: {TotalTeamsCreated}\n" +
        $"  • Players: {TotalPlayersCreated}\n" +
        $"  • Competitions: {TotalCompetitionsCreated}\n" +
        $"  • Fixtures: {TotalFixturesCreated}\n" +
        $"  • Updated: {TotalRecordsUpdated}\n\n" +
        $"Duration: {Duration.TotalSeconds:F1}s";
}

/// <summary>
/// Progress information for batch import
/// </summary>
public class BatchImportProgress
{
    public int TotalFiles { get; set; }
    public int FilesProcessed { get; set; }
    public string CurrentFile { get; set; } = "";
    public string CurrentOperation { get; set; } = "";
    public int PercentComplete => TotalFiles > 0 ? (FilesProcessed * 100 / TotalFiles) : 0;
}
