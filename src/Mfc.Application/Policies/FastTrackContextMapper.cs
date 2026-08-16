using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Policies;

/// <summary>
/// Maps canonical routing/NAT/Mangle/filter/packet-path records onto Domain FastTrack analysis (M2-15).
/// Does not call RouterOS, does not compile the ACCEPT fallback pair, and does not write FastTrack.
/// </summary>
public static class FastTrackContextMapper
{
    public static FastTrackAnalysisResult Analyze(
        IReadOnlyList<PolicyRule> rules,
        TopologyDependencyProfile profile,
        TopologyDependencyCanonicalSections sections,
        IReadOnlyList<CanonicalRecord>? ipv4Filter = null,
        IReadOnlyList<CanonicalRecord>? packetPathNodes = null,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject>? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sections);
        TopologyDependencyFacts facts = TopologyDependencyContextMapper.FromCanonical(profile, sections);
        FastTrackTopologyContext topology = FastTrackTopologyContext.From(
            facts,
            hasVrf: HasVrf(packetPathNodes),
            hasPreAnchorUnmanagedFastTrack: FastTrackAnalysis.HasPreAnchorUnmanagedFastTrack(
                ActualFilterContextMapper.FromCanonicalFilter(IpAddressFamily.IPv4, ipv4Filter ?? [])));
        return FastTrackAnalysis.Analyze(rules, topology, catalog);
    }

    private static bool HasVrf(IReadOnlyList<CanonicalRecord>? nodes)
    {
        if (nodes is null)
        {
            return false;
        }

        foreach (CanonicalRecord record in nodes)
        {
            string? kind = record.Properties.TryGetValue("kind", out string? value) ? value : null;
            if (string.Equals(kind, "Vrf", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
