namespace Mfc.Domain.Routing;

/// <summary>
/// Builds <see cref="EcmpRouteSet"/> from route resolution outcomes (M7.1-04 / Spec §9).
/// </summary>
public static class EcmpRouteSetBuilder
{
    /// <summary>One selected ECMP member with its resolved immediate next hop and operational flags.</summary>
    public sealed record Member(
        bool Active,
        bool HardwareOffloaded,
        ImmediateNextHop ResolvedNextHop);

    /// <summary>
    /// Returns null when the outcome is not multi-path ECMP forward (single hop or non-forward).
    /// </summary>
    public static EcmpRouteSet? Build(
        RouteResolutionQuery query,
        string? selectedTable,
        string? matchedPrefix,
        string? decision,
        IReadOnlyList<Member> members)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(members);
        if (members.Count <= 1
            || !string.Equals(decision, RouteResolutionDecisions.Forward, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(selectedTable))
        {
            return null;
        }

        List<EcmpNextHop> nextHops = members
            .Select(static m => ToNextHop(m.ResolvedNextHop))
            .OrderBy(static h => h.Gateway ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static h => h.Interface ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        List<EcmpNextHop> activeNextHops = members
            .Where(static m => m.Active)
            .Select(static m => ToNextHop(m.ResolvedNextHop))
            .OrderBy(static h => h.Gateway ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static h => h.Interface ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        List<EcmpNextHop> hardwareOffloadedNextHops = members
            .Where(static m => m.HardwareOffloaded)
            .Select(static m => ToNextHop(m.ResolvedNextHop))
            .OrderBy(static h => h.Gateway ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static h => h.Interface ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        return new EcmpRouteSet
        {
            Destination = query.DestinationAddress,
            Table = selectedTable,
            NextHops = nextHops,
            ActiveNextHops = activeNextHops,
            HardwareOffloadedNextHops = hardwareOffloadedNextHops,
            HashingContext = CreateHashingContext(query),
        };
    }

    /// <summary>Deterministic flow-key shell from probe inputs; not RouterOS per-packet hash.</summary>
    public static EcmpHashingContext CreateHashingContext(RouteResolutionQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Dictionary<string, string> material = new(StringComparer.Ordinal)
        {
            ["ecmp.flow.family"] = query.Family.Trim().ToLowerInvariant(),
            ["ecmp.flow.destination"] = query.DestinationAddress.Trim(),
            ["ecmp.flow.source"] = query.SourceAddress?.Trim() ?? string.Empty,
            ["ecmp.flow.ingress"] = query.IngressInterface?.Trim() ?? string.Empty,
            ["ecmp.flow.routing_mark"] = query.RoutingMark?.Trim() ?? string.Empty,
        };

        return new EcmpHashingContext
        {
            Family = query.Family,
            SourceAddress = query.SourceAddress,
            DestinationAddress = query.DestinationAddress,
            IngressInterface = query.IngressInterface,
            RoutingMark = query.RoutingMark,
            FlowKeyMaterial = material
                .OrderBy(static kv => kv.Key, StringComparer.Ordinal)
                .ToDictionary(static kv => kv.Key, static kv => kv.Value, StringComparer.Ordinal),
        };
    }

    private static EcmpNextHop ToNextHop(ImmediateNextHop hop)
        => new()
        {
            Gateway = hop.Gateway,
            Interface = hop.Interface,
        };
}
