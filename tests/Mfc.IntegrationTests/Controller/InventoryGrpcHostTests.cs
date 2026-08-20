using System.Text;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Authorization;
using Mfc.Controller.Grpc;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using DomainSupportState = Mfc.Domain.Inventory.SupportState;
using ProtoDeclaredUplinkMode = Mfc.Contracts.Mfc.V1.DeclaredUplinkMode;
using ProtoDeviceRole = Mfc.Contracts.Mfc.V1.DeviceRole;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoSupportState = Mfc.Contracts.Mfc.V1.SupportState;
using ProtoTrust = Mfc.Contracts.Mfc.V1.CertificateTrustMode;

namespace Mfc.IntegrationTests.Controller;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class InventoryGrpcHostTests
{
    private readonly PostgresFixture _postgres;

    public InventoryGrpcHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task InventoryLifecycleListGetRegisterValidateAndIdempotency()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        CountingProbePort probe = new();

        await using var app = Program.BuildHost(
            DevArgs(url, connectionString),
            builder =>
            {
                builder.Services.RemoveAll<IRouterOsReadPort>();
                builder.Services.AddSingleton<IRouterOsReadPort>(probe);
            });

        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient client = new(channel);
            Metadata headers = ActorHeaders("tester");

            Guid siteKey = Guid.NewGuid();
            Site site = await client.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(siteKey),
                    Code = "LAB25",
                    Name = "Lab M1-25",
                },
                headers,
                deadline: Deadline());
            Assert.Equal("LAB25", site.Code);

            Site replaySite = await client.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(siteKey),
                    Code = "LAB25",
                    Name = "Lab M1-25",
                },
                headers,
                deadline: Deadline());
            Assert.Equal(site.Id, replaySite.Id);

            ListSitesResponse listed = await client.ListSitesAsync(
                new ListSitesRequest { Page = new PageRequest { PageSize = 50 } },
                headers,
                deadline: Deadline());
            Assert.Contains(listed.Sites, s => s.Id.Equals(site.Id));

            Node node = await client.CreateNodeAsync(
                new CreateNodeRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    SiteId = site.Id,
                    Name = "core",
                    DeclaredKind = ProtoNodeKind.Router,
                    DeclaredUplinkMode = ProtoDeclaredUplinkMode.One,
                },
                headers,
                deadline: Deadline());

            ListNodesResponse listedNodes = await client.ListNodesAsync(
                new ListNodesRequest
                {
                    SiteId = site.Id,
                    Page = new PageRequest { PageSize = 50 },
                },
                headers,
                deadline: Deadline());
            Assert.Contains(listedNodes.Nodes, n => n.Id.Equals(node.Id));

            Device device = await client.RegisterDeviceAsync(
                new RegisterDeviceRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    NodeId = node.Id,
                    DisplayName = "core",
                    ManagementHost = "192.0.2.25",
                    ManagementPort = 8729,
                    Role = ProtoDeviceRole.Router,
                },
                headers,
                deadline: Deadline());

            NodeDetails details = await client.GetNodeAsync(
                new GetNodeRequest { NodeId = node.Id },
                headers,
                deadline: Deadline());
            Assert.Equal(node.Id, details.Node.Id);
            Assert.Single(details.Devices);
            Assert.Equal(device.Id, details.Devices[0].Id);
            Assert.Equal("Unknown", details.Devices[0].Reachability);
            Assert.False(details.Devices[0].HasRouterosVersion);
            Assert.False(details.Devices[0].HasModel);
            Assert.Empty(details.Devices[0].VrrpRoleLabels);
            Assert.Null(details.Devices[0].LastSnapshotAt);

            byte[] password = Encoding.UTF8.GetBytes("super-secret-password");
            DeviceConnectionSummary summary = await client.UpdateDeviceConnectionAsync(
                new UpdateDeviceConnectionRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    DeviceId = device.Id,
                    Username = "readonly",
                    PasswordUtf8 = Google.Protobuf.ByteString.CopyFrom(password),
                    TrustMode = ProtoTrust.InternalCa,
                    CaProfileRef = "lab-ca",
                    ConnectTimeoutMs = 5000,
                    CommandTimeoutMs = 30_000,
                    MaxResponseBytes = 1_048_576,
                },
                headers,
                deadline: Deadline());

            Assert.Equal("readonly", summary.Username);
            string summaryText = summary.ToString();
            Assert.DoesNotContain("super-secret-password", summaryText, StringComparison.Ordinal);
            Assert.DoesNotContain("password", summaryText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ciphertext", summaryText, StringComparison.OrdinalIgnoreCase);

            ValidateDeviceConnectionResponse validated = await client.ValidateDeviceConnectionAsync(
                new ValidateDeviceConnectionRequest { DeviceId = device.Id },
                headers,
                deadline: Deadline());
            Assert.Equal(device.Id, validated.DeviceId);
            Assert.False(validated.RouterosMutated);
            Assert.Equal(ProtoSupportState.Supported, validated.SupportState);
            Assert.Equal(1, probe.ProbeCount);

            // Concurrent validate for the same device coalesces to one probe.
            probe.Delay = TimeSpan.FromMilliseconds(250);
            Task<ValidateDeviceConnectionResponse> a = client.ValidateDeviceConnectionAsync(
                new ValidateDeviceConnectionRequest { DeviceId = device.Id },
                headers,
                deadline: Deadline()).ResponseAsync;
            Task<ValidateDeviceConnectionResponse> b = client.ValidateDeviceConnectionAsync(
                new ValidateDeviceConnectionRequest { DeviceId = device.Id },
                headers,
                deadline: Deadline()).ResponseAsync;
            await Task.WhenAll(a, b);
            Assert.Equal(2, probe.ProbeCount); // one from earlier + one coalesced pair
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task InventoryMutationsAreForbiddenWithoutPermission()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(
            DevArgs(url, connectionString),
            builder =>
            {
                builder.Services.RemoveAll<IAuthorizationBoundary>();
                builder.Services.AddSingleton<IAuthorizationBoundary, DenyAllAuthorizationBoundary>();
            });

        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient client = new(channel);

            RpcException ex = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await client.CreateSiteAsync(
                    new CreateSiteRequest
                    {
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                        Code = "DENY1",
                        Name = "Denied",
                    },
                    ActorHeaders("guest"),
                    deadline: Deadline());
            });

            Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
            Assert.Contains(ex.Trailers, e => e.Key == GrpcApplicationErrorMapper.ErrorDetailMetadataKey);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
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
        { InventoryGrpcService.ActorMetadataKey, actor },
    };

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(15);

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

    private sealed class CountingProbePort : IRouterOsReadPort
    {
        private int _probeCount;

        public int ProbeCount => Volatile.Read(ref _probeCount);

        public TimeSpan Delay { get; set; }

        public async Task<RouterOsProbeResult> ProbeAsync(
            RouterOsReadTarget target,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _probeCount);
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
            }

            return new RouterOsProbeResult
            {
                Identity = "chr-lab-25",
                SupportState = DomainSupportState.Supported,
            };
        }
    }
}
