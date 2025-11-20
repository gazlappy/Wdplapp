using Microsoft.EntityFrameworkCore;
using Wdpl2.Models;

namespace Wdpl2.Data;

/// <summary>
/// Entity Framework Core database context for the league management system.
/// Provides access to all entities and configures relationships.
/// </summary>
public class LeagueContext : DbContext
{
    // Entity sets
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Fixture> Fixtures => Set<Fixture>();
    public DbSet<Competition> Competitions => Set<Competition>();

    public LeagueContext() : base()
    {
    }

    public LeagueContext(DbContextOptions<LeagueContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Get the database path in the app's data directory
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "league.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

#if DEBUG
            // Enable detailed logging in debug mode
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
#endif
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ====== SEASON CONFIGURATION ======
        modelBuilder.Entity<Season>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.StartDate);
            entity.HasIndex(e => e.IsActive);
        });

        // ====== DIVISION CONFIGURATION ======
        modelBuilder.Entity<Division>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.SeasonId);
            
            // Relationship: Division belongs to Season
            entity.HasOne<Season>()
                .WithMany()
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ====== TEAM CONFIGURATION ======
        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.HasIndex(e => e.SeasonId);
            entity.HasIndex(e => e.DivisionId);
            
            // Relationships
            entity.HasOne<Season>()
                .WithMany()
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne<Division>()
                .WithMany()
                .HasForeignKey(e => e.DivisionId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne<Venue>()
                .WithMany()
                .HasForeignKey(e => e.VenueId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ====== PLAYER CONFIGURATION ======
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.SeasonId);
            entity.HasIndex(e => new { e.LastName, e.FirstName });
            
            // Ignore computed properties
            entity.Ignore(e => e.FullName);
            entity.Ignore(e => e.Name);
            
            // Relationships
            entity.HasOne<Season>()
                .WithMany()
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ====== VENUE CONFIGURATION ======
        modelBuilder.Entity<Venue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.SeasonId);
            
            // Store VenueTables as JSON
            entity.OwnsMany(e => e.Tables, tables =>
            {
                tables.ToJson();
                tables.Property(t => t.Label).HasMaxLength(50);
            });
            
            // Relationships
            entity.HasOne<Season>()
                .WithMany()
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ====== FIXTURE CONFIGURATION ======
        modelBuilder.Entity<Fixture>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SeasonId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.SeasonId, e.Date });
            
            // Ignore computed properties
            entity.Ignore(e => e.HomeScore);
            entity.Ignore(e => e.AwayScore);
            
            // Store Frames as JSON
            entity.OwnsMany(e => e.Frames, frames =>
            {
                frames.ToJson();
            });
            
            // Relationships
            entity.HasOne<Season>()
                .WithMany()
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne<Division>()
                .WithMany()
                .HasForeignKey(e => e.DivisionId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(e => e.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(e => e.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ====== COMPETITION CONFIGURATION ======
        modelBuilder.Entity<Competition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.SeasonId);
            entity.HasIndex(e => e.CreatedDate);
            
            // Store complex properties as JSON
            entity.Property(e => e.ParticipantIds)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Guid>()
                );
            
            entity.OwnsMany(e => e.DoublesTeams, teams =>
            {
                teams.ToJson();
            });
            
            entity.OwnsMany(e => e.Rounds, rounds =>
            {
                rounds.ToJson();
            });
            
            entity.OwnsMany(e => e.Groups, groups =>
            {
                groups.ToJson();
            });
            
            entity.OwnsOne(e => e.GroupSettings, settings =>
            {
                settings.ToJson();
            });
            
            // Relationships
            entity.HasOne<Season>()
                .WithMany()
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// Ensures the database is created and all migrations are applied
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        await Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Gets the database file path
    /// </summary>
    public static string GetDatabasePath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, "league.db");
    }
}
