using System.Text.Json;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class PacketPathClassifierTests
{
    [Fact]
    public void ClassifiesCpuFirewallWhenNoHwEvidence()
    {
        PacketPathClassificationResult result = PacketPathClassifier.Classify(
            Bridge(
                useIpFirewall: false,
                ports:
                [
                    ("bridge1", "ether1", "false"),
                    ("bridge1", "ether2", "false"),
                ],
                switches: [],
                switchPorts: []));

        Assert.Equal(PacketPathClass.CpuFirewallPath, result.WorstPathClass);
        Assert.All(result.Pairs, p => Assert.Equal(PacketPathClass.CpuFirewallPath, p.PathClass));
        Assert.False(result.BlocksManagedForwardPolicy);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ClassifiesHardwareOffloadedWhenBothPortsOffloadOrL3Hw()
    {
        PacketPathClassificationResult both = PacketPathClassifier.Classify(
            Bridge(
                useIpFirewall: false,
                ports:
                [
                    ("bridge1", "ether1", "true"),
                    ("bridge1", "ether2", "true"),
                ],
                switches: [("switch1", "98DX8212", "no")],
                switchPorts: []));

        Assert.Equal(PacketPathClass.HardwareOffloadedPath, both.WorstPathClass);
        Assert.Contains(both.Findings, f => f.Code == DiscoveryFinding.PacketPathBypassesIpFirewall);
        Assert.All(
            both.Pairs,
            p => Assert.Equal(PacketPathBlockerHint.PacketPathBypassesIpFirewall, p.BlockerHint));

        PacketPathClassificationResult l3 = PacketPathClassifier.Classify(
            Bridge(
                useIpFirewall: false,
                ports:
                [
                    ("bridge1", "ether1", "false"),
                    ("bridge1", "ether2", "false"),
                ],
                switches: [("switch1", "98DX8212", "yes")],
                switchPorts: [("ether1", "switch1", "yes")]));

        Assert.Equal(PacketPathClass.HardwareOffloadedPath, l3.WorstPathClass);
    }

    [Fact]
    public void ClassifiesMixedWhenFirewallForcedWithOffload()
    {
        PacketPathClassificationResult result = PacketPathClassifier.Classify(
            Bridge(
                useIpFirewall: true,
                ports:
                [
                    ("bridge1", "ether1", "true"),
                    ("bridge1", "ether2", "false"),
                ],
                switches: [("switch1", "98DX8212", "no")],
                switchPorts: []));

        Assert.Equal(PacketPathClass.MixedPath, result.WorstPathClass);
        Assert.Contains(result.Pairs, p => p.PathClass == PacketPathClass.MixedPath);
        Assert.False(result.BlocksManagedForwardPolicy);
    }

    [Fact]
    public void ClassifiesIndeterminateForUnknownChipOrMissingPort()
    {
        PacketPathClassificationResult unknown = PacketPathClassifier.Classify(
            Bridge(
                useIpFirewall: false,
                ports:
                [
                    ("bridge1", "ether1", "true"),
                    ("bridge1", "ether2", "true"),
                ],
                switches: [("switch1", "unknown", "yes")],
                switchPorts: []));

        Assert.Equal(PacketPathClass.Indeterminate, unknown.WorstPathClass);
        Assert.Contains(unknown.Findings, f => f.Code == DiscoveryFinding.PacketPathNotProven);

        PacketPathClassificationResult missing = PacketPathClassifier.Classify(
            Bridge(
                useIpFirewall: false,
                ports: [("bridge1", "ether1", "false")],
                switches: [],
                switchPorts: []),
            pairs: [("ether1", "ether99", null)]);

        Assert.Equal(PacketPathClass.Indeterminate, missing.WorstPathClass);
    }

    [Fact]
    public void PathClassLivesInObservationHashNotConfigurationHash()
    {
        PacketPathClassificationResult result = PacketPathClassifier.Classify(
            Bridge(
                useIpFirewall: false,
                ports:
                [
                    ("bridge1", "ether1", "true"),
                    ("bridge1", "ether2", "true"),
                ],
                switches: [("switch1", "98DX8212", "no")],
                switchPorts: []));

        Assert.DoesNotContain(result.ConfigurationHashMaterial.Values, v => v.Contains("Hardware", StringComparison.Ordinal));
        Assert.Contains(result.ObservationHashMaterial.Values, v => v == nameof(PacketPathClass.HardwareOffloadedPath));
    }

    [Theory]
    [InlineData("packet-path-cpu.sanitized.json", "CPU_FIREWALL_PATH")]
    [InlineData("packet-path-hw.sanitized.json", "HARDWARE_OFFLOADED_PATH")]
    [InlineData("packet-path-mixed.sanitized.json", "MIXED_PATH")]
    [InlineData("packet-path-indeterminate.sanitized.json", "INDETERMINATE")]
    public void SanitizedFixturesCoverAllPathClasses(string fileName, string expectedClass)
    {
        string path = Path.Combine(FindRepoRoot(), "tests", "Mfc.UnitTests", "RouterOs", "Fixtures", fileName);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(expectedClass, doc.RootElement.GetProperty("worstPathClass").GetString());
        Assert.Equal(
            PacketPathClassifier.ParseClassName(expectedClass),
            PacketPathClassifier.ParseClassName(doc.RootElement.GetProperty("worstPathClass").GetString()!));
    }

    private static BridgeSwitchDiscoveryResult Bridge(
        bool useIpFirewall,
        (string Bridge, string Interface, string HwOffload)[] ports,
        (string Name, string Type, string L3Hw)[] switches,
        (string Name, string Switch, string L3Hw)[] switchPorts)
    {
        RosReadCommandResult bridgeRows = Ok(
            RosReadCommandId.Bridges,
            Row(("name", "bridge1"), ("vlan-filtering", "true"), ("running", "true")));
        RosReadCommandResult portRows = Ok(
            RosReadCommandId.BridgePorts,
            ports.Select(p => Row(
                ("bridge", p.Bridge),
                ("interface", p.Interface),
                ("hw-offload", p.HwOffload),
                ("hw", "true"))).ToArray());
        RosReadCommandResult settings = Ok(
            RosReadCommandId.BridgeSettings,
            Row(
                ("use-ip-firewall", useIpFirewall ? "true" : "false"),
                ("use-ip-firewall-for-vlan", "false")));
        RosReadCommandResult switchRows = Ok(
            RosReadCommandId.EthernetSwitches,
            switches.Select(s => Row(("name", s.Name), ("type", s.Type), ("l3-hw-offloading", s.L3Hw))).ToArray());
        RosReadCommandResult switchPortRows = Ok(
            RosReadCommandId.EthernetSwitchPorts,
            switchPorts.Select(p => Row(
                ("name", p.Name),
                ("switch", p.Switch),
                ("l3-hw-offloading", p.L3Hw))).ToArray());

        return BridgeSwitchDiscovery.BuildResult(
            bridgeRows,
            portRows,
            settings,
            Ok(RosReadCommandId.BridgeVlans),
            switchRows,
            switchPortRows);
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
