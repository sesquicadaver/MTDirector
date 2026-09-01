using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Topology;
using Xunit;

namespace Mfc.UnitTests.Topology;

/// <summary>W6-02: Node-scoped VRRP pair consistency from last-capture sections.</summary>
public sealed class VrrpPairConsistencyAnalyzerTests
{
    private static readonly Guid DeviceA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DeviceB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Ac1AgreeingMembersPass()
    {
        Node node = VrrpNode();
        VrrpPairConsistencyResult result = VrrpPairConsistencyAnalyzer.Analyze(
            node,
            [
                Member("a", DeviceA, priority: "100", role: "Master"),
                Member("b", DeviceB, priority: "90", role: "Backup"),
            ]);

        Assert.True(result.Passed);
        Assert.DoesNotContain(result.Findings, static f => f.Severity == VrrpPairFindingSeverity.Blocker);
    }

    [Fact]
    public void Ac2VipMismatchIsBlocker()
    {
        Node node = VrrpNode();
        VrrpPairMemberInput a = Member("a", DeviceA, priority: "100", vip: "10.0.0.1/32");
        VrrpPairMemberInput b = Member("b", DeviceB, priority: "90", vip: "10.0.0.2/32");

        VrrpPairConsistencyResult result = VrrpPairConsistencyAnalyzer.Analyze(node, [a, b]);

        Assert.False(result.Passed);
        Assert.Contains(
            result.Findings,
            static f => f.Code == VrrpPairConsistencyFinding.ConfigFieldMismatch
                        && f.Subject != null
                        && f.Subject.Contains("addresses", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac3EqualPrioritiesAreFindingNotBlocker()
    {
        Node node = VrrpNode();
        VrrpPairConsistencyResult result = VrrpPairConsistencyAnalyzer.Analyze(
            node,
            [
                Member("a", DeviceA, priority: "100"),
                Member("b", DeviceB, priority: "100"),
            ]);

        Assert.True(result.Passed);
        Assert.Contains(
            result.Findings,
            static f => f.Code == VrrpPairConsistencyFinding.EqualPriorities
                        && f.Severity == VrrpPairFindingSeverity.Finding);
    }

    [Fact]
    public void Ac4FilterLogicalMismatchIsBlocker()
    {
        Node node = VrrpNode();
        VrrpPairMemberInput a = Member(
            "a",
            DeviceA,
            priority: "100",
            filterAction: "accept");
        VrrpPairMemberInput b = Member(
            "b",
            DeviceB,
            priority: "90",
            filterAction: "drop");

        VrrpPairConsistencyResult result = VrrpPairConsistencyAnalyzer.Analyze(node, [a, b]);

        Assert.False(result.Passed);
        Assert.Contains(
            result.Findings,
            static f => f.Code == VrrpPairConsistencyFinding.FilterLogicalMismatch);
    }

    [Fact]
    public void Ac5MissingCaptureIsBlocker()
    {
        Node node = VrrpNode();
        VrrpPairConsistencyResult result = VrrpPairConsistencyAnalyzer.Analyze(
            node,
            [
                Member("a", DeviceA, priority: "100"),
                new VrrpPairMemberInput
                {
                    DeviceId = new DeviceId(DeviceB),
                    DisplayName = "b",
                    Sections = [],
                },
            ]);

        Assert.False(result.Passed);
        Assert.Contains(
            result.Findings,
            static f => f.Code == VrrpPairConsistencyFinding.MissingCapture);
    }

    [Fact]
    public void Ac6AgreementFieldsListIsStable()
    {
        Assert.Equal(
            [
                "addresses",
                "version",
                "interval",
                "preemption-mode",
                "disabled",
                "sync-connection-tracking",
                "connection-tracking-port",
                "remote-address",
            ],
            VrrpPairConsistencyAnalyzer.AgreementConfigFields);
        Assert.DoesNotContain("priority", VrrpPairConsistencyAnalyzer.AgreementConfigFields);
        Assert.DoesNotContain("interface", VrrpPairConsistencyAnalyzer.AgreementConfigFields);
        Assert.DoesNotContain("name", VrrpPairConsistencyAnalyzer.AgreementConfigFields);
    }

    private static Node VrrpNode()
        => Node.Create(
            SiteId.New(),
            NonEmptyName.Create("pair"),
            NodeKind.Vrrp,
            DeclaredUplinkMode.One);

    private static VrrpPairMemberInput Member(
        string name,
        Guid deviceId,
        string priority,
        string vip = "10.0.0.1/32",
        string role = "Backup",
        string filterAction = "accept")
    {
        CanonicalSection vrrpConfig = new(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.HaVrrp,
            ordered: false,
            [
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["group"] = "Ipv4/vrid=10/if=ether1",
                    ["name"] = name + "-vrrp",
                    ["interface"] = name == "a" ? "ether1" : "ether2",
                    ["vrid"] = "10",
                    ["family"] = "Ipv4",
                    ["priority"] = priority,
                    ["version"] = "3",
                    ["interval"] = "1s",
                    ["preemption-mode"] = "yes",
                    ["disabled"] = "false",
                    ["sync-connection-tracking"] = "yes",
                    ["connection-tracking-port"] = "3780",
                    ["remote-address"] = "10.255.10.12",
                    ["addresses"] = vip,
                }),
            ]);
        CanonicalSection vrrpObs = new(
            CanonicalDomain.Observations,
            CanonicalSectionIds.HaVrrp,
            ordered: false,
            [
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["group"] = "Ipv4/vrid=10/if=ether1",
                    ["role"] = role,
                }),
            ]);
        CanonicalSection filter = new(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            [
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ordinal"] = "0",
                    ["chain"] = "forward",
                    ["action"] = filterAction,
                    ["comment"] = "lab",
                }),
            ]);

        return new VrrpPairMemberInput
        {
            DeviceId = new DeviceId(deviceId),
            DisplayName = name,
            Sections = [vrrpConfig, vrrpObs, filter],
        };
    }
}
