using Mfc.Domain;
using Mfc.Domain.Routing;
using Xunit;

namespace Mfc.UnitTests.Routing;

/// <summary>Extra Domain branch coverage for M7.1-03 RouteResolutionTraceEngine.</summary>
public sealed class RouteResolutionTraceCoverageTests
{
    [Fact]
    public void AnalyzeRejectsUnsupportedFamily()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            RouteResolutionTraceEngine.Analyze(
                new RouteResolutionQuery { Family = "ipx", DestinationAddress = "1.2.3.4" },
                EmptyConfig(),
                EmptyOps()));
        Assert.Contains("family", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeRejectsInvalidDestination()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            RouteResolutionTraceEngine.Analyze(
                new RouteResolutionQuery { Family = "ipv4", DestinationAddress = "not-an-ip" },
                EmptyConfig(),
                EmptyOps()));
        Assert.Contains("Destination", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyzeManyPreservesQueryOrder()
    {
        (RoutingConfigurationSnapshot configuration, RoutingOperationalSnapshot operational) = Fixtures(
            [Table("main")],
            [Route("0.0.0.0/0", "1.1.1.1", "main")],
            [Obs("0.0.0.0/0", "1.1.1.1", "main", "1.1.1.1%ether1")]);
        IReadOnlyList<RouteResolutionTrace> traces = RouteResolutionTraceEngine.AnalyzeMany(
            [Query("10.0.0.1"), Query("10.0.0.2")],
            configuration,
            operational);
        Assert.Equal(2, traces.Count);
        Assert.Equal("10.0.0.1", traces[0].DestinationAddress);
        Assert.Equal("10.0.0.2", traces[1].DestinationAddress);
    }

    [Fact]
    public void CustomPolicyDecisionOrderUsesVrfBeforeMain()
    {
        RoutingSettingsFact settings = new()
        {
            PolicyRules = "vrf,main",
            CheckGatewayPingCount = null,
            CheckGatewayPingInterval = null,
            CheckGatewayPingTimeout = null,
            ConnectedInChain = null,
            DynamicInChain = null,
            SingleProcess = null,
        };
        (RoutingConfigurationSnapshot configuration, RoutingOperationalSnapshot operational) = Fixtures(
            [Table("main"), Table("corp-vrf")],
            [Route("10.30.0.0/16", "10.30.0.1", "corp-vrf")],
            [Obs("10.30.0.0/16", "10.30.0.1", "corp-vrf", "10.30.0.1%ipsec1")],
            settings: settings,
            vrfs: [new VrfDefinitionFact { Name = "corp-vrf", Interfaces = "ipsec1", Disabled = "false" }]);
        RouteResolutionTrace trace = RouteResolutionTraceEngine.Analyze(
            Query("10.30.0.5", ingress: "ipsec1"),
            configuration,
            operational);
        Assert.Equal("corp-vrf", trace.SelectedTable);
        Assert.Equal("corp-vrf", trace.SelectedVrf);
    }

    [Fact]
    public void ProhibitRouteYieldsProhibitDecision()
    {
        (RoutingConfigurationSnapshot configuration, RoutingOperationalSnapshot operational) = Fixtures(
            [Table("main")],
            [Route("10.40.0.0/16", "prohibit", "main")],
            [Obs("10.40.0.0/16", "prohibit", "main")]);
        RouteResolutionTrace trace = RouteResolutionTraceEngine.Analyze(
            Query("10.40.0.5"),
            configuration,
            operational);
        Assert.Equal(RouteResolutionDecisions.Prohibit, trace.Decision);
        Assert.Empty(trace.ImmediateNextHops);
    }

    [Fact]
    public void HardwareOffloadedRouteClassifiesHardwareExecutionPath()
    {
        (RoutingConfigurationSnapshot configuration, RoutingOperationalSnapshot operational) = Fixtures(
            [Table("main")],
            [Route("10.50.0.0/16", "10.0.0.1", "main")],
            [Obs("10.50.0.0/16", "10.0.0.1", "main", "10.0.0.1%ether1", hwOffloaded: "true")]);
        RouteResolutionTrace trace = RouteResolutionTraceEngine.Analyze(
            Query("10.50.0.10"),
            configuration,
            operational);
        Assert.Equal(RouteResolutionExecutionPaths.Hardware, trace.ExecutionPath);
    }

    [Fact]
    public void MixedHwAndCpuRoutesClassifyMixedExecutionPath()
    {
        (RoutingConfigurationSnapshot configuration, RoutingOperationalSnapshot operational) = Fixtures(
            [Table("main")],
            [
                Route("10.80.0.0/16", "10.0.0.2", "main"),
                Route("10.80.0.0/16", "10.0.0.3", "main"),
            ],
            [
                Obs("10.80.0.0/16", "10.0.0.2", "main", "10.0.0.2%ether1", hwOffloaded: "true"),
                Obs("10.80.0.0/16", "10.0.0.3", "main", "10.0.0.3%ether2", hwOffloaded: "false"),
            ]);
        RouteResolutionTrace trace = RouteResolutionTraceEngine.Analyze(
            Query("10.80.0.10"),
            configuration,
            operational);
        Assert.Equal(RouteResolutionExecutionPaths.Mixed, trace.ExecutionPath);
        Assert.NotNull(trace.EcmpRouteSet);
        Assert.Single(trace.EcmpRouteSet!.HardwareOffloadedNextHops);
    }

    [Fact]
    public void RoutingRuleWithSourceSelectorMatchesOnlyWhenSourceFits()
    {
        (RoutingConfigurationSnapshot configuration, RoutingOperationalSnapshot operational) = Fixtures(
            [Table("main"), Table("guest")],
            [
                Route("0.0.0.0/0", "1.1.1.1", "main"),
                Route("0.0.0.0/0", "2.2.2.2", "guest"),
            ],
            [
                Obs("0.0.0.0/0", "1.1.1.1", "main", "1.1.1.1%ether1"),
                Obs("0.0.0.0/0", "2.2.2.2", "guest", "2.2.2.2%ether2"),
            ],
            rules: [Rule(0, RoutingRuleActions.Lookup, src: "192.168.50.0/24", table: "guest")]);
        RouteResolutionTrace match = RouteResolutionTraceEngine.Analyze(
            Query("8.8.8.8", source: "192.168.50.10"),
            configuration,
            operational);
        Assert.Equal("guest", match.SelectedTable);

        RouteResolutionTrace miss = RouteResolutionTraceEngine.Analyze(
            Query("8.8.8.8", source: "192.168.1.10"),
            configuration,
            operational);
        Assert.Equal("main", miss.SelectedTable);
    }

    [Fact]
    public void NoMatchingRouteReturnsNoRouteDecision()
    {
        (RoutingConfigurationSnapshot configuration, RoutingOperationalSnapshot operational) = Fixtures([], [], []);
        RouteResolutionTrace trace = RouteResolutionTraceEngine.Analyze(
            Query("203.0.113.1"),
            configuration,
            operational);
        Assert.Equal(RouteResolutionDecisions.NoRoute, trace.Decision);
        Assert.Equal(RouteResolutionExecutionPaths.Indeterminate, trace.ExecutionPath);
    }

    [Fact]
    public void Ipv6FamilyResolvesMainTableRoute()
    {
        (RoutingConfigurationSnapshot configuration, RoutingOperationalSnapshot operational) = Fixtures(
            [Table("main")],
            [Route("2001:db8::/32", "2001:db8::1", "main", family: "ipv6")],
            [Obs("2001:db8::/32", "2001:db8::1", "main", "2001:db8::1%ether1", family: "ipv6")]);
        RouteResolutionTrace trace = RouteResolutionTraceEngine.Analyze(
            new RouteResolutionQuery
            {
                Family = "ipv6",
                SourceAddress = "2001:db8::10",
                DestinationAddress = "2001:db8::20",
            },
            configuration,
            operational);
        Assert.Equal(RouteResolutionDecisions.Forward, trace.Decision);
        Assert.Equal("ipv6", trace.Family);
    }

    private static RoutingConfigurationSnapshot EmptyConfig()
        => Fixtures([], [], []).Configuration;

    private static RoutingOperationalSnapshot EmptyOps()
        => Fixtures([], [], []).Operational;

    private static (RoutingConfigurationSnapshot Configuration, RoutingOperationalSnapshot Operational) Fixtures(
        IReadOnlyList<RoutingTableFact> tables,
        IReadOnlyList<StaticRouteConfigFact> staticRoutes,
        IReadOnlyList<RouteObservationFact> routes,
        RoutingSettingsFact? settings = null,
        IReadOnlyList<RoutingRuleFact>? rules = null,
        IReadOnlyList<VrfDefinitionFact>? vrfs = null)
    {
        Dictionary<string, string> material = new(StringComparer.Ordinal);
        foreach (RoutingTableFact table in tables)
        {
            material[$"rtab.{table.Name}.fib"] = table.Fib ?? string.Empty;
        }

        RoutingConfigurationSnapshot configuration = new(
            tables,
            settings ?? RoutingSettingsFact.Empty,
            rules ?? [],
            vrfs ?? [],
            staticRoutes,
            [],
            [],
            material);
        RoutingOperationalSnapshot operational = new(routes, [], new Dictionary<string, string>(StringComparer.Ordinal));
        return (configuration, operational);
    }

    private static RouteResolutionQuery Query(string destination, string? source = "192.168.0.10", string? ingress = null)
        => new()
        {
            Family = "ipv4",
            SourceAddress = source,
            DestinationAddress = destination,
            IngressInterface = ingress,
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
        string family = "ipv4")
        => new()
        {
            Family = family,
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

    private static RouteObservationFact Obs(
        string dst,
        string gateway,
        string table,
        string? immediateGw = null,
        string? hwOffloaded = null,
        string family = "ipv4")
        => new()
        {
            Family = family,
            DstAddress = dst,
            RoutingTable = table,
            Gateway = gateway,
            Active = "true",
            ImmediateGateway = immediateGw,
            GatewayStatus = "reachable",
            IsDynamic = false,
            HwOffloaded = hwOffloaded,
        };
}
