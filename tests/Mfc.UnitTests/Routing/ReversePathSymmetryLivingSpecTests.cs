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

/// <summary>Living Spec matrix for Issue Set M7.1-07 AC (reverse-path symmetry analysis).</summary>
public sealed class ReversePathSymmetryLivingSpecTests
{
    [Fact]
    public void Ac1SymmetricPairMatchesTableVrfEgressAndDecision()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes:
            [
                Route("10.10.0.0/16", "10.0.0.1", "main"),
                Route("192.168.0.0/16", "10.0.0.2", "main"),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("10.10.0.0/16", "10.0.0.1", "main", immediateGw: "10.0.0.1%ether1"),
            Obs("192.168.0.0/16", "10.0.0.2", "main", immediateGw: "10.0.0.2%ether1"),
        ]);

        RouteResolutionTrace forward = Analyze(
            new RouteResolutionQuery
            {
                Family = "ipv4",
                SourceAddress = "192.168.1.50",
                DestinationAddress = "10.10.0.20",
            },
            configuration,
            operational);

        ReversePathSymmetryAnalysis analysis = ReversePathSymmetryAnalyzer.Analyze(forward, configuration, operational);

        Assert.Equal(ReversePathSymmetryResults.Symmetric, analysis.Result);
        Assert.NotNull(analysis.ReverseTrace);
        Assert.Equal("main", analysis.ReverseTrace!.SelectedTable);
        Assert.Equal("main", analysis.ReverseTrace.SelectedVrf);
        Assert.Equal(RouteResolutionDecisions.Forward, analysis.ReverseTrace.Decision);
        Assert.Equal(["ether1"], analysis.ReverseTrace.EgressInterfaces);
        Assert.Empty(analysis.MismatchedDimensions);
    }

    [Fact]
    public void Ac2ReversePathMissingWhenReturnRouteAbsent()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes: [Route("10.10.0.0/16", "10.0.0.1", "main")]);
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

        ReversePathSymmetryAnalysis analysis = ReversePathSymmetryAnalyzer.Analyze(forward, configuration, operational);

        Assert.Equal(ReversePathSymmetryResults.ReversePathMissing, analysis.Result);
        Assert.NotNull(analysis.ReverseTrace);
        Assert.Equal(RouteResolutionDecisions.NoRoute, analysis.ReverseTrace!.Decision);

        RouteFinding finding = Assert.Single(
            RouteExpectationEvaluator.Evaluate(
                [Expectation("10.10.0.0/16", requireReversePath: true)],
                [forward],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.ReversePathMissing, finding.Code);
    }

    [Fact]
    public void Ac3AsymmetricExpectedWhenFlagSet()
    {
        (RoutingConfigurationSnapshot configuration, RoutingOperationalSnapshot operational, RouteResolutionTrace forward) =
            MultiWanAsymmetricFixture();

        ReversePathSymmetryAnalysis analysis = ReversePathSymmetryAnalyzer.Analyze(
            forward,
            configuration,
            operational,
            new ReversePathSymmetryAnalyzerOptions { ExpectAsymmetricReversePath = true });

        Assert.Equal(ReversePathSymmetryResults.AsymmetricExpected, analysis.Result);
        Assert.Contains("egress", analysis.MismatchedDimensions);
        Assert.Empty(
            RouteExpectationEvaluator.Evaluate(
                [Expectation("203.0.113.0/24", requireReversePath: true, expectAsymmetricReversePath: true)],
                [forward],
                configuration,
                operational));
    }

    [Fact]
    public void Ac4AsymmetricUnexpectedProducesEvaluatorFinding()
    {
        (RoutingConfigurationSnapshot configuration, RoutingOperationalSnapshot operational, RouteResolutionTrace forward) =
            MultiWanAsymmetricFixture();

        ReversePathSymmetryAnalysis analysis = ReversePathSymmetryAnalyzer.Analyze(forward, configuration, operational);

        Assert.Equal(ReversePathSymmetryResults.AsymmetricUnexpected, analysis.Result);
        Assert.Contains("egress", analysis.MismatchedDimensions);

        RouteFinding finding = Assert.Single(
            RouteExpectationEvaluator.Evaluate(
                [Expectation("203.0.113.0/24", requireReversePath: true)],
                [forward],
                configuration,
                operational));
        Assert.Equal(RouteExpectationCodes.AsymmetricReversePathUnexpected, finding.Code);
    }

    [Fact]
    public void Ac5IndeterminateWhenForwardSourceMissing()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes: [Route("0.0.0.0/0", "1.1.1.1", "main")]);
        RoutingOperationalSnapshot operational = Ops(
            [Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1")]);

        RouteResolutionTrace forward = Analyze(
            new RouteResolutionQuery
            {
                Family = "ipv4",
                SourceAddress = null,
                DestinationAddress = "203.0.113.10",
            },
            configuration,
            operational);

        ReversePathSymmetryAnalysis analysis = ReversePathSymmetryAnalyzer.Analyze(forward, configuration, operational);

        Assert.Equal(ReversePathSymmetryResults.Indeterminate, analysis.Result);
        Assert.Null(analysis.ReverseTrace);
    }

    [Fact]
    public void Ac6MultiWanDifferentEgressInterfacesAreAsymmetric()
    {
        (RoutingConfigurationSnapshot configuration, RoutingOperationalSnapshot operational, RouteResolutionTrace forward) =
            MultiWanAsymmetricFixture();

        Assert.Equal(RouteResolutionDecisions.Forward, forward.Decision);
        Assert.Equal("ether1", Assert.Single(forward.EgressInterfaces));

        ReversePathSymmetryAnalysis analysis = ReversePathSymmetryAnalyzer.Analyze(forward, configuration, operational);

        Assert.Equal(ReversePathSymmetryResults.AsymmetricUnexpected, analysis.Result);
        Assert.NotNull(analysis.ReverseTrace);
        Assert.Equal("ether2", Assert.Single(analysis.ReverseTrace!.EgressInterfaces));
        Assert.NotEqual(forward.SelectedTable, analysis.ReverseTrace.SelectedTable);
    }

    [Fact]
    public void Ac7NoRoutingWriteApisOpened()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/rule/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/route/add"));
    }

    [Fact]
    public async Task Ac8PersistenceRoundTripStoresReversePathSymmetryOnTrace()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeRoutingAssuranceStateStore store = new();
        FakeClock clock = new();
        Device device = CreateDevice();
        await devices.AddAsync(device);

        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main")],
            staticRoutes:
            [
                Route("10.10.0.0/16", "10.0.0.1", "main"),
                Route("192.168.0.0/16", "10.0.0.2", "main"),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("10.10.0.0/16", "10.0.0.1", "main", immediateGw: "10.0.0.1%ether1"),
            Obs("192.168.0.0/16", "10.0.0.2", "main", immediateGw: "10.0.0.2%ether1"),
        ]);

        UpsertRoutingAssuranceStateUseCase upsert = new(auth, devices, store, clock);
        ApplicationResult<RoutingAssuranceStateView> written = await upsert.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = configuration,
                OperationalState = operational,
                TraceQueries =
                [
                    new RouteResolutionQuery
                    {
                        Family = "ipv4",
                        SourceAddress = "192.168.1.50",
                        DestinationAddress = "10.10.0.20",
                    },
                ],
            });
        Assert.True(written.IsSuccess);

        RoutingAssuranceState? persisted = await store.GetAsync(device.Id);
        Assert.NotNull(persisted);
        RouteResolutionTrace trace = Assert.Single(persisted!.ResolutionTraces);
        Assert.NotNull(trace.ReversePathSymmetry);
        Assert.Equal(ReversePathSymmetryResults.Symmetric, trace.ReversePathSymmetry!.Result);
        Assert.NotNull(trace.ReversePathSymmetry.ReverseTrace);
    }

    private static (RoutingConfigurationSnapshot Configuration, RoutingOperationalSnapshot Operational, RouteResolutionTrace Forward)
        MultiWanAsymmetricFixture()
    {
        RoutingConfigurationSnapshot configuration = Config(
            tables: [Table("main"), Table("wan1")],
            rules:
            [
                Rule(0, RoutingRuleActions.Lookup, src: "10.1.0.0/24", table: "wan1"),
            ],
            staticRoutes:
            [
                Route("0.0.0.0/0", "1.1.1.1", "wan1"),
                Route("0.0.0.0/0", "2.2.2.2", "main"),
            ]);
        RoutingOperationalSnapshot operational = Ops(
        [
            Obs("0.0.0.0/0", "1.1.1.1", "wan1", immediateGw: "1.1.1.1%ether1"),
            Obs("0.0.0.0/0", "2.2.2.2", "main", immediateGw: "2.2.2.2%ether2"),
        ]);

        RouteResolutionTrace forward = Analyze(
            new RouteResolutionQuery
            {
                Family = "ipv4",
                SourceAddress = "10.1.0.5",
                DestinationAddress = "203.0.113.10",
            },
            configuration,
            operational);

        return (configuration, operational, forward);
    }

    private static RouteResolutionTrace Analyze(
        RouteResolutionQuery query,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational)
        => RouteResolutionTraceEngine.Analyze(query, configuration, operational);

    private static RouteExpectation Expectation(
        string destinationPrefix,
        bool requireReversePath = false,
        bool expectAsymmetricReversePath = false)
        => new()
        {
            NodeId = null,
            Family = "ipv4",
            DestinationPrefix = destinationPrefix,
            RequireReversePath = requireReversePath,
            ExpectAsymmetricReversePath = expectAsymmetricReversePath,
        };

    private static RoutingTableFact Table(string name)
        => new() { Name = name, Fib = "yes", Disabled = "false" };

    private static RoutingRuleFact Rule(
        int ordinal,
        string action,
        string? src = null,
        string? dst = null,
        string? table = null)
        => new()
        {
            EffectiveOrdinal = ordinal,
            Action = action,
            SrcAddress = src,
            DstAddress = dst,
            Table = table,
            RoutingMark = null,
            Disabled = "false",
        };

    private static StaticRouteConfigFact Route(string dst, string gateway, string table)
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            Gateway = gateway,
            RoutingTable = table,
            Distance = 1,
            Scope = null,
            TargetScope = null,
            PrefSrc = null,
            CheckGateway = null,
            Disabled = "false",
        };

    private static RouteObservationFact Obs(string dst, string gateway, string table, string? immediateGw = null)
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
            RouteType = null,
            Origin = null,
            HwOffloaded = null,
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
