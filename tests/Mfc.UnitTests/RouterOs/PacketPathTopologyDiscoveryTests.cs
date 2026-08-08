using System.Text.Json;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class PacketPathTopologyDiscoveryTests
{
    [Fact]
    public void ProjectsContainerAppVethBridgeVlanVrfGraphWithoutOneToOneAssumptions()
    {
        PacketPathTopologyResult result = PacketPathTopologyDiscovery.BuildResult(
            containers: Ok(
                RosReadCommandId.Containers,
                Row(("name", "pihole"), ("interface", "veth1"), ("status", "running")),
                Row(("name", "pg"), ("interface", "veth1"), ("status", "stopped"))),
            apps: Ok(
                RosReadCommandId.Apps,
                Row(("name", "store-app"), ("interface", "veth2"), ("running", "true"))),
            vethInterfaces: Ok(
                RosReadCommandId.VethInterfaces,
                Row(("name", "veth1"), ("address", "172.17.0.2/24"), ("gateway", "172.17.0.1"), ("running", "true")),
                Row(("name", "veth2"), ("address", "172.18.0.2/24"), ("running", "true"))),
            vlanInterfaces: Ok(
                RosReadCommandId.VlanInterfaces,
                Row(("name", "vlan120"), ("vlan-id", "120"), ("interface", "bridge1"), ("running", "true"))),
            bridges: Bridges(
                bridgeName: "bridge1",
                ports:
                [
                    ("bridge1", "veth1", "10"),
                    ("bridge1", "ether2", "1"),
                ],
                vlans:
                [
                    ("bridge1", "10", "ether1", "veth1"),
                ]),
            vrfs: Ok(
                RosReadCommandId.IpVrfs,
                Row(("name", "containers"), ("interfaces", "vlan120,veth2"))));

        Assert.Contains(result.Edges, e => e.Kind == PacketPathEdgeKind.UsesVeth && e.FromKey == "container:pihole" && e.ToKey == "veth:veth1");
        Assert.Contains(result.Edges, e => e.Kind == PacketPathEdgeKind.UsesVeth && e.FromKey == "container:pg" && e.ToKey == "veth:veth1");
        Assert.Contains(result.SharedVethNames, n => n == "veth1");
        Assert.Contains(result.Findings, f => f.Code == DiscoveryFinding.SharedVethMultiEndpoint);
        Assert.Contains(result.Edges, e => e.Kind == PacketPathEdgeKind.BridgeMember && e.FromKey == "veth:veth1" && e.ToKey == "bridge:bridge1");
        Assert.Contains(result.Edges, e => e.Kind == PacketPathEdgeKind.BridgeVlanMembership && e.Attributes["role"] == "untagged");
        Assert.Contains(result.Edges, e => e.Kind == PacketPathEdgeKind.VlanOnParent && e.FromKey == "vlanif:vlan120" && e.ToKey == "bridge:bridge1");
        Assert.Contains(result.Edges, e => e.Kind == PacketPathEdgeKind.VrfMember && e.ToKey == "vrf:containers");
        Assert.False(result.AssumesBridgeTrafficPassesIpFirewall);
        Assert.Contains(result.ConfigurationHashMaterial.Keys, k => k.Contains("veth:veth1.address", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ConfigurationHashMaterial.Keys, k => k.EndsWith(".status", StringComparison.Ordinal));
        Assert.Contains(result.ObservationHashMaterial.Keys, k => k.EndsWith(".status", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingVethAndVrfReferencesProduceFindings()
    {
        PacketPathTopologyResult result = PacketPathTopologyDiscovery.BuildResult(
            containers: Ok(RosReadCommandId.Containers, Row(("name", "orphan"), ("interface", "veth-missing"))),
            apps: Ok(RosReadCommandId.Apps),
            vethInterfaces: Ok(RosReadCommandId.VethInterfaces),
            vlanInterfaces: Ok(RosReadCommandId.VlanInterfaces),
            bridges: Bridges("bridge1", [], []),
            vrfs: Ok(RosReadCommandId.IpVrfs, Row(("name", "wan"), ("interfaces", "ether99"))));

        Assert.Contains(result.Findings, f => f.Code == DiscoveryFinding.MissingVethReference && f.Subject == "orphan");
        Assert.Contains(result.Findings, f => f.Code == DiscoveryFinding.MissingVrfInterfaceReference);
    }

    [Fact]
    public void SanitizedFixtureCoversFullChain()
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests",
            "Mfc.UnitTests",
            "RouterOs",
            "Fixtures",
            "packet-path-topology.sanitized.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("container-veth-bridge-vlan-vrf", doc.RootElement.GetProperty("scenario").GetString());
        Assert.False(doc.RootElement.GetProperty("assumesBridgeTrafficPassesIpFirewall").GetBoolean());
        Assert.Contains(
            doc.RootElement.GetProperty("sharedVethNames").EnumerateArray().Select(e => e.GetString()),
            n => n == "veth1");
        Assert.True(doc.RootElement.GetProperty("edges").GetArrayLength() >= 5);
    }

    private static BridgeSwitchDiscoveryResult Bridges(
        string bridgeName,
        (string Bridge, string Interface, string Pvid)[] ports,
        (string Bridge, string VlanIds, string Tagged, string Untagged)[] vlans)
    {
        RosReadCommandResult bridgeRows = Ok(
            RosReadCommandId.Bridges,
            Row(("name", bridgeName), ("vlan-filtering", "true"), ("pvid", "1"), ("running", "true")));
        RosReadCommandResult portRows = Ok(
            RosReadCommandId.BridgePorts,
            ports.Select(p => Row(("bridge", p.Bridge), ("interface", p.Interface), ("pvid", p.Pvid))).ToArray());
        RosReadCommandResult vlanRows = Ok(
            RosReadCommandId.BridgeVlans,
            vlans.Select(v => Row(
                ("bridge", v.Bridge),
                ("vlan-ids", v.VlanIds),
                ("tagged", v.Tagged),
                ("untagged", v.Untagged))).ToArray());
        return BridgeSwitchDiscovery.BuildResult(
            bridgeRows,
            portRows,
            Ok(RosReadCommandId.BridgeSettings, Row(("use-ip-firewall", "false"))),
            vlanRows,
            Ok(RosReadCommandId.EthernetSwitches),
            Ok(RosReadCommandId.EthernetSwitchPorts));
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static RosReadCommandResult Ok(RosReadCommandId id, params RosReadRecord[] rows)
        => new()
        {
            CommandId = id,
            Lifecycle = RosCommandLifecycle.Completed,
            Records = rows,
            SessionInvalidated = false,
            Error = null,
        };

    private static RosReadRecord Row(params (string Name, string Value)[] properties)
    {
        Dictionary<string, string> known = new(StringComparer.Ordinal);
        foreach ((string name, string value) in properties)
        {
            known[name] = value;
        }

        return new RosReadRecord
        {
            KnownProperties = known,
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
