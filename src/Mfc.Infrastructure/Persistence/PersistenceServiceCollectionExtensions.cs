using Mfc.Infrastructure.Persistence.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mfc.Infrastructure.Persistence;

/// <summary>
/// Registers PostgreSQL EF Core persistence for the Controller host.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="MfcDbContext"/> and schema readiness guard. Does not auto-migrate on startup.
    /// </summary>
    public static IServiceCollection AddMfcPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<MfcDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(MfcDbContext).Assembly.FullName);
            });
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(false);
        });

        services.AddHostedService<DatabaseSchemaGuardHostedService>();
        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations. Intended for <c>--migrate-only</c> and test setup.
    /// </summary>
    public static async Task MigrateAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        SchemaMetadataEntitySeed.EnsureBootstrapMetadata(db);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
