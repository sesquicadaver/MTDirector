using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>Fully immutable onboarding plan (Onboarding Spec §25–§26). Default lifetime 30 minutes.</summary>
public sealed class OnboardingPlan
{
    private OnboardingPlan(
        OnboardingPlanId id,
        NodeId nodeId,
        Hash256 nodeMembershipHash,
        Hash256 topologyProjectionHash,
        IReadOnlyList<DeviceOnboardingPlan> devicePlans,
        UserId createdBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        Hash256 planHash)
    {
        Id = id;
        NodeId = nodeId;
        NodeMembershipHash = nodeMembershipHash;
        TopologyProjectionHash = topologyProjectionHash;
        DevicePlans = devicePlans;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        PlanHash = planHash;
    }

    public OnboardingPlanId Id { get; }

    public NodeId NodeId { get; }

    public Hash256 NodeMembershipHash { get; }

    public Hash256 TopologyProjectionHash { get; }

    public IReadOnlyList<DeviceOnboardingPlan> DevicePlans { get; }

    public UserId CreatedBy { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public Hash256 PlanHash { get; }

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc.ToUniversalTime() >= ExpiresAtUtc;

    /// <summary>Creates an immutable plan and computes <see cref="PlanHash"/>.</summary>
    public static OnboardingPlan Create(
        Node node,
        Hash256 nodeMembershipHash,
        Hash256 topologyProjectionHash,
        IReadOnlyList<DeviceOnboardingPlan> devicePlans,
        UserId createdBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(nodeMembershipHash);
        ArgumentNullException.ThrowIfNull(topologyProjectionHash);
        ArgumentNullException.ThrowIfNull(devicePlans);
        DateTimeOffset created = createdAtUtc.ToUniversalTime();
        DateTimeOffset expires = (expiresAtUtc ?? created + OnboardingCodes.DefaultPlanLifetime).ToUniversalTime();
        if (expires <= created)
        {
            throw new DomainInvariantException("expires_at must be greater than created_at.");
        }

        DeviceOnboardingPlan[] ordered = NormalizeAndValidateDevicePlans(node, devicePlans);
        Hash256 planHash = OnboardingPlanHasher.Hash(
            node.Id,
            nodeMembershipHash,
            topologyProjectionHash,
            ordered,
            createdBy,
            created,
            expires);
        return new OnboardingPlan(
            OnboardingPlanId.New(),
            node.Id,
            nodeMembershipHash,
            topologyProjectionHash,
            ordered,
            createdBy,
            created,
            expires,
            planHash);
    }

    /// <summary>Rebuilds a plan from persistence and verifies <paramref name="planHash"/>.</summary>
    public static OnboardingPlan Reconstitute(
        OnboardingPlanId id,
        NodeId nodeId,
        Hash256 nodeMembershipHash,
        Hash256 topologyProjectionHash,
        IReadOnlyList<DeviceOnboardingPlan> devicePlans,
        UserId createdBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        Hash256 planHash)
    {
        ArgumentNullException.ThrowIfNull(nodeMembershipHash);
        ArgumentNullException.ThrowIfNull(topologyProjectionHash);
        ArgumentNullException.ThrowIfNull(devicePlans);
        ArgumentNullException.ThrowIfNull(planHash);
        DateTimeOffset created = createdAtUtc.ToUniversalTime();
        DateTimeOffset expires = expiresAtUtc.ToUniversalTime();
        if (expires <= created)
        {
            throw new DomainInvariantException("expires_at must be greater than created_at.");
        }

        DeviceOnboardingPlan[] ordered = devicePlans
            .OrderBy(static p => p.DeviceId.Value)
            .ToArray();
        if (ordered.Select(static p => p.DeviceId.Value).Distinct().Count() != ordered.Length)
        {
            throw new DomainInvariantException("device_plans must have unique device_id values.");
        }

        Hash256 expected = OnboardingPlanHasher.Hash(
            nodeId,
            nodeMembershipHash,
            topologyProjectionHash,
            ordered,
            createdBy,
            created,
            expires);
        if (!expected.Equals(planHash))
        {
            throw new DomainInvariantException($"{OnboardingCodes.PlanHashMismatch}: stored plan_hash does not match content.");
        }

        return new OnboardingPlan(
            id,
            nodeId,
            nodeMembershipHash,
            topologyProjectionHash,
            ordered,
            createdBy,
            created,
            expires,
            planHash);
    }

    private static DeviceOnboardingPlan[] NormalizeAndValidateDevicePlans(
        Node node,
        IReadOnlyList<DeviceOnboardingPlan> devicePlans)
    {
        if (devicePlans.Count == 0)
        {
            throw new DomainInvariantException($"{OnboardingCodes.DevicePlanCardinality}: device_plans must be non-empty.");
        }

        List<Device> enabled = node.Devices.Where(static d => d.Enabled).OrderBy(static d => d.Id.Value).ToList();
        if (enabled.Count == 0)
        {
            throw new DomainInvariantException($"{OnboardingCodes.DevicePlanCardinality}: Node has no enabled Devices.");
        }

        switch (node.DeclaredKind)
        {
            case NodeKind.Router:
            case NodeKind.Switch:
                if (enabled.Count != 1 || devicePlans.Count != 1)
                {
                    throw new DomainInvariantException(
                        $"{OnboardingCodes.DevicePlanCardinality}: {node.DeclaredKind} requires exactly one device plan.");
                }

                break;
            case NodeKind.Vrrp:
                if (devicePlans.Count != enabled.Count)
                {
                    throw new DomainInvariantException(
                        $"{OnboardingCodes.DevicePlanCardinality}: VRRP onboarding must cover every Node member.");
                }

                break;
            default:
                throw new DomainInvariantException($"Unsupported NodeKind '{node.DeclaredKind}'.");
        }

        HashSet<Guid> enabledIds = enabled.Select(static d => d.Id.Value).ToHashSet();
        HashSet<Guid> planIds = [];
        foreach (DeviceOnboardingPlan plan in devicePlans)
        {
            ArgumentNullException.ThrowIfNull(plan);
            if (!enabledIds.Contains(plan.DeviceId.Value))
            {
                throw new DomainInvariantException(
                    $"{OnboardingCodes.DevicePlanCardinality}: device '{plan.DeviceId}' is not an enabled member.");
            }

            if (!planIds.Add(plan.DeviceId.Value))
            {
                throw new DomainInvariantException("device_plans must have unique device_id values.");
            }

            if (node.DeclaredKind == NodeKind.Switch
                && plan.RequiredAnchorSet.Any(static a => a.Chain == FilterBuiltInContext.Forward))
            {
                throw new DomainInvariantException("Switch onboarding must not require FORWARD anchors.");
            }
        }

        if (planIds.Count != enabledIds.Count)
        {
            throw new DomainInvariantException(
                $"{OnboardingCodes.DevicePlanCardinality}: device_plans must cover every Node member.");
        }

        return devicePlans.OrderBy(static p => p.DeviceId.Value).ToArray();
    }
}
