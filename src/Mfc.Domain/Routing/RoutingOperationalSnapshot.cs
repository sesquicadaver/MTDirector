namespace Mfc.Domain.Routing;

/// <summary>
/// Structured routing operational snapshot (M7.1 Spec §2 Operational state).
/// Includes active/inactive routes, defaults, immediate gateways, reachability.
/// </summary>
public sealed class RoutingOperationalSnapshot
{
    public IReadOnlyList<RouteObservationFact> Routes { get; }

    public IReadOnlyList<DefaultRouteObservationFact> DefaultRoutes { get; }

    /// <summary>Deterministic key/value material used for <see cref="RoutingAssuranceHashContract.HashOperational"/>.</summary>
    public IReadOnlyDictionary<string, string> HashMaterial { get; }

    public RoutingOperationalSnapshot(
        IReadOnlyList<RouteObservationFact> routes,
        IReadOnlyList<DefaultRouteObservationFact> defaultRoutes,
        IReadOnlyDictionary<string, string> hashMaterial)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(defaultRoutes);
        ArgumentNullException.ThrowIfNull(hashMaterial);
        Routes = routes;
        DefaultRoutes = defaultRoutes;
        HashMaterial = hashMaterial;
    }

    /// <summary>Empty operational shell with empty hash material.</summary>
    public static RoutingOperationalSnapshot Empty { get; } = new(
        [],
        [],
        new Dictionary<string, string>(StringComparer.Ordinal));
}

/// <summary>Observed route runtime fields (active, immediate gateway, reachability).</summary>
public sealed class RouteObservationFact
{
    public required string Family { get; init; }

    public required string? DstAddress { get; init; }

    public required string? RoutingTable { get; init; }

    public required string? Gateway { get; init; }

    public required string? Active { get; init; }

    public required string? ImmediateGateway { get; init; }

    public required string? GatewayStatus { get; init; }

    public required bool IsDynamic { get; init; }

    public required string? HwOffloaded { get; init; }
}

/// <summary>Observed default-route runtime state.</summary>
public sealed class DefaultRouteObservationFact
{
    public required string Family { get; init; }

    public required string? DstAddress { get; init; }

    public required string? RoutingTable { get; init; }

    public required string? Gateway { get; init; }

    public required int? Distance { get; init; }

    public required string? Active { get; init; }

    public required string? ImmediateGateway { get; init; }

    public required string? GatewayStatus { get; init; }

    public required bool IsDynamic { get; init; }

    public required bool IsStatic { get; init; }
}
