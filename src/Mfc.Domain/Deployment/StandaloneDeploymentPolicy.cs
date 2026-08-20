using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>
/// Standalone Node deployment policy gates (Safe Deployment Spec §35 / M4-08).
/// VRRP and multi-member topologies are out of scope for this coordinator.
/// </summary>
public static class StandaloneDeploymentPolicy
{
    /// <summary>True when old and new artifact hashes are identical — no writes allowed (AC#2 / AC#10).</summary>
    public static bool IsNoChanges(DeviceDeploymentPlan devicePlan)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        return devicePlan.OldArtifactHash.Equals(devicePlan.NewArtifactHash);
    }

    /// <summary>
    /// Standalone coordinator accepts Router/Switch with exactly one device plan.
    /// VRRP is M4-10; multi-WAN verification extras are M4-09.
    /// </summary>
    public static void EnsureEligible(Node node, DeploymentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        if (node.Id != plan.NodeId)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.DevicePlanCardinality}: plan.node_id must match the target Node.");
        }

        if (node.DeclaredKind is not (NodeKind.Router or NodeKind.Switch))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.StandaloneNodeRequired}: standalone coordinator requires Router or Switch.");
        }

        if (plan.DevicePlans.Count != 1 || node.Devices.Count != 1)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.StandaloneNodeRequired}: standalone coordinator requires exactly one Device.");
        }

        if (plan.DevicePlans[0].DeviceId != node.Devices[0].Id)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.DevicePlanCardinality}: device plan must cover the Node's single Device.");
        }
    }

    /// <summary>Re-check start preconditions immediately before staging (AC#1).</summary>
    public static void RecheckPreconditions(
        Node node,
        DeploymentPlan plan,
        IReadOnlyList<DeploymentOperation> existingForNode,
        DateTimeOffset nowUtc,
        IReadOnlyList<PacketPathPairFact> packetPathPairs)
        => DeploymentOperationGate.EnsureCanStart(node, plan, existingForNode, nowUtc, packetPathPairs);
}
