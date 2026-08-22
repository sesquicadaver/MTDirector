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

/// <summary>Living Spec matrix for Issue Set M7.1-06 AC (RouteExpectation evaluation).</summary>
public sealed class RouteExpectationLivingSpecTests
{
    [Fact]
    public void Ac1ExpectedTableAndVrfMatchPassAndFail()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main"), Table("corp")],
            vrfs: [Vrf("corp", "vlan10")],
            staticRoutes:
            [
                Route("0.0.0.0/0", "1.1.1.1", "main"),
                Route("10.20.0.0/16", "10.99.0.1", "corp"),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1"),
            Obs("10.20.0.0/16", "10.99.0.1", "corp", immediateGw: "10.99.0.1%ipsec1"),
        ]);

        RouteResolutionTrace mainTrace = Analyze(Query("203.0.113.10"), configuration, operational);
        RouteResolutionTrace corpTrace = Analyze(
            new RouteResolutionQuery
            {
                Family = "ipv4",
                SourceAddress = "10.20.0.5",
                DestinationAddress = "10.20.0.50",
                IngressInterface = "vlan10",
            },
            configuration,
            operational);

        RouteExpectation pass = Expectation("203.0.113.0/24", expectedTable: "main", expectedVrf: "main");
        Assert.Empty(Evaluate(pass, [mainTrace], configuration, operational));

        RouteExpectation tableFail = Expectation("203.0.113.0/24", expectedTable: "corp");
        RouteFinding tableFinding = Assert.Single(Evaluate(tableFail, [mainTrace], configuration, operational));
        Assert.Equal(RouteExpectationCodes.ExpectedTableMismatch, tableFinding.Code);

        RouteExpectation vrfFail = Expectation("10.20.0.0/16", expectedVrf: "main");
        RouteFinding vrfFinding = Assert.Single(Evaluate(vrfFail, [corpTrace], configuration, operational));
        Assert.Equal(RouteExpectationCodes.ExpectedVrfMismatch, vrfFinding.Code);
    }

    [Fact]
    public void Ac2AllowedNextHopAndEgressInterfaceViolations()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes: [Route("0.0.0.0/0", "1.1.1.1", "main")]);
        RoutingOperationalSnapshot operational = Ops(
            [Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1")]);
        RouteResolutionTrace trace = Analyze(Query("203.0.113.10"), configuration, operational);

        RouteFinding hopFinding = Assert.Single(
            Evaluate(
                Expectation("203.0.113.0/24", allowedNextHops: ["9.9.9.9"]),
                [trace],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.AllowedNextHopViolation, hopFinding.Code);

        RouteFinding egressFinding = Assert.Single(
            Evaluate(
                Expectation("203.0.113.0/24", allowedEgressInterfaces: ["ether2"]),
                [trace],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.AllowedEgressInterfaceViolation, egressFinding.Code);

        Assert.Empty(
            Evaluate(
                Expectation("203.0.113.0/24", allowedNextHops: ["1.1.1.1"], allowedEgressInterfaces: ["ether1"]),
                [trace],
                configuration,
                operational));
    }

    [Fact]
    public void Ac3ForbiddenBlackholeAndUnreachableDecisions()
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

        RouteResolutionTrace blackhole = Analyze(Query("10.66.0.5"), configuration, operational);
        RouteFinding blackholeFinding = Assert.Single(
            Evaluate(
                Expectation("10.66.0.0/16", forbiddenRouteTypes: [RouteResolutionDecisions.Blackhole]),
                [blackhole],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.ForbiddenRouteTypePresent, blackholeFinding.Code);

        RouteResolutionTrace unreachable = Analyze(Query("10.67.0.5"), configuration, operational);
        RouteFinding unreachableFinding = Assert.Single(
            Evaluate(
                Expectation("10.67.0.0/16", forbiddenRouteTypes: [RouteResolutionDecisions.Unreachable]),
                [unreachable],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.ForbiddenRouteTypePresent, unreachableFinding.Code);
    }

    [Fact]
    public void Ac4RequiredOriginTypeMustBePresent()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes: [Route("203.0.113.0/24", "198.51.100.1", "main")]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs(
                "203.0.113.0/24",
                "198.51.100.1",
                "main",
                immediateGw: "198.51.100.1%ether1",
                isDynamic: true,
                routeType: "bgp",
                origin: RouteOrigins.Bgp),
        ]);
        RouteResolutionTrace trace = Analyze(Query("203.0.113.10"), configuration, operational);

        Assert.Empty(
            Evaluate(
                Expectation("203.0.113.0/24", requiredRouteTypes: [RouteOrigins.Bgp]),
                [trace],
                configuration,
                operational));

        RouteFinding finding = Assert.Single(
            Evaluate(
                Expectation("203.0.113.0/24", requiredRouteTypes: [RouteOrigins.Static]),
                [trace],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.RequiredRouteTypeMissing, finding.Code);
    }

    [Fact]
    public void Ac5CpuFirewallPathRequirementFailsOnHardwareOnly()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes: [Route("0.0.0.0/0", "1.1.1.1", "main")]);
        RoutingOperationalSnapshot operational = Ops(
            [Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1", hwOffloaded: "true")]);
        RouteResolutionTrace trace = Analyze(Query("203.0.113.10"), configuration, operational);

        RouteFinding finding = Assert.Single(
            Evaluate(
                Expectation("203.0.113.0/24", requireCpuFirewallPath: true),
                [trace],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.CpuFirewallPathRequired, finding.Code);
    }

    [Fact]
    public void Ac6ReversePathMissingProducesFinding()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes:
            [
                Route("10.10.0.0/16", "10.0.0.1", "main"),
            ]);
        RoutingOperationalSnapshot operational = Ops(
            [Obs("10.10.0.0/16", "10.0.0.1", "main", immediateGw: "10.0.0.1%ether1")]);

        RouteResolutionTrace forward = Analyze(
            new RouteResolutionQuery
            {
                Family = "ipv4",
                SourceAddress = "192.168.1.50",
                DestinationAddress = "10.10.0.20",
            },
            configuration,
            operational);

        RouteFinding finding = Assert.Single(
            Evaluate(
                Expectation("10.10.0.0/16", requireReversePath: true),
                [forward],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.ReversePathMissing, finding.Code);
    }

    [Fact]
    public void Ac7CriticalExpectationsUseCriticalFindingCodes()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes: [Route("0.0.0.0/0", "1.1.1.1", "main")]);
        RoutingOperationalSnapshot operational = Ops(
            [Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1")]);
        RouteResolutionTrace trace = Analyze(Query("203.0.113.10"), configuration, operational);

        RouteFinding warning = Assert.Single(
            Evaluate(
                Expectation("203.0.113.0/24", expectedTable: "corp", critical: false),
                [trace],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.ExpectedTableMismatch, warning.Code);

        RouteFinding critical = Assert.Single(
            Evaluate(
                Expectation("203.0.113.0/24", expectedTable: "corp", critical: true),
                [trace],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.ExpectedTableMismatchCritical, critical.Code);
    }

    [Fact]
    public async Task Ac8PersistenceRoundTripStoresExpectationsAndFindings()
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

        RouteExpectation expectation = Expectation("203.0.113.0/24", expectedTable: "corp", critical: true);
        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock);
        ApplicationResult<RoutingAssuranceStateView> written = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = operational,
                RouteExpectations = [expectation],
                TraceQueries = [Query("203.0.113.10")],
            });
        Assert.True(written.IsSuccess);
        Assert.Equal(1, written.Value!.RouteExpectationCount);
        Assert.Equal(1, written.Value.RouteFindingCount);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        Assert.Equal(expectation.DestinationPrefix, Assert.Single(persisted!.RouteExpectations).DestinationPrefix);
        RouteFinding finding = Assert.Single(persisted.RouteFindings);
        Assert.Equal(RouteExpectationCodes.ExpectedTableMismatchCritical, finding.Code);
    }

    [Fact]
    public void Ac9NoRoutingWriteApisOpened()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/rule/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
    }

    [Fact]
    public void Ac10EcmpOneOfAllowedHopSetPassesWhenAnyHopAllowed()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes:
            [
                Route("10.80.0.0/16", "10.0.0.2", "main"),
                Route("10.80.0.0/16", "10.0.0.3", "main"),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("10.80.0.0/16", "10.0.0.2", "main", immediateGw: "10.0.0.2%ether1"),
            Obs("10.80.0.0/16", "10.0.0.3", "main", immediateGw: "10.0.0.3%ether2"),
        ]);
        RouteResolutionTrace trace = Analyze(Query("10.80.0.10"), configuration, operational);

        Assert.Empty(
            Evaluate(
                Expectation("10.80.0.0/16", allowedNextHops: ["10.0.0.3"]),
                [trace],
                configuration,
                operational));

        RouteFinding finding = Assert.Single(
            Evaluate(
                Expectation("10.80.0.0/16", allowedNextHops: ["10.0.0.9"]),
                [trace],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.AllowedNextHopViolation, finding.Code);
    }

    private static IReadOnlyList<RouteFinding> Evaluate(
        RouteExpectation expectation,
        IReadOnlyList<RouteResolutionTrace> traces,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational)
        => RouteExpectationEvaluator.Evaluate([expectation], traces, configuration, operational);

    private static RouteResolutionTrace Analyze(
        RouteResolutionQuery query,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational)
        => RouteResolutionTraceEngine.Analyze(query, configuration, operational);

    private static RouteExpectation Expectation(
        string destinationPrefix,
        string? expectedVrf = null,
        string? expectedTable = null,
        IReadOnlyList<string>? allowedNextHops = null,
        IReadOnlyList<string>? allowedEgressInterfaces = null,
        IReadOnlyList<string>? requiredRouteTypes = null,
        IReadOnlyList<string>? forbiddenRouteTypes = null,
        bool requireCpuFirewallPath = false,
        bool requireReversePath = false,
        bool critical = false)
        => new()
        {
            NodeId = null,
            Family = "ipv4",
            DestinationPrefix = destinationPrefix,
            ExpectedVrf = expectedVrf,
            ExpectedTable = expectedTable,
            AllowedNextHops = allowedNextHops ?? [],
            AllowedEgressInterfaces = allowedEgressInterfaces ?? [],
            RequiredRouteTypes = requiredRouteTypes ?? [],
            ForbiddenRouteTypes = forbiddenRouteTypes ?? [],
            RequireCpuFirewallPath = requireCpuFirewallPath,
            RequireReversePath = requireReversePath,
            Critical = critical,
        };

    private static RouteResolutionQuery Query(string destination, string? source = "192.168.0.10")
        => new()
        {
            Family = "ipv4",
            SourceAddress = source,
            DestinationAddress = destination,
        };

    private static RoutingTableFact Table(string name)
        => new() { Name = name, Fib = "yes", Disabled = "false" };

    private static VrfDefinitionFact Vrf(string name, string interfaces)
        => new() { Name = name, Interfaces = interfaces, Disabled = "false" };

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
        string? hwOffloaded = null,
        bool isDynamic = false,
        string? routeType = null,
        string? origin = null)
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            RoutingTable = table,
            Gateway = gateway,
            Active = "true",
            ImmediateGateway = immediateGw,
            GatewayStatus = "reachable",
            IsDynamic = isDynamic,
            RouteType = routeType,
            Origin = origin,
            HwOffloaded = hwOffloaded,
        };

    private static RoutingConfigurationSnapshot Config(
        IReadOnlyList<RoutingTableFact> tables,
        IReadOnlyList<RoutingRuleFact>? rules = null,
        IReadOnlyList<VrfDefinitionFact>? vrfs = null,
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
            vrfs ?? [],
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
