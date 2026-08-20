using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using ProtoDeclaredUplinkMode = Mfc.Contracts.Mfc.V1.DeclaredUplinkMode;
using ProtoDeviceRole = Mfc.Contracts.Mfc.V1.DeviceRole;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoTrust = Mfc.Contracts.Mfc.V1.CertificateTrustMode;

namespace Mfc.IntegrationTests.Acceptance;

/// <summary>
/// M1-31 multi-WAN failover/balanced vertical-slice acceptance (in-process Controller + Postgres).
/// </summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class MultiWanVerticalSliceAcceptanceTests
{
    private static readonly string[] ExpectedSections =
    [
        "routing.tables",
        "routing.rules",
        "routing.ipv4.static-routes",
        "routing.ipv4.default-state",
        "firewall.ipv4.nat",
        "firewall.ipv4.mangle",
        "network.ipv4.settings",
        "topology.validation",
    ];

    private readonly PostgresFixture _postgres;

    public MultiWanVerticalSliceAcceptanceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task MultiWanFailoverAndBalancedDiscoveryHashesAndDiff()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        MultiWanVerticalSliceCapturePort capture = new();

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
            Metadata headers = ActorHeaders("acceptance");

            // AC#1 multi-wan-failover
            Device failoverDevice = await SeedDeviceAsync(
                inventory,
                headers,
                siteCode: "MWF31",
                host: "10.255.20.10",
                name: "failover",
                uplink: ProtoDeclaredUplinkMode.Failover);
            capture.Mode = MultiWanVerticalSliceCapturePort.WanMode.Failover;
            capture.StaticRouteGateway = "10.255.21.1";
            capture.DefaultRouteActive = "true";
            capture.RpFilter = "strict";

            StartCaptureResponse failoverCapture = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = failoverDevice.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                },
                headers,
                deadline: Deadline());
            Assert.False(failoverCapture.Deduplicated);
            SnapshotSummary failoverSummary = await snapshots.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = failoverCapture.CaptureId },
                headers,
                deadline: Deadline());
            AssertExpectedSections(failoverSummary);
            await AssertUplinksNotMixedAsync(snapshots, headers, failoverCapture.CaptureId!, "primary", "secondary");
            await AssertStrictRpFilterFindingAsync(snapshots, headers, failoverCapture.CaptureId!);

            string failoverConfig = Hex(failoverSummary.ConfigurationHash);
            string failoverObs = Hex(failoverSummary.ObservationHash);

            // AC#8: route active-state change does not change configuration hash.
            capture.DefaultRouteActive = "false";
            StartCaptureResponse activeChanged = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = failoverDevice.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                },
                headers,
                deadline: Deadline());
            SnapshotSummary afterActive = await snapshots.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = activeChanged.CaptureId },
                headers,
                deadline: Deadline());
            Assert.Equal(failoverConfig, Hex(afterActive.ConfigurationHash));
            Assert.NotEqual(failoverObs, Hex(afterActive.ObservationHash));

            DiffPage activeDiff = await snapshots.CompareSnapshotsAsync(
                new CompareSnapshotsRequest
                {
                    LeftCaptureId = failoverCapture.CaptureId,
                    RightCaptureId = activeChanged.CaptureId,
                    Page = new PageRequest { PageSize = 100 },
                },
                headers,
                deadline: Deadline());
            Assert.Contains(
                activeDiff.Entries,
                e => e.SectionId == "routing.ipv4.default-state"
                     && e.Domain == DiffDomain.Observation);
            Assert.DoesNotContain(
                activeDiff.Entries,
                e => e.SectionId == "routing.ipv4.static-routes"
                     && e.Domain == DiffDomain.Configuration);

            // AC#9: static route change changes configuration hash.
            capture.DefaultRouteActive = "false";
            capture.StaticRouteGateway = "10.255.21.2";
            StartCaptureResponse routeChanged = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = failoverDevice.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                },
                headers,
                deadline: Deadline());
            SnapshotSummary afterRoute = await snapshots.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = routeChanged.CaptureId },
                headers,
                deadline: Deadline());
            Assert.NotEqual(failoverConfig, Hex(afterRoute.ConfigurationHash));

            // AC#11: configuration vs operational route changes separated in diff.
            DiffPage routeDiff = await snapshots.CompareSnapshotsAsync(
                new CompareSnapshotsRequest
                {
                    LeftCaptureId = activeChanged.CaptureId,
                    RightCaptureId = routeChanged.CaptureId,
                    Page = new PageRequest { PageSize = 100 },
                },
                headers,
                deadline: Deadline());
            Assert.Contains(
                routeDiff.Entries,
                e => e.SectionId == "routing.ipv4.static-routes"
                     && e.Domain == DiffDomain.Configuration);
            Assert.Contains(
                routeDiff.Entries,
                e => e.SectionId == "routing.ipv4.default-state"
                     && e.Domain == DiffDomain.Observation);

            // AC#2 multi-wan-balanced (+ AC#3–#6 sections already covered by ExpectedSections).
            Device balancedDevice = await SeedDeviceAsync(
                inventory,
                headers,
                siteCode: "MWB31",
                host: "10.255.30.10",
                name: "balanced",
                uplink: ProtoDeclaredUplinkMode.Balanced);
            capture.Mode = MultiWanVerticalSliceCapturePort.WanMode.Balanced;
            capture.StaticRouteGateway = "10.255.31.1";
            capture.DefaultRouteActive = "true";
            capture.RpFilter = "strict";

            StartCaptureResponse balancedCapture = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = balancedDevice.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                },
                headers,
                deadline: Deadline());
            SnapshotSummary balancedSummary = await snapshots.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = balancedCapture.CaptureId },
                headers,
                deadline: Deadline());
            AssertExpectedSections(balancedSummary);
            await AssertUplinksNotMixedAsync(snapshots, headers, balancedCapture.CaptureId!, "balanced", "balanced");

            // AC#12: Controller did not mutate WAN/routing — only lab capture port state we set.
            Assert.Equal(MultiWanVerticalSliceCapturePort.WanMode.Balanced, capture.Mode);
            Assert.Equal("10.255.31.1", capture.StaticRouteGateway);
            Assert.True(capture.CaptureCount >= 4);
        }
        finally
        {
            using CancellationTokenSource stop = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stop.Token);
        }
    }

    private static void AssertExpectedSections(SnapshotSummary summary)
    {
        foreach (string sectionId in ExpectedSections)
        {
            Assert.Contains(
                summary.Sections,
                s => s.SectionId == sectionId && s.Status == SnapshotSectionCaptureStatus.Ok);
        }
    }

    private static async Task AssertUplinksNotMixedAsync(
        SnapshotService.SnapshotServiceClient snapshots,
        Metadata headers,
        Uuid captureId,
        string wan1Role,
        string wan2Role)
    {
        SnapshotSectionPage tables = await snapshots.GetSnapshotSectionAsync(
            new GetSnapshotSectionRequest
            {
                CaptureId = captureId,
                SectionId = "routing.tables",
                Domain = DiffDomain.Configuration,
                Page = new PageRequest { PageSize = 50 },
            },
            headers,
            deadline: Deadline());
        string wan1 = Field(tables, "wan1", "uplink-role");
        string wan2 = Field(tables, "wan2", "uplink-role");
        Assert.Equal(wan1Role, wan1);
        Assert.Equal(wan2Role, wan2);
        if (!string.Equals(wan1Role, wan2Role, StringComparison.Ordinal))
        {
            Assert.NotEqual(wan1, wan2);
        }
    }

    private static async Task AssertStrictRpFilterFindingAsync(
        SnapshotService.SnapshotServiceClient snapshots,
        Metadata headers,
        Uuid captureId)
    {
        SnapshotSectionPage settings = await snapshots.GetSnapshotSectionAsync(
            new GetSnapshotSectionRequest
            {
                CaptureId = captureId,
                SectionId = "network.ipv4.settings",
                Domain = DiffDomain.Configuration,
                Page = new PageRequest { PageSize = 10 },
            },
            headers,
            deadline: Deadline());
        Assert.Contains(
            settings.Records.SelectMany(r => r.Configuration),
            f => f.Name == "rp-filter" && f.Value.StringValue == "strict");

        SnapshotSectionPage findings = await snapshots.GetSnapshotSectionAsync(
            new GetSnapshotSectionRequest
            {
                CaptureId = captureId,
                SectionId = "topology.validation",
                Domain = DiffDomain.Observation,
                Page = new PageRequest { PageSize = 10 },
            },
            headers,
            deadline: Deadline());
        Assert.Contains(
            findings.Records.SelectMany(r => r.Observations),
            f => f.Name == "code" && f.Value.StringValue == "STRICT_RP_FILTER");
    }

    private static string Field(SnapshotSectionPage page, string tableName, string fieldName)
    {
        SnapshotRecord? record = page.Records.FirstOrDefault(r =>
            r.Configuration.Any(f => f.Name == "name" && f.Value.StringValue == tableName));
        Assert.NotNull(record);
        CanonicalField? field = record!.Configuration.FirstOrDefault(f => f.Name == fieldName);
        Assert.NotNull(field);
        return field!.Value.StringValue;
    }

    private static string Hex(Sha256 hash) => Convert.ToHexString(hash.Value.Span);

    private static async Task<Device> SeedDeviceAsync(
        InventoryService.InventoryServiceClient inventory,
        Metadata headers,
        string siteCode,
        string host,
        string name,
        ProtoDeclaredUplinkMode uplink)
    {
        Site site = await inventory.CreateSiteAsync(
            new CreateSiteRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                Code = siteCode,
                Name = $"Multi-WAN {name}",
            },
            headers,
            deadline: Deadline());
        Node node = await inventory.CreateNodeAsync(
            new CreateNodeRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                SiteId = site.Id,
                Name = name,
                DeclaredKind = ProtoNodeKind.Router,
                DeclaredUplinkMode = uplink,
            },
            headers,
            deadline: Deadline());
        Device device = await inventory.RegisterDeviceAsync(
            new RegisterDeviceRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                NodeId = node.Id,
                DisplayName = $"chr-{name}",
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
