using System.Net;
using System.Text.Json;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicyAnalysisEngineTests
{
    [Fact]
    public void Ac1ValidRuleHasNoBlockersAndInvokesSequence()
    {
        PolicyRule rule = AllowRule();
        int calls = 0;
        PolicyAnalysisResult result = Analyze(
            [rule],
            sequence: _ =>
            {
                calls++;
                return [];
            });
        Assert.False(result.HasBlockers);
        Assert.True(result.SequenceAnalyzerInvoked);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Ac2WrongZoneDirectionIsBlocker()
    {
        ZoneSelector zones = ZoneSelector.Create([ZoneId.New()]);
        PolicyAnalysisFinding? input = PolicyAnalysisEngine.TryZoneDirection(
            Guid.NewGuid(),
            PolicyFilterChain.Input,
            TrafficPredicate.Create(egressZones: zones));
        Assert.NotNull(input);
        Assert.Equal(PolicyAnalysisCodes.ZoneDirection, input!.Code);
        Assert.Equal(PolicyAnalysisCodes.SeverityBlocker, input.Severity);

        PolicyAnalysisFinding? output = PolicyAnalysisEngine.TryZoneDirection(
            Guid.NewGuid(),
            PolicyFilterChain.Output,
            TrafficPredicate.Create(ingressZones: zones));
        Assert.NotNull(output);
        Assert.Equal(PolicyAnalysisCodes.ZoneDirection, output!.Code);

        Assert.Null(PolicyAnalysisEngine.TryZoneDirection(
            Guid.NewGuid(),
            PolicyFilterChain.Forward,
            TrafficPredicate.Create(ingressZones: zones, egressZones: zones)));
    }

    [Fact]
    public void Ac3EmptySelectorIsUnsatisfiableBlocker()
    {
        AddressObject net = CompanyAddress("net", AddressEntry.Prefix(
            IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 24));
        PolicyRule rule = AllowRule(TrafficPredicate.Create(
            sourceAddresses: AddressSelector.Create([net.Id], [net.Id])));
        PolicyAnalysisResult result = Analyze(
            [rule],
            addresses: Catalog(net),
            sequence: _ => throw new InvalidOperationException("sequence must not run"));
        AssertFinding(result, PolicyAnalysisCodes.Unsatisfiable, rule.Id.Value);
        Assert.False(result.SequenceAnalyzerInvoked);
    }

    [Fact]
    public void Ac4TcpFlagsWithUdpServiceAreBlocked()
    {
        ServiceObject udp = CompanyService(
            "udp",
            ServiceTerm.Create(IpProtocol.Create(IpProtocol.Udp, "udp")));
        PolicyRule rule = AllowRule(TrafficPredicate.Create(
            services: ServiceSelector.Create([udp.Id]),
            tcpFlags: TcpFlagConstraint.Create([TcpHeaderBit.Syn])));
        PolicyAnalysisResult result = Analyze([rule], services: Catalog(udp));
        AssertFinding(result, PolicyAnalysisCodes.TcpFlagsProtocol, rule.Id.Value);
        Assert.False(result.SequenceAnalyzerInvoked);
    }

    [Fact]
    public void Ac4TcpFlagsWithAnyProtocolRemainSatisfiable()
    {
        PolicyRule rule = AllowRule(TrafficPredicate.Create(
            tcpFlags: TcpFlagConstraint.Create([TcpHeaderBit.Syn])));
        PolicyAnalysisResult result = Analyze([rule]);
        Assert.False(result.HasBlockers);
    }

    [Fact]
    public void Ac5IcmpFamilyMismatchIsBlocked()
    {
        ServiceObject icmp6 = CompanyService(
            "icmp6",
            ServiceTerm.Create(IpProtocol.Create(IpProtocol.IcmpV6, "ipv6-icmp")));
        PolicyRule rule = AllowRule(TrafficPredicate.Create(
            services: ServiceSelector.Create([icmp6.Id])));
        PolicyAnalysisResult result = Analyze([rule], services: Catalog(icmp6));
        AssertFinding(result, PolicyAnalysisCodes.IcmpFamily, rule.Id.Value);
    }

    [Fact]
    public void Ac6IpsecDirectionIsChecked()
    {
        PolicyRule inputOut = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Input,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(
                ipsecPolicy: IpsecPolicyPredicate.Create(IpsecDirection.Out, IpsecPolicyKind.Ipsec)),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyAnalysisResult input = Analyze([inputOut]);
        AssertFinding(input, PolicyAnalysisCodes.IpsecDirection, inputOut.Id.Value);

        PolicyRule outputIn = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Output,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(
                ipsecPolicy: IpsecPolicyPredicate.Create(IpsecDirection.In, IpsecPolicyKind.Ipsec)),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyAnalysisResult output = Analyze([outputIn]);
        AssertFinding(output, PolicyAnalysisCodes.IpsecDirection, outputIn.Id.Value);

        PolicyRule forward = AllowRule(TrafficPredicate.Create(
            ipsecPolicy: IpsecPolicyPredicate.Create(IpsecDirection.Out, IpsecPolicyKind.Ipsec)));
        Assert.False(Analyze([forward]).HasBlockers);
    }

    [Fact]
    public void Ac7ConnectionStateContradictionIsBlocked()
    {
        PolicyRule rule = AllowRule(TrafficPredicate.Create(
            connectionStates: [ConnectionState.Invalid, ConnectionState.Established]));
        PolicyAnalysisResult result = Analyze([rule]);
        AssertFinding(result, PolicyAnalysisCodes.ConnectionState, rule.Id.Value);
    }

    [Fact]
    public void Ac8UnsupportedMatcherBlocksRule()
    {
        PolicyRule rule = AllowRule();
        PolicyAnalysisResult result = Analyze(
            [rule],
            extra: new Dictionary<Guid, IReadOnlyList<string>>
            {
                [rule.Id.Value] = ["src-mac-address"],
            },
            sequence: _ => throw new InvalidOperationException("sequence must not run"));
        AssertFinding(result, PolicyAnalysisCodes.UnsupportedMatcher, rule.Id.Value);
        Assert.False(result.SequenceAnalyzerInvoked);
    }

    [Fact]
    public void Ac9DisabledRuleStillGetsStructuralValidation()
    {
        AddressObject all = CompanyAddress(
            "all",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("0.0.0.0"), 0));
        PolicyRule disabled = AllowRule(
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create(include: null, exclude: [all.Id])),
            enabled: false);
        PolicyAnalysisResult result = Analyze([disabled], addresses: Catalog(all));
        AssertFinding(result, PolicyAnalysisCodes.Unsatisfiable, disabled.Id.Value);
        Assert.False(result.SequenceAnalyzerInvoked);
    }

    [Fact]
    public void Ac10Ac11FindingsAreStructuredWithStableCodes()
    {
        PolicyRule rule = AllowRule(TrafficPredicate.Create(
            connectionStates: [ConnectionState.Untracked, ConnectionState.New]));
        PolicyAnalysisResult result = Analyze([rule]);
        PolicyAnalysisFinding finding = Assert.Single(result.Findings);
        Assert.Equal(PolicyAnalysisCodes.ConnectionState, finding.Code);
        Assert.Equal(PolicyAnalysisCodes.SeverityBlocker, finding.Severity);
        Assert.Equal(rule.Id.Value, finding.RuleId);
        Assert.False(string.IsNullOrWhiteSpace(finding.Message));
        Assert.StartsWith("RULE_", finding.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac12InvalidRuleIsNotPassedToSequenceAnalyzer()
    {
        bool invoked = false;
        PolicyRule rule = AllowRule(TrafficPredicate.Create(
            connectionStates: [ConnectionState.Invalid, ConnectionState.Related]));
        PolicyAnalysisResult result = Analyze(
            [rule],
            sequence: _ =>
            {
                invoked = true;
                return [];
            });
        Assert.True(result.HasBlockers);
        Assert.False(result.SequenceAnalyzerInvoked);
        Assert.False(invoked);
    }

    [Fact]
    public void Ipv6BroadcastAddressTypeIsUnsatisfiable()
    {
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv6,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(sourceAddressTypes: [AddressType.Broadcast]),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyAnalysisResult result = Analyze([rule]);
        AssertFinding(result, PolicyAnalysisCodes.Unsatisfiable, rule.Id.Value);
    }

    [Fact]
    public void EmptyZoneIncludeMinusExcludeIsUnsatisfiable()
    {
        ZoneId zone = ZoneId.New();
        PolicyRule rule = AllowRule(TrafficPredicate.Create(
            ingressZones: ZoneSelector.Create([zone], [zone])));
        PolicyAnalysisResult result = Analyze([rule], zones: new HashSet<Guid> { zone.Value });
        AssertFinding(result, PolicyAnalysisCodes.Unsatisfiable, rule.Id.Value);
    }

    [Fact]
    public void TcpResetWithoutTcpServiceIsBlockedOnReconstitute()
    {
        PolicyRule rule = PolicyRule.Reconstitute(
            RuleId.New(),
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            enabled: true,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.TcpReset),
            LogSpecification.Disabled,
            exceptionEligible: false,
            description: null);
        PolicyAnalysisResult result = Analyze([rule]);
        AssertFinding(result, PolicyAnalysisCodes.TcpFlagsProtocol, rule.Id.Value);
    }

    [Fact]
    public void DisabledDanglingSelectorFailsCompose()
    {
        PolicyRule disabled = AllowRule(
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(Guid.NewGuid())])),
            enabled: false);
        PolicyComposeResult result = Compose(CompanyLayer(CompanyDocument(rules: [disabled])));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.SelectorUnresolved, result.Code);
    }

    [Fact]
    public void DisabledUnsatisfiableRuleFailsCompose()
    {
        Guid id = Guid.NewGuid();
        PolicyRule disabled = AllowRule(
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create(include: null, exclude: [new AddressObjectId(id)])),
            enabled: false);
        PolicyLayer company = CompanyLayer(CompanyDocument(
            addressObjects: [UniverseAddressJson(id)],
            rules: [disabled]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyAnalysisCodes.Unsatisfiable, result.Code);
    }

    [Fact]
    public void TcpFlagsWithUdpServiceFailsCompose()
    {
        Guid serviceId = Guid.NewGuid();
        PolicyRule rule = AllowRule(TrafficPredicate.Create(
            services: ServiceSelector.Create([new ServiceObjectId(serviceId)]),
            tcpFlags: TcpFlagConstraint.Create([TcpHeaderBit.Syn])));
        PolicyLayer company = CompanyLayer(CompanyDocument(
            serviceObjects: [UdpServiceJson(serviceId)],
            rules: [rule]));
        PolicyComposeResult result = Compose(company);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyAnalysisCodes.TcpFlagsProtocol, result.Code);
    }

    [Fact]
    public void IpsecInputOutFailsCompose()
    {
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Input,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(
                ipsecPolicy: IpsecPolicyPredicate.Create(IpsecDirection.Out, IpsecPolicyKind.Ipsec)),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyComposeResult result = Compose(CompanyLayer(CompanyDocument(rules: [rule])));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyAnalysisCodes.IpsecDirection, result.Code);
    }

    private static PolicyAnalysisResult Analyze(
        IReadOnlyList<PolicyRule> rules,
        IReadOnlyDictionary<AddressObjectId, AddressObject>? addresses = null,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject>? services = null,
        IReadOnlySet<Guid>? zones = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? extra = null,
        PolicySequenceAnalyzer? sequence = null)
        => PolicyAnalysisEngine.Analyze(
            rules,
            addresses ?? new Dictionary<AddressObjectId, AddressObject>(),
            services ?? new Dictionary<ServiceObjectId, ServiceObject>(),
            zones ?? new HashSet<Guid>(),
            extra,
            sequence);

    private static void AssertFinding(PolicyAnalysisResult result, string code, Guid ruleId)
    {
        Assert.True(result.HasBlockers);
        PolicyAnalysisFinding finding = Assert.Single(result.Findings, f => f.Code == code);
        Assert.Equal(PolicyAnalysisCodes.SeverityBlocker, finding.Severity);
        Assert.Equal(ruleId, finding.RuleId);
    }

    private static PolicyRule AllowRule(TrafficPredicate? predicate = null, bool enabled = true)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            predicate ?? TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            enabled: enabled);

    private static AddressObject CompanyAddress(string name, params AddressEntry[] entries)
        => AddressObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create(name),
            IpAddressFamily.IPv4,
            entries);

    private static ServiceObject CompanyService(string name, params ServiceTerm[] terms)
        => ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create(name),
            terms);

    private static Dictionary<AddressObjectId, AddressObject> Catalog(AddressObject obj)
        => new() { [obj.Id] = obj };

    private static Dictionary<ServiceObjectId, ServiceObject> Catalog(ServiceObject obj)
        => new() { [obj.Id] = obj };

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
        IReadOnlyList<JsonElement>? addressObjects = null,
        IReadOnlyList<JsonElement>? serviceObjects = null,
        IReadOnlyList<PolicyRule>? rules = null)
        => new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            addressObjects: addressObjects,
            serviceObjects: serviceObjects,
            rules: rules);

    private static JsonElement UniverseAddressJson(Guid id)
        => JsonDocument.Parse(
            "{\"id\":\"" + id + "\",\"name\":\"all\",\"family\":\"IPv4\",\"entries\":[" +
            "{\"kind\":\"PREFIX\",\"address\":\"0.0.0.0\",\"prefix_length\":0}]}").RootElement.Clone();

    private static JsonElement UdpServiceJson(Guid id)
        => JsonDocument.Parse(
            "{\"id\":\"" + id + "\",\"name\":\"dns\",\"terms\":[{" +
            "\"protocol\":{\"number\":17,\"canonical_name\":\"udp\"}," +
            "\"destination_ports\":[{\"start\":53,\"end\":53}]}]}").RootElement.Clone();
}
