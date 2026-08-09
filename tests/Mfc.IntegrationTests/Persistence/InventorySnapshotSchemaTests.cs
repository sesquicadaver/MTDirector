using Mfc.Controller;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Mfc.IntegrationTests.Persistence;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class InventorySnapshotSchemaTests
{
    private static readonly string[] InventoryTables =
    [
        "capture_operations",
        "device_connection_profiles",
        "devices",
        "nodes",
        "sites",
        "snapshot_capture_sections",
        "snapshot_captures",
        "snapshot_payloads",
    ];

    private readonly PostgresFixture _postgres;

    public InventorySnapshotSchemaTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task MigrateFromBootstrapAppliesInventorySnapshotTables()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        IEnumerable<string> applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, name => name.Contains("InitialBootstrap", StringComparison.Ordinal));
        Assert.Contains(applied, name => name.Contains("InventorySnapshotSchema", StringComparison.Ordinal));
        Assert.Contains(applied, name => name.Contains("SnapshotCaptureSectionsM123", StringComparison.Ordinal));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        List<string> tables = await QueryPublicTablesAsync(connectionString, InventoryTables);
        Assert.Equal(InventoryTables, tables);

        SchemaMetadataEntity? meta = await db.SchemaMetadata.FindAsync(
            SchemaMetadataEntitySeed.InventorySnapshotSchemaKey);
        Assert.NotNull(meta);
        Assert.Equal(SchemaMetadataEntitySeed.InventorySnapshotSchemaValue, meta.Value);

        SchemaMetadataEntity? persistMeta = await db.SchemaMetadata.FindAsync(
            SchemaMetadataEntitySeed.SnapshotPersistSchemaKey);
        Assert.NotNull(persistMeta);
        Assert.Equal(SchemaMetadataEntitySeed.SnapshotPersistSchemaValue, persistMeta.Value);
    }

    [Fact]
    public async Task SiteCodeAndActiveEndpointConstraintsAreEnforced()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        SiteEntity site = NewSite("EDGE01");
        db.Sites.Add(site);
        await db.SaveChangesAsync();

        db.Sites.Add(NewSite("EDGE01"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        NodeEntity node = NewNode(site.Id, "r1");
        db.Nodes.Add(node);
        await db.SaveChangesAsync();

        DeviceEntity first = NewDevice(node.Id, "10.0.0.1", enabled: true);
        db.Devices.Add(first);
        await db.SaveChangesAsync();

        db.Devices.Add(NewDevice(node.Id, "10.0.0.1", enabled: true));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // Disabled devices may share the same endpoint as an enabled peer is unique only when enabled.
        db.Devices.Add(NewDevice(node.Id, "10.0.0.1", enabled: false));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SnapshotPayloadsAndCompletedCapturesAreImmutable()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        SiteEntity site = NewSite("LAB01");
        NodeEntity node = NewNode(site.Id, "core");
        DeviceEntity device = NewDevice(node.Id, "192.0.2.10", enabled: true);
        CaptureOperationEntity operation = new()
        {
            Id = Guid.NewGuid(),
            TargetType = 1,
            TargetId = device.Id,
            RequestedBy = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid(),
            Status = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        byte[] rawHash = Enumerable.Repeat((byte)0x11, 32).ToArray();
        byte[] canonicalHash = Enumerable.Repeat((byte)0x22, 32).ToArray();
        SnapshotPayloadEntity raw = NewPayload(rawHash, payloadKind: 1, schemaVersion: 1);
        SnapshotPayloadEntity canonical = NewPayload(canonicalHash, payloadKind: 2, schemaVersion: 1);
        SnapshotCaptureEntity capture = new()
        {
            Id = Guid.NewGuid(),
            OperationId = operation.Id,
            DeviceId = device.Id,
            Status = SnapshotCaptureEntity.CompletedStatus,
            AttemptCount = 1,
            CaptureStartedAtUtc = DateTimeOffset.UtcNow,
            CaptureCompletedAtUtc = DateTimeOffset.UtcNow,
            RawPayloadHash = rawHash,
            ConfigurationPayloadHash = canonicalHash,
            SectionResultsJson = "[]",
        };

        db.Sites.Add(site);
        db.Nodes.Add(node);
        db.Devices.Add(device);
        db.CaptureOperations.Add(operation);
        db.SnapshotPayloads.AddRange(raw, canonical);
        db.SnapshotCaptures.Add(capture);
        await db.SaveChangesAsync();

        raw.UncompressedSize = 99;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        SnapshotPayloadEntity trackedPayload = await db.SnapshotPayloads.SingleAsync(p => p.PayloadHash == rawHash);
        db.SnapshotPayloads.Remove(trackedPayload);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        SnapshotCaptureEntity trackedCapture = await db.SnapshotCaptures.SingleAsync(c => c.Id == capture.Id);
        trackedCapture.ErrorCode = "tamper";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        SnapshotCaptureEntity toDelete = await db.SnapshotCaptures.SingleAsync(c => c.Id == capture.Id);
        db.SnapshotCaptures.Remove(toDelete);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task RestrictDeleteDoesNotCascadeToSnapshotsOrAudit()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        SiteEntity site = NewSite("KEEP01");
        NodeEntity node = NewNode(site.Id, "n1");
        DeviceEntity device = NewDevice(node.Id, "198.51.100.1", enabled: true);
        CaptureOperationEntity operation = new()
        {
            Id = Guid.NewGuid(),
            TargetType = 1,
            TargetId = device.Id,
            RequestedBy = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid(),
            Status = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        SnapshotCaptureEntity capture = new()
        {
            Id = Guid.NewGuid(),
            OperationId = operation.Id,
            DeviceId = device.Id,
            Status = 3,
            AttemptCount = 1,
            CaptureStartedAtUtc = DateTimeOffset.UtcNow,
            SectionResultsJson = "[]",
        };
        AuditEventEntity audit = new()
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Actor = "test",
            Action = "inventory.self-check",
            PayloadJson = """{"ok":true}""",
            EventHash = Enumerable.Repeat((byte)3, 32).ToArray(),
        };

        db.Sites.Add(site);
        db.Nodes.Add(node);
        db.Devices.Add(device);
        db.CaptureOperations.Add(operation);
        db.SnapshotCaptures.Add(capture);
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using (NpgsqlCommand deleteDevice = conn.CreateCommand())
        {
            deleteDevice.CommandText = """DELETE FROM devices WHERE "Id" = @id;""";
            deleteDevice.Parameters.AddWithValue("id", device.Id);
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(
                () => deleteDevice.ExecuteNonQueryAsync());
            // RESTRICT / FK protection must keep snapshot history (23503 or 23001).
            Assert.StartsWith("23", ex.SqlState, StringComparison.Ordinal);
        }

        await using (NpgsqlCommand countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = """
                SELECT
                  (SELECT COUNT(*) FROM snapshot_captures),
                  (SELECT COUNT(*) FROM audit_events);
                """;
            await using NpgsqlDataReader reader = await countCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, reader.GetInt64(0));
            Assert.Equal(1L, reader.GetInt64(1));
        }

        await using (NpgsqlCommand fkCmd = conn.CreateCommand())
        {
            fkCmd.CommandText = """
                SELECT c.confdeltype
                FROM pg_constraint c
                JOIN pg_class rel ON rel.oid = c.conrelid
                WHERE rel.relname = 'snapshot_captures'
                  AND c.contype = 'f';
                """;
            List<char> deleteActions = [];
            await using NpgsqlDataReader reader = await fkCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                deleteActions.Add(reader.GetChar(0));
            }

            Assert.NotEmpty(deleteActions);
            Assert.All(deleteActions, action => Assert.Equal('r', action)); // restrict
        }
    }

    [Fact]
    public async Task CaptureOperationIdempotencyIsUnique()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        Guid actor = Guid.NewGuid();
        Guid key = Guid.NewGuid();
        db.CaptureOperations.Add(new CaptureOperationEntity
        {
            Id = Guid.NewGuid(),
            TargetType = 1,
            TargetId = Guid.NewGuid(),
            RequestedBy = actor,
            IdempotencyKey = key,
            Status = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        db.CaptureOperations.Add(new CaptureOperationEntity
        {
            Id = Guid.NewGuid(),
            TargetType = 1,
            TargetId = Guid.NewGuid(),
            RequestedBy = actor,
            IdempotencyKey = key,
            Status = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static SiteEntity NewSite(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code + "-name",
        Status = 0,
        RowVersion = 1,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    private static NodeEntity NewNode(Guid siteId, string name) => new()
    {
        Id = Guid.NewGuid(),
        SiteId = siteId,
        Name = name,
        DeclaredKind = 0,
        DeclaredUplinkMode = 1,
        Status = 0,
        RowVersion = 1,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    private static DeviceEntity NewDevice(Guid nodeId, string host, bool enabled) => new()
    {
        Id = Guid.NewGuid(),
        NodeId = nodeId,
        DisplayName = host,
        ManagementHost = host,
        ManagementHostKind = 0,
        ManagementPort = 8729,
        Enabled = enabled,
        RowVersion = 1,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    private static SnapshotPayloadEntity NewPayload(byte[] hash, short payloadKind, int schemaVersion) => new()
    {
        PayloadHash = hash,
        PayloadKind = payloadKind,
        SchemaVersion = schemaVersion,
        Compression = 1,
        UncompressedSize = 16,
        CompressedPayload = [1, 2, 3, 4],
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };

    private static async Task<List<string>> QueryPublicTablesAsync(string connectionString, string[] names)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        string list = string.Join(", ", names.Select(n => $"'{n}'"));
        cmd.CommandText = $"""
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = ANY(ARRAY[{list}])
            ORDER BY table_name;
            """;
        List<string> tables = [];
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
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
