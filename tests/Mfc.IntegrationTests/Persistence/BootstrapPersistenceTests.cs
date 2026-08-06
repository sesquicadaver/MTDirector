using System.Reflection;
using Mfc.Controller;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.Infrastructure.Persistence.Logging;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Mfc.IntegrationTests.Persistence;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class BootstrapPersistenceTests
{
    private readonly PostgresFixture _postgres;

    public BootstrapPersistenceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task MigrateAppliesBootstrapTablesOnEmptyDatabase()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains(
            await db.Database.GetAppliedMigrationsAsync(),
            name => name.Contains("InitialBootstrap", StringComparison.Ordinal));

        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = ANY(ARRAY[
                'controller_instances',
                'schema_metadata',
                'audit_events',
                'encrypted_secrets',
                'idempotency_records'
              ])
            ORDER BY table_name;
            """;
        List<string> tables = [];
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Equal(
            [
                "audit_events",
                "controller_instances",
                "encrypted_secrets",
                "idempotency_records",
                "schema_metadata",
            ],
            tables);

        SchemaMetadataEntity? meta = await db.SchemaMetadata.FindAsync(SchemaMetadataEntitySeed.BootstrapSchemaKey);
        Assert.NotNull(meta);
        Assert.Equal(SchemaMetadataEntitySeed.BootstrapSchemaValue, meta.Value);
        Assert.Equal(TimeSpan.Zero, meta.UpdatedAtUtc.Offset);
    }

    [Fact]
    public async Task SecondMigrateIsIdempotent()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();
        string[] afterFirst = (await GetAppliedAsync(app)).ToArray();

        await app.Services.MigrateAsync();
        string[] afterSecond = (await GetAppliedAsync(app)).ToArray();

        Assert.Equal(afterFirst, afterSecond);
        Assert.Empty(await GetPendingAsync(app));
    }

    [Fact]
    public async Task HostStartFailsWhenMigrationsPending()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await app.StartAsync());

        Assert.Contains("migrate-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HostStartsAfterMigrate()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        await using WebApplication app = BuildApp(connectionString, url);
        await app.Services.MigrateAsync();
        await app.StartAsync();

        using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
        await app.StopAsync(stopCts.Token);
    }

    [Fact]
    public async Task AuditEventsRejectUpdateAndDelete()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        AuditEventEntity audit = new()
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Actor = "test",
            Action = "bootstrap.self-check",
            PayloadJson = """{"ok":true}""",
            PreviousEventHash = null,
            EventHash = Enumerable.Repeat((byte)1, 32).ToArray(),
        };

        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync();

        audit.Action = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        AuditEventEntity tracked = await db.AuditEvents.SingleAsync(e => e.Id == audit.Id);
        db.AuditEvents.Remove(tracked);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void EncryptedSecretsEntityHasNoPlaintextMembers()
    {
        PropertyInfo[] props = typeof(EncryptedSecretEntity).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        string[] names = props.Select(p => p.Name).ToArray();

        Assert.Contains(nameof(EncryptedSecretEntity.Ciphertext), names);
        Assert.Contains(nameof(EncryptedSecretEntity.WrappedDek), names);
        Assert.DoesNotContain(names, n => n.Contains("Plain", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("SecretText", StringComparison.OrdinalIgnoreCase));
        Assert.All(
            props,
            p => Assert.True(
                p.PropertyType != typeof(string) || p.Name == nameof(EncryptedSecretEntity.Algorithm),
                $"Unexpected string property '{p.Name}' on encrypted_secrets."));
    }

    [Fact]
    public void ConnectionStringRedactionRemovesPassword()
    {
        const string raw = "Host=127.0.0.1;Database=mfc;Username=mfc;Password=super-secret;Timeout=5";
        string redacted = RedactingJsonConsoleLoggerProvider.RedactForTests(raw);

        Assert.DoesNotContain("super-secret", redacted, StringComparison.Ordinal);
        Assert.Contains("Password=***", redacted, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplication BuildApp(string connectionString, string? listenUrl = null)
    {
        string url = listenUrl ?? $"http://127.0.0.1:{GetFreeTcpPort()}";
        return Program.BuildHost(
            [
                "--environment", "Development",
                $"--Mfc:Grpc:ListenAddress={url}",
                "--Mfc:Grpc:AllowInsecureLoopback=true",
                "--Mfc:Grpc:ShutdownTimeoutSeconds=5",
                "--Mfc:Security:RequireTls=true",
                "--Mfc:Security:MasterKeyProvider=Development",
                "--Mfc:Authentication:AllowDevelopmentAuthentication=true",
                $"--Mfc:Database:ConnectionString={connectionString}",
            ]);
    }

    private static async Task<IEnumerable<string>> GetAppliedAsync(WebApplication app)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        return await db.Database.GetAppliedMigrationsAsync();
    }

    private static async Task<IEnumerable<string>> GetPendingAsync(WebApplication app)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        return await db.Database.GetPendingMigrationsAsync();
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
