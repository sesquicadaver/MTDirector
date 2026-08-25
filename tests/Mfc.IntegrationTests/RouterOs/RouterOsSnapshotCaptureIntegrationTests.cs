using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Mfc.RouterOs.Ports;
using Mfc.RouterOs.Snapshot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using ProtoDeclaredUplinkMode = Mfc.Contracts.Mfc.V1.DeclaredUplinkMode;
using ProtoDeviceRole = Mfc.Contracts.Mfc.V1.DeviceRole;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoTrust = Mfc.Contracts.Mfc.V1.CertificateTrustMode;

namespace Mfc.IntegrationTests.RouterOs;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class RouterOsSnapshotCaptureIntegrationTests
{
    private readonly PostgresFixture _postgres;

    public RouterOsSnapshotCaptureIntegrationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task StartCapturePersistsProductionCapturePortResult()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        RouterOsDiscoveryDataset dataset = RouterOsCaptureIntegrationFixtures.MinimalDataset();

        await using var app = Program.BuildHost(
            DevArgs(url, connectionString),
            builder =>
            {
                builder.Services.RemoveAll<ISnapshotCapturePort>();
                builder.Services.RemoveAll<IRouterOsStableReadAttemptFactoryProvider>();
                builder.Services.AddSingleton<IRouterOsStableReadAttemptFactoryProvider>(
                    new FixtureStableReadAttemptFactoryProvider(dataset));
                builder.Services.AddSingleton<ISnapshotCapturePort, RouterOsSnapshotCapturePort>();
            });

        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient inventory = new(channel);
            SnapshotService.SnapshotServiceClient snapshots = new(channel);
            Metadata headers = ActorHeaders("tester");

            Device device = await SeedDeviceWithConnectionAsync(inventory, headers, "192.0.2.50");
            Guid idempotencyKey = Guid.NewGuid();
            StartCaptureResponse started = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = device.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(idempotencyKey),
                },
                headers,
                deadline: Deadline());
            Assert.False(started.Deduplicated);
            Assert.NotNull(started.CaptureId);

            using CancellationTokenSource watchCts = new(TimeSpan.FromSeconds(10));
            bool completed = false;
            await foreach (CaptureProgress item in snapshots.WatchCapture(
                               new WatchCaptureRequest { OperationId = started.OperationId },
                               headers,
                               cancellationToken: watchCts.Token)
                           .ResponseStream.ReadAllAsync(watchCts.Token))
            {
                if (item.Stage == CaptureStage.Completed)
                {
                    completed = true;
                    break;
                }
            }

            Assert.True(completed);

            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
            Assert.Equal(1, await db.CaptureOperations.CountAsync());
            Assert.Equal(1, await db.SnapshotCaptures.CountAsync());
            Assert.True(await db.SnapshotPayloads.CountAsync() >= 2);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task<Device> SeedDeviceWithConnectionAsync(
        InventoryService.InventoryServiceClient inventory,
        Metadata headers,
        string host)
    {
        Site site = await inventory.CreateSiteAsync(
            new CreateSiteRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                Code = "P205",
                Name = "P2-05 Lab",
            },
            headers,
            deadline: Deadline());
        Node node = await inventory.CreateNodeAsync(
            new CreateNodeRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                SiteId = site.Id,
                Name = "edge",
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
                DisplayName = "edge",
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
                Username = "admin",
                PasswordUtf8 = ByteString.CopyFrom(Encoding.UTF8.GetBytes("lab-password")),
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

    private sealed class FixtureStableReadAttemptFactoryProvider(RouterOsDiscoveryDataset dataset)
        : IRouterOsStableReadAttemptFactoryProvider
    {
        public IStableReadAttemptFactory<RouterOsDiscoveryDataset> Create(RouterOsReadTarget target)
            => new FixtureStableReadAttemptFactory(dataset);
    }

    private sealed class FixtureStableReadAttemptFactory(RouterOsDiscoveryDataset dataset)
        : IStableReadAttemptFactory<RouterOsDiscoveryDataset>
    {
        public Task<IStableReadAttemptSession<RouterOsDiscoveryDataset>> OpenAsync(CancellationToken cancellationToken)
            => Task.FromResult<IStableReadAttemptSession<RouterOsDiscoveryDataset>>(new FixtureSession(dataset));
    }

    private sealed class FixtureSession(RouterOsDiscoveryDataset dataset) : IStableReadAttemptSession<RouterOsDiscoveryDataset>
    {
        private static readonly ConfigurationFingerprintSet Fingerprints = BuildFingerprints();

        public Task<ConfigurationFingerprintSet> ReadConfigurationFingerprintsAsync(
            StableReadExecutionContext context,
            CancellationToken cancellationToken)
            => Task.FromResult(Fingerprints);

        public Task<RouterOsDiscoveryDataset> ReadCompleteDiscoveryDatasetAsync(
            StableReadExecutionContext context,
            CancellationToken cancellationToken)
            => Task.FromResult(dataset);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static ConfigurationFingerprintSet BuildFingerprints()
        {
            List<MenuFingerprint> menus = [];
            foreach (CriticalConfigurationMenu menu in CriticalConfigurationMenus.All)
            {
                menus.Add(new MenuFingerprint
                {
                    Menu = menu,
                    Digest = Hash256.Create(new byte[Hash256.Size]),
                    Available = menu != CriticalConfigurationMenu.ManagedAnchors,
                });
            }

            return new ConfigurationFingerprintSet(menus);
        }
    }

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

    private static Metadata ActorHeaders(string actor) => new()
    {
        { SnapshotGrpcService.ActorMetadataKey, actor },
    };

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(30);

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
