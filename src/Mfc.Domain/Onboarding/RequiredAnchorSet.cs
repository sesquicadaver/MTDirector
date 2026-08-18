using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Required permanent-anchor set for a Node kind (Onboarding Spec §18). IPv4 is always required.
/// SWITCH never includes FORWARD.
/// </summary>
public static class RequiredAnchorSet
{
    /// <summary>
    /// Builds the normative required set. IPv6 anchors are included only when
    /// <paramref name="includeIpv6"/> is true (policy/device support is validated in M5-02+).
    /// </summary>
    public static IReadOnlyList<AnchorKey> For(NodeKind kind, bool includeIpv6)
    {
        if (kind is not (NodeKind.Router or NodeKind.Vrrp or NodeKind.Switch))
        {
            throw new DomainInvariantException($"Unsupported node kind '{kind}'.");
        }

        List<AnchorKey> keys = [.. ForFamily(kind, IpAddressFamily.IPv4)];
        if (includeIpv6)
        {
            keys.AddRange(ForFamily(kind, IpAddressFamily.IPv6));
        }

        keys.Sort(static (a, b) => string.CompareOrdinal(a.Marker, b.Marker));
        return keys;
    }

    /// <summary>True when <paramref name="keys"/> contains FORWARD for any family.</summary>
    public static bool ContainsForward(IEnumerable<AnchorKey> keys)
        => keys.Any(static k => k.Chain == FilterBuiltInContext.Forward);

    private static IEnumerable<AnchorKey> ForFamily(NodeKind kind, IpAddressFamily family)
    {
        yield return AnchorKey.Create(family, FilterBuiltInContext.Input);
        if (kind is NodeKind.Router or NodeKind.Vrrp)
        {
            yield return AnchorKey.Create(family, FilterBuiltInContext.Forward);
        }

        yield return AnchorKey.Create(family, FilterBuiltInContext.Output);
    }
}
