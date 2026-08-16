using Mfc.Application.Abstractions.Persistence;
using Mfc.Infrastructure.Persistence.Hosting;
using Mfc.Infrastructure.Persistence.Inventory;
using Mfc.Infrastructure.Persistence.Policies;
using Mfc.Infrastructure.Persistence.Snapshots;
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

        // IAuditEventWriter → EfAuditEventWriter is registered in AddMfcSecrets (Security).
        services.AddScoped<ISiteStore, EfSiteStore>();
        services.AddScoped<INodeStore, EfNodeStore>();
        services.AddScoped<IDeviceStore, EfDeviceStore>();
        services.AddScoped<IConnectionProfileReadStore, EfConnectionProfileReadStore>();
        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
        services.AddScoped<ISnapshotStore, EfSnapshotStore>();
        services.AddScoped<IPolicyStore, EfPolicyStore>();
        services.AddScoped<IPolicyApprovalStore, EfPolicyApprovalStore>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IZoneDefinitionStore, EfZoneDefinitionStore>();
        services.AddScoped<INodeZoneBindingStore, EfNodeZoneBindingStore>();
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
