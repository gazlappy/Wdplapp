using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Handles migration of data from JSON file storage to SQLite database.
/// </summary>
public class DataMigrationService
{
    private readonly LeagueContext _context;
    private readonly string _jsonFilePath;

    public DataMigrationService(LeagueContext context)
    {
        _context = context;
        // Use the same path as DataStore
        _jsonFilePath = Path.Combine(FileSystem.AppDataDirectory, "wdpl2", "data.json");
    }

    /// <summary>
    /// Checks if migration is needed (JSON file exists and database is empty)
    /// </summary>
    public async Task<bool> IsMigrationNeededAsync()
    {
        // Check if JSON file exists
        if (!File.Exists(_jsonFilePath))
            return false;

        // Check if database is empty
        var hasData = await _context.Seasons.AnyAsync();
        return !hasData;
    }

    /// <summary>
    /// Migrates all data from JSON to SQLite database
    /// </summary>
    public async Task<MigrationResult> MigrateAsync(IProgress<MigrationProgress>? progress = null)
    {
        var result = new MigrationResult();
        var startTime = DateTime.Now;

        try
        {
            // Report start
            progress?.Report(new MigrationProgress { Stage = "Starting migration", Percentage = 0 });

            // 1. Load JSON data
            progress?.Report(new MigrationProgress { Stage = "Loading JSON data", Percentage = 10 });
            var jsonData = await LoadJsonDataAsync();
            
            if (jsonData == null)
            {
                result.Success = false;
                result.ErrorMessage = "Failed to load JSON data";
                return result;
            }

            // 2. Ensure database is created
            progress?.Report(new MigrationProgress { Stage = "Creating database", Percentage = 20 });
            await _context.Database.EnsureCreatedAsync();

            // 3. Migrate Seasons
            progress?.Report(new MigrationProgress { Stage = "Migrating seasons", Percentage = 30 });
            result.SeasonsCount = await MigrateSeasonsAsync(jsonData.Seasons);

            // 4. Migrate Divisions
            progress?.Report(new MigrationProgress { Stage = "Migrating divisions", Percentage = 40 });
            result.DivisionsCount = await MigrateDivisionsAsync(jsonData.Divisions);

            // 5. Migrate Venues
            progress?.Report(new MigrationProgress { Stage = "Migrating venues", Percentage = 50 });
            result.VenuesCount = await MigrateVenuesAsync(jsonData.Venues);

            // 6. Migrate Teams
            progress?.Report(new MigrationProgress { Stage = "Migrating teams", Percentage = 60 });
            result.TeamsCount = await MigrateTeamsAsync(jsonData.Teams);

            // 7. Migrate Players
            progress?.Report(new MigrationProgress { Stage = "Migrating players", Percentage = 70 });
            result.PlayersCount = await MigratePlayersAsync(jsonData.Players);

            // 8. Migrate Fixtures
            progress?.Report(new MigrationProgress { Stage = "Migrating fixtures", Percentage = 80 });
            result.FixturesCount = await MigrateFixturesAsync(jsonData.Fixtures);

            // 9. Migrate Competitions
            progress?.Report(new MigrationProgress { Stage = "Migrating competitions", Percentage = 90 });
            result.CompetitionsCount = await MigrateCompetitionsAsync(jsonData.Competitions);

            // 10. Backup original JSON file
            progress?.Report(new MigrationProgress { Stage = "Creating backup", Percentage = 95 });
            BackupJsonFile();

            // Complete
            progress?.Report(new MigrationProgress { Stage = "Migration complete", Percentage = 100 });
            result.Success = true;
            result.Duration = DateTime.Now - startTime;

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;
            return result;
        }
    }

    private async Task<LeagueData?> LoadJsonDataAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_jsonFilePath);
            return JsonSerializer.Deserialize<LeagueData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading JSON: {ex.Message}");
            return null;
        }
    }

    private async Task<int> MigrateSeasonsAsync(List<Season> seasons)
    {
        if (seasons == null || seasons.Count == 0) return 0;

        await _context.Seasons.AddRangeAsync(seasons);
        await _context.SaveChangesAsync();
        return seasons.Count;
    }

    private async Task<int> MigrateDivisionsAsync(List<Division> divisions)
    {
        if (divisions == null || divisions.Count == 0) return 0;

        await _context.Divisions.AddRangeAsync(divisions);
        await _context.SaveChangesAsync();
        return divisions.Count;
    }

    private async Task<int> MigrateVenuesAsync(List<Venue> venues)
    {
        if (venues == null || venues.Count == 0) return 0;

        await _context.Venues.AddRangeAsync(venues);
        await _context.SaveChangesAsync();
        return venues.Count;
    }

    private async Task<int> MigrateTeamsAsync(List<Team> teams)
    {
        if (teams == null || teams.Count == 0) return 0;

        await _context.Teams.AddRangeAsync(teams);
        await _context.SaveChangesAsync();
        return teams.Count;
    }

    private async Task<int> MigratePlayersAsync(List<Player> players)
    {
        if (players == null || players.Count == 0) return 0;

        await _context.Players.AddRangeAsync(players);
        await _context.SaveChangesAsync();
        return players.Count;
    }

    private async Task<int> MigrateFixturesAsync(List<Fixture> fixtures)
    {
        if (fixtures == null || fixtures.Count == 0) return 0;

        await _context.Fixtures.AddRangeAsync(fixtures);
        await _context.SaveChangesAsync();
        return fixtures.Count;
    }

    private async Task<int> MigrateCompetitionsAsync(List<Competition> competitions)
    {
        if (competitions == null || competitions.Count == 0) return 0;

        await _context.Competitions.AddRangeAsync(competitions);
        await _context.SaveChangesAsync();
        return competitions.Count;
    }

    private void BackupJsonFile()
    {
        if (!File.Exists(_jsonFilePath)) return;

        var backupPath = $"{_jsonFilePath}.backup_{DateTime.Now:yyyyMMdd_HHmmss}";
        File.Copy(_jsonFilePath, backupPath, overwrite: true);
        
        System.Diagnostics.Debug.WriteLine($"JSON backup created: {backupPath}");
    }

    /// <summary>
    /// Rolls back migration by deleting the database and restoring from backup
    /// </summary>
    public async Task<bool> RollbackMigrationAsync()
    {
        try
        {
            // Delete database
            await _context.Database.EnsureDeletedAsync();
            
            // Find most recent backup
            var backupFiles = Directory.GetFiles(
                Path.GetDirectoryName(_jsonFilePath) ?? "",
                Path.GetFileName(_jsonFilePath) + ".backup_*"
            ).OrderByDescending(f => f).ToList();

            if (backupFiles.Any())
            {
                // Restore most recent backup
                File.Copy(backupFiles.First(), _jsonFilePath, overwrite: true);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Rollback error: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Result of a data migration operation
/// </summary>
public class MigrationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public TimeSpan Duration { get; set; }
    
    // Counts
    public int SeasonsCount { get; set; }
    public int DivisionsCount { get; set; }
    public int VenuesCount { get; set; }
    public int TeamsCount { get; set; }
    public int PlayersCount { get; set; }
    public int FixturesCount { get; set; }
    public int CompetitionsCount { get; set; }
    
    public int TotalRecords => SeasonsCount + DivisionsCount + VenuesCount + 
                               TeamsCount + PlayersCount + FixturesCount + CompetitionsCount;

    public override string ToString()
    {
        if (!Success)
            return $"Migration failed: {ErrorMessage}";

        return $"Migration successful! Migrated {TotalRecords} records in {Duration.TotalSeconds:F1}s";
    }
}

/// <summary>
/// Progress information for migration operation
/// </summary>
public class MigrationProgress
{
    public string Stage { get; set; } = "";
    public int Percentage { get; set; }
    public string? Details { get; set; }
}
