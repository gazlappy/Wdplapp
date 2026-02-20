using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Maui;
using Plugin.LocalNotification;
using Plugin.Maui.OCR;
using Wdpl2.Data;
using Wdpl2.Services;
using Wdpl2.ViewModels;
using Wdpl2.Views;
using WdplNotificationService = Wdpl2.Services.INotificationService;

namespace Wdpl2;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()  // Add Community Toolkit
            .UseLocalNotification()     // Add Local Notifications
            .UseOcr()                   // Add OCR support for score card scanning
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register Database Context (Transient to avoid captive-dependency with data store)
        builder.Services.AddDbContext<LeagueContext>(ServiceLifetime.Transient);

        // Register Data Services
        // Use SqliteDataStore for new implementation, DataStoreService for legacy
        builder.Services.AddTransient<IDataStore, SqliteDataStore>();
        builder.Services.AddTransient<DataMigrationService>();
        
        // Register Notification Services (NEW) - Use alias to avoid conflicts
        builder.Services.AddSingleton<WdplNotificationService, LocalNotificationService>();
        builder.Services.AddSingleton<MatchReminderService>();
        
        // Register ViewModels
        builder.Services.AddTransient<CompetitionsViewModel>();
        builder.Services.AddTransient<VenuesViewModel>();
        builder.Services.AddTransient<DivisionsViewModel>();
        builder.Services.AddTransient<PlayersViewModel>();
        builder.Services.AddTransient<TeamsViewModel>();
        builder.Services.AddTransient<SeasonsViewModel>();
        builder.Services.AddTransient<FixturesViewModel>();
        builder.Services.AddTransient<LeagueTablesViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        
        // Register Pages
        builder.Services.AddTransient<CompetitionsPage>();
        builder.Services.AddTransient<VenuesPage>();
        builder.Services.AddTransient<DivisionsPage>();
        builder.Services.AddTransient<PlayersPage>();
        builder.Services.AddTransient<TeamsPage>();
        builder.Services.AddTransient<SeasonsPage>();
        builder.Services.AddTransient<FixturesPage>();
        builder.Services.AddTransient<LeagueTablesPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<SqlImportPage>();  // Add SQL Import page

        var app = builder.Build();

        // Initialize database and run migration if needed.
        // Use Task.Run to avoid SynchronizationContext deadlock on WinUI main thread.
        try
        {
            Task.Run(() => InitializeDatabaseAsync(app.Services)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database init failed at top level: {ex.Message}");
        }

        return app;
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LeagueContext>();
        var migrationService = scope.ServiceProvider.GetRequiredService<DataMigrationService>();

        // Delete stale database from previous broken model to force clean recreation
        var dbPath = LeagueContext.GetDatabasePath();
        try
        {
            if (File.Exists(dbPath))
            {
                // Only delete if migration is still needed (DB has no data = safe to delete)
                await context.Database.EnsureCreatedAsync();
                if (await migrationService.IsMigrationNeededAsync())
                {
                    System.Diagnostics.Debug.WriteLine("Database empty - deleting for clean recreation with updated model...");
                    await context.Database.EnsureDeletedAsync();
                }
            }
        }
        catch
        {
            // Model validation failed on old DB - delete it
            System.Diagnostics.Debug.WriteLine("Old database incompatible - deleting...");
            try { File.Delete(dbPath); } catch { }
        }

        try
        {
            // Initialize database
            await context.InitializeDatabaseAsync();

            // Check if migration is needed
            if (await migrationService.IsMigrationNeededAsync())
            {
                System.Diagnostics.Debug.WriteLine("Starting data migration from JSON to SQLite...");
                
                var result = await migrationService.MigrateAsync(new Progress<MigrationProgress>(p =>
                {
                    System.Diagnostics.Debug.WriteLine($"Migration: {p.Stage} ({p.Percentage}%)");
                }));

                if (result.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"Migration successful! Migrated {result.TotalRecords} records in {result.Duration.TotalSeconds:F1}s");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Migration failed: {result.ErrorMessage}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No migration needed - database already initialized");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}");
            
            // The database may have been created with an old/broken model.
            // Delete and recreate it so the new model schema is applied.
            try
            {
                System.Diagnostics.Debug.WriteLine("Recreating database with updated model...");
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
                
                // Retry migration after recreating
                if (await migrationService.IsMigrationNeededAsync())
                {
                    var result = await migrationService.MigrateAsync();
                    System.Diagnostics.Debug.WriteLine(result.Success
                        ? $"Migration retry successful! {result.TotalRecords} records"
                        : $"Migration retry failed: {result.ErrorMessage}");
                }
            }
            catch (Exception retryEx)
            {
                System.Diagnostics.Debug.WriteLine($"Database recreation failed: {retryEx.Message}");
            }
        }
    }
}
