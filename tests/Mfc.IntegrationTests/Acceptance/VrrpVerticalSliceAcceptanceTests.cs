using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Topology;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using DomainDeclaredUplinkMode = Mfc.Domain.Inventory.DeclaredUplinkMode;
using DomainDevice = Mfc.Domain.Inventory.Device;
using DomainDeviceRole = Mfc.Domain.Inventory.DeviceRole;
using DomainNode = Mfc.Domain.Inventory.Node;
using DomainNodeKind = Mfc.Domain.Inventory.NodeKind;
using DomainNodeStatus = Mfc.Domain.Inventory.NodeStatus;
using ProtoDeclaredUplinkMode = Mfc.Contracts.Mfc.V1.DeclaredUplinkMode;
using ProtoDevice = Mfc.Contracts.Mfc.V1.Device;
using ProtoDeviceRole = Mfc.Contracts.Mfc.V1.DeviceRole;
using ProtoNode = Mfc.Contracts.Mfc.V1.Node;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoSite = Mfc.Contracts.Mfc.V1.Site;
using ProtoTrust = Mfc.Contracts.Mfc.V1.CertificateTrustMode;

namespace Mfc.IntegrationTests.Acceptance;

/// <summary>
/// M1-32 VRRP active/passive and split-master vertical-slice acceptance (in-process Controller + Postgres).
/// </summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class VrrpVerticalSliceAcceptanceTests
{
    private static readonly string[] ExpectedSections =
    [
        "system.identity",
        "ha.vrrp",
        "topology.validation",
        "capabilities.device",
    ];

    private readonly PostgresFixture _postgres;

    public VrrpVerticalSliceAcceptanceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task VrrpActivePassiveAndSplitMasterDiscoveryHashesAndTopology()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        VrrpVerticalSliceCapturePort capture = new();

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

            // AC#1/#4: active/passive members addressed individually.
            capture.Mode = VrrpVerticalSliceCapturePort.TopologyMode.ActivePassive;
            capture.PrimaryHost = "10.255.40.10";
            capture.SecondaryHost = "10.255.40.11";
            capture.PrimaryRole = "Master";
            capture.SecondaryRole = "Backup";

            (ProtoSite site, ProtoNode node, ProtoDevice primary, ProtoDevice secondary) = await SeedVrrpNodeAsync(
                inventory,
                headers,
                siteCode: "VRP32",
                nodeName: "vrrp-ap",
                primaryHost: capture.PrimaryHost,
                secondaryHost: capture.SecondaryHost);

            StartCaptureResponse primaryCapture = await CaptureAsync(snapshots, headers, primary.Id);
            Assert.Equal(capture.PrimaryHost, capture.LastTarget!.Endpoint.Host.Value);
            SnapshotSummary primarySummary = await SummaryAsync(snapshots, headers, primaryCapture.CaptureId!);
            AssertExpectedSections(primarySummary);
            await AssertVrrpGroupsAndRolesAsync(
                snapshots,
                headers,
                primaryCapture.CaptureId!,
                expectedRole: "Master",
                expectedVridCount: 2);

            StartCaptureResponse secondaryCapture = await CaptureAsync(snapshots, headers, secondary.Id);
            Assert.Equal(capture.SecondaryHost, capture.LastTarget!.Endpoint.Host.Value);
            SnapshotSummary secondarySummary = await SummaryAsync(snapshots, headers, secondaryCapture.CaptureId!);
            AssertExpectedSections(secondarySummary);
            await AssertVrrpGroupsAndRolesAsync(
                snapshots,
                headers,
                secondaryCapture.CaptureId!,
                expectedRole: "Backup",
                expectedVridCount: 2);

            // AC#10: each member snapshot is stored separately.
            Assert.NotEqual(primaryCapture.CaptureId, secondaryCapture.CaptureId);
            Assert.Equal(ProtoUuid.ToGuid(primary.Id), ProtoUuid.ToGuid(primarySummary.DeviceId));
            Assert.Equal(ProtoUuid.ToGuid(secondary.Id), ProtoUuid.ToGuid(secondarySummary.DeviceId));

            string primaryConfig = Hex(primarySummary.ConfigurationHash);
            string primaryObs = Hex(primarySummary.ObservationHash);

            // AC#6/#7: role switch changes observation hash only.
            capture.PrimaryRole = "Backup";
            capture.SecondaryRole = "Master";
            StartCaptureResponse afterRole = await CaptureAsync(snapshots, headers, primary.Id);
            SnapshotSummary afterRoleSummary = await SummaryAsync(snapshots, headers, afterRole.CaptureId!);
            Assert.Equal(primaryConfig, Hex(afterRoleSummary.ConfigurationHash));
            Assert.NotEqual(primaryObs, Hex(afterRoleSummary.ObservationHash));

            DiffPage roleDiff = await snapshots.CompareSnapshotsAsync(
                new CompareSnapshotsRequest
                {
                    LeftCaptureId = primaryCapture.CaptureId,
                    RightCaptureId = afterRole.CaptureId,
                    Page = new PageRequest { PageSize = 100 },
                },
                headers,
                deadline: Deadline());
            Assert.Contains(
                roleDiff.Entries,
                e => e.SectionId == "ha.vrrp"
                     && e.Domain == DiffDomain.Observation);
            Assert.DoesNotContain(
                roleDiff.Entries,
                e => e.SectionId == "ha.vrrp"
                     && e.Domain == DiffDomain.Configuration);

            // AC#11: node-level view aggregates members without losing device-level data.
            NodeDetails details = await inventory.GetNodeAsync(
                new GetNodeRequest { NodeId = node.Id },
                headers,
                deadline: Deadline());
            Assert.Equal(2, details.Devices.Count);
            Assert.Contains(details.Devices, d => d.ManagementHost == capture.PrimaryHost);
            Assert.Contains(details.Devices, d => d.ManagementHost == capture.SecondaryHost);
            Assert.Contains(details.Devices, d => d.Id.Equals(primary.Id));
            Assert.Contains(details.Devices, d => d.Id.Equals(secondary.Id));
            ListNodesResponse listed = await inventory.ListNodesAsync(
                new ListNodesRequest
                {
                    SiteId = site.Id,
                    Page = new PageRequest { PageSize = 20 },
                },
                headers,
                deadline: Deadline());
            Assert.Contains(listed.Nodes, n => n.Id.Equals(node.Id) && n.DeclaredKind == ProtoNodeKind.Vrrp);

            // Domain topology: active/passive is valid (one master).
            DomainNode domainNode = RebuildDomainNode(site, node, primary, secondary);
            NodeTopologyValidationResult activePassive = NodeTopologyValidator.Validate(
                domainNode,
                [
                    VrrpFacts(primary, "7.16.1", VrrpMemberObservedState.Master),
                    VrrpFacts(secondary, "7.16.1", VrrpMemberObservedState.Backup),
                ]);
            Assert.True(activePassive.IsValid);
            Assert.DoesNotContain(
                activePassive.Findings,
                f => f.Code == TopologyValidationFinding.VrrpSplitMaster);

            // AC#5: split-master is not classified as one global master.
            capture.Mode = VrrpVerticalSliceCapturePort.TopologyMode.SplitMaster;
            capture.PrimaryHost = "10.255.50.10";
            capture.SecondaryHost = "10.255.50.11";
            capture.PrimaryRole = "Master";
            capture.SecondaryRole = "Master";
            (ProtoSite splitSite, ProtoNode splitNode, ProtoDevice splitA, ProtoDevice splitB) = await SeedVrrpNodeAsync(
                inventory,
                headers,
                siteCode: "VRS32",
                nodeName: "vrrp-split",
                primaryHost: capture.PrimaryHost,
                secondaryHost: capture.SecondaryHost);

            StartCaptureResponse splitACapture = await CaptureAsync(snapshots, headers, splitA.Id);
            SnapshotSummary splitASummary = await SummaryAsync(snapshots, headers, splitACapture.CaptureId!);
            AssertExpectedSections(splitASummary);
            await AssertVrrpGroupsAndRolesAsync(
                snapshots,
                headers,
                splitACapture.CaptureId!,
                expectedRole: "Master",
                expectedVridCount: 1);
            SnapshotSectionPage splitFindings = await snapshots.GetSnapshotSectionAsync(
                new GetSnapshotSectionRequest
                {
                    CaptureId = splitACapture.CaptureId,
                    SectionId = "topology.validation",
                    Domain = DiffDomain.Observation,
                    Page = new PageRequest { PageSize = 10 },
                },
                headers,
                deadline: Deadline());
            Assert.Contains(
                splitFindings.Records.SelectMany(r => r.Observations),
                f => f.Name == "code" && f.Value.StringValue == "VRRP_SPLIT_MASTER");
            Assert.Contains(
                splitFindings.Records.SelectMany(r => r.Observations),
                f => f.Name == "global-master" && f.Value.StringValue == "false");

            DomainNode splitDomain = RebuildDomainNode(splitSite, splitNode, splitA, splitB);
            NodeTopologyValidationResult splitResult = NodeTopologyValidator.Validate(
                splitDomain,
                [
                    VrrpFacts(splitA, "7.16.1", VrrpMemberObservedState.Master),
                    VrrpFacts(splitB, "7.16.1", VrrpMemberObservedState.Master),
                ]);
            Assert.False(splitResult.IsValid);
            Assert.Contains(
                splitResult.Findings,
                f => f.Code == TopologyValidationFinding.VrrpSplitMaster
                     && f.Severity == TopologyFindingSeverity.Blocker);

            // AC#8: version mismatch creates topology blocker.
            NodeTopologyValidationResult versionMismatch = NodeTopologyValidator.Validate(
                domainNode,
                [
                    VrrpFacts(primary, "7.16.1", VrrpMemberObservedState.Master),
                    VrrpFacts(secondary, "7.15.3", VrrpMemberObservedState.Backup),
                ]);
            Assert.False(versionMismatch.IsValid);
            Assert.Contains(
                versionMismatch.Findings,
                f => f.Code == TopologyValidationFinding.VrrpVersionMismatch
                     && f.Severity == TopologyFindingSeverity.Blocker);

            // AC#9: unreachable member is not masked (missing facts → explicit blocker).
            NodeTopologyValidationResult unreachable = NodeTopologyValidator.Validate(
                domainNode,
                [VrrpFacts(primary, "7.16.1", VrrpMemberObservedState.Master)]);
            Assert.False(unreachable.IsValid);
            Assert.Contains(
                unreachable.Findings,
                f => f.Code == TopologyValidationFinding.FactsDeviceUnknown
                     && f.Subject == ProtoUuid.ToGuid(secondary.Id).ToString("D"));

            // AC#12: Controller did not mutate VRRP configuration — only lab capture port state.
            Assert.Equal(VrrpVerticalSliceCapturePort.TopologyMode.SplitMaster, capture.Mode);
            Assert.Equal("Master", capture.PrimaryRole);
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

    private static async Task AssertVrrpGroupsAndRolesAsync(
        SnapshotService.SnapshotServiceClient snapshots,
        Metadata headers,
        Uuid captureId,
        string expectedRole,
        int expectedVridCount)
    {
        SnapshotSectionPage config = await snapshots.GetSnapshotSectionAsync(
            new GetSnapshotSectionRequest
            {
                CaptureId = captureId,
                SectionId = "ha.vrrp",
                Domain = DiffDomain.Configuration,
                Page = new PageRequest { PageSize = 20 },
            },
            headers,
            deadline: Deadline());
        Assert.Equal(expectedVridCount, config.Records.Count);
        Assert.All(
            config.Records,
            r => Assert.Contains(r.Configuration, f => f.Name == "group" && f.Value.StringValue.Contains("vrid=", StringComparison.Ordinal)));

        SnapshotSectionPage obs = await snapshots.GetSnapshotSectionAsync(
            new GetSnapshotSectionRequest
            {
                CaptureId = captureId,
                SectionId = "ha.vrrp",
                Domain = DiffDomain.Observation,
                Page = new PageRequest { PageSize = 20 },
            },
            headers,
            deadline: Deadline());
        Assert.Equal(expectedVridCount, obs.Records.Count);
        Assert.All(
            obs.Records,
            r => Assert.Contains(r.Observations, f => f.Name == "role" && f.Value.StringValue == expectedRole));
    }

    private static async Task<StartCaptureResponse> CaptureAsync(
        SnapshotService.SnapshotServiceClient snapshots,
        Metadata headers,
        Uuid deviceId)
        => await snapshots.StartCaptureAsync(
            new StartCaptureRequest
            {
                DeviceId = deviceId,
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
            },
            headers,
            deadline: Deadline());

    private static async Task<SnapshotSummary> SummaryAsync(
        SnapshotService.SnapshotServiceClient snapshots,
        Metadata headers,
        Uuid captureId)
        => await snapshots.GetSnapshotSummaryAsync(
            new GetSnapshotSummaryRequest { CaptureId = captureId },
            headers,
            deadline: Deadline());

    private static DomainNode RebuildDomainNode(ProtoSite site, ProtoNode node, ProtoDevice primary, ProtoDevice secondary)
    {
        NodeId nodeId = new(ProtoUuid.ToGuid(node.Id));
        DomainNode domain = DomainNode.Reconstitute(
            nodeId,
            new SiteId(ProtoUuid.ToGuid(site.Id)),
            NonEmptyName.Create(node.Name),
            DomainNodeKind.Vrrp,
            DomainDeclaredUplinkMode.Failover,
            DomainNodeStatus.Active,
            rowVersion: 1);
        domain.AttachDevice(DomainDevice.Reconstitute(
            new DeviceId(ProtoUuid.ToGuid(primary.Id)),
            nodeId,
            NonEmptyName.Create(primary.DisplayName),
            ManagementEndpoint.Create(primary.ManagementHost, 8729),
            DomainDeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            rowVersion: 1));
        domain.AttachDevice(DomainDevice.Reconstitute(
            new DeviceId(ProtoUuid.ToGuid(secondary.Id)),
            nodeId,
            NonEmptyName.Create(secondary.DisplayName),
            ManagementEndpoint.Create(secondary.ManagementHost, 8729),
            DomainDeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            rowVersion: 1));
        return domain;
    }

    private static DeviceTopologyFacts VrrpFacts(
        ProtoDevice device,
        string version,
        VrrpMemberObservedState state)
        => new()
        {
            DeviceId = new DeviceId(ProtoUuid.ToGuid(device.Id)),
            RouterOsVersion = version,
            BoardRole = ObservedBoardRole.Router,
            IsExplicitlyBoundToNode = true,
            VrrpInstances =
            [
                new ObservedVrrpInstance
                {
                    Family = IpAddressFamily.IPv4,
                    Vrid = 10,
                    InterfaceKey = "ether1",
                    ObservedState = state,
                    RouterOsVersion = version,
                },
            ],
            UplinkEvidence = ObservedUplinkEvidence.FailoverDistanceRoutes,
            ObservedUplinkInterfaceCount = 2,
            GrantsTransitFirewallCapability = true,
            CapabilityHash = null,
        };

    private static string Hex(Sha256 hash) => Convert.ToHexString(hash.Value.Span);

    private static async Task<(ProtoSite Site, ProtoNode Node, ProtoDevice Primary, ProtoDevice Secondary)> SeedVrrpNodeAsync(
        InventoryService.InventoryServiceClient inventory,
        Metadata headers,
        string siteCode,
        string nodeName,
        string primaryHost,
        string secondaryHost)
    {
        ProtoSite site = await inventory.CreateSiteAsync(
            new CreateSiteRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                Code = siteCode,
                Name = $"VRRP {nodeName}",
            },
            headers,
            deadline: Deadline());
        ProtoNode node = await inventory.CreateNodeAsync(
            new CreateNodeRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                SiteId = site.Id,
                Name = nodeName,
                DeclaredKind = ProtoNodeKind.Vrrp,
                DeclaredUplinkMode = ProtoDeclaredUplinkMode.Failover,
            },
            headers,
            deadline: Deadline());
        ProtoDevice primary = await RegisterMemberAsync(
            inventory,
            headers,
            node.Id,
            displayName: $"{nodeName}-a",
            host: primaryHost);
        ProtoDevice secondary = await RegisterMemberAsync(
            inventory,
            headers,
            node.Id,
            displayName: $"{nodeName}-b",
            host: secondaryHost);
        return (site, node, primary, secondary);
    }

    private static async Task<ProtoDevice> RegisterMemberAsync(
        InventoryService.InventoryServiceClient inventory,
        Metadata headers,
        Uuid nodeId,
        string displayName,
        string host)
    {
        ProtoDevice device = await inventory.RegisterDeviceAsync(
            new RegisterDeviceRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                NodeId = nodeId,
                DisplayName = displayName,
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
