namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Immutable onboarding plan header (Onboarding Spec §25 / M5-01).</summary>
public sealed class OnboardingPlanEntity
{
    public Guid Id { get; set; }

    public Guid NodeId { get; set; }

    public required byte[] NodeMembershipHash { get; set; }

    public required byte[] TopologyProjectionHash { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public required byte[] PlanHash { get; set; }

    public List<OnboardingDevicePlanEntity> DevicePlans { get; set; } = [];
}
