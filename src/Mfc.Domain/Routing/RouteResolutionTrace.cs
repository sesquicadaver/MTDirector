namespace Mfc.Domain.Routing;

/// <summary>
/// Route resolution trace for a probe or critical flow (M7.1 Spec §4).
/// Produced by <see cref="RouteResolutionTraceEngine"/>.
/// </summary>
public sealed class RouteResolutionTrace
{
    public required string Family { get; init; }

    public string? SourceAddress { get; init; }

    public string? DestinationAddress { get; init; }

    public string? IngressInterface { get; init; }

    public string? InitialVrf { get; init; }

    public string? RoutingMark { get; init; }

    public IReadOnlyList<string> RoutingDecisionOrder { get; init; } = [];

    public MatchedMangleRule? MatchedMangleRule { get; init; }

    public MatchedRoutingRule? MatchedRoutingRule { get; init; }

    public string? RoutingRuleAction { get; init; }

    public string? SelectedVrf { get; init; }

    public string? SelectedTable { get; init; }

    public string? MatchedPrefix { get; init; }

    public IReadOnlyList<RouteCandidate> RouteCandidates { get; init; } = [];

    public IReadOnlyList<SelectedRoute> SelectedRoutes { get; init; } = [];

    public IReadOnlyList<RecursiveResolutionStep> RecursiveResolution { get; init; } = [];

    public IReadOnlyList<ImmediateNextHop> ImmediateNextHops { get; init; } = [];

    /// <summary>Bounded ECMP next-hop set when multiple equal-cost forward paths exist; null for single-hop or non-forward outcomes (M7.1-04).</summary>
    public EcmpRouteSet? EcmpRouteSet { get; init; }

    public IReadOnlyList<string> EgressInterfaces { get; init; } = [];

    public string? PreferredSource { get; init; }

    public string? Decision { get; init; }

    public string? ExecutionPath { get; init; }

    public string? Certainty { get; init; }

    /// <summary>Reverse B→A symmetry analysis when computed (M7.1-07); null when not analyzed.</summary>
    public ReversePathSymmetryAnalysis? ReversePathSymmetry { get; init; }

    /// <summary>Trace-bound network path latency probes (M7.1-08).</summary>
    public IReadOnlyList<NetworkPathProbeBinding> NetworkPathProbeBindings { get; init; } = [];

    /// <summary>Returns a copy with optional reverse-path symmetry analysis attached.</summary>
    public RouteResolutionTrace WithReversePathSymmetry(ReversePathSymmetryAnalysis? analysis)
        => CopyWith(reversePathSymmetry: analysis);

    /// <summary>Returns a copy with network path probe bindings attached (M7.1-08).</summary>
    public RouteResolutionTrace WithNetworkPathProbeBindings(IReadOnlyList<NetworkPathProbeBinding> bindings)
        => CopyWith(networkPathProbeBindings: bindings);

    private RouteResolutionTrace CopyWith(
        ReversePathSymmetryAnalysis? reversePathSymmetry = null,
        IReadOnlyList<NetworkPathProbeBinding>? networkPathProbeBindings = null)
        => new()
        {
            Family = Family,
            SourceAddress = SourceAddress,
            DestinationAddress = DestinationAddress,
            IngressInterface = IngressInterface,
            InitialVrf = InitialVrf,
            RoutingMark = RoutingMark,
            RoutingDecisionOrder = RoutingDecisionOrder,
            MatchedMangleRule = MatchedMangleRule,
            MatchedRoutingRule = MatchedRoutingRule,
            RoutingRuleAction = RoutingRuleAction,
            SelectedVrf = SelectedVrf,
            SelectedTable = SelectedTable,
            MatchedPrefix = MatchedPrefix,
            RouteCandidates = RouteCandidates,
            SelectedRoutes = SelectedRoutes,
            RecursiveResolution = RecursiveResolution,
            ImmediateNextHops = ImmediateNextHops,
            EcmpRouteSet = EcmpRouteSet,
            EgressInterfaces = EgressInterfaces,
            PreferredSource = PreferredSource,
            Decision = Decision,
            ExecutionPath = ExecutionPath,
            Certainty = Certainty,
            ReversePathSymmetry = reversePathSymmetry ?? ReversePathSymmetry,
            NetworkPathProbeBindings = networkPathProbeBindings ?? NetworkPathProbeBindings,
        };
}
