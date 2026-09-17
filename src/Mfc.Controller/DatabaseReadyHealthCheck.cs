using Microsoft.Extensions.Diagnostics.HealthChecks;
using Mfc.Infrastructure.Persistence;

namespace Mfc.Controller;

/// <summary>
/// Fail-closed readiness probe: Controller is ready only when PostgreSQL is reachable.
/// </summary>
public sealed class DatabaseReadyHealthCheck : IHealthCheck
{
    private readonly MfcDbContext _db;

    public DatabaseReadyHealthCheck(MfcDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            bool ok = await _db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            return ok
                ? HealthCheckResult.Healthy("database")
                : HealthCheckResult.Unhealthy("database unreachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("database unreachable", ex);
        }
    }
}
