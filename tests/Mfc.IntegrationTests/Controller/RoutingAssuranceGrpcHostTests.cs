using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Common;
using Mfc.Application.Routing;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Domain.Routing;
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

/// <summary>CT-ROUTING-01: RoutingAssuranceService GrpcHost contract (GetDeviceRoutingAssuranceState, read-only).</summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class RoutingAssuranceGrpcHostTests
{
    private readonly PostgresFixture _postgres;

    public RoutingAssuranceGrpcHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task GetDeviceRoutingAssuranceStateAfterUpsert()
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
            RoutingAssuranceService.RoutingAssuranceServiceClient routing = new(channel);
            Metadata headers = ActorHeaders("tester");
            (_, Uuid deviceId) = await SeedRouterAsync(inventory, headers);

            RpcException missing = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await routing.GetDeviceRoutingAssuranceStateAsync(
                    new GetDeviceRoutingAssuranceStateRequest { DeviceId = deviceId },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.NotFound, missing.StatusCode);

            await SeedRoutingStateAsync(app.Services, ProtoUuid.ToGuid(deviceId));

            RoutingAssuranceStateDetail detail = await routing.GetDeviceRoutingAssuranceStateAsync(
                new GetDeviceRoutingAssuranceStateRequest { DeviceId = deviceId },
                headers,
                deadline: Deadline());
            Assert.Equal(deviceId, detail.DeviceId);
            Assert.NotNull(detail.ConfigurationHash);
            Assert.NotNull(detail.OperationalHash);
            Assert.True(detail.RowVersion > 0);
            Assert.Equal(1u, detail.ConfigurationTableCount);
            Assert.Equal(1u, detail.ConfigurationVrfCount);
            Assert.Equal(1u, detail.ConfigurationStaticRouteCount);
            Assert.Equal(1u, detail.ConfigurationFilterRuleCount);
            Assert.NotNull(detail.UpdatedAt);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public void RoutingAssuranceServiceHasNoMutationRpcsOnWire()
    {
        string[] methods = RoutingAssuranceService.Descriptor.Methods
            .Select(static m => m.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Equal(["GetDeviceRoutingAssuranceState"], methods);
    }

    private static async Task SeedRoutingStateAsync(IServiceProvider services, Guid deviceId)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        UpsertRoutingAssuranceStateUseCase upsert =
            scope.ServiceProvider.GetRequiredService<UpsertRoutingAssuranceStateUseCase>();
        GetRoutingAssuranceStateUseCase get =
            scope.ServiceProvider.GetRequiredService<GetRoutingAssuranceStateUseCase>();

        Dictionary<string, string> material = new(StringComparer.Ordinal)
        {
            ["rtab.main.fib"] = "yes",
            ["rsettings.policy-rules"] = "lookup",
            ["vrf.corp.interfaces"] = "vlan10",
            ["route.4:main:0.0.0.0/0:1.1.1.1.distance"] = "1",
            ["filter.0.rule"] = "accept",
        };
        RoutingConfigurationSnapshot configuration = new(
            [new RoutingTableFact { Name = "main", Fib = "yes", Disabled = "false" }],
            new RoutingSettingsFact
            {
                PolicyRules = "lookup",
                CheckGatewayPingCount = null,
                CheckGatewayPingInterval = null,
                CheckGatewayPingTimeout = null,
                ConnectedInChain = null,
                DynamicInChain = null,
                SingleProcess = "yes",
            },
            [],
            [new VrfDefinitionFact { Name = "corp", Interfaces = "vlan10", Disabled = "false" }],
            [
                new StaticRouteConfigFact
                {
                    Family = "ipv4",
                    DstAddress = "0.0.0.0/0",
                    Gateway = "1.1.1.1",
                    RoutingTable = "main",
                    Distance = 1,
                    Scope = 30,
                    TargetScope = 10,
                    PrefSrc = null,
                    CheckGateway = null,
                    Disabled = "false",
                },
            ],
            [new RouteFilterRuleFact { EffectiveOrdinal = 0, Chain = "bgp-in", Rule = "accept", Disabled = "false" }],
            [],
            material);

        ApplicationResult<Mfc.Application.Models.RoutingAssuranceStateView> result = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = deviceId,
                Configuration = configuration,
                OperationalState = RoutingOperationalSnapshot.Empty,
            });
        Assert.True(result.IsSuccess, result.Error?.Message);

        ApplicationResult<Mfc.Application.Models.RoutingAssuranceDetailView> loaded = await get.ExecuteAsync(
            new GetRoutingAssuranceStateQuery { Actor = "tester", DeviceId = deviceId });
        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        Assert.Equal(1, loaded.Value!.ConfigurationTableCount);
    }

    private static async Task<(Uuid NodeId, Uuid DeviceId)> SeedRouterAsync(
        InventoryService.InventoryServiceClient inventory,
        Metadata headers)
    {
        ProtoSite site = await inventory.CreateSiteAsync(
            new CreateSiteRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                Code = "RA1",
                Name = "Routing",
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
                ManagementHost = "192.0.2.71",
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
