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

    public IReadOnlyList<string> EgressInterfaces { get; init; } = [];

    public string? PreferredSource { get; init; }

    public string? Decision { get; init; }

    public string? ExecutionPath { get; init; }

    public string? Certainty { get; init; }
}
