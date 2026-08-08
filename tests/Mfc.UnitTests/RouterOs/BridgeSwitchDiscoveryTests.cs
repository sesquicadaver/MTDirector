using System.Text.Json;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class BridgeSwitchDiscoveryTests
{
    [Fact]
    public void SeparatesHwOffloadObservationFromBridgeVlanConfiguration()
    {
        BridgeSwitchDiscoveryResult result = Build(
            bridges: Ok(
                RosReadCommandId.Bridges,
                Row(
                    ("name", "bridge1"),
                    ("vlan-filtering", "true"),
                    ("protocol-mode", "rstp"),
                    ("pvid", "1"),
                    ("running", "true"),
                    ("root-bridge", "true"))),
            ports: Ok(
                RosReadCommandId.BridgePorts,
                Row(
                    ("bridge", "bridge1"),
                    ("interface", "ether2"),
                    ("pvid", "10"),
                    ("hw", "true"),
                    ("hw-offload", "true"),
                    ("role", "DesignatedPort"))),
            settings: Ok(
                RosReadCommandId.BridgeSettings,
                Row(
                    ("use-ip-firewall", "false"),
                    ("use-ip-firewall-for-vlan", "false"),
                    ("allow-fast-path", "true"),
                    ("bridge-fast-path-active", "true"))),
            vlans: Ok(
                RosReadCommandId.BridgeVlans,
                Row(
                    ("bridge", "bridge1"),
                    ("vlan-ids", "10"),
                    ("tagged", "ether1"),
                    ("untagged", "ether2"),
                    ("current-tagged", "ether1"),
                    ("current-untagged", "ether2"))));

        Assert.Equal("true", result.ConfigurationHashMaterial["bridge.bridge1.vlan-filtering"]);
        Assert.Equal("10", result.ConfigurationHashMaterial["bvlan.0.vlan-ids"]);
        Assert.DoesNotContain(result.ConfigurationHashMaterial.Keys, k => k.Contains("hw-offload", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ConfigurationHashMaterial.Keys, k => k.Contains("current-tagged", StringComparison.Ordinal));
        Assert.Equal("true", result.ObservationHashMaterial["bport.bridge1.ether2.hw-offload"]);
        Assert.Contains(BridgePathRoleIndicator.HardwareOffloadObserved, result.PathRoleIndicators);
        Assert.False(result.AssumesHardwareSwitchedTrafficPassesIpFirewall);
    }

    [Fact]
    public void UnknownSwitchChipProducesFindingAndNeverGrantsWrite()
    {
        BridgeSwitchDiscoveryResult result = Build(
            switches: Ok(
                RosReadCommandId.EthernetSwitches,
                Row(("name", "switch1"), ("type", "unknown"), ("l3-hw-offloading", "yes"))),
            switchPorts: Ok(
                RosReadCommandId.EthernetSwitchPorts,
                Row(
                    ("name", "ether1"),
                    ("switch", "switch1"),
                    ("vlan-mode", "secure"),
                    ("l3-hw-offloading", "yes"))));

        Assert.Contains(result.Findings, f => f.Code == DiscoveryFinding.UnknownSwitchChip);
        Assert.False(Assert.Single(result.EthernetSwitches).HasKnownChipProfile);
        Assert.False(result.GrantsSwitchWriteCapability);
        Assert.False(result.CompilesTransitAcl);
        Assert.Contains(BridgePathRoleIndicator.UnknownSwitchChip, result.PathRoleIndicators);
        Assert.Contains(BridgePathRoleIndicator.L3HardwareOffloadConfigured, result.PathRoleIndicators);
    }

    [Fact]
    public void UseIpFirewallMarksBridgedTrafficMayHitCpuWithoutAssumingHwPath()
    {
        BridgeSwitchDiscoveryResult result = Build(
            settings: Ok(
                RosReadCommandId.BridgeSettings,
                Row(("use-ip-firewall", "true"), ("use-ip-firewall-for-vlan", "true"))));

        Assert.Contains(BridgePathRoleIndicator.BridgedTrafficMayHitIpFirewall, result.PathRoleIndicators);
        Assert.False(result.AssumesHardwareSwitchedTrafficPassesIpFirewall);
        Assert.Equal("true", result.ConfigurationHashMaterial["bset.use-ip-firewall"]);
    }

    [Fact]
    public void DiscoveryTargetsAreRouterOsOnlyNeverSwOs()
    {
        foreach (RosReadCommandId id in BridgeSwitchDiscovery.DiscoveryCommandIds)
        {
            string path = RosReadCommandRegistry.Get(id).FixedPath;
            Assert.StartsWith("/interface/", path, StringComparison.Ordinal);
            Assert.DoesNotContain("swos", path, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("/print", path, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("bridge-switch-router.sanitized.json", "router")]
    [InlineData("bridge-switch-crs.sanitized.json", "crs")]
    [InlineData("bridge-switch-unknown.sanitized.json", "unknown")]
    public void SanitizedFixturesCoverRouterCrsAndUnknownBoard(string fileName, string boardClass)
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests",
            "Mfc.UnitTests",
            "RouterOs",
            "Fixtures",
            fileName);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(boardClass, doc.RootElement.GetProperty("boardClass").GetString());
        Assert.True(doc.RootElement.GetProperty("bridges").GetArrayLength() >= 1);
        Assert.False(doc.RootElement.GetProperty("assumesHardwareSwitchedTrafficPassesIpFirewall").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("grantsSwitchWriteCapability").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("compilesTransitAcl").GetBoolean());
        if (boardClass == "unknown")
        {
            Assert.Contains(
                doc.RootElement.GetProperty("findings").EnumerateArray(),
                f => f.GetProperty("code").GetString() == DiscoveryFinding.UnknownSwitchChip);
        }
    }

    private static BridgeSwitchDiscoveryResult Build(
        RosReadCommandResult? bridges = null,
        RosReadCommandResult? ports = null,
        RosReadCommandResult? settings = null,
        RosReadCommandResult? vlans = null,
        RosReadCommandResult? switches = null,
        RosReadCommandResult? switchPorts = null)
        => BridgeSwitchDiscovery.BuildResult(
            bridges ?? Ok(RosReadCommandId.Bridges),
            ports ?? Ok(RosReadCommandId.BridgePorts),
            settings ?? Ok(RosReadCommandId.BridgeSettings, Row(("use-ip-firewall", "false"))),
            vlans ?? Ok(RosReadCommandId.BridgeVlans),
            switches ?? Ok(RosReadCommandId.EthernetSwitches),
            switchPorts ?? Ok(RosReadCommandId.EthernetSwitchPorts));

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
