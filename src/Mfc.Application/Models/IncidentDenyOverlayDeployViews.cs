using Mfc.Application.Deployment;
using Mfc.Application.Policies;

namespace Mfc.Application.Models;

/// <summary>One Node incident overlay compile + deploy orchestration result (M7.4-03).</summary>
public sealed class DeployIncidentDenyOverlayView
{
    public required Guid NodeId { get; init; }

    public required Guid OverlayPolicyId { get; init; }

    public required byte[] LogicalEffectivePolicyHash { get; init; }

    public required string LogicalEffectivePolicyHashHex { get; init; }

    public required IReadOnlyList<FilterArtifactSummaryView> Artifacts { get; init; }

    public required Guid PlanId { get; init; }

    public required byte[] PlanHash { get; init; }

    public required Guid OperationId { get; init; }

    public required string DeploymentState { get; init; }

    public static DeployIncidentDenyOverlayView FromParts(
        Guid overlayPolicyId,
        CompileNodeFilterArtifactsView compile,
        DeploymentPlanSummaryView plan,
        DeploymentOperationSummaryView operation)
        => new()
        {
            NodeId = compile.NodeId,
            OverlayPolicyId = overlayPolicyId,
            LogicalEffectivePolicyHash = compile.LogicalEffectivePolicyHash,
            LogicalEffectivePolicyHashHex = compile.LogicalEffectivePolicyHashHex,
            Artifacts = compile.Artifacts,
            PlanId = plan.PlanId,
            PlanHash = plan.PlanHash,
            OperationId = operation.OperationId,
            DeploymentState = operation.State.ToString(),
        };
}
