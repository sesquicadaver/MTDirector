using System.Security.Cryptography;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Drift;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainDeviceId = Mfc.Domain.Inventory.Primitives.DeviceId;
using DomainFindingKind = Mfc.Domain.Drift.DriftFindingKind;
using ProtoDevice = Mfc.Contracts.Mfc.V1.Device;
using ProtoNode = Mfc.Contracts.Mfc.V1.Node;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoSite = Mfc.Contracts.Mfc.V1.Site;

namespace Mfc.IntegrationTests.Controller;

/// <summary>CT-DRIFT-01: DriftService GrpcHost contract (ListDeviceDriftEvents / GetDriftEvent, read-only).</summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class DriftGrpcHostTests
{
    private readonly PostgresFixture _postgres;

    public DriftGrpcHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task ListAndGetDriftEventsAfterDetect()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient inventory = new(channel);
            DriftService.DriftServiceClient drift = new(channel);
            Metadata headers = ActorHeaders("tester");
            (Uuid nodeId, Uuid deviceId) = await SeedRouterAsync(inventory, headers);

            ListDeviceDriftEventsResponse empty = await drift.ListDeviceDriftEventsAsync(
                new ListDeviceDriftEventsRequest { DeviceId = deviceId },
                headers,
                deadline: Deadline());
            Assert.Empty(empty.Events);

            RpcException missing = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await drift.GetDriftEventAsync(
                    new GetDriftEventRequest { DriftEventId = ProtoUuid.FromGuid(Guid.NewGuid()) },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.NotFound, missing.StatusCode);

            await SeedHashStateAndDetectAsync(app.Services, ProtoUuid.ToGuid(deviceId));

            ListDeviceDriftEventsResponse listed = await drift.ListDeviceDriftEventsAsync(
                new ListDeviceDriftEventsRequest { DeviceId = deviceId },
                headers,
                deadline: Deadline());
            Assert.Single(listed.Events);
            global::Mfc.Contracts.Mfc.V1.DriftEvent listedEvent = listed.Events[0];
            Assert.True(listedEvent.Immutable);
            Assert.Equal(DriftOutcome.CriticalDrift, listedEvent.Outcome);
            Assert.True(listedEvent.BlocksDeployment);
            Assert.Equal(deviceId, listedEvent.DeviceId);
            Assert.Equal(nodeId, listedEvent.NodeId);

            global::Mfc.Contracts.Mfc.V1.DriftEvent loaded = await drift.GetDriftEventAsync(
                new GetDriftEventRequest { DriftEventId = listedEvent.Id },
                headers,
                deadline: Deadline());
            Assert.Equal(listedEvent.Id, loaded.Id);
            Assert.Equal(listedEvent.SemanticDiffCanonical, loaded.SemanticDiffCanonical);
            Assert.True(loaded.Immutable);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public void DriftServiceHasNoMutationRpcsOnWire()
    {
        // Host contract mirrors proto surface: only List + Get (no ForceRepair).
        string[] methods = DriftService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["GetDriftEvent", "ListDeviceDriftEvents"], methods);
    }

    private static async Task SeedHashStateAndDetectAsync(IServiceProvider services, Guid deviceId)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IDeviceHashStateStore hashStates = scope.ServiceProvider.GetRequiredService<IDeviceHashStateStore>();
        DetectManagedDriftUseCase detect = scope.ServiceProvider.GetRequiredService<DetectManagedDriftUseCase>();

        Hash256 committed = Hash256.Create(SHA256.HashData("committed-baseline"u8));
        await hashStates.UpsertAsync(
            DeviceHashState.Create(
                new DomainDeviceId(deviceId),
                desiredPolicyHash: committed,
                desiredArtifactHash: committed,
                lastCommittedPolicyHash: committed,
                lastCommittedArtifactHash: committed,
                actualManagedResourceHash: committed,
                actualKnown: true,
                anchorKnown: true,
                updatedAtUtc: DateTimeOffset.UtcNow));

        Hash256 drifted = Hash256.Create(SHA256.HashData("actual-drifted"u8));
        ApplicationResult<Mfc.Application.Models.DriftEventView> result = await detect.ExecuteAsync(
            new DetectManagedDriftCommand
            {
                Actor = "tester",
                DeviceId = deviceId,
                ActualManagedResourceHashHex = drifted.ToString(),
                Findings =
                [
                    new DriftFindingInput { Kind = DomainFindingKind.AnchorTargetChanged, Detail = "jump changed" },
                ],
                SemanticDiffCanonical = """{"entries":[{"section":"filter","change":"modified"}]}""",
                PersistActualHash = true,
            });
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    private static async Task<(Uuid NodeId, Uuid DeviceId)> SeedRouterAsync(
        InventoryService.InventoryServiceClient inventory,
        Metadata headers)
    {
        ProtoSite site = await inventory.CreateSiteAsync(
            new CreateSiteRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                Code = "DR1",
                Name = "Drift",
            },
            headers,
            deadline: Deadline());
        ProtoNode node = await inventory.CreateNodeAsync(
            new CreateNodeRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                SiteId = site.Id,
                Name = "edge",
                DeclaredKind = ProtoNodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            },
            headers,
            deadline: Deadline());
        ProtoDevice device = await inventory.RegisterDeviceAsync(
            new RegisterDeviceRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                NodeId = node.Id,
                DisplayName = "edge",
                ManagementHost = "192.0.2.70",
                ManagementPort = 8729,
                Role = DeviceRole.Router,
            },
            headers,
            deadline: Deadline());
        return (node.Id, device.Id);
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
        { InventoryGrpcService.ActorMetadataKey, actor },
    };

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(60);

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
