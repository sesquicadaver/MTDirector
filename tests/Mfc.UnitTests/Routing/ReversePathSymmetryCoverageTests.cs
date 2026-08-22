using Mfc.Domain;
using Mfc.Domain.Routing;
using Xunit;

namespace Mfc.UnitTests.Routing;

/// <summary>Extra Domain branch coverage for M7.1-07 ReversePathSymmetryAnalyzer.</summary>
public sealed class ReversePathSymmetryCoverageTests
{
    [Fact]
    public void AnalyzeRejectsNullForwardTrace()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ReversePathSymmetryAnalyzer.Analyze(
                null!,
                EmptyConfig(),
                EmptyOps()));
    }

    [Fact]
    public void AttachAnalysisRejectsNullArguments()
    {
        RouteResolutionTrace trace = MinimalTrace();
        ReversePathSymmetryAnalysis analysis = new() { Result = ReversePathSymmetryResults.Symmetric };
        Assert.Throws<ArgumentNullException>(() => ReversePathSymmetryAnalyzer.AttachAnalysis(null!, analysis));
        Assert.Throws<ArgumentNullException>(() => ReversePathSymmetryAnalyzer.AttachAnalysis(trace, null!));
    }

    [Fact]
    public void WithReversePathSymmetryCopiesAllFields()
    {
        RouteResolutionTrace trace = MinimalTrace();
        ReversePathSymmetryAnalysis analysis = new()
        {
            Result = ReversePathSymmetryResults.Symmetric,
            Detail = "ok",
        };
        RouteResolutionTrace enriched = trace.WithReversePathSymmetry(analysis);
        Assert.Equal(trace.Family, enriched.Family);
        Assert.Equal(trace.DestinationAddress, enriched.DestinationAddress);
        Assert.Same(analysis, enriched.ReversePathSymmetry);
    }

    [Fact]
    public void IndeterminateWhenForwardTraceDecisionIsIndeterminate()
    {
        RouteResolutionTrace forward = new()
        {
            Family = "ipv4",
            SourceAddress = "10.0.0.1",
            DestinationAddress = "10.0.0.2",
            Decision = RouteResolutionDecisions.Indeterminate,
            Certainty = RouteResolutionCertainties.Indeterminate,
        };

        ReversePathSymmetryAnalysis analysis = ReversePathSymmetryAnalyzer.Analyze(
            forward,
            ConfigWithBidirectionalRoutes(),
            OpsWithBidirectionalRoutes());

        Assert.Equal(ReversePathSymmetryResults.Indeterminate, analysis.Result);
    }

    private static RouteResolutionTrace MinimalTrace()
        => new()
        {
            Family = "ipv4",
            SourceAddress = "10.0.0.1",
            DestinationAddress = "10.0.0.2",
            Decision = RouteResolutionDecisions.Forward,
            Certainty = RouteResolutionCertainties.Definite,
        };

    private static RoutingConfigurationSnapshot ConfigWithBidirectionalRoutes()
        => new(
            [new RoutingTableFact { Name = "main", Fib = "yes", Disabled = "false" }],
            new RoutingSettingsFact
            {
                PolicyRules = "lookup",
                CheckGatewayPingCount = null,
                CheckGatewayPingInterval = null,
                CheckGatewayPingTimeout = null,
                ConnectedInChain = null,
                DynamicInChain = null,
                SingleProcess = null,
            },
            [],
            [],
            [
                new StaticRouteConfigFact
                {
                    Family = "ipv4",
                    DstAddress = "10.0.0.0/24",
                    Gateway = "10.0.0.254",
                    RoutingTable = "main",
                    Distance = 1,
                    Scope = null,
                    TargetScope = null,
                    PrefSrc = null,
                    CheckGateway = null,
                    Disabled = "false",
                },
            ],
            [],
            [],
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static RoutingOperationalSnapshot OpsWithBidirectionalRoutes()
        => new(
            [
                new RouteObservationFact
                {
                    Family = "ipv4",
                    DstAddress = "10.0.0.0/24",
                    RoutingTable = "main",
                    Gateway = "10.0.0.254",
                    Active = "true",
                    ImmediateGateway = "10.0.0.254%ether1",
                    GatewayStatus = "reachable",
                    IsDynamic = false,
                    RouteType = null,
                    Origin = null,
                    HwOffloaded = null,
                },
            ],
            [],
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static RoutingConfigurationSnapshot EmptyConfig()
        => new(
            [],
            new RoutingSettingsFact
            {
                PolicyRules = null,
                CheckGatewayPingCount = null,
                CheckGatewayPingInterval = null,
                CheckGatewayPingTimeout = null,
                ConnectedInChain = null,
                DynamicInChain = null,
                SingleProcess = null,
            },
            [],
            [],
            [],
            [],
            [],
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static RoutingOperationalSnapshot EmptyOps()
        => new([], [], new Dictionary<string, string>(StringComparer.Ordinal));
}
