namespace Mfc.RouterOs.Discovery;

/// <summary>Node kinds in the Container/App→VETH→Bridge→VLAN→VRF projection (N1-02).</summary>
public enum PacketPathNodeKind : byte
{
    Container = 0,
    App = 1,
    Veth = 2,
    Bridge = 3,
    BridgeVlan = 4,
    VlanInterface = 5,
    Vrf = 6,

    /// <summary>Generic interface reference (ether/bridge port) when not a typed VETH/VLAN-IF node.</summary>
    Interface = 7,
}

/// <summary>Directed edge kinds for the packet-path topology graph.</summary>
public enum PacketPathEdgeKind : byte
{
    UsesVeth = 0,
    BridgeMember = 1,
    BridgeVlanMembership = 2,
    VlanOnParent = 3,
    VrfMember = 4,
}

/// <summary>One projected topology node. No ContainerPolicy / VlanPolicy entities.</summary>
public sealed class PacketPathTopologyNode
{
    public required PacketPathNodeKind Kind { get; init; }

    public required string Key { get; init; }

    public required string? Name { get; init; }

    public required IReadOnlyDictionary<string, string> Attributes { get; init; }
}

/// <summary>One projected topology edge.</summary>
public sealed class PacketPathTopologyEdge
{
    public required PacketPathEdgeKind Kind { get; init; }

    public required string FromKey { get; init; }

    public required string ToKey { get; init; }

    public required IReadOnlyDictionary<string, string> Attributes { get; init; }
}

/// <summary>Aggregate topology projection for packet-path analysis (N1-02).</summary>
public sealed class PacketPathTopologyResult
{
    public required IReadOnlyList<PacketPathTopologyNode> Nodes { get; init; }

    public required IReadOnlyList<PacketPathTopologyEdge> Edges { get; init; }

    public required IReadOnlyList<DiscoveryFinding> Findings { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>VETH names used by more than one container/app — never collapse to 1:1.</summary>
    public required IReadOnlyList<string> SharedVethNames { get; init; }

    /// <summary>Invariant: bridge membership is never assumed to imply IP-firewall traversal.</summary>
    public required bool AssumesBridgeTrafficPassesIpFirewall { get; init; }

    public IReadOnlyDictionary<string, string> ConfigurationHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            foreach (PacketPathTopologyNode node in Nodes.OrderBy(n => n.Key, StringComparer.Ordinal))
            {
                Put(material, $"node.{node.Key}.kind", node.Kind.ToString());
                Put(material, $"node.{node.Key}.name", node.Name);
                foreach (KeyValuePair<string, string> attr in node.Attributes
                             .Where(a => !IsObservationAttribute(a.Key))
                             .OrderBy(a => a.Key, StringComparer.Ordinal))
                {
                    Put(material, $"node.{node.Key}.{attr.Key}", attr.Value);
                }
            }

            int edgeOrdinal = 0;
            foreach (PacketPathTopologyEdge edge in Edges
                         .OrderBy(e => e.Kind)
                         .ThenBy(e => e.FromKey, StringComparer.Ordinal)
                         .ThenBy(e => e.ToKey, StringComparer.Ordinal))
            {
                string p = $"edge.{edgeOrdinal++}";
                Put(material, $"{p}.kind", edge.Kind.ToString());
                Put(material, $"{p}.from", edge.FromKey);
                Put(material, $"{p}.to", edge.ToKey);
                foreach (KeyValuePair<string, string> attr in edge.Attributes
                             .Where(a => !IsObservationAttribute(a.Key))
                             .OrderBy(a => a.Key, StringComparer.Ordinal))
                {
                    Put(material, $"{p}.{attr.Key}", attr.Value);
                }
            }

            return material;
        }
    }

    public IReadOnlyDictionary<string, string> ObservationHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            foreach (PacketPathTopologyNode node in Nodes.OrderBy(n => n.Key, StringComparer.Ordinal))
            {
                foreach (KeyValuePair<string, string> attr in node.Attributes
                             .Where(a => IsObservationAttribute(a.Key))
                             .OrderBy(a => a.Key, StringComparer.Ordinal))
                {
                    Put(material, $"node.{node.Key}.{attr.Key}", attr.Value);
                }
            }

            return material;
        }
    }

    private static bool IsObservationAttribute(string key)
        => key is "status" or "running";

    private static void Put(Dictionary<string, string> target, string key, string? value)
    {
        if (value is not null)
        {
            target[key] = value;
        }
    }
}
