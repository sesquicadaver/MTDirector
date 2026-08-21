using System.Globalization;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Maps <see cref="RoutingDependencyDiscoveryResult"/> into Domain <see cref="RoutingAssuranceState"/> (M7.1-02).
/// Domain remains free of RouterOS types; this adapter lives in RouterOs.
/// </summary>
public static class RoutingAssuranceStateMapper
{
    /// <summary>
    /// Builds a persistable routing assurance state from discovery output.
    /// Deferred slots remain empty typed collections.
    /// </summary>
    public static RoutingAssuranceState ToState(
        DeviceId deviceId,
        RoutingDependencyDiscoveryResult discovery,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        RoutingConfigurationSnapshot configuration = ToConfiguration(discovery);
        RoutingOperationalSnapshot operational = ToOperational(discovery);
        return RoutingAssuranceState.Create(deviceId, configuration, operational, updatedAtUtc);
    }

    private static RoutingConfigurationSnapshot ToConfiguration(RoutingDependencyDiscoveryResult discovery)
    {
        RoutingTableFact[] tables = discovery.RoutingTables
            .Select(static t => new RoutingTableFact
            {
                Name = t.Name,
                Fib = t.Fib,
                Disabled = t.Disabled,
            })
            .ToArray();

        RoutingSettingsFact settings = new()
        {
            PolicyRules = discovery.RoutingSettings.PolicyRules,
            CheckGatewayPingCount = discovery.RoutingSettings.CheckGatewayPingCount,
            CheckGatewayPingInterval = discovery.RoutingSettings.CheckGatewayPingInterval,
            CheckGatewayPingTimeout = discovery.RoutingSettings.CheckGatewayPingTimeout,
            ConnectedInChain = discovery.RoutingSettings.ConnectedInChain,
            DynamicInChain = discovery.RoutingSettings.DynamicInChain,
            SingleProcess = discovery.RoutingSettings.SingleProcess,
        };

        RoutingRuleFact[] rules = discovery.RoutingRules
            .Where(static r => !r.IsDynamic)
            .Select(static r => new RoutingRuleFact
            {
                EffectiveOrdinal = r.EffectiveOrdinal,
                Action = r.Action,
                SrcAddress = r.SrcAddress,
                DstAddress = r.DstAddress,
                RoutingMark = r.RoutingMark,
                Table = r.Table,
                Disabled = r.Disabled,
            })
            .ToArray();

        VrfDefinitionFact[] vrfs = discovery.Vrfs
            .Where(static v => !v.IsDynamic)
            .Select(static v => new VrfDefinitionFact
            {
                Name = v.Name,
                Interfaces = v.Interfaces,
                Disabled = v.Disabled,
            })
            .ToArray();

        StaticRouteConfigFact[] staticRoutes = discovery.Ipv4StaticRoutes
            .Concat(discovery.Ipv6StaticRoutes)
            .Where(static r => !r.IsDynamic)
            .Select(static r => new StaticRouteConfigFact
            {
                Family = FamilyName(r.Family),
                DstAddress = r.DstAddress,
                Gateway = r.Gateway,
                RoutingTable = r.RoutingTable,
                Distance = r.Distance,
                Scope = r.Scope,
                TargetScope = r.TargetScope,
                PrefSrc = r.PrefSrc,
                CheckGateway = r.CheckGateway,
                Disabled = r.Disabled,
            })
            .ToArray();

        RouteFilterRuleFact[] filterRules = discovery.RoutingFilterRules
            .Where(static r => !r.IsDynamic)
            .Select(static r => new RouteFilterRuleFact
            {
                EffectiveOrdinal = r.EffectiveOrdinal,
                Chain = r.Chain,
                Rule = r.Rule,
                Disabled = r.Disabled,
            })
            .ToArray();

        RouteFilterSelectRuleFact[] filterSelectRules = discovery.RoutingFilterSelectRules
            .Where(static r => !r.IsDynamic)
            .Select(static r => new RouteFilterSelectRuleFact
            {
                EffectiveOrdinal = r.EffectiveOrdinal,
                Chain = r.Chain,
                Rule = r.Rule,
                Disabled = r.Disabled,
            })
            .ToArray();

        // Assurance-focused material: tables/settings/rules/VRF/routes/filters only (no NAT/RAW/Mangle).
        Dictionary<string, string> material = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in discovery.ConfigurationHashMaterial)
        {
            if (pair.Key.StartsWith("nat", StringComparison.Ordinal)
                || pair.Key.StartsWith("raw", StringComparison.Ordinal)
                || pair.Key.StartsWith("mangle", StringComparison.Ordinal)
                || pair.Key.StartsWith("ip4.", StringComparison.Ordinal)
                || pair.Key.StartsWith("ip6.", StringComparison.Ordinal))
            {
                continue;
            }

            material[pair.Key] = pair.Value;
        }

        return new RoutingConfigurationSnapshot(
            tables,
            settings,
            rules,
            vrfs,
            staticRoutes,
            filterRules,
            filterSelectRules,
            material);
    }

    private static RoutingOperationalSnapshot ToOperational(RoutingDependencyDiscoveryResult discovery)
    {
        RouteObservationFact[] routes = discovery.Ipv4StaticRoutes
            .Concat(discovery.Ipv6StaticRoutes)
            .Select(static r => new RouteObservationFact
            {
                Family = FamilyName(r.Family),
                DstAddress = r.DstAddress,
                RoutingTable = r.RoutingTable,
                Gateway = r.Gateway,
                Active = r.Active,
                ImmediateGateway = r.ImmediateGateway,
                GatewayStatus = r.GatewayStatus,
                IsDynamic = r.IsDynamic,
                HwOffloaded = null,
            })
            .ToArray();

        DefaultRouteObservationFact[] defaults = discovery.Ipv4DefaultRouteState
            .Concat(discovery.Ipv6DefaultRouteState)
            .Select(static r => new DefaultRouteObservationFact
            {
                Family = FamilyName(r.Family),
                DstAddress = r.DstAddress,
                RoutingTable = r.RoutingTable,
                Gateway = r.Gateway,
                Distance = r.Distance,
                Active = r.Active,
                ImmediateGateway = r.ImmediateGateway,
                GatewayStatus = r.GatewayStatus,
                IsDynamic = r.IsDynamic,
                IsStatic = r.IsStatic,
            })
            .ToArray();

        return new RoutingOperationalSnapshot(
            routes,
            defaults,
            discovery.OperationalHashMaterial.ToDictionary(static kv => kv.Key, static kv => kv.Value, StringComparer.Ordinal));
    }

    private static string FamilyName(IpAddressFamilyKind family)
        => family switch
        {
            IpAddressFamilyKind.Ipv4 => "ipv4",
            IpAddressFamilyKind.Ipv6 => "ipv6",
            _ => ((int)family).ToString(CultureInfo.InvariantCulture),
        };
}
