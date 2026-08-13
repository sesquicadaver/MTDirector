using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Include/exclude zone selector (Policy Model §22).</summary>
public sealed class ZoneSelector
{
    public IReadOnlyList<ZoneId> Include { get; }

    public IReadOnlyList<ZoneId> Exclude { get; }

    private ZoneSelector(IReadOnlyList<ZoneId> include, IReadOnlyList<ZoneId> exclude)
    {
        Include = include;
        Exclude = exclude;
    }

    public static ZoneSelector Create(
        IEnumerable<ZoneId>? include = null,
        IEnumerable<ZoneId>? exclude = null)
    {
        ZoneId[] includeIds = (include ?? []).ToArray();
        ZoneId[] excludeIds = (exclude ?? []).ToArray();
        EnsureUnique(includeIds, "include");
        EnsureUnique(excludeIds, "exclude");
        return new ZoneSelector(includeIds, excludeIds);
    }

    /// <summary>
    /// Chain constraints (Policy Model §22 / M2-05 AC#10–11):
    /// INPUT forbids egress zones; OUTPUT forbids ingress zones; FORWARD allows both.
    /// </summary>
    public static void EnsureAllowedOnChain(
        PolicyFilterChain chain,
        ZoneSelector? ingressZones,
        ZoneSelector? egressZones)
    {
        switch (chain)
        {
            case PolicyFilterChain.Input:
                if (egressZones is not null)
                {
                    throw new DomainInvariantException(
                        "INPUT chain forbids egress zone selectors.");
                }

                break;

            case PolicyFilterChain.Output:
                if (ingressZones is not null)
                {
                    throw new DomainInvariantException(
                        "OUTPUT chain forbids ingress zone selectors.");
                }

                break;

            case PolicyFilterChain.Forward:
                break;

            default:
                throw new DomainInvariantException($"Unknown filter chain '{chain}'.");
        }
    }

    private static void EnsureUnique(IReadOnlyList<ZoneId> ids, string label)
    {
        HashSet<Guid> seen = [];
        foreach (ZoneId id in ids)
        {
            if (!seen.Add(id.Value))
            {
                throw new DomainInvariantException($"Duplicate zone id in selector {label}.");
            }
        }
    }
}
