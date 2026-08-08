using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Reads bridge/VLAN/switch topology metadata via the typed allowlist (M1-16).
/// Never assumes hardware-switched traffic passes the IP firewall; never opens write/transit-ACL paths.
/// </summary>
public static class BridgeSwitchDiscovery
{
    private static readonly RosReadCommandId[] CommandSet =
    [
        RosReadCommandId.Bridges,
        RosReadCommandId.BridgePorts,
        RosReadCommandId.BridgeSettings,
        RosReadCommandId.BridgeVlans,
        RosReadCommandId.EthernetSwitches,
        RosReadCommandId.EthernetSwitchPorts,
    ];

    /// <summary>Allowlisted discovery command ids (RouterOS API only — never SwOS).</summary>
    public static IReadOnlyList<RosReadCommandId> DiscoveryCommandIds => CommandSet;

    public static async Task<BridgeSwitchDiscoveryResult> DiscoverAsync(
        RosSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        List<string> warnings = [];
        Dictionary<RosReadCommandId, RosReadCommandResult> results = new();
        foreach (RosReadCommandId id in CommandSet)
        {
            results[id] = await ExecuteAsync(session, id, warnings, cancellationToken).ConfigureAwait(false);
        }

        return BuildResult(
            results[RosReadCommandId.Bridges],
            results[RosReadCommandId.BridgePorts],
            results[RosReadCommandId.BridgeSettings],
            results[RosReadCommandId.BridgeVlans],
            results[RosReadCommandId.EthernetSwitches],
            results[RosReadCommandId.EthernetSwitchPorts],
            warnings);
    }

    /// <summary>Builds discovery from executed bridge/switch prints.</summary>
    public static BridgeSwitchDiscoveryResult BuildResult(
        RosReadCommandResult bridges,
        RosReadCommandResult bridgePorts,
        RosReadCommandResult bridgeSettings,
        RosReadCommandResult bridgeVlans,
        RosReadCommandResult ethernetSwitches,
        RosReadCommandResult ethernetSwitchPorts,
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(bridges);
        ArgumentNullException.ThrowIfNull(bridgePorts);
        ArgumentNullException.ThrowIfNull(bridgeSettings);
        ArgumentNullException.ThrowIfNull(bridgeVlans);
        ArgumentNullException.ThrowIfNull(ethernetSwitches);
        ArgumentNullException.ThrowIfNull(ethernetSwitchPorts);

        List<DiscoveryFinding> findings = [];
        List<BridgeDiscovery> bridgeRows = MapBridges(bridges);
        List<BridgePortDiscovery> portRows = MapBridgePorts(bridgePorts);
        BridgeSettingsDiscovery settings = MapBridgeSettings(bridgeSettings);
        List<BridgeVlanDiscovery> vlanRows = MapBridgeVlans(bridgeVlans);
        List<EthernetSwitchDiscovery> switches = MapSwitches(ethernetSwitches, findings);
        List<EthernetSwitchPortDiscovery> switchPorts = MapSwitchPorts(ethernetSwitchPorts);

        return new BridgeSwitchDiscoveryResult
        {
            Bridges = bridgeRows.OrderBy(b => b.Name, StringComparer.Ordinal).ToArray(),
            BridgePorts = portRows
                .OrderBy(p => p.Bridge, StringComparer.Ordinal)
                .ThenBy(p => p.Interface, StringComparer.Ordinal)
                .ToArray(),
            BridgeSettings = settings,
            BridgeVlans = vlanRows
                .OrderBy(v => v.Bridge, StringComparer.Ordinal)
                .ThenBy(v => v.VlanIds, StringComparer.Ordinal)
                .ToArray(),
            EthernetSwitches = switches.OrderBy(s => s.Name, StringComparer.Ordinal).ToArray(),
            EthernetSwitchPorts = switchPorts
                .OrderBy(p => p.Switch, StringComparer.Ordinal)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .ToArray(),
            PathRoleIndicators = DerivePathIndicators(settings, portRows, switches, switchPorts),
            Findings = findings,
            Warnings = warnings?.ToArray() ?? [],
            AssumesHardwareSwitchedTrafficPassesIpFirewall = false,
            GrantsSwitchWriteCapability = false,
            CompilesTransitAcl = false,
        };
    }

    private static async Task<RosReadCommandResult> ExecuteAsync(
        RosSession session,
        RosReadCommandId id,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
            session,
            id,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            warnings.Add($"{id}: {result.Error?.Code} {result.Error?.Message}");
        }

        return result;
    }

    private static List<BridgeDiscovery> MapBridges(RosReadCommandResult result)
    {
        List<BridgeDiscovery> rows = [];
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            rows.Add(new BridgeDiscovery
            {
                Name = Get(known, "name"),
                VlanFiltering = Get(known, "vlan-filtering"),
                ProtocolMode = Get(known, "protocol-mode"),
                Pvid = Get(known, "pvid"),
                FrameTypes = Get(known, "frame-types"),
                IngressFiltering = Get(known, "ingress-filtering"),
                Mtu = Get(known, "mtu"),
                Disabled = Get(known, "disabled"),
                Comment = Get(known, "comment"),
                Running = Get(known, "running"),
                RootBridge = Get(known, "root-bridge"),
                RawProperties = ToDict(row.RawProperties),
            });
        }

        return rows;
    }

    private static List<BridgePortDiscovery> MapBridgePorts(RosReadCommandResult result)
    {
        List<BridgePortDiscovery> rows = [];
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            rows.Add(new BridgePortDiscovery
            {
                Bridge = Get(known, "bridge"),
                Interface = Get(known, "interface"),
                Pvid = Get(known, "pvid"),
                FrameTypes = Get(known, "frame-types"),
                IngressFiltering = Get(known, "ingress-filtering"),
                Hw = Get(known, "hw"),
                Disabled = Get(known, "disabled"),
                HwOffload = Get(known, "hw-offload"),
                Role = Get(known, "role"),
                RawProperties = ToDict(row.RawProperties),
            });
        }

        return rows;
    }

    private static BridgeSettingsDiscovery MapBridgeSettings(RosReadCommandResult result)
    {
        Dictionary<string, string> known = result.Records.Count > 0
            ? ToDict(result.Records[0].KnownProperties)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, string> raw = result.Records.Count > 0
            ? ToDict(result.Records[0].RawProperties)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        return new BridgeSettingsDiscovery
        {
            UseIpFirewall = Get(known, "use-ip-firewall"),
            UseIpFirewallForVlan = Get(known, "use-ip-firewall-for-vlan"),
            UseIpFirewallForPppoe = Get(known, "use-ip-firewall-for-pppoe"),
            AllowFastPath = Get(known, "allow-fast-path"),
            BridgeFastPathActive = Get(known, "bridge-fast-path-active"),
            RawProperties = raw,
        };
    }

    private static List<BridgeVlanDiscovery> MapBridgeVlans(RosReadCommandResult result)
    {
        List<BridgeVlanDiscovery> rows = [];
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            rows.Add(new BridgeVlanDiscovery
            {
                Bridge = Get(known, "bridge"),
                VlanIds = Get(known, "vlan-ids"),
                Tagged = Get(known, "tagged"),
                Untagged = Get(known, "untagged"),
                Disabled = Get(known, "disabled"),
                Comment = Get(known, "comment"),
                CurrentTagged = Get(known, "current-tagged"),
                CurrentUntagged = Get(known, "current-untagged"),
                RawProperties = ToDict(row.RawProperties),
            });
        }

        return rows;
    }

    private static List<EthernetSwitchDiscovery> MapSwitches(
        RosReadCommandResult result,
        List<DiscoveryFinding> findings)
    {
        List<EthernetSwitchDiscovery> rows = [];
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? name = Get(known, "name");
            string? type = Get(known, "type");
            bool knownChip = IsKnownChipType(type);
            if (!knownChip)
            {
                findings.Add(new DiscoveryFinding
                {
                    Code = DiscoveryFinding.UnknownSwitchChip,
                    Message = $"Switch '{name}' has unknown chip type '{type}' — no write/offload profile granted.",
                    Subject = name,
                });
            }

            rows.Add(new EthernetSwitchDiscovery
            {
                Name = name,
                Type = type,
                L3HwOffloading = Get(known, "l3-hw-offloading"),
                HasKnownChipProfile = knownChip,
                RawProperties = ToDict(row.RawProperties),
            });
        }

        return rows;
    }

    private static List<EthernetSwitchPortDiscovery> MapSwitchPorts(RosReadCommandResult result)
    {
        List<EthernetSwitchPortDiscovery> rows = [];
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            rows.Add(new EthernetSwitchPortDiscovery
            {
                Name = Get(known, "name"),
                Switch = Get(known, "switch"),
                DefaultVlanId = Get(known, "default-vlan-id"),
                VlanMode = Get(known, "vlan-mode"),
                VlanHeader = Get(known, "vlan-header"),
                L3HwOffloading = Get(known, "l3-hw-offloading"),
                RawProperties = ToDict(row.RawProperties),
            });
        }

        return rows;
    }

    private static BridgePathRoleIndicator[] DerivePathIndicators(
        BridgeSettingsDiscovery settings,
        IReadOnlyList<BridgePortDiscovery> ports,
        IReadOnlyList<EthernetSwitchDiscovery> switches,
        IReadOnlyList<EthernetSwitchPortDiscovery> switchPorts)
    {
        HashSet<BridgePathRoleIndicator> indicators = [BridgePathRoleIndicator.L2ForwardingPossible];
        if (IsTruthy(settings.UseIpFirewall)
            || IsTruthy(settings.UseIpFirewallForVlan)
            || IsTruthy(settings.UseIpFirewallForPppoe))
        {
            indicators.Add(BridgePathRoleIndicator.BridgedTrafficMayHitIpFirewall);
        }

        if (ports.Any(p => IsTruthy(p.HwOffload)))
        {
            indicators.Add(BridgePathRoleIndicator.HardwareOffloadObserved);
        }

        if (switches.Any(s => IsTruthy(s.L3HwOffloading))
            || switchPorts.Any(p => IsTruthy(p.L3HwOffloading)))
        {
            indicators.Add(BridgePathRoleIndicator.L3HardwareOffloadConfigured);
        }

        if (switches.Any(s => !s.HasKnownChipProfile))
        {
            indicators.Add(BridgePathRoleIndicator.UnknownSwitchChip);
        }

        return indicators.OrderBy(i => (byte)i).ToArray();
    }

    private static bool IsKnownChipType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        return !string.Equals(type, "unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ToDict(IReadOnlyDictionary<string, string> source)
        => source.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

    private static string? Get(Dictionary<string, string> known, string name)
        => known.TryGetValue(name, out string? value) ? value : null;
}
