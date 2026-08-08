namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Classifies ingress/egress pairs into CPU / HW-offload / MIXED / INDETERMINATE (N1-03 / next-1).
/// Never assumes hardware-switched traffic traverses the IP firewall.
/// Blocker emission for analysis is N1-04; this surface only attaches hints.
/// </summary>
public static class PacketPathClassifier
{
    public static PacketPathClassificationResult Classify(
        BridgeSwitchDiscoveryResult bridgeSwitch,
        IReadOnlyList<(string Ingress, string Egress, string? VlanId)>? pairs = null)
    {
        ArgumentNullException.ThrowIfNull(bridgeSwitch);

        List<DiscoveryFinding> findings = [];
        Dictionary<string, BridgePortDiscovery> portsByIface = bridgeSwitch.BridgePorts
            .Where(p => !string.IsNullOrWhiteSpace(p.Interface))
            .GroupBy(p => p.Interface!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        bool unknownChip = bridgeSwitch.EthernetSwitches.Any(s => !s.HasKnownChipProfile)
                           || bridgeSwitch.PathRoleIndicators.Contains(BridgePathRoleIndicator.UnknownSwitchChip);
        bool l3HwConfigured = bridgeSwitch.EthernetSwitches.Any(s => IsTruthy(s.L3HwOffloading))
                              || bridgeSwitch.EthernetSwitchPorts.Any(p => IsTruthy(p.L3HwOffloading))
                              || bridgeSwitch.PathRoleIndicators.Contains(BridgePathRoleIndicator.L3HardwareOffloadConfigured);
        bool firewallForced = IsTruthy(bridgeSwitch.BridgeSettings.UseIpFirewall)
                              || IsTruthy(bridgeSwitch.BridgeSettings.UseIpFirewallForVlan)
                              || IsTruthy(bridgeSwitch.BridgeSettings.UseIpFirewallForPppoe);

        List<(string Ingress, string Egress, string? VlanId)> workPairs = pairs is null
            ? DerivePairs(bridgeSwitch)
            : pairs.ToList();

        List<PacketPathPairClassification> classified = [];
        foreach ((string ingress, string egress, string? vlanId) in workPairs)
        {
            if (string.IsNullOrWhiteSpace(ingress)
                || string.IsNullOrWhiteSpace(egress)
                || string.Equals(ingress, egress, StringComparison.Ordinal))
            {
                continue;
            }

            portsByIface.TryGetValue(ingress, out BridgePortDiscovery? inPort);
            portsByIface.TryGetValue(egress, out BridgePortDiscovery? outPort);
            string? bridge = FirstNonEmpty(inPort?.Bridge, outPort?.Bridge);

            bool inHw = IsTruthy(inPort?.HwOffload);
            bool outHw = IsTruthy(outPort?.HwOffload);
            bool pairL3Hw = l3HwConfigured; // per-VLAN L3HW refinement lands with richer switch profiles later
            List<string> reasons = [];

            PacketPathClass pathClass = ResolveClass(
                unknownChip,
                firewallForced,
                inHw,
                outHw,
                pairL3Hw,
                inPort is not null,
                outPort is not null,
                reasons);

            PacketPathBlockerHint hint = pathClass switch
            {
                PacketPathClass.HardwareOffloadedPath => PacketPathBlockerHint.PacketPathBypassesIpFirewall,
                PacketPathClass.Indeterminate => PacketPathBlockerHint.PacketPathNotProven,
                _ => PacketPathBlockerHint.None,
            };

            if (hint == PacketPathBlockerHint.PacketPathBypassesIpFirewall)
            {
                findings.Add(new DiscoveryFinding
                {
                    Code = DiscoveryFinding.PacketPathBypassesIpFirewall,
                    Message = $"Pair {ingress}→{egress} classified HARDWARE_OFFLOADED_PATH (managed FORWARD not proven on CPU).",
                    Subject = $"{ingress}->{egress}",
                });
            }
            else if (hint == PacketPathBlockerHint.PacketPathNotProven)
            {
                findings.Add(new DiscoveryFinding
                {
                    Code = DiscoveryFinding.PacketPathNotProven,
                    Message = $"Pair {ingress}→{egress} classified INDETERMINATE (packet path through IP firewall not proven).",
                    Subject = $"{ingress}->{egress}",
                });
            }

            classified.Add(new PacketPathPairClassification
            {
                IngressInterface = ingress,
                EgressInterface = egress,
                Bridge = bridge,
                VlanId = vlanId,
                PathClass = pathClass,
                BlockerHint = hint,
                Reasons = reasons.OrderBy(r => r, StringComparer.Ordinal).ToArray(),
            });
        }

        return new PacketPathClassificationResult
        {
            Pairs = classified
                .OrderBy(p => p.Bridge, StringComparer.Ordinal)
                .ThenBy(p => p.IngressInterface, StringComparer.Ordinal)
                .ThenBy(p => p.EgressInterface, StringComparer.Ordinal)
                .ThenBy(p => p.VlanId, StringComparer.Ordinal)
                .ToArray(),
            WorstPathClass = Worst(classified.Select(p => p.PathClass)),
            Findings = findings,
            Warnings = [],
        };
    }

    /// <summary>Maps next-1 path class names to the typed enum.</summary>
    public static PacketPathClass ParseClassName(string name)
        => name.Trim().ToUpperInvariant() switch
        {
            "CPU_FIREWALL_PATH" or "CPUFIREWALLPATH" => PacketPathClass.CpuFirewallPath,
            "HARDWARE_OFFLOADED_PATH" or "HARDWAREOFFLOADEDPATH" => PacketPathClass.HardwareOffloadedPath,
            "MIXED_PATH" or "MIXEDPATH" => PacketPathClass.MixedPath,
            "INDETERMINATE" => PacketPathClass.Indeterminate,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown packet path class."),
        };

    private static PacketPathClass ResolveClass(
        bool unknownChip,
        bool firewallForced,
        bool ingressHwObserved,
        bool egressHwObserved,
        bool l3HwConfigured,
        bool ingressPortKnown,
        bool egressPortKnown,
        List<string> reasons)
    {
        if (!ingressPortKnown || !egressPortKnown)
        {
            reasons.Add("missing-bridge-port");
            return PacketPathClass.Indeterminate;
        }

        if (unknownChip)
        {
            reasons.Add("unknown-switch-chip");
            return PacketPathClass.Indeterminate;
        }

        bool anyHwObs = ingressHwObserved || egressHwObserved;
        bool bothHwObs = ingressHwObserved && egressHwObserved;

        if (l3HwConfigured)
        {
            reasons.Add("l3-hw-offloading-configured");
        }

        if (ingressHwObserved)
        {
            reasons.Add("ingress-hw-offload-observed");
        }

        if (egressHwObserved)
        {
            reasons.Add("egress-hw-offload-observed");
        }

        if (firewallForced)
        {
            reasons.Add("use-ip-firewall-enabled");
        }

        // HW evidence with forced CPU firewall bridging => mixed / unproven split path.
        if (firewallForced && (anyHwObs || l3HwConfigured))
        {
            reasons.Add("mixed-firewall-and-offload");
            return PacketPathClass.MixedPath;
        }

        if (l3HwConfigured || bothHwObs)
        {
            reasons.Add("hardware-path");
            return PacketPathClass.HardwareOffloadedPath;
        }

        // Only one side offloaded — cannot claim a uniform CPU firewall path.
        if (anyHwObs)
        {
            reasons.Add("asymmetric-hw-offload");
            return PacketPathClass.MixedPath;
        }

        // No HW evidence and known chip/ports: software/CPU path for filter analysis.
        reasons.Add("no-hw-offload-evidence");
        return PacketPathClass.CpuFirewallPath;
    }

    private static List<(string Ingress, string Egress, string? VlanId)> DerivePairs(
        BridgeSwitchDiscoveryResult bridgeSwitch)
    {
        List<(string Ingress, string Egress, string? VlanId)> derived = [];
        foreach (IGrouping<string?, BridgePortDiscovery> group in bridgeSwitch.BridgePorts
                     .Where(p => !string.IsNullOrWhiteSpace(p.Interface) && !string.IsNullOrWhiteSpace(p.Bridge))
                     .GroupBy(p => p.Bridge, StringComparer.Ordinal))
        {
            string[] ifaces = group
                .Select(p => p.Interface!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            for (int i = 0; i < ifaces.Length; i++)
            {
                for (int j = 0; j < ifaces.Length; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    derived.Add((Ingress: ifaces[i], Egress: ifaces[j], VlanId: null));
                }
            }
        }

        return derived;
    }

    private static PacketPathClass Worst(IEnumerable<PacketPathClass> classes)
    {
        PacketPathClass worst = PacketPathClass.CpuFirewallPath;
        foreach (PacketPathClass c in classes)
        {
            if (Rank(c) > Rank(worst))
            {
                worst = c;
            }
        }

        return worst;
    }

    private static int Rank(PacketPathClass c)
        => c switch
        {
            PacketPathClass.CpuFirewallPath => 0,
            PacketPathClass.MixedPath => 1,
            PacketPathClass.HardwareOffloadedPath => 2,
            PacketPathClass.Indeterminate => 3,
            _ => 3,
        };

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
