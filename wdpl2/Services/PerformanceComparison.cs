using System.Diagnostics;
using Wdpl2.Data;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Utility for comparing performance between JSON and SQLite implementations
/// </summary>
public class PerformanceComparison
{
    public static async Task<ComparisonResult> ComparePerformanceAsync(Guid seasonId)
    {
        var result = new ComparisonResult();
        
        // Test SQLite
        using (var context = new LeagueContext())
        {
            var sqlite = new SqliteDataStore(context);
            
            // Warm up
            await sqlite.GetTeamsAsync(seasonId);
            
            // Test 1: Load Teams
            var sw = Stopwatch.StartNew();
            var teams = await sqlite.GetTeamsAsync(seasonId);
            sw.Stop();
            result.SqliteLoadTeamsMs = sw.ElapsedMilliseconds;
            result.TeamsCount = teams.Count;
            
            // Test 2: Load Players
            sw.Restart();
            var players = await sqlite.GetPlayersAsync(seasonId);
            sw.Stop();
            result.SqliteLoadPlayersMs = sw.ElapsedMilliseconds;
            result.PlayersCount = players.Count;
            
            // Test 3: Load Fixtures
            sw.Restart();
            var fixtures = await sqlite.GetFixturesAsync(seasonId);
            sw.Stop();
            result.SqliteLoadFixturesMs = sw.ElapsedMilliseconds;
            result.FixturesCount = fixtures.Count;
        }
        
        // Test JSON (if available)
        try
        {
            var jsonStore = new DataStoreService();
            
            var sw = Stopwatch.StartNew();
            var teams = await jsonStore.GetTeamsAsync(seasonId);
            sw.Stop();
            result.JsonLoadTeamsMs = sw.ElapsedMilliseconds;
            
            sw.Restart();
            var players = await jsonStore.GetPlayersAsync(seasonId);
            sw.Stop();
            result.JsonLoadPlayersMs = sw.ElapsedMilliseconds;
            
            sw.Restart();
            var fixtures = await jsonStore.GetFixturesAsync(seasonId);
            sw.Stop();
            result.JsonLoadFixturesMs = sw.ElapsedMilliseconds;
        }
        catch
        {
            // JSON implementation not available
            result.JsonLoadTeamsMs = -1;
            result.JsonLoadPlayersMs = -1;
            result.JsonLoadFixturesMs = -1;
        }
        
        return result;
    }
}

public class ComparisonResult
{
    public int TeamsCount { get; set; }
    public int PlayersCount { get; set; }
    public int FixturesCount { get; set; }
    
    public long SqliteLoadTeamsMs { get; set; }
    public long SqliteLoadPlayersMs { get; set; }
    public long SqliteLoadFixturesMs { get; set; }
    
    public long JsonLoadTeamsMs { get; set; }
    public long JsonLoadPlayersMs { get; set; }
    public long JsonLoadFixturesMs { get; set; }
    
    public double TeamsSpeedup => JsonLoadTeamsMs > 0 ? (double)JsonLoadTeamsMs / SqliteLoadTeamsMs : 0;
    public double PlayersSpeedup => JsonLoadPlayersMs > 0 ? (double)JsonLoadPlayersMs / SqliteLoadPlayersMs : 0;
    public double FixturesSpeedup => JsonLoadFixturesMs > 0 ? (double)JsonLoadFixturesMs / SqliteLoadFixturesMs : 0;
    
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Performance Comparison ===");
        sb.AppendLine($"Teams: {TeamsCount} records");
        sb.AppendLine($"  SQLite: {SqliteLoadTeamsMs}ms");
        if (JsonLoadTeamsMs > 0)
        {
            sb.AppendLine($"  JSON:   {JsonLoadTeamsMs}ms");
            sb.AppendLine($"  Speedup: {TeamsSpeedup:F1}x faster");
        }
        sb.AppendLine();
        
        sb.AppendLine($"Players: {PlayersCount} records");
        sb.AppendLine($"  SQLite: {SqliteLoadPlayersMs}ms");
        if (JsonLoadPlayersMs > 0)
        {
            sb.AppendLine($"  JSON:   {JsonLoadPlayersMs}ms");
            sb.AppendLine($"  Speedup: {PlayersSpeedup:F1}x faster");
        }
        sb.AppendLine();
        
        sb.AppendLine($"Fixtures: {FixturesCount} records");
        sb.AppendLine($"  SQLite: {SqliteLoadFixturesMs}ms");
        if (JsonLoadFixturesMs > 0)
        {
            sb.AppendLine($"  JSON:   {JsonLoadFixturesMs}ms");
            sb.AppendLine($"  Speedup: {FixturesSpeedup:F1}x faster");
        }
        
        return sb.ToString();
    }
}
