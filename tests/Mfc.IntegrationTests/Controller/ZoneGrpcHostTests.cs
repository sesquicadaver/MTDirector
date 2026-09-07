using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ProtoDevice = Mfc.Contracts.Mfc.V1.Device;
using ProtoNode = Mfc.Contracts.Mfc.V1.Node;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoSite = Mfc.Contracts.Mfc.V1.Site;

namespace Mfc.IntegrationTests.Controller;

/// <summary>CT-ZONE-01: ZoneService GrpcHost contract (definitions, bindings, resolve, row-version CAS).</summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class ZoneGrpcHostTests
{
    private readonly PostgresFixture _postgres;

    public ZoneGrpcHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task ZoneDefinitionBindingResolveAndRowVersionCas()
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
            ZoneService.ZoneServiceClient zones = new(channel);
            Metadata headers = ActorHeaders("tester");
            (Uuid nodeId, Uuid deviceId) = await SeedRouterAsync(inventory, headers);

            ZoneDefinition created = await zones.CreateZoneDefinitionAsync(
                new CreateZoneDefinitionRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    OwnerScope = PolicyOwnerScope.Company,
                    Key = "lan",
                    Name = "LAN",
                    Description = "company lan",
                },
                headers,
                deadline: Deadline());
            Assert.NotEqual(0UL, created.RowVersion);
            Assert.Equal(PolicyOwnerScope.Company, created.OwnerScope);

            ListZoneDefinitionsResponse listed = await zones.ListZoneDefinitionsAsync(
                new ListZoneDefinitionsRequest(),
                headers,
                deadline: Deadline());
            Assert.Contains(listed.Zones, z => z.Id.Equals(created.Id));

            ZoneDefinition updated = await zones.UpdateZoneDefinitionAsync(
                new UpdateZoneDefinitionRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    ZoneId = created.Id,
                    ExpectedRowVersion = created.RowVersion,
                    Name = "LAN-core",
                },
                headers,
                deadline: Deadline());
            Assert.Equal("LAN-core", updated.Name);
            Assert.True(updated.RowVersion > created.RowVersion);

            RpcException stale = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await zones.UpdateZoneDefinitionAsync(
                    new UpdateZoneDefinitionRequest
                    {
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                        ZoneId = created.Id,
                        ExpectedRowVersion = created.RowVersion,
                        Name = "stale",
                    },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.Aborted, stale.StatusCode);

            NodeZoneBinding binding = await zones.UpsertNodeZoneBindingAsync(
                new UpsertNodeZoneBindingRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    NodeId = nodeId,
                    ZoneId = updated.Id,
                    Kind = NodeZoneBindingKind.SingleInterface,
                    Values = { "ether1" },
                },
                headers,
                deadline: Deadline());
            Assert.Equal(NodeZoneBindingKind.SingleInterface, binding.Kind);
            Assert.Contains("ether1", binding.Values);

            ListNodeZoneBindingsResponse bindings = await zones.ListNodeZoneBindingsAsync(
                new ListNodeZoneBindingsRequest { NodeId = nodeId },
                headers,
                deadline: Deadline());
            Assert.Single(bindings.Bindings);

            ZoneResolveBatch deviceResolve = await zones.ResolveZonesForDeviceAsync(
                new ResolveZonesForDeviceRequest { DeviceId = deviceId },
                headers,
                deadline: Deadline());
            Assert.NotEmpty(deviceResolve.Results);
            Assert.Contains(
                deviceResolve.Results.SelectMany(static r => r.Blockers),
                static b => b.Code == Mfc.Domain.Policy.ZoneResolveBlockerCodes.ObservationUnavailable);

            ZoneResolveBatch nodeResolve = await zones.ResolveZonesForNodeAsync(
                new ResolveZonesForNodeRequest { NodeId = nodeId },
                headers,
                deadline: Deadline());
            Assert.NotEmpty(nodeResolve.Results);

            ListNodeZoneBindingsResponse bindingsAfterResolve = await zones.ListNodeZoneBindingsAsync(
                new ListNodeZoneBindingsRequest { NodeId = nodeId },
                headers,
                deadline: Deadline());
            NodeZoneBinding currentBinding = Assert.Single(bindingsAfterResolve.Bindings);

            await zones.DeleteNodeZoneBindingAsync(
                new DeleteNodeZoneBindingRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    BindingId = currentBinding.Id,
                    ExpectedRowVersion = currentBinding.RowVersion,
                },
                headers,
                deadline: Deadline());

            DeleteZoneDefinitionResponse deleted = await zones.DeleteZoneDefinitionAsync(
                new DeleteZoneDefinitionRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    ZoneId = updated.Id,
                    ExpectedRowVersion = updated.RowVersion,
                },
                headers,
                deadline: Deadline());
            Assert.True(deleted.Deleted);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task CreateZoneAndUpsertAreIdempotent()
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
            ZoneService.ZoneServiceClient zones = new(channel);
            Metadata headers = ActorHeaders("tester");
            (Uuid nodeId, _) = await SeedRouterAsync(inventory, headers);

            Guid zoneKey = Guid.NewGuid();
            CreateZoneDefinitionRequest create = new()
            {
                IdempotencyKey = ProtoUuid.FromGuid(zoneKey),
                OwnerScope = PolicyOwnerScope.Company,
                Key = "wan",
                Name = "WAN",
            };
            ZoneDefinition first = await zones.CreateZoneDefinitionAsync(create, headers, deadline: Deadline());
            ZoneDefinition replayed = await zones.CreateZoneDefinitionAsync(create, headers, deadline: Deadline());
            Assert.Equal(first.Id, replayed.Id);

            Guid bindKey = Guid.NewGuid();
            UpsertNodeZoneBindingRequest upsert = new()
            {
                IdempotencyKey = ProtoUuid.FromGuid(bindKey),
                NodeId = nodeId,
                ZoneId = first.Id,
                Kind = NodeZoneBindingKind.SingleInterface,
                Values = { "wan1" },
            };
            NodeZoneBinding binding = await zones.UpsertNodeZoneBindingAsync(upsert, headers, deadline: Deadline());
            NodeZoneBinding bindingAgain = await zones.UpsertNodeZoneBindingAsync(upsert, headers, deadline: Deadline());
            Assert.Equal(binding.Id, bindingAgain.Id);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    private static async Task<(Uuid NodeId, Uuid DeviceId)> SeedRouterAsync(
        InventoryService.InventoryServiceClient inventory,
        Metadata headers)
    {
        ProtoSite site = await inventory.CreateSiteAsync(
            new CreateSiteRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                Code = "ZN1",
                Name = "Zones",
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
                ManagementHost = "192.0.2.60",
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
