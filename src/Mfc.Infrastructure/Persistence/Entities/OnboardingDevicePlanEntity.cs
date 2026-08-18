namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Immutable per-device onboarding plan (Onboarding Spec §25 / M5-01).</summary>
public sealed class OnboardingDevicePlanEntity
{
    public Guid Id { get; set; }

    public Guid PlanId { get; set; }

    public Guid DeviceId { get; set; }

    public required string ExpectedRouterOsVersion { get; set; }

    public required byte[] ExpectedCapabilityHash { get; set; }

    public required byte[] ExpectedConfigurationHash { get; set; }

    public required byte[] ExpectedCompatibilityHash { get; set; }

    public required byte[] ExpectedApiServiceHash { get; set; }

    public required byte[] ExpectedReadAccountHash { get; set; }

    public required byte[] ExpectedDeploymentAccountHash { get; set; }

    public required byte[] ExpectedDeviceModeHash { get; set; }

    public required byte[] ExpectedGuardHash { get; set; }

    public required string RequiredAnchorSetJson { get; set; }

    public required byte[] BootstrapArtifactHash { get; set; }

    public int WatchdogTtlSeconds { get; set; }

    public OnboardingPlanEntity? Plan { get; set; }

    public List<OnboardingAnchorPlacementEntity> Placements { get; set; } = [];
}
