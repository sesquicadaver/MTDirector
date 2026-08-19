using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Deployment;

/// <summary>Fully immutable Node deployment plan (Safe Deployment Spec §9–§10). No campaign fields.</summary>
public sealed class DeploymentPlan
{
    private DeploymentPlan(
        DeploymentPlanId id,
        NodeId nodeId,
        Hash256 logicalPolicyHash,
        Hash256 analysisBundleHash,
        Hash256 topologyProjectionHash,
        IReadOnlyList<DeviceDeploymentPlan> devicePlans,
        IReadOnlyList<DeviceId> activationOrder,
        IReadOnlyList<DeviceId> rollbackOrder,
        UserId createdBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        Hash256 planHash)
    {
        Id = id;
        NodeId = nodeId;
        LogicalPolicyHash = logicalPolicyHash;
        AnalysisBundleHash = analysisBundleHash;
        TopologyProjectionHash = topologyProjectionHash;
        DevicePlans = devicePlans;
        ActivationOrder = activationOrder;
        RollbackOrder = rollbackOrder;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        PlanHash = planHash;
    }

    public DeploymentPlanId Id { get; }

    public NodeId NodeId { get; }

    public Hash256 LogicalPolicyHash { get; }

    public Hash256 AnalysisBundleHash { get; }

    public Hash256 TopologyProjectionHash { get; }

    public IReadOnlyList<DeviceDeploymentPlan> DevicePlans { get; }

    public IReadOnlyList<DeviceId> ActivationOrder { get; }

    public IReadOnlyList<DeviceId> RollbackOrder { get; }

    public UserId CreatedBy { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public Hash256 PlanHash { get; }

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc.ToUniversalTime() >= ExpiresAtUtc;

    public static DeploymentPlan Create(
        Node node,
        Hash256 logicalPolicyHash,
        Hash256 analysisBundleHash,
        Hash256 topologyProjectionHash,
        IReadOnlyList<DeviceDeploymentPlan> devicePlans,
        UserId createdBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(logicalPolicyHash);
        ArgumentNullException.ThrowIfNull(analysisBundleHash);
        ArgumentNullException.ThrowIfNull(topologyProjectionHash);
        ArgumentNullException.ThrowIfNull(devicePlans);
        if (node.Status == NodeStatus.Disabled)
        {
            throw new DomainInvariantException($"{DeploymentCodes.NodeDisabled}: disabled Node cannot be planned.");
        }

        DateTimeOffset created = TruncateToUtcMicroseconds(createdAtUtc);
        DateTimeOffset expires = TruncateToUtcMicroseconds(expiresAtUtc ?? created + DeploymentCodes.DefaultPlanLifetime);
        if (expires <= created)
        {
            throw new DomainInvariantException("expires_at must be greater than created_at.");
        }

        (DeviceDeploymentPlan[] ordered, DeviceId[] activation, DeviceId[] rollback) =
            NormalizeAndValidate(node, devicePlans);
        Hash256 planHash = DeploymentPlanHasher.Hash(
            node.Id,
            logicalPolicyHash,
            analysisBundleHash,
            topologyProjectionHash,
            ordered,
            activation,
            rollback,
            createdBy,
            created,
            expires);
        return new DeploymentPlan(
            DeploymentPlanId.New(),
            node.Id,
            logicalPolicyHash,
            analysisBundleHash,
            topologyProjectionHash,
            ordered,
            activation,
            rollback,
            createdBy,
            created,
            expires,
            planHash);
    }

    public static DeploymentPlan Reconstitute(
        DeploymentPlanId id,
        NodeId nodeId,
        Hash256 logicalPolicyHash,
        Hash256 analysisBundleHash,
        Hash256 topologyProjectionHash,
        IReadOnlyList<DeviceDeploymentPlan> devicePlans,
        IReadOnlyList<DeviceId> activationOrder,
        IReadOnlyList<DeviceId> rollbackOrder,
        UserId createdBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        Hash256 planHash)
    {
        ArgumentNullException.ThrowIfNull(logicalPolicyHash);
        ArgumentNullException.ThrowIfNull(analysisBundleHash);
        ArgumentNullException.ThrowIfNull(topologyProjectionHash);
        ArgumentNullException.ThrowIfNull(devicePlans);
        ArgumentNullException.ThrowIfNull(activationOrder);
        ArgumentNullException.ThrowIfNull(rollbackOrder);
        ArgumentNullException.ThrowIfNull(planHash);
        DateTimeOffset created = TruncateToUtcMicroseconds(createdAtUtc);
        DateTimeOffset expires = TruncateToUtcMicroseconds(expiresAtUtc);
        if (expires <= created)
        {
            throw new DomainInvariantException("expires_at must be greater than created_at.");
        }

        DeviceDeploymentPlan[] ordered = devicePlans.OrderBy(static p => p.DeviceId.Value).ToArray();
        DeviceId[] activation = activationOrder.ToArray();
        DeviceId[] rollback = rollbackOrder.ToArray();
        Hash256 expected = DeploymentPlanHasher.Hash(
            nodeId,
            logicalPolicyHash,
            analysisBundleHash,
            topologyProjectionHash,
            ordered,
            activation,
            rollback,
            createdBy,
            created,
            expires);
        if (!expected.Equals(planHash))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.PlanHashMismatch}: stored plan_hash does not match content.");
        }

        return new DeploymentPlan(
            id,
            nodeId,
            logicalPolicyHash,
            analysisBundleHash,
            topologyProjectionHash,
            ordered,
            activation,
            rollback,
            createdBy,
            created,
            expires,
            planHash);
    }

    private static (DeviceDeploymentPlan[] Ordered, DeviceId[] Activation, DeviceId[] Rollback) NormalizeAndValidate(
        Node node,
        IReadOnlyList<DeviceDeploymentPlan> devicePlans)
    {
        if (devicePlans.Count == 0)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.DevicePlanCardinality}: device_plans must be non-empty.");
        }

        List<Device> enabled = node.Devices.Where(static d => d.Enabled).OrderBy(static d => d.Id.Value).ToList();
        if (enabled.Count == 0)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.DevicePlanCardinality}: Node has no enabled Devices.");
        }

        switch (node.DeclaredKind)
        {
            case NodeKind.Router:
            case NodeKind.Switch:
                if (enabled.Count != 1 || devicePlans.Count != 1)
                {
                    throw new DomainInvariantException(
                        $"{DeploymentCodes.DevicePlanCardinality}: {node.DeclaredKind} requires exactly one device plan.");
                }

                break;
            case NodeKind.Vrrp:
                if (devicePlans.Count != enabled.Count)
                {
                    throw new DomainInvariantException(
                        $"{DeploymentCodes.DevicePlanCardinality}: VRRP deployment must cover every Node member.");
                }

                break;
            default:
                throw new DomainInvariantException($"Unsupported NodeKind '{node.DeclaredKind}'.");
        }

        HashSet<Guid> enabledIds = enabled.Select(static d => d.Id.Value).ToHashSet();
        HashSet<Guid> planIds = [];
        foreach (DeviceDeploymentPlan plan in devicePlans)
        {
            ArgumentNullException.ThrowIfNull(plan);
            if (!enabledIds.Contains(plan.DeviceId.Value) || !planIds.Add(plan.DeviceId.Value))
            {
                throw new DomainInvariantException(
                    $"{DeploymentCodes.DevicePlanCardinality}: device_plans must match enabled members uniquely.");
            }
        }

        if (planIds.Count != enabledIds.Count)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.DevicePlanCardinality}: every enabled member must have a device plan.");
        }

        DeviceId[] activation = enabled.Select(static d => d.Id).ToArray();
        DeviceId[] rollback = activation.Reverse().ToArray();
        DeviceDeploymentPlan[] ordered = devicePlans.OrderBy(static p => p.DeviceId.Value).ToArray();
        return (ordered, activation, rollback);
    }

    private static DateTimeOffset TruncateToUtcMicroseconds(DateTimeOffset value)
    {
        DateTimeOffset utc = value.ToUniversalTime();
        long ticks = utc.UtcTicks - (utc.UtcTicks % TimeSpan.TicksPerMicrosecond);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }
}
