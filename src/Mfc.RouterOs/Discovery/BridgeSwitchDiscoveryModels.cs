namespace Mfc.RouterOs.Discovery;

/// <summary>High-level L2/L3 path indicators derived from bridge/switch reads (M1-16).</summary>
public enum BridgePathRoleIndicator : byte
{
    /// <summary>Bridge/VLAN config without evidence of HW offload or IP-firewall bridging.</summary>
    L2ForwardingPossible = 0,

    /// <summary><c>use-ip-firewall*</c> enabled — bridged traffic may hit CPU/IP firewall.</summary>
    BridgedTrafficMayHitIpFirewall = 1,

    /// <summary>At least one port reports hardware offload active (observation).</summary>
    HardwareOffloadObserved = 2,

    /// <summary>L3 hardware offloading configured on a switch/port.</summary>
    L3HardwareOffloadConfigured = 3,

    /// <summary>Switch chip type unknown — no implicit write/offload profile.</summary>
    UnknownSwitchChip = 4,
}

/// <summary>One <c>/interface/bridge</c> instance.</summary>
public sealed class BridgeDiscovery
{
    public required string? Name { get; init; }

    public required string? VlanFiltering { get; init; }

    public required string? ProtocolMode { get; init; }

    public required string? Pvid { get; init; }

    public required string? FrameTypes { get; init; }

    public required string? IngressFiltering { get; init; }

    public required string? Mtu { get; init; }

    public required string? Disabled { get; init; }

    public required string? Comment { get; init; }

    public required string? Running { get; init; }

    public required string? RootBridge { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>One <c>/interface/bridge/port</c> row.</summary>
public sealed class BridgePortDiscovery
{
    public required string? Bridge { get; init; }

    public required string? Interface { get; init; }

    public required string? Pvid { get; init; }

    public required string? FrameTypes { get; init; }

    public required string? IngressFiltering { get; init; }

    public required string? Hw { get; init; }

    public required string? Disabled { get; init; }

    /// <summary>Observation: hardware offload state — never configuration hash material.</summary>
    public required string? HwOffload { get; init; }

    public required string? Role { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Singleton <c>/interface/bridge/settings</c>.</summary>
public sealed class BridgeSettingsDiscovery
{
    public required string? UseIpFirewall { get; init; }

    public required string? UseIpFirewallForVlan { get; init; }

    public required string? UseIpFirewallForPppoe { get; init; }

    public required string? AllowFastPath { get; init; }

    public required string? BridgeFastPathActive { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>One <c>/interface/bridge/vlan</c> row.</summary>
public sealed class BridgeVlanDiscovery
{
    public required string? Bridge { get; init; }

    public required string? VlanIds { get; init; }

    public required string? Tagged { get; init; }

    public required string? Untagged { get; init; }

    public required string? Disabled { get; init; }

    public required string? Comment { get; init; }

    public required string? CurrentTagged { get; init; }

    public required string? CurrentUntagged { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>One generic ethernet switch chip row.</summary>
public sealed class EthernetSwitchDiscovery
{
    public required string? Name { get; init; }

    public required string? Type { get; init; }

    public required string? L3HwOffloading { get; init; }

    /// <summary>False for unknown/empty chip types — never grants switch write capability.</summary>
    public required bool HasKnownChipProfile { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>One generic ethernet switch port row.</summary>
public sealed class EthernetSwitchPortDiscovery
{
    public required string? Name { get; init; }

    public required string? Switch { get; init; }

    public required string? DefaultVlanId { get; init; }

    public required string? VlanMode { get; init; }

    public required string? VlanHeader { get; init; }

    public required string? L3HwOffloading { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Bridge/VLAN/switch topology metadata discovery (M1-16). Read-only; no transit ACL compilation.</summary>
public sealed class BridgeSwitchDiscoveryResult
{
    public required IReadOnlyList<BridgeDiscovery> Bridges { get; init; }

    public required IReadOnlyList<BridgePortDiscovery> BridgePorts { get; init; }

    public required BridgeSettingsDiscovery BridgeSettings { get; init; }

    public required IReadOnlyList<BridgeVlanDiscovery> BridgeVlans { get; init; }

    public required IReadOnlyList<EthernetSwitchDiscovery> EthernetSwitches { get; init; }

    public required IReadOnlyList<EthernetSwitchPortDiscovery> EthernetSwitchPorts { get; init; }

    public required IReadOnlyList<BridgePathRoleIndicator> PathRoleIndicators { get; init; }

    public required IReadOnlyList<DiscoveryFinding> Findings { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Invariant: hardware-switched traffic is never assumed to traverse the IP firewall.
    /// Always false for this discovery surface.
    /// </summary>
    public required bool AssumesHardwareSwitchedTrafficPassesIpFirewall { get; init; }

    /// <summary>Switch metadata never opens a RouterOS write path.</summary>
    public required bool GrantsSwitchWriteCapability { get; init; }

    /// <summary>Transit ACL data is never compiled from these reads.</summary>
    public required bool CompilesTransitAcl { get; init; }

    public IReadOnlyDictionary<string, string> ConfigurationHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            foreach (BridgeDiscovery bridge in Bridges.OrderBy(b => b.Name, StringComparer.Ordinal))
            {
                string p = $"bridge.{bridge.Name}";
                Put(material, $"{p}.vlan-filtering", bridge.VlanFiltering);
                Put(material, $"{p}.protocol-mode", bridge.ProtocolMode);
                Put(material, $"{p}.pvid", bridge.Pvid);
                Put(material, $"{p}.frame-types", bridge.FrameTypes);
                Put(material, $"{p}.ingress-filtering", bridge.IngressFiltering);
                Put(material, $"{p}.mtu", bridge.Mtu);
                Put(material, $"{p}.disabled", bridge.Disabled);
            }

            foreach (BridgePortDiscovery port in BridgePorts
                         .OrderBy(p => p.Bridge, StringComparer.Ordinal)
                         .ThenBy(p => p.Interface, StringComparer.Ordinal))
            {
                string p = $"bport.{port.Bridge}.{port.Interface}";
                Put(material, $"{p}.pvid", port.Pvid);
                Put(material, $"{p}.frame-types", port.FrameTypes);
                Put(material, $"{p}.ingress-filtering", port.IngressFiltering);
                Put(material, $"{p}.hw", port.Hw);
                Put(material, $"{p}.disabled", port.Disabled);
            }

            Put(material, "bset.use-ip-firewall", BridgeSettings.UseIpFirewall);
            Put(material, "bset.use-ip-firewall-for-vlan", BridgeSettings.UseIpFirewallForVlan);
            Put(material, "bset.use-ip-firewall-for-pppoe", BridgeSettings.UseIpFirewallForPppoe);
            Put(material, "bset.allow-fast-path", BridgeSettings.AllowFastPath);

            int vlanOrdinal = 0;
            foreach (BridgeVlanDiscovery vlan in BridgeVlans
                         .OrderBy(v => v.Bridge, StringComparer.Ordinal)
                         .ThenBy(v => v.VlanIds, StringComparer.Ordinal))
            {
                string p = $"bvlan.{vlanOrdinal++}";
                Put(material, $"{p}.bridge", vlan.Bridge);
                Put(material, $"{p}.vlan-ids", vlan.VlanIds);
                Put(material, $"{p}.tagged", vlan.Tagged);
                Put(material, $"{p}.untagged", vlan.Untagged);
                Put(material, $"{p}.disabled", vlan.Disabled);
            }

            foreach (EthernetSwitchDiscovery sw in EthernetSwitches.OrderBy(s => s.Name, StringComparer.Ordinal))
            {
                string p = $"switch.{sw.Name}";
                Put(material, $"{p}.type", sw.Type);
                Put(material, $"{p}.l3-hw-offloading", sw.L3HwOffloading);
                Put(material, $"{p}.known-chip", sw.HasKnownChipProfile ? "true" : "false");
            }

            foreach (EthernetSwitchPortDiscovery port in EthernetSwitchPorts
                         .OrderBy(p => p.Switch, StringComparer.Ordinal)
                         .ThenBy(p => p.Name, StringComparer.Ordinal))
            {
                string p = $"swport.{port.Switch}.{port.Name}";
                Put(material, $"{p}.default-vlan-id", port.DefaultVlanId);
                Put(material, $"{p}.vlan-mode", port.VlanMode);
                Put(material, $"{p}.vlan-header", port.VlanHeader);
                Put(material, $"{p}.l3-hw-offloading", port.L3HwOffloading);
            }

            return material;
        }
    }

    public IReadOnlyDictionary<string, string> ObservationHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            foreach (BridgeDiscovery bridge in Bridges.OrderBy(b => b.Name, StringComparer.Ordinal))
            {
                string p = $"bridge.{bridge.Name}";
                Put(material, $"{p}.running", bridge.Running);
                Put(material, $"{p}.root-bridge", bridge.RootBridge);
            }

            foreach (BridgePortDiscovery port in BridgePorts
                         .OrderBy(p => p.Bridge, StringComparer.Ordinal)
                         .ThenBy(p => p.Interface, StringComparer.Ordinal))
            {
                string p = $"bport.{port.Bridge}.{port.Interface}";
                Put(material, $"{p}.hw-offload", port.HwOffload);
                Put(material, $"{p}.role", port.Role);
            }

            Put(material, "bset.bridge-fast-path-active", BridgeSettings.BridgeFastPathActive);

            int vlanOrdinal = 0;
            foreach (BridgeVlanDiscovery vlan in BridgeVlans
                         .OrderBy(v => v.Bridge, StringComparer.Ordinal)
                         .ThenBy(v => v.VlanIds, StringComparer.Ordinal))
            {
                string p = $"bvlan.{vlanOrdinal++}";
                Put(material, $"{p}.current-tagged", vlan.CurrentTagged);
                Put(material, $"{p}.current-untagged", vlan.CurrentUntagged);
            }

            return material;
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
