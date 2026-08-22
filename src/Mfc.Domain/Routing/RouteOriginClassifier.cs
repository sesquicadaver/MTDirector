namespace Mfc.Domain.Routing;

/// <summary>
/// Read-only route origin classifier from RouterOS route observations (M7.1 Spec §10).
/// Uses route <c>type</c>, static/dynamic flags, and connected gateway heuristics.
/// </summary>
public static class RouteOriginClassifier
{
    /// <summary>Classifies an observed route fact into a <see cref="RouteOrigins"/> value.</summary>
    public static string Classify(RouteObservationFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (!fact.IsDynamic)
        {
            if (IsConnectedGateway(fact.Gateway))
            {
                return RouteOrigins.Connected;
            }

            return RouteOrigins.Static;
        }

        string? fromType = MapRouteType(fact.RouteType);
        if (fromType is not null)
        {
            return fromType;
        }

        if (IsConnectedGateway(fact.Gateway))
        {
            return RouteOrigins.Connected;
        }

        return RouteOrigins.Other;
    }

    /// <summary>Classifies an observed default-route fact into a <see cref="RouteOrigins"/> value.</summary>
    public static string Classify(DefaultRouteObservationFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (fact.IsStatic && !fact.IsDynamic)
        {
            if (IsConnectedGateway(fact.Gateway))
            {
                return RouteOrigins.Connected;
            }

            return RouteOrigins.Static;
        }

        if (IsConnectedGateway(fact.Gateway))
        {
            return RouteOrigins.Connected;
        }

        return RouteOrigins.Other;
    }

    /// <summary>Connected routes use an interface name as gateway (no dots or colons).</summary>
    public static bool IsConnectedGateway(string? gateway)
    {
        if (string.IsNullOrWhiteSpace(gateway))
        {
            return false;
        }

        return !gateway.Contains('.', StringComparison.Ordinal)
               && !gateway.Contains(':', StringComparison.Ordinal);
    }

    private static string? MapRouteType(string? routeType)
    {
        if (string.IsNullOrWhiteSpace(routeType))
        {
            return null;
        }

        return routeType.Trim().ToLowerInvariant() switch
        {
            "bgp" => RouteOrigins.Bgp,
            "ospf" => RouteOrigins.Ospf,
            "rip" => RouteOrigins.Rip,
            "dhcp" => RouteOrigins.Dhcp,
            "vpn" or "ipsec" or "wireguard" => RouteOrigins.Vpn,
            "connect" or "connected" => RouteOrigins.Connected,
            "static" => RouteOrigins.Static,
            _ => null,
        };
    }
}
