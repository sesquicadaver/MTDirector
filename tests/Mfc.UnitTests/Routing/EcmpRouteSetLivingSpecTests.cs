using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Routing;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.RouterOs.Commands;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Routing;

/// <summary>Living Spec matrix for Issue Set M7.1-04 AC (EcmpRouteSet / ONE_OF bounded next-hop sets).</summary>
public sealed class EcmpRouteSetLivingSpecTests
{
    [Fact]
    public void Ac1MultiPathEcmpPopulatesEcmpRouteSetFields()
    {
        RouteResolutionTrace trace = AnalyzeEcmp();

        Assert.NotNull(trace.EcmpRouteSet);
        EcmpRouteSet set = trace.EcmpRouteSet!;
        Assert.Equal("10.80.0.10", set.Destination);
        Assert.Equal("main", set.Table);
        Assert.Equal(2, set.NextHops.Count);
        Assert.Contains(set.NextHops, h => h.Gateway == "10.0.0.2" && h.Interface == "ether1");
        Assert.Contains(set.NextHops, h => h.Gateway == "10.0.0.3" && h.Interface == "ether2");
        Assert.NotNull(set.HashingContext);
    }

    [Fact]
    public void Ac2NextHopsAlignWithImmediateOneOfHops()
    {
        RouteResolutionTrace trace = AnalyzeEcmp();

        Assert.NotNull(trace.EcmpRouteSet);
        EcmpRouteSet set = trace.EcmpRouteSet!;
        Assert.Equal(trace.ImmediateNextHops.Count, set.NextHops.Count);
        foreach (ImmediateNextHop hop in trace.ImmediateNextHops)
        {
            Assert.Contains(
                set.NextHops,
                n => n.Gateway == hop.Gateway && n.Interface == hop.Interface);
        }
    }

    [Fact]
    public void Ac3InactiveEqualCostRouteExcludedFromEcmpSet()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes:
            [
                Route("10.80.0.0/16", "10.0.0.2", "main", distance: 1),
                Route("10.80.0.0/16", "10.0.0.3", "main", distance: 1),
                Route("10.80.0.0/16", "10.0.0.4", "main", distance: 1),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("10.80.0.0/16", "10.0.0.2", "main", immediateGw: "10.0.0.2%ether1"),
            Obs("10.80.0.0/16", "10.0.0.3", "main", immediateGw: "10.0.0.3%ether2"),
            Obs("10.80.0.0/16", "10.0.0.4", "main", immediateGw: "10.0.0.4%ether3", active: "false"),
        ]);

        RouteResolutionTrace trace = Analyze(Query("10.80.0.10"), configuration, operational);

        Assert.NotNull(trace.EcmpRouteSet);
        EcmpRouteSet set = trace.EcmpRouteSet!;
        Assert.Equal(2, set.NextHops.Count);
        Assert.Equal(2, set.ActiveNextHops.Count);
        Assert.DoesNotContain(set.NextHops, h => h.Gateway == "10.0.0.4");
        Assert.DoesNotContain(set.ActiveNextHops, h => h.Gateway == "10.0.0.4");
    }

    [Fact]
    public void Ac4PartialHardwareOffloadListsSubsetAndMixedExecutionPath()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes:
            [
                Route("10.80.0.0/16", "10.0.0.2", "main", distance: 1),
                Route("10.80.0.0/16", "10.0.0.3", "main", distance: 1),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("10.80.0.0/16", "10.0.0.2", "main", immediateGw: "10.0.0.2%ether1", hwOffloaded: "true"),
            Obs("10.80.0.0/16", "10.0.0.3", "main", immediateGw: "10.0.0.3%ether2", hwOffloaded: "false"),
        ]);

        RouteResolutionTrace trace = Analyze(Query("10.80.0.10"), configuration, operational);

        Assert.NotNull(trace.EcmpRouteSet);
        EcmpRouteSet set = trace.EcmpRouteSet!;
        Assert.Single(set.HardwareOffloadedNextHops);
        Assert.Equal("10.0.0.2", set.HardwareOffloadedNextHops[0].Gateway);
        Assert.Equal(2, set.NextHops.Count);
        Assert.True(set.HardwareOffloadedNextHops.Count < set.NextHops.Count);
        Assert.Equal(RouteResolutionExecutionPaths.Mixed, trace.ExecutionPath);
    }

    [Fact]
    public void Ac5HashingContextDeterministicFromQueryFields()
    {
        RouteResolutionQuery query = new()
        {
            Family = "ipv4",
            SourceAddress = "192.168.0.10",
            DestinationAddress = "10.80.0.10",
            IngressInterface = "ether1",
            RoutingMark = "wan2-mark",
        };
        RouteResolutionTrace trace = Analyze(query, EcmpConfiguration(), EcmpOperational());

        Assert.NotNull(trace.EcmpRouteSet);
        EcmpHashingContext context = trace.EcmpRouteSet!.HashingContext;
        Assert.Equal("ipv4", context.Family);
        Assert.Equal("192.168.0.10", context.SourceAddress);
        Assert.Equal("10.80.0.10", context.DestinationAddress);
        Assert.Equal("ether1", context.IngressInterface);
        Assert.Equal("wan2-mark", context.RoutingMark);
        Assert.Equal("ipv4", context.FlowKeyMaterial["ecmp.flow.family"]);
        Assert.Equal("10.80.0.10", context.FlowKeyMaterial["ecmp.flow.destination"]);
        Assert.Equal("192.168.0.10", context.FlowKeyMaterial["ecmp.flow.source"]);
        Assert.Equal("ether1", context.FlowKeyMaterial["ecmp.flow.ingress"]);
        Assert.Equal("wan2-mark", context.FlowKeyMaterial["ecmp.flow.routing_mark"]);

        EcmpHashingContext again = EcmpRouteSetBuilder.CreateHashingContext(query);
        Assert.Equal(context.FlowKeyMaterial, again.FlowKeyMaterial);
    }

    [Fact]
    public void Ac6SingleHopForwardLeavesEcmpRouteSetNull()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes: [Route("0.0.0.0/0", "1.1.1.1", "main")]);
        RoutingOperationalSnapshot operational = Ops(
            [Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1")]);

        RouteResolutionTrace trace = Analyze(Query("203.0.113.1"), configuration, operational);

        Assert.Null(trace.EcmpRouteSet);
        Assert.Equal(RouteResolutionCertainties.Definite, trace.Certainty);
        Assert.Single(trace.ImmediateNextHops);
        Assert.Null(trace.ImmediateNextHops[0].Selector);
    }

    [Fact]
    public void Ac7EcmpUsesOneOfSelectorAndIndeterminateCertainty()
    {
        RouteResolutionTrace trace = AnalyzeEcmp();

        Assert.NotNull(trace.EcmpRouteSet);
        Assert.Equal(RouteResolutionCertainties.Indeterminate, trace.Certainty);
        Assert.Equal(RouteResolutionDecisions.Forward, trace.Decision);
        Assert.All(trace.ImmediateNextHops, h => Assert.Equal(ImmediateNextHopSelectors.OneOf, h.Selector));
    }

    [Fact]
    public async Task Ac8PersistenceRoundTripIncludesEcmpRouteSet()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeRoutingAssuranceStateStore store = new();
        FakeClock clock = new();
        Device device = CreateDevice();
        await devices.AddAsync(device);

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock, new FakeUnitOfWork());
        ApplicationResult<RoutingAssuranceStateView> written = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = EcmpConfiguration(),
                OperationalState = EcmpOperational(),
                TraceQueries =
                [
                    new RouteResolutionQuery
                    {
                        Family = "ipv4",
                        SourceAddress = "192.168.0.10",
                        DestinationAddress = "10.80.0.10",
                        IngressInterface = "ether1",
                        RoutingMark = "wan2-mark",
                    },
                ],
            });
        Assert.True(written.IsSuccess);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        RouteResolutionTrace trace = Assert.Single(persisted!.ResolutionTraces);
        Assert.NotNull(trace.EcmpRouteSet);
        EcmpRouteSet set = trace.EcmpRouteSet!;
        Assert.Equal(2, set.NextHops.Count);
        Assert.Single(set.HardwareOffloadedNextHops);
        Assert.Equal("wan2-mark", set.HashingContext.RoutingMark);
    }

    [Fact]
    public void Ac9NoRoutingWriteApisOpened()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/rule/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
    }

    private static RouteResolutionTrace AnalyzeEcmp()
        => Analyze(Query("10.80.0.10"), EcmpConfiguration(), EcmpOperational());

    private static RoutingConfigurationSnapshot EcmpConfiguration()
        => Config(
            tables: [Table("main")],
            staticRoutes:
            [
                Route("10.80.0.0/16", "10.0.0.2", "main", distance: 1),
                Route("10.80.0.0/16", "10.0.0.3", "main", distance: 1),
            ]);

    private static RoutingOperationalSnapshot EcmpOperational()
        => Ops(
        [
            Obs("10.80.0.0/16", "10.0.0.2", "main", immediateGw: "10.0.0.2%ether1", hwOffloaded: "true"),
            Obs("10.80.0.0/16", "10.0.0.3", "main", immediateGw: "10.0.0.3%ether2", hwOffloaded: "false"),
        ]);

    private static RouteResolutionTrace Analyze(
        RouteResolutionQuery query,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational)
        => RouteResolutionTraceEngine.Analyze(query, configuration, operational);

    private static RouteResolutionQuery Query(string destination, string? source = "192.168.0.10")
        => new()
        {
            Family = "ipv4",
            SourceAddress = source,
            DestinationAddress = destination,
        };

    private static RoutingTableFact Table(string name)
        => new() { Name = name, Fib = "yes", Disabled = "false" };

    private static StaticRouteConfigFact Route(
        string dst,
        string gateway,
        string table,
        int distance = 1)
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            Gateway = gateway,
            RoutingTable = table,
            Distance = distance,
            Scope = null,
            TargetScope = null,
            PrefSrc = null,
            CheckGateway = null,
            Disabled = "false",
        };

    private static RouteObservationFact Obs(
        string dst,
        string gateway,
        string table,
        string? immediateGw = null,
        string? hwOffloaded = null,
        string? active = "true")
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            RoutingTable = table,
            Gateway = gateway,
            Active = active,
            ImmediateGateway = immediateGw,
            GatewayStatus = "reachable",
            IsDynamic = false,
            HwOffloaded = hwOffloaded,
        };

    private static RoutingConfigurationSnapshot Config(
        IReadOnlyList<RoutingTableFact> tables,
        IReadOnlyList<StaticRouteConfigFact>? staticRoutes = null)
        => new(
            tables,
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
            [],
            staticRoutes ?? [],
            [],
            [],
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static RoutingOperationalSnapshot Ops(IReadOnlyList<RouteObservationFact> routes)
        => new(routes, [], new Dictionary<string, string>(StringComparer.Ordinal));

    private static Device CreateDevice()
        => Device.Reconstitute(
            DeviceId.New(),
            NodeId.New(),
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.1", 8729),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1,
            lastCompletedCaptureId: null);
}
