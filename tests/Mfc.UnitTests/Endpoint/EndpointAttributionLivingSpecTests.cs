using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Commands;
using Xunit;

namespace Mfc.UnitTests.Endpoint;

/// <summary>Living Spec matrix for Issue Set M7.2-01 AC (endpoint attribution resolver).</summary>
public sealed class EndpointAttributionLivingSpecTests
{
    [Fact]
    public void Ac1LanIpResolvesThroughDhcpAndBridgeHost()
    {
        EndpointAttributionSnapshot snapshot = new()
        {
            DhcpLeases =
            [
                new DhcpLeaseFact
                {
                    IpAddress = "192.168.1.50",
                    MacAddress = "AA:BB:CC:DD:EE:01",
                    Interface = "dhcp1",
                    Status = "bound",
                },
            ],
            BridgeHostEntries =
            [
                new BridgeHostFact
                {
                    MacAddress = "AA:BB:CC:DD:EE:01",
                    VlanId = "10",
                    Bridge = "br-lan",
                    Port = "ether2",
                    Interface = "ether2",
                },
            ],
        };

        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipv4",
                IpAddress = "192.168.1.50",
            },
            snapshot);

        Assert.Equal(EndpointAttributionCertainty.Proven, result.Certainty);
        Assert.Equal(
            [
                EndpointAttributionHopKind.Ip,
                EndpointAttributionHopKind.Mac,
                EndpointAttributionHopKind.Vlan,
                EndpointAttributionHopKind.Bridge,
                EndpointAttributionHopKind.Port,
                EndpointAttributionHopKind.Interface,
            ],
            result.Chain.Hops.Select(static h => h.Kind).ToArray());
        Assert.Equal("192.168.1.50", result.Chain.Hops[0].Value);
        Assert.Equal("aa:bb:cc:dd:ee:01", result.Chain.Hops[1].Value);
        Assert.Equal("10", result.Chain.Hops[2].Value);
        Assert.Equal("br-lan", result.Chain.Hops[3].Value);
        Assert.Equal("ether2", result.Chain.Hops[4].Value);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Ac2ContainerIpResolvesThroughVethMapping()
    {
        EndpointAttributionSnapshot snapshot = new()
        {
            ArpEntries =
            [
                new ArpFact
                {
                    IpAddress = "172.17.0.5",
                    MacAddress = "02:42:AC:11:00:05",
                    Interface = "veth-web",
                },
            ],
            VethMappings =
            [
                new VethEndpointFact
                {
                    VethName = "veth-web",
                    ContainerName = "web-app",
                    IpAddress = "172.17.0.5",
                    MacAddress = "02:42:AC:11:00:05",
                    Interface = "veth-web",
                },
            ],
        };

        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipv4",
                IpAddress = "172.17.0.5",
            },
            snapshot);

        Assert.Equal(EndpointAttributionCertainty.Proven, result.Certainty);
        Assert.Contains(result.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.Veth && h.Value == "veth-web");
        Assert.Contains(result.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.Container && h.Value == "web-app");
    }

    [Fact]
    public void Ac3VpnInternalIpResolvesToWireGuardPeer()
    {
        EndpointAttributionSnapshot snapshot = new()
        {
            ArpEntries =
            [
                new ArpFact
                {
                    IpAddress = "10.8.0.3",
                    MacAddress = "DE:AD:BE:EF:00:03",
                    Interface = "wg0",
                },
            ],
            VpnSessions =
            [
                new VpnSessionFact
                {
                    Protocol = "wireguard",
                    InternalAddress = "10.8.0.3/32",
                    PeerName = "wg-client",
                    RemoteEndpoint = "198.51.100.20:51820",
                },
            ],
        };

        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipv4",
                IpAddress = "10.8.0.3",
            },
            snapshot);

        Assert.Equal(EndpointAttributionCertainty.Proven, result.Certainty);
        EndpointAttributionHop vpnHop = Assert.Single(result.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.VpnPeer);
        Assert.Equal("wg-client", vpnHop.Value);
        Assert.Equal("wireguard", vpnHop.Detail);
    }

    [Fact]
    public void Ac4AmbiguousMacSourcesProducePartialAndFinding()
    {
        EndpointAttributionSnapshot snapshot = new()
        {
            DhcpLeases =
            [
                new DhcpLeaseFact
                {
                    IpAddress = "192.168.1.60",
                    MacAddress = "AA:BB:CC:DD:EE:01",
                },
            ],
            ArpEntries =
            [
                new ArpFact
                {
                    IpAddress = "192.168.1.60",
                    MacAddress = "AA:BB:CC:DD:EE:02",
                    Interface = "ether1",
                },
            ],
        };

        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipv4",
                IpAddress = "192.168.1.60",
            },
            snapshot);

        Assert.Equal(EndpointAttributionCertainty.Partial, result.Certainty);
        EndpointAttributionFinding finding = Assert.Single(result.Findings);
        Assert.Equal(EndpointAttributionCodes.MacAmbiguous, finding.Code);
    }

    [Fact]
    public void Ac5UnknownIpProducesUnknownCertainty()
    {
        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipv4",
                IpAddress = "203.0.113.99",
            },
            new EndpointAttributionSnapshot());

        Assert.Equal(EndpointAttributionCertainty.Unknown, result.Certainty);
        EndpointAttributionFinding finding = Assert.Single(result.Findings);
        Assert.Equal(EndpointAttributionCodes.IpUnresolved, finding.Code);
        Assert.Equal(EndpointAttributionHopKind.Ip, Assert.Single(result.Chain.Hops).Kind);
    }

    [Fact]
    public void Ac6Ipv6NeighborDiscoveryPathResolvesMac()
    {
        EndpointAttributionSnapshot snapshot = new()
        {
            Ipv6Neighbors =
            [
                new Ipv6NeighborFact
                {
                    IpAddress = "fe80::1",
                    MacAddress = "AA:BB:CC:DD:EE:FF",
                    Interface = "ether1",
                },
            ],
            BridgeHostEntries =
            [
                new BridgeHostFact
                {
                    MacAddress = "AA:BB:CC:DD:EE:FF",
                    VlanId = "20",
                    Bridge = "br-core",
                    Port = "sfp1",
                },
            ],
        };

        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipv6",
                IpAddress = "fe80::1",
            },
            snapshot);

        Assert.Equal(EndpointAttributionCertainty.Proven, result.Certainty);
        Assert.Equal("nd", result.Chain.Hops.First(h => h.Kind == EndpointAttributionHopKind.Mac).Detail);
        Assert.Contains(result.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.Vlan && h.Value == "20");
    }

    [Fact]
    public void Ac7NoRoutingOrFirewallWriteApisOpened()
    {
        foreach (string path in EndpointAttributionAllowlist.FixedPaths)
        {
            Assert.True(RosReadCommandRegistry.IsAllowlistedPath(path), path);
        }

        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/arp/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/arp/set"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/dhcp-server/lease/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/interface/bridge/host/remove"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/routing/table/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/firewall/filter/add"));
        Assert.DoesNotContain(
            typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes(),
            static t => t.Namespace is not null
                        && t.Namespace.StartsWith("Mfc.RouterOs.Write", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac8InventoryAnchorsAttachedWhenProvided()
    {
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        DeviceId device = DeviceId.New();

        EndpointAttributionResult result = EndpointAttributionResolver.Resolve(
            new EndpointAttributionQuery
            {
                Family = "ipv4",
                IpAddress = "192.168.1.10",
                SiteId = site,
                NodeId = node,
                DeviceId = device,
            },
            new EndpointAttributionSnapshot
            {
                DhcpLeases =
                [
                    new DhcpLeaseFact
                    {
                        IpAddress = "192.168.1.10",
                        MacAddress = "AA:BB:CC:DD:EE:10",
                    },
                ],
            });

        Assert.Contains(result.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.Site && h.Value == site.Value.ToString("D"));
        Assert.Contains(result.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.Node && h.Value == node.Value.ToString("D"));
        Assert.Contains(result.Chain.Hops, h => h.Kind == EndpointAttributionHopKind.Device && h.Value == device.Value.ToString("D"));
    }
}
