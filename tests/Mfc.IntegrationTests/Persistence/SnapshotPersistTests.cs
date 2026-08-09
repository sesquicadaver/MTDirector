using System.Text;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Controller;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.Infrastructure.Persistence.Snapshots;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Mfc.IntegrationTests.Persistence;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class SnapshotPersistTests
{
    private readonly PostgresFixture _postgres;

    public SnapshotPersistTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task PersistCompletedDeduplicatesPayloadsByContentHashRegardlessOfCompressionForm()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        ISnapshotStore store = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();

        DeviceEntity device = await SeedDeviceAsync(db, "DEDUP01", "10.10.0.1");
        byte[] body = Encoding.UTF8.GetBytes("""{"canonical":"same-bytes"}""");
        BrotliPayloadCodec.EncodedPayload encoded = BrotliPayloadCodec.Encode(body);

        // Pre-seed equivalent payload with Compression=None — content hash must not depend on codec form.
        db.SnapshotPayloads.Add(new SnapshotPayloadEntity
        {
            PayloadHash = encoded.PayloadHash,
            PayloadKind = (short)SnapshotPayloadKind.CanonicalConfiguration,
            SchemaVersion = 1,
            Compression = (short)SnapshotCompression.None,
            UncompressedSize = body.LongLength,
            CompressedPayload = body,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        StoredSnapshot first = await store.PersistCompletedAsync(
            NewPersistRequest(device.Id, CreateCapture(body, digestSeed: 0x41)));
        StoredSnapshot second = await store.PersistCompletedAsync(
            NewPersistRequest(device.Id, CreateCapture(body, digestSeed: 0x42), Guid.NewGuid()));

        Assert.NotEqual(first.Metadata.Id, second.Metadata.Id);
        Assert.Equal(first.ConfigurationPayloadHash!.ToString(), second.ConfigurationPayloadHash!.ToString());

        long payloadRows = await db.SnapshotPayloads.LongCountAsync(p => p.PayloadHash == encoded.PayloadHash);
        Assert.Equal(1, payloadRows);
    }

    [Fact]
    public async Task PersistCompletedRollsBackEntireTransactionOnIdempotencyConflict()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        ISnapshotStore store = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();

        DeviceEntity device = await SeedDeviceAsync(db, "ROLL01", "10.10.0.9");
        Guid requestedBy = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid idempotencyKey = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Pre-existing operation with the same idempotency tuple forces SaveChanges failure mid-persist.
        db.CaptureOperations.Add(new CaptureOperationEntity
        {
            Id = Guid.NewGuid(),
            TargetType = 1,
            TargetId = device.Id,
            RequestedBy = requestedBy,
            IdempotencyKey = idempotencyKey,
            Status = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        long payloadsBefore = await db.SnapshotPayloads.LongCountAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            store.PersistCompletedAsync(NewPersistRequest(
                device.Id,
                CreateCapture(Encoding.UTF8.GetBytes("""{"rollback":true}"""), digestSeed: 0x10),
                idempotencyKey)));

        Assert.Equal(1, await db.CaptureOperations.CountAsync());
        Assert.Equal(0, await db.SnapshotCaptures.CountAsync());
        Assert.Equal(payloadsBefore, await db.SnapshotPayloads.LongCountAsync());
        Assert.Equal(0, await db.SnapshotCaptureSections.CountAsync());
        Assert.Null((await db.Devices.SingleAsync(d => d.Id == device.Id)).LastCompletedCaptureId);
    }

    [Fact]
    public async Task PersistCompletedRejectsMissingDeviceWithoutOrphans()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        ISnapshotStore store = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();

        Guid missingDeviceId = Guid.NewGuid();
        SnapshotCaptureResult capture = CreateCapture(Encoding.UTF8.GetBytes("""{"x":1}"""), digestSeed: 0x10);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.PersistCompletedAsync(NewPersistRequest(missingDeviceId, capture)));
        Assert.Contains(missingDeviceId.ToString(), ex.Message, StringComparison.Ordinal);

        Assert.Equal(0, await db.CaptureOperations.CountAsync());
        Assert.Equal(0, await db.SnapshotCaptures.CountAsync());
        Assert.Equal(0, await db.SnapshotPayloads.CountAsync());
        Assert.Equal(0, await db.SnapshotCaptureSections.CountAsync());
    }

    [Fact]
    public async Task PersistCompletedWritesOperationCapturePayloadsAndSectionsAtomically()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        ISnapshotStore store = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();

        DeviceEntity device = await SeedDeviceAsync(db, "ATOM01", "10.10.0.2");
        byte[] body = Encoding.UTF8.GetBytes("""{"section":"interfaces"}""");
        byte[] sectionBody = Encoding.UTF8.GetBytes("""{"rows":[]}""");
        Guid requestedBy = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid idempotencyKey = Guid.Parse("11111111-1111-1111-1111-111111111111");

        SnapshotCaptureResult capture = CreateCapture(
            body,
            digestSeed: 0x55,
            sections:
            [
                new CapturedSectionDescriptor
                {
                    SectionId = "interfaces",
                    SectionVersion = 1,
                    Status = 1,
                    Ordered = true,
                    ConfigurationRecordCount = 2,
                    ObservationRecordCount = 1,
                    ConfigurationPayload = sectionBody,
                },
            ]);

        StoredSnapshot stored = await store.PersistCompletedAsync(new SnapshotPersistRequest
        {
            DeviceId = new DeviceId(device.Id),
            RequestedBy = requestedBy,
            IdempotencyKey = idempotencyKey,
            Capture = capture,
            CapturedAtUtc = DateTimeOffset.UtcNow,
        });

        Assert.Equal(1, await db.CaptureOperations.CountAsync());
        Assert.Equal(1, await db.SnapshotCaptures.CountAsync());
        Assert.True(await db.SnapshotPayloads.CountAsync() >= 2);
        Assert.Equal(1, await db.SnapshotCaptureSections.CountAsync());

        DeviceEntity reloaded = await db.Devices.SingleAsync(d => d.Id == device.Id);
        Assert.Equal(stored.Metadata.Id.Value, reloaded.LastCompletedCaptureId);

        StoredSnapshot? byIdempotency = await store.FindByIdempotencyAsync(requestedBy, idempotencyKey);
        Assert.NotNull(byIdempotency);
        Assert.Equal(stored.Metadata.Id, byIdempotency.Metadata.Id);

        StoredSnapshotPayload? payload = await store.GetPayloadAsync(stored.ConfigurationPayloadHash!);
        Assert.NotNull(payload);
        Assert.Equal(Encoding.UTF8.GetString(body), Encoding.UTF8.GetString(payload.UncompressedBytes.Span));
    }

    [Fact]
    public async Task CompletedCaptureAndSectionsAreImmutable()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        ISnapshotStore store = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();

        DeviceEntity device = await SeedDeviceAsync(db, "IMM01", "10.10.0.3");
        byte[] body = Encoding.UTF8.GetBytes("""{"imm":true}""");
        StoredSnapshot stored = await store.PersistCompletedAsync(
            NewPersistRequest(
                device.Id,
                CreateCapture(
                    body,
                    digestSeed: 0x77,
                    sections:
                    [
                        new CapturedSectionDescriptor
                        {
                            SectionId = "system",
                            SectionVersion = 1,
                            Status = 1,
                            Ordered = false,
                            ConfigurationPayload = body,
                        },
                    ])));

        db.ChangeTracker.Clear();
        SnapshotCaptureEntity capture = await db.SnapshotCaptures.SingleAsync(c => c.Id == stored.Metadata.Id.Value);
        capture.ErrorCode = "tamper";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        SnapshotCaptureEntity toDelete = await db.SnapshotCaptures.SingleAsync(c => c.Id == stored.Metadata.Id.Value);
        db.SnapshotCaptures.Remove(toDelete);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        SnapshotCaptureSectionEntity section = await db.SnapshotCaptureSections.SingleAsync();
        section.Status = 99;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        SnapshotCaptureSectionEntity sectionDelete = await db.SnapshotCaptureSections.SingleAsync();
        db.SnapshotCaptureSections.Remove(sectionDelete);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SnapshotHashIndexesExistInPostgreSQL()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = 'snapshot_captures'
              AND indexname = ANY(ARRAY[
                'IX_snapshot_captures_ConfigurationHash',
                'IX_snapshot_captures_ObservationHash',
                'IX_snapshot_captures_SnapshotHash'
              ])
            ORDER BY indexname;
            """;
        List<string> indexes = [];
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        Assert.Equal(
            [
                "IX_snapshot_captures_ConfigurationHash",
                "IX_snapshot_captures_ObservationHash",
                "IX_snapshot_captures_SnapshotHash",
            ],
            indexes);
    }

    [Fact]
    public async Task ListByDevicePageAsyncSupportsBase64UrlCursor()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        ISnapshotStore store = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();

        DeviceEntity device = await SeedDeviceAsync(db, "PAGE01", "10.10.0.4");
        DateTimeOffset t0 = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        for (int i = 0; i < 3; i++)
        {
            byte[] body = Encoding.UTF8.GetBytes($"{{\"n\":{i}}}");
            await store.PersistCompletedAsync(new SnapshotPersistRequest
            {
                DeviceId = new DeviceId(device.Id),
                RequestedBy = Guid.NewGuid(),
                IdempotencyKey = Guid.NewGuid(),
                Capture = CreateCapture(body, digestSeed: (byte)(0x80 + i)),
                CapturedAtUtc = t0.AddMinutes(i),
            });
        }

        StoredSnapshotPage page1 = await store.ListByDevicePageAsync(new DeviceId(device.Id), limit: 2, cursor: null);
        Assert.Equal(2, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);
        Assert.True(page1.Items[0].Metadata.CompletedAtUtc >= page1.Items[1].Metadata.CompletedAtUtc);

        StoredSnapshotPage page2 = await store.ListByDevicePageAsync(
            new DeviceId(device.Id),
            limit: 2,
            cursor: page1.NextCursor);
        Assert.Single(page2.Items);
        Assert.Null(page2.NextCursor);
        Assert.DoesNotContain(page2.Items, s => page1.Items.Any(p => p.Metadata.Id == s.Metadata.Id));
    }

    [Fact]
    public async Task SchemaMetadataIncludesSnapshotPersistM123()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        SchemaMetadataEntity? meta = await db.SchemaMetadata.FindAsync(
            SchemaMetadataEntitySeed.SnapshotPersistSchemaKey);
        Assert.NotNull(meta);
        Assert.Equal(SchemaMetadataEntitySeed.SnapshotPersistSchemaValue, meta.Value);

        SchemaMetadataEntity? inventory = await db.SchemaMetadata.FindAsync(
            SchemaMetadataEntitySeed.InventorySnapshotSchemaKey);
        Assert.NotNull(inventory);
        Assert.Equal(SchemaMetadataEntitySeed.InventorySnapshotSchemaValue, inventory.Value);
    }

    private static SnapshotPersistRequest NewPersistRequest(
        Guid deviceId,
        SnapshotCaptureResult capture,
        Guid? idempotencyKey = null)
        => new()
        {
            DeviceId = new DeviceId(deviceId),
            RequestedBy = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            IdempotencyKey = idempotencyKey ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Capture = capture,
            CapturedAtUtc = DateTimeOffset.UtcNow,
        };

    private static SnapshotCaptureResult CreateCapture(
        byte[] body,
        byte digestSeed,
        IReadOnlyList<CapturedSectionDescriptor>? sections = null)
    {
        byte[] digest = Enumerable.Repeat(digestSeed, 32).ToArray();
        Hash256 hash = Hash256.Create(digest);
        return new SnapshotCaptureResult
        {
            ConfigurationHash = ConfigurationHash.FromDigest(hash),
            ObservationHash = ObservationHash.FromDigest(hash),
            CapabilityHash = CapabilityHash.FromDigest(hash),
            SnapshotHash = SnapshotHash.FromDigest(hash),
            SchemaVersion = 1,
            RawPayload = body,
            ConfigurationPayload = body,
            ObservationPayload = body,
            CapabilityPayload = body,
            Sections = sections ?? [],
        };
    }

    private static async Task<DeviceEntity> SeedDeviceAsync(MfcDbContext db, string siteCode, string host)
    {
        SiteEntity site = new()
        {
            Id = Guid.NewGuid(),
            Code = siteCode,
            Name = siteCode + "-name",
            Status = 0,
            RowVersion = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        NodeEntity node = new()
        {
            Id = Guid.NewGuid(),
            SiteId = site.Id,
            Name = "n1",
            DeclaredKind = 0,
            DeclaredUplinkMode = 1,
            Status = 0,
            RowVersion = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        DeviceEntity device = new()
        {
            Id = Guid.NewGuid(),
            NodeId = node.Id,
            DisplayName = host,
            ManagementHost = host,
            ManagementHostKind = 0,
            ManagementPort = 8729,
            Enabled = true,
            RowVersion = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        db.Sites.Add(site);
        db.Nodes.Add(node);
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return device;
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
