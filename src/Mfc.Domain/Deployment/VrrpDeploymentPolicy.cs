using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Deployment;

/// <summary>Deployment classification for a VRRP member (Safe Deployment Spec §38).</summary>
public enum VrrpDeploymentMemberClass : byte
{
    StandbyOnly = 0,
    TrafficBearing = 1,
}

/// <summary>One observed VRRP instance role on a member (family+VRID scoped).</summary>
public sealed record VrrpInstanceRoleFact
{
    public required IpAddressFamily Family { get; init; }

    public required byte Vrid { get; init; }

    public required VrrpMemberObservedState ObservedState { get; init; }
}

/// <summary>Per-member role snapshot used to build the deployment RoleVector.</summary>
public sealed record VrrpMemberRoleSnapshot
{
    public required DeviceId DeviceId { get; init; }

    public required IReadOnlyList<VrrpInstanceRoleFact> Instances { get; init; }

    /// <summary>True when the member has proven independent routed traffic (blocks STANDBY_ONLY).</summary>
    public required bool HasIndependentRoutedTraffic { get; init; }

    /// <summary>False blocks deployment before activation (AC#9).</summary>
    public required bool Reachable { get; init; }
}

/// <summary>Cluster RoleVector for VRRP deployment ordering and change detection.</summary>
public sealed record VrrpRoleVector
{
    public required IReadOnlyList<VrrpMemberRoleSnapshot> Members { get; init; }

    public static string CanonicalFingerprint(VrrpRoleVector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        IEnumerable<string> parts = vector.Members
            .OrderBy(static m => m.DeviceId.Value)
            .Select(static m =>
            {
                string instances = string.Join(
                    ',',
                    m.Instances
                        .OrderBy(static i => i.Family)
                        .ThenBy(static i => i.Vrid)
                        .Select(static i => $"{(int)i.Family}:{i.Vrid}:{(int)i.ObservedState}"));
                return $"{m.DeviceId.Value:D}|{m.HasIndependentRoutedTraffic}|{instances}";
            });
        return string.Join(';', parts);
    }
}

/// <summary>Outcome of classifying and ordering VRRP members for activation.</summary>
public sealed class VrrpActivationPlan
{
    public required IReadOnlyList<(DeviceId DeviceId, VrrpDeploymentMemberClass Class, int MasterCount)> OrderedMembers
    {
        get;
        init;
    }

    public required IReadOnlyList<string> Findings { get; init; }

    public bool HasBlockers => Findings.Count > 0;
}

/// <summary>
/// VRRP member classification and activation order (Safe Deployment Spec §38–§39 / M4-10).
/// </summary>
public static class VrrpMemberClassifier
{
    /// <summary>
    /// STANDBY_ONLY: not MASTER for any relevant instance and no independent routed traffic.
    /// Unknown observed state → TRAFFIC_BEARING (AC#6). All other cases → TRAFFIC_BEARING.
    /// </summary>
    public static VrrpDeploymentMemberClass Classify(VrrpMemberRoleSnapshot member)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(member.Instances);
        if (member.Instances.Any(static i => i.ObservedState == VrrpMemberObservedState.Unknown))
        {
            return VrrpDeploymentMemberClass.TrafficBearing;
        }

        bool anyMaster = member.Instances.Any(static i => i.ObservedState == VrrpMemberObservedState.Master);
        if (!anyMaster && !member.HasIndependentRoutedTraffic)
        {
            return VrrpDeploymentMemberClass.StandbyOnly;
        }

        return VrrpDeploymentMemberClass.TrafficBearing;
    }

    public static int CountMasterInstances(VrrpMemberRoleSnapshot member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return member.Instances.Count(static i => i.ObservedState == VrrpMemberObservedState.Master);
    }
}

/// <summary>Plans STANDBY_ONLY first, then TRAFFIC_BEARING by master-count ascending, DeviceId (Spec §39).</summary>
public static class VrrpActivationOrderPlanner
{
    public static VrrpActivationPlan Plan(VrrpRoleVector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentNullException.ThrowIfNull(vector.Members);
        List<(DeviceId DeviceId, VrrpDeploymentMemberClass Class, int MasterCount)> classified = [];
        foreach (VrrpMemberRoleSnapshot member in vector.Members)
        {
            VrrpDeploymentMemberClass cls = VrrpMemberClassifier.Classify(member);
            classified.Add((member.DeviceId, cls, VrrpMemberClassifier.CountMasterInstances(member)));
        }

        List<(DeviceId DeviceId, VrrpDeploymentMemberClass Class, int MasterCount)> ordered = classified
            .OrderBy(static m => m.Class == VrrpDeploymentMemberClass.StandbyOnly ? 0 : 1)
            .ThenBy(static m => m.Class == VrrpDeploymentMemberClass.TrafficBearing ? m.MasterCount : 0)
            .ThenBy(static m => m.DeviceId.Value)
            .ToList();

        return new VrrpActivationPlan
        {
            OrderedMembers = ordered,
            Findings = [],
        };
    }
}

/// <summary>VRRP deployment policy gates (Safe Deployment Spec §37–§42 / M4-10).</summary>
public static class VrrpDeploymentPolicy
{
    public static void EnsureEligible(Node node, DeploymentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        if (node.Id != plan.NodeId)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.DevicePlanCardinality}: plan.node_id must match the target Node.");
        }

        if (node.DeclaredKind != NodeKind.Vrrp)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.VrrpNodeRequired}: VRRP coordinator requires a VRRP Node.");
        }

        if (node.Devices.Count < 2 || plan.DevicePlans.Count != node.Devices.Count)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.DevicePlanCardinality}: VRRP deployment must cover every Node member.");
        }

        HashSet<Guid> planDevices = plan.DevicePlans.Select(static p => p.DeviceId.Value).ToHashSet();
        foreach (Device device in node.Devices)
        {
            if (!planDevices.Contains(device.Id.Value))
            {
                throw new DomainInvariantException(
                    $"{DeploymentCodes.DevicePlanCardinality}: missing device plan for '{device.Id.Value:D}'.");
            }
        }
    }

    /// <summary>Block deployment when any member is unreachable before activation (AC#9).</summary>
    public static void EnsureAllMembersReachable(VrrpRoleVector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        foreach (VrrpMemberRoleSnapshot member in vector.Members)
        {
            if (!member.Reachable)
            {
                throw new DomainInvariantException(
                    $"{DeploymentCodes.VrrpMemberUnreachable}: member '{member.DeviceId.Value:D}' is unreachable before activation.");
            }
        }
    }

    /// <summary>
    /// Split-master must not be collapsed into a global master role (AC#12 / Spec §42).
    /// Returns true when any (family,vrid) has more than one Master.
    /// </summary>
    public static bool HasSplitMaster(VrrpRoleVector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        Dictionary<(IpAddressFamily Family, byte Vrid), int> masters = [];
        foreach (VrrpMemberRoleSnapshot member in vector.Members)
        {
            foreach (VrrpInstanceRoleFact instance in member.Instances)
            {
                if (instance.ObservedState != VrrpMemberObservedState.Master)
                {
                    continue;
                }

                (IpAddressFamily Family, byte Vrid) key = (instance.Family, instance.Vrid);
                masters[key] = masters.GetValueOrDefault(key) + 1;
            }
        }

        return masters.Values.Any(static c => c > 1);
    }

    public static void EnsureNoSplitMasterSimplification(VrrpRoleVector vector)
    {
        if (HasSplitMaster(vector))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.VrrpSplitMaster}: split-master must not be simplified; deployment is blocked.");
        }
    }

    /// <summary>Any RoleVector change after the first activation forces rollback (AC#8 / Spec §40).</summary>
    public static bool RoleVectorChanged(VrrpRoleVector before, VrrpRoleVector after)
        => !string.Equals(
            VrrpRoleVector.CanonicalFingerprint(before),
            VrrpRoleVector.CanonicalFingerprint(after),
            StringComparison.Ordinal);

    /// <summary>Partial Node commit is impossible — all members must be verified (AC#13).</summary>
    public static void EnsureFullCommitAllowed(IReadOnlyCollection<DeviceId> plannedMembers, IReadOnlySet<DeviceId> verifiedMembers)
    {
        ArgumentNullException.ThrowIfNull(plannedMembers);
        ArgumentNullException.ThrowIfNull(verifiedMembers);
        if (plannedMembers.Count == 0 || verifiedMembers.Count != plannedMembers.Count
            || plannedMembers.Any(id => !verifiedMembers.Contains(id)))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.VrrpPartialCommitForbidden}: partial VRRP commit is not allowed.");
        }
    }

    /// <summary>
    /// After partial activation failure: rollback reachable activated members;
    /// unreachable activated members retain watchdog (AC#10 / AC#11).
    /// </summary>
    public static (IReadOnlyList<DeviceId> RollbackTargets, IReadOnlyList<DeviceId> RetainWatchdog)
        PlanPartialFailureActions(
            IReadOnlyList<DeviceId> activatedInOrder,
            IReadOnlySet<DeviceId> currentlyReachable)
    {
        ArgumentNullException.ThrowIfNull(activatedInOrder);
        ArgumentNullException.ThrowIfNull(currentlyReachable);
        List<DeviceId> rollback = [];
        List<DeviceId> retain = [];
        foreach (DeviceId id in activatedInOrder.Reverse())
        {
            if (currentlyReachable.Contains(id))
            {
                rollback.Add(id);
            }
            else
            {
                retain.Add(id);
            }
        }

        return (rollback, retain);
    }
}
