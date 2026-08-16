using System.Globalization;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Application.Policies;

/// <summary>
/// Maps canonical routing/NAT/RAW/Mangle/VRRP/settings records onto Domain topology-dependency analysis (M2-14).
/// Does not call RouterOS and does not write NAT/RAW/Mangle/VRRP or disable primary WAN.
/// </summary>
public static class TopologyDependencyContextMapper
{
    public static TopologyDependencyAnalysisResult Analyze(
        TopologyDependencyProfile profile,
        TopologyDependencyCanonicalSections sections)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sections);
        return TopologyDependencyAnalysis.Analyze(FromCanonical(profile, sections));
    }

    public static TopologyDependencyFacts FromCanonical(
        TopologyDependencyProfile profile,
        TopologyDependencyCanonicalSections sections)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sections);
        List<VrrpInstanceFacts> instances = MapVrrpInstances(sections.VrrpConfiguration);
        List<VrrpRoleAssignment> roles = MapVrrpRoles(
            sections.VrrpObservations,
            instances,
            profile.ObservingDeviceId);
        return TopologyDependencyFacts.Create(
            profile.Kind,
            profile.UplinkMode,
            profile.Uplinks,
            instances,
            profile.DeclaredVrrpMemberIds,
            profile.ObservedVrrpMemberIds,
            roles,
            MapTables(sections.RoutingTables),
            MapRoutingRules(sections.RoutingRules),
            RpFilter(sections.Ipv4Settings),
            MapFacility(IpAddressFamily.IPv4, sections.Ipv4Raw)
                .Concat(MapFacility(IpAddressFamily.IPv6, sections.Ipv6Raw))
                .ToArray(),
            MapFacility(IpAddressFamily.IPv4, sections.Ipv4Nat)
                .Concat(MapFacility(IpAddressFamily.IPv6, sections.Ipv6Nat))
                .ToArray(),
            MapFacility(IpAddressFamily.IPv4, sections.Ipv4Mangle)
                .Concat(MapFacility(IpAddressFamily.IPv6, sections.Ipv6Mangle))
                .ToArray(),
            profile.Candidate,
            ResolveSwitchHardwareKnown(profile, sections),
            ResolveSwitchTransitProven(profile, sections),
            MapDefaultRoutes(sections.Ipv4DefaultState, IpAddressFamily.IPv4)
                .Concat(MapDefaultRoutes(sections.Ipv6DefaultState, IpAddressFamily.IPv6))
                .ToArray());
    }

    private static List<VrrpInstanceFacts> MapVrrpInstances(IReadOnlyList<CanonicalRecord> records)
    {
        List<VrrpInstanceFacts> instances = [];
        foreach (CanonicalRecord record in records)
        {
            IReadOnlyDictionary<string, string> properties = record.Properties;
            if (!TryParseVrrpIdentity(properties, out IpAddressFamily family, out byte vrid, out string iface))
            {
                continue;
            }

            ushort port = TopologyDependencyAnalysis.DefaultVrrpSyncPort;
            string? portText = Get(properties, "connection-tracking-port");
            if (!string.IsNullOrWhiteSpace(portText)
                && ushort.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out ushort parsed)
                && parsed != 0)
            {
                port = parsed;
            }

            instances.Add(VrrpInstanceFacts.Create(
                family,
                vrid,
                iface,
                disabled: IsTruthy(Get(properties, "disabled")),
                syncConnectionTracking: IsTruthy(Get(properties, "sync-connection-tracking")),
                syncPort: port,
                remoteAddress: Get(properties, "remote-address")));
        }

        return instances;
    }

    private static List<VrrpRoleAssignment> MapVrrpRoles(
        IReadOnlyList<CanonicalRecord> observations,
        List<VrrpInstanceFacts> instances,
        string deviceId)
    {
        List<VrrpRoleAssignment> roles = [];
        foreach (CanonicalRecord record in observations)
        {
            IReadOnlyDictionary<string, string> properties = record.Properties;
            if (!TryParseVrrpIdentity(properties, out IpAddressFamily family, out byte vrid, out string iface))
            {
                VrrpInstanceFacts? match = instances.Count == 1 ? instances[0] : null;
                if (match is null)
                {
                    continue;
                }

                family = match.Family;
                vrid = match.Vrid;
                iface = match.ParentInterface;
            }

            roles.Add(VrrpRoleAssignment.Create(
                deviceId,
                family,
                vrid,
                iface,
                ParseRole(Get(properties, "role"))));
        }

        return roles;
    }

    private static List<RoutingTableFact> MapTables(IReadOnlyList<CanonicalRecord> records)
    {
        List<RoutingTableFact> tables = [];
        foreach (CanonicalRecord record in records)
        {
            string? name = Get(record.Properties, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            tables.Add(RoutingTableFact.Create(name, IsTruthy(Get(record.Properties, "disabled"))));
        }

        return tables;
    }

    private static List<RoutingRuleFact> MapRoutingRules(IReadOnlyList<CanonicalRecord> records)
    {
        List<RoutingRuleFact> rules = [];
        for (int i = 0; i < records.Count; i++)
        {
            IReadOnlyDictionary<string, string> properties = records[i].Properties;
            int ordinal = i;
            if (properties.TryGetValue("ordinal", out string? ordinalText)
                && int.TryParse(ordinalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                ordinal = parsed;
            }

            rules.Add(RoutingRuleFact.Create(
                ordinal,
                Get(properties, "action"),
                Get(properties, "table"),
                Get(properties, "routing-mark"),
                IsTruthy(Get(properties, "disabled"))));
        }

        return rules;
    }

    private static List<FacilityRuleFact> MapFacility(IpAddressFamily family, IReadOnlyList<CanonicalRecord> records)
    {
        List<FacilityRuleFact> rules = [];
        for (int i = 0; i < records.Count; i++)
        {
            IReadOnlyDictionary<string, string> properties = records[i].Properties;
            int ordinal = i;
            if (properties.TryGetValue("ordinal", out string? ordinalText)
                && int.TryParse(ordinalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                ordinal = parsed;
            }

            string? unsupported = Get(properties, "unsupported-matchers");
            IReadOnlyList<string> matchers = string.IsNullOrWhiteSpace(unsupported)
                ? []
                : unsupported.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            rules.Add(FacilityRuleFact.Create(
                family,
                ordinal,
                Get(properties, "chain"),
                Get(properties, "action"),
                IsTruthy(Get(properties, "disabled")),
                Get(properties, "routing-mark"),
                Get(properties, "new-routing-mark"),
                Get(properties, "per-connection-classifier") ?? Get(properties, "pcc"),
                Get(properties, "connection-mark"),
                Get(properties, "packet-mark"),
                Get(properties, "new-connection-mark"),
                Get(properties, "new-packet-mark"),
                Get(properties, "to-addresses"),
                Get(properties, "to-ports"),
                Get(properties, "connection-state"),
                Get(properties, "connection-nat-state"),
                matchers));
        }

        return rules;
    }

    private static List<DefaultRouteObservation> MapDefaultRoutes(
        IReadOnlyList<CanonicalRecord> records,
        IpAddressFamily family)
    {
        List<DefaultRouteObservation> routes = [];
        foreach (CanonicalRecord record in records)
        {
            IReadOnlyDictionary<string, string> properties = record.Properties;
            routes.Add(DefaultRouteObservation.Create(
                family,
                Get(properties, "routing-table") ?? Get(properties, "table"),
                Get(properties, "gateway"),
                Get(properties, "active"),
                Get(properties, "gateway-status")));
        }

        return routes;
    }

    private static string? RpFilter(IReadOnlyList<CanonicalRecord> settings)
    {
        if (settings.Count == 0)
        {
            return null;
        }

        return Get(settings[0].Properties, "rp-filter");
    }

    private static bool ResolveSwitchHardwareKnown(
        TopologyDependencyProfile profile,
        TopologyDependencyCanonicalSections sections)
    {
        if (profile.Kind != NodeKind.Switch)
        {
            return profile.SwitchHardwareProfileKnown;
        }

        if (sections.SwitchInstances.Count == 0)
        {
            return false;
        }

        return sections.SwitchInstances.All(static r =>
            IsTruthy(Get(r.Properties, "known-chip"))
            && !string.Equals(Get(r.Properties, "type"), "unknown", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(Get(r.Properties, "type")));
    }

    private static bool ResolveSwitchTransitProven(
        TopologyDependencyProfile profile,
        TopologyDependencyCanonicalSections sections)
    {
        if (profile.Kind != NodeKind.Switch)
        {
            return profile.SwitchTransitPathProven;
        }

        bool useIpFirewall = sections.BridgeSettings.Any(static r =>
            IsTruthy(Get(r.Properties, "use-ip-firewall")));
        bool l3Offload = sections.SwitchInstances.Any(static r =>
            IsTruthy(Get(r.Properties, "l3-hw-offloading")));
        return useIpFirewall && !l3Offload && ResolveSwitchHardwareKnown(profile, sections);
    }

    private static bool TryParseVrrpIdentity(
        IReadOnlyDictionary<string, string> properties,
        out IpAddressFamily family,
        out byte vrid,
        out string iface)
    {
        family = IpAddressFamily.IPv4;
        vrid = 0;
        iface = Get(properties, "interface") ?? string.Empty;
        string? familyText = Get(properties, "family");
        string? group = Get(properties, "group");
        if (string.IsNullOrWhiteSpace(familyText) && !string.IsNullOrWhiteSpace(group))
        {
            int slash = group.IndexOf('/');
            familyText = slash > 0 ? group[..slash] : group;
        }

        if (!string.IsNullOrWhiteSpace(familyText)
            && familyText.Contains("ipv6", StringComparison.OrdinalIgnoreCase))
        {
            family = IpAddressFamily.IPv6;
        }

        string? vridText = Get(properties, "vrid");
        if (string.IsNullOrWhiteSpace(vridText) && !string.IsNullOrWhiteSpace(group))
        {
            const string marker = "vrid=";
            int at = group.IndexOf(marker, StringComparison.Ordinal);
            if (at >= 0)
            {
                int start = at + marker.Length;
                int end = group.IndexOf('/', start);
                vridText = end < 0 ? group[start..] : group[start..end];
            }
        }

        if (string.IsNullOrWhiteSpace(iface) && !string.IsNullOrWhiteSpace(group))
        {
            const string marker = "if=";
            int at = group.IndexOf(marker, StringComparison.Ordinal);
            if (at >= 0)
            {
                iface = group[(at + marker.Length)..];
            }
        }

        return !string.IsNullOrWhiteSpace(iface)
               && byte.TryParse(vridText, NumberStyles.Integer, CultureInfo.InvariantCulture, out vrid)
               && vrid != 0;
    }

    private static VrrpMemberObservedState ParseRole(string? role)
    {
        if (string.Equals(role, "Master", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, nameof(VrrpMemberObservedState.Master), StringComparison.OrdinalIgnoreCase))
        {
            return VrrpMemberObservedState.Master;
        }

        if (string.Equals(role, "Backup", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, nameof(VrrpMemberObservedState.Backup), StringComparison.OrdinalIgnoreCase))
        {
            return VrrpMemberObservedState.Backup;
        }

        if (string.Equals(role, "Initializing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Init", StringComparison.OrdinalIgnoreCase))
        {
            return VrrpMemberObservedState.Init;
        }

        return VrrpMemberObservedState.Unknown;
    }

    private static string? Get(IReadOnlyDictionary<string, string> properties, string key)
        => properties.TryGetValue(key, out string? value) ? value : null;

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Canonical section records for topology-dependency mapping. Empty lists are allowed.</summary>
public sealed class TopologyDependencyCanonicalSections
{
    public IReadOnlyList<CanonicalRecord> VrrpConfiguration { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> VrrpObservations { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> RoutingTables { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> RoutingRules { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> Ipv4Nat { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> Ipv6Nat { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> Ipv4Raw { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> Ipv6Raw { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> Ipv4Mangle { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> Ipv6Mangle { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> Ipv4Settings { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> Ipv4DefaultState { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> Ipv6DefaultState { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> SwitchInstances { get; init; } = [];

    public IReadOnlyList<CanonicalRecord> BridgeSettings { get; init; } = [];
}
