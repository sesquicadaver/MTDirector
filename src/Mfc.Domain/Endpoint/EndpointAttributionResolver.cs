using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Endpoint;

/// <summary>
/// Deterministic endpoint attribution resolver (M7.2-01 / next-2 §3).
/// IP → MAC → VLAN → bridge → port → interface → VETH/container → VPN peer → Site/Node/Device.
/// </summary>
public static class EndpointAttributionResolver
{
    public const string AnalyzerVersion = "mfc.endpoint-attribution.v1";

    /// <summary>Resolves one IP against a scripted discovery snapshot.</summary>
    public static EndpointAttributionResult Resolve(
        EndpointAttributionQuery query,
        EndpointAttributionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!TryNormalizeFamily(query.Family, out string family))
        {
            return Unknown(
                [IpHop(query.IpAddress)],
                new EndpointAttributionFinding
                {
                    Code = EndpointAttributionCodes.UnsupportedFamily,
                    Message = $"Unsupported address family '{query.Family}'.",
                    Subject = query.Family,
                });
        }

        if (string.IsNullOrWhiteSpace(query.IpAddress))
        {
            throw new DomainInvariantException("Endpoint attribution query requires a non-empty IP address.");
        }

        string ip = query.IpAddress.Trim();
        List<EndpointAttributionHop> hops = [IpHop(ip)];
        List<EndpointAttributionFinding> findings = [];
        EndpointAttributionCertainty certainty = EndpointAttributionCertainty.Proven;

        MacResolution macResolution = ResolveMac(ip, family, snapshot);
        if (macResolution.Mac is null)
        {
            findings.Add(new EndpointAttributionFinding
            {
                Code = EndpointAttributionCodes.IpUnresolved,
                Message = $"No MAC address could be resolved for IP '{ip}'.",
                Subject = ip,
            });
            AttachInventoryAnchors(query, snapshot, hops);
            return new EndpointAttributionResult
            {
                Chain = new EndpointAttributionChain { Hops = hops },
                Certainty = EndpointAttributionCertainty.Unknown,
                Findings = findings,
            };
        }

        if (macResolution.Ambiguous)
        {
            certainty = EndpointAttributionCertainty.Partial;
            findings.Add(new EndpointAttributionFinding
            {
                Code = EndpointAttributionCodes.MacAmbiguous,
                Message = $"Conflicting MAC sources for IP '{ip}': {string.Join(", ", macResolution.Candidates.OrderBy(static m => m, StringComparer.Ordinal))}.",
                Subject = ip,
            });
        }

        string mac = macResolution.Mac;
        hops.Add(new EndpointAttributionHop
        {
            Kind = EndpointAttributionHopKind.Mac,
            Value = mac,
            Detail = macResolution.Source,
        });

        L2Resolution l2 = ResolveL2(mac, snapshot);
        if (l2.VlanId is not null)
        {
            hops.Add(new EndpointAttributionHop
            {
                Kind = EndpointAttributionHopKind.Vlan,
                Value = l2.VlanId,
                Detail = l2.VlanSource,
            });
        }
        else if (l2.PartialVlan)
        {
            certainty = Downgrade(certainty, EndpointAttributionCertainty.Partial);
            findings.Add(new EndpointAttributionFinding
            {
                Code = EndpointAttributionCodes.VlanPartial,
                Message = $"VLAN membership for MAC '{mac}' is incomplete.",
                Subject = mac,
            });
        }

        if (l2.Bridge is not null)
        {
            hops.Add(new EndpointAttributionHop
            {
                Kind = EndpointAttributionHopKind.Bridge,
                Value = l2.Bridge,
            });
        }
        else if (l2.PartialBridge)
        {
            certainty = Downgrade(certainty, EndpointAttributionCertainty.Partial);
            findings.Add(new EndpointAttributionFinding
            {
                Code = EndpointAttributionCodes.BridgePartial,
                Message = $"Bridge attribution for MAC '{mac}' is incomplete.",
                Subject = mac,
            });
        }

        if (l2.Port is not null)
        {
            hops.Add(new EndpointAttributionHop
            {
                Kind = EndpointAttributionHopKind.Port,
                Value = l2.Port,
            });
        }

        if (l2.Interface is not null)
        {
            hops.Add(new EndpointAttributionHop
            {
                Kind = EndpointAttributionHopKind.Interface,
                Value = l2.Interface,
            });
        }

        VethResolution veth = ResolveVeth(ip, mac, macResolution.Interface ?? l2.Interface, snapshot);
        if (veth.VethName is not null)
        {
            hops.Add(new EndpointAttributionHop
            {
                Kind = EndpointAttributionHopKind.Veth,
                Value = veth.VethName,
            });
        }

        if (veth.ContainerName is not null)
        {
            hops.Add(new EndpointAttributionHop
            {
                Kind = EndpointAttributionHopKind.Container,
                Value = veth.ContainerName,
            });
        }
        else if (veth.Partial)
        {
            certainty = Downgrade(certainty, EndpointAttributionCertainty.Partial);
            findings.Add(new EndpointAttributionFinding
            {
                Code = EndpointAttributionCodes.VethPartial,
                Message = $"VETH/container mapping for IP '{ip}' is incomplete.",
                Subject = ip,
            });
        }

        VpnResolution vpn = ResolveVpn(ip, snapshot);
        if (vpn.PeerName is not null)
        {
            hops.Add(new EndpointAttributionHop
            {
                Kind = EndpointAttributionHopKind.VpnPeer,
                Value = vpn.PeerName,
                Detail = vpn.Protocol,
            });
        }
        else if (vpn.Partial)
        {
            certainty = Downgrade(certainty, EndpointAttributionCertainty.Partial);
            findings.Add(new EndpointAttributionFinding
            {
                Code = EndpointAttributionCodes.VpnPartial,
                Message = $"VPN session attribution for IP '{ip}' is incomplete.",
                Subject = ip,
            });
        }

        AttachInventoryAnchors(query, snapshot, hops);

        if (certainty == EndpointAttributionCertainty.Proven
            && hops.Count == 2
            && vpn.PeerName is null
            && veth.ContainerName is null
            && l2.VlanId is null
            && l2.Bridge is null)
        {
            certainty = EndpointAttributionCertainty.Partial;
        }

        return new EndpointAttributionResult
        {
            Chain = new EndpointAttributionChain { Hops = hops },
            Certainty = certainty,
            Findings = findings,
        };
    }

    private static void AttachInventoryAnchors(
        EndpointAttributionQuery query,
        EndpointAttributionSnapshot snapshot,
        List<EndpointAttributionHop> hops)
    {
        SiteId? site = query.SiteId ?? snapshot.SiteId;
        NodeId? node = query.NodeId ?? snapshot.NodeId;
        DeviceId? device = query.DeviceId ?? snapshot.DeviceId;

        if (site is not null)
        {
            hops.Add(new EndpointAttributionHop
            {
                Kind = EndpointAttributionHopKind.Site,
                Value = site.Value.Value.ToString("D"),
            });
        }

        if (node is not null)
        {
            hops.Add(new EndpointAttributionHop
            {
                Kind = EndpointAttributionHopKind.Node,
                Value = node.Value.Value.ToString("D"),
            });
        }

        if (device is not null)
        {
            hops.Add(new EndpointAttributionHop
            {
                Kind = EndpointAttributionHopKind.Device,
                Value = device.Value.Value.ToString("D"),
            });
        }
    }

    private static MacResolution ResolveMac(string ip, string family, EndpointAttributionSnapshot snapshot)
    {
        List<DhcpLeaseFact> dhcpRows = snapshot.DhcpLeases
            .Where(l => IpEquals(l.IpAddress, ip))
            .ToList();
        HashSet<string> dhcp = dhcpRows
            .Select(l => NormalizeMac(l.MacAddress))
            .Where(static m => m.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<ArpFact> arpRows = snapshot.ArpEntries
            .Where(a => IpEquals(a.IpAddress, ip))
            .ToList();
        List<Ipv6NeighborFact> ndRows = snapshot.Ipv6Neighbors
            .Where(n => IpEquals(n.IpAddress, ip))
            .ToList();
        HashSet<string> arpOrNd = family == "ipv6"
            ? ndRows
                .Select(n => NormalizeMac(n.MacAddress))
                .Where(static m => m.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : arpRows
                .Select(a => NormalizeMac(a.MacAddress))
                .Where(static m => m.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<DhcpSnoopingBindingFact> snoopingRows = snapshot.DhcpSnoopingBindings
            .Where(b => IpEquals(b.IpAddress, ip))
            .ToList();
        HashSet<string> snooping = snoopingRows
            .Select(b => NormalizeMac(b.MacAddress))
            .Where(static m => m.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (dhcp.Count > 0)
        {
            if (dhcp.Count > 1 || Conflicts(dhcp, arpOrNd) || Conflicts(dhcp, snooping))
            {
                return Ambiguous(dhcp, arpOrNd, snooping, "dhcp");
            }

            DhcpLeaseFact lease = dhcpRows.First(l => MacEquals(l.MacAddress, dhcp.First()));
            return new MacResolution(dhcp.First(), false, "dhcp", Union(dhcp, arpOrNd, snooping), lease.Interface);
        }

        if (arpOrNd.Count > 0)
        {
            if (arpOrNd.Count > 1 || Conflicts(arpOrNd, snooping))
            {
                return Ambiguous(dhcp, arpOrNd, snooping, family == "ipv6" ? "nd" : "arp");
            }

            string? iface = family == "ipv6"
                ? ndRows.First(n => MacEquals(n.MacAddress, arpOrNd.First())).Interface
                : arpRows.First(a => MacEquals(a.MacAddress, arpOrNd.First())).Interface;
            return new MacResolution(
                arpOrNd.First(),
                false,
                family == "ipv6" ? "nd" : "arp",
                Union(dhcp, arpOrNd, snooping),
                iface);
        }

        if (snooping.Count > 0)
        {
            if (snooping.Count > 1)
            {
                return Ambiguous(dhcp, arpOrNd, snooping, "dhcp-snooping");
            }

            DhcpSnoopingBindingFact binding = snoopingRows.First(b => MacEquals(b.MacAddress, snooping.First()));
            return new MacResolution(
                snooping.First(),
                false,
                "dhcp-snooping",
                Union(dhcp, arpOrNd, snooping),
                binding.Port);
        }

        return new MacResolution(null, false, null, [], null);
    }

    private static L2Resolution ResolveL2(string mac, EndpointAttributionSnapshot snapshot)
    {
        BridgeHostFact? bridgeHost = snapshot.BridgeHostEntries
            .FirstOrDefault(h => MacEquals(h.MacAddress, mac));
        if (bridgeHost is not null)
        {
            return new L2Resolution(
                bridgeHost.VlanId,
                bridgeHost.Bridge,
                bridgeHost.Port ?? bridgeHost.Interface,
                bridgeHost.Interface ?? bridgeHost.Port,
                PartialVlan: false,
                PartialBridge: string.IsNullOrWhiteSpace(bridgeHost.Bridge),
                VlanSource: "bridge-host");
        }

        DhcpSnoopingBindingFact? snooping = snapshot.DhcpSnoopingBindings
            .FirstOrDefault(b => MacEquals(b.MacAddress, mac));
        if (snooping is not null)
        {
            return new L2Resolution(
                snooping.VlanId,
                snooping.Bridge,
                snooping.Port,
                snooping.Port,
                PartialVlan: string.IsNullOrWhiteSpace(snooping.VlanId),
                PartialBridge: string.IsNullOrWhiteSpace(snooping.Bridge),
                VlanSource: "dhcp-snooping");
        }

        VlanMembershipFact? vlan = snapshot.VlanMemberships
            .FirstOrDefault(v => MacEquals(v.Interface, mac));
        if (vlan is not null)
        {
            return new L2Resolution(
                vlan.VlanId,
                vlan.Bridge,
                vlan.Interface,
                vlan.Interface,
                PartialVlan: false,
                PartialBridge: string.IsNullOrWhiteSpace(vlan.Bridge),
                VlanSource: "vlan-table");
        }

        return new L2Resolution(null, null, null, null, false, false, null);
    }

    private static VethResolution ResolveVeth(
        string ip,
        string mac,
        string? interfaceName,
        EndpointAttributionSnapshot snapshot)
    {
        VethEndpointFact? byIp = snapshot.VethMappings
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.IpAddress) && IpEquals(v.IpAddress, ip));
        if (byIp is not null)
        {
            return new VethResolution(byIp.VethName, byIp.ContainerName ?? byIp.AppName, false);
        }

        VethEndpointFact? byMac = snapshot.VethMappings
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.MacAddress) && MacEquals(v.MacAddress, mac));
        if (byMac is not null)
        {
            return new VethResolution(byMac.VethName, byMac.ContainerName ?? byMac.AppName, false);
        }

        if (!string.IsNullOrWhiteSpace(interfaceName))
        {
            VethEndpointFact? byIface = snapshot.VethMappings
                .FirstOrDefault(v =>
                    string.Equals(v.VethName, interfaceName, StringComparison.Ordinal)
                    || string.Equals(v.Interface, interfaceName, StringComparison.Ordinal));
            if (byIface is not null)
            {
                return new VethResolution(byIface.VethName, byIface.ContainerName ?? byIface.AppName, false);
            }

            if (interfaceName.Contains("veth", StringComparison.OrdinalIgnoreCase))
            {
                return new VethResolution(interfaceName, null, true);
            }
        }

        return new VethResolution(null, null, false);
    }

    private static VpnResolution ResolveVpn(string ip, EndpointAttributionSnapshot snapshot)
    {
        VpnSessionFact? session = snapshot.VpnSessions
            .FirstOrDefault(v => IpMatchesVpnAddress(ip, v.InternalAddress));
        if (session is null)
        {
            return new VpnResolution(null, null, false);
        }

        string peer = session.PeerName
                      ?? session.User
                      ?? session.RemoteEndpoint
                      ?? session.Protocol;
        if (string.IsNullOrWhiteSpace(peer))
        {
            return new VpnResolution(null, session.Protocol, true);
        }

        return new VpnResolution(peer, session.Protocol, false);
    }

    private static bool IpMatchesVpnAddress(string ip, string internalAddress)
    {
        if (IpEquals(ip, internalAddress))
        {
            return true;
        }

        int slash = internalAddress.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0)
        {
            return false;
        }

        string prefix = internalAddress[..slash];
        return IpEquals(ip, prefix);
    }

    private static EndpointAttributionHop IpHop(string ip)
        => new()
        {
            Kind = EndpointAttributionHopKind.Ip,
            Value = ip,
        };

    private static EndpointAttributionResult Unknown(
        IReadOnlyList<EndpointAttributionHop> hops,
        EndpointAttributionFinding finding)
        => new()
        {
            Chain = new EndpointAttributionChain { Hops = hops },
            Certainty = EndpointAttributionCertainty.Unknown,
            Findings = [finding],
        };

    private static bool TryNormalizeFamily(string family, out string normalized)
    {
        normalized = family.Trim().ToLowerInvariant();
        return normalized is "ipv4" or "ipv6" or "ip4" or "ip6" or "4" or "6";
    }

    private static bool IpEquals(string left, string? right)
    {
        if (string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MacEquals(string left, string? right)
        => !string.IsNullOrWhiteSpace(right)
           && string.Equals(NormalizeMac(left), NormalizeMac(right), StringComparison.OrdinalIgnoreCase);

    internal static string NormalizeMac(string mac)
    {
        if (string.IsNullOrWhiteSpace(mac))
        {
            return string.Empty;
        }

        string compact = new string(mac.Where(static c => char.IsAsciiHexDigit(c)).ToArray()).ToLowerInvariant();
        if (compact.Length != 12)
        {
            return mac.Trim().ToLowerInvariant();
        }

        return string.Join(':', Enumerable.Range(0, 6).Select(i => compact.Substring(i * 2, 2)));
    }

    private static bool Conflicts(HashSet<string> primary, HashSet<string> secondary)
        => secondary.Count > 0 && !primary.SetEquals(secondary);

    private static MacResolution Ambiguous(
        HashSet<string> dhcp,
        HashSet<string> arpOrNd,
        HashSet<string> snooping,
        string source)
    {
        HashSet<string> all = Union(dhcp, arpOrNd, snooping);
        string? chosen = dhcp.FirstOrDefault() ?? arpOrNd.FirstOrDefault() ?? snooping.FirstOrDefault();
        return new MacResolution(chosen, true, source, all, null);
    }

    private static HashSet<string> Union(params HashSet<string>[] sets)
    {
        HashSet<string> all = new(StringComparer.OrdinalIgnoreCase);
        foreach (HashSet<string> set in sets)
        {
            all.UnionWith(set);
        }

        return all;
    }

    private static EndpointAttributionCertainty Downgrade(
        EndpointAttributionCertainty current,
        EndpointAttributionCertainty floor)
        => (EndpointAttributionCertainty)Math.Max((int)current, (int)floor);

    private sealed record MacResolution(
        string? Mac,
        bool Ambiguous,
        string? Source,
        IReadOnlyCollection<string> Candidates,
        string? Interface);

    private sealed record L2Resolution(
        string? VlanId,
        string? Bridge,
        string? Port,
        string? Interface,
        bool PartialVlan,
        bool PartialBridge,
        string? VlanSource);

    private sealed record VethResolution(string? VethName, string? ContainerName, bool Partial);

    private sealed record VpnResolution(string? PeerName, string? Protocol, bool Partial);
}
