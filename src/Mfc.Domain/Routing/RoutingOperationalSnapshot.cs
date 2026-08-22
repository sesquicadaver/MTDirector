namespace Mfc.Domain.Routing;

/// <summary>
/// Structured routing operational snapshot (M7.1 Spec §2 Operational state).
/// Includes active/inactive routes, defaults, immediate gateways, reachability.
/// </summary>
public sealed class RoutingOperationalSnapshot
{
    public IReadOnlyList<RouteObservationFact> Routes { get; }

    public IReadOnlyList<DefaultRouteObservationFact> DefaultRoutes { get; }

    /// <summary>Read-only dynamic route origin analysis (M7.1-05); computed when omitted.</summary>
    public DynamicRouteOriginAnalysis DynamicRouteOrigins { get; }

    /// <summary>Deterministic key/value material used for <see cref="RoutingAssuranceHashContract.HashOperational"/>.</summary>
    public IReadOnlyDictionary<string, string> HashMaterial { get; }

    public RoutingOperationalSnapshot(
        IReadOnlyList<RouteObservationFact> routes,
        IReadOnlyList<DefaultRouteObservationFact> defaultRoutes,
        IReadOnlyDictionary<string, string> hashMaterial,
        DynamicRouteOriginAnalysis? dynamicRouteOrigins = null)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(defaultRoutes);
        ArgumentNullException.ThrowIfNull(hashMaterial);
        Routes = routes;
        DefaultRoutes = defaultRoutes;
        HashMaterial = hashMaterial;
        DynamicRouteOrigins = dynamicRouteOrigins
                            ?? DynamicRouteOriginAnalyzer.Analyze(routes, defaultRoutes);
    }

    /// <summary>Empty operational shell with empty hash material.</summary>
    public static RoutingOperationalSnapshot Empty { get; } = new(
        [],
        [],
        new Dictionary<string, string>(StringComparer.Ordinal),
        DynamicRouteOriginAnalysis.Empty);
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

    /// <summary>RouterOS route <c>type</c> field when present (M7.1-05).</summary>
    public string? RouteType { get; init; }

    /// <summary>Classified route origin (<see cref="RouteOrigins"/>); stored when mapped from discovery.</summary>
    public string? Origin { get; init; }
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

    /// <summary>RouterOS route <c>type</c> field when present (M7.1-05).</summary>
    public string? RouteType { get; init; }

    /// <summary>Classified route origin (<see cref="RouteOrigins"/>); stored when mapped from discovery.</summary>
    public string? Origin { get; init; }
}
