using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Maui;
using Plugin.LocalNotification;
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
            .UseLocalNotification()     // Add Local Notifications (NEW)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register Database Context
        builder.Services.AddDbContext<LeagueContext>();
        
        // Register Data Services
        // Use SqliteDataStore for new implementation, DataStoreService for legacy
        builder.Services.AddSingleton<IDataStore, SqliteDataStore>();
        builder.Services.AddSingleton<DataMigrationService>();
        
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

        var app = builder.Build();

        // Initialize database and run migration if needed
        InitializeDatabaseAsync(app.Services).GetAwaiter().GetResult();

        return app;
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LeagueContext>();
        var migrationService = scope.ServiceProvider.GetRequiredService<DataMigrationService>();

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
            // Don't crash the app, but log the error
        }
    }
}
