using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>Per-member runtime port for VRRP deployment coordination (M4-10).</summary>
public interface IVrrpMemberDeploymentRuntime
{
    DeviceId DeviceId { get; }

    Task<bool> IsReachableAsync(CancellationToken cancellationToken = default);

    Task<VrrpMemberRoleSnapshot> ReadRoleSnapshotAsync(CancellationToken cancellationToken = default);

    Task PrecheckAsync(CancellationToken cancellationToken = default);

    Task StageArtifactAsync(CancellationToken cancellationToken = default);

    Task ArmWatchdogAsync(CancellationToken cancellationToken = default);

    Task ActivateAsync(CancellationToken cancellationToken = default);

    Task VerifyAsync(CancellationToken cancellationToken = default);

    Task DisarmWatchdogAsync(CancellationToken cancellationToken = default);

    Task RollbackActivationAsync(CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="ExecuteVrrpDeploymentUseCase"/>.</summary>
public sealed class VrrpDeploymentResult
{
    public required bool Succeeded { get; init; }

    public required DeploymentOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }

    public required IReadOnlyList<DeviceId> ActivationOrderUsed { get; init; }

    public required IReadOnlyList<DeviceId> RolledBackMembers { get; init; }

    public required IReadOnlyList<DeviceId> WatchdogRetainedMembers { get; init; }

    public required bool PartialCommitAttempted { get; init; }
}

/// <summary>
/// Coordinates VRRP Node deployment as a recoverable pseudo-transaction
/// (Safe Deployment Spec §37–§42 / M4-10).
/// </summary>
public static class ExecuteVrrpDeploymentUseCase
{
    public static async Task<VrrpDeploymentResult> ExecuteAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperation operation,
        IReadOnlyList<IVrrpMemberDeploymentRuntime> members,
        IReadOnlyList<DeploymentOperation> existingForNode,
        IReadOnlyList<PacketPathPairFact> packetPathPairs,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(existingForNode);
        ArgumentNullException.ThrowIfNull(packetPathPairs);

        List<string> timeline = [];
        List<DeviceId> activated = [];
        List<DeviceId> rolledBack = [];
        List<DeviceId> retainedWatchdog = [];
        List<DeviceId> activationOrderUsed = [];
        Dictionary<DeviceId, IVrrpMemberDeploymentRuntime> byId = members.ToDictionary(static m => m.DeviceId);

        try
        {
            VrrpDeploymentPolicy.EnsureEligible(node, plan);
            if (operation.NodeId != node.Id || plan.NodeId != node.Id)
            {
                throw new DomainInvariantException("VRRP deployment node/plan/operation mismatch.");
            }

            if (members.Count != plan.DevicePlans.Count
                || plan.DevicePlans.Any(p => !byId.ContainsKey(p.DeviceId)))
            {
                throw new DomainInvariantException(
                    $"{DeploymentCodes.DevicePlanCardinality}: runtime ports must cover every device plan.");
            }

            DeploymentOperationGate.EnsureCanStart(node, plan, existingForNode, nowUtc, packetPathPairs);
            Advance(operation, DeploymentOperationState.Prechecking, nowUtc);
            timeline.Add("precheck:start");

            foreach (DeviceDeploymentPlan devicePlan in plan.DevicePlans.OrderBy(static p => p.DeviceId.Value))
            {
                await byId[devicePlan.DeviceId].PrecheckAsync(cancellationToken).ConfigureAwait(false);
                timeline.Add($"precheck:{devicePlan.DeviceId.Value:D}");
            }

            VrrpRoleVector precheckVector = await ReadVectorAsync(members, cancellationToken).ConfigureAwait(false);
            VrrpDeploymentPolicy.EnsureAllMembersReachable(precheckVector);
            VrrpDeploymentPolicy.EnsureNoSplitMasterSimplification(precheckVector);
            timeline.Add("precheck:all");

            Advance(operation, DeploymentOperationState.Staging, nowUtc);
            foreach (DeviceDeploymentPlan devicePlan in plan.DevicePlans.OrderBy(static p => p.DeviceId.Value))
            {
                await byId[devicePlan.DeviceId].StageArtifactAsync(cancellationToken).ConfigureAwait(false);
                timeline.Add($"stage:{devicePlan.DeviceId.Value:D}");
            }

            timeline.Add("stage:all");
            Advance(operation, DeploymentOperationState.Staged, nowUtc);

            Advance(operation, DeploymentOperationState.ArmingWatchdog, nowUtc);
            HashSet<DeviceId> armed = [];
            foreach (DeviceDeploymentPlan devicePlan in plan.DevicePlans.OrderBy(static p => p.DeviceId.Value))
            {
                await byId[devicePlan.DeviceId].ArmWatchdogAsync(cancellationToken).ConfigureAwait(false);
                armed.Add(devicePlan.DeviceId);
                timeline.Add($"watchdog-armed:{devicePlan.DeviceId.Value:D}");
            }

            DeploymentWatchdogPlanResult armedGate = PlanDeploymentWatchdogUseCase.EnsureAllDevicesArmed(
                plan.DevicePlans.Select(static p => p.DeviceId).ToArray(),
                armed);
            if (armedGate.HasBlockers)
            {
                string code = armedGate.Findings.Count > 0
                    ? armedGate.Findings[0].Code
                    : DeploymentCodes.WatchdogNotArmed;
                throw new DomainInvariantException($"{code}: not all VRRP watchdogs are armed.");
            }

            timeline.Add("watchdog:all-armed");
            Advance(operation, DeploymentOperationState.WatchdogArmed, nowUtc);
            Advance(operation, DeploymentOperationState.Activating, nowUtc);

            VrrpRoleVector lastVector = await ReadVectorAsync(members, cancellationToken).ConfigureAwait(false);
            VrrpDeploymentPolicy.EnsureAllMembersReachable(lastVector);
            VrrpDeploymentPolicy.EnsureNoSplitMasterSimplification(lastVector);
            List<DeviceId> queue = VrrpActivationOrderPlanner.Plan(lastVector).OrderedMembers
                .Select(static m => m.DeviceId)
                .ToList();
            activationOrderUsed = queue.ToList();
            timeline.Add("role-vector:initial");
            timeline.Add("activation-order:" + string.Join(',', queue.Select(static d => d.Value.ToString("D"))));

            bool firstActivated = false;
            int index = 0;
            while (index < queue.Count)
            {
                DeviceId deviceId = queue[index];
                VrrpRoleVector fresh = await ReadVectorAsync(members, cancellationToken).ConfigureAwait(false);
                timeline.Add($"role-vector:before:{deviceId.Value:D}");

                if (firstActivated && VrrpDeploymentPolicy.RoleVectorChanged(lastVector, fresh))
                {
                    timeline.Add("role-change:detected");
                    await RollbackActivatedAsync(
                        activated,
                        byId,
                        rolledBack,
                        retainedWatchdog,
                        timeline,
                        cancellationToken).ConfigureAwait(false);
                    FinishRollback(operation, nowUtc, DeploymentCodes.VrrpRoleChangedDuringDeployment);
                    return Fail(
                        operation.State,
                        DeploymentCodes.VrrpRoleChangedDuringDeployment,
                        timeline,
                        activationOrderUsed,
                        rolledBack,
                        retainedWatchdog);
                }

                if (!firstActivated && VrrpDeploymentPolicy.RoleVectorChanged(lastVector, fresh))
                {
                    VrrpDeploymentPolicy.EnsureAllMembersReachable(fresh);
                    VrrpDeploymentPolicy.EnsureNoSplitMasterSimplification(fresh);
                    queue = VrrpActivationOrderPlanner.Plan(fresh).OrderedMembers
                        .Select(static m => m.DeviceId)
                        .ToList();
                    activationOrderUsed = queue.ToList();
                    lastVector = fresh;
                    index = 0;
                    timeline.Add("activation-order:rebuilt");
                    continue;
                }

                if (!fresh.Members.Single(m => m.DeviceId.Equals(deviceId)).Reachable)
                {
                    timeline.Add($"unreachable:{deviceId.Value:D}");
                    if (!firstActivated)
                    {
                        throw new DomainInvariantException(
                            $"{DeploymentCodes.VrrpMemberUnreachable}: member '{deviceId.Value:D}' unreachable before activation.");
                    }

                    await RollbackActivatedAsync(
                        activated,
                        byId,
                        rolledBack,
                        retainedWatchdog,
                        timeline,
                        cancellationToken).ConfigureAwait(false);
                    FinishRollback(operation, nowUtc, DeploymentCodes.VrrpMemberUnreachable);
                    return Fail(
                        operation.State,
                        DeploymentCodes.VrrpMemberUnreachable,
                        timeline,
                        activationOrderUsed,
                        rolledBack,
                        retainedWatchdog);
                }

                lastVector = fresh;
                await byId[deviceId].ActivateAsync(cancellationToken).ConfigureAwait(false);
                activated.Add(deviceId);
                firstActivated = true;
                timeline.Add($"activate:{deviceId.Value:D}");
                await byId[deviceId].VerifyAsync(cancellationToken).ConfigureAwait(false);
                timeline.Add($"verify-member:{deviceId.Value:D}");
                index++;
            }

            timeline.Add("verify:node");
            VrrpDeploymentPolicy.EnsureFullCommitAllowed(
                plan.DevicePlans.Select(static p => p.DeviceId).ToArray(),
                activated.ToHashSet());

            Advance(operation, DeploymentOperationState.Verifying, nowUtc);
            Advance(operation, DeploymentOperationState.DisarmingWatchdog, nowUtc);
            foreach (DeviceId deviceId in activated.OrderBy(static d => d.Value))
            {
                await byId[deviceId].DisarmWatchdogAsync(cancellationToken).ConfigureAwait(false);
                timeline.Add($"watchdog-disarmed:{deviceId.Value:D}");
            }

            Advance(operation, DeploymentOperationState.Committed, nowUtc);
            timeline.Add("commit:all");
            return new VrrpDeploymentResult
            {
                Succeeded = true,
                State = operation.State,
                Timeline = timeline,
                ActivationOrderUsed = activationOrderUsed,
                RolledBackMembers = rolledBack,
                WatchdogRetainedMembers = retainedWatchdog,
                PartialCommitAttempted = false,
            };
        }
        catch (Exception ex) when (ex is DomainInvariantException or InvalidOperationException)
        {
            string code = ex is DomainInvariantException dix && dix.Message.Contains(':', StringComparison.Ordinal)
                ? dix.Message.Split(':', 2)[0]
                : DeploymentCodes.InvalidTransition;
            if (activated.Count > 0)
            {
                await RollbackActivatedAsync(
                    activated,
                    byId,
                    rolledBack,
                    retainedWatchdog,
                    timeline,
                    cancellationToken).ConfigureAwait(false);
                FinishRollback(operation, nowUtc, code);
            }
            else if (!operation.IsTerminal)
            {
                if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.Blocked))
                {
                    Advance(operation, DeploymentOperationState.Blocked, nowUtc, code);
                }
                else if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.Failed))
                {
                    Advance(operation, DeploymentOperationState.Failed, nowUtc, code);
                }
                else if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RollbackPending))
                {
                    FinishRollback(operation, nowUtc, code);
                }
            }

            timeline.Add($"fail:{code}");
            return Fail(
                operation.State,
                code,
                timeline,
                activationOrderUsed.Count > 0
                    ? activationOrderUsed
                    : plan.DevicePlans.Select(static p => p.DeviceId).ToArray(),
                rolledBack,
                retainedWatchdog);
        }
    }

    private static void FinishRollback(DeploymentOperation operation, DateTimeOffset nowUtc, string code)
    {
        if (operation.IsTerminal)
        {
            return;
        }

        if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RollbackPending))
        {
            Advance(operation, DeploymentOperationState.RollbackPending, nowUtc, code);
        }

        if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RollingBack))
        {
            Advance(operation, DeploymentOperationState.RollingBack, nowUtc, code);
        }

        if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RolledBack))
        {
            Advance(operation, DeploymentOperationState.RolledBack, nowUtc, code);
        }
        else if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RecoveryRequired))
        {
            Advance(operation, DeploymentOperationState.RecoveryRequired, nowUtc, code);
        }
    }

    private static async Task RollbackActivatedAsync(
        List<DeviceId> activated,
        Dictionary<DeviceId, IVrrpMemberDeploymentRuntime> byId,
        List<DeviceId> rolledBack,
        List<DeviceId> retainedWatchdog,
        List<string> timeline,
        CancellationToken cancellationToken)
    {
        HashSet<DeviceId> reachable = [];
        foreach (DeviceId id in activated)
        {
            if (await byId[id].IsReachableAsync(cancellationToken).ConfigureAwait(false))
            {
                reachable.Add(id);
            }
        }

        (IReadOnlyList<DeviceId> targets, IReadOnlyList<DeviceId> retain) =
            VrrpDeploymentPolicy.PlanPartialFailureActions(activated, reachable);
        foreach (DeviceId id in targets)
        {
            await byId[id].RollbackActivationAsync(cancellationToken).ConfigureAwait(false);
            rolledBack.Add(id);
            timeline.Add($"rollback:{id.Value:D}");
        }

        foreach (DeviceId id in retain)
        {
            retainedWatchdog.Add(id);
            timeline.Add($"watchdog-retain:{id.Value:D}");
        }
    }

    private static async Task<VrrpRoleVector> ReadVectorAsync(
        IReadOnlyList<IVrrpMemberDeploymentRuntime> members,
        CancellationToken cancellationToken)
    {
        List<VrrpMemberRoleSnapshot> snapshots = [];
        foreach (IVrrpMemberDeploymentRuntime member in members.OrderBy(static m => m.DeviceId.Value))
        {
            snapshots.Add(await member.ReadRoleSnapshotAsync(cancellationToken).ConfigureAwait(false));
        }

        return new VrrpRoleVector { Members = snapshots };
    }

    private static void Advance(
        DeploymentOperation operation,
        DeploymentOperationState next,
        DateTimeOffset nowUtc,
        string? errorCode = null)
        => operation.EnsureTransition(next, nowUtc, errorCode);

    private static VrrpDeploymentResult Fail(
        DeploymentOperationState state,
        string code,
        List<string> timeline,
        IReadOnlyList<DeviceId> activationOrder,
        List<DeviceId> rolledBack,
        List<DeviceId> retainedWatchdog)
        => new()
        {
            Succeeded = false,
            State = state,
            ErrorCode = code,
            Timeline = timeline,
            ActivationOrderUsed = activationOrder,
            RolledBackMembers = rolledBack,
            WatchdogRetainedMembers = retainedWatchdog,
            PartialCommitAttempted = false,
        };
}
