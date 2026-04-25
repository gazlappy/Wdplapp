using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wdpl2.Data;

namespace Wdpl2.Services;

/// <summary>
/// DI registrations for persistence: EF Core context, data store, migration and backup services.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        // EF Core context (Transient to avoid captive-dependency with data store)
        services.AddDbContext<LeagueContext>(ServiceLifetime.Transient);

        services.AddTransient<IDataStore, SqliteDataStore>();
        services.AddTransient<DataMigrationService>();
        services.AddTransient<BackupService>();

        return services;
    }
}
