using System.Reflection;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Application.Endpoint;
using Mfc.Application.Models;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.RouterOs.Commands;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Endpoint;
using Xunit;

namespace Mfc.UnitTests.E2E;

/// <summary>
/// Living Spec matrix for Issue Set M7.2-04 AC 1–10 (CHR acceptance: endpoint migration scenarios).
/// Scripted in-process fixtures ONLY — live CHR matrix remains OFF.
/// Chains M7.2-01…M7.2-03 end-to-end via attribution, presence, mobility, and assessment stores.
/// </summary>
public sealed class EndpointMobilityChrAcceptanceLivingSpecTests
{
    private static readonly DateTimeOffset T08 = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T10 = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T12 = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T14 = new(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T16 = new(2026, 8, 22, 16, 0, 0, TimeSpan.Zero);
    [Fact]
    public void Ac1AttributionResolvesBranchAEndpointAnchors()
    {
        SiteId siteA = SiteId.New();
        NodeId nodeA = NodeId.New();
        EndpointAttributionResult attribution = EndpointAttributionResolver.Resolve(
            LanQuery(siteA, nodeA, "192.168.1.50"),
            LanSnapshot("192.168.1.50", "AA:BB:CC:DD:EE:01", vlan: "10"));

        Assert.Equal(EndpointAttributionCertainty.Proven, attribution.Certainty);
        Assert.Contains(attribution.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.Mac);
        Assert.Contains(attribution.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.Vlan && h.Value == "10");
    }

    [Fact]
    public async Task Ac2OpenPresenceAtBranchAStoresRoutingContext()
    {
        (OpenEndpointPresenceUseCase open, FakeEndpointPresenceStore presence, _, EndpointId endpointId, SiteId siteA, NodeId nodeA) =
            await SeedBranchAAsync(T10);
        ApplicationResult<EndpointPresenceUpsertResultView> opened = await open.ExecuteAsync(
            BranchCommand(endpointId, siteA, nodeA, "192.168.1.50", "10", "corp",
                Trace("10.20.0.10", "corp", "ether2")));
        Assert.True(opened.IsSuccess, opened.Error?.Message);
        EndpointRoutingContext? context = await presence.GetRoutingContextAsync(
            new PresenceId(opened.Value!.RoutingContext.PresenceId));
        Assert.NotNull(context);
        Assert.Equal("corp", context!.Vrf);
        Assert.Equal("10.20.0.10", context.CorporateRouteTrace!.DestinationAddress);
    }

    [Fact]
    public async Task Ac3ActiveIncidentAssessmentBoundToEndpoint()
    {
        (_, _, _, EndpointId endpointId, SiteId siteA, NodeId nodeA) =
            await SeedBranchAAsync(T10);
        FakeResponseAssessmentStore assessments = new();
        assessments.Seed(ResponseAssessment.CreateActive(
            IncidentId.New(),
            endpointId,
            PresenceId.New(),
            nodeA,
            ResponseAssessmentFeasibility.FullyEnforceable,
            T08));
        ResponseAssessment? active = await assessments.GetActiveByEndpointAsync(endpointId);
        Assert.NotNull(active);
        Assert.True(active!.IsActive);
        Assert.Equal(endpointId, active.EndpointId);
    }

    [Fact]
    public async Task Ac4MigrationClosesBranchAAndOpensBranchBPresence()
    {
        (OpenEndpointPresenceUseCase open, FakeEndpointPresenceStore presence, FakeClock clock, EndpointId endpointId, SiteId siteA, NodeId nodeA) =
            await SeedBranchAAsync(T10);
        await open.ExecuteAsync(BranchCommand(endpointId, siteA, nodeA));
        SiteId siteB = SiteId.New();
        NodeId nodeB = NodeId.New();
        clock.UtcNow = T14;
        ApplicationResult<EndpointPresenceUpsertResultView> migrated = await open.ExecuteAsync(
            BranchCommand(endpointId, siteB, nodeB, "192.168.2.50", "20", "guest"));
        Assert.True(migrated.IsSuccess);
        EndpointPresenceInterval? active = await presence.GetActiveIntervalAsync(endpointId);
        Assert.NotNull(active);
        Assert.Equal(siteB, active!.SiteId);
        Assert.Equal("20", active.VlanId);
        EndpointPresenceInterval? historical = await presence.GetIntervalAsOfAsync(endpointId, T12);
        Assert.NotNull(historical);
        Assert.Equal(siteA, historical!.SiteId);
    }

    [Fact]
    public async Task Ac5IncidentMobilityInvalidatesAssessmentAndRecomputesTraces()
    {
        (OpenEndpointPresenceUseCase open, FakeResponseAssessmentStore assessments, DeviceId deviceId, EndpointId endpointId, SiteId siteA, NodeId nodeA) =
            await SeedIncidentMobilityAsync(T10);
        FakeClock clock = GetClock(open);
        await open.ExecuteAsync(BranchCommand(endpointId, siteA, nodeA));
        assessments.Seed(ResponseAssessment.CreateActive(
            IncidentId.New(),
            endpointId,
            PresenceId.New(),
            nodeA,
            ResponseAssessmentFeasibility.FullyEnforceable,
            T08));
        SiteId siteB = SiteId.New();
        NodeId nodeB = NodeId.New();
        clock.UtcNow = T14;
        ApplicationResult<EndpointPresenceUpsertResultView> migrated = await open.ExecuteAsync(
            BranchCommand(endpointId, siteB, nodeB, "192.168.2.50", "20", "guest",
                mobilityDeviceId: deviceId,
                probeTargets: MobilityRoutingFixtures.ProbeTargets()));
        Assert.True(migrated.IsSuccess, migrated.Error?.Message);
        Assert.NotNull(migrated.Value!.InvalidatedAssessment);
        Assert.Equal("10.20.0.10", migrated.Value.RoutingContext.CorporateRouteTrace!.DestinationAddress);
        Assert.Equal("203.0.113.10", migrated.Value.RoutingContext.InternetRouteTrace!.DestinationAddress);
        Assert.Null(await assessments.GetActiveByEndpointAsync(endpointId));
    }

    [Fact]
    public async Task Ac6EnforcementNodeFollowsOpenedPresenceAtBranchB()
    {
        (OpenEndpointPresenceUseCase open, FakeResponseAssessmentStore assessments, DeviceId deviceId, EndpointId endpointId, SiteId siteA, NodeId nodeA) =
            await SeedIncidentMobilityAsync(T10);
        FakeClock clock = GetClock(open);
        await open.ExecuteAsync(BranchCommand(endpointId, siteA, nodeA));
        assessments.Seed(ResponseAssessment.CreateActive(
            IncidentId.New(),
            endpointId,
            PresenceId.New(),
            nodeA,
            ResponseAssessmentFeasibility.FullyEnforceable,
            T08));
        SiteId siteB = SiteId.New();
        NodeId nodeB = NodeId.New();
        clock.UtcNow = T14;
        ApplicationResult<EndpointPresenceUpsertResultView> migrated = await open.ExecuteAsync(
            BranchCommand(endpointId, siteB, nodeB, "192.168.2.50", "20", "guest",
                mobilityDeviceId: deviceId,
                probeTargets: MobilityRoutingFixtures.ProbeTargets()));
        Assert.Equal(nodeB.Value, migrated.Value!.EnforcementNodeId);
    }

    [Fact]
    public async Task Ac7AutoDeploySuppressedOnIncidentMobilityPath()
    {
        (OpenEndpointPresenceUseCase open, FakeResponseAssessmentStore assessments, DeviceId deviceId, EndpointId endpointId, SiteId siteA, NodeId nodeA) =
            await SeedIncidentMobilityAsync(T10);
        FakeClock clock = GetClock(open);
        await open.ExecuteAsync(BranchCommand(endpointId, siteA, nodeA));
        assessments.Seed(ResponseAssessment.CreateActive(
            IncidentId.New(), endpointId, PresenceId.New(), nodeA, ResponseAssessmentFeasibility.Indeterminate, T08));
        SiteId siteB = SiteId.New();
        NodeId nodeB = NodeId.New();
        clock.UtcNow = T14;
        ApplicationResult<EndpointPresenceUpsertResultView> migrated = await open.ExecuteAsync(
            BranchCommand(endpointId, siteB, nodeB, "192.168.2.50", "20", "guest",
                mobilityDeviceId: deviceId,
                probeTargets: MobilityRoutingFixtures.ProbeTargets()));
        Assert.True(migrated.Value!.AutoDeploySuppressed);
    }

    [Fact]
    public async Task Ac8AsOfQueryReturnsBranchAHistoricalRoutingContext()
    {
        FakeAuthorizationBoundary auth = new();
        FakeEndpointPresenceStore presence = new();
        FakeClock clock = new() { UtcNow = T10 };
        GetEndpointRoutingContextUseCase get = new(auth, presence, clock);
        SiteId siteA = SiteId.New();
        NodeId nodeA = NodeId.New();
        SiteId siteB = SiteId.New();
        NodeId nodeB = NodeId.New();
        EndpointId endpointId = EndpointId.New();
        OpenEndpointPresenceUseCase open = EndpointPresenceTestKit.CreateOpenUseCase(auth, presence, clock: clock);
        await open.ExecuteAsync(BranchCommand(endpointId, siteA, nodeA, vrf: "corp"));
        clock.UtcNow = T14;
        await open.ExecuteAsync(BranchCommand(endpointId, siteB, nodeB, "192.168.2.50", "20", "guest"));
        ApplicationResult<EndpointRoutingContextView> historical = await get.ExecuteAsync(
            new GetEndpointRoutingContextQuery
            {
                Actor = "tester",
                EndpointId = endpointId.Value,
                AsOfUtc = T12,
            });
        Assert.True(historical.IsSuccess);
        Assert.Equal(siteA.Value, historical.Value!.SiteId);
        Assert.Equal("corp", historical.Value.Vrf);
    }

    [Fact]
    public void Ac9NoRoutingWriteApisOpened()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
        Assert.DoesNotContain(
            typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes(),
            static t => t.Namespace is not null
                        && t.Namespace.StartsWith("Mfc.RouterOs.Write", StringComparison.Ordinal));
        Assert.Null(typeof(OpenEndpointPresenceUseCase).GetMethod(
            "Deploy",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
    }

    [Fact]
    public void Ac10DeterministicLivingSpecNoLiveChr()
    {
        string root = RepoRoot;
        string thisFile = Path.Combine(root, "tests", "Mfc.UnitTests", "E2E", "EndpointMobilityChrAcceptanceLivingSpecTests.cs");
        Assert.True(File.Exists(thisFile), thisFile);
        string source = File.ReadAllText(thisFile);
        Assert.Contains("live CHR matrix remains OFF", source, StringComparison.Ordinal);
        Assert.Contains("Scripted in-process fixtures ONLY", source, StringComparison.Ordinal);
        string forbiddenLiveToken = string.Concat("Connect", "ToLive", "Chr");
        Assert.DoesNotContain(forbiddenLiveToken, source, StringComparison.Ordinal);

        string testing = File.ReadAllText(Path.Combine(root, "docs", "development", "testing.md"));
        Assert.Contains("M7.2-04", testing, StringComparison.Ordinal);
        Assert.Contains("EndpointMobilityChrAcceptanceLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Live CHR", testing, StringComparison.OrdinalIgnoreCase);

        string readme = File.ReadAllText(Path.Combine(root, "testlab", "chr", "README.md"));
        Assert.Contains("endpoint-mobility-migration", readme, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "testlab", "chr", "scripts", "provision-endpoint-mobility.sh")));
    }

    private static async Task<(OpenEndpointPresenceUseCase Open, FakeEndpointPresenceStore Presence, FakeClock Clock, EndpointId EndpointId, SiteId SiteA, NodeId NodeA)>
        SeedBranchAAsync(DateTimeOffset at)
    {
        FakeAuthorizationBoundary auth = new();
        FakeEndpointPresenceStore presence = new();
        FakeClock clock = new() { UtcNow = at };
        EndpointId endpointId = EndpointId.New();
        SiteId siteA = SiteId.New();
        NodeId nodeA = NodeId.New();
        OpenEndpointPresenceUseCase open = EndpointPresenceTestKit.CreateOpenUseCase(auth, presence, clock: clock);
        await Task.CompletedTask;
        return (open, presence, clock, endpointId, siteA, nodeA);
    }

    private static async Task<(OpenEndpointPresenceUseCase Open, FakeResponseAssessmentStore Assessments, DeviceId DeviceId, EndpointId EndpointId, SiteId SiteA, NodeId NodeA)>
        SeedIncidentMobilityAsync(DateTimeOffset at)
    {
        FakeAuthorizationBoundary auth = new();
        FakeEndpointPresenceStore presence = new();
        FakeResponseAssessmentStore assessments = new();
        FakeRoutingAssuranceStateStore routing = new();
        FakeClock clock = new() { UtcNow = at };
        DeviceId deviceId = DeviceId.New();
        routing.Seed(MobilityRoutingFixtures.RoutingState(deviceId));
        EndpointId endpointId = EndpointId.New();
        SiteId siteA = SiteId.New();
        NodeId nodeA = NodeId.New();
        OpenEndpointPresenceUseCase open = EndpointPresenceTestKit.CreateOpenUseCase(auth, presence, assessments, routing, clock);
        await Task.CompletedTask;
        return (open, assessments, deviceId, endpointId, siteA, nodeA);
    }

    private static FakeClock GetClock(OpenEndpointPresenceUseCase open)
    {
        FieldInfo? field = typeof(OpenEndpointPresenceUseCase).GetField("_clock", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return (FakeClock)field!.GetValue(open)!;
    }

    private static UpsertEndpointPresenceCommand BranchCommand(
        EndpointId endpointId,
        SiteId site,
        NodeId node,
        string ip = "192.168.1.50",
        string vlan = "10",
        string? vrf = "corp",
        RouteResolutionTrace? corporateRouteTrace = null,
        DeviceId? mobilityDeviceId = null,
        EndpointMobilityProbeTargets? probeTargets = null)
        => EndpointPresenceTestKit.Command(
            endpointId, site, node, ip, vlan, vrf, corporateRouteTrace, mobilityDeviceId, probeTargets);

    private static EndpointAttributionQuery LanQuery(SiteId site, NodeId node, string ip)
        => new()
        {
            Family = "ipv4",
            IpAddress = ip,
            SiteId = site,
            NodeId = node,
        };

    private static EndpointAttributionSnapshot LanSnapshot(string ip, string mac, string vlan)
        => new()
        {
            DhcpLeases = [new DhcpLeaseFact { IpAddress = ip, MacAddress = mac }],
            BridgeHostEntries =
            [
                new BridgeHostFact
                {
                    MacAddress = mac,
                    VlanId = vlan,
                    Bridge = "br-lan",
                    Port = "ether2",
                },
            ],
        };

    private static RouteResolutionTrace Trace(string destination, string table, string egress)
        => new()
        {
            Family = "ipv4",
            SourceAddress = "192.168.1.50",
            DestinationAddress = destination,
            SelectedTable = table,
            SelectedVrf = table == "corp" ? "corp" : null,
            Decision = RouteResolutionDecisions.Forward,
            EgressInterfaces = [egress],
            Certainty = RouteResolutionCertainties.Definite,
        };

    private static string RepoRoot
    {
        get
        {
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("Repository root not found.");
        }
    }
}
