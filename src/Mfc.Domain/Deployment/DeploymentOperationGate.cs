using Mfc.Domain.Drift;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>Pure start/transition gate (Issue Set M4-01 / N1-06 / M6-02). Campaign state is rejected by construction.</summary>
public static class DeploymentOperationGate
{
    public static void EnsureCanStart(
        Node node,
        DeploymentPlan plan,
        IReadOnlyList<DeploymentOperation> existingForNode,
        DateTimeOffset nowUtc,
        IReadOnlyList<PacketPathPairFact> packetPathPairs,
        bool hasBlockingCriticalDrift = false)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(existingForNode);
        ArgumentNullException.ThrowIfNull(packetPathPairs);
        if (node.Id != plan.NodeId)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.DevicePlanCardinality}: plan.node_id must match the target Node.");
        }

        if (node.Status == NodeStatus.Disabled)
        {
            throw new DomainInvariantException($"{DeploymentCodes.NodeDisabled}: Node is DISABLED.");
        }

        if (!DeploymentPlanHasher.Compute(plan).Equals(plan.PlanHash))
        {
            throw new DomainInvariantException($"{DeploymentCodes.PlanHashMismatch}: plan_hash mismatch.");
        }

        if (plan.IsExpired(nowUtc))
        {
            throw new DomainInvariantException($"{DeploymentCodes.PlanExpired}: plan lifetime elapsed.");
        }

        if (existingForNode.Any(o => o.NodeId == plan.NodeId && o.IsNonterminal))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.NonterminalExists}: only one nonterminal deployment per Node.");
        }

        if (hasBlockingCriticalDrift)
        {
            throw new DomainInvariantException(
                $"{DriftCodes.CriticalDriftBlocksDeploy}: Critical configuration drift blocks new deployment; restore via normal deployment only.");
        }

        DeploymentPacketPathGate.EnsureCleared(node.DeclaredKind, packetPathPairs);
    }
}
