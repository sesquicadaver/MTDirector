namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Immutable anchor placement row (Onboarding Spec §20 / M5-01).</summary>
public sealed class OnboardingAnchorPlacementEntity
{
    public Guid Id { get; set; }

    public Guid DevicePlanId { get; set; }

    public short Family { get; set; }

    public short Chain { get; set; }

    public short Mode { get; set; }

    public byte[]? ReferenceRuleFingerprint { get; set; }

    public long? ReferenceOccurrenceRank { get; set; }

    public byte[]? ExpectedPredecessorFingerprint { get; set; }

    public byte[]? ExpectedSuccessorFingerprint { get; set; }

    public long ExpectedAnchorOrdinal { get; set; }

    public OnboardingDevicePlanEntity? DevicePlan { get; set; }
}
