using Mfc.Application.Policies;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class PolicyEvidenceContextMapperTests
{
    [Fact]
    public void CanonicalFilterEnablesNodeEffectiveWithoutWritingPolicy()
    {
        PolicyRule allow = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyTestCase test = PolicyTestCase.Create(
            "node",
            PolicyTestOrigin.User,
            PolicyTestExecutionMode.NodeEffective,
            PolicyTestPacket.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Forward,
                "192.0.2.1",
                "192.0.2.2",
                protocol: IpProtocol.Tcp),
            PolicyTestExpectedDisposition.Accept,
            allow.Id);
        CanonicalRecord anchor = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "0",
            ["chain"] = "forward",
            ["action"] = "jump",
            ["comment"] = "fwc:anchor:forward",
        });
        PolicyEvidenceAnalysisResult result = PolicyEvidenceContextMapper.Analyze(
            [allow],
            [test],
            ChainContractSet.CreateForCompanyBaseline(
                [
                    ChainContract.Create(
                        IpAddressFamily.IPv4,
                        PolicyFilterChain.Forward,
                        ChainDefaultDisposition.Drop,
                        rejectMode: null,
                        PolicyRuntimeMode.ManagedOnly),
                ],
                PolicyRuntimeMode.ManagedOnly),
            new Dictionary<AddressObjectId, AddressObject>(),
            new Dictionary<ServiceObjectId, ServiceObject>(),
            ipv4Filter: [anchor]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.OutcomePass, Assert.Single(result.TestResults).Outcome);
        Assert.Equal(allow.Id, Assert.Single(result.TestResults).MatchedRuleId);
        Assert.False(result.HasBlockers);
    }

    [Fact]
    public void CanonicalIpv6FilterIsMappedWithoutWritingPolicy()
    {
        CanonicalRecord anchor = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "0",
            ["chain"] = "forward",
            ["action"] = "jump",
            ["comment"] = "fwc:anchor:forward",
        });
        PolicyEvidenceAnalysisResult result = PolicyEvidenceContextMapper.Analyze(
            [],
            [],
            ChainContractSet.CreateForCompanyBaseline(
                [
                    ChainContract.Create(
                        IpAddressFamily.IPv6,
                        PolicyFilterChain.Forward,
                        ChainDefaultDisposition.Drop,
                        rejectMode: null,
                        PolicyRuntimeMode.ManagedOnly),
                ],
                PolicyRuntimeMode.ManagedOnly),
            new Dictionary<AddressObjectId, AddressObject>(),
            new Dictionary<ServiceObjectId, ServiceObject>(),
            ipv6Filter: [anchor]);
        Assert.False(result.HasBlockers);
        Assert.Empty(result.TestResults);
    }
}
