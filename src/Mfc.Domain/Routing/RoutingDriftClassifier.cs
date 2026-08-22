namespace Mfc.Domain.Routing;

/// <summary>
/// Classifies a single hash-material key change into M7.1 Spec §14 drift categories.
/// Deterministic and side-effect free.
/// </summary>
public static class RoutingDriftClassifier
{
    private static readonly HashSet<string> StaticRouteConfigSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "distance",
        "scope",
        "target-scope",
        "disabled",
        "check-gateway",
        "suppress-hw-offload",
    };

    /// <summary>
    /// Classifies a hash-material key. Set <paramref name="isConfigurationMaterial"/> when the key
    /// originates from <see cref="RoutingConfigurationSnapshot.HashMaterial"/>.
    /// </summary>
    public static RoutingDriftKind ClassifyMaterialKey(string materialKey, bool isConfigurationMaterial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialKey);
        string key = materialKey.Trim();

        if (isConfigurationMaterial)
        {
            return ClassifyConfigurationKey(key);
        }

        return ClassifyOperationalKey(key);
    }

    /// <summary>
    /// Classifies an operational material change with optional previous/current values
    /// (e.g. gateway-status → unreachable).
    /// </summary>
    public static RoutingDriftKind ClassifyOperationalChange(
        string materialKey,
        string? previousValue,
        string? currentValue)
    {
        string key = materialKey.Trim();
        if (key.StartsWith("route.", StringComparison.Ordinal)
            && string.Equals(LeafSegment(key), "gateway-status", StringComparison.OrdinalIgnoreCase))
        {
            return IsGatewayUnreachableTransition(previousValue, currentValue)
                ? RoutingDriftKind.GatewayUnreachable
                : RoutingDriftKind.ActiveRouteChanged;
        }

        return ClassifyOperationalKey(key);
    }

    /// <summary>Returns true when the key belongs to configuration hash material conventions.</summary>
    public static bool IsConfigurationMaterialKey(string materialKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialKey);
        string key = materialKey.Trim();
        if (key.StartsWith("default.", StringComparison.Ordinal))
        {
            return false;
        }

        if (key.StartsWith("route.", StringComparison.Ordinal))
        {
            string leaf = LeafSegment(key);
            return StaticRouteConfigSuffixes.Contains(leaf)
                   || RoutingAssurancePropertyClassifier.ClassifyPropertyName(leaf)
                       == RoutingAssurancePropertyKind.Configuration;
        }

        return key.StartsWith("rtab.", StringComparison.Ordinal)
               || key.StartsWith("rsettings.", StringComparison.Ordinal)
               || key.StartsWith("rrule.", StringComparison.Ordinal)
               || key.StartsWith("vrf.", StringComparison.Ordinal)
               || key.StartsWith("filter.", StringComparison.Ordinal)
               || key.StartsWith("filter-select.", StringComparison.Ordinal)
               || key.StartsWith("nat", StringComparison.Ordinal)
               || key.StartsWith("raw", StringComparison.Ordinal)
               || key.StartsWith("mangle", StringComparison.Ordinal)
               || key.StartsWith("ip4.", StringComparison.Ordinal)
               || key.StartsWith("ip6.", StringComparison.Ordinal);
    }

    private static RoutingDriftKind ClassifyConfigurationKey(string key)
    {
        if (key.StartsWith("rtab.", StringComparison.Ordinal))
        {
            return RoutingDriftKind.RoutingTableChanged;
        }

        if (key.StartsWith("rsettings.", StringComparison.Ordinal))
        {
            return RoutingDriftKind.RoutingSettingsChanged;
        }

        if (key.StartsWith("rrule.", StringComparison.Ordinal))
        {
            return RoutingDriftKind.RoutingRuleChanged;
        }

        if (key.StartsWith("vrf.", StringComparison.Ordinal))
        {
            return RoutingDriftKind.VrfBindingChanged;
        }

        if (key.StartsWith("route.", StringComparison.Ordinal))
        {
            return RoutingDriftKind.StaticRouteChanged;
        }

        if (key.StartsWith("filter.", StringComparison.Ordinal)
            || key.StartsWith("filter-select.", StringComparison.Ordinal))
        {
            return RoutingDriftKind.RouteFilterChanged;
        }

        if (key.StartsWith("nat", StringComparison.Ordinal)
            || key.StartsWith("raw", StringComparison.Ordinal)
            || key.StartsWith("mangle", StringComparison.Ordinal)
            || key.StartsWith("ip4.", StringComparison.Ordinal)
            || key.StartsWith("ip6.", StringComparison.Ordinal))
        {
            return RoutingDriftKind.FirewallRoutingDependencyChanged;
        }

        return RoutingDriftKind.StaticRouteChanged;
    }

    private static RoutingDriftKind ClassifyOperationalKey(string key)
    {
        if (key.StartsWith("default.", StringComparison.Ordinal))
        {
            return RoutingDriftKind.DefaultWanChanged;
        }

        if (!key.StartsWith("route.", StringComparison.Ordinal))
        {
            return RoutingDriftKind.ActiveRouteChanged;
        }

        string leaf = LeafSegment(key);
        return leaf switch
        {
            "active" => RoutingDriftKind.ActiveRouteChanged,
            "gateway-status" => RoutingDriftKind.ActiveRouteChanged,
            "immediate-gw" or "immediate-gateway" => RoutingDriftKind.EcmpMemberChanged,
            "dynamic" or "type" => RoutingDriftKind.DynamicBestPathChanged,
            "hw-offloaded" or "hardware-offloaded" => RoutingDriftKind.RouteExecutionPathChanged,
            "running" or "selected" or "ecmp" => RoutingDriftKind.ProtocolSessionChanged,
            _ => RoutingAssurancePropertyClassifier.ClassifyPropertyName(leaf) == RoutingAssurancePropertyKind.Observation
                ? RoutingDriftKind.ActiveRouteChanged
                : RoutingDriftKind.DynamicBestPathChanged,
        };
    }

    private static bool IsGatewayUnreachableTransition(string? previousValue, string? currentValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return false;
        }

        if (currentValue.Contains("unreachable", StringComparison.OrdinalIgnoreCase))
        {
            return !string.Equals(previousValue, currentValue, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string LeafSegment(string key)
    {
        int lastDot = key.LastIndexOf('.');
        return lastDot >= 0 && lastDot < key.Length - 1 ? key[(lastDot + 1)..] : key;
    }
}
