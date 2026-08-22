using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Maps endpoint-attribution RouterOS reads into Domain <see cref="EndpointAttributionSnapshot"/> (M7.2-01).
/// </summary>
public static class EndpointAttributionSnapshotMapper
{
    public static EndpointAttributionSnapshot Map(
        IReadOnlyDictionary<RosReadCommandId, RosReadCommandResult> reads,
        SiteId? siteId = null,
        NodeId? nodeId = null,
        DeviceId? deviceId = null)
    {
        ArgumentNullException.ThrowIfNull(reads);

        return new EndpointAttributionSnapshot
        {
            ArpEntries = MapArp(Get(reads, RosReadCommandId.Ipv4Arp)),
            DhcpLeases = MapDhcpLeases(Get(reads, RosReadCommandId.DhcpServerLeases)),
            DhcpSnoopingBindings = MapSnooping(Get(reads, RosReadCommandId.DhcpSnoopingBindings)),
            Ipv6Neighbors = MapNeighbors(Get(reads, RosReadCommandId.Ipv6Neighbors)),
            BridgeHostEntries = MapBridgeHosts(Get(reads, RosReadCommandId.BridgeHosts)),
            VlanMemberships = [],
            VpnSessions = MapVpnSessions(
                Get(reads, RosReadCommandId.WireGuardPeers),
                Get(reads, RosReadCommandId.IpsecActivePeers),
                Get(reads, RosReadCommandId.PppActiveSessions)),
            VethMappings = [],
            SiteId = siteId,
            NodeId = nodeId,
            DeviceId = deviceId,
        };
    }

    private static RosReadCommandResult Get(IReadOnlyDictionary<RosReadCommandId, RosReadCommandResult> reads, RosReadCommandId id)
        => reads.TryGetValue(id, out RosReadCommandResult? result) ? result : Empty(id);

    private static RosReadCommandResult Empty(RosReadCommandId id)
        => new()
        {
            CommandId = id,
            Lifecycle = RosCommandLifecycle.Completed,
            Records = [],
            SessionInvalidated = false,
            Error = null,
        };

    private static List<ArpFact> MapArp(RosReadCommandResult result)
    {
        List<ArpFact> facts = [];
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? address = Get(known, "address");
            string? mac = Get(known, "mac-address");
            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(mac))
            {
                continue;
            }

            facts.Add(new ArpFact
            {
                IpAddress = address,
                MacAddress = mac,
                Interface = Get(known, "interface"),
            });
        }

        return facts;
    }

    private static List<DhcpLeaseFact> MapDhcpLeases(RosReadCommandResult result)
    {
        List<DhcpLeaseFact> facts = [];
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? address = Get(known, "address");
            string? mac = Get(known, "mac-address") ?? Get(known, "active-mac-address");
            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(mac))
            {
                continue;
            }

            facts.Add(new DhcpLeaseFact
            {
                IpAddress = address,
                MacAddress = mac,
                Interface = Get(known, "server"),
                Status = Get(known, "status"),
            });
        }

        return facts;
    }

    private static List<DhcpSnoopingBindingFact> MapSnooping(RosReadCommandResult result)
    {
        List<DhcpSnoopingBindingFact> facts = [];
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? address = Get(known, "address");
            string? mac = Get(known, "mac-address");
            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(mac))
            {
                continue;
            }

            facts.Add(new DhcpSnoopingBindingFact
            {
                IpAddress = address,
                MacAddress = mac,
                VlanId = Get(known, "vlan"),
                Bridge = Get(known, "bridge"),
                Port = Get(known, "interface"),
            });
        }

        return facts;
    }

    private static List<Ipv6NeighborFact> MapNeighbors(RosReadCommandResult result)
    {
        List<Ipv6NeighborFact> facts = [];
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? address = Get(known, "address");
            string? mac = Get(known, "mac-address");
            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(mac))
            {
                continue;
            }

            facts.Add(new Ipv6NeighborFact
            {
                IpAddress = address,
                MacAddress = mac,
                Interface = Get(known, "interface"),
            });
        }

        return facts;
    }

    private static List<BridgeHostFact> MapBridgeHosts(RosReadCommandResult result)
    {
        List<BridgeHostFact> facts = [];
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? mac = Get(known, "mac-address");
            if (string.IsNullOrWhiteSpace(mac))
            {
                continue;
            }

            facts.Add(new BridgeHostFact
            {
                MacAddress = mac,
                Bridge = Get(known, "bridge"),
                Port = Get(known, "interface"),
                Interface = Get(known, "on-interface") ?? Get(known, "interface"),
            });
        }

        return facts;
    }

    private static List<VpnSessionFact> MapVpnSessions(
        RosReadCommandResult wireguard,
        RosReadCommandResult ipsec,
        RosReadCommandResult ppp)
    {
        List<VpnSessionFact> facts = [];

        foreach (RosReadRecord row in wireguard.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? allowed = Get(known, "allowed-address");
            if (string.IsNullOrWhiteSpace(allowed))
            {
                continue;
            }

            foreach (string address in SplitList(allowed))
            {
                facts.Add(new VpnSessionFact
                {
                    Protocol = "wireguard",
                    InternalAddress = address,
                    PeerName = Get(known, "name") ?? Get(known, "interface"),
                    RemoteEndpoint = Get(known, "current-endpoint-address"),
                });
            }
        }

        foreach (RosReadRecord row in ipsec.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? local = Get(known, "local-address");
            string? remote = Get(known, "remote-address");
            if (string.IsNullOrWhiteSpace(local) && string.IsNullOrWhiteSpace(remote))
            {
                continue;
            }

            facts.Add(new VpnSessionFact
            {
                Protocol = "ipsec",
                InternalAddress = local ?? remote!,
                PeerName = remote,
                RemoteEndpoint = remote,
            });
        }

        foreach (RosReadRecord row in ppp.Records)
        {
            Dictionary<string, string> known = ToDict(row.KnownProperties);
            string? address = Get(known, "address");
            if (string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            facts.Add(new VpnSessionFact
            {
                Protocol = "ppp",
                InternalAddress = address,
                PeerName = Get(known, "name"),
                User = Get(known, "caller-id"),
            });
        }

        return facts;
    }

    private static Dictionary<string, string> ToDict(IReadOnlyDictionary<string, string> source)
        => source.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

    private static string? Get(Dictionary<string, string> known, string name)
        => known.TryGetValue(name, out string? value) ? value : null;

    private static IEnumerable<string> SplitList(string value)
    {
        foreach (string part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            yield return part;
        }
    }
}
