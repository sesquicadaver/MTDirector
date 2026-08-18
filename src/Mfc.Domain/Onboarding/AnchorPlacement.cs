using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>Immutable planned anchor placement (Onboarding Spec §20).</summary>
public sealed class AnchorPlacement
{
    private AnchorPlacement(
        IpAddressFamily family,
        FilterBuiltInContext chain,
        AnchorPlacementMode mode,
        uint expectedAnchorOrdinal,
        Hash256? referenceRuleFingerprint,
        uint? referenceOccurrenceRank,
        Hash256? expectedPredecessorFingerprint,
        Hash256? expectedSuccessorFingerprint)
    {
        Family = family;
        Chain = chain;
        Mode = mode;
        ExpectedAnchorOrdinal = expectedAnchorOrdinal;
        ReferenceRuleFingerprint = referenceRuleFingerprint;
        ReferenceOccurrenceRank = referenceOccurrenceRank;
        ExpectedPredecessorFingerprint = expectedPredecessorFingerprint;
        ExpectedSuccessorFingerprint = expectedSuccessorFingerprint;
    }

    public IpAddressFamily Family { get; }

    public FilterBuiltInContext Chain { get; }

    public AnchorPlacementMode Mode { get; }

    public Hash256? ReferenceRuleFingerprint { get; }

    public uint? ReferenceOccurrenceRank { get; }

    public Hash256? ExpectedPredecessorFingerprint { get; }

    public Hash256? ExpectedSuccessorFingerprint { get; }

    public uint ExpectedAnchorOrdinal { get; }

    public AnchorKey Key => new(Family, Chain);

    public static AnchorPlacement Create(
        IpAddressFamily family,
        FilterBuiltInContext chain,
        AnchorPlacementMode mode,
        uint expectedAnchorOrdinal,
        Hash256? referenceRuleFingerprint = null,
        uint? referenceOccurrenceRank = null,
        Hash256? expectedPredecessorFingerprint = null,
        Hash256? expectedSuccessorFingerprint = null)
    {
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

        return new AnchorPlacement(
            family,
            chain,
            mode,
            expectedAnchorOrdinal,
            referenceRuleFingerprint,
            referenceOccurrenceRank,
            expectedPredecessorFingerprint,
            expectedSuccessorFingerprint);
    }

    public static AnchorPlacement Reconstitute(
        IpAddressFamily family,
        FilterBuiltInContext chain,
        AnchorPlacementMode mode,
        uint expectedAnchorOrdinal,
        Hash256? referenceRuleFingerprint,
        uint? referenceOccurrenceRank,
        Hash256? expectedPredecessorFingerprint,
        Hash256? expectedSuccessorFingerprint)
        => Create(
            family,
            chain,
            mode,
            expectedAnchorOrdinal,
            referenceRuleFingerprint,
            referenceOccurrenceRank,
            expectedPredecessorFingerprint,
            expectedSuccessorFingerprint);
}
