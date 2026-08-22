using Mfc.Application.Common;
using Mfc.Application.Endpoint;
using Mfc.Application.Models;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.RouterOs.Commands;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Endpoint;

/// <summary>Living Spec matrix for Issue Set M7.2-03 AC (endpoint mobility).</summary>
public sealed class EndpointMobilityLivingSpecTests
{
    private static readonly DateTimeOffset T08 = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T10 = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T14 = new(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T16 = new(2026, 8, 22, 16, 0, 0, TimeSpan.Zero);
    [Fact]
    public void Ac1MobilityDetectedWhenRoutingAnchorsChange()
    {
        EndpointId endpointId = EndpointId.New();
        SiteId siteA = SiteId.New();
        NodeId nodeA = NodeId.New();
        SiteId siteB = SiteId.New();
        NodeId nodeB = NodeId.New();
        EndpointPresenceInterval prior = BuildInterval(endpointId, siteA, nodeA, vlan: "10", vrf: "corp", ip: "192.168.1.50");
        EndpointPresenceInterval opened = BuildInterval(endpointId, siteB, nodeB, vlan: "20", vrf: "guest", ip: "192.168.2.50");

        Assert.True(EndpointMobilityHandler.IsMobilityEvent(prior, opened));
        Assert.False(EndpointMobilityHandler.IsMobilityEvent(null, opened));
    }

    [Fact]
    public void Ac2ActiveAssessmentInvalidatedOnIncidentMobility()
    {
        EndpointId endpointId = EndpointId.New();
        PresenceId priorPresence = PresenceId.New();
        SiteId siteA = SiteId.New();
        NodeId nodeA = NodeId.New();
        SiteId siteB = SiteId.New();
        NodeId nodeB = NodeId.New();
        ResponseAssessment active = ResponseAssessment.CreateActive(
            IncidentId.New(),
            endpointId,
            priorPresence,
            nodeA,
            ResponseAssessmentFeasibility.FullyEnforceable,
            T08);
        EndpointPresenceInterval opened = BuildInterval(endpointId, siteB, nodeB, vlan: "20", ip: "192.168.2.50");
        RoutingConfigurationSnapshot configuration = MobilityRoutingFixtures.Configuration();
        RoutingOperationalSnapshot operational = MobilityRoutingFixtures.Operational();

        EndpointMobilityOutcome outcome = EndpointMobilityHandler.ProcessActiveIncidentMobility(
            opened,
            active,
            configuration,
            operational,
            MobilityRoutingFixtures.ProbeTargets(),
            T14);

        Assert.NotNull(outcome.InvalidatedAssessment);
        Assert.Equal(ResponseAssessmentStatus.Invalidated, outcome.InvalidatedAssessment!.Status);
        Assert.Equal(EndpointMobilityCodes.MobilityInvalidation, outcome.InvalidatedAssessment.InvalidationReason);
        Assert.Equal(T14, outcome.InvalidatedAssessment.InvalidatedAt);
    }

    [Fact]
    public void Ac3RouteTracesRecomputedForNewPresenceContext()
    {
        EndpointId endpointId = EndpointId.New();
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        EndpointPresenceInterval opened = BuildInterval(
            endpointId,
            site,
            node,
            vlan: "20",
            vrf: "guest",
            ip: "192.168.2.50");
        ResponseAssessment active = ResponseAssessment.CreateActive(
            IncidentId.New(),
            endpointId,
            PresenceId.New(),
            node,
            ResponseAssessmentFeasibility.Indeterminate,
            T10);
        RoutingConfigurationSnapshot configuration = MobilityRoutingFixtures.Configuration();
        RoutingOperationalSnapshot operational = MobilityRoutingFixtures.Operational();

        EndpointMobilityOutcome outcome = EndpointMobilityHandler.ProcessActiveIncidentMobility(
            opened,
            active,
            configuration,
            operational,
            MobilityRoutingFixtures.ProbeTargets(),
            T14);

        Assert.Equal("10.20.0.10", outcome.RoutingContext.CorporateRouteTrace!.DestinationAddress);
        Assert.Equal("203.0.113.10", outcome.RoutingContext.InternetRouteTrace!.DestinationAddress);
        Assert.Equal("10.50.0.5", outcome.RoutingContext.WazuhRouteTrace!.DestinationAddress);
        Assert.Equal("guest", outcome.RoutingContext.Vrf);
    }

    [Fact]
    public void Ac4EnforcementNodeResolvedFromOpenedPresence()
    {
        NodeId node = NodeId.New();
        EndpointPresenceInterval opened = BuildInterval(EndpointId.New(), SiteId.New(), node);

        Assert.Equal(node, EndpointMobilityHandler.ResolveEnforcementNode(opened));
    }

    [Fact]
    public void Ac5AutoDeploySuppressedOnIncidentMobility()
    {
        EndpointPresenceInterval opened = BuildInterval(EndpointId.New(), SiteId.New(), NodeId.New());
        ResponseAssessment active = ResponseAssessment.CreateActive(
            IncidentId.New(),
            opened.EndpointId,
            PresenceId.New(),
            opened.NodeId,
            ResponseAssessmentFeasibility.FullyEnforceable,
            T10);
        EndpointMobilityOutcome outcome = EndpointMobilityHandler.ProcessActiveIncidentMobility(
            opened,
            active,
            MobilityRoutingFixtures.Configuration(),
            MobilityRoutingFixtures.Operational(),
            MobilityRoutingFixtures.ProbeTargets(),
            T14);

        Assert.True(outcome.AutoDeploySuppressed);
    }

    [Fact]
    public async Task Ac6MobilityWithoutActiveIncidentKeepsCommandTraces()
    {
        FakeAuthorizationBoundary auth = new();
        FakeEndpointPresenceStore presence = new();
        FakeResponseAssessmentStore assessments = new();
        FakeRoutingAssuranceStateStore routing = new();
        FakeClock clock = new() { UtcNow = T10 };
        SiteId siteA = SiteId.New();
        NodeId nodeA = NodeId.New();
        SiteId siteB = SiteId.New();
        NodeId nodeB = NodeId.New();
        EndpointId endpointId = EndpointId.New();
        OpenEndpointPresenceUseCase open = EndpointPresenceTestKit.CreateOpenUseCase(auth, presence, assessments, routing, clock);
        RouteResolutionTrace corporate = Trace("10.20.0.10", "corp", "ether2");

        await open.ExecuteAsync(EndpointPresenceTestKit.Command(
            endpointId,
            siteA,
            nodeA,
            corporateRouteTrace: corporate));
        clock.UtcNow = T14;
        ApplicationResult<EndpointPresenceUpsertResultView> migrated = await open.ExecuteAsync(
            EndpointPresenceTestKit.Command(
                endpointId,
                siteB,
                nodeB,
                ip: "192.168.2.50",
                vlan: "20",
                corporateRouteTrace: Trace("10.30.0.10", "corp", "ether3")));

        Assert.True(migrated.IsSuccess);
        Assert.Null(migrated.Value!.InvalidatedAssessment);
        Assert.False(migrated.Value.AutoDeploySuppressed);
        Assert.Equal("10.30.0.10", migrated.Value.RoutingContext.CorporateRouteTrace!.DestinationAddress);
    }

    [Fact]
    public void Ac7NoRoutingWriteApisOpened()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
        Assert.DoesNotContain(
            typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes(),
            static t => t.Namespace is not null
                        && t.Namespace.StartsWith("Mfc.RouterOs.Write", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac8UseCaseRoundTripInvalidatesAssessmentAndStoresRecomputedContext()
    {
        FakeAuthorizationBoundary auth = new();
        FakeEndpointPresenceStore presence = new();
        FakeResponseAssessmentStore assessments = new();
        FakeRoutingAssuranceStateStore routing = new();
        FakeClock clock = new() { UtcNow = T10 };
        SiteId siteA = SiteId.New();
        NodeId nodeA = NodeId.New();
        SiteId siteB = SiteId.New();
        NodeId nodeB = NodeId.New();
        EndpointId endpointId = EndpointId.New();
        DeviceId deviceId = DeviceId.New();
        routing.Seed(MobilityRoutingFixtures.RoutingState(deviceId));
        assessments.Seed(ResponseAssessment.CreateActive(
            IncidentId.New(),
            endpointId,
            PresenceId.New(),
            nodeA,
            ResponseAssessmentFeasibility.FullyEnforceable,
            T08));
        OpenEndpointPresenceUseCase open = EndpointPresenceTestKit.CreateOpenUseCase(auth, presence, assessments, routing, clock);

        await open.ExecuteAsync(EndpointPresenceTestKit.Command(endpointId, siteA, nodeA));
        clock.UtcNow = T14;
        ApplicationResult<EndpointPresenceUpsertResultView> migrated = await open.ExecuteAsync(
            EndpointPresenceTestKit.Command(
                endpointId,
                siteB,
                nodeB,
                ip: "192.168.2.50",
                vlan: "20",
                vrf: "guest",
                mobilityDeviceId: deviceId,
                probeTargets: MobilityRoutingFixtures.ProbeTargets()));

        Assert.True(migrated.IsSuccess, migrated.Error?.Message);
        Assert.NotNull(migrated.Value!.InvalidatedAssessment);
        Assert.Equal(ResponseAssessmentStatus.Invalidated.ToString(), migrated.Value.InvalidatedAssessment!.Status);
        Assert.True(migrated.Value.AutoDeploySuppressed);
        Assert.Equal(nodeB.Value, migrated.Value.EnforcementNodeId);
        Assert.Equal("10.20.0.10", migrated.Value.RoutingContext.CorporateRouteTrace!.DestinationAddress);

        ResponseAssessment? persisted = await assessments.GetActiveByEndpointAsync(endpointId);
        Assert.Null(persisted);
        EndpointRoutingContext? context = await presence.GetRoutingContextAsync(
            new PresenceId(migrated.Value.RoutingContext.PresenceId));
        Assert.NotNull(context);
        Assert.Equal("203.0.113.10", context!.InternetRouteTrace!.DestinationAddress);
    }

    private static EndpointPresenceInterval BuildInterval(
        EndpointId endpointId,
        SiteId site,
        NodeId node,
        string vlan = "10",
        string vrf = "corp",
        string ip = "192.168.1.50")
        => EndpointPresenceBuilder.BuildInterval(
            endpointId,
            PresenceId.New(),
            EndpointAttributionResolver.Resolve(
                LanQuery(site, node, ip),
                LanSnapshot(ip, vlan: vlan)),
            LanQuery(site, node, ip),
            T10,
            vrf: vrf);

    private static EndpointAttributionQuery LanQuery(SiteId site, NodeId node, string ip)
        => new()
        {
            Family = "ipv4",
            IpAddress = ip,
            SiteId = site,
            NodeId = node,
        };

    private static EndpointAttributionSnapshot LanSnapshot(string ip = "192.168.1.50", string vlan = "10")
        => new()
        {
            DhcpLeases = [new DhcpLeaseFact { IpAddress = ip, MacAddress = "AA:BB:CC:DD:EE:01" }],
            BridgeHostEntries =
            [
                new BridgeHostFact
                {
                    MacAddress = "AA:BB:CC:DD:EE:01",
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
}
