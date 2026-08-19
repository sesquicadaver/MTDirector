namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Immutable deployment plan header (Safe Deployment Spec §9 / M4-01).</summary>
public sealed class DeploymentPlanEntity
{
    public Guid Id { get; set; }

    public Guid NodeId { get; set; }

    public required byte[] LogicalPolicyHash { get; set; }

    public required byte[] AnalysisBundleHash { get; set; }

    public required byte[] TopologyProjectionHash { get; set; }

    public required string ActivationOrderJson { get; set; }

    public required string RollbackOrderJson { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public required byte[] PlanHash { get; set; }

    public List<DeploymentDevicePlanEntity> DevicePlans { get; set; } = [];
}
