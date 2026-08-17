using System.Security.Cryptography;
using System.Text;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class RouterOsFilterArtifactTests
{
    private static readonly DeviceId Device =
        new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    private static readonly Hash256 ProfileHash =
        Hash256.ParseHex("1111111111111111111111111111111111111111111111111111111111111111");

    [Fact]
    public void Ac1Ac9CreateSealsImmutableAddressListsChainsAndAnchors()
    {
        RouterOsFilterArtifact artifact = CreateSampleArtifact();
        Assert.Single(artifact.AddressLists);
        Assert.Single(artifact.Chains);
        Assert.Single(artifact.AnchorTargets);
        Assert.Equal("mfc4.a.deadbeefdeadbeef", artifact.AddressLists[0].Name);
        Assert.Equal("mfc4.f.r." + artifact.ArtifactId, artifact.Chains[0].Name);
        Assert.Equal("mfc:anchor:v1:4:f", artifact.AnchorTargets[0].ExpectedAnchorComment);
        Assert.True(artifact.AddressLists.IsDefault == false);
        Assert.True(artifact.CanonicalBytes.IsDefault == false);
        Assert.Throws<NotSupportedException>(() =>
            ((System.Collections.IDictionary)artifact.Chains[0].Rules[0].Matchers).Add("x", "y"));
    }

    [Fact]
    public void Ac2Ac3RejectApiCommandsAndRouterOsId()
    {
        Assert.Throws<DomainInvariantException>(() =>
            FilterRuleArtifact.Create(0, action: "add", comment: "mfc:x"));
        Assert.Throws<DomainInvariantException>(() =>
            FilterRuleArtifact.Create(
                0,
                action: "jump",
                comment: "mfc:x",
                matchers: new Dictionary<string, string> { [".id"] = "*1" }));
        Assert.Throws<DomainInvariantException>(() =>
            FilterRuleArtifact.Create(
                0,
                action: "jump",
                comment: "mfc:x",
                matchers: new Dictionary<string, string> { ["id"] = "*1" }));
        Assert.Throws<DomainInvariantException>(() =>
            FilterRuleArtifact.Create(
                0,
                action: "set",
                comment: "mfc:rule:x"));
        Assert.Throws<DomainInvariantException>(() =>
            FilterRuleArtifact.Create(0, action: "accept", comment: "ticket-42 guest wifi"));
        Assert.Throws<DomainInvariantException>(() =>
            FilterRuleArtifact.Create(0, action: "accept", comment: "mfc:rule:.id=*1"));
        Assert.Throws<DomainInvariantException>(() =>
            FilterRuleArtifact.Create(0, action: "accept.id", comment: "mfc:rule:x"));
        Assert.Throws<DomainInvariantException>(() =>
            AnchorTargetArtifact.Create(
                IpAddressFamily.IPv4,
                FilterBuiltInContext.Forward,
                "mfc:anchor:v1:4:f",
                "mfc4.f.r..id"));
    }

    [Fact]
    public void Ac4Ac5Ac6Ac7IdentityHashesAreDeterministicAndExcludeTimestamps()
    {
        PhysicalSemanticsMaterial material = BuildSemantics();
        Hash256 semantics = RouterOsFilterArtifactIdentity.HashPhysicalSemantics(material);
        string artifactId = RouterOsFilterArtifactIdentity.ComputeArtifactId(ProfileHash, semantics, Device);
        Assert.Equal(16, artifactId.Length);
        Assert.Matches("^[0-9a-f]{16}$", artifactId);
        Assert.Equal(semantics.ToString(), RouterOsFilterArtifactIdentity.HashPhysicalSemantics(material).ToString());
        Assert.Equal(
            artifactId,
            RouterOsFilterArtifactIdentity.ComputeArtifactId(ProfileHash, semantics, Device));

        RouterOsFilterArtifact first = CreateSampleArtifact(semantics);
        RouterOsFilterArtifact second = CreateSampleArtifact(semantics);
        Assert.Equal(first.ArtifactId, second.ArtifactId);
        Assert.Equal(first.ResourceHash.ToString(), second.ResourceHash.ToString());
        Assert.Equal(first.CanonicalBytes.ToArray(), second.CanonicalBytes.ToArray());
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(first.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            first.ResourceHash.ToString());
    }

    [Fact]
    public void Ac8DescriptionOnlyChangeDoesNotAlterPhysicalSemanticsOrArtifact()
    {
        PhysicalSemanticsMaterial baseMaterial = BuildSemantics();
        Hash256 baseHash = RouterOsFilterArtifactIdentity.HashPhysicalSemantics(baseMaterial);

        PhysicalSemanticsMaterial sameSemantics = new()
        {
            LayoutVersion = baseMaterial.LayoutVersion,
            CompilerProfileHash = baseMaterial.CompilerProfileHash,
            RuleIds = baseMaterial.RuleIds,
            ResolvedPredicateDigests = baseMaterial.ResolvedPredicateDigests,
            ResolvedZoneDigests = baseMaterial.ResolvedZoneDigests,
            ActionDigests = baseMaterial.ActionDigests,
            LoggingDigests = baseMaterial.LoggingDigests,
            ChainContractDigests = baseMaterial.ChainContractDigests,
        };
        Assert.Equal(baseHash.ToString(), RouterOsFilterArtifactIdentity.HashPhysicalSemantics(sameSemantics).ToString());

        RouterOsFilterArtifact left = CreateSampleArtifact(baseHash);
        RouterOsFilterArtifact right = CreateSampleArtifact(baseHash);
        Assert.Equal(left.ResourceHash.ToString(), right.ResourceHash.ToString());
        Assert.Equal(left.ArtifactId, right.ArtifactId);

        // Free-form operator descriptions are rejected on the artifact surface (Spec §23).
        Assert.Throws<DomainInvariantException>(() =>
            FilterRuleArtifact.Create(0, "accept", "ticket-42 allow guest wifi for marketing"));
    }

    [Fact]
    public void Ac10CanonicalTestVectorsAreFixed()
    {
        PhysicalSemanticsMaterial material = new()
        {
            LayoutVersion = "1",
            CompilerProfileHash = ProfileHash,
            RuleIds = [Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")],
            ResolvedPredicateDigests = ["pred:accept-any"],
            ResolvedZoneDigests = ["zone:lan"],
            ActionDigests = ["action:accept"],
            LoggingDigests = ["log:false"],
            ChainContractDigests = ["contract:ipv4-forward-drop"],
        };
        Hash256 semantics = RouterOsFilterArtifactIdentity.HashPhysicalSemantics(material);
        Assert.Equal(
            "b9b55d08d4f688db8029c0550d6d0adde21ff5c94b1bcd0c10f02af990c4058e",
            semantics.ToString());

        string artifactId = RouterOsFilterArtifactIdentity.ComputeArtifactId(ProfileHash, semantics, Device);
        Assert.Equal("04b7904dd0fe5a2e", artifactId);

        RouterOsFilterArtifact artifact = RouterOsFilterArtifact.Create(
            ProfileHash,
            semantics,
            Device,
            addressLists:
            [
                new AddressListArtifactDraft
                {
                    Family = IpAddressFamily.IPv4,
                    Name = "mfc4.a.0123456789abcdef",
                    Entries = [AddressListEntryArtifact.Create("10.0.0.1")],
                },
            ],
            chains:
            [
                new ChainArtifactDraft
                {
                    Family = IpAddressFamily.IPv4,
                    BuiltInContext = FilterBuiltInContext.Forward,
                    Name = "mfc4.f.r." + artifactId,
                    Role = FilterChainArtifactRole.Root,
                    Rules =
                    [
                        FilterRuleArtifact.Create(
                            ordinal: 0,
                            action: "accept",
                            comment: "mfc:rule:bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb:0",
                            logicalRuleId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                            matchers: new Dictionary<string, string> { ["connection-state"] = "established,related" }),
                    ],
                },
            ],
            anchorTargets:
            [
                AnchorTargetArtifact.Create(
                    IpAddressFamily.IPv4,
                    FilterBuiltInContext.Forward,
                    expectedAnchorComment: "mfc:anchor:v1:4:f",
                    desiredJumpTarget: "mfc4.f.r." + artifactId),
            ]);

        string json = Encoding.UTF8.GetString(artifact.CanonicalBytes.AsSpan());
        Assert.StartsWith(
            "{\"schema\":\"mfc.routeros-filter-artifact/1\",\"layoutVersion\":\"1\",\"artifactId\":",
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain(" ", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain(".id", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"add\"", json, StringComparison.Ordinal);
        Assert.Equal(artifactId, artifact.ArtifactId);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(artifact.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            artifact.ResourceHash.ToString());
    }

    [Fact]
    public void SortingIsDeterministicRegardlessOfInputOrder()
    {
        Hash256 semantics = RouterOsFilterArtifactIdentity.HashPhysicalSemantics(BuildSemantics());
        string id = RouterOsFilterArtifactIdentity.ComputeArtifactId(ProfileHash, semantics, Device);
        RouterOsFilterArtifact a = RouterOsFilterArtifact.Create(
            ProfileHash,
            semantics,
            Device,
            addressLists:
            [
                new AddressListArtifactDraft
                {
                    Family = IpAddressFamily.IPv6,
                    Name = "mfc6.a.aaaaaaaaaaaaaaaa",
                    Entries = [AddressListEntryArtifact.Create("2001:db8::2"), AddressListEntryArtifact.Create("2001:db8::1")],
                },
                new AddressListArtifactDraft
                {
                    Family = IpAddressFamily.IPv4,
                    Name = "mfc4.a.bbbbbbbbbbbbbbbb",
                    Entries = [AddressListEntryArtifact.Create("10.0.0.2")],
                },
            ],
            chains:
            [
                new ChainArtifactDraft
                {
                    Family = IpAddressFamily.IPv4,
                    BuiltInContext = FilterBuiltInContext.Input,
                    Name = "mfc4.i.r." + id,
                    Role = FilterChainArtifactRole.Root,
                    Rules = [FilterRuleArtifact.Create(0, "return", "mfc:term")],
                },
                new ChainArtifactDraft
                {
                    Family = IpAddressFamily.IPv4,
                    BuiltInContext = FilterBuiltInContext.Forward,
                    Name = "mfc4.f.r." + id,
                    Role = FilterChainArtifactRole.Root,
                    Rules = [FilterRuleArtifact.Create(0, "return", "mfc:term")],
                },
            ],
            anchorTargets:
            [
                AnchorTargetArtifact.Create(
                    IpAddressFamily.IPv4,
                    FilterBuiltInContext.Forward,
                    "mfc:anchor:v1:4:f",
                    "mfc4.f.r." + id),
                AnchorTargetArtifact.Create(
                    IpAddressFamily.IPv4,
                    FilterBuiltInContext.Input,
                    "mfc:anchor:v1:4:i",
                    "mfc4.i.r." + id),
            ]);

        Assert.Equal("IPv4", RouterOsFilterArtifactIdentity.FormatFamily(a.AddressLists[0].Family));
        Assert.Equal("2001:db8::1", a.AddressLists[1].Entries[0].Address);
        Assert.Equal(FilterBuiltInContext.Forward, a.Chains[0].BuiltInContext);
        Assert.Equal(FilterBuiltInContext.Forward, a.AnchorTargets[0].BuiltInChain);
        Assert.Equal(FilterBuiltInContext.Input, a.AnchorTargets[1].BuiltInChain);
    }

    private static RouterOsFilterArtifact CreateSampleArtifact(Hash256? semantics = null)
    {
        Hash256 physical = semantics ?? RouterOsFilterArtifactIdentity.HashPhysicalSemantics(BuildSemantics());
        string artifactId = RouterOsFilterArtifactIdentity.ComputeArtifactId(ProfileHash, physical, Device);
        return RouterOsFilterArtifact.Create(
            ProfileHash,
            physical,
            Device,
            addressLists:
            [
                new AddressListArtifactDraft
                {
                    Family = IpAddressFamily.IPv4,
                    Name = "mfc4.a.deadbeefdeadbeef",
                    Entries = [AddressListEntryArtifact.Create("10.10.0.0/16")],
                },
            ],
            chains:
            [
                new ChainArtifactDraft
                {
                    Family = IpAddressFamily.IPv4,
                    BuiltInContext = FilterBuiltInContext.Forward,
                    Name = "mfc4.f.r." + artifactId,
                    Role = FilterChainArtifactRole.Root,
                    Rules =
                    [
                        FilterRuleArtifact.Create(0, "jump", "mfc:stage:company-allow", matchers: new Dictionary<string, string>
                        {
                            ["jump-target"] = "mfc4.f.r." + artifactId,
                        }),
                    ],
                },
            ],
            anchorTargets:
            [
                AnchorTargetArtifact.Create(
                    IpAddressFamily.IPv4,
                    FilterBuiltInContext.Forward,
                    "mfc:anchor:v1:4:f",
                    "mfc4.f.r." + artifactId),
            ]);
    }

    private static PhysicalSemanticsMaterial BuildSemantics()
        => new()
        {
            LayoutVersion = "1",
            CompilerProfileHash = ProfileHash,
            RuleIds = [Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")],
            ResolvedPredicateDigests = ["pred:1"],
            ResolvedZoneDigests = ["zone:1"],
            ActionDigests = ["accept"],
            LoggingDigests = ["off"],
            ChainContractDigests = ["drop"],
        };
}
