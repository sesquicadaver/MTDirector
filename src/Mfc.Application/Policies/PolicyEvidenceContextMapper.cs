using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Policies;

/// <summary>
/// Maps typed policy documents and canonical filter records onto Domain test/diff/risk analysis (M2-16).
/// Does not call RouterOS, does not approve, and does not write policy or filter rules.
/// </summary>
public static class PolicyEvidenceContextMapper
{
    public static PolicyEvidenceAnalysisResult Analyze(
        IReadOnlyList<PolicyRule> afterRules,
        IReadOnlyList<PolicyTestCase> tests,
        ChainContractSet contracts,
        IReadOnlyDictionary<AddressObjectId, AddressObject> afterAddresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> afterServices,
        IReadOnlyList<PolicyRule>? beforeRules = null,
        IReadOnlyDictionary<AddressObjectId, AddressObject>? beforeAddresses = null,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject>? beforeServices = null,
        IReadOnlySet<Guid>? beforeZoneIds = null,
        IReadOnlySet<Guid>? afterZoneIds = null,
        IReadOnlyList<CanonicalRecord>? ipv4Filter = null,
        IReadOnlyList<CanonicalRecord>? ipv6Filter = null,
        PolicyEvidenceSignals? signals = null)
    {
        ArgumentNullException.ThrowIfNull(afterRules);
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(afterAddresses);
        ArgumentNullException.ThrowIfNull(afterServices);
        IReadOnlyList<ActualFilterRule>? actual = null;
        if (ipv4Filter is not null || ipv6Filter is not null)
        {
            List<ActualFilterRule> rules = [];
            if (ipv4Filter is not null)
            {
                rules.AddRange(ActualFilterContextMapper.FromCanonicalFilter(IpAddressFamily.IPv4, ipv4Filter));
            }

            if (ipv6Filter is not null)
            {
                rules.AddRange(ActualFilterContextMapper.FromCanonicalFilter(IpAddressFamily.IPv6, ipv6Filter));
            }

            actual = rules;
        }

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
