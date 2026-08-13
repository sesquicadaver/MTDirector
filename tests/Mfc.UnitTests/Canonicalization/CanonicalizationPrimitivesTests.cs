using System.Text;
using Mfc.Application.Snapshots;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Xunit;

namespace Mfc.UnitTests.Canonicalization;

public sealed class CanonicalizationPrimitivesTests
{
    private static readonly string[] ExpectedSortedSet = ["established", "new", "related"];
    private static readonly string[] ExpectedPreservedOrder = ["c", "a", "b"];
    private static readonly string[] SetInput = ["related", "established", "established", "new"];
    private static readonly string[] OrderedInput = ["c", "a", "b"];

    [Fact]
    public void RegistrySectionIdsExposeOrderedFlagAndIndex()
    {
        HashSet<string> orderedIds =
        [
            CanonicalSectionIds.FirewallIpv4Filter,
            CanonicalSectionIds.FirewallIpv6Filter,
            CanonicalSectionIds.FirewallIpv4Nat,
            CanonicalSectionIds.FirewallIpv6Nat,
            CanonicalSectionIds.FirewallIpv4Raw,
            CanonicalSectionIds.FirewallIpv6Raw,
            CanonicalSectionIds.FirewallIpv4Mangle,
            CanonicalSectionIds.FirewallIpv6Mangle,
            CanonicalSectionIds.RoutingRules,
        ];

        for (int i = 0; i < CanonicalSectionIds.AllInRegistryOrder.Count; i++)
        {
            string sectionId = CanonicalSectionIds.AllInRegistryOrder[i];
            Assert.Equal(orderedIds.Contains(sectionId), CanonicalSectionIds.IsOrdered(sectionId));
            Assert.True(CanonicalSectionIds.RegistryOrderIndex.TryGetValue(sectionId, out int index));
            Assert.Equal(i, index);
        }

        Assert.False(CanonicalSectionIds.IsOrdered("unknown.section"));
        Assert.False(CanonicalSectionIds.RegistryOrderIndex.ContainsKey("unknown.section"));
    }

    [Fact]
    public void IpAddressesAndPrefixesHaveCanonicalForm()
    {
        Assert.True(CanonicalIp.TryCanonicalizeAddress("192.168.1.10", out string v4, out _));
        Assert.Equal("192.168.1.10", v4);

        Assert.True(CanonicalIp.TryCanonicalizeAddress("2001:DB8::1", out string v6, out _));
        Assert.Equal("2001:db8::1", v6);

        Assert.True(CanonicalIp.TryCanonicalizeInterfaceAddress("192.168.1.19/24", out string ifaddr, out _));
        Assert.Equal("192.168.1.19/24", ifaddr);

        Assert.True(CanonicalIp.TryCanonicalizePrefix("192.168.1.19/24", out string prefix, out _));
        Assert.Equal("192.168.1.0/24", prefix);
    }

    [Fact]
    public void SetsAreSortedAndDeduplicatedAndOrderedCollectionsPreserveOrder()
    {
        IReadOnlyList<string> set = CanonicalCollections.CanonicalizeSet(SetInput);
        Assert.Equal(ExpectedSortedSet, set);

        IReadOnlyList<string> ordered = CanonicalCollections.PreserveOrder(OrderedInput);
        Assert.Equal(ExpectedPreservedOrder, ordered);
    }

    [Fact]
    public void EmptyAndDefaultValuesNormalizePerSchema()
    {
        CanonicalSection section = Canonicalizer.Canonicalize(new CanonicalSectionInput
        {
            Domain = CanonicalDomain.Configuration,
            SectionId = "system.identity",
            Ordered = true,
            Records =
            [
                new CanonicalRecordInput
                {
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["name"] = "gw1",
                        ["comment"] = string.Empty,
                        ["optional"] = null!,
                        ["disabled"] = "yes",
                    },
                },
            ],
        });

        Assert.Equal("gw1", section.Records[0].Properties["name"]);
        Assert.Equal(string.Empty, section.Records[0].Properties["comment"]);
        Assert.False(section.Records[0].Properties.ContainsKey("optional"));
        Assert.Equal("true", section.Records[0].Properties["disabled"]);
    }

    [Fact]
    public void NumbersUseInvariantCulture()
    {
        Assert.Equal("8729", CanonicalNumber.FormatUInt64(8729));
        Assert.True(CanonicalNumber.TryNormalizeInteger("08729", out string port, out _));
        Assert.Equal("8729", port);

        CanonicalSection section = Canonicalizer.Canonicalize(new CanonicalSectionInput
        {
            Domain = CanonicalDomain.Configuration,
            SectionId = "ip.service",
            Ordered = true,
            Records =
            [
                new CanonicalRecordInput
                {
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["port"] = "08729",
                        ["name"] = "api-ssl",
                    },
                },
            ],
        });
        Assert.Equal("8729", section.Records[0].Properties["port"]);
    }

    [Fact]
    public void JsonPropertyOrderIsDeterministic()
    {
        CanonicalSection a = Canonicalizer.Canonicalize(SampleConfigSection(includeNoise: true));
        CanonicalSection b = Canonicalizer.Canonicalize(SampleConfigSection(includeNoise: true));
        Assert.Equal(a.Utf8Bytes, b.Utf8Bytes);

        string json = Encoding.UTF8.GetString(a.Utf8Bytes);
        Assert.DoesNotContain(" ", json, StringComparison.Ordinal);
        Assert.StartsWith(
            "{\"schema\":\"mfc.canonical-section/1\",\"domain\":\"configuration\",\"section\":",
            json,
            StringComparison.Ordinal);
        int schema = json.IndexOf("\"schema\"", StringComparison.Ordinal);
        int domain = json.IndexOf("\"domain\"", StringComparison.Ordinal);
        int section = json.IndexOf("\"section\"", StringComparison.Ordinal);
        Assert.True(schema < domain && domain < section);
    }

    [Fact]
    public void IdAndCountersExcludedFromConfiguration()
    {
        Assert.True(CanonicalPropertyRules.IsExcludedFromConfiguration(".id"));
        Assert.True(CanonicalPropertyRules.IsExcludedFromConfiguration("bytes"));
        Assert.True(CanonicalPropertyRules.IsExcludedFromConfiguration("packets"));

        CanonicalSection section = Canonicalizer.Canonicalize(new CanonicalSectionInput
        {
            Domain = CanonicalDomain.Configuration,
            SectionId = "firewall.ipv4.filter",
            Ordered = true,
            Records =
            [
                new CanonicalRecordInput
                {
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [".id"] = "*1",
                        ["chain"] = "forward",
                        ["action"] = "accept",
                        ["bytes"] = "999",
                        ["packets"] = "42",
                    },
                },
            ],
        });

        Assert.False(section.Records[0].Properties.ContainsKey(".id"));
        Assert.False(section.Records[0].Properties.ContainsKey("bytes"));
        Assert.False(section.Records[0].Properties.ContainsKey("packets"));
        Assert.Equal("forward", section.Records[0].Properties["chain"]);
    }

    [Fact]
    public void ConfigurationAndObservationsHaveSeparateHashes()
    {
        CanonicalSection config = Canonicalizer.Canonicalize(SampleConfigSection(includeNoise: false));
        CanonicalSection observation = Canonicalizer.Canonicalize(new CanonicalSectionInput
        {
            Domain = CanonicalDomain.Observations,
            SectionId = "interface.runtime",
            Ordered = false,
            Records =
            [
                new CanonicalRecordInput
                {
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["name"] = "ether1",
                        ["running"] = "true",
                        ["bytes"] = "100",
                    },
                },
            ],
        });

        ConfigurationHash configurationHash = CanonicalHashContract.HashConfiguration(
            [(config.SectionId, CanonicalHashContract.HashSection(config))]);
        ObservationHash observationHash = CanonicalHashContract.HashObservations(
            [(observation.SectionId, CanonicalHashContract.HashSection(observation))]);

        Assert.NotEqual(configurationHash.ToString(), observationHash.ToString());

        SnapshotHash snapshotHash = CanonicalHashContract.HashSnapshot(1, configurationHash, observationHash);
        SnapshotHash again = CanonicalHashContract.HashSnapshot(1, configurationHash, observationHash);
        Assert.Equal(snapshotHash, again);

        SnapshotHash otherSchema = CanonicalHashContract.HashSnapshot(2, configurationHash, observationHash);
        Assert.NotEqual(snapshotHash, otherSchema);
    }

    [Fact]
    public void CanonicalizeIsIdempotentAndDeterministic()
    {
        CanonicalSection first = Canonicalizer.Canonicalize(SampleConfigSection(includeNoise: true));
        CanonicalSection second = Canonicalizer.Canonicalize(first);
        CanonicalSection third = Canonicalizer.Canonicalize(SampleConfigSection(includeNoise: true));

        Assert.Equal(first.Utf8Bytes, second.Utf8Bytes);
        Assert.Equal(first.Utf8Bytes, third.Utf8Bytes);
        Assert.Equal(
            CanonicalHashContract.HashSection(first),
            CanonicalHashContract.HashSection(second));
    }

    [Fact]
    public void ApplicationFacadeBuildsSnapshotHashBundle()
    {
        CanonicalSection config = CanonicalizationService.CanonicalizeSection(SampleConfigSection(includeNoise: false));
        CanonicalSection observation = CanonicalizationService.CanonicalizeSection(new CanonicalSectionInput
        {
            Domain = CanonicalDomain.Observations,
            SectionId = "vrrp.role",
            Ordered = true,
            Records =
            [
                new CanonicalRecordInput
                {
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["vrid"] = "10",
                        ["role"] = "master",
                    },
                },
            ],
        });

        SnapshotHashBundle bundle = CanonicalizationService.HashSnapshotBundle(1, [config], [observation]);
        Assert.Equal(1, bundle.SchemaVersion);
        Assert.Equal(64, bundle.ConfigurationHash.ToString().Length);
        Assert.Equal(64, bundle.ObservationHash.ToString().Length);
        Assert.Equal(64, bundle.SnapshotHash.ToString().Length);
        Assert.NotEqual(bundle.ConfigurationHash.ToString(), bundle.ObservationHash.ToString());
        Assert.Equal(
            CanonicalHashContract.HashSnapshot(1, bundle.ConfigurationHash, bundle.ObservationHash),
            bundle.SnapshotHash);
    }

    private static CanonicalSectionInput SampleConfigSection(bool includeNoise)
        => new()
        {
            Domain = CanonicalDomain.Configuration,
            SectionId = "system.identity",
            Ordered = true,
            Records =
            [
                new CanonicalRecordInput
                {
                    Properties = BuildProps(includeNoise),
                },
            ],
        };

    private static Dictionary<string, string> BuildProps(bool includeNoise)
    {
        Dictionary<string, string> props = new(StringComparer.Ordinal)
        {
            ["name"] = "lab-gw1",
            ["address"] = "10.0.0.1/24",
        };
        if (includeNoise)
        {
            props[".id"] = "*A";
            props["bytes"] = "123";
            props["packets"] = "9";
        }

        return props;
    }
}
