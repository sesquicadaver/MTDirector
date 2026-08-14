using System.Reflection;
using System.Text;
using System.Text.Json;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class EffectivePolicyComposerTests
{
    [Fact]
    public void D1MissingCompanyReturnsCompanyRequired()
    {
        PolicyComposeResult result = EffectivePolicyComposer.Compose(
            company: null,
            site: null,
            node: null,
            nodeId: Guid.NewGuid(),
            siteId: Guid.NewGuid(),
            knownZoneIds: new HashSet<Guid>());
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.CompanyRequired, result.Code);
    }

    [Fact]
    public void D2OverlaysAreOptional()
    {
        PolicyLayer company = CompanyLayer(CompanyDocument());
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.ActiveRules);
    }

    [Fact]
    public void D3DanglingSelectorIsUnresolved()
    {
        Guid missing = Guid.NewGuid();
        PolicyRule rule = CompanyAllowRule(
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([new AddressObjectId(missing)])));
        PolicyLayer company = CompanyLayer(CompanyDocument(rules: [rule]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.SelectorUnresolved, result.Code);
    }

    [Fact]
    public void D4ParentContextMismatch()
    {
        PolicyLayer company = CompanyLayer(CompanyDocument());
        PolicyDocument siteDoc = new(PolicyKind.SiteOverlay, PolicyOwnerScope.Site);
        PolicyLayer site = new()
        {
            Kind = PolicyKind.SiteOverlay,
            OwnerScope = PolicyOwnerScope.Site,
            OwnerId = Guid.NewGuid(),
            ContentHash = PolicyHashing.HashContent(siteDoc),
            ParentContextHash = Hash256.Create(new byte[32]),
            PolicyDocument = siteDoc,
        };
        PolicyComposeResult result = Compose(company, site);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.ParentContextMismatch, result.Code);
    }

    [Fact]
    public void D5VisibilityCompanyCannotReferenceSiteObject()
    {
        Guid siteId = Guid.NewGuid();
        Guid objectId = Guid.NewGuid();
        PolicyLayer company = CompanyLayer(CompanyDocument(rules:
        [
            CompanyAllowRule(TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(objectId)]))),
        ]));
        PolicyDocument siteDoc = new(
            PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site,
            addressObjects: [ObjectJson(objectId)]);
        PolicyLayer site = OverlayLayer(PolicyKind.SiteOverlay, PolicyOwnerScope.Site, siteId, siteDoc, company.ContentHash);
        PolicyComposeResult result = Compose(company, site, siteId: siteId);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.Visibility, result.Code);
    }

    [Fact]
    public void D6StageOwnershipRejectsSiteRuleInCompanyAllow()
    {
        Guid siteId = Guid.NewGuid();
        PolicyLayer company = CompanyLayer(CompanyDocument());
        PolicyRule siteRule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyDocument siteDoc = new(PolicyKind.SiteOverlay, PolicyOwnerScope.Site, rules: [siteRule]);
        PolicyLayer site = OverlayLayer(PolicyKind.SiteOverlay, PolicyOwnerScope.Site, siteId, siteDoc, company.ContentHash);
        PolicyComposeResult result = Compose(company, site, siteId: siteId);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.StageOwnership, result.Code);
    }

    [Fact]
    public void D7DisabledDroppedOriginalOrdinalsKeptAndDisabledBytesNotInHash()
    {
        PolicyRule enabled0 = CompanyAllowRule(id: RuleId.New(), ordinal: 0, enabled: true);
        PolicyRule disabled1 = CompanyAllowRule(id: RuleId.New(), ordinal: 1, enabled: false);
        PolicyRule enabled2 = CompanyAllowRule(id: RuleId.New(), ordinal: 2, enabled: true);
        PolicyLayer company = CompanyLayer(CompanyDocument(rules: [enabled0, disabled1, enabled2]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.ActiveRules.Count);
        Assert.Equal(new uint[] { 0, 2 }, result.Value.ActiveRules.Select(r => r.Ordinal).ToArray());
        Assert.DoesNotContain(result.Value.ActiveRules, r => r.Id == disabled1.Id);

        byte[] disabledBytes = PolicyCanonicalWriter.WriteRuleBytes(disabled1);
        byte[] preimage = PolicyHashing.BuildLogicalEffectivePreimage(
            company.PolicyDocument.SchemaVersion,
            company.ContentHash,
            siteContentHash: null,
            nodeContentHash: null,
            exceptionCount: 0,
            canonicalMergedObjects: [],
            canonicalActiveRules: result.Value.ActiveRules.Select(PolicyCanonicalWriter.WriteRuleBytes).ToArray(),
            chainContractBytes: PolicyCanonicalWriter.WriteChainContractSetBytes(company.PolicyDocument.ChainContracts));
        Assert.Equal(-1, IndexOf(preimage, disabledBytes));
    }

    [Fact]
    public void D8DuplicatePredicatesAreKept()
    {
        TrafficPredicate predicate = TrafficPredicate.Create();
        PolicyRule first = CompanyAllowRule(predicate, ordinal: 0);
        PolicyRule second = CompanyAllowRule(predicate, ordinal: 1);
        PolicyLayer company = CompanyLayer(CompanyDocument(rules: [first, second]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.ActiveRules.Count);
    }

    [Fact]
    public void D9PipelineV1OrderAndExemptionStagesEmpty()
    {
        PolicyRule ipv6 = PolicyRule.Create(
            IpAddressFamily.IPv6,
            PolicyFilterChain.Input,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyRule ipv4Output = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Output,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyRule ipv4Deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop));
        PolicyLayer company = CompanyLayer(CompanyDocument(rules: [ipv6, ipv4Output, ipv4Deny]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsSuccess);
        PolicyRule[] active = result.Value!.ActiveRules.ToArray();
        Assert.Equal(new[] { ipv4Deny.Id, ipv4Output.Id, ipv6.Id }, active.Select(r => r.Id).ToArray());
        Assert.DoesNotContain(
            active,
            r => r.Stage is PolicyPipelineStage.CompanyDenyExemptions
                or PolicyPipelineStage.SiteDenyExemptions
                or PolicyPipelineStage.NodeDenyExemptions);
        Assert.DoesNotContain(active, r => r.Effect.Kind == PolicyRuleEffect.ExemptDenyStage);
    }

    [Fact]
    public void D10ComposerSignatureHasNoVrrpWanDeviceOrBindings()
    {
        MethodInfo compose = typeof(EffectivePolicyComposer).GetMethod(nameof(EffectivePolicyComposer.Compose))!;
        string[] names = compose.GetParameters().Select(p => p.Name!).ToArray();
        Assert.All(names, name =>
        {
            Assert.DoesNotContain("vrrp", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("wan", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("device", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("binding", name, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains("company", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("knownZoneIds", names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void D11IdenticalInputsYieldIdenticalLogicalEffectiveHash()
    {
        PolicyLayer company = CompanyLayer(CompanyDocument(rules: [CompanyAllowRule()]));
        PolicyComposeResult first = Compose(company);
        PolicyComposeResult second = Compose(company);
        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value!.LogicalEffectiveHash.ToString(), second.Value!.LogicalEffectiveHash.ToString());
    }

    [Fact]
    public void D12ShuffledListsYieldSameLogicalEffectiveHash()
    {
        Guid a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        PolicyRule r1 = CompanyAllowRule(id: new RuleId(Guid.Parse("11111111-1111-1111-1111-111111111111")), ordinal: 0);
        PolicyRule r2 = CompanyAllowRule(id: new RuleId(Guid.Parse("22222222-2222-2222-2222-222222222222")), ordinal: 1);
        PolicyDocument ordered = CompanyDocument(addressObjects: [ObjectJson(a), ObjectJson(b)], rules: [r1, r2]);
        PolicyLayer canonical = CompanyLayer(ordered);
        PolicyDocument shuffled = new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            addressObjects: [ObjectJson(b), ObjectJson(a)],
            rules: [r2, r1]);
        PolicyLayer shuffledLayer = new()
        {
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
            ContentHash = canonical.ContentHash,
            PolicyDocument = shuffled,
        };
        PolicyComposeResult left = Compose(canonical);
        PolicyComposeResult right = Compose(shuffledLayer);
        Assert.True(left.IsSuccess);
        Assert.Equal(left.Value!.LogicalEffectiveHash.ToString(), right.Value!.LogicalEffectiveHash.ToString());
    }

    [Fact]
    public void D13UnusedObjectIsInfoAndComposeSucceeds()
    {
        Guid unused = Guid.NewGuid();
        PolicyLayer company = CompanyLayer(CompanyDocument(
            addressObjects: [ObjectJson(unused)],
            rules: [CompanyAllowRule()]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value!.Findings, f => f.Code == PolicyComposeCodes.UnusedPolicyObject);
        Assert.Equal(unused.ToString("D"), result.Value.Findings[0].Subject);
    }

    [Fact]
    public void D14ObjectUuidCollision()
    {
        Guid shared = Guid.NewGuid();
        Guid siteId = Guid.NewGuid();
        PolicyLayer company = CompanyLayer(CompanyDocument(addressObjects: [ObjectJson(shared)]));
        PolicyDocument siteDoc = new(
            PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site,
            addressObjects: [ObjectJson(shared)]);
        PolicyLayer site = OverlayLayer(PolicyKind.SiteOverlay, PolicyOwnerScope.Site, siteId, siteDoc, company.ContentHash);
        PolicyComposeResult result = Compose(company, site, siteId: siteId);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.UuidCollision, result.Code);
    }

    [Fact]
    public void D15RuleUuidCollisionAcrossLayers()
    {
        Guid siteId = Guid.NewGuid();
        RuleId shared = new(Guid.NewGuid());
        PolicyLayer company = CompanyLayer(CompanyDocument(rules: [CompanyAllowRule(id: shared)]));
        PolicyRule siteRule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.SiteAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            id: shared);
        PolicyDocument siteDoc = new(PolicyKind.SiteOverlay, PolicyOwnerScope.Site, rules: [siteRule]);
        PolicyLayer site = OverlayLayer(PolicyKind.SiteOverlay, PolicyOwnerScope.Site, siteId, siteDoc, company.ContentHash);
        PolicyComposeResult result = Compose(company, site, siteId: siteId);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.UuidCollision, result.Code);
    }

    [Fact]
    public void D16MissingZoneIsComposeZoneNotFound()
    {
        Guid missingZone = Guid.NewGuid();
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(ingressZones: ZoneSelector.Create([new ZoneId(missingZone)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyLayer company = CompanyLayer(CompanyDocument(rules: [rule]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.ZoneNotFound, result.Code);
        Assert.NotEqual("not_found", result.Code);
    }

    [Fact]
    public void D17LogicalEffectiveHashDiffersFromSyntheticDocumentHashContent()
    {
        Guid objectId = Guid.NewGuid();
        PolicyRule rule = CompanyAllowRule();
        PolicyLayer company = CompanyLayer(CompanyDocument(addressObjects: [ObjectJson(objectId)], rules: [rule]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsSuccess);
        PolicyDocument synthetic = new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            chainContracts: company.PolicyDocument.ChainContracts,
            addressObjects: result.Value!.MergedAddressObjects,
            serviceObjects: result.Value.MergedServiceObjects,
            rules: result.Value.ActiveRules);
        Hash256 documentHash = PolicyHashing.HashContent(synthetic);
        Assert.NotEqual(result.Value.LogicalEffectiveHash.ToString(), documentHash.ToString());
    }

    [Fact]
    public void D18CompanyOnlyHashDiffersFromZeroPaddedSiteDigest()
    {
        PolicyLayer company = CompanyLayer(CompanyDocument());
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsSuccess);
        byte[] contracts = PolicyCanonicalWriter.WriteChainContractSetBytes(company.PolicyDocument.ChainContracts);
        Hash256 padded = PolicyHashing.HashLogicalEffective(
            company.PolicyDocument.SchemaVersion,
            company.ContentHash,
            siteContentHash: null,
            nodeContentHash: null,
            exceptionCount: 0,
            canonicalMergedObjects: [],
            canonicalActiveRules: [],
            chainContractBytes: contracts,
            padAbsentSiteWithZeros: true);
        Assert.NotEqual(result.Value!.LogicalEffectiveHash.ToString(), padded.ToString());
    }

    [Fact]
    public void D19LogicalEffectivePreimageHasPrefixNulAndExceptionCountSlot()
    {
        PolicyLayer company = CompanyLayer(CompanyDocument());
        byte[] contracts = PolicyCanonicalWriter.WriteChainContractSetBytes(company.PolicyDocument.ChainContracts);
        byte[] withSlot = PolicyHashing.BuildLogicalEffectivePreimage(
            company.PolicyDocument.SchemaVersion,
            company.ContentHash,
            siteContentHash: null,
            nodeContentHash: null,
            exceptionCount: 0,
            canonicalMergedObjects: [],
            canonicalActiveRules: [],
            chainContractBytes: contracts);
        byte[] withoutSlot = PolicyHashing.BuildLogicalEffectivePreimage(
            company.PolicyDocument.SchemaVersion,
            company.ContentHash,
            siteContentHash: null,
            nodeContentHash: null,
            exceptionCount: 0,
            canonicalMergedObjects: [],
            canonicalActiveRules: [],
            chainContractBytes: contracts,
            includeExceptionCountSlot: false);
        byte[] prefix = Encoding.UTF8.GetBytes(PolicyHashing.LogicalEffectivePrefix);
        Assert.True(withSlot.AsSpan().StartsWith(prefix));
        Assert.Equal(0, withSlot[prefix.Length]);
        Assert.NotEqual(Convert.ToHexString(withSlot), Convert.ToHexString(withoutSlot));
        Assert.Equal(withSlot.Length, withoutSlot.Length + 4);
        Hash256 countZero = PolicyHashing.HashLogicalEffective(
            company.PolicyDocument.SchemaVersion,
            company.ContentHash,
            null,
            null,
            exceptionCount: 0,
            [],
            [],
            contracts);
        Hash256 countOne = PolicyHashing.HashLogicalEffective(
            company.PolicyDocument.SchemaVersion,
            company.ContentHash,
            null,
            null,
            exceptionCount: 1,
            [],
            [],
            contracts);
        Assert.NotEqual(countZero.ToString(), countOne.ToString());
    }

    [Fact]
    public void MalformedObjectIdFailsCompose()
    {
        JsonElement malformed = JsonDocument.Parse("{\"name\":\"no-id\"}").RootElement.Clone();
        PolicyLayer company = CompanyLayer(CompanyDocument(addressObjects: [malformed]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.ObjectIdMalformed, result.Code);
    }

    [Fact]
    public void ObjectOwnerScopeMismatchIsVisibility()
    {
        Guid objectId = Guid.NewGuid();
        JsonElement mismatched = JsonDocument.Parse(
            "{\"id\":\"" + objectId + "\",\"owner_scope\":\"SITE\",\"owner_id\":\"" + Guid.NewGuid() + "\"}")
            .RootElement.Clone();
        PolicyLayer company = CompanyLayer(CompanyDocument(addressObjects: [mismatched]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.Visibility, result.Code);
    }

    [Fact]
    public void DanglingServiceSelectorIsUnresolved()
    {
        PolicyRule rule = CompanyAllowRule(TrafficPredicate.Create(
            services: ServiceSelector.Create([new ServiceObjectId(Guid.NewGuid())])));
        PolicyLayer company = CompanyLayer(CompanyDocument(rules: [rule]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.SelectorUnresolved, result.Code);
    }

    [Fact]
    public void KnownZoneSelectorSucceeds()
    {
        Guid zoneId = Guid.NewGuid();
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(ingressZones: ZoneSelector.Create([new ZoneId(zoneId)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyLayer company = CompanyLayer(CompanyDocument(rules: [rule]));
        PolicyComposeResult result = Compose(company, knownZoneIds: new HashSet<Guid> { zoneId });
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.ActiveRules);
    }

    [Fact]
    public void SiteAndNodeOverlaysComposeWhenParentHashesMatch()
    {
        Guid siteId = Guid.NewGuid();
        Guid nodeId = Guid.NewGuid();
        PolicyLayer company = CompanyLayer(CompanyDocument(rules: [CompanyAllowRule()]));
        PolicyRule siteRule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.SiteAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyLayer site = OverlayLayer(
            PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site,
            siteId,
            new PolicyDocument(PolicyKind.SiteOverlay, PolicyOwnerScope.Site, rules: [siteRule]),
            company.ContentHash);
        Hash256 nodeParent = PolicyHashing.ComputeParentContextHash(
            PolicyKind.NodeOverlay, company.ContentHash, site.ContentHash, null, null)!;
        PolicyRule nodeRule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.NodeAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyLayer node = OverlayLayer(
            PolicyKind.NodeOverlay,
            PolicyOwnerScope.Node,
            nodeId,
            new PolicyDocument(PolicyKind.NodeOverlay, PolicyOwnerScope.Node, rules: [nodeRule]),
            nodeParent);
        PolicyComposeResult result = Compose(company, site, node, nodeId, siteId);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.ActiveRules.Count);
    }

    [Fact]
    public void WriteRuleBytesMatchesEmbeddedRuleObject()
    {
        PolicyRule rule = CompanyAllowRule(
            id: new RuleId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
        byte[] isolated = PolicyCanonicalWriter.WriteRuleBytes(rule);
        string json = Encoding.UTF8.GetString(PolicyCanonicalWriter.Write(CompanyDocument(rules: [rule])));
        Assert.Contains(Encoding.UTF8.GetString(isolated), json, StringComparison.Ordinal);
        Assert.StartsWith("{", Encoding.UTF8.GetString(isolated), StringComparison.Ordinal);
    }

    private static PolicyComposeResult Compose(
        PolicyLayer company,
        PolicyLayer? site = null,
        PolicyLayer? node = null,
        Guid? nodeId = null,
        Guid? siteId = null,
        IReadOnlySet<Guid>? knownZoneIds = null)
        => EffectivePolicyComposer.Compose(
            company,
            site,
            node,
            nodeId ?? Guid.NewGuid(),
            siteId,
            knownZoneIds ?? new HashSet<Guid>());

    private static PolicyLayer CompanyLayer(PolicyDocument document)
        => new()
        {
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
            ContentHash = PolicyHashing.HashContent(document),
            PolicyDocument = document,
        };

    private static PolicyLayer OverlayLayer(
        PolicyKind kind,
        PolicyOwnerScope scope,
        Guid ownerId,
        PolicyDocument document,
        Hash256 parentContextHash)
        => new()
        {
            Kind = kind,
            OwnerScope = scope,
            OwnerId = ownerId,
            ContentHash = PolicyHashing.HashContent(document),
            ParentContextHash = parentContextHash,
            PolicyDocument = document,
        };

    private static PolicyDocument CompanyDocument(
        IReadOnlyList<JsonElement>? addressObjects = null,
        IReadOnlyList<JsonElement>? serviceObjects = null,
        IReadOnlyList<PolicyRule>? rules = null)
        => new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            addressObjects: addressObjects,
            serviceObjects: serviceObjects,
            rules: rules);

    private static PolicyRule CompanyAllowRule(
        TrafficPredicate? predicate = null,
        RuleId? id = null,
        uint ordinal = 0,
        bool enabled = true)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal,
            predicate ?? TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            enabled: enabled,
            id: id);

    private static JsonElement ObjectJson(Guid id)
        => JsonDocument.Parse("{\"id\":\"" + id + "\"}").RootElement.Clone();

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }
}
