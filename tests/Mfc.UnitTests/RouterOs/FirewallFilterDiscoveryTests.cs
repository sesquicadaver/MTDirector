using System.Text.Json;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class FirewallFilterDiscoveryTests
{
    [Fact]
    public void FamiliesAreSeparateAndOrderPreservedWithStaticOrdinals()
    {
        RosReadCommandResult ipv4 = Ok(
            RosReadCommandId.Ipv4Filter,
            Row(("chain", "forward"), ("action", "accept"), ("comment", "unmanaged"), ("disabled", "false")),
            Row(
                ("chain", "forward"),
                ("action", "fasttrack-connection"),
                ("comment", "fwc:rule:11111111-1111-1111-1111-111111111111:1"),
                ("connection-state", "established,related"),
                ("hw-offload", "yes"),
                ("disabled", "false")),
            Row(("chain", "forward"), ("action", "drop"), ("dynamic", "true"), ("comment", "dyn")),
            Row(("chain", "input"), ("action", "drop"), ("comment", "unmanaged drop"), ("disabled", "true")));
        RosReadCommandResult ipv6 = Ok(
            RosReadCommandId.Ipv6Filter,
            Row(("chain", "input"), ("action", "accept"), ("comment", "fwc:rule:aabb:1")));

        FirewallFilterDiscoveryResult result = FirewallFilterDiscovery.BuildResult(
            ipv4,
            ipv6,
            Ok(RosReadCommandId.Ipv4AddressLists),
            Ok(RosReadCommandId.Ipv6AddressLists));

        Assert.Equal(4, result.Ipv4FilterRules.Count);
        Assert.Single(result.Ipv6FilterRules);
        Assert.All(result.Ipv4FilterRules, r => Assert.Equal(IpAddressFamilyKind.Ipv4, r.Family));
        Assert.All(result.Ipv6FilterRules, r => Assert.Equal(IpAddressFamilyKind.Ipv6, r.Family));

        Assert.Equal(0, result.Ipv4FilterRules[0].EffectiveOrdinal);
        Assert.Equal(0, result.Ipv4FilterRules[0].StaticOrdinal);
        Assert.Equal(1, result.Ipv4FilterRules[1].EffectiveOrdinal);
        Assert.Equal(1, result.Ipv4FilterRules[1].StaticOrdinal);
        Assert.True(result.Ipv4FilterRules[2].IsDynamic);
        Assert.Null(result.Ipv4FilterRules[2].StaticOrdinal);
        Assert.Equal(2, result.Ipv4FilterRules[3].StaticOrdinal);
        Assert.Equal("true", result.Ipv4FilterRules[3].Disabled);
    }

    [Fact]
    public void FwcMarkerRecognizedWithoutMutationAndFastTrackKeepsActionFields()
    {
        RosReadCommandResult ipv4 = Ok(
            RosReadCommandId.Ipv4Filter,
            Row(
                ("chain", "forward"),
                ("action", "fasttrack-connection"),
                ("comment", "fwc:rule:11111111-1111-1111-1111-111111111111:1 keep-me"),
                ("connection-state", "established,related"),
                ("hw-offload", "yes"),
                ("disabled", "false")));

        FirewallFilterDiscoveryResult result = FirewallFilterDiscovery.BuildResult(
            ipv4,
            Ok(RosReadCommandId.Ipv6Filter),
            Ok(RosReadCommandId.Ipv4AddressLists),
            Ok(RosReadCommandId.Ipv6AddressLists));

        FirewallFilterRuleDiscovery rule = Assert.Single(result.Ipv4FilterRules);
        Assert.True(rule.HasFwcOwnershipMarker);
        Assert.Equal("fwc:rule:11111111-1111-1111-1111-111111111111:1", rule.FwcOwnershipMarker);
        Assert.Equal("fwc:rule:11111111-1111-1111-1111-111111111111:1 keep-me", rule.Comment);
        Assert.Equal("fasttrack-connection", rule.Action);
        Assert.Equal("yes", rule.HwOffload);
        Assert.Equal("established,related", rule.ConnectionState);
        Assert.False(FwcOwnershipMarker.IsManaged("unmanaged established"));
    }

    [Fact]
    public void UnknownMatchersStayInRawBagAndCountersExcludedFromHash()
    {
        RosReadCommandResult ipv4 = Ok(
            RosReadCommandId.Ipv4Filter,
            new RosReadRecord
            {
                KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["chain"] = "forward",
                    ["action"] = "accept",
                    ["comment"] = "x",
                    [".id"] = "*9",
                    ["bytes"] = "999",
                    ["packets"] = "9",
                },
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["mystery-matcher"] = "keep",
                    ["bytes"] = "should-drop",
                },
            });

        FirewallFilterDiscoveryResult result = FirewallFilterDiscovery.BuildResult(
            ipv4,
            Ok(RosReadCommandId.Ipv6Filter),
            Ok(RosReadCommandId.Ipv4AddressLists),
            Ok(RosReadCommandId.Ipv6AddressLists));

        FirewallFilterRuleDiscovery rule = Assert.Single(result.Ipv4FilterRules);
        Assert.Equal("keep", rule.RawProperties["mystery-matcher"]);
        Assert.False(rule.KnownProperties.ContainsKey("bytes"));
        Assert.False(rule.RawProperties.ContainsKey("bytes"));
        Assert.DoesNotContain(result.ConfigurationHashMaterial.Keys, k => k.Contains("*9", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ConfigurationHashMaterial.Values, v => v == "999");
        Assert.Equal("*9", rule.RouterOsRowId);
    }

    [Fact]
    public void DynamicAndTimeoutAddressListEntriesAreDigestedNotStored()
    {
        RosReadCommandResult lists = Ok(
            RosReadCommandId.Ipv4AddressLists,
            Row(("list", "BLOCK"), ("address", "10.0.0.1/32"), ("disabled", "false"), ("comment", "static")),
            Row(("list", "BLOCK"), ("address", "10.0.0.2/32"), ("dynamic", "true")),
            Row(("list", "BLOCK"), ("address", "10.0.0.3/32"), ("timeout", "1h")),
            Row(("list", "ALLOW"), ("address", "10.1.0.1/32"), ("dynamic", "true")));

        FirewallFilterDiscoveryResult result = FirewallFilterDiscovery.BuildResult(
            Ok(RosReadCommandId.Ipv4Filter),
            Ok(RosReadCommandId.Ipv6Filter),
            lists,
            Ok(RosReadCommandId.Ipv6AddressLists));

        Assert.Single(result.Ipv4StaticAddressListEntries);
        Assert.Equal("10.0.0.1/32", result.Ipv4StaticAddressListEntries[0].AddressCanonical);
        Assert.Equal(2, result.Ipv4DynamicAddressListSummaries.Count);
        DynamicAddressListSummary block = result.Ipv4DynamicAddressListSummaries.Single(s => s.ListName == "BLOCK");
        Assert.Equal(2, block.EntryCount);
        Assert.Equal(64, block.SortedEntryDigestHex.Length);
        Assert.DoesNotContain("10.0.0.2", result.ConfigurationHashMaterial.Values);
        Assert.DoesNotContain("10.0.0.3", result.ConfigurationHashMaterial.Values);
        Assert.Contains(
            result.ConfigurationHashMaterial.Keys,
            k => k.StartsWith("alist-dyn.4:BLOCK", StringComparison.Ordinal));
    }

    [Fact]
    public void FilterProfilesDoNotRequestCounters()
    {
        string v4 = RosReadCommandRegistry.Get(RosReadCommandId.Ipv4Filter).PropertyProfile.ProplistValue;
        string v6 = RosReadCommandRegistry.Get(RosReadCommandId.Ipv6Filter).PropertyProfile.ProplistValue;
        Assert.DoesNotContain("bytes", v4.Split(','), StringComparer.Ordinal);
        Assert.DoesNotContain("packets", v4.Split(','), StringComparer.Ordinal);
        Assert.Contains("hw-offload", v4.Split(','), StringComparer.Ordinal);
        Assert.Contains("hop-limit", v6.Split(','), StringComparer.Ordinal);
        Assert.DoesNotContain("fragment", v6.Split(','), StringComparer.Ordinal);
    }

    [Fact]
    public void SanitizedFixtureContainsUnmanagedAndFwcRules()
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests",
            "Mfc.UnitTests",
            "RouterOs",
            "Fixtures",
            "firewall-filter-discovery.sanitized.json");
        Assert.True(File.Exists(path));
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement rules = doc.RootElement.GetProperty("ipv4FilterRules");
        Assert.Contains(
            rules.EnumerateArray(),
            r => r.GetProperty("comment").GetString() == "unmanaged established");
        Assert.Contains(
            rules.EnumerateArray(),
            r => r.GetProperty("hasFwcOwnershipMarker").GetBoolean()
                 && r.GetProperty("action").GetString() == "fasttrack-connection");
        Assert.DoesNotContain("bytes", doc.RootElement.ToString(), StringComparison.Ordinal);
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
