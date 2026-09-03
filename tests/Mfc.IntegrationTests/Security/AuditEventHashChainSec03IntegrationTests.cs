using Mfc.Application.Abstractions.Audit;
using Mfc.Controller;
using Mfc.Infrastructure.Audit;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mfc.IntegrationTests.Security;

/// <summary>SEC-03: audit hash chain uses predecessor bytes; appends serialize under Serializable.</summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class AuditEventHashChainSec03IntegrationTests
{
    private readonly PostgresFixture _postgres;

    public AuditEventHashChainSec03IntegrationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task AppendedEventsHashChainIncludesPredecessorBytes()
    {
        string cs = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(cs);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IAuditEventWriter audit = scope.ServiceProvider.GetRequiredService<IAuditEventWriter>();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        await audit.AppendAsync("sec03@test", "sec03.first", """{"n":1}""");
        await audit.AppendAsync("sec03@test", "sec03.second", """{"n":2}""");

        List<AuditEventEntity> rows = await db.AuditEvents
            .OrderBy(e => e.OccurredAtUtc)
            .ThenBy(e => e.Id)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Null(rows[0].PreviousEventHash);
        Assert.NotNull(rows[1].PreviousEventHash);
        Assert.True(rows[0].EventHash.AsSpan().SequenceEqual(rows[1].PreviousEventHash));

        byte[] expectedFirst = AuditEventHashing.Compute(
            null,
            rows[0].Id,
            rows[0].Actor,
            rows[0].Action,
            rows[0].PayloadJson);
        byte[] expectedSecond = AuditEventHashing.Compute(
            rows[0].EventHash,
            rows[1].Id,
            rows[1].Actor,
            rows[1].Action,
            rows[1].PayloadJson);
        Assert.True(expectedFirst.AsSpan().SequenceEqual(rows[0].EventHash));
        Assert.True(expectedSecond.AsSpan().SequenceEqual(rows[1].EventHash));
    }

    [Fact]
    public async Task ConcurrentAppendsDoNotForkTipSilently()
    {
        string cs = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(cs);
        await app.Services.MigrateAsync();

        await using (AsyncServiceScope seed = app.Services.CreateAsyncScope())
        {
            IAuditEventWriter audit = seed.ServiceProvider.GetRequiredService<IAuditEventWriter>();
            await audit.AppendAsync("sec03@test", "sec03.seed", """{"seed":true}""");
        }

        Task[] writers =
        [
            AppendOnceAsync(app, "sec03@test", "sec03.race-a", """{"lane":"a"}"""),
            AppendOnceAsync(app, "sec03@test", "sec03.race-b", """{"lane":"b"}"""),
            AppendOnceAsync(app, "sec03@test", "sec03.race-c", """{"lane":"c"}"""),
        ];
        await Task.WhenAll(writers);

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        List<AuditEventEntity> rows = await db.AuditEvents.ToListAsync();
        Assert.Equal(4, rows.Count);

        AuditEventEntity genesis = Assert.Single(rows, static r => r.PreviousEventHash is null);
        Dictionary<string, AuditEventEntity> byPrevious = rows
            .Where(static r => r.PreviousEventHash is not null)
            .ToDictionary(
                static r => Convert.ToHexString(r.PreviousEventHash!),
                static r => r,
                StringComparer.Ordinal);
        Assert.Equal(rows.Count - 1, byPrevious.Count);

        List<AuditEventEntity> chain = [genesis];
        while (byPrevious.TryGetValue(Convert.ToHexString(chain[^1].EventHash), out AuditEventEntity? next))
        {
            chain.Add(next);
        }

        Assert.Equal(rows.Count, chain.Count);
        for (int i = 0; i < chain.Count; i++)
        {
            byte[]? previous = i == 0 ? null : chain[i - 1].EventHash;
            byte[] expected = AuditEventHashing.Compute(
                previous,
                chain[i].Id,
                chain[i].Actor,
                chain[i].Action,
                chain[i].PayloadJson);
            Assert.True(expected.AsSpan().SequenceEqual(chain[i].EventHash));
            if (i > 0)
            {
                Assert.True(previous!.AsSpan().SequenceEqual(chain[i].PreviousEventHash));
            }
        }
    }

    private static async Task AppendOnceAsync(WebApplication app, string actor, string action, string payload)
    {
        const int attempts = 8;
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
                IAuditEventWriter audit = scope.ServiceProvider.GetRequiredService<IAuditEventWriter>();
                await audit.AppendAsync(actor, action, payload);
                return;
            }
            catch (Exception ex) when (IsSerializationConflict(ex) && i < attempts - 1)
            {
                await Task.Delay(15 * (i + 1));
            }
        }

        throw new InvalidOperationException($"Failed to append audit event '{action}' after contention retries.");
    }

    private static bool IsSerializationConflict(Exception ex)
    {
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            string message = cur.Message;
            if (message.Contains("could not serialize", StringComparison.OrdinalIgnoreCase)
                || message.Contains("40001", StringComparison.Ordinal)
                || message.Contains("serialization failure", StringComparison.OrdinalIgnoreCase)
                || message.Contains("deadlock", StringComparison.OrdinalIgnoreCase)
                || message.Contains("IX_audit_events_PreviousEventHash_unique", StringComparison.Ordinal)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static WebApplication BuildApp(string connectionString)
    {
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        return Program.BuildHost(
            [
                "--environment", "Development",
                $"--Mfc:Grpc:ListenAddress={url}",
                "--Mfc:Grpc:AllowInsecureLoopback=true",
                "--Mfc:Grpc:ShutdownTimeoutSeconds=5",
                "--Mfc:Security:RequireTls=true",
                "--Mfc:Security:MasterKeyProvider=Development",
                "--Mfc:Authentication:AllowDevelopmentAuthentication=true",
                "--Mfc:OperationalJobs:Enabled=false",
                $"--Mfc:Database:ConnectionString={connectionString}",
            ]);
    }

    private static int GetFreeTcpPort()
    {
        System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
