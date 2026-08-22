using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Routing;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Routing;

/// <summary>Living Spec matrix for Issue Set M7.1-05 AC (dynamic route origin read-only analysis).</summary>
public sealed class DynamicRouteOriginLivingSpecTests
{
    [Fact]
    public void Ac1StaticConfiguredRouteClassifiesAsStatic()
    {
        RouteObservationFact route = Obs(
            "10.0.0.0/8",
            "192.168.0.1",
            "main",
            isDynamic: false,
            routeType: "unicast");

        Assert.Equal(RouteOrigins.Static, RouteOriginClassifier.Classify(route));
    }

    [Fact]
    public void Ac2ConnectedInterfaceGatewayClassifiesAsConnected()
    {
        RouteObservationFact route = Obs("10.0.0.0/24", "ether1", "main", isDynamic: true, routeType: "unicast");

        Assert.Equal(RouteOrigins.Connected, RouteOriginClassifier.Classify(route));
    }

    [Theory]
    [InlineData("bgp", RouteOrigins.Bgp)]
    [InlineData("ospf", RouteOrigins.Ospf)]
    [InlineData("dhcp", RouteOrigins.Dhcp)]
    [InlineData("vpn", RouteOrigins.Vpn)]
    public void Ac3DynamicRouteTypeMapsProtocolOrigin(string routeType, string expectedOrigin)
    {
        RouteObservationFact route = Obs("203.0.113.0/24", "198.51.100.1", "main", isDynamic: true, routeType: routeType);

        Assert.Equal(expectedOrigin, RouteOriginClassifier.Classify(route));
    }

    [Fact]
    public void Ac4DynamicDefaultRouteIncludedInActiveDynamicFacts()
    {
        RoutingOperationalSnapshot operational = Ops(
            routes:
            [
                Obs("0.0.0.0/0", "203.0.113.1", "main", isDynamic: true, routeType: "bgp"),
            ],
            defaults:
            [
                Default("0.0.0.0/0", "203.0.113.1", "main", isDynamic: true, isStatic: false),
            ]);

        DynamicRouteOriginFact fact = Assert.Single(operational.DynamicRouteOrigins.ActiveDynamicRoutes);
        Assert.Equal(RouteOrigins.Bgp, fact.Origin);
        Assert.Equal("0.0.0.0/0", fact.DstAddress);
    }

    [Fact]
    public void Ac5UnknownDynamicTypeFallsBackToOther()
    {
        RouteObservationFact route = Obs("198.51.100.0/24", "203.0.113.9", "main", isDynamic: true, routeType: "unicast");

        Assert.Equal(RouteOrigins.Other, RouteOriginClassifier.Classify(route));
    }

    [Fact]
    public void Ac6PerTableSummaryCountsOrigins()
    {
        RoutingOperationalSnapshot operational = Ops(
            routes:
            [
                Obs("10.0.0.0/8", "192.168.0.1", "main", isDynamic: false, routeType: "unicast"),
                Obs("10.10.0.0/16", "ether1", "main", isDynamic: true, routeType: "connect"),
                Obs("203.0.113.0/24", "198.51.100.1", "wan1", isDynamic: true, routeType: "bgp"),
                Obs("198.51.100.0/24", "203.0.113.9", "wan1", isDynamic: true, routeType: "unicast"),
            ]);

        DynamicRouteOriginTableSummary main = Assert.Single(
            operational.DynamicRouteOrigins.TableSummaries,
            s => s.Table == "main");
        Assert.Equal(1, main.CountsByOrigin[RouteOrigins.Static]);
        Assert.Equal(1, main.CountsByOrigin[RouteOrigins.Connected]);

        DynamicRouteOriginTableSummary wan1 = Assert.Single(
            operational.DynamicRouteOrigins.TableSummaries,
            s => s.Table == "wan1");
        Assert.Equal(1, wan1.CountsByOrigin[RouteOrigins.Bgp]);
        Assert.Equal(1, wan1.CountsByOrigin[RouteOrigins.Other]);
    }

    [Fact]
    public void Ac7NoRoutingWriteApisOpened()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/bgp/instance/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/ospf/instance/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
    }

    [Fact]
    public async Task Ac8PersistenceRoundTripIncludesDynamicRouteOriginAnalysis()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeRoutingAssuranceStateStore store = new();
        FakeClock clock = new();
        Device device = CreateDevice();
        await devices.AddAsync(device);

        RoutingOperationalSnapshot operational = Ops(
            routes:
            [
                Obs("203.0.113.0/24", "198.51.100.1", "main", isDynamic: true, routeType: "bgp"),
                Obs("10.10.0.0/16", "ether1", "main", isDynamic: true, routeType: "connect"),
            ]);

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock);
        ApplicationResult<RoutingAssuranceStateView> written = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = SampleConfiguration(),
                OperationalState = operational,
            });
        Assert.True(written.IsSuccess);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted!.OperationalState.DynamicRouteOrigins.ActiveDynamicRoutes.Count);
        Assert.Contains(
            persisted.OperationalState.DynamicRouteOrigins.ActiveDynamicRoutes,
            f => f.Origin == RouteOrigins.Bgp);
        Assert.Contains(
            persisted.OperationalState.Routes,
            r => r.Origin == RouteOrigins.Connected);
    }

    [Fact]
    public void DiscoveryMapsRouteTypeAndIncludesDynamicRoutesInOperationalObservations()
    {
        RoutingDependencyDiscoveryResult discovery = RoutingDependencyDiscovery.BuildResult(
            Ok(RosReadCommandId.RoutingTables, Row(("name", "main"), ("fib", "yes"))),
            Ok(RosReadCommandId.RoutingSettings),
            Ok(RosReadCommandId.RoutingRules),
            Ok(RosReadCommandId.IpVrfs),
            Ok(
                RosReadCommandId.Ipv4StaticRoutes,
                Row(
                    ("dst-address", "10.0.0.0/8"),
                    ("gateway", "192.168.0.1"),
                    ("distance", "1"),
                    ("type", "unicast"),
                    ("static", "true"),
                    ("dynamic", "false"),
                    ("routing-table", "main")),
                Row(
                    ("dst-address", "203.0.113.0/24"),
                    ("gateway", "198.51.100.1"),
                    ("type", "bgp"),
                    ("dynamic", "true"),
                    ("static", "false"),
                    ("active", "true"),
                    ("routing-table", "main"))),
            Ok(RosReadCommandId.Ipv6StaticRoutes),
            Ok(RosReadCommandId.Ipv4DefaultRouteState),
            Ok(RosReadCommandId.Ipv6DefaultRouteState),
            Ok(RosReadCommandId.RoutingFilterRules),
            Ok(RosReadCommandId.RoutingFilterSelectRules),
            Ok(RosReadCommandId.Ipv4Nat),
            Ok(RosReadCommandId.Ipv6Nat),
            Ok(RosReadCommandId.Ipv4Raw),
            Ok(RosReadCommandId.Ipv6Raw),
            Ok(RosReadCommandId.Ipv4Mangle),
            Ok(RosReadCommandId.Ipv6Mangle),
            Ok(RosReadCommandId.Ipv4Settings, Row(("rp-filter", "no"))),
            Ok(RosReadCommandId.Ipv6Settings, Row(("forward", "true"))));

        Assert.Single(discovery.Ipv4StaticRoutes);
        Assert.Equal(2, discovery.Ipv4RouteObservations.Count);
        Assert.Equal("bgp", discovery.Ipv4RouteObservations[1].RouteType);

        RoutingAssuranceState state = RoutingAssuranceStateMapper.ToState(
            DeviceId.New(),
            discovery,
            DateTimeOffset.UtcNow);
        RouteObservationFact dynamic = Assert.Single(
            state.OperationalState.Routes,
            r => r.IsDynamic);
        Assert.Equal(RouteOrigins.Bgp, dynamic.Origin);
        Assert.Equal("bgp", dynamic.RouteType);
    }

    [Fact]
    public void TraceExposesOriginOnSelectedRouteWhenObservationPresent()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes: [Route("203.0.113.0/24", "198.51.100.1", "main")]);
        RoutingOperationalSnapshot operational = Ops(
            routes:
            [
                Obs("203.0.113.0/24", "198.51.100.1", "main", isDynamic: true, routeType: "bgp"),
            ]);

        RouteResolutionTrace trace = RouteResolutionTraceEngine.Analyze(
            new RouteResolutionQuery
            {
                Family = "ipv4",
                SourceAddress = "192.168.0.10",
                DestinationAddress = "203.0.113.50",
            },
            configuration,
            operational);

        SelectedRoute selected = Assert.Single(trace.SelectedRoutes);
        Assert.Equal(RouteOrigins.Bgp, selected.Origin);
        Assert.Contains(trace.RouteCandidates, c => c.Selected && c.Origin == RouteOrigins.Bgp);
    }

    private static RouteObservationFact Obs(
        string dst,
        string gateway,
        string table,
        bool isDynamic,
        string? routeType = null,
        string? active = "true")
    {
        RouteObservationFact draft = new()
        {
            Family = "ipv4",
            DstAddress = dst,
            RoutingTable = table,
            Gateway = gateway,
            Active = active,
            ImmediateGateway = null,
            GatewayStatus = "reachable",
            IsDynamic = isDynamic,
            HwOffloaded = null,
            RouteType = routeType,
            Origin = null,
        };
        return new RouteObservationFact
        {
            Family = draft.Family,
            DstAddress = draft.DstAddress,
            RoutingTable = draft.RoutingTable,
            Gateway = draft.Gateway,
            Active = draft.Active,
            ImmediateGateway = draft.ImmediateGateway,
            GatewayStatus = draft.GatewayStatus,
            IsDynamic = draft.IsDynamic,
            HwOffloaded = draft.HwOffloaded,
            RouteType = draft.RouteType,
            Origin = RouteOriginClassifier.Classify(draft),
        };
    }

    private static DefaultRouteObservationFact Default(
        string dst,
        string gateway,
        string table,
        bool isDynamic,
        bool isStatic)
    {
        DefaultRouteObservationFact draft = new()
        {
            Family = "ipv4",
            DstAddress = dst,
            RoutingTable = table,
            Gateway = gateway,
            Distance = 1,
            Active = "true",
            ImmediateGateway = null,
            GatewayStatus = "reachable",
            IsDynamic = isDynamic,
            IsStatic = isStatic,
            RouteType = null,
            Origin = null,
        };
        return new DefaultRouteObservationFact
        {
            Family = draft.Family,
            DstAddress = draft.DstAddress,
            RoutingTable = draft.RoutingTable,
            Gateway = draft.Gateway,
            Distance = draft.Distance,
            Active = draft.Active,
            ImmediateGateway = draft.ImmediateGateway,
            GatewayStatus = draft.GatewayStatus,
            IsDynamic = draft.IsDynamic,
            IsStatic = draft.IsStatic,
            RouteType = draft.RouteType,
            Origin = RouteOriginClassifier.Classify(draft),
        };
    }

    private static RoutingOperationalSnapshot Ops(
        IReadOnlyList<RouteObservationFact> routes,
        IReadOnlyList<DefaultRouteObservationFact>? defaults = null)
        => new(routes, defaults ?? [], new Dictionary<string, string>(StringComparer.Ordinal));

    private static RoutingTableFact Table(string name)
        => new() { Name = name, Fib = "yes", Disabled = "false" };

    private static StaticRouteConfigFact Route(string dst, string gateway, string table, int distance = 1)
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

    private static RoutingConfigurationSnapshot SampleConfiguration()
        => Config(tables: [Table("main")]);

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

    private static RosReadCommandResult Ok(RosReadCommandId id, params RosReadRecord[] rows)
        => new()
        {
            CommandId = id,
            Lifecycle = RosCommandLifecycle.Completed,
            Records = rows,
            SessionInvalidated = false,
            Error = null,
        };

    private static RosReadRecord Row(params (string Name, string Value)[] properties)
    {
        Dictionary<string, string> known = new(StringComparer.Ordinal);
        foreach ((string name, string value) in properties)
        {
            known[name] = value;
        }

        return new RosReadRecord
        {
            KnownProperties = known,
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
