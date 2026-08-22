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

/// <summary>Living Spec matrix for Issue Set M7.1-03 AC (RouteResolutionTrace).</summary>
public sealed class RouteResolutionTraceLivingSpecTests
{
    [Fact]
    public void Ac1MainTableForwardRouteResolvesNextHop()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes:
            [
                Route("0.0.0.0/0", "1.1.1.1", "main", distance: 1),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1"),
        ]);

        RouteResolutionTrace trace = Analyze(
            Query("203.0.113.10"),
            configuration,
            operational);

        Assert.Equal(RouteResolutionDecisions.Forward, trace.Decision);
        Assert.Equal("main", trace.SelectedTable);
        Assert.Equal("0.0.0.0/0", trace.MatchedPrefix);
        Assert.Single(trace.SelectedRoutes);
        Assert.Equal("1.1.1.1", trace.SelectedRoutes[0].Gateway);
        Assert.Single(trace.ImmediateNextHops);
        Assert.Equal("1.1.1.1", trace.ImmediateNextHops[0].Gateway);
        Assert.Equal("ether1", Assert.Single(trace.EgressInterfaces));
        Assert.Equal(RouteResolutionCertainties.Definite, trace.Certainty);
    }

    [Fact]
    public void Ac2RoutingRuleLookupSelectsNonMainTable()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main"), Table("corp")],
            rules:
            [
                Rule(0, RoutingRuleActions.Lookup, dst: "10.20.0.0/16", table: "corp"),
            ],
            staticRoutes:
            [
                Route("10.20.0.0/16", "10.99.0.1", "corp", distance: 1),
                Route("0.0.0.0/0", "1.1.1.1", "main", distance: 1),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("10.20.0.0/16", "10.99.0.1", "corp", immediateGw: "10.99.0.1%ipsec1"),
            Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1"),
        ]);

        RouteResolutionTrace trace = Analyze(
            Query("10.20.0.50"),
            configuration,
            operational);

        Assert.Equal("corp", trace.SelectedTable);
        Assert.Equal(RouteResolutionDecisions.Forward, trace.Decision);
        Assert.NotNull(trace.MatchedRoutingRule);
        Assert.Equal(RoutingRuleActions.Lookup, trace.MatchedRoutingRule!.Action);
        Assert.Equal("corp", trace.MatchedRoutingRule.Table);
    }

    [Fact]
    public void Ac3RoutingMarkFromProbeSelectsMarkedTable()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main"), Table("wan2")],
            rules:
            [
                Rule(0, RoutingRuleActions.Lookup, routingMark: "wan2-mark", table: "wan2"),
            ],
            staticRoutes:
            [
                Route("0.0.0.0/0", "2.2.2.2", "wan2", distance: 1),
                Route("0.0.0.0/0", "1.1.1.1", "main", distance: 1),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("0.0.0.0/0", "2.2.2.2", "wan2", immediateGw: "2.2.2.2%ether2"),
            Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1"),
        ]);

        RouteResolutionTrace trace = Analyze(
            new RouteResolutionQuery
            {
                Family = "ipv4",
                DestinationAddress = "203.0.113.1",
                RoutingMark = "wan2-mark",
                MatchedMangleRule = new MatchedMangleRule
                {
                    Ordinal = 3,
                    Chain = "prerouting",
                    AssignedRoutingMark = "wan2-mark",
                },
            },
            configuration,
            operational);

        Assert.Equal("wan2-mark", trace.RoutingMark);
        Assert.NotNull(trace.MatchedMangleRule);
        Assert.Equal("wan2", trace.SelectedTable);
        Assert.Equal("2.2.2.2", trace.SelectedRoutes[0].Gateway);
    }

    [Fact]
    public void Ac4DropAndUnreachableRuleDecisions()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            rules:
            [
                Rule(0, RoutingRuleActions.Drop, dst: "10.66.0.0/16"),
                Rule(1, RoutingRuleActions.Unreachable, dst: "10.67.0.0/16"),
            ],
            staticRoutes: [Route("0.0.0.0/0", "1.1.1.1", "main")]);
        RoutingOperationalSnapshot operational = Ops([Obs("0.0.0.0/0", "1.1.1.1", "main")]);

        RouteResolutionTrace drop = Analyze(Query("10.66.0.5"), configuration, operational);
        Assert.Equal(RouteResolutionDecisions.Blackhole, drop.Decision);
        Assert.Equal(RoutingRuleActions.Drop, drop.RoutingRuleAction);

        RouteResolutionTrace unreachable = Analyze(Query("10.67.0.5"), configuration, operational);
        Assert.Equal(RouteResolutionDecisions.Unreachable, unreachable.Decision);
        Assert.Equal(RoutingRuleActions.Unreachable, unreachable.RoutingRuleAction);
    }

    [Fact]
    public void Ac5RecursiveGatewayResolutionChain()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes:
            [
                Route("10.50.0.0/16", "10.0.0.254", "main", distance: 1, scope: 30, targetScope: 10),
                Route("10.0.0.0/24", "10.0.0.1", "main", distance: 1, scope: 10, targetScope: 10),
                Route("10.0.0.0/8", "ether1", "main", distance: 0, scope: 10, targetScope: 5),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("10.50.0.0/16", "10.0.0.254", "main"),
            Obs("10.0.0.0/24", "10.0.0.1", "main", immediateGw: "10.0.0.1%ether1"),
            Obs("10.0.0.0/8", "ether1", "main", immediateGw: "10.0.0.1%ether1"),
        ]);

        RouteResolutionTrace trace = Analyze(Query("10.50.0.20"), configuration, operational);

        Assert.Equal(RouteResolutionDecisions.Forward, trace.Decision);
        Assert.NotEmpty(trace.RecursiveResolution);
        Assert.Contains(trace.RecursiveResolution, s => s.Target == "10.0.0.254");
        Assert.Equal("ether1", Assert.Single(trace.EgressInterfaces));
    }

    [Fact]
    public void Ac6EcmpReturnsOneOfSetWithIndeterminateCertainty()
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
            Obs("10.80.0.0/16", "10.0.0.2", "main", immediateGw: "10.0.0.2%ether1"),
            Obs("10.80.0.0/16", "10.0.0.3", "main", immediateGw: "10.0.0.3%ether2"),
        ]);

        RouteResolutionTrace trace = Analyze(Query("10.80.0.10"), configuration, operational);

        Assert.Equal(2, trace.SelectedRoutes.Count);
        Assert.Equal(2, trace.ImmediateNextHops.Count);
        Assert.All(trace.ImmediateNextHops, h => Assert.Equal(ImmediateNextHopSelectors.OneOf, h.Selector));
        Assert.Equal(RouteResolutionCertainties.Indeterminate, trace.Certainty);
        Assert.Equal(RouteResolutionDecisions.Forward, trace.Decision);
        Assert.NotNull(trace.EcmpRouteSet);
        EcmpRouteSet set = trace.EcmpRouteSet!;
        Assert.Equal("main", set.Table);
        Assert.Equal(2, set.NextHops.Count);
        Assert.Equal(2, set.ActiveNextHops.Count);
    }

    [Fact]
    public void Ac7NoRouteWhenLookupOnlyFails()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main"), Table("corp")],
            rules: [Rule(0, RoutingRuleActions.LookupOnly, dst: "10.90.0.0/16", table: "corp")],
            staticRoutes: [Route("0.0.0.0/0", "1.1.1.1", "main")]);
        RoutingOperationalSnapshot operational = Ops([Obs("0.0.0.0/0", "1.1.1.1", "main")]);

        RouteResolutionTrace trace = Analyze(Query("10.90.0.5"), configuration, operational);

        Assert.Equal(RouteResolutionDecisions.NoRoute, trace.Decision);
        Assert.Equal(RoutingRuleActions.LookupOnly, trace.RoutingRuleAction);
    }

    [Fact]
    public void Ac8LocalDeliveryForConnectedRoute()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes: [Route("192.168.1.0/24", "ether1", "main", distance: 0)]);
        RoutingOperationalSnapshot operational = Ops(
            [Obs("192.168.1.0/24", "ether1", "main", immediateGw: "192.168.1.1%ether1")]);

        RouteResolutionTrace trace = Analyze(Query("192.168.1.50"), configuration, operational);

        Assert.Equal(RouteResolutionDecisions.LocalDelivery, trace.Decision);
        Assert.Equal("ether1", Assert.Single(trace.EgressInterfaces));
    }

    [Fact]
    public async Task Ac9PersistenceRoundTripStoresResolutionTraces()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeRoutingAssuranceStateStore store = new();
        FakeClock clock = new();
        Device device = CreateDevice();
        await devices.AddAsync(device);

        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes: [Route("0.0.0.0/0", "1.1.1.1", "main")]);
        RoutingOperationalSnapshot operational = Ops(
            [Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1")]);

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock);
        ApplicationResult<RoutingAssuranceStateView> written = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = operational,
                TraceQueries = [Query("203.0.113.5")],
            });
        Assert.True(written.IsSuccess);
        Assert.Equal(1, written.Value!.ResolutionTraceCount);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        RouteResolutionTrace trace = Assert.Single(persisted!.ResolutionTraces);
        Assert.Equal(RouteResolutionDecisions.Forward, trace.Decision);
        Assert.Equal("203.0.113.5", trace.DestinationAddress);
    }

    [Fact]
    public void Ac10NoRoutingWriteApisOpened()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/rule/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
    }

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

    private static RoutingRuleFact Rule(
        int ordinal,
        string action,
        string? src = null,
        string? dst = null,
        string? routingMark = null,
        string? table = null)
        => new()
        {
            EffectiveOrdinal = ordinal,
            Action = action,
            SrcAddress = src,
            DstAddress = dst,
            RoutingMark = routingMark,
            Table = table,
            Disabled = "false",
        };

    private static StaticRouteConfigFact Route(
        string dst,
        string gateway,
        string table,
        int distance = 1,
        int? scope = null,
        int? targetScope = null)
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            Gateway = gateway,
            RoutingTable = table,
            Distance = distance,
            Scope = scope,
            TargetScope = targetScope,
            PrefSrc = null,
            CheckGateway = null,
            Disabled = "false",
        };

    private static RouteObservationFact Obs(
        string dst,
        string gateway,
        string table,
        string? immediateGw = null,
        string? hwOffloaded = null)
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            RoutingTable = table,
            Gateway = gateway,
            Active = "true",
            ImmediateGateway = immediateGw,
            GatewayStatus = "reachable",
            IsDynamic = false,
            HwOffloaded = hwOffloaded,
        };

    private static RoutingConfigurationSnapshot Config(
        IReadOnlyList<RoutingTableFact> tables,
        IReadOnlyList<RoutingRuleFact>? rules = null,
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
            rules ?? [],
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
