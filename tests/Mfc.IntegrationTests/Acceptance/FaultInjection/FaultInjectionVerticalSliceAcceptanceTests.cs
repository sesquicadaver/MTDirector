using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using ProtoDeclaredUplinkMode = Mfc.Contracts.Mfc.V1.DeclaredUplinkMode;
using ProtoDeviceRole = Mfc.Contracts.Mfc.V1.DeviceRole;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoTrust = Mfc.Contracts.Mfc.V1.CertificateTrustMode;

namespace Mfc.IntegrationTests.Acceptance.FaultInjection;

/// <summary>
/// M1-33 snapshot-path fault injection acceptance (in-process Controller + Postgres, no production network).
/// </summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class FaultInjectionVerticalSliceAcceptanceTests
{
    private readonly PostgresFixture _postgres;

    public FaultInjectionVerticalSliceAcceptanceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task FaultsDoNotPersistCompleteCapturesAndRecoverySucceeds()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        FaultInjectingSnapshotCapturePort capture = new();

        await using var app = Program.BuildHost(
            DevArgs(url, connectionString),
            builder =>
            {
                builder.Services.RemoveAll<ISnapshotCapturePort>();
                builder.Services.AddSingleton<ISnapshotCapturePort>(capture);
            });

        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient inventory = new(channel);
            SnapshotService.SnapshotServiceClient snapshots = new(channel);
            Metadata headers = ActorHeaders("fault-injection");

            Device device = await SeedDeviceAsync(inventory, headers, "FLT33", "10.255.99.10");

            // AC: unstable → typed code, no completed capture.
            capture.Mode = FaultInjectingSnapshotCapturePort.CaptureMode.Unstable;
            RpcException unstable = await Assert.ThrowsAsync<RpcException>(() =>
                snapshots.StartCaptureAsync(
                    new StartCaptureRequest
                    {
                        DeviceId = device.Id,
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    },
                    headers,
                    deadline: Deadline()).ResponseAsync);
            Assert.Equal(StatusCode.Aborted, unstable.StatusCode);
            Assert.Contains("SNAPSHOT_UNSTABLE", unstable.Status.Detail, StringComparison.Ordinal);

            // AC: oversized → typed code.
            capture.Mode = FaultInjectingSnapshotCapturePort.CaptureMode.Oversized;
            RpcException oversized = await Assert.ThrowsAsync<RpcException>(() =>
                snapshots.StartCaptureAsync(
                    new StartCaptureRequest
                    {
                        DeviceId = device.Id,
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    },
                    headers,
                    deadline: Deadline()).ResponseAsync);
            Assert.Equal(StatusCode.ResourceExhausted, oversized.StatusCode);
            Assert.Contains("SNAPSHOT_TOO_LARGE", oversized.Status.Detail, StringComparison.Ordinal);

            // AC: dependency fault → Unavailable, still no complete.
            capture.Mode = FaultInjectingSnapshotCapturePort.CaptureMode.DependencyFault;
            RpcException dependency = await Assert.ThrowsAsync<RpcException>(() =>
                snapshots.StartCaptureAsync(
                    new StartCaptureRequest
                    {
                        DeviceId = device.Id,
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    },
                    headers,
                    deadline: Deadline()).ResponseAsync);
            Assert.Equal(StatusCode.Unavailable, dependency.StatusCode);

            ListCapturesResponse afterFaults = await snapshots.ListCapturesAsync(
                new ListCapturesRequest
                {
                    DeviceId = device.Id,
                    Page = new PageRequest { PageSize = 50 },
                },
                headers,
                deadline: Deadline());
            Assert.Empty(afterFaults.Captures);

            // AC: controller cancellation mid-capture.
            capture.Mode = FaultInjectingSnapshotCapturePort.CaptureMode.HangUntilCancelled;
            using CancellationTokenSource cancelCapture = new(TimeSpan.FromMilliseconds(120));
            await Assert.ThrowsAnyAsync<Exception>(() =>
                snapshots.StartCaptureAsync(
                    new StartCaptureRequest
                    {
                        DeviceId = device.Id,
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    },
                    headers,
                    deadline: Deadline(),
                    cancellationToken: cancelCapture.Token).ResponseAsync);

            ListCapturesResponse afterCancel = await snapshots.ListCapturesAsync(
                new ListCapturesRequest
                {
                    DeviceId = device.Id,
                    Page = new PageRequest { PageSize = 50 },
                },
                headers,
                deadline: Deadline());
            Assert.Empty(afterCancel.Captures);

            // AC: recovery capture succeeds after faults.
            capture.Mode = FaultInjectingSnapshotCapturePort.CaptureMode.Succeed;
            capture.Note = "recovered";
            StartCaptureResponse recovered = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = device.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                },
                headers,
                deadline: Deadline());
            Assert.False(recovered.Deduplicated);
            Assert.NotNull(recovered.CaptureId);
            Assert.True(capture.SuccessCount >= 1);

            // AC: DB failure during persistence leaves no orphan captures/sections.
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
            ISnapshotStore store = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();
            Guid deviceGuid = ProtoUuid.ToGuid(device.Id);
            Guid requestedBy = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            Guid idempotencyKey = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
            db.CaptureOperations.Add(new CaptureOperationEntity
            {
                Id = Guid.NewGuid(),
                TargetType = 1,
                TargetId = deviceGuid,
                RequestedBy = requestedBy,
                IdempotencyKey = idempotencyKey,
                Status = 0,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            long capturesBefore = await db.SnapshotCaptures.LongCountAsync();
            long sectionsBefore = await db.SnapshotCaptureSections.LongCountAsync();
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                store.PersistCompletedAsync(new SnapshotPersistRequest
                {
                    DeviceId = new DeviceId(deviceGuid),
                    RequestedBy = requestedBy,
                    IdempotencyKey = idempotencyKey,
                    Capture = MinimalCapture(),
                    CapturedAtUtc = DateTimeOffset.UtcNow,
                }));
            Assert.Equal(capturesBefore, await db.SnapshotCaptures.LongCountAsync());
            Assert.Equal(sectionsBefore, await db.SnapshotCaptureSections.LongCountAsync());
        }
        finally
        {
            using CancellationTokenSource stop = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stop.Token);
        }
    }

    [Fact]
    public async Task CompletedCaptureSurvivesControllerRestart()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        FaultInjectingSnapshotCapturePort capture = new() { Mode = FaultInjectingSnapshotCapturePort.CaptureMode.Succeed };

        Uuid captureId;
        string snapshotHash;
        Guid deviceId;

        await using (var app = Program.BuildHost(
                   DevArgs(url, connectionString),
                   builder =>
                   {
                       builder.Services.RemoveAll<ISnapshotCapturePort>();
                       builder.Services.AddSingleton<ISnapshotCapturePort>(capture);
                   }))
        {
            await app.Services.MigrateAsync();
            await app.StartAsync();
            try
            {
                await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
                using GrpcChannel channel = GrpcChannel.ForAddress(url);
                InventoryService.InventoryServiceClient inventory = new(channel);
                SnapshotService.SnapshotServiceClient snapshots = new(channel);
                Metadata headers = ActorHeaders("fault-injection");
                Device device = await SeedDeviceAsync(inventory, headers, "RST33", "10.255.99.11");
                deviceId = ProtoUuid.ToGuid(device.Id);
                StartCaptureResponse started = await snapshots.StartCaptureAsync(
                    new StartCaptureRequest
                    {
                        DeviceId = device.Id,
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    },
                    headers,
                    deadline: Deadline());
                captureId = started.CaptureId!;
                SnapshotSummary summary = await snapshots.GetSnapshotSummaryAsync(
                    new GetSnapshotSummaryRequest { CaptureId = captureId },
                    headers,
                    deadline: Deadline());
                snapshotHash = Convert.ToHexString(summary.SnapshotHash.Value.Span);
            }
            finally
            {
                using CancellationTokenSource stop = new(TimeSpan.FromSeconds(5));
                await app.StopAsync(stop.Token);
            }
        }

        string url2 = $"http://127.0.0.1:{GetFreeTcpPort()}";
        await using var app2 = Program.BuildHost(DevArgs(url2, connectionString));
        await app2.StartAsync();
        try
        {
            await WaitForPortAsync(url2, TimeSpan.FromSeconds(10));
            using GrpcChannel channel2 = GrpcChannel.ForAddress(url2);
            SnapshotService.SnapshotServiceClient snapshots2 = new(channel2);
            Metadata headers2 = ActorHeaders("fault-injection");
            SnapshotSummary reloaded = await snapshots2.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = captureId },
                headers2,
                deadline: Deadline());
            Assert.Equal(SnapshotCaptureStatus.Completed, reloaded.Status);
            Assert.Equal(snapshotHash, Convert.ToHexString(reloaded.SnapshotHash.Value.Span));
            Assert.Equal(deviceId, ProtoUuid.ToGuid(reloaded.DeviceId));
        }
        finally
        {
            using CancellationTokenSource stop = new(TimeSpan.FromSeconds(5));
            await app2.StopAsync(stop.Token);
        }
    }

    private static SnapshotCaptureResult MinimalCapture()
    {
        byte[] body = Encoding.UTF8.GetBytes("""{"fault":"db"}""");
        byte[] digest = Enumerable.Repeat((byte)0x33, 32).ToArray();
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
            Sections = [],
        };
    }

    private static async Task<Device> SeedDeviceAsync(
        InventoryService.InventoryServiceClient inventory,
        Metadata headers,
        string siteCode,
        string host)
    {
        Site site = await inventory.CreateSiteAsync(
            new CreateSiteRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                Code = siteCode,
                Name = "Fault injection",
            },
            headers,
            deadline: Deadline());
        Node node = await inventory.CreateNodeAsync(
            new CreateNodeRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                SiteId = site.Id,
                Name = "fault-node",
                DeclaredKind = ProtoNodeKind.Router,
                DeclaredUplinkMode = ProtoDeclaredUplinkMode.One,
            },
            headers,
            deadline: Deadline());
        Device device = await inventory.RegisterDeviceAsync(
            new RegisterDeviceRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                NodeId = node.Id,
                DisplayName = "chr-fault",
                ManagementHost = host,
                ManagementPort = 8729,
                Role = ProtoDeviceRole.Router,
            },
            headers,
            deadline: Deadline());
        await inventory.UpdateDeviceConnectionAsync(
            new UpdateDeviceConnectionRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                DeviceId = device.Id,
                Username = "readonly",
                PasswordUtf8 = ByteString.CopyFrom(Encoding.UTF8.GetBytes("ephemeral-lab-secret")),
                TrustMode = ProtoTrust.InternalCa,
                CaProfileRef = "lab-ca",
                ConnectTimeoutMs = 5000,
                CommandTimeoutMs = 30_000,
                MaxResponseBytes = 1_048_576,
            },
            headers,
            deadline: Deadline());
        return device;
    }

    private static Metadata ActorHeaders(string actor) => new()
    {
        { SnapshotGrpcService.ActorMetadataKey, actor },
    };

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(45);

    private static string[] DevArgs(string url, string connectionString)
        =>
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
        ];

    private static int GetFreeTcpPort()
    {
        System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForPortAsync(string url, TimeSpan timeout)
    {
        Uri uri = new(url);
        using CancellationTokenSource delay = new(timeout);
        while (!delay.IsCancellationRequested)
        {
            try
            {
                using System.Net.Sockets.TcpClient client = new();
                await client.ConnectAsync(uri.Host, uri.Port, delay.Token);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await Task.Delay(50, delay.Token);
            }
        }

        throw new TimeoutException($"Timed out waiting for {url}");
    }
}
