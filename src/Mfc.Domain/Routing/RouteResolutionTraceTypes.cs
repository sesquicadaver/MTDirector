namespace Mfc.Domain.Routing;

/// <summary>Route resolution outcome (M7.1 Spec §4).</summary>
public static class RouteResolutionDecisions
{
    public const string LocalDelivery = "LOCAL_DELIVERY";

    public const string Forward = "FORWARD";

    public const string Blackhole = "BLACKHOLE";

    public const string Prohibit = "PROHIBIT";

    public const string Unreachable = "UNREACHABLE";

    public const string NoRoute = "NO_ROUTE";

    public const string Indeterminate = "INDETERMINATE";
}

/// <summary>Forwarding execution path (M7.1 Spec §4 / §9).</summary>
public static class RouteResolutionExecutionPaths
{
    public const string Cpu = "CPU";

    public const string Hardware = "HARDWARE";

    public const string Mixed = "MIXED";

    public const string Indeterminate = "INDETERMINATE";
}

/// <summary>Trace certainty — ECMP and partial offload may be indeterminate (M7.1 Spec §9).</summary>
public static class RouteResolutionCertainties
{
    public const string Definite = "DEFINITE";

    public const string Indeterminate = "INDETERMINATE";
}

/// <summary>Routing rule actions modeled for policy routing (M7.1 Spec §6).</summary>
public static class RoutingRuleActions
{
    public const string Lookup = "LOOKUP";

    public const string LookupOnly = "LOOKUP_ONLY";

    public const string Drop = "DROP";

    public const string Unreachable = "UNREACHABLE";
}

/// <summary>ECMP next-hop selector when a single hop cannot be chosen (M7.1 Spec §9).</summary>
public static class ImmediateNextHopSelectors
{
    public const string OneOf = "ONE_OF";
}

/// <summary>Probe / flow input for route resolution analysis.</summary>
public sealed class RouteResolutionQuery
{
    public required string Family { get; init; }

    public string? SourceAddress { get; init; }

    public required string DestinationAddress { get; init; }

    public string? IngressInterface { get; init; }

    public string? InitialVrf { get; init; }

    /// <summary>Routing mark assigned before routing decision (e.g. from Mangle probe).</summary>
    public string? RoutingMark { get; init; }

    /// <summary>Optional matched Mangle rule facts from probe input.</summary>
    public MatchedMangleRule? MatchedMangleRule { get; init; }
}

/// <summary>Matched Mangle rule that assigned a routing mark (M7.1 Spec §5).</summary>
public sealed class MatchedMangleRule
{
    public required int Ordinal { get; init; }

    public required string Chain { get; init; }

    public required string AssignedRoutingMark { get; init; }
}

/// <summary>Matched ordered routing rule (M7.1 Spec §6).</summary>
public sealed class MatchedRoutingRule
{
    public required int Ordinal { get; init; }

    public required string Action { get; init; }

    public string? Table { get; init; }

    public string? RoutingMark { get; init; }
}

/// <summary>Route competing in table lookup (M7.1 Spec §7).</summary>
public sealed class RouteCandidate
{
    public required string DstPrefix { get; init; }

    public required string Table { get; init; }

    public required string Gateway { get; init; }

    public int? Distance { get; init; }

    public int? Scope { get; init; }

    public int? TargetScope { get; init; }

    public bool Active { get; init; }

    public bool Selected { get; init; }

    public string? RouteKind { get; init; }
}

/// <summary>Recursive gateway resolution hop (M7.1 Spec §8).</summary>
public sealed class RecursiveResolutionStep
{
    public required string Table { get; init; }

    public required string Target { get; init; }

    public required string ResolvingPrefix { get; init; }

    public int? Scope { get; init; }

    public int? TargetScope { get; init; }

    public string? NextHop { get; init; }

    public string? Interface { get; init; }

    public bool Active { get; init; }
}

/// <summary>Immediate next hop after recursive resolution (M7.1 Spec §4 / §9).</summary>
public sealed class ImmediateNextHop
{
    public string? Gateway { get; init; }

    public string? Interface { get; init; }

    /// <summary><see cref="ImmediateNextHopSelectors.OneOf"/> when part of an ECMP set.</summary>
    public string? Selector { get; init; }
}

/// <summary>Selected route from FIB lookup.</summary>
public sealed class SelectedRoute
{
    public required string DstPrefix { get; init; }

    public required string Table { get; init; }

    public required string Gateway { get; init; }

    public int? Distance { get; init; }

    public string? ImmediateGateway { get; init; }

    public string? RouteKind { get; init; }
}
