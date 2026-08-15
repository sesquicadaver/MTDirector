using System.Globalization;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Snapshots;
using Mfc.Domain.Topology;
using Mfc.RouterOs.Discovery;

namespace Mfc.RouterOs.Snapshot;

/// <summary>
/// Projects RouterOS discovery results into menu-specific canonical sections (M1-22).
/// Known schema fields enter configuration/observation hashes; unknown raw properties
/// are retained only in a compatibility observation section (AC#6–7).
/// </summary>
public static class DiscoveryCanonicalProjector
{
    /// <summary>Projects discovery inputs into a hashed canonical device snapshot.</summary>
    public static CanonicalDeviceSnapshot Project(DiscoveryCanonicalInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        List<CanonicalSectionInput> configuration = [];
        List<CanonicalSectionInput> observations = [];
        List<CanonicalRecordInput> unknownPropertyRecords = [];

        if (input.System is { } system)
        {
            ProjectSystem(system, configuration, observations, unknownPropertyRecords);
        }

        if (input.Interfaces is { } interfaces)
        {
            ProjectInterfaces(interfaces, configuration, observations, unknownPropertyRecords);
        }

        if (input.Firewall is { } firewall)
        {
            ProjectFirewall(firewall, configuration, observations, unknownPropertyRecords);
        }

        if (input.Routing is { } routing)
        {
            ProjectRouting(routing, configuration, observations, unknownPropertyRecords);
        }

        if (input.Vrrp is { } vrrp)
        {
            ProjectVrrp(vrrp, configuration, observations, unknownPropertyRecords);
        }

        if (input.BridgeSwitch is { } bridge)
        {
            ProjectBridgeSwitch(bridge, configuration, observations, unknownPropertyRecords);
        }

        if (input.Capabilities is { } capabilities)
        {
            configuration.Add(Singleton(
                CanonicalSectionIds.CapabilitiesDevice,
                CanonicalDomain.Configuration,
                ordered: false,
                BuildCapabilityProperties(capabilities)));
        }

        if (input.PacketPathTopology is { } packetPath)
        {
            ProjectPacketPathMembership(packetPath, configuration);
        }

        if (input.TopologyValidation is { } topology)
        {
            observations.Add(ProjectTopologyValidation(topology));
        }

        if (unknownPropertyRecords.Count > 0)
        {
            observations.Add(new CanonicalSectionInput
            {
                Domain = CanonicalDomain.Observations,
                SectionId = CanonicalSectionIds.CompatibilityUnknownProperties,
                Ordered = false,
                Records = unknownPropertyRecords,
            });
        }

        List<CanonicalSection> configSections = configuration
            .Select(Canonicalizer.Canonicalize)
            .OrderBy(static s => s.SectionId, StringComparer.Ordinal)
            .ToList();
        List<CanonicalSection> obsSections = observations
            .Select(Canonicalizer.Canonicalize)
            .OrderBy(static s => s.SectionId, StringComparer.Ordinal)
            .ToList();

        ConfigurationHash configurationHash = CanonicalHashContract.HashConfiguration(
            configSections.Select(static s => (s.SectionId, CanonicalHashContract.HashSection(s))));
        ObservationHash observationHash = CanonicalHashContract.HashObservations(
            obsSections.Select(static s => (s.SectionId, CanonicalHashContract.HashSection(s))));
        SnapshotHash snapshotHash = CanonicalHashContract.HashSnapshot(
            input.SchemaVersion,
            configurationHash,
            observationHash);

        return new CanonicalDeviceSnapshot
        {
            SchemaVersion = input.SchemaVersion,
            ConfigurationSections = configSections,
            ObservationSections = obsSections,
            ConfigurationHash = configurationHash,
            ObservationHash = observationHash,
            SnapshotHash = snapshotHash,
        };
    }

    private static void ProjectSystem(
        SystemServiceDiscoveryResult system,
        List<CanonicalSectionInput> configuration,
        List<CanonicalSectionInput> observations,
        List<CanonicalRecordInput> unknown)
    {
        configuration.Add(Singleton(
            CanonicalSectionIds.SystemIdentity,
            CanonicalDomain.Configuration,
            ordered: false,
            Props(("name", system.Identity.Name))));

        // Uptime is observation-only.
        observations.Add(Singleton(
            CanonicalSectionIds.SystemResource,
            CanonicalDomain.Observations,
            ordered: false,
            Props(
                ("version", system.Resource.Version),
                ("uptime", system.Resource.Uptime),
                ("architecture-name", system.Resource.ArchitectureName),
                ("board-name", system.Resource.BoardName))));

        configuration.Add(Singleton(
            CanonicalSectionIds.ManagementIpServices,
            CanonicalDomain.Configuration,
            ordered: false,
            Props(
                ("api-ssl.disabled", system.ApiSsl.Found ? (system.ApiSsl.Disabled ? "true" : "false") : null),
                ("api-ssl.port", system.ApiSsl.Port),
                ("api-ssl.address", system.ApiSsl.AddressPrefixes),
                ("api-ssl.certificate", system.ApiSsl.Certificate),
                ("api-ssl.tls-version", system.ApiSsl.TlsVersion))));

        CollectUnknown(unknown, CanonicalSectionIds.SystemIdentity, system.Identity.RawProperties);
        CollectUnknown(unknown, CanonicalSectionIds.SystemResource, system.Resource.RawProperties);
    }

    private static void ProjectInterfaces(
        InterfaceAddressDiscoveryResult interfaces,
        List<CanonicalSectionInput> configuration,
        List<CanonicalSectionInput> observations,
        List<CanonicalRecordInput> unknown)
    {
        List<CanonicalRecordInput> ifaceConfig = [];
        List<CanonicalRecordInput> ifaceObs = [];
        foreach (InterfaceDiscovery iface in interfaces.Interfaces.OrderBy(i => i.Name, StringComparer.Ordinal))
        {
            string name = iface.Name ?? iface.Id ?? "unknown";
            ifaceConfig.Add(Record(Props(
                ("name", name),
                ("type", iface.Type),
                ("mtu", iface.Mtu),
                ("mac-address", iface.MacAddress),
                ("disabled", iface.Disabled),
                ("dynamic", iface.Dynamic))));
            ifaceObs.Add(Record(Props(
                ("name", name),
                ("running", iface.Running),
                ("actual-mtu", iface.ActualMtu),
                ("dynamic", iface.Dynamic))));
            CollectUnknown(unknown, CanonicalSectionIds.NetworkInterfaces, iface.RawProperties);
        }

        configuration.Add(Section(CanonicalSectionIds.NetworkInterfaces, CanonicalDomain.Configuration, ordered: false, ifaceConfig));
        observations.Add(Section(CanonicalSectionIds.NetworkInterfaces, CanonicalDomain.Observations, ordered: false, ifaceObs));

        configuration.Add(Section(
            CanonicalSectionIds.NetworkIpv4Addresses,
            CanonicalDomain.Configuration,
            ordered: false,
            interfaces.Ipv4StaticAddresses
                .OrderBy(a => a.AddressCidr, StringComparer.Ordinal)
                .Select(a => Record(Props(
                    ("address", a.AddressCidr),
                    ("interface", a.Interface),
                    ("network", a.Network),
                    ("disabled", a.Disabled))))
                .ToArray()));

        configuration.Add(Section(
            CanonicalSectionIds.NetworkIpv6Addresses,
            CanonicalDomain.Configuration,
            ordered: false,
            interfaces.Ipv6StaticAddresses
                .OrderBy(a => a.AddressCidr, StringComparer.Ordinal)
                .Select(a => Record(Props(
                    ("address", a.AddressCidr),
                    ("interface", a.Interface),
                    ("disabled", a.Disabled))))
                .ToArray()));

        // Dynamic addresses → observations only.
        observations.Add(Section(
            CanonicalSectionIds.NetworkIpv4Addresses,
            CanonicalDomain.Observations,
            ordered: false,
            interfaces.Ipv4DynamicAddresses
                .OrderBy(a => a.AddressCidr, StringComparer.Ordinal)
                .Select(a => Record(Props(
                    ("address", a.AddressCidr),
                    ("interface", a.Interface),
                    ("dynamic", "true"))))
                .ToArray()));

        configuration.Add(Section(
            CanonicalSectionIds.NetworkInterfaceLists,
            CanonicalDomain.Configuration,
            ordered: false,
            interfaces.ResolvedMembership
                .OrderBy(m => m.ListName, StringComparer.Ordinal)
                .Select(m => Record(Props(
                    ("list", m.ListName),
                    ("members", string.Join(',', m.Members)))))
                .ToArray()));
    }

    private static void ProjectFirewall(
        FirewallFilterDiscoveryResult firewall,
        List<CanonicalSectionInput> configuration,
        List<CanonicalSectionInput> observations,
        List<CanonicalRecordInput> unknown)
    {
        configuration.Add(ProjectOrderedFilter(CanonicalSectionIds.FirewallIpv4Filter, firewall.Ipv4FilterRules, unknown));
        configuration.Add(ProjectOrderedFilter(CanonicalSectionIds.FirewallIpv6Filter, firewall.Ipv6FilterRules, unknown));

        configuration.Add(Section(
            CanonicalSectionIds.FirewallIpv4AddressLists,
            CanonicalDomain.Configuration,
            ordered: false,
            firewall.Ipv4StaticAddressListEntries
                .OrderBy(e => e.List, StringComparer.Ordinal)
                .ThenBy(e => e.AddressCanonical ?? e.Address, StringComparer.Ordinal)
                .Select(e => Record(Props(
                    ("list", e.List),
                    ("address", e.AddressCanonical ?? e.Address),
                    ("disabled", e.Disabled),
                    ("comment", e.Comment))))
                .ToArray()));

        configuration.Add(Section(
            CanonicalSectionIds.FirewallIpv6AddressLists,
            CanonicalDomain.Configuration,
            ordered: false,
            firewall.Ipv6StaticAddressListEntries
                .OrderBy(e => e.List, StringComparer.Ordinal)
                .ThenBy(e => e.AddressCanonical ?? e.Address, StringComparer.Ordinal)
                .Select(e => Record(Props(
                    ("list", e.List),
                    ("address", e.AddressCanonical ?? e.Address),
                    ("disabled", e.Disabled),
                    ("comment", e.Comment))))
                .ToArray()));

        // Dynamic address-list entries → observations only (AC#4).
        observations.Add(Section(
            CanonicalSectionIds.FirewallIpv4AddressLists,
            CanonicalDomain.Observations,
            ordered: false,
            firewall.Ipv4DynamicAddressListSummaries
                .OrderBy(s => s.ListName, StringComparer.Ordinal)
                .Select(s => Record(Props(
                    ("list", s.ListName),
                    ("dynamic-count", s.EntryCount.ToString(CultureInfo.InvariantCulture)),
                    ("dynamic-digest", s.SortedEntryDigestHex))))
                .ToArray()));

        observations.Add(Section(
            CanonicalSectionIds.FirewallIpv6AddressLists,
            CanonicalDomain.Observations,
            ordered: false,
            firewall.Ipv6DynamicAddressListSummaries
                .OrderBy(s => s.ListName, StringComparer.Ordinal)
                .Select(s => Record(Props(
                    ("list", s.ListName),
                    ("dynamic-count", s.EntryCount.ToString(CultureInfo.InvariantCulture)),
                    ("dynamic-digest", s.SortedEntryDigestHex))))
                .ToArray()));
    }

    private static CanonicalSectionInput ProjectOrderedFilter(
        string sectionId,
        IReadOnlyList<FirewallFilterRuleDiscovery> rules,
        List<CanonicalRecordInput> unknown)
    {
        // Preserve RouterOS order by EffectiveOrdinal (AC#1). Dynamic rules stay in order too for observations
        // but configuration uses static rules only with static ordinal.
        List<CanonicalRecordInput> records = [];
        foreach (FirewallFilterRuleDiscovery rule in rules.Where(static r => !r.IsDynamic).OrderBy(static r => r.EffectiveOrdinal))
        {
            records.Add(Record(Props(
                ("ordinal", rule.StaticOrdinal?.ToString(CultureInfo.InvariantCulture)
                             ?? rule.EffectiveOrdinal.ToString(CultureInfo.InvariantCulture)),
                ("chain", rule.Chain),
                ("action", rule.Action),
                ("protocol", rule.Protocol),
                ("src-address", rule.SrcAddress),
                ("dst-address", rule.DstAddress),
                ("connection-state", rule.ConnectionState),
                ("disabled", rule.Disabled),
                ("comment", rule.Comment))));
            CollectUnknown(unknown, sectionId, rule.RawProperties);
            // .id intentionally omitted — CanonicalPropertyRules also strips it.
        }

        return Section(sectionId, CanonicalDomain.Configuration, ordered: true, records);
    }

    private static void ProjectRouting(
        RoutingDependencyDiscoveryResult routing,
        List<CanonicalSectionInput> configuration,
        List<CanonicalSectionInput> observations,
        List<CanonicalRecordInput> unknown)
    {
        configuration.Add(Section(
            CanonicalSectionIds.RoutingTables,
            CanonicalDomain.Configuration,
            ordered: false,
            routing.RoutingTables
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .Select(t => Record(Props(("name", t.Name), ("fib", t.Fib), ("disabled", t.Disabled))))
                .ToArray()));

        configuration.Add(Section(
            CanonicalSectionIds.RoutingRules,
            CanonicalDomain.Configuration,
            ordered: true,
            routing.RoutingRules
                .Where(static r => !r.IsDynamic)
                .OrderBy(static r => r.EffectiveOrdinal)
                .Select(r => Record(Props(
                    ("ordinal", r.EffectiveOrdinal.ToString(CultureInfo.InvariantCulture)),
                    ("action", r.Action),
                    ("table", r.Table),
                    ("routing-mark", r.RoutingMark),
                    ("disabled", r.Disabled))))
                .ToArray()));

        configuration.Add(ProjectStaticRoutes(CanonicalSectionIds.RoutingIpv4StaticRoutes, routing.Ipv4StaticRoutes, unknown));
        configuration.Add(ProjectStaticRoutes(CanonicalSectionIds.RoutingIpv6StaticRoutes, routing.Ipv6StaticRoutes, unknown));

        // Active / gateway status → observations only (AC#2).
        observations.Add(Section(
            CanonicalSectionIds.RoutingIpv4DefaultState,
            CanonicalDomain.Observations,
            ordered: false,
            routing.Ipv4DefaultRouteState.Select(MapDefaultState).ToArray()));
        observations.Add(Section(
            CanonicalSectionIds.RoutingIpv6DefaultState,
            CanonicalDomain.Observations,
            ordered: false,
            routing.Ipv6DefaultRouteState.Select(MapDefaultState).ToArray()));

        observations.Add(Section(
            CanonicalSectionIds.RoutingIpv4StaticRoutes,
            CanonicalDomain.Observations,
            ordered: false,
            routing.Ipv4StaticRoutes
                .Where(static r => !r.IsDynamic)
                .Select(r => Record(Props(
                    ("dst-address", r.DstAddress),
                    ("gateway", r.Gateway),
                    ("active", r.Active),
                    ("gateway-status", r.GatewayStatus),
                    ("immediate-gw", r.ImmediateGateway))))
                .ToArray()));

        configuration.Add(ProjectFacility(CanonicalSectionIds.FirewallIpv4Nat, routing.Ipv4NatRules, unknown));
        configuration.Add(ProjectFacility(CanonicalSectionIds.FirewallIpv6Nat, routing.Ipv6NatRules, unknown));
        configuration.Add(ProjectFacility(CanonicalSectionIds.FirewallIpv4Raw, routing.Ipv4RawRules, unknown));
        configuration.Add(ProjectFacility(CanonicalSectionIds.FirewallIpv6Raw, routing.Ipv6RawRules, unknown));
        configuration.Add(ProjectFacility(CanonicalSectionIds.FirewallIpv4Mangle, routing.Ipv4MangleRules, unknown));
        configuration.Add(ProjectFacility(CanonicalSectionIds.FirewallIpv6Mangle, routing.Ipv6MangleRules, unknown));

        configuration.Add(Singleton(
            CanonicalSectionIds.NetworkIpv4Settings,
            CanonicalDomain.Configuration,
            ordered: false,
            Props(
                ("rp-filter", routing.Ipv4Settings.RpFilter),
                ("ip-forward", routing.Ipv4Settings.IpForward))));
        configuration.Add(Singleton(
            CanonicalSectionIds.NetworkIpv6Settings,
            CanonicalDomain.Configuration,
            ordered: false,
            Props(
                ("forward", routing.Ipv6Settings.Forward),
                ("disable-ipv6", routing.Ipv6Settings.DisableIpv6))));
    }

    private static CanonicalSectionInput ProjectStaticRoutes(
        string sectionId,
        IReadOnlyList<StaticRouteDiscovery> routes,
        List<CanonicalRecordInput> unknown)
    {
        List<CanonicalRecordInput> records = [];
        foreach (StaticRouteDiscovery route in routes
                     .Where(static r => !r.IsDynamic)
                     .OrderBy(r => r.DstAddress, StringComparer.Ordinal)
                     .ThenBy(r => r.Gateway, StringComparer.Ordinal))
        {
            records.Add(Record(Props(
                ("dst-address", route.DstAddress),
                ("gateway", route.Gateway),
                ("routing-table", route.RoutingTable),
                ("distance", route.Distance?.ToString(CultureInfo.InvariantCulture)),
                ("scope", route.Scope?.ToString(CultureInfo.InvariantCulture)),
                ("target-scope", route.TargetScope?.ToString(CultureInfo.InvariantCulture)),
                ("disabled", route.Disabled))));
            // Omit Active / GatewayStatus / ImmediateGateway from configuration.
            CollectUnknown(unknown, sectionId, route.RawProperties);
        }

        return Section(sectionId, CanonicalDomain.Configuration, ordered: false, records);
    }

    private static CanonicalRecordInput MapDefaultState(DefaultRouteStateDiscovery state)
        => Record(Props(
            ("dst-address", state.DstAddress),
            ("gateway", state.Gateway),
            ("routing-table", state.RoutingTable),
            ("distance", state.Distance?.ToString(CultureInfo.InvariantCulture)),
            ("active", state.Active),
            ("immediate-gw", state.ImmediateGateway),
            ("gateway-status", state.GatewayStatus)));

    private static CanonicalSectionInput ProjectFacility(
        string sectionId,
        IReadOnlyList<OrderedFirewallFacilityRuleDiscovery> rules,
        List<CanonicalRecordInput> unknown)
    {
        List<CanonicalRecordInput> records = [];
        foreach (OrderedFirewallFacilityRuleDiscovery rule in rules.OrderBy(static r => r.EffectiveOrdinal))
        {
            records.Add(Record(Props(
                ("ordinal", rule.EffectiveOrdinal.ToString(CultureInfo.InvariantCulture)),
                ("chain", rule.Chain),
                ("action", rule.Action),
                ("disabled", rule.Disabled),
                ("routing-mark", rule.RoutingMark),
                ("new-routing-mark", rule.NewRoutingMark))));
            CollectUnknown(unknown, sectionId, rule.RawProperties);
        }

        return Section(sectionId, CanonicalDomain.Configuration, ordered: true, records);
    }

    private static void ProjectVrrp(
        VrrpDiscoveryResult vrrp,
        List<CanonicalSectionInput> configuration,
        List<CanonicalSectionInput> observations,
        List<CanonicalRecordInput> unknown)
    {
        List<CanonicalRecordInput> configRecords = [];
        List<CanonicalRecordInput> obsRecords = [];
        foreach (VrrpInstanceDiscovery instance in vrrp.Instances
                     .OrderBy(i => i.GroupKey.ToString(), StringComparer.Ordinal))
        {
            configRecords.Add(Record(Props(
                ("group", instance.GroupKey.ToString()),
                ("name", instance.Name),
                ("priority", instance.Priority?.ToString(CultureInfo.InvariantCulture)),
                ("version", instance.Version),
                ("interval", instance.Interval),
                ("preemption-mode", instance.PreemptionMode),
                ("disabled", instance.Disabled),
                ("addresses", string.Join(',', instance.VirtualAddresses.OrderBy(a => a, StringComparer.Ordinal))))));
            // Role separated from configuration (AC#3).
            obsRecords.Add(Record(Props(
                ("group", instance.GroupKey.ToString()),
                ("role", instance.ObservedRole.ToString()),
                ("running", instance.Running),
                ("master", instance.Master),
                ("backup", instance.Backup))));
            CollectUnknown(unknown, CanonicalSectionIds.HaVrrp, instance.RawProperties);
        }

        configuration.Add(Section(CanonicalSectionIds.HaVrrp, CanonicalDomain.Configuration, ordered: false, configRecords));
        observations.Add(Section(CanonicalSectionIds.HaVrrp, CanonicalDomain.Observations, ordered: false, obsRecords));
    }

    private static void ProjectBridgeSwitch(
        BridgeSwitchDiscoveryResult bridge,
        List<CanonicalSectionInput> configuration,
        List<CanonicalSectionInput> observations,
        List<CanonicalRecordInput> unknown)
    {
        configuration.Add(Section(
            CanonicalSectionIds.BridgeInstances,
            CanonicalDomain.Configuration,
            ordered: false,
            bridge.Bridges
                .OrderBy(b => b.Name, StringComparer.Ordinal)
                .Select(b =>
                {
                    CollectUnknown(unknown, CanonicalSectionIds.BridgeInstances, b.RawProperties);
                    return Record(Props(
                        ("name", b.Name),
                        ("vlan-filtering", b.VlanFiltering),
                        ("protocol-mode", b.ProtocolMode),
                        ("pvid", b.Pvid),
                        ("disabled", b.Disabled)));
                })
                .ToArray()));

        observations.Add(Section(
            CanonicalSectionIds.BridgeInstances,
            CanonicalDomain.Observations,
            ordered: false,
            bridge.Bridges
                .OrderBy(b => b.Name, StringComparer.Ordinal)
                .Select(b => Record(Props(("name", b.Name), ("running", b.Running))))
                .ToArray()));

        configuration.Add(Section(
            CanonicalSectionIds.BridgePorts,
            CanonicalDomain.Configuration,
            ordered: false,
            bridge.BridgePorts
                .OrderBy(p => p.Bridge, StringComparer.Ordinal)
                .ThenBy(p => p.Interface, StringComparer.Ordinal)
                .Select(p => Record(Props(
                    ("bridge", p.Bridge),
                    ("interface", p.Interface),
                    ("pvid", p.Pvid),
                    ("hw", p.Hw),
                    ("disabled", p.Disabled))))
                .ToArray()));

        observations.Add(Section(
            CanonicalSectionIds.BridgePorts,
            CanonicalDomain.Observations,
            ordered: false,
            bridge.BridgePorts
                .OrderBy(p => p.Bridge, StringComparer.Ordinal)
                .ThenBy(p => p.Interface, StringComparer.Ordinal)
                .Select(p => Record(Props(
                    ("bridge", p.Bridge),
                    ("interface", p.Interface),
                    ("hw-offload", p.HwOffload))))
                .ToArray()));

        configuration.Add(Singleton(
            CanonicalSectionIds.BridgeSettings,
            CanonicalDomain.Configuration,
            ordered: false,
            Props(
                ("use-ip-firewall", bridge.BridgeSettings.UseIpFirewall),
                ("use-ip-firewall-for-vlan", bridge.BridgeSettings.UseIpFirewallForVlan))));

        configuration.Add(Section(
            CanonicalSectionIds.BridgeVlans,
            CanonicalDomain.Configuration,
            ordered: false,
            bridge.BridgeVlans
                .OrderBy(v => v.Bridge, StringComparer.Ordinal)
                .ThenBy(v => v.VlanIds, StringComparer.Ordinal)
                .Select(v => Record(Props(
                    ("bridge", v.Bridge),
                    ("vlan-ids", v.VlanIds),
                    ("tagged", v.Tagged),
                    ("untagged", v.Untagged),
                    ("disabled", v.Disabled))))
                .ToArray()));

        configuration.Add(Section(
            CanonicalSectionIds.SwitchInstances,
            CanonicalDomain.Configuration,
            ordered: false,
            bridge.EthernetSwitches
                .OrderBy(s => s.Name, StringComparer.Ordinal)
                .Select(s => Record(Props(
                    ("name", s.Name),
                    ("type", s.Type),
                    ("l3-hw-offloading", s.L3HwOffloading))))
                .ToArray()));

        configuration.Add(Section(
            CanonicalSectionIds.SwitchPorts,
            CanonicalDomain.Configuration,
            ordered: false,
            bridge.EthernetSwitchPorts
                .OrderBy(p => p.Switch, StringComparer.Ordinal)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => Record(Props(
                    ("switch", p.Switch),
                    ("name", p.Name),
                    ("vlan-mode", p.VlanMode),
                    ("l3-hw-offloading", p.L3HwOffloading))))
                .ToArray()));
    }

    /// <summary>
    /// Emits minimal N1-05 membership sections from an in-RouterOs packet-path result.
    /// Does not project the full graph, VRF, bridge-VLAN zone graph, or classifier.
    /// </summary>
    private static void ProjectPacketPathMembership(
        PacketPathTopologyResult topology,
        List<CanonicalSectionInput> configuration)
    {
        List<CanonicalRecordInput> edgeRecords = [];
        foreach (PacketPathTopologyEdge edge in topology.Edges
                     .Where(static e => e.Kind == PacketPathEdgeKind.UsesVeth)
                     .OrderBy(e => e.FromKey, StringComparer.Ordinal)
                     .ThenBy(e => e.ToKey, StringComparer.Ordinal))
        {
            if (!TryParseEndpointKey(edge.FromKey, out string endpointKind, out string endpointName))
            {
                continue;
            }

            if (!TryParseVethKey(edge.ToKey, out string vethName))
            {
                continue;
            }

            edgeRecords.Add(Record(Props(
                ("endpoint_kind", endpointKind),
                ("endpoint_name", endpointName),
                ("veth_name", vethName))));
        }

        configuration.Add(Section(
            CanonicalSectionIds.TopologyContainerVeth,
            CanonicalDomain.Configuration,
            ordered: false,
            edgeRecords));

        configuration.Add(Section(
            CanonicalSectionIds.TopologySharedVeth,
            CanonicalDomain.Configuration,
            ordered: false,
            topology.SharedVethNames
                .OrderBy(n => n, StringComparer.Ordinal)
                .Select(n => Record(Props(("veth_name", n))))
                .ToArray()));
    }

    private static bool TryParseEndpointKey(string key, out string endpointKind, out string endpointName)
    {
        endpointKind = string.Empty;
        endpointName = string.Empty;
        const string containerPrefix = "container:";
        const string appPrefix = "app:";
        if (key.StartsWith(containerPrefix, StringComparison.Ordinal))
        {
            endpointKind = "container";
            endpointName = key[containerPrefix.Length..];
            return endpointName.Length > 0;
        }

        if (key.StartsWith(appPrefix, StringComparison.Ordinal))
        {
            endpointKind = "app";
            endpointName = key[appPrefix.Length..];
            return endpointName.Length > 0;
        }

        return false;
    }

    private static bool TryParseVethKey(string key, out string vethName)
    {
        const string prefix = "veth:";
        if (key.StartsWith(prefix, StringComparison.Ordinal) && key.Length > prefix.Length)
        {
            vethName = key[prefix.Length..];
            return true;
        }

        vethName = string.Empty;
        return false;
    }

    private static CanonicalSectionInput ProjectTopologyValidation(NodeTopologyValidationResult topology)
    {
        List<CanonicalRecordInput> records = topology.Findings
            .Select(f => Record(Props(
                ("code", f.Code),
                ("severity", f.Severity.ToString()),
                ("subject", f.Subject),
                ("message", f.Message))))
            .ToList();
        records.Add(Record(Props(
            ("node-id", topology.NodeId.ToString()),
            ("is-valid", topology.IsValid ? "true" : "false"),
            ("uplink-evidence", topology.EffectiveUplinkEvidence.ToString()))));
        return Section(
            CanonicalSectionIds.TopologyValidation,
            CanonicalDomain.Observations,
            ordered: false,
            records);
    }

    private static Dictionary<string, string> BuildCapabilityProperties(CapabilityProfile profile)
    {
        Dictionary<string, string> props = new(StringComparer.Ordinal)
        {
            ["version"] = profile.Version.ToString(),
            ["architecture"] = profile.Architecture.Value,
            ["model"] = profile.Model.Value,
            ["ipv6"] = profile.Ipv6Supported ? "true" : "false",
            ["vrrp"] = profile.VrrpSupported ? "true" : "false",
            ["bridge"] = profile.BridgeSupported ? "true" : "false",
            ["api-ssl-cert"] = profile.ApiSslCertificatePresent ? "true" : "false",
            ["support-state"] = profile.SupportState.ToString(),
            ["manifest-hash"] = profile.CompatibilityManifestHash.ToString(),
            ["packages"] = string.Join(',', profile.Packages),
        };
        return props;
    }

    private static void CollectUnknown(
        List<CanonicalRecordInput> unknown,
        string sectionId,
        IReadOnlyDictionary<string, string>? rawProperties)
    {
        if (rawProperties is null || rawProperties.Count == 0)
        {
            return;
        }

        foreach ((string key, string value) in rawProperties.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            if (CanonicalPropertyRules.IsExcludedFromConfiguration(key))
            {
                continue;
            }

            unknown.Add(Record(Props(
                ("section", sectionId),
                ("property", key),
                ("value", value))));
        }
    }

    private static CanonicalSectionInput Singleton(
        string sectionId,
        CanonicalDomain domain,
        bool ordered,
        IReadOnlyDictionary<string, string> properties)
        => Section(sectionId, domain, ordered, [Record(properties)]);

    private static CanonicalSectionInput Section(
        string sectionId,
        CanonicalDomain domain,
        bool ordered,
        IReadOnlyList<CanonicalRecordInput> records)
        => new()
        {
            Domain = domain,
            SectionId = sectionId,
            Ordered = ordered,
            Records = records,
        };

    private static CanonicalRecordInput Record(IReadOnlyDictionary<string, string> properties)
        => new() { Properties = properties };

    private static Dictionary<string, string> Props(params (string Key, string? Value)[] pairs)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach ((string key, string? value) in pairs)
        {
            if (value is not null)
            {
                map[key] = value;
            }
        }

        return map;
    }
}
