using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Projects Container/App → VETH → Bridge → Bridge-VLAN / VLAN-IF → VRF topology (N1-02 / next-1).
/// Does not assume 1:1 container↔VETH, VLAN↔interface, or bridge↔IP-firewall path.
/// </summary>
public static class PacketPathTopologyDiscovery
{
    private static readonly RosReadCommandId[] CommandSet =
    [
        RosReadCommandId.Containers,
        RosReadCommandId.Apps,
        RosReadCommandId.VethInterfaces,
        RosReadCommandId.VlanInterfaces,
        RosReadCommandId.Bridges,
        RosReadCommandId.BridgePorts,
        RosReadCommandId.BridgeSettings,
        RosReadCommandId.BridgeVlans,
        RosReadCommandId.IpVrfs,
    ];

    public static IReadOnlyList<RosReadCommandId> DiscoveryCommandIds => CommandSet;

    public static async Task<PacketPathTopologyResult> DiscoverAsync(
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

        BridgeSwitchDiscoveryResult bridges = BridgeSwitchDiscovery.BuildResult(
            results[RosReadCommandId.Bridges],
            results[RosReadCommandId.BridgePorts],
            results[RosReadCommandId.BridgeSettings],
            results[RosReadCommandId.BridgeVlans],
            OkEmpty(RosReadCommandId.EthernetSwitches),
            OkEmpty(RosReadCommandId.EthernetSwitchPorts));

        return BuildResult(
            results[RosReadCommandId.Containers],
            results[RosReadCommandId.Apps],
            results[RosReadCommandId.VethInterfaces],
            results[RosReadCommandId.VlanInterfaces],
            bridges,
            results[RosReadCommandId.IpVrfs],
            warnings);
    }

    /// <summary>Builds the topology graph from already-executed typed reads.</summary>
    public static PacketPathTopologyResult BuildResult(
        RosReadCommandResult containers,
        RosReadCommandResult apps,
        RosReadCommandResult vethInterfaces,
        RosReadCommandResult vlanInterfaces,
        BridgeSwitchDiscoveryResult bridges,
        RosReadCommandResult vrfs,
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(containers);
        ArgumentNullException.ThrowIfNull(apps);
        ArgumentNullException.ThrowIfNull(vethInterfaces);
        ArgumentNullException.ThrowIfNull(vlanInterfaces);
        ArgumentNullException.ThrowIfNull(bridges);
        ArgumentNullException.ThrowIfNull(vrfs);

        List<DiscoveryFinding> findings = [];
        Dictionary<string, PacketPathTopologyNode> nodes = new(StringComparer.Ordinal);
        List<PacketPathTopologyEdge> edges = [];
        Dictionary<string, List<string>> vethConsumers = new(StringComparer.Ordinal);

        HashSet<string> vethNames = [];
        foreach (RosReadRecord row in vethInterfaces.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? name = Get(known, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            vethNames.Add(name);
            AddNode(nodes, PacketPathNodeKind.Veth, NodeKey(PacketPathNodeKind.Veth, name), name, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["address"] = Get(known, "address") ?? string.Empty,
                ["gateway"] = Get(known, "gateway") ?? string.Empty,
                ["gateway6"] = Get(known, "gateway6") ?? string.Empty,
                ["dhcp"] = Get(known, "dhcp") ?? string.Empty,
                ["disabled"] = Get(known, "disabled") ?? string.Empty,
                ["running"] = Get(known, "running") ?? string.Empty,
            });
        }

        MapEndpointUsesVeth(
            containers.Records,
            PacketPathNodeKind.Container,
            "status",
            nodes,
            edges,
            vethNames,
            vethConsumers,
            findings);

        MapEndpointUsesVeth(
            apps.Records,
            PacketPathNodeKind.App,
            "running",
            nodes,
            edges,
            vethNames,
            vethConsumers,
            findings);

        foreach (BridgeDiscovery bridge in bridges.Bridges)
        {
            if (string.IsNullOrWhiteSpace(bridge.Name))
            {
                continue;
            }

            AddNode(nodes, PacketPathNodeKind.Bridge, NodeKey(PacketPathNodeKind.Bridge, bridge.Name), bridge.Name, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vlan-filtering"] = bridge.VlanFiltering ?? string.Empty,
                ["pvid"] = bridge.Pvid ?? string.Empty,
                ["disabled"] = bridge.Disabled ?? string.Empty,
                ["running"] = bridge.Running ?? string.Empty,
            });
        }

        foreach (BridgePortDiscovery port in bridges.BridgePorts)
        {
            if (string.IsNullOrWhiteSpace(port.Bridge) || string.IsNullOrWhiteSpace(port.Interface))
            {
                continue;
            }

            string bridgeKey = NodeKey(PacketPathNodeKind.Bridge, port.Bridge);
            if (!nodes.ContainsKey(bridgeKey))
            {
                AddNode(nodes, PacketPathNodeKind.Bridge, bridgeKey, port.Bridge, new Dictionary<string, string>(StringComparer.Ordinal));
            }

            string fromKey = ResolveInterfaceKey(port.Interface, vethNames, nodes);
            edges.Add(new PacketPathTopologyEdge
            {
                Kind = PacketPathEdgeKind.BridgeMember,
                FromKey = fromKey,
                ToKey = bridgeKey,
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["pvid"] = port.Pvid ?? string.Empty,
                    ["hw"] = port.Hw ?? string.Empty,
                    ["disabled"] = port.Disabled ?? string.Empty,
                },
            });
        }

        int vlanOrdinal = 0;
        foreach (BridgeVlanDiscovery vlan in bridges.BridgeVlans)
        {
            if (string.IsNullOrWhiteSpace(vlan.Bridge) || string.IsNullOrWhiteSpace(vlan.VlanIds))
            {
                continue;
            }

            string vlanKey = $"bridgevlan:{vlan.Bridge}:{vlan.VlanIds}:{vlanOrdinal++}";
            AddNode(nodes, PacketPathNodeKind.BridgeVlan, vlanKey, $"{vlan.Bridge}/{vlan.VlanIds}", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["bridge"] = vlan.Bridge,
                ["vlan-ids"] = vlan.VlanIds,
                ["tagged"] = vlan.Tagged ?? string.Empty,
                ["untagged"] = vlan.Untagged ?? string.Empty,
                ["disabled"] = vlan.Disabled ?? string.Empty,
            });

            foreach (string member in SplitList(vlan.Tagged))
            {
                edges.Add(MembershipEdge(member, vlanKey, "tagged", vethNames, nodes));
            }

            foreach (string member in SplitList(vlan.Untagged))
            {
                edges.Add(MembershipEdge(member, vlanKey, "untagged", vethNames, nodes));
            }
        }

        foreach (RosReadRecord row in vlanInterfaces.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? name = Get(known, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string vlanKey = NodeKey(PacketPathNodeKind.VlanInterface, name);
            AddNode(nodes, PacketPathNodeKind.VlanInterface, vlanKey, name, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vlan-id"] = Get(known, "vlan-id") ?? string.Empty,
                ["parent"] = Get(known, "interface") ?? string.Empty,
                ["disabled"] = Get(known, "disabled") ?? string.Empty,
                ["running"] = Get(known, "running") ?? string.Empty,
            });

            string? parent = Get(known, "interface");
            if (!string.IsNullOrWhiteSpace(parent))
            {
                string parentKey = ResolveInterfaceKey(parent, vethNames, nodes);
                edges.Add(new PacketPathTopologyEdge
                {
                    Kind = PacketPathEdgeKind.VlanOnParent,
                    FromKey = vlanKey,
                    ToKey = parentKey,
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["vlan-id"] = Get(known, "vlan-id") ?? string.Empty,
                    },
                });
            }
        }

        foreach (RosReadRecord row in vrfs.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? name = Get(known, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string vrfKey = NodeKey(PacketPathNodeKind.Vrf, name);
            AddNode(nodes, PacketPathNodeKind.Vrf, vrfKey, name, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["interfaces"] = Get(known, "interfaces") ?? string.Empty,
                ["disabled"] = Get(known, "disabled") ?? string.Empty,
            });

            foreach (string iface in SplitList(Get(known, "interfaces")))
            {
                if (!InterfaceExists(iface, vethNames, nodes, vlanInterfaces))
                {
                    findings.Add(new DiscoveryFinding
                    {
                        Code = DiscoveryFinding.MissingVrfInterfaceReference,
                        Message = $"VRF '{name}' references missing interface '{iface}'.",
                        Subject = name,
                    });
                }

                string ifaceKey = ResolveInterfaceKey(iface, vethNames, nodes);
                edges.Add(new PacketPathTopologyEdge
                {
                    Kind = PacketPathEdgeKind.VrfMember,
                    FromKey = ifaceKey,
                    ToKey = vrfKey,
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal),
                });
            }
        }

        List<string> shared = vethConsumers
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => kv.Key)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        foreach (string sharedVeth in shared)
        {
            findings.Add(new DiscoveryFinding
            {
                Code = DiscoveryFinding.SharedVethMultiEndpoint,
                Message = $"VETH '{sharedVeth}' is used by multiple containers/apps; 1:1 mapping must not be assumed.",
                Subject = sharedVeth,
            });
        }

        return new PacketPathTopologyResult
        {
            Nodes = nodes.Values.OrderBy(n => n.Key, StringComparer.Ordinal).ToArray(),
            Edges = edges
                .OrderBy(e => e.Kind)
                .ThenBy(e => e.FromKey, StringComparer.Ordinal)
                .ThenBy(e => e.ToKey, StringComparer.Ordinal)
                .ToArray(),
            Findings = findings,
            Warnings = warnings?.ToArray() ?? [],
            SharedVethNames = shared,
            AssumesBridgeTrafficPassesIpFirewall = false,
        };
    }

    private static void MapEndpointUsesVeth(
        IReadOnlyList<RosReadRecord> rows,
        PacketPathNodeKind kind,
        string observationProperty,
        Dictionary<string, PacketPathTopologyNode> nodes,
        List<PacketPathTopologyEdge> edges,
        HashSet<string> vethNames,
        Dictionary<string, List<string>> vethConsumers,
        List<DiscoveryFinding> findings)
    {
        foreach (RosReadRecord row in rows)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? name = Get(known, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string nodeKey = NodeKey(kind, name);
            AddNode(nodes, kind, nodeKey, name, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["interface"] = Get(known, "interface") ?? string.Empty,
                ["disabled"] = Get(known, "disabled") ?? string.Empty,
                [observationProperty] = Get(known, observationProperty) ?? string.Empty,
            });

            string? veth = Get(known, "interface");
            if (string.IsNullOrWhiteSpace(veth))
            {
                continue;
            }

            if (!vethNames.Contains(veth))
            {
                findings.Add(new DiscoveryFinding
                {
                    Code = DiscoveryFinding.MissingVethReference,
                    Message = $"{kind} '{name}' references missing VETH '{veth}'.",
                    Subject = name,
                });
                AddNode(nodes, PacketPathNodeKind.Veth, NodeKey(PacketPathNodeKind.Veth, veth), veth, new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["missing"] = "true",
                });
            }

            string vethKey = NodeKey(PacketPathNodeKind.Veth, veth);
            edges.Add(new PacketPathTopologyEdge
            {
                Kind = PacketPathEdgeKind.UsesVeth,
                FromKey = nodeKey,
                ToKey = vethKey,
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal),
            });

            if (!vethConsumers.TryGetValue(veth, out List<string>? consumers))
            {
                consumers = [];
                vethConsumers[veth] = consumers;
            }

            consumers.Add(nodeKey);
        }
    }

    private static PacketPathTopologyEdge MembershipEdge(
        string member,
        string vlanKey,
        string role,
        HashSet<string> vethNames,
        Dictionary<string, PacketPathTopologyNode> nodes)
    {
        string fromKey = ResolveInterfaceKey(member, vethNames, nodes);
        return new PacketPathTopologyEdge
        {
            Kind = PacketPathEdgeKind.BridgeVlanMembership,
            FromKey = fromKey,
            ToKey = vlanKey,
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = role,
            },
        };
    }

    private static string ResolveInterfaceKey(
        string iface,
        HashSet<string> vethNames,
        Dictionary<string, PacketPathTopologyNode> nodes)
    {
        if (vethNames.Contains(iface))
        {
            return NodeKey(PacketPathNodeKind.Veth, iface);
        }

        string vlanKey = NodeKey(PacketPathNodeKind.VlanInterface, iface);
        if (nodes.ContainsKey(vlanKey))
        {
            return vlanKey;
        }

        string bridgeKey = NodeKey(PacketPathNodeKind.Bridge, iface);
        if (nodes.ContainsKey(bridgeKey))
        {
            return bridgeKey;
        }

        string ifaceKey = NodeKey(PacketPathNodeKind.Interface, iface);
        if (!nodes.ContainsKey(ifaceKey))
        {
            AddNode(nodes, PacketPathNodeKind.Interface, ifaceKey, iface, new Dictionary<string, string>(StringComparer.Ordinal));
        }

        return ifaceKey;
    }

    private static bool InterfaceExists(
        string iface,
        HashSet<string> vethNames,
        Dictionary<string, PacketPathTopologyNode> nodes,
        RosReadCommandResult vlanInterfaces)
    {
        if (vethNames.Contains(iface)
            || nodes.ContainsKey(NodeKey(PacketPathNodeKind.Bridge, iface))
            || nodes.ContainsKey(NodeKey(PacketPathNodeKind.VlanInterface, iface)))
        {
            return true;
        }

        return vlanInterfaces.Records.Any(r =>
            string.Equals(Get(ToDict(r.KnownProperties), "name"), iface, StringComparison.Ordinal));
    }

    private static void AddNode(
        Dictionary<string, PacketPathTopologyNode> nodes,
        PacketPathNodeKind kind,
        string key,
        string? name,
        Dictionary<string, string> attributes)
    {
        if (nodes.ContainsKey(key))
        {
            return;
        }

        // Drop empty attribute values for stable hashes.
        Dictionary<string, string> cleaned = attributes
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        nodes[key] = new PacketPathTopologyNode
        {
            Kind = kind,
            Key = key,
            Name = name,
            Attributes = cleaned,
        };
    }

    private static string NodeKey(PacketPathNodeKind kind, string name)
        => kind switch
        {
            PacketPathNodeKind.Container => $"container:{name}",
            PacketPathNodeKind.App => $"app:{name}",
            PacketPathNodeKind.Veth => $"veth:{name}",
            PacketPathNodeKind.Bridge => $"bridge:{name}",
            PacketPathNodeKind.VlanInterface => $"vlanif:{name}",
            PacketPathNodeKind.Vrf => $"vrf:{name}",
            PacketPathNodeKind.Interface => $"iface:{name}",
            _ => $"{kind}:{name}",
        };

    private static IEnumerable<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            yield return part;
        }
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

    private static RosReadCommandResult OkEmpty(RosReadCommandId id)
        => new()
        {
            CommandId = id,
            Lifecycle = RosCommandLifecycle.Completed,
            Records = [],
            SessionInvalidated = false,
            Error = null,
        };

    private static Dictionary<string, string> ToDict(IReadOnlyDictionary<string, string> source)
        => source.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

    private static string? Get(Dictionary<string, string> known, string name)
        => known.TryGetValue(name, out string? value) ? value : null;
}
