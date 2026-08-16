using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class PolicyEvidenceBlockerMapperTests
{
    [Fact]
    public void DiscoveryPreAnchorAcceptFailsNodeEffectiveSafetyWithoutWritingFilters()
    {
        PolicyRule allow = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyTestCase safety = PolicyTestCase.Create(
            "sys-forward",
            PolicyTestOrigin.System,
            PolicyTestExecutionMode.NodeEffective,
            PolicyTestPacket.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Forward,
                "192.0.2.1",
                "192.0.2.2",
                protocol: IpProtocol.Tcp),
            PolicyTestExpectedDisposition.Accept,
            allow.Id);
        FirewallFilterDiscoveryResult filter = FirewallFilterDiscovery.BuildResult(
            Ok(
                RosReadCommandId.Ipv4Filter,
                Row(("chain", "forward"), ("action", "accept"), ("comment", "unmanaged")),
                Row(("chain", "forward"), ("action", "jump"), ("comment", "fwc:anchor:forward"))),
            Ok(RosReadCommandId.Ipv6Filter),
            Ok(RosReadCommandId.Ipv4AddressLists),
            Ok(RosReadCommandId.Ipv6AddressLists));
        PolicyEvidenceAnalysisResult result = PolicyEvidenceBlockerMapper.Analyze(
            [allow],
            [safety],
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
            filter);
        PolicyTestResult test = Assert.Single(result.TestResults);
        Assert.Equal(PolicyTestExpectedDisposition.Accept, test.FinalDisposition);
        Assert.Contains(test.MatchedPath, h => h.Kind == PolicyTestPathKind.UnmanagedRule);
        Assert.Null(test.MatchedRuleId);
        Assert.Equal(PolicyEvidenceAnalysisCodes.OutcomeFail, test.Outcome);
        Assert.Contains(result.Findings, f => f.Code == PolicyEvidenceAnalysisCodes.SafetyTestFailed);
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
