using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>Normative permanent-anchor enable order (Onboarding Spec §37).</summary>
public static class OnboardingEnableOrder
{
    /// <summary>
    /// IPv4 FORWARD, IPv6 FORWARD, IPv4 OUTPUT, IPv6 OUTPUT, IPv4 INPUT, IPv6 INPUT.
    /// Missing keys are omitted. SWITCH has no FORWARD.
    /// </summary>
    public static IReadOnlyList<AnchorKey> Sort(IEnumerable<AnchorKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return keys
            .OrderBy(static k => Rank(k.Chain))
            .ThenBy(static k => k.Family == IpAddressFamily.IPv4 ? 0 : 1)
            .ToArray();
    }

    private static int Rank(FilterBuiltInContext chain)
        => chain switch
        {
            FilterBuiltInContext.Forward => 0,
            FilterBuiltInContext.Output => 1,
            FilterBuiltInContext.Input => 2,
            _ => throw new DomainInvariantException($"Unsupported enable-order chain '{chain}'."),
        };
}
