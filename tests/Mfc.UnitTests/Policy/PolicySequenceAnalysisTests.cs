using System.Net;
using System.Text.Json;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicySequenceAnalysisTests
{
    [Fact]
    public void Ac1ExactDuplicatesAreWarningsAndKeepBothRules()
    {
        PolicyRule first = Allow();
        PolicyRule second = Allow(ordinal: 1);
        IReadOnlyList<PolicyAnalysisFinding> findings = Analyze([first, second]);
        PolicyAnalysisFinding finding = Assert.Single(findings);
        Assert.Equal(PolicyAnalysisCodes.ExactDuplicate, finding.Code);
        Assert.Equal(PolicyAnalysisCodes.SeverityWarning, finding.Severity);
        Assert.Equal(second.Id.Value, finding.RuleId);
        Assert.Equal(first.Id.Value, finding.RelatedRuleId);
        Assert.NotNull(finding.Witness);

        PolicyComposeResult composed = Compose(CompanyLayer(CompanyDocument(rules: [first, second])));
        Assert.True(composed.IsSuccess);
        Assert.Equal(2, composed.Value!.ActiveRules.Count);
        Assert.Contains(composed.Value.Findings, f => f.Code == PolicyAnalysisCodes.ExactDuplicate);
    }

    [Fact]
    public void Ac2SamePredicateDifferentEffectIsBlocker()
    {
        PolicyRule drop = Deny();
        PolicyRule reject = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            1,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.AdminProhibited));
        IReadOnlyList<PolicyAnalysisFinding> findings = Analyze([drop, reject]);
        PolicyAnalysisFinding finding = Assert.Single(findings);
        Assert.Equal(PolicyAnalysisCodes.ConflictingDuplicate, finding.Code);
        Assert.Equal(PolicyAnalysisCodes.SeverityBlocker, finding.Severity);
        Assert.NotNull(finding.Witness);

        PolicyComposeResult composed = Compose(CompanyLayer(CompanyDocument(rules: [drop, reject])));
        Assert.True(composed.IsFailure);
        Assert.Equal(PolicyAnalysisCodes.ConflictingDuplicate, composed.Code);
    }

    [Fact]
    public void Ac3FullyShadowedEnabledRuleIsBlocker()
    {
        AddressObject host = CompanyAddress(
            "host",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        PolicyRule earlier = Deny();
        PolicyRule later = Deny(
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([host.Id])),
            ordinal: 1);
        IReadOnlyList<PolicyAnalysisFinding> findings = Analyze(
            [earlier, later],
            addresses: Catalog(host));
        Assert.Contains(findings, f =>
            f.Code == PolicyAnalysisCodes.FullyShadowed
            && f.Severity == PolicyAnalysisCodes.SeverityBlocker
            && f.RuleId == later.Id.Value
            && f.Witness is not null);

        PolicyComposeResult composed = Compose(CompanyLayer(CompanyDocument(
            rules: [earlier, later],
            addressObjects: [HostJson(host, "10.0.0.1")])));
        Assert.True(composed.IsFailure);
        Assert.Equal(PolicyAnalysisCodes.FullyShadowed, composed.Code);
    }

    [Fact]
    public void Ac4PartialShadowingIsWarning()
    {
        AddressObject host = CompanyAddress(
            "host",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        AddressObject net = CompanyAddress(
            "net",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 24));
        PolicyRule earlier = Deny(
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([host.Id])));
        PolicyRule later = Deny(
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([net.Id])),
            ordinal: 1);
        IReadOnlyList<PolicyAnalysisFinding> findings = Analyze(
            [earlier, later],
            addresses: Catalog(host, net));
        Assert.Contains(findings, f =>
            f.Code == PolicyAnalysisCodes.PartiallyShadowed
            && f.Severity == PolicyAnalysisCodes.SeverityWarning
            && f.RuleId == later.Id.Value
            && f.Witness is not null);
    }

    [Fact]
    public void Ac5AllowBeforeDenyOverlapIsDetected()
    {
        AddressObject sources = CompanyAddress(
            "src",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 16));
        AddressObject destinations = CompanyAddress(
            "dst",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("192.168.0.0"), 24));
        PolicyRule allow = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([sources.Id])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyRule drop = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            1,
            TrafficPredicate.Create(destinationAddresses: AddressSelector.Create([destinations.Id])),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop));
        IReadOnlyList<PolicyAnalysisFinding> findings = Analyze(
            [allow, drop],
            addresses: Catalog(sources, destinations));
        PolicyAnalysisFinding bypass = Assert.Single(
            findings,
            f => f.Code == PolicyAnalysisCodes.EarlierAllowBypassesDeny);
        Assert.Equal(PolicyAnalysisCodes.SeverityBlocker, bypass.Severity);
        Assert.Equal(drop.Id.Value, bypass.RuleId);
        Assert.NotNull(bypass.Witness);

        PolicyComposeResult composed = Compose(CompanyLayer(CompanyDocument(
            rules: [allow, drop],
            addressObjects:
            [
                PrefixJson(sources, "10.0.0.0", 16),
                PrefixJson(destinations, "192.168.0.0", 24),
            ])));
        Assert.True(composed.IsFailure);
        Assert.Equal(PolicyAnalysisCodes.EarlierAllowBypassesDeny, composed.Code);
    }

    [Fact]
    public void Ac6DenyBeforeAllowOverlapIsDetected()
    {
        AddressObject narrow = CompanyAddress(
            "n",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 24));
        AddressObject wide = CompanyAddress(
            "w",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 16));
        PolicyRule drop = Deny(
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([narrow.Id])));
        PolicyRule allow = Allow(
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([wide.Id])));
        IReadOnlyList<PolicyAnalysisFinding> findings = Analyze(
            [drop, allow],
            addresses: Catalog(narrow, wide));
        Assert.Contains(findings, f =>
            f.Code == PolicyAnalysisCodes.OrderDependentOverlap
            && f.RuleId == allow.Id.Value
            && f.Witness is not null);
    }

    [Fact]
    public void Ac7FasttrackOverlapIsDistinct()
    {
        AddressObject wide = CompanyAddress(
            "w",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 16));
        AddressObject narrow = CompanyAddress(
            "n",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 24));
        PolicyRule fast = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([wide.Id])),
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept));
        PolicyRule drop = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            1,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([narrow.Id])),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop));
        IReadOnlyList<PolicyAnalysisFinding> findings = Analyze(
            [fast, drop],
            addresses: Catalog(wide, narrow));
        PolicyAnalysisFinding overlap = Assert.Single(
            findings,
            f => f.Code == PolicyAnalysisCodes.FasttrackOverlap);
        Assert.Equal(PolicyAnalysisCodes.SeverityBlocker, overlap.Severity);
        Assert.DoesNotContain(findings, f => f.Code == PolicyAnalysisCodes.EarlierAllowBypassesDeny);

        PolicyComposeResult composed = Compose(CompanyLayer(CompanyDocument(
            rules: [fast, drop],
            addressObjects:
            [
                PrefixJson(wide, "10.0.0.0", 16),
                PrefixJson(narrow, "10.0.0.0", 24),
            ])));
        Assert.True(composed.IsFailure);
        Assert.Equal(PolicyAnalysisCodes.FasttrackOverlap, composed.Code);
    }

    [Fact]
    public void Ac8Ac9SplitCoverEmptyResidualIsIndeterminateBlocker()
    {
        AddressObject low = CompanyAddress(
            "low",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 25));
        AddressObject high = CompanyAddress(
            "high",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.128"), 25));
        AddressObject all = CompanyAddress(
            "all",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 24));
        PolicyRule first = Deny(
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([low.Id])));
        PolicyRule second = Deny(
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([high.Id])),
            ordinal: 1);
        PolicyRule later = Deny(
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([all.Id])),
            ordinal: 2);
        IReadOnlyList<PolicyAnalysisFinding> findings = Analyze(
            [first, second, later],
            addresses: Catalog(low, high, all));
        PolicyAnalysisFinding indeterminate = Assert.Single(
            findings,
            f => f.Code == PolicyAnalysisCodes.ShadowIndeterminate);
        Assert.Equal(PolicyAnalysisCodes.SeverityBlocker, indeterminate.Severity);
        Assert.Equal(later.Id.Value, indeterminate.RuleId);
        Assert.NotNull(indeterminate.Witness);

        PolicyComposeResult composed = Compose(CompanyLayer(CompanyDocument(
            rules: [first, second, later],
            addressObjects:
            [
                PrefixJson(low, "10.0.0.0", 25),
                PrefixJson(high, "10.0.0.128", 25),
                PrefixJson(all, "10.0.0.0", 24),
            ])));
        Assert.True(composed.IsFailure);
        Assert.Equal(PolicyAnalysisCodes.ShadowIndeterminate, composed.Code);
    }

    [Fact]
    public void Ac10ProvenFindingsHaveWitnessPackets()
    {
        PolicyRule first = Allow();
        PolicyRule second = Allow(ordinal: 1);
        PolicyAnalysisFinding finding = Assert.Single(Analyze([first, second]));
        Assert.NotNull(finding.Witness);
        Assert.Equal(IpAddressFamily.IPv4, finding.Witness!.Family);
        Assert.Equal(PolicyFilterChain.Forward, finding.Witness.Chain);
        Assert.False(string.IsNullOrWhiteSpace(finding.Witness.SourceAddress));
        Assert.DoesNotContain("password", finding.Witness.SourceAddress, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac11DuplicateIsNotRemovedFromCompose()
    {
        PolicyRule first = Allow(id: RuleId.New());
        PolicyRule second = Allow(id: RuleId.New(), ordinal: 1);
        PolicyComposeResult result = Compose(CompanyLayer(CompanyDocument(rules: [first, second])));
        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { first.Id, second.Id }.OrderBy(static id => id.Value),
            result.Value!.ActiveRules.Select(static r => r.Id).OrderBy(static id => id.Value));
    }

    [Fact]
    public void Ac12FindingsAreIndependentOfRepeatedInvocation()
    {
        PolicyRule first = Allow();
        PolicyRule second = Allow(ordinal: 1);
        string[] left = Codes(Analyze([first, second]));
        string[] right = Codes(Analyze([first, second]));
        Assert.Equal(left, right);
    }

    [Fact]
    public void DifferentFamiliesDoNotShadowEachOther()
    {
        PolicyRule ipv4 = Allow();
        PolicyRule ipv6 = PolicyRule.Create(
            IpAddressFamily.IPv6,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        Assert.Empty(Analyze([ipv4, ipv6]));
    }

    [Fact]
    public void DisabledRulesAreIgnoredBySequenceAnalysis()
    {
        PolicyRule enabled = Allow();
        PolicyRule disabled = Allow(enabled: false);
        Assert.Empty(Analyze([enabled, disabled]));
    }

    [Fact]
    public void ExemptDenyStageIsSkippedForDuplicateShadowAndOverlap()
    {
        PolicyRule deny = Deny();
        PolicyRule exempt = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDenyExemptions,
            0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage));
        Assert.Empty(Analyze([deny, exempt]));
        Assert.Empty(Analyze([exempt, deny]));
    }

    [Fact]
    public void SameEffectDifferentLoggingIsFullyShadowedNotConflictingDuplicate()
    {
        PolicyRule first = Allow();
        PolicyRule second = Allow(ordinal: 1, logging: LogSpecification.Create(true, "mfc"));
        IReadOnlyList<PolicyAnalysisFinding> findings = Analyze([first, second]);
        Assert.DoesNotContain(findings, f => f.Code == PolicyAnalysisCodes.ConflictingDuplicate);
        Assert.Contains(findings, f =>
            f.Code == PolicyAnalysisCodes.FullyShadowed
            && f.Severity == PolicyAnalysisCodes.SeverityBlocker
            && f.RuleId == second.Id.Value);

        PolicyComposeResult composed = Compose(CompanyLayer(CompanyDocument(rules: [first, second])));
        Assert.True(composed.IsFailure);
        Assert.Equal(PolicyAnalysisCodes.FullyShadowed, composed.Code);
    }

    [Fact]
    public void EmptyCubeCannotProduceWitness()
    {
        AtomicTrafficCube empty = AtomicTrafficCube.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            [],
            []);
        Assert.Throws<DomainInvariantException>(() => PolicyWitnessPacket.FromCube(empty));
        Assert.Null(PolicyWitnessPacket.TryFrom(NormalizedPredicate.Empty));
    }

    private static IReadOnlyList<PolicyAnalysisFinding> Analyze(
        IReadOnlyList<PolicyRule> rules,
        IReadOnlyDictionary<AddressObjectId, AddressObject>? addresses = null)
        => PolicySequenceAnalysis.Analyze(
            rules,
            addresses ?? new Dictionary<AddressObjectId, AddressObject>(),
            new Dictionary<ServiceObjectId, ServiceObject>());

    private static string[] Codes(IReadOnlyList<PolicyAnalysisFinding> findings)
        => findings.Select(static f => $"{f.Code}:{f.RuleId:D}:{f.RelatedRuleId:D}").ToArray();

    private static PolicyRule Allow(
        TrafficPredicate? predicate = null,
        uint ordinal = 0,
        bool enabled = true,
        RuleId? id = null,
        LogSpecification? logging = null)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal,
            predicate ?? TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            logging: logging,
            enabled: enabled,
            id: id);

    private static PolicyRule Deny(TrafficPredicate? predicate = null, uint ordinal = 0)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            ordinal,
            predicate ?? TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop));

    private static AddressObject CompanyAddress(string name, params AddressEntry[] entries)
        => AddressObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create(name),
            IpAddressFamily.IPv4,
            entries);

    private static Dictionary<AddressObjectId, AddressObject> Catalog(params AddressObject[] objects)
        => objects.ToDictionary(static o => o.Id);

    private static PolicyComposeResult Compose(PolicyLayer company)
        => EffectivePolicyComposer.Compose(
            company, null, null, Guid.NewGuid(), null, new HashSet<Guid>());

    private static PolicyLayer CompanyLayer(PolicyDocument document)
        => new()
        {
            PolicyId = Guid.NewGuid(),
            RevisionId = Guid.NewGuid(),
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
            ContentHash = PolicyHashing.HashContent(document),
            PolicyDocument = document,
        };

    private static PolicyDocument CompanyDocument(
        IReadOnlyList<PolicyRule>? rules = null,
        IReadOnlyList<JsonElement>? addressObjects = null)
        => new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            rules: rules,
            addressObjects: addressObjects);

    private static JsonElement PrefixJson(AddressObject obj, string address, int prefixLength)
        => JsonDocument.Parse(
            "{\"id\":\"" + obj.Id.Value.ToString("D") +
            "\",\"name\":\"" + obj.Name.Value +
            "\",\"family\":\"IPv4\",\"entries\":[{\"kind\":\"PREFIX\",\"address\":\"" +
            address + "\",\"prefix_length\":" + prefixLength + "}]}").RootElement.Clone();

    private static JsonElement HostJson(AddressObject obj, string address)
        => JsonDocument.Parse(
            "{\"id\":\"" + obj.Id.Value.ToString("D") +
            "\",\"name\":\"" + obj.Name.Value +
            "\",\"family\":\"IPv4\",\"entries\":[{\"kind\":\"HOST\",\"address\":\"" +
            address + "\"}]}").RootElement.Clone();
}
