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

/// <summary>Routing + NAT/RAW/Mangle dependency discovery result (M1-14).</summary>
public sealed class RoutingDependencyDiscoveryResult
{
    public required IReadOnlyList<RoutingTableDiscovery> RoutingTables { get; init; }

    public required IReadOnlyList<RoutingRuleDiscovery> RoutingRules { get; init; }

    public required IReadOnlyList<StaticRouteDiscovery> Ipv4StaticRoutes { get; init; }

    public required IReadOnlyList<StaticRouteDiscovery> Ipv6StaticRoutes { get; init; }

    public required IReadOnlyList<DefaultRouteStateDiscovery> Ipv4DefaultRouteState { get; init; }

    public required IReadOnlyList<DefaultRouteStateDiscovery> Ipv6DefaultRouteState { get; init; }

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

            foreach (RoutingRuleDiscovery rule in RoutingRules.Where(r => !r.IsDynamic).OrderBy(r => r.EffectiveOrdinal))
            {
                string p = $"rrule.{rule.EffectiveOrdinal}";
                Put(material, $"{p}.action", rule.Action);
                Put(material, $"{p}.table", rule.Table);
                Put(material, $"{p}.routing-mark", rule.RoutingMark);
                Put(material, $"{p}.disabled", rule.Disabled);
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
                // Omit Active / GatewayStatus / ImmediateGateway.
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
