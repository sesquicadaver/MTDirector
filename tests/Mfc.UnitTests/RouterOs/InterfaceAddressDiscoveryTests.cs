using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class InterfaceAddressDiscoveryTests
{
    [Fact]
    public void CidrNormalizerNormalizesIpv4()
    {
        Assert.True(CidrNormalizer.TryNormalizeIpv4("10.0.0.1/24", out string normalized, out _));
        Assert.Equal("10.0.0.1/24", normalized);
        Assert.False(CidrNormalizer.TryNormalizeIpv4("010.0.0.1/24", out _, out string? error));
        Assert.Contains("leading zeros", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CidrNormalizerRejectsCrossFamilyMixing()
    {
        Assert.False(CidrNormalizer.TryNormalizeIpv4("2001:db8::1/64", out _, out string? v4Error));
        Assert.Contains("IPv4", v4Error, StringComparison.Ordinal);
        Assert.False(CidrNormalizer.TryNormalizeIpv6("10.0.0.1/24", out _, out string? v6Error));
        Assert.Contains("IPv6", v6Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CidrNormalizerNormalizesIpv6Compressed()
    {
        Assert.True(CidrNormalizer.TryNormalizeIpv6("2001:0db8:0000:0000:0000:0000:0000:0001/64", out string n, out _));
        Assert.Equal("2001:db8::1/64", n);
    }

    [Fact]
    public void NestedIncludeExcludeIsDeterministicAndOrderIndependent()
    {
        InterfaceListSpec[] lists =
        [
            new() { Name = "ALL", Include = ["LAN", "WAN"], Exclude = ["BLOCK"] },
            new() { Name = "LAN", Include = [], Exclude = [] },
            new() { Name = "WAN", Include = [], Exclude = [] },
            new() { Name = "BLOCK", Include = [], Exclude = [] },
        ];
        InterfaceListMemberSpec[] membersA =
        [
            new() { List = "WAN", Interface = "ether2", Disabled = false },
            new() { List = "LAN", Interface = "ether1", Disabled = false },
            new() { List = "BLOCK", Interface = "ether1", Disabled = false },
            new() { List = "LAN", Interface = "bridge1", Disabled = false },
        ];
        InterfaceListMemberSpec[] membersB = membersA.Reverse().ToArray();
        HashSet<string> known = ["ether1", "ether2", "bridge1"];

        IReadOnlyList<ResolvedInterfaceListMembership> first =
            InterfaceListMembershipResolver.Resolve(lists, membersA, known, out _);
        IReadOnlyList<ResolvedInterfaceListMembership> second =
            InterfaceListMembershipResolver.Resolve(lists.Reverse(), membersB, known, out _);

        string[] allFirst = first.Single(r => r.ListName == "ALL").Members.ToArray();
        string[] allSecond = second.Single(r => r.ListName == "ALL").Members.ToArray();
        Assert.Equal(["bridge1", "ether2"], allFirst);
        Assert.Equal(allFirst, allSecond);
    }

    [Fact]
    public void IncludeCycleProducesFindingWithoutSilentEmptySuccess()
    {
        InterfaceListSpec[] lists =
        [
            new() { Name = "A", Include = ["B"], Exclude = [] },
            new() { Name = "B", Include = ["A"], Exclude = [] },
        ];

        IReadOnlyList<ResolvedInterfaceListMembership> resolved = InterfaceListMembershipResolver.Resolve(
            lists,
            Array.Empty<InterfaceListMemberSpec>(),
            new HashSet<string>(StringComparer.Ordinal),
            out IReadOnlyList<DiscoveryFinding> findings);

        Assert.Contains(findings, f => f.Code == DiscoveryFinding.InterfaceListCycle);
        Assert.Contains(resolved, r => r.HasCycle);
    }

    [Fact]
    public void MissingInterfaceReferenceCreatesFinding()
    {
        _ = InterfaceListMembershipResolver.Resolve(
            [new InterfaceListSpec { Name = "LAN", Include = [], Exclude = [] }],
            [new InterfaceListMemberSpec { List = "LAN", Interface = "missing0", Disabled = false }],
            new HashSet<string>(StringComparer.Ordinal) { "ether1" },
            out IReadOnlyList<DiscoveryFinding> findings);

        Assert.Contains(
            findings,
            f => f.Code == DiscoveryFinding.MissingInterfaceReference && f.Subject == "missing0");
    }

    [Fact]
    public void BuildResultSeparatesFamiliesDynamicAndExcludesRunningFromHash()
    {
        RosReadCommandResult interfaces = Ok(
            RosReadCommandId.Interfaces,
            Row(("name", "ether1"), ("type", "ether"), ("mtu", "1500"), ("running", "true"), ("disabled", "false")),
            Row(("name", "bridge1"), ("type", "bridge"), ("mtu", "1500"), ("running", "false"), ("disabled", "false")));
        RosReadCommandResult ipv4 = Ok(
            RosReadCommandId.Ipv4Addresses,
            Row(("address", "10.0.0.1/24"), ("interface", "ether1"), ("dynamic", "false")),
            Row(("address", "192.168.1.1/24"), ("interface", "bridge1"), ("dynamic", "true")),
            Row(("address", "not-a-cidr"), ("interface", "ether1"), ("dynamic", "false")));
        RosReadCommandResult ipv6 = Ok(
            RosReadCommandId.Ipv6Addresses,
            Row(("address", "2001:0db8::2/64"), ("interface", "ether1"), ("dynamic", "false")),
            Row(("address", "fe80::1/64"), ("interface", "ether1"), ("dynamic", "true")),
            Row(("address", "10.0.0.1/24"), ("interface", "ether1"), ("dynamic", "false")));
        RosReadCommandResult lists = Ok(
            RosReadCommandId.InterfaceLists,
            Row(("name", "LAN"), ("include", ""), ("exclude", "")));
        RosReadCommandResult members = Ok(
            RosReadCommandId.InterfaceListMembers,
            Row(("list", "LAN"), ("interface", "ether1"), ("disabled", "false")),
            Row(("list", "LAN"), ("interface", "ghost"), ("disabled", "false")));

        InterfaceAddressDiscoveryResult result = InterfaceAddressDiscovery.BuildResult(
            interfaces, ipv4, ipv6, lists, members);

        Assert.Equal(2, result.Ipv4StaticAddresses.Count);
        Assert.Single(result.Ipv4DynamicAddresses);
        Assert.Equal(2, result.Ipv6StaticAddresses.Count);
        Assert.Single(result.Ipv6DynamicAddresses);
        Assert.All(result.Ipv4StaticAddresses.Concat(result.Ipv4DynamicAddresses), a => Assert.Equal(IpAddressFamilyKind.Ipv4, a.Family));
        Assert.All(result.Ipv6StaticAddresses.Concat(result.Ipv6DynamicAddresses), a => Assert.Equal(IpAddressFamilyKind.Ipv6, a.Family));
        Assert.Contains(result.Ipv4StaticAddresses, a => a.AddressNormalized && a.AddressCidr == "10.0.0.1/24");
        Assert.Contains(result.Ipv6StaticAddresses, a => a.AddressNormalized && a.AddressCidr == "2001:db8::2/64");
        Assert.Contains(result.Findings, f => f.Code == DiscoveryFinding.InvalidCidr);
        Assert.Contains(result.Findings, f => f.Code == DiscoveryFinding.MissingInterfaceReference && f.Subject == "ghost");
        Assert.DoesNotContain(result.ConfigurationHashMaterial.Keys, k => k.Contains("running", StringComparison.Ordinal));
        Assert.DoesNotContain(result.ConfigurationHashMaterial.Keys, k => k.Contains("192.168.1.1", StringComparison.Ordinal));
        Assert.Equal("1500", result.ConfigurationHashMaterial["iface.ether1.mtu"]);
        Assert.Equal(
            ["ether1", "ghost"],
            result.ResolvedMembership.Single(r => r.ListName == "LAN").Members);
        Assert.Equal("true", result.Interfaces.Single(i => i.Name == "ether1").Running);
    }

    [Fact]
    public void PropertyProfilesRequestObservationFieldsSeparatelyFromConfig()
    {
        RosPropertyProfile interfaces = RosReadCommandRegistry.Get(RosReadCommandId.Interfaces).PropertyProfile;
        Assert.True(interfaces.TryGet("running", out RosPropertyDefinition? running));
        Assert.Equal(RosPropertyClassification.ObservationTyped, running!.Classification);
        Assert.True(interfaces.TryGet("name", out RosPropertyDefinition? name));
        Assert.Equal(RosPropertyClassification.ConfigTyped, name!.Classification);
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
