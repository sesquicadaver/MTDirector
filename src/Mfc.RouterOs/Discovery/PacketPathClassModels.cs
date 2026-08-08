namespace Mfc.RouterOs.Discovery;

/// <summary>Packet path class for one ingress/egress pair (next-1 / N1-03).</summary>
public enum PacketPathClass : byte
{
    CpuFirewallPath = 0,
    HardwareOffloadedPath = 1,
    MixedPath = 2,
    Indeterminate = 3,
}

/// <summary>
/// Hint for later analysis blockers (N1-04). Classification only — does not mutate policy.
/// </summary>
public enum PacketPathBlockerHint : byte
{
    None = 0,

    /// <summary>Maps from <see cref="PacketPathClass.HardwareOffloadedPath"/>.</summary>
    PacketPathBypassesIpFirewall = 1,

    /// <summary>Maps from <see cref="PacketPathClass.Indeterminate"/>.</summary>
    PacketPathNotProven = 2,
}

/// <summary>One classified ingress/egress interface pair.</summary>
public sealed class PacketPathPairClassification
{
    public required string IngressInterface { get; init; }

    public required string EgressInterface { get; init; }

    public required string? Bridge { get; init; }

    public required string? VlanId { get; init; }

    public required PacketPathClass PathClass { get; init; }

    public required PacketPathBlockerHint BlockerHint { get; init; }

    public required IReadOnlyList<string> Reasons { get; init; }
}

/// <summary>Aggregate packet-path classification result (N1-03).</summary>
public sealed class PacketPathClassificationResult
{
    public required IReadOnlyList<PacketPathPairClassification> Pairs { get; init; }

    /// <summary>
    /// Device-level worst class for managed FORWARD gating:
    /// Indeterminate &gt; HardwareOffloaded &gt; Mixed &gt; CpuFirewall.
    /// </summary>
    public required PacketPathClass WorstPathClass { get; init; }

    public required IReadOnlyList<DiscoveryFinding> Findings { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>True when any pair would block managed FORWARD under next-1 rules.</summary>
    public bool BlocksManagedForwardPolicy
        => Pairs.Any(p => p.BlockerHint != PacketPathBlockerHint.None);

    public IReadOnlyDictionary<string, string> ConfigurationHashMaterial
    {
        get
        {
            // L3HW configuration and forced firewall settings are config; path class itself is derived.
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            int i = 0;
            foreach (PacketPathPairClassification pair in Pairs
                         .OrderBy(p => p.Bridge, StringComparer.Ordinal)
                         .ThenBy(p => p.IngressInterface, StringComparer.Ordinal)
                         .ThenBy(p => p.EgressInterface, StringComparer.Ordinal)
                         .ThenBy(p => p.VlanId, StringComparer.Ordinal))
            {
                string prefix = $"pair.{i++}";
                Put(material, $"{prefix}.ingress", pair.IngressInterface);
                Put(material, $"{prefix}.egress", pair.EgressInterface);
                Put(material, $"{prefix}.bridge", pair.Bridge);
                Put(material, $"{prefix}.vlan", pair.VlanId);
                // Path class depends on observation (hw-offload) — keep out of configuration hash.
            }

            return material;
        }
    }

    public IReadOnlyDictionary<string, string> ObservationHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            int i = 0;
            foreach (PacketPathPairClassification pair in Pairs
                         .OrderBy(p => p.Bridge, StringComparer.Ordinal)
                         .ThenBy(p => p.IngressInterface, StringComparer.Ordinal)
                         .ThenBy(p => p.EgressInterface, StringComparer.Ordinal)
                         .ThenBy(p => p.VlanId, StringComparer.Ordinal))
            {
                string prefix = $"pair.{i++}";
                Put(material, $"{prefix}.class", pair.PathClass.ToString());
                Put(material, $"{prefix}.blocker", pair.BlockerHint.ToString());
            }

            Put(material, "worst", WorstPathClass.ToString());
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
