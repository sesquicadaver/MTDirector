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
        FastTrackTopologyContext topology = MapTopology(profile, sections, ipv4Filter, packetPathNodes);
        return FastTrackAnalysis.Analyze(rules, topology, catalog);
    }

    /// <summary>
    /// Builds per-device FastTrack topology from capture-derived canonical sections (AUDIT-INT-01 §16).
    /// Never invents <see cref="FastTrackTopologyContext.SafeSingleWan"/> — callers leave null when capture/sections are absent.
    /// </summary>
    public static FastTrackTopologyContext MapTopology(
        TopologyDependencyProfile profile,
        TopologyDependencyCanonicalSections sections,
        IReadOnlyList<CanonicalRecord>? ipv4Filter = null,
        IReadOnlyList<CanonicalRecord>? packetPathNodes = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sections);
        TopologyDependencyFacts facts = TopologyDependencyContextMapper.FromCanonical(profile, sections);
        return FastTrackTopologyContext.From(
            facts,
            hasVrf: HasVrf(packetPathNodes),
            hasPreAnchorUnmanagedFastTrack: FastTrackAnalysis.HasPreAnchorUnmanagedFastTrack(
                ActualFilterContextMapper.FromCanonicalFilter(IpAddressFamily.IPv4, ipv4Filter ?? [])));
    }

    /// <summary>Projects stored snapshot sections into topology + filter inputs for FastTrack mapping.</summary>
    public static (
        TopologyDependencyCanonicalSections Topology,
        IReadOnlyList<CanonicalRecord> Ipv4Filter,
        IReadOnlyList<CanonicalRecord> PacketPathNodes) FromCanonicalSnapshotSections(
        IReadOnlyList<CanonicalSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        return (
            ToTopologySections(sections),
            Records(sections, CanonicalSectionIds.FirewallIpv4Filter, CanonicalDomain.Configuration),
            PacketPathNodes(sections));
    }

    public static TopologyDependencyCanonicalSections ToTopologySections(IReadOnlyList<CanonicalSection> sections)
        => new()
        {
            VrrpConfiguration = Records(sections, CanonicalSectionIds.HaVrrp, CanonicalDomain.Configuration),
            VrrpObservations = Records(sections, CanonicalSectionIds.HaVrrp, CanonicalDomain.Observations),
            RoutingTables = Records(sections, CanonicalSectionIds.RoutingTables, CanonicalDomain.Configuration),
            RoutingRules = Records(sections, CanonicalSectionIds.RoutingRules, CanonicalDomain.Configuration),
            Ipv4Nat = Records(sections, CanonicalSectionIds.FirewallIpv4Nat, CanonicalDomain.Configuration),
            Ipv6Nat = Records(sections, CanonicalSectionIds.FirewallIpv6Nat, CanonicalDomain.Configuration),
            Ipv4Raw = Records(sections, CanonicalSectionIds.FirewallIpv4Raw, CanonicalDomain.Configuration),
            Ipv6Raw = Records(sections, CanonicalSectionIds.FirewallIpv6Raw, CanonicalDomain.Configuration),
            Ipv4Mangle = Records(sections, CanonicalSectionIds.FirewallIpv4Mangle, CanonicalDomain.Configuration),
            Ipv6Mangle = Records(sections, CanonicalSectionIds.FirewallIpv6Mangle, CanonicalDomain.Configuration),
            Ipv4Settings = Records(sections, CanonicalSectionIds.NetworkIpv4Settings, CanonicalDomain.Configuration),
            Ipv4DefaultState = Records(
                sections, CanonicalSectionIds.RoutingIpv4DefaultState, CanonicalDomain.Configuration),
            Ipv6DefaultState = Records(
                sections, CanonicalSectionIds.RoutingIpv6DefaultState, CanonicalDomain.Configuration),
            SwitchInstances = Records(sections, CanonicalSectionIds.SwitchInstances, CanonicalDomain.Configuration),
            BridgeSettings = Records(sections, CanonicalSectionIds.BridgeSettings, CanonicalDomain.Configuration),
        };

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

    private static IReadOnlyList<CanonicalRecord> Records(
        IReadOnlyList<CanonicalSection> sections,
        string sectionId,
        CanonicalDomain domain)
    {
        foreach (CanonicalSection section in sections)
        {
            if (section.SectionId == sectionId && section.Domain == domain)
            {
                return section.Records;
            }
        }

        return [];
    }

    private static List<CanonicalRecord> PacketPathNodes(IReadOnlyList<CanonicalSection> sections)
    {
        List<CanonicalRecord> nodes = [];
        foreach (CanonicalSection section in sections)
        {
            if (section.SectionId is CanonicalSectionIds.TopologyValidation
                or CanonicalSectionIds.TopologyContainerVeth)
            {
                nodes.AddRange(section.Records);
            }
        }

        return nodes;
    }
}
