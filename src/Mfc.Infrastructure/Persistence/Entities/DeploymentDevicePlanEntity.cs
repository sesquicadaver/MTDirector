namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Immutable per-device deployment plan (Safe Deployment Spec §9 / M4-01).</summary>
public sealed class DeploymentDevicePlanEntity
{
    public Guid Id { get; set; }

    public Guid PlanId { get; set; }

    public Guid DeviceId { get; set; }

    public required string ExpectedRouterOsVersion { get; set; }

    public required byte[] ExpectedCapabilityHash { get; set; }

    public required byte[] ExpectedConfigurationHash { get; set; }

    public required byte[] ExpectedCompatibilityHash { get; set; }

    public required byte[] ExpectedGuardContextHash { get; set; }

    public required byte[] ExpectedAnchorContextHash { get; set; }

    public required byte[] OldArtifactHash { get; set; }

    public required byte[] NewArtifactHash { get; set; }

    public required string OldAnchorTargetsJson { get; set; }

    public required string NewAnchorTargetsJson { get; set; }

    public required string AnchorActivationOrderJson { get; set; }

    public required string AnchorRollbackOrderJson { get; set; }

    public required string TransitionStateHashesJson { get; set; }

    public int RollbackTtlSeconds { get; set; }

    public required string ProbesJson { get; set; }

    public DeploymentPlanEntity? Plan { get; set; }
}
