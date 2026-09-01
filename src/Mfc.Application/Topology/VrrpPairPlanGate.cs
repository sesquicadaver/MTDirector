using Mfc.Application.Common;
using Mfc.Domain.Inventory;
using Mfc.Domain.Topology;

namespace Mfc.Application.Topology;

/// <summary>Shared VRRP pair gate for Onboarding/Deploy CreatePlan (W6-02).</summary>
public static class VrrpPairPlanGate
{
    public static async Task<ApplicationError?> BlockIfFailedAsync(
        VrrpPairConsistencyLoader loader,
        Node node,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(node);
        if (node.DeclaredKind != NodeKind.Vrrp)
        {
            return null;
        }

        VrrpPairConsistencyResult result = await loader
            .AnalyzeNodeAsync(node, cancellationToken)
            .ConfigureAwait(false);
        if (result.Passed)
        {
            return null;
        }

        string summary = string.Join(
            "; ",
            result.Findings
                .Where(static f => f.Severity == VrrpPairFindingSeverity.Blocker)
                .Take(5)
                .Select(static f => f.Code + ": " + f.Message));
        return ApplicationError.Conflict(
            "VRRP pair consistency blockers prevent CreatePlan. " + summary);
    }
}
