using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Operator-chosen insertion intent (Onboarding Spec §20–§21). Never inferred by the controller.
/// </summary>
public sealed class AnchorPlacementIntent
{
    private AnchorPlacementIntent(
        IpAddressFamily family,
        FilterBuiltInContext chain,
        AnchorPlacementMode mode,
        Hash256? referenceRuleFingerprint,
        uint? referenceOccurrenceRank)
    {
        Family = family;
        Chain = chain;
        Mode = mode;
        ReferenceRuleFingerprint = referenceRuleFingerprint;
        ReferenceOccurrenceRank = referenceOccurrenceRank;
    }

    public IpAddressFamily Family { get; }

    public FilterBuiltInContext Chain { get; }

    public AnchorPlacementMode Mode { get; }

    public Hash256? ReferenceRuleFingerprint { get; }

    public uint? ReferenceOccurrenceRank { get; }

    public static AnchorPlacementIntent Append(IpAddressFamily family, FilterBuiltInContext chain)
        => Create(family, chain, AnchorPlacementMode.Append);

    public static AnchorPlacementIntent BeforeStaticRule(
        IpAddressFamily family,
        FilterBuiltInContext chain,
        Hash256 referenceRuleFingerprint,
        uint referenceOccurrenceRank)
        => Create(
            family,
            chain,
            AnchorPlacementMode.BeforeStaticRule,
            referenceRuleFingerprint,
            referenceOccurrenceRank);

    public static AnchorPlacementIntent Create(
        IpAddressFamily family,
        FilterBuiltInContext chain,
        AnchorPlacementMode mode,
        Hash256? referenceRuleFingerprint = null,
        uint? referenceOccurrenceRank = null)
    {
        if (family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported placement family '{family}'.");
        }

        if (chain is not (FilterBuiltInContext.Input or FilterBuiltInContext.Forward or FilterBuiltInContext.Output))
        {
            throw new DomainInvariantException($"Unsupported placement chain '{chain}'.");
        }

        if (mode == AnchorPlacementMode.Append)
        {
            if (referenceRuleFingerprint is not null || referenceOccurrenceRank is not null)
            {
                throw new DomainInvariantException("APPEND placement must not set a reference rule fingerprint or rank.");
            }
        }
        else if (mode == AnchorPlacementMode.BeforeStaticRule)
        {
            if (referenceRuleFingerprint is null || referenceOccurrenceRank is null)
            {
                throw new DomainInvariantException(
                    "BEFORE_STATIC_RULE placement requires reference fingerprint and occurrence rank.");
            }
        }
        else
        {
            throw new DomainInvariantException($"Unsupported placement mode '{mode}'.");
        }

        return new AnchorPlacementIntent(family, chain, mode, referenceRuleFingerprint, referenceOccurrenceRank);
    }
}
