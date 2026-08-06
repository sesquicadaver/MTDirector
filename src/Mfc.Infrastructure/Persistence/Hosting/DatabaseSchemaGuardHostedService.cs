using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mfc.Infrastructure.Persistence.Hosting;

/// <summary>
/// Fails host start when mandatory EF migrations are pending. Does not apply migrations.
/// </summary>
public sealed partial class DatabaseSchemaGuardHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseSchemaGuardHostedService> _logger;

    public DatabaseSchemaGuardHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseSchemaGuardHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        string[] pending = (await db.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false)).ToArray();

        if (pending.Length > 0)
        {
            string names = string.Join(", ", pending);
            LogPendingMigrations(_logger, pending.Length);
            throw new InvalidOperationException(
                $"Mandatory database migrations are pending ({names}). Run Mfc.Controller --migrate-only.");
        }

        LogSchemaReady(_logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Controller refused to start: pending database migrations ({Count}). Run with --migrate-only.")]
    private static partial void LogPendingMigrations(ILogger logger, int count);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Database schema is up to date.")]
    private static partial void LogSchemaReady(ILogger logger);
}
