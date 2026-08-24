using Mfc.Application.Deployment;
using Mfc.Application.Policies;

namespace Mfc.Application.Models;

/// <summary>Mandatory TTL removal plan without deployment start (M7.4-04).</summary>
public sealed class PlanIncidentDenyOverlayRemovalView
{
    public required Guid NodeId { get; init; }

    public required Guid OverlayPolicyId { get; init; }

    public required byte[] LogicalEffectivePolicyHash { get; init; }

    public required string LogicalEffectivePolicyHashHex { get; init; }

    public required IReadOnlyList<FilterArtifactSummaryView> Artifacts { get; init; }

    public required Guid PlanId { get; init; }

    public required byte[] PlanHash { get; init; }

    public static PlanIncidentDenyOverlayRemovalView FromParts(
        Guid overlayPolicyId,
        CompileNodeFilterArtifactsView compile,
        DeploymentPlanSummaryView plan)
        => new()
        {
            NodeId = compile.NodeId,
            OverlayPolicyId = overlayPolicyId,
            LogicalEffectivePolicyHash = compile.LogicalEffectivePolicyHash,
            LogicalEffectivePolicyHashHex = compile.LogicalEffectivePolicyHashHex,
            Artifacts = compile.Artifacts,
            PlanId = plan.PlanId,
            PlanHash = plan.PlanHash,
        };
}
