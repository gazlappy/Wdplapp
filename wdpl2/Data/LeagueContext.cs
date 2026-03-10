using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
                )
                .Metadata.SetValueComparer(new ValueComparer<List<Guid>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    c => c.Aggregate(0, (h, v) => HashCode.Combine(h, v.GetHashCode())),
                    c => c.ToList()
                ));

            entity.Property(e => e.NoShowIds)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Guid>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<Guid>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    c => c.Aggregate(0, (h, v) => HashCode.Combine(h, v.GetHashCode())),
                    c => c.ToList()
                ));
            
            entity.OwnsMany(e => e.DoublesTeams, teams =>
            {
                teams.ToJson();
            });
            
            entity.OwnsMany(e => e.Rounds, rounds =>
            {
                rounds.ToJson();
                rounds.OwnsMany(r => r.Matches);
                rounds.OwnsMany(r => r.SelectedVenues, venue =>
                {
                    venue.OwnsMany(v => v.SelectedTables);
                });
                rounds.Ignore(r => r.TotalTables);
            });
            
            entity.OwnsMany(e => e.Groups, groups =>
            {
                groups.ToJson();
                groups.OwnsMany(g => g.Matches);
                groups.OwnsMany(g => g.Standings);
            });

            entity.OwnsMany(e => e.PreviousGroups, groups =>
            {
                groups.ToJson();
                groups.OwnsMany(g => g.Matches);
                groups.OwnsMany(g => g.Standings);
            });
            
            entity.OwnsOne(e => e.GroupSettings, settings =>
            {
                settings.ToJson();
                settings.OwnsMany(s => s.SelectedVenues, venue =>
                {
                    venue.OwnsMany(v => v.SelectedTables);
                });
            });

            // NOTE: No FK relationship to Season. Seasons are managed in the
            // JSON data store while competitions live in SQLite, so a database-
            // level FK constraint would always fail. SeasonId is kept as an
            // indexed column for filtering only.
        });
    }

    /// <summary>
    /// Ensures the database is created and all migrations are applied
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        await Database.EnsureCreatedAsync();
        await ApplyManualMigrationsAsync();
    }

    /// <summary>
    /// Adds columns that were introduced after the initial schema.
    /// Each statement uses IF NOT EXISTS / safe checks so it's idempotent.
    /// </summary>
    private async Task ApplyManualMigrationsAsync()
    {
        var conn = Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            // Check if BestOf column exists on Competitions table
            cmd.CommandText = "PRAGMA table_info(Competitions)";
            bool hasBestOf = false;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1).Equals("BestOf", StringComparison.OrdinalIgnoreCase))
                    {
                        hasBestOf = true;
                        break;
                    }
                }
            }
            if (!hasBestOf)
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Competitions ADD COLUMN BestOf INTEGER NOT NULL DEFAULT 0";
                await alter.ExecuteNonQueryAsync();
            }

            // Check if RandomDraw column exists on Competitions table
            bool hasRandomDraw = false;
            using (var cmd2 = conn.CreateCommand())
            {
                cmd2.CommandText = "PRAGMA table_info(Competitions)";
                using var reader2 = await cmd2.ExecuteReaderAsync();
                while (await reader2.ReadAsync())
                {
                    if (reader2.GetString(1).Equals("RandomDraw", StringComparison.OrdinalIgnoreCase))
                    {
                        hasRandomDraw = true;
                        break;
                    }
                }
            }
            if (!hasRandomDraw)
            {
                using var alter2 = conn.CreateCommand();
                alter2.CommandText = "ALTER TABLE Competitions ADD COLUMN RandomDraw INTEGER NOT NULL DEFAULT 1";
                await alter2.ExecuteNonQueryAsync();
            }

            // Check if ParentCompetitionId column exists on Competitions table
            bool hasParentCompId = false;
            using (var cmd3 = conn.CreateCommand())
            {
                cmd3.CommandText = "PRAGMA table_info(Competitions)";
                using var reader3 = await cmd3.ExecuteReaderAsync();
                while (await reader3.ReadAsync())
                {
                    if (reader3.GetString(1).Equals("ParentCompetitionId", StringComparison.OrdinalIgnoreCase))
                    {
                        hasParentCompId = true;
                        break;
                    }
                }
            }
            if (!hasParentCompId)
            {
                using var alter3 = conn.CreateCommand();
                alter3.CommandText = "ALTER TABLE Competitions ADD COLUMN ParentCompetitionId TEXT";
                await alter3.ExecuteNonQueryAsync();
            }

            // Check if PreviousGroups JSON column exists on Competitions table
            bool hasPreviousGroups = false;
            using (var cmd4 = conn.CreateCommand())
            {
                cmd4.CommandText = "PRAGMA table_info(Competitions)";
                using var reader4 = await cmd4.ExecuteReaderAsync();
                while (await reader4.ReadAsync())
                {
                    if (reader4.GetString(1).Equals("PreviousGroups", StringComparison.OrdinalIgnoreCase))
                    {
                        hasPreviousGroups = true;
                        break;
                    }
                }
            }
            if (!hasPreviousGroups)
            {
                using var alter4 = conn.CreateCommand();
                alter4.CommandText = "ALTER TABLE Competitions ADD COLUMN PreviousGroups TEXT DEFAULT '[]'";
                await alter4.ExecuteNonQueryAsync();
            }

            // Check if NoShowIds column exists on Competitions table
            bool hasNoShowIds = false;
            using (var cmd5 = conn.CreateCommand())
            {
                cmd5.CommandText = "PRAGMA table_info(Competitions)";
                using var reader5 = await cmd5.ExecuteReaderAsync();
                while (await reader5.ReadAsync())
                {
                    if (reader5.GetString(1).Equals("NoShowIds", StringComparison.OrdinalIgnoreCase))
                    {
                        hasNoShowIds = true;
                        break;
                    }
                }
            }
            if (!hasNoShowIds)
            {
                using var alter5 = conn.CreateCommand();
                alter5.CommandText = "ALTER TABLE Competitions ADD COLUMN NoShowIds TEXT DEFAULT '[]'";
                await alter5.ExecuteNonQueryAsync();
            }

            // Remove the SeasonId FK constraint from Competitions.
            // Seasons live in the JSON store, so the FK always fails.
            // SQLite can't drop constraints, so we recreate the table.
            await RemoveCompetitionsForeignKeysAsync(conn);
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    /// <summary>
    /// Recreates the Competitions table without FOREIGN KEY constraints.
    /// Seasons are managed in the JSON data store, so the FK to Seasons
    /// is invalid and causes 'FOREIGN KEY constraint failed' errors.
    /// This is idempotent — it checks for the FK before doing anything.
    /// </summary>
    private static async Task RemoveCompetitionsForeignKeysAsync(System.Data.Common.DbConnection conn)
    {
        // Check whether the table actually has a FK to Seasons
        bool hasFk = false;
        using (var fkCmd = conn.CreateCommand())
        {
            fkCmd.CommandText = "PRAGMA foreign_key_list(Competitions)";
            using var fkReader = await fkCmd.ExecuteReaderAsync();
            while (await fkReader.ReadAsync())
            {
                // Column 2 is the referenced table name
                if (fkReader.GetString(2).Equals("Seasons", StringComparison.OrdinalIgnoreCase))
                {
                    hasFk = true;
                    break;
                }
            }
        }

        if (!hasFk) return; // Already clean

        // Temporarily disable FK enforcement so the table swap doesn't fail
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF";
            await pragma.ExecuteNonQueryAsync();
        }

        try
        {
            // Get the current CREATE TABLE statement
            string? createSql = null;
            using (var schemaCmd = conn.CreateCommand())
            {
                schemaCmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='Competitions'";
                createSql = (string?)await schemaCmd.ExecuteScalarAsync();
            }

            if (string.IsNullOrEmpty(createSql)) return;

            // Remove the FK clause from the CREATE TABLE statement.
            // The clause looks like:
            //   FOREIGN KEY ("SeasonId") REFERENCES "Seasons" ("Id") ON DELETE CASCADE
            // or similar variations with/without quotes.
            var fkPattern = new System.Text.RegularExpressions.Regex(
                @",?\s*FOREIGN\s+KEY\s*\([^)]+\)\s*REFERENCES\s*[""']?Seasons[""']?\s*\([^)]+\)(\s*ON\s+DELETE\s+\w+)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var cleanSql = fkPattern.Replace(createSql, "");

            // Swap to the new table name
            var newTableSql = cleanSql.Replace("CREATE TABLE \"Competitions\"", "CREATE TABLE \"Competitions_new\"");

            using var transaction = conn.BeginTransaction();
            try
            {
                using (var c1 = conn.CreateCommand()) { c1.Transaction = transaction; c1.CommandText = newTableSql; await c1.ExecuteNonQueryAsync(); }
                using (var c2 = conn.CreateCommand()) { c2.Transaction = transaction; c2.CommandText = "INSERT INTO \"Competitions_new\" SELECT * FROM \"Competitions\""; await c2.ExecuteNonQueryAsync(); }
                using (var c3 = conn.CreateCommand()) { c3.Transaction = transaction; c3.CommandText = "DROP TABLE \"Competitions\""; await c3.ExecuteNonQueryAsync(); }
                using (var c4 = conn.CreateCommand()) { c4.Transaction = transaction; c4.CommandText = "ALTER TABLE \"Competitions_new\" RENAME TO \"Competitions\""; await c4.ExecuteNonQueryAsync(); }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            // Re-enable FK enforcement
            using var pragmaOn = conn.CreateCommand();
            pragmaOn.CommandText = "PRAGMA foreign_keys = ON";
            await pragmaOn.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Gets the database file path
    /// </summary>
    public static string GetDatabasePath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, "league.db");
    }
}
