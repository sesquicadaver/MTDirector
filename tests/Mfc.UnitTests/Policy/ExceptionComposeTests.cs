using System.Globalization;
using System.Text.Json;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class ExceptionComposeTests
{
    private static readonly Guid AddrTarget = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AddrSubset = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AddrOther = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Until = new(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void D1MissingTargetIsNotFound()
    {
        PolicyLayer company = CompanyWithDeny();
        PolicyLayer exception = ExceptionLayer(
            company,
            siteId: Guid.NewGuid(),
            waived: RuleId.New(),
            ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.TargetNotFound, result.Code);
    }

    [Fact]
    public void D2DisabledTargetIsNotEligible()
    {
        PolicyRule deny = DenyRule(AddrTarget, enabled: false);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.TargetNotEligible, result.Code);
    }

    [Fact]
    public void D3StageMismatchWhenMetadataDiffersFromTarget()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithDeny(deny);
        ExceptionMetadata meta = Meta(Guid.NewGuid(), deny.Id, PolicyPipelineStage.SiteDeny);
        PolicyLayer exception = ExceptionLayer(company, meta.TargetScopeId, deny.Id, ExemptRule(AddrSubset), meta);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.StageMismatch, result.Code);
    }

    [Fact]
    public void D4FamilyChainMismatch()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyRule exempt = PolicyRule.Create(
            IpAddressFamily.IPv6,
            PolicyFilterChain.Input,
            PolicyPipelineStage.CompanyDenyExemptions,
            0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrSubset)])),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage));
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, exempt);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.FamilyChainMismatch, result.Code);
    }

    [Fact]
    public void D5OmitServicesVsConstrainedTargetIsNotSubset()
    {
        Guid http = Guid.NewGuid();
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)]),
                services: ServiceSelector.Create([new ServiceObjectId(http)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithDeny(deny, extraService: http);
        PolicyRule exempt = ExemptRule(AddrSubset);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, exempt);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.NotSubset, result.Code);
    }

    [Fact]
    public void D5ProperSubsetInsertsExemptionBeforeDeny()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyRule exempt = ExemptRule(AddrSubset);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, exempt);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.ActiveRules.Count);
        Assert.Equal(exempt.Id, result.Value.ActiveRules[0].Id);
        Assert.Equal(PolicyPipelineStage.CompanyDenyExemptions, result.Value.ActiveRules[0].Stage);
        Assert.Equal(deny.Id, result.Value.ActiveRules[1].Id);
    }

    [Fact]
    public void D5OmitIngressZonesVsConstrainedTargetIsNotSubset()
    {
        Guid zone = Guid.NewGuid();
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)]),
                ingressZones: ZoneSelector.Create([new ZoneId(zone)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, new HashSet<Guid> { zone }, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.NotSubset, result.Code);
    }

    [Fact]
    public void D5OmitAddressExcludeVsTargetExcludeIsNotSubset()
    {
        Guid inside = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create(
                    [new AddressObjectId(AddrTarget)],
                    [new AddressObjectId(inside)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithExtraAddresses(
            deny,
            AddressPrefix(AddrTarget, "10.0.0.0", 24),
            AddressHost(inside, "10.0.0.2"));
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.NotSubset, result.Code);
    }

    [Fact]
    public void D5OmitConntrackVsConstrainedTargetIsNotSubset()
    {
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)]),
                connectionStates: [ConnectionState.New]),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.NotSubset, result.Code);
    }

    [Fact]
    public void D5OmitNatStatesVsConstrainedTargetIsNotSubset()
    {
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)]),
                connectionNatStates: [ConnectionNatState.SrcNat]),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.NotSubset, result.Code);
    }

    [Fact]
    public void D5OmitAddressTypesVsConstrainedTargetIsNotSubset()
    {
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)]),
                sourceAddressTypes: [AddressType.Unicast]),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.NotSubset, result.Code);
    }

    [Fact]
    public void D5OmitTcpFlagsVsConstrainedTargetIsNotSubset()
    {
        TcpFlagConstraint syn = TcpFlagConstraint.Create([TcpHeaderBit.Syn], []);
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)]),
                tcpFlags: syn),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.NotSubset, result.Code);
    }

    [Fact]
    public void D5OmitIpsecVsConstrainedTargetIsNotSubset()
    {
        IpsecPolicyPredicate ipsec = IpsecPolicyPredicate.Create(IpsecDirection.In, IpsecPolicyKind.Ipsec);
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)]),
                ipsecPolicy: ipsec),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.NotSubset, result.Code);
    }

    [Fact]
    public void D5UnequalIpsecIsNotSubset()
    {
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)]),
                ipsecPolicy: IpsecPolicyPredicate.Create(IpsecDirection.In, IpsecPolicyKind.Ipsec)),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyRule exempt = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDenyExemptions,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrSubset)]),
                ipsecPolicy: IpsecPolicyPredicate.Create(IpsecDirection.Out, IpsecPolicyKind.Ipsec)),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage));
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, exempt);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.NotSubset, result.Code);
    }

    [Fact]
    public void D5CopiedFlagsAndExtraExcludeSucceeds()
    {
        TcpFlagConstraint syn = TcpFlagConstraint.Create([TcpHeaderBit.Syn], []);
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)]),
                tcpFlags: syn),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyRule exempt = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDenyExemptions,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create(
                    [new AddressObjectId(AddrSubset)],
                    [new AddressObjectId(AddrOther)]),
                tcpFlags: syn),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage));
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, exempt);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyPipelineStage.CompanyDenyExemptions, result.Value!.ActiveRules[0].Stage);
    }

    [Fact]
    public void D5UnequalTcpFlagsIsNotSubset()
    {
        TcpFlagConstraint syn = TcpFlagConstraint.Create([TcpHeaderBit.Syn], []);
        TcpFlagConstraint ack = TcpFlagConstraint.Create([TcpHeaderBit.Ack], []);
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)]),
                tcpFlags: syn),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyRule exempt = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDenyExemptions,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrSubset)]),
                tcpFlags: ack),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage));
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, exempt);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.NotSubset, result.Code);
    }

    [Fact]
    public void D6UuidDisjointIncludeComposes()
    {
        PolicyRule target = DenyRule(AddrTarget);
        PolicyRule other = DenyRule(AddrOther, id: RuleId.New(), ordinal: 1);
        PolicyLayer company = CompanyWithDeny(target, extraDeny: other);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), target.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void D6OverlappingUuidIsOverlap()
    {
        PolicyRule target = DenyRule(AddrTarget);
        PolicyRule other = DenyRule(AddrTarget, id: RuleId.New(), ordinal: 1);
        PolicyLayer company = CompanyWithDeny(target, extraDeny: other);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), target.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.Overlap, result.Code);
    }

    [Fact]
    public void D5IntervalHostInsidePrefixDifferentUuidSucceeds()
    {
        Guid hostId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithExtraAddresses(deny, AddressPrefix(AddrTarget, "10.0.0.0", 24), AddressHost(hostId, "10.0.0.1"));
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(hostId));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyPipelineStage.CompanyDenyExemptions, result.Value!.ActiveRules[0].Stage);
    }

    [Fact]
    public void D6DifferentUuidsSamePrefixIsOverlap()
    {
        Guid samePrefix = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        PolicyRule target = DenyRule(AddrTarget);
        PolicyRule other = DenyRule(samePrefix, id: RuleId.New(), ordinal: 1);
        PolicyLayer company = CompanyWithExtraAddresses(
            target,
            AddressPrefix(AddrTarget, "10.0.0.0", 24),
            AddressPrefix(samePrefix, "10.0.0.0", 24),
            extraDeny: other);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), target.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.Overlap, result.Code);
    }

    [Fact]
    public void UnparseableExceptionPathObjectIsSelectorUnresolved()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyDocument document = new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            addressObjects: [JsonDocument.Parse("{\"id\":\"" + AddrTarget + "\"}").RootElement.Clone()],
            rules: [deny]);
        PolicyLayer company = CompanyLayer(document);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.SelectorUnresolved, result.Code);
    }

    [Fact]
    public void PredicateComplexityLimitOnExceptionServiceExpansion()
    {
        Guid fat = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)]),
                services: ServiceSelector.Create([new ServiceObjectId(fat)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyDocument document = new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            addressObjects: [AddressJson(AddrTarget), AddressJson(AddrOther)],
            serviceObjects: [FatServiceJson(fat, PredicateAlgebraCodes.MaxCubesPerRule + 1)],
            rules: [deny]);
        PolicyLayer company = CompanyLayer(document);
        PolicyRule exempt = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDenyExemptions,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrSubset)]),
                services: ServiceSelector.Create([new ServiceObjectId(fat)])),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage));
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, exempt);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PredicateAlgebraCodes.ComplexityLimit, result.Code);
    }

    [Fact]
    public void D7MandatoryDenyForbidden()
    {
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.MandatoryPreStateDeny,
            0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop));
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.MandatoryDeny, result.Code);
    }

    [Fact]
    public void D8NonExemptEffect()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyRule accept = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrSubset)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, accept);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.Effect, result.Code);
    }

    [Fact]
    public void D9CompanyWideExceptionForbiddenAtCreate()
    {
        Assert.Throws<Mfc.Domain.DomainInvariantException>(() =>
            Mfc.Domain.Policy.Policy.Create(
                NonEmptyName.Create("ex"),
                PolicyKind.Exception,
                PolicyOwnerScope.Company,
                null));
    }

    [Fact]
    public void D10InvalidWindowRejectedAtCreate()
    {
        Assert.Throws<Mfc.Domain.DomainInvariantException>(() =>
            ExceptionMetadata.Create(
                PolicyOwnerScope.Site,
                Guid.NewGuid(),
                PolicyPipelineStage.CompanyDeny,
                RuleId.New(),
                Until,
                From,
                "reason",
                "TICKET-1"));
        Assert.Throws<Mfc.Domain.DomainInvariantException>(() =>
            ExceptionMetadata.Create(
                PolicyOwnerScope.Site,
                Guid.NewGuid(),
                PolicyPipelineStage.CompanyDeny,
                RuleId.New(),
                From,
                Until,
                " ",
                "TICKET-1"));
    }

    [Fact]
    public void D11UniverseTargetForbidden()
    {
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.UniverseTarget, result.Code);
    }

    [Fact]
    public void D12TargetBytesChangeMismatchesParentContext()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer companyMutated = CompanyWithDeny(DenyRule(AddrTarget, id: deny.Id, description: "mutated"));
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(companyMutated, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.ParentContextMismatch, result.Code);
    }

    [Fact]
    public void D13HashSlotIndependentOfInputOrder()
    {
        PolicyRule denyA = DenyRule(AddrTarget);
        PolicyRule denyB = DenyRule(AddrOther, id: RuleId.New(), ordinal: 1);
        PolicyLayer company = CompanyWithDeny(denyA, extraDeny: denyB);
        Guid site = Guid.NewGuid();
        PolicyLayer first = ExceptionLayer(company, site, denyA.Id, ExemptRule(AddrSubset), policyId: Guid.Parse("11111111-1111-1111-1111-111111111111"));
        PolicyLayer second = ExceptionLayer(
            company,
            site,
            denyB.Id,
            ExemptRule(AddrOther),
            Meta(site, denyB.Id),
            policyId: Guid.Parse("22222222-2222-2222-2222-222222222222"));
        PolicyComposeResult left = Compose(company, first, second);
        PolicyComposeResult right = Compose(company, second, first);
        Assert.True(left.IsSuccess);
        Assert.Equal(left.Value!.LogicalEffectiveHash.ToString(), right.Value!.LogicalEffectiveHash.ToString());
    }

    [Fact]
    public void D15ExceptionHashesAppearAfterCount()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsSuccess);
        byte[] preimage = PolicyHashing.BuildLogicalEffectivePreimage(
            company.PolicyDocument.SchemaVersion,
            company.ContentHash,
            null,
            null,
            [exception.ContentHash],
            [],
            result.Value!.ActiveRules.Select(PolicyCanonicalWriter.WriteRuleBytes).ToArray(),
            PolicyCanonicalWriter.WriteChainContractSetBytes(company.PolicyDocument.ChainContracts));
        Assert.Contains(
            Convert.ToHexString(exception.ContentHash.Bytes.ToArray()),
            Convert.ToHexString(preimage),
            StringComparison.Ordinal);
        Assert.Equal(1, ReadExceptionCount(preimage, company.ContentHash));
    }

    [Fact]
    public void D16HashDiffersFromSyntheticDocument()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, ExemptRule(AddrSubset));
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsSuccess);
        PolicyDocument synthetic = new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            chainContracts: company.PolicyDocument.ChainContracts,
            addressObjects: result.Value!.MergedAddressObjects,
            rules: result.Value.ActiveRules);
        Assert.NotEqual(
            result.Value.LogicalEffectiveHash.ToString(),
            PolicyHashing.HashContent(synthetic).ToString());
    }

    [Fact]
    public void D17NodeCannotWaiveSiteDeny()
    {
        Guid siteId = Guid.NewGuid();
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.SiteDeny,
            0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrTarget)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyLayer company = CompanyLayer(CompanyDocument());
        PolicyDocument siteDoc = new(
            PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site,
            addressObjects: [AddressJson(AddrTarget)],
            rules: [deny]);
        PolicyLayer site = Overlay(PolicyKind.SiteOverlay, PolicyOwnerScope.Site, siteId, siteDoc, company.ContentHash);
        PolicyRule exempt = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.SiteDenyExemptions,
            0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrSubset)])),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage));
        ExceptionMetadata meta = ExceptionMetadata.Create(
            PolicyOwnerScope.Node,
            Guid.NewGuid(),
            PolicyPipelineStage.SiteDeny,
            deny.Id,
            From,
            Until,
            "reason",
            "TICKET-1");
        PolicyDocument exDoc = new(
            PolicyKind.Exception,
            PolicyOwnerScope.Node,
            rules: [exempt],
            exceptionMetadata: meta);
        Hash256 parent = PolicyHashing.ComputeParentContextHash(
            PolicyKind.Exception,
            company.ContentHash,
            site.ContentHash,
            null,
            PolicyHashing.HashContent(PolicyCanonicalWriter.WriteRuleBytes(deny)))!;
        PolicyLayer exception = new()
        {
            PolicyId = Guid.NewGuid(),
            RevisionId = Guid.NewGuid(),
            Kind = PolicyKind.Exception,
            OwnerScope = PolicyOwnerScope.Node,
            OwnerId = meta.TargetScopeId,
            ContentHash = PolicyHashing.HashContent(exDoc),
            ParentContextHash = parent,
            PolicyDocument = exDoc,
        };
        PolicyComposeResult result = EffectivePolicyComposer.Compose(
            company, site, null, Guid.NewGuid(), siteId, new HashSet<Guid>(), [exception]);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.StageOwnership, result.Code);
        Assert.NotEqual(PolicyComposeCodes.StageOwnership, result.Code);
    }

    [Fact]
    public void D18OwnerMismatchIsMetadataInvalid()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithDeny(deny);
        Guid siteId = Guid.NewGuid();
        ExceptionMetadata meta = Meta(Guid.NewGuid(), deny.Id);
        PolicyLayer exception = ExceptionLayer(company, siteId, deny.Id, ExemptRule(AddrSubset), meta);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.MetadataInvalid, result.Code);
    }

    [Fact]
    public void D19ObjectsForbidden()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithDeny(deny);
        Guid siteId = Guid.NewGuid();
        PolicyRule exempt = ExemptRule(AddrSubset);
        ExceptionMetadata meta = Meta(siteId, deny.Id);
        PolicyDocument doc = new(
            PolicyKind.Exception,
            PolicyOwnerScope.Site,
            addressObjects: [ObjectJson(AddrSubset)],
            rules: [exempt],
            exceptionMetadata: meta);
        PolicyLayer exception = WrapException(doc, siteId, company, deny);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.ObjectsForbidden, result.Code);
    }

    [Fact]
    public void D14ExemptForbiddenOnDenyStage()
    {
        Mfc.Domain.DomainInvariantException ex = Assert.Throws<Mfc.Domain.DomainInvariantException>(() =>
            PolicyRule.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Forward,
                PolicyPipelineStage.CompanyDeny,
                0,
                TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([new AddressObjectId(AddrSubset)])),
                RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage)));
        Assert.Contains("EXEMPT_DENY_STAGE", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DSortExemptionStageOrdersByRevisionThenOrdinalThenRuleId()
    {
        PolicyRule denyA = DenyRule(AddrTarget);
        PolicyRule denyB = DenyRule(AddrOther, id: RuleId.New(), ordinal: 1);
        PolicyLayer company = CompanyWithDeny(denyA, extraDeny: denyB);
        Guid site = Guid.NewGuid();
        PolicyRule exemptA = ExemptRule(AddrSubset);
        PolicyRule exemptB = ExemptRule(AddrOther);
        PolicyLayer laterRevision = ExceptionLayer(
            company,
            site,
            denyA.Id,
            exemptA,
            policyId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            revisionId: Guid.Parse("22222222-2222-2222-2222-222222222222"));
        PolicyLayer earlierRevision = ExceptionLayer(
            company,
            site,
            denyB.Id,
            exemptB,
            Meta(site, denyB.Id),
            policyId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            revisionId: Guid.Parse("11111111-1111-1111-1111-111111111111"));
        PolicyComposeResult result = Compose(company, laterRevision, earlierRevision);
        Assert.True(result.IsSuccess);
        Assert.Equal(exemptB.Id, result.Value!.ActiveRules[0].Id);
        Assert.Equal(exemptA.Id, result.Value.ActiveRules[1].Id);
        Assert.Equal(PolicyPipelineStage.CompanyDenyExemptions, result.Value.ActiveRules[0].Stage);
        Assert.Equal(PolicyPipelineStage.CompanyDenyExemptions, result.Value.ActiveRules[1].Stage);
    }

    [Fact]
    public void DRuleCountZeroEnabledFails()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithDeny(deny);
        PolicyRule disabled = ExemptRule(AddrSubset, enabled: false);
        PolicyLayer exception = ExceptionLayer(company, Guid.NewGuid(), deny.Id, disabled);
        PolicyComposeResult result = Compose(company, exception);
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.RuleCount, result.Code);
    }

    [Fact]
    public void DParentSiteOmitsNodeOverlayHash()
    {
        PolicyRule deny = DenyRule(AddrTarget);
        PolicyLayer company = CompanyWithDeny(deny);
        Guid siteId = Guid.NewGuid();
        Guid nodeId = Guid.NewGuid();
        PolicyRule exempt = ExemptRule(AddrSubset);
        Hash256 waived = PolicyHashing.HashContent(PolicyCanonicalWriter.WriteRuleBytes(deny));
        PolicyDocument nodeDoc = new(PolicyKind.NodeOverlay, PolicyOwnerScope.Node);
        Hash256 nodeParent = PolicyHashing.ComputeParentContextHash(
            PolicyKind.NodeOverlay, company.ContentHash, null, null, null)!;
        PolicyLayer node = Overlay(PolicyKind.NodeOverlay, PolicyOwnerScope.Node, nodeId, nodeDoc, nodeParent);
        Hash256 expected = PolicyHashing.ComputeParentContextHash(
            PolicyKind.Exception, company.ContentHash, null, null, waived)!;
        Hash256 withNode = PolicyHashing.ComputeParentContextHash(
            PolicyKind.Exception, company.ContentHash, null, node.ContentHash, waived)!;
        Assert.NotEqual(expected.ToString(), withNode.ToString());
        PolicyLayer exception = ExceptionLayer(company, siteId, deny.Id, exempt, parentOverride: expected);
        PolicyComposeResult result = EffectivePolicyComposer.Compose(
            company, null, node, nodeId, siteId, new HashSet<Guid>(), [exception]);
        Assert.True(result.IsSuccess);
    }

    private static PolicyComposeResult Compose(PolicyLayer company, params PolicyLayer[] exceptions)
        => Compose(company, new HashSet<Guid>(), exceptions);

    private static PolicyComposeResult Compose(
        PolicyLayer company,
        IReadOnlySet<Guid> knownZones,
        params PolicyLayer[] exceptions)
        => EffectivePolicyComposer.Compose(
            company, null, null, Guid.NewGuid(), Guid.NewGuid(), knownZones, exceptions);

    private static PolicyLayer CompanyWithExtraAddresses(
        PolicyRule deny,
        JsonElement first,
        JsonElement second,
        PolicyRule? extraDeny = null)
    {
        List<PolicyRule> rules = [deny];
        if (extraDeny is not null)
        {
            rules.Add(extraDeny);
        }

        return CompanyLayer(new PolicyDocument(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            addressObjects: [first, second],
            rules: rules));
    }

    private static JsonElement FatServiceJson(Guid id, int termCount)
    {
        List<string> terms = [];
        byte protocol = 1;
        for (int i = 0; i < termCount; i++)
        {
            if (protocol == IpProtocol.IcmpV6)
            {
                protocol++;
            }

            terms.Add("{\"protocol\":{\"number\":" + protocol.ToString(CultureInfo.InvariantCulture) + "}}");
            protocol++;
        }

        return JsonDocument.Parse(
            "{\"id\":\"" + id + "\",\"name\":\"fat\",\"terms\":[" + string.Join(",", terms) + "]}").RootElement.Clone();
    }

    private static PolicyLayer CompanyWithDeny(
        PolicyRule? deny = null,
        PolicyRule? extraDeny = null,
        Guid? extraService = null)
    {
        deny ??= DenyRule(AddrTarget);
        List<PolicyRule> rules = [deny];
        if (extraDeny is not null)
        {
            rules.Add(extraDeny);
        }

        List<JsonElement> addresses = [AddressJson(AddrTarget), AddressJson(AddrOther)];
        List<JsonElement> services = extraService is Guid sid ? [ServiceJson(sid)] : [];
        return CompanyLayer(new PolicyDocument(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            addressObjects: addresses,
            serviceObjects: services,
            rules: rules));
    }

    private static PolicyDocument CompanyDocument()
        => new(PolicyKind.CompanyBaseline, PolicyOwnerScope.Company);

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

    private static PolicyLayer Overlay(
        PolicyKind kind,
        PolicyOwnerScope scope,
        Guid ownerId,
        PolicyDocument document,
        Hash256 parent)
        => new()
        {
            PolicyId = Guid.NewGuid(),
            RevisionId = Guid.NewGuid(),
            Kind = kind,
            OwnerScope = scope,
            OwnerId = ownerId,
            ContentHash = PolicyHashing.HashContent(document),
            ParentContextHash = parent,
            PolicyDocument = document,
        };

    private static PolicyLayer ExceptionLayer(
        PolicyLayer company,
        Guid siteId,
        RuleId waived,
        PolicyRule exempt,
        ExceptionMetadata? meta = null,
        Guid? policyId = null,
        Hash256? parentOverride = null,
        Guid? revisionId = null)
    {
        meta ??= Meta(siteId, waived);
        PolicyDocument doc = new(
            PolicyKind.Exception,
            PolicyOwnerScope.Site,
            rules: [exempt],
            exceptionMetadata: meta);
        PolicyRule? target = company.PolicyDocument.Rules.FirstOrDefault(r => r.Id == waived);
        return WrapException(doc, siteId, company, target, policyId, parentOverride, revisionId);
    }

    private static PolicyLayer WrapException(
        PolicyDocument doc,
        Guid ownerId,
        PolicyLayer company,
        PolicyRule? target,
        Guid? policyId = null,
        Hash256? parentOverride = null,
        Guid? revisionId = null)
    {
        Hash256 waivedHash = target is null
            ? Hash256.Create(new byte[32])
            : PolicyHashing.HashContent(PolicyCanonicalWriter.WriteRuleBytes(target));
        Hash256 parent = parentOverride ?? PolicyHashing.ComputeParentContextHash(
            PolicyKind.Exception,
            company.ContentHash,
            null,
            null,
            waivedHash)!;
        return new PolicyLayer
        {
            PolicyId = policyId ?? Guid.NewGuid(),
            RevisionId = revisionId ?? Guid.NewGuid(),
            Kind = PolicyKind.Exception,
            OwnerScope = doc.OwnerScope,
            OwnerId = ownerId,
            ContentHash = PolicyHashing.HashContent(doc),
            ParentContextHash = parent,
            PolicyDocument = doc,
        };
    }

    private static ExceptionMetadata Meta(
        Guid siteId,
        RuleId waived,
        PolicyPipelineStage stage = PolicyPipelineStage.CompanyDeny)
        => ExceptionMetadata.Create(
            PolicyOwnerScope.Site,
            siteId,
            stage,
            waived,
            From,
            Until,
            "change window",
            "TICKET-1");

    private static PolicyRule DenyRule(
        Guid addr,
        RuleId? id = null,
        bool enabled = true,
        uint ordinal = 0,
        string? description = null)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            ordinal,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([new AddressObjectId(addr)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            enabled: enabled,
            exceptionEligible: true,
            description: description,
            id: id);

    private static PolicyRule ExemptRule(Guid addr, bool enabled = true)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDenyExemptions,
            0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([new AddressObjectId(addr)])),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage),
            enabled: enabled);

    private static JsonElement ObjectJson(Guid id)
        => AddressJson(id);

    private static JsonElement AddressJson(Guid id)
    {
        if (id == Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))
        {
            return AddressPrefix(id, "10.0.0.0", 24);
        }

        if (id == Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))
        {
            return AddressHost(id, "10.0.1.1");
        }

        return AddressHost(id, "10.0.0.1");
    }

    private static JsonElement AddressPrefix(Guid id, string address, int prefixLength)
        => JsonDocument.Parse(
            "{\"id\":\"" + id + "\",\"name\":\"addr\",\"family\":\"IPv4\",\"entries\":[{\"kind\":\"PREFIX\",\"address\":\"" +
            address + "\",\"prefix_length\":" + prefixLength + "}]}").RootElement.Clone();

    private static JsonElement AddressHost(Guid id, string address)
        => JsonDocument.Parse(
            "{\"id\":\"" + id + "\",\"name\":\"addr\",\"family\":\"IPv4\",\"entries\":[{\"kind\":\"HOST\",\"address\":\"" +
            address + "\"}]}").RootElement.Clone();

    private static JsonElement ServiceJson(Guid id, byte protocol = 6, ushort port = 80)
        => JsonDocument.Parse(
            "{\"id\":\"" + id +
            "\",\"name\":\"svc\",\"terms\":[{\"protocol\":{\"number\":" + protocol +
            "},\"destination_ports\":[{\"start\":" + port + ",\"end\":" + port + "}]}]}").RootElement.Clone();

    private static int ReadExceptionCount(byte[] preimage, Hash256 companyHash)
    {
        byte[] needle = companyHash.Bytes.ToArray();
        for (int i = 0; i <= preimage.Length - needle.Length - 4; i++)
        {
            if (preimage.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return (preimage[i + needle.Length] << 24)
                       | (preimage[i + needle.Length + 1] << 16)
                       | (preimage[i + needle.Length + 2] << 8)
                       | preimage[i + needle.Length + 3];
            }
        }

        return -1;
    }
}
