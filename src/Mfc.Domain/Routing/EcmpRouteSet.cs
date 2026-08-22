namespace Mfc.Domain.Routing;

/// <summary>
/// Resolved immediate next hop within an ECMP set (M7.1 Spec §9).
/// </summary>
public sealed class EcmpNextHop
{
    public string? Gateway { get; init; }

    public string? Interface { get; init; }
}

/// <summary>
/// Deterministic flow-key shell for ECMP hashing context (M7.1 Spec §9).
/// Mirrors RouterOS hash inputs at a high level; not the full RouterOS ECMP hash algorithm.
/// </summary>
public sealed class EcmpHashingContext
{
    public required string Family { get; init; }

    public string? SourceAddress { get; init; }

    public required string DestinationAddress { get; init; }

    public string? IngressInterface { get; init; }

    public string? RoutingMark { get; init; }

    /// <summary>Ordered flow-key material derived from probe inputs (family, src, dst, ingress, routing-mark).</summary>
    public IReadOnlyDictionary<string, string> FlowKeyMaterial { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// Bounded ECMP next-hop set for packet-path ONE_OF results (M7.1 Spec §9).
/// </summary>
public sealed class EcmpRouteSet
{
    public required string Destination { get; init; }

    public required string Table { get; init; }

    public IReadOnlyList<EcmpNextHop> NextHops { get; init; } = [];

    public IReadOnlyList<EcmpNextHop> ActiveNextHops { get; init; } = [];

    public IReadOnlyList<EcmpNextHop> HardwareOffloadedNextHops { get; init; } = [];

    public required EcmpHashingContext HashingContext { get; init; }
}
