using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>Anchor change criticality for activation order (Safe Deployment Spec §28).</summary>
public enum AnchorActivationCriticality : byte
{
    NonManagementCritical = 0,
    ManagementCritical = 1,
}

/// <summary>
/// Plans permanent-anchor activation order: non-management-critical first, management-critical last.
/// Typical direct management path puts INPUT (and usually OUTPUT) last; order is not hardcoded forever —
/// callers may supply analysis-derived criticality (Spec §28).
/// </summary>
public static class DeploymentAnchorOrder
{
    /// <summary>
    /// Default criticality for a typical direct management path:
    /// FORWARD = non-critical; OUTPUT/INPUT = management-critical.
    /// </summary>
    public static AnchorActivationCriticality DefaultCriticality(AnchorKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.Chain switch
        {
            FilterBuiltInContext.Forward => AnchorActivationCriticality.NonManagementCritical,
            FilterBuiltInContext.Output => AnchorActivationCriticality.ManagementCritical,
            FilterBuiltInContext.Input => AnchorActivationCriticality.ManagementCritical,
            _ => throw new DomainInvariantException($"Unsupported activation chain '{key.Chain}'."),
        };
    }

    /// <summary>
    /// Sort keys so all non-management-critical anchors precede management-critical ones.
    /// Within a group: FORWARD, then OUTPUT, then INPUT; IPv4 before IPv6.
    /// </summary>
    public static IReadOnlyList<AnchorKey> Sort(
        IEnumerable<AnchorKey> keys,
        IReadOnlyDictionary<AnchorKey, AnchorActivationCriticality>? criticality = null)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return keys
            .OrderBy(k => (int)Resolve(k, criticality))
            .ThenBy(static k => ChainRank(k.Chain))
            .ThenBy(static k => k.Family == IpAddressFamily.IPv4 ? 0 : 1)
            .ToArray();
    }

    /// <summary>Rollback order is the reverse of activation (Spec §9).</summary>
    public static IReadOnlyList<AnchorKey> RollbackOrder(
        IEnumerable<AnchorKey> activationOrder,
        IReadOnlyDictionary<AnchorKey, AnchorActivationCriticality>? criticality = null)
        => Sort(activationOrder, criticality).Reverse().ToArray();

    /// <summary>
    /// True when every management-critical key appears after every non-management-critical key.
    /// </summary>
    public static bool IsManagementCriticalLast(
        IReadOnlyList<AnchorKey> activationOrder,
        IReadOnlyDictionary<AnchorKey, AnchorActivationCriticality>? criticality = null)
    {
        ArgumentNullException.ThrowIfNull(activationOrder);
        bool seenCritical = false;
        foreach (AnchorKey key in activationOrder)
        {
            AnchorActivationCriticality c = Resolve(key, criticality);
            if (c == AnchorActivationCriticality.ManagementCritical)
            {
                seenCritical = true;
            }
            else if (seenCritical)
            {
                return false;
            }
        }

        return true;
    }

    private static AnchorActivationCriticality Resolve(
        AnchorKey key,
        IReadOnlyDictionary<AnchorKey, AnchorActivationCriticality>? criticality)
    {
        if (criticality is not null && criticality.TryGetValue(key, out AnchorActivationCriticality mapped))
        {
            return mapped;
        }

        return DefaultCriticality(key);
    }

    private static int ChainRank(FilterBuiltInContext chain)
        => chain switch
        {
            FilterBuiltInContext.Forward => 0,
            FilterBuiltInContext.Output => 1,
            FilterBuiltInContext.Input => 2,
            _ => throw new DomainInvariantException($"Unsupported activation chain '{chain}'."),
        };
}
