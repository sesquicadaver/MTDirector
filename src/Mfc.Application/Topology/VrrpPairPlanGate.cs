using Mfc.Application.Common;
using Mfc.Domain.Inventory;
using Mfc.Domain.Topology;

namespace Mfc.Application.Topology;

/// <summary>Shared VRRP pair gate for Onboarding/Deploy CreatePlan (W6-02).</summary>
public static class VrrpPairPlanGate
{
    /// <summary>
    /// When true, missing captures / empty VRRP sections do not block CreatePlan
    /// (onboarding often runs before first capture). Deploy stays strict.
    /// </summary>
    public static async Task<ApplicationError?> BlockIfFailedAsync(
        VrrpPairConsistencyLoader loader,
        Node node,
        bool allowIncompleteCaptures = false,
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
        IEnumerable<VrrpPairConsistencyFinding> blockers = result.Findings
            .Where(static f => f.Severity == VrrpPairFindingSeverity.Blocker);
        if (allowIncompleteCaptures)
        {
            blockers = blockers.Where(static f =>
                f.Code is not (
                    VrrpPairConsistencyFinding.MissingCapture
                    or VrrpPairConsistencyFinding.NoVrrpGroups
                    or VrrpPairConsistencyFinding.InsufficientMembers));
        }

        VrrpPairConsistencyFinding[] remaining = blockers.ToArray();
        if (remaining.Length == 0)
        {
            return null;
        }

        string summary = string.Join(
            "; ",
            remaining.Take(5).Select(static f => f.Code + ": " + f.Message));
        return ApplicationError.Conflict(
            "VRRP pair consistency blockers prevent CreatePlan. " + summary);
    }
}
