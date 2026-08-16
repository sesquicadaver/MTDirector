using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Maps filter discovery onto Domain policy-test NODE_EFFECTIVE evaluation (M2-16).
/// Does not write tests, policy, or RouterOS filter rules.
/// </summary>
public static class PolicyEvidenceBlockerMapper
{
    public static PolicyEvidenceAnalysisResult Analyze(
        IReadOnlyList<PolicyRule> afterRules,
        IReadOnlyList<PolicyTestCase> tests,
        ChainContractSet contracts,
        IReadOnlyDictionary<AddressObjectId, AddressObject> afterAddresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> afterServices,
        FirewallFilterDiscoveryResult? filter = null,
        IReadOnlyList<PolicyRule>? beforeRules = null,
        IReadOnlyDictionary<AddressObjectId, AddressObject>? beforeAddresses = null,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject>? beforeServices = null,
        IReadOnlySet<Guid>? beforeZoneIds = null,
        IReadOnlySet<Guid>? afterZoneIds = null,
        PolicyEvidenceSignals? signals = null)
    {
        ArgumentNullException.ThrowIfNull(afterRules);
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(afterAddresses);
        ArgumentNullException.ThrowIfNull(afterServices);
        IReadOnlyList<ActualFilterRule>? actual = filter is null
            ? null
            : ActualFilterRuleMapper.FromDiscovery(filter);
        return PolicyEvidenceAnalysis.Analyze(
            afterRules,
            tests,
            contracts,
            afterAddresses,
            afterServices,
            beforeRules,
            beforeAddresses,
            beforeServices,
            beforeZoneIds,
            afterZoneIds,
            actual,
            signals);
    }
}
