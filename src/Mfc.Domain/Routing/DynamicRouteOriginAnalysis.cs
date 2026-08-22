namespace Mfc.Domain.Routing;

/// <summary>Detailed fact for one active dynamic route in the operational FIB (M7.1 Spec §10).</summary>
public sealed class DynamicRouteOriginFact
{
    public required string Family { get; init; }

    public required string? DstAddress { get; init; }

    public required string? RoutingTable { get; init; }

    public required string? Gateway { get; init; }

    public required string Origin { get; init; }

    public required string? RouteType { get; init; }

    public required string? Active { get; init; }

    public required string? ImmediateGateway { get; init; }
}

/// <summary>Per-table origin counts over the operational route snapshot (M7.1 Spec §10).</summary>
public sealed class DynamicRouteOriginTableSummary
{
    public required string Table { get; init; }

    public required IReadOnlyDictionary<string, int> CountsByOrigin { get; init; }
}

/// <summary>
/// Read-only analysis of dynamic route origins from operational observations (M7.1-05).
/// Full BGP tables are never loaded — only routes already present in the FIB snapshot.
/// </summary>
public sealed class DynamicRouteOriginAnalysis
{
    public IReadOnlyList<DynamicRouteOriginTableSummary> TableSummaries { get; }

    public IReadOnlyList<DynamicRouteOriginFact> ActiveDynamicRoutes { get; }

    public DynamicRouteOriginAnalysis(
        IReadOnlyList<DynamicRouteOriginTableSummary> tableSummaries,
        IReadOnlyList<DynamicRouteOriginFact> activeDynamicRoutes)
    {
        ArgumentNullException.ThrowIfNull(tableSummaries);
        ArgumentNullException.ThrowIfNull(activeDynamicRoutes);
        TableSummaries = tableSummaries;
        ActiveDynamicRoutes = activeDynamicRoutes;
    }

    public static DynamicRouteOriginAnalysis Empty { get; } = new([], []);
}

/// <summary>Builds <see cref="DynamicRouteOriginAnalysis"/> from operational route facts.</summary>
public static class DynamicRouteOriginAnalyzer
{
    /// <summary>
    /// Analyzes route observations and default-route state into per-table summaries
    /// and detailed facts for active dynamic routes only.
    /// </summary>
    public static DynamicRouteOriginAnalysis Analyze(
        IReadOnlyList<RouteObservationFact> routes,
        IReadOnlyList<DefaultRouteObservationFact> defaultRoutes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(defaultRoutes);
        if (routes.Count == 0 && defaultRoutes.Count == 0)
        {
            return DynamicRouteOriginAnalysis.Empty;
        }

        Dictionary<string, Dictionary<string, int>> tableCounts = new(StringComparer.Ordinal);
        List<DynamicRouteOriginFact> activeDynamic = [];

        foreach (RouteObservationFact route in routes)
        {
            string table = route.RoutingTable ?? "main";
            string origin = route.Origin ?? RouteOriginClassifier.Classify(route);
            Increment(tableCounts, table, origin);

            if (route.IsDynamic && IsActive(route.Active))
            {
                activeDynamic.Add(ToFact(route, origin));
            }
        }

        HashSet<string> routeKeys = routes
            .Select(static r => RouteKey(r.Family, r.RoutingTable, r.DstAddress, r.Gateway))
            .ToHashSet(StringComparer.Ordinal);

        foreach (DefaultRouteObservationFact route in defaultRoutes)
        {
            string key = RouteKey(route.Family, route.RoutingTable, route.DstAddress, route.Gateway);
            if (routeKeys.Contains(key))
            {
                continue;
            }

            string table = route.RoutingTable ?? "main";
            string origin = route.Origin ?? RouteOriginClassifier.Classify(route);
            Increment(tableCounts, table, origin);

            if (route.IsDynamic && IsActive(route.Active))
            {
                activeDynamic.Add(new DynamicRouteOriginFact
                {
                    Family = route.Family,
                    DstAddress = route.DstAddress,
                    RoutingTable = route.RoutingTable,
                    Gateway = route.Gateway,
                    Origin = origin,
                    RouteType = null,
                    Active = route.Active,
                    ImmediateGateway = route.ImmediateGateway,
                });
            }
        }

        DynamicRouteOriginTableSummary[] summaries = tableCounts
            .OrderBy(static kv => kv.Key, StringComparer.Ordinal)
            .Select(static kv => new DynamicRouteOriginTableSummary
            {
                Table = kv.Key,
                CountsByOrigin = kv.Value
                    .OrderBy(static o => o.Key, StringComparer.Ordinal)
                    .ToDictionary(static o => o.Key, static o => o.Value, StringComparer.Ordinal),
            })
            .ToArray();

        DynamicRouteOriginFact[] orderedFacts = activeDynamic
            .OrderBy(static f => f.Family, StringComparer.Ordinal)
            .ThenBy(static f => f.RoutingTable ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static f => f.DstAddress ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static f => f.Gateway ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        return new DynamicRouteOriginAnalysis(summaries, orderedFacts);
    }

    /// <summary>Returns a snapshot copy with origin analysis populated when absent.</summary>
    public static RoutingOperationalSnapshot EnsureAnalysis(RoutingOperationalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.DynamicRouteOrigins is not null)
        {
            return snapshot;
        }

        return new RoutingOperationalSnapshot(
            snapshot.Routes,
            snapshot.DefaultRoutes,
            snapshot.HashMaterial,
            Analyze(snapshot.Routes, snapshot.DefaultRoutes));
    }

    private static DynamicRouteOriginFact ToFact(RouteObservationFact route, string origin)
        => new()
        {
            Family = route.Family,
            DstAddress = route.DstAddress,
            RoutingTable = route.RoutingTable,
            Gateway = route.Gateway,
            Origin = origin,
            RouteType = route.RouteType,
            Active = route.Active,
            ImmediateGateway = route.ImmediateGateway,
        };

    private static void Increment(
        Dictionary<string, Dictionary<string, int>> tableCounts,
        string table,
        string origin)
    {
        if (!tableCounts.TryGetValue(table, out Dictionary<string, int>? counts))
        {
            counts = new Dictionary<string, int>(StringComparer.Ordinal);
            tableCounts[table] = counts;
        }

        counts.TryGetValue(origin, out int current);
        counts[origin] = current + 1;
    }

    private static bool IsActive(string? active)
        => !string.Equals(active?.Trim(), "false", StringComparison.OrdinalIgnoreCase);

    private static string RouteKey(string family, string? table, string? dst, string? gateway)
        => $"{family}|{table ?? "main"}|{dst ?? string.Empty}|{gateway ?? string.Empty}";
}
