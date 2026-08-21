namespace Mfc.RouterOs.Discovery;

public enum OrderedFirewallFacility : byte
{
    Nat = 1,
    Raw = 2,
    Mangle = 3,
}

public sealed class RoutingTableDiscovery
{
    public required string? Name { get; init; }

    public required string? Fib { get; init; }

    public required string? Disabled { get; init; }

    public required string? Dynamic { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

public sealed class RoutingRuleDiscovery
{
    public required int EffectiveOrdinal { get; init; }

    public required string? Action { get; init; }

    public required string? SrcAddress { get; init; }

    public required string? DstAddress { get; init; }

    public required string? RoutingMark { get; init; }

    public required string? Table { get; init; }

    public required string? Disabled { get; init; }

    public required string? Comment { get; init; }

    public required bool IsDynamic { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Static route configuration with typed metric fields and separated observations.</summary>
public sealed class StaticRouteDiscovery
{
    public required IpAddressFamilyKind Family { get; init; }

    public required string? DstAddress { get; init; }

    public required string? Gateway { get; init; }

    public required string? RoutingTable { get; init; }

    public required int? Distance { get; init; }

    public required int? Scope { get; init; }

    public required int? TargetScope { get; init; }

    public required string? PrefSrc { get; init; }

    public required string? CheckGateway { get; init; }

    public required string? Disabled { get; init; }

    public required string? Comment { get; init; }

    public required bool IsDynamic { get; init; }

    public required string? Active { get; init; }

    public required string? ImmediateGateway { get; init; }

    public required string? GatewayStatus { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Default-route runtime state — observation only.</summary>
public sealed class DefaultRouteStateDiscovery
{
    public required IpAddressFamilyKind Family { get; init; }

    public required string? DstAddress { get; init; }

    public required string? RoutingTable { get; init; }

    public required string? Gateway { get; init; }

    public required int? Distance { get; init; }

    public required string? Active { get; init; }

    public required string? ImmediateGateway { get; init; }

    public required string? GatewayStatus { get; init; }

    public required bool IsDynamic { get; init; }

    public required bool IsStatic { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

public sealed class OrderedFirewallFacilityRuleDiscovery
{
    public required OrderedFirewallFacility Facility { get; init; }

    public required IpAddressFamilyKind Family { get; init; }

    public required int EffectiveOrdinal { get; init; }

    public required string? Chain { get; init; }

    public required string? Action { get; init; }

    public required string? Disabled { get; init; }

    public required string? Comment { get; init; }

    public required string? ConnectionMark { get; init; }

    public required string? PacketMark { get; init; }

    public required string? RoutingMark { get; init; }

    public required string? NewRoutingMark { get; init; }

    public required bool UnsupportedForEditing { get; init; }

    public required IReadOnlyList<string> UnsupportedMatchers { get; init; }

    public required IReadOnlyDictionary<string, string> KnownProperties { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

public sealed class Ipv4SettingsDiscovery
{
    public required string? IpForward { get; init; }

    /// <summary>Reverse-path filter mode for topology validators.</summary>
    public required string? RpFilter { get; init; }

    public required string? AcceptSourceRoute { get; init; }

    public required string? AllowFastPath { get; init; }

    public required string? TcpSyncookies { get; init; }

    public required string? Ipv4FasttrackActive { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

public sealed class Ipv6SettingsDiscovery
{
    public required string? Forward { get; init; }

    public required string? DisableIpv6 { get; init; }

    public required string? AcceptRouterAdvertisements { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Routing decision-order / check-gateway settings (M7.1-02).</summary>
public sealed class RoutingSettingsDiscovery
{
    public required string? PolicyRules { get; init; }

    public required string? CheckGatewayPingCount { get; init; }

    public required string? CheckGatewayPingInterval { get; init; }

    public required string? CheckGatewayPingTimeout { get; init; }

    public required string? ConnectedInChain { get; init; }

    public required string? DynamicInChain { get; init; }

    public required string? SingleProcess { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>VRF definition (M7.1-02).</summary>
public sealed class VrfDiscovery
{
    public required string? Name { get; init; }

    public required string? Interfaces { get; init; }

    public required string? Disabled { get; init; }

    public required bool IsDynamic { get; init; }

    public required string? Inactive { get; init; }

    public required string? Invalid { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Route filter rule (M7.1-02).</summary>
public sealed class RoutingFilterRuleDiscovery
{
    public required int EffectiveOrdinal { get; init; }

    public required string? Chain { get; init; }

    public required string? Rule { get; init; }

    public required string? Disabled { get; init; }

    public required bool IsDynamic { get; init; }

    public required string? Inactive { get; init; }

    public required string? Invalid { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Route filter select-rule (M7.1-02).</summary>
public sealed class RoutingFilterSelectRuleDiscovery
{
    public required int EffectiveOrdinal { get; init; }

    public required string? Chain { get; init; }

    public required string? Rule { get; init; }

    public required string? Disabled { get; init; }

    public required bool IsDynamic { get; init; }

    public required string? Inactive { get; init; }

    public required string? Invalid { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Routing + NAT/RAW/Mangle dependency discovery result (M1-14 / M7.1-02).</summary>
public sealed class RoutingDependencyDiscoveryResult
{
    public required IReadOnlyList<RoutingTableDiscovery> RoutingTables { get; init; }

    public required RoutingSettingsDiscovery RoutingSettings { get; init; }

    public required IReadOnlyList<RoutingRuleDiscovery> RoutingRules { get; init; }

    public required IReadOnlyList<VrfDiscovery> Vrfs { get; init; }

    public required IReadOnlyList<StaticRouteDiscovery> Ipv4StaticRoutes { get; init; }

    public required IReadOnlyList<StaticRouteDiscovery> Ipv6StaticRoutes { get; init; }

    public required IReadOnlyList<DefaultRouteStateDiscovery> Ipv4DefaultRouteState { get; init; }

    public required IReadOnlyList<DefaultRouteStateDiscovery> Ipv6DefaultRouteState { get; init; }

    public required IReadOnlyList<RoutingFilterRuleDiscovery> RoutingFilterRules { get; init; }

    public required IReadOnlyList<RoutingFilterSelectRuleDiscovery> RoutingFilterSelectRules { get; init; }

    public required IReadOnlyList<OrderedFirewallFacilityRuleDiscovery> Ipv4NatRules { get; init; }

    public required IReadOnlyList<OrderedFirewallFacilityRuleDiscovery> Ipv6NatRules { get; init; }

    public required IReadOnlyList<OrderedFirewallFacilityRuleDiscovery> Ipv4RawRules { get; init; }

    public required IReadOnlyList<OrderedFirewallFacilityRuleDiscovery> Ipv6RawRules { get; init; }

    public required IReadOnlyList<OrderedFirewallFacilityRuleDiscovery> Ipv4MangleRules { get; init; }

    public required IReadOnlyList<OrderedFirewallFacilityRuleDiscovery> Ipv6MangleRules { get; init; }

    public required Ipv4SettingsDiscovery Ipv4Settings { get; init; }

    public required Ipv6SettingsDiscovery Ipv6Settings { get; init; }

    public required IReadOnlyList<DiscoveryFinding> Findings { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Configuration hash material. Excludes default-route runtime state and gateway reachability.
    /// Includes routing settings, VRF, and filter rules (M7.1-02).
    /// </summary>
    public IReadOnlyDictionary<string, string> ConfigurationHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            foreach (RoutingTableDiscovery table in RoutingTables.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                Put(material, $"rtab.{table.Name}.fib", table.Fib);
                Put(material, $"rtab.{table.Name}.disabled", table.Disabled);
            }

            Put(material, "rsettings.policy-rules", RoutingSettings.PolicyRules);
            Put(material, "rsettings.check-gateway-ping-count", RoutingSettings.CheckGatewayPingCount);
            Put(material, "rsettings.check-gateway-ping-interval", RoutingSettings.CheckGatewayPingInterval);
            Put(material, "rsettings.check-gateway-ping-timeout", RoutingSettings.CheckGatewayPingTimeout);
            Put(material, "rsettings.connected-in-chain", RoutingSettings.ConnectedInChain);
            Put(material, "rsettings.dynamic-in-chain", RoutingSettings.DynamicInChain);
            Put(material, "rsettings.single-process", RoutingSettings.SingleProcess);

            foreach (RoutingRuleDiscovery rule in RoutingRules.Where(r => !r.IsDynamic).OrderBy(r => r.EffectiveOrdinal))
            {
                string p = $"rrule.{rule.EffectiveOrdinal}";
                Put(material, $"{p}.action", rule.Action);
                Put(material, $"{p}.table", rule.Table);
                Put(material, $"{p}.routing-mark", rule.RoutingMark);
                Put(material, $"{p}.disabled", rule.Disabled);
            }

            foreach (VrfDiscovery vrf in Vrfs.Where(v => !v.IsDynamic).OrderBy(v => v.Name, StringComparer.Ordinal))
            {
                string p = $"vrf.{vrf.Name}";
                Put(material, $"{p}.interfaces", vrf.Interfaces);
                Put(material, $"{p}.disabled", vrf.Disabled);
            }

            foreach (StaticRouteDiscovery route in Ipv4StaticRoutes.Concat(Ipv6StaticRoutes)
                         .Where(r => !r.IsDynamic)
                         .OrderBy(r => r.Family)
                         .ThenBy(r => r.DstAddress, StringComparer.Ordinal)
                         .ThenBy(r => r.Gateway, StringComparer.Ordinal))
            {
                string key = $"{(int)route.Family}:{route.RoutingTable}:{route.DstAddress}:{route.Gateway}";
                Put(material, $"route.{key}.distance", route.Distance?.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Put(material, $"route.{key}.scope", route.Scope?.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Put(material, $"route.{key}.target-scope", route.TargetScope?.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Put(material, $"route.{key}.disabled", route.Disabled);
                Put(material, $"route.{key}.check-gateway", route.CheckGateway);
                // Omit Active / GatewayStatus / ImmediateGateway.
            }

            foreach (RoutingFilterRuleDiscovery rule in RoutingFilterRules.Where(r => !r.IsDynamic).OrderBy(r => r.EffectiveOrdinal))
            {
                string p = $"filter.{rule.EffectiveOrdinal}";
                Put(material, $"{p}.chain", rule.Chain);
                Put(material, $"{p}.rule", rule.Rule);
                Put(material, $"{p}.disabled", rule.Disabled);
            }

            foreach (RoutingFilterSelectRuleDiscovery rule in RoutingFilterSelectRules.Where(r => !r.IsDynamic).OrderBy(r => r.EffectiveOrdinal))
            {
                string p = $"filter-select.{rule.EffectiveOrdinal}";
                Put(material, $"{p}.chain", rule.Chain);
                Put(material, $"{p}.rule", rule.Rule);
                Put(material, $"{p}.disabled", rule.Disabled);
            }

            AppendFacility(material, "nat4", Ipv4NatRules);
            AppendFacility(material, "nat6", Ipv6NatRules);
            AppendFacility(material, "raw4", Ipv4RawRules);
            AppendFacility(material, "raw6", Ipv6RawRules);
            AppendFacility(material, "mangle4", Ipv4MangleRules);
            AppendFacility(material, "mangle6", Ipv6MangleRules);

            Put(material, "ip4.rp-filter", Ipv4Settings.RpFilter);
            Put(material, "ip4.ip-forward", Ipv4Settings.IpForward);
            Put(material, "ip6.forward", Ipv6Settings.Forward);
            Put(material, "ip6.disable-ipv6", Ipv6Settings.DisableIpv6);
            return material;
        }
    }

    /// <summary>
    /// Operational hash material: active routes, immediate gateways, reachability, defaults.
    /// Never mixed into <see cref="ConfigurationHashMaterial"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> OperationalHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            foreach (StaticRouteDiscovery route in Ipv4StaticRoutes.Concat(Ipv6StaticRoutes)
                         .OrderBy(r => r.Family)
                         .ThenBy(r => r.DstAddress, StringComparer.Ordinal)
                         .ThenBy(r => r.Gateway, StringComparer.Ordinal))
            {
                string key = $"{(int)route.Family}:{route.RoutingTable}:{route.DstAddress}:{route.Gateway}";
                Put(material, $"route.{key}.active", route.Active);
                Put(material, $"route.{key}.immediate-gw", route.ImmediateGateway);
                Put(material, $"route.{key}.gateway-status", route.GatewayStatus);
                Put(material, $"route.{key}.dynamic", route.IsDynamic ? "true" : "false");
            }

            foreach (DefaultRouteStateDiscovery route in Ipv4DefaultRouteState.Concat(Ipv6DefaultRouteState)
                         .OrderBy(r => r.Family)
                         .ThenBy(r => r.RoutingTable, StringComparer.Ordinal)
                         .ThenBy(r => r.Gateway, StringComparer.Ordinal))
            {
                string key = $"{(int)route.Family}:{route.RoutingTable}:{route.Gateway}";
                Put(material, $"default.{key}.active", route.Active);
                Put(material, $"default.{key}.immediate-gw", route.ImmediateGateway);
                Put(material, $"default.{key}.gateway-status", route.GatewayStatus);
                Put(material, $"default.{key}.distance", route.Distance?.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Put(material, $"default.{key}.dynamic", route.IsDynamic ? "true" : "false");
            }

            return material;
        }
    }

    private static void AppendFacility(
        Dictionary<string, string> material,
        string key,
        IReadOnlyList<OrderedFirewallFacilityRuleDiscovery> rules)
    {
        foreach (OrderedFirewallFacilityRuleDiscovery rule in rules.OrderBy(r => r.EffectiveOrdinal))
        {
            string p = $"{key}.{rule.EffectiveOrdinal}";
            Put(material, $"{p}.chain", rule.Chain);
            Put(material, $"{p}.action", rule.Action);
            Put(material, $"{p}.disabled", rule.Disabled);
            Put(material, $"{p}.routing-mark", rule.RoutingMark);
            Put(material, $"{p}.new-routing-mark", rule.NewRoutingMark);
            Put(material, $"{p}.unsupported", rule.UnsupportedForEditing ? "true" : "false");
        }
    }

    private static void Put(Dictionary<string, string> target, string key, string? value)
    {
        if (value is not null)
        {
            target[key] = value;
        }
    }
}
