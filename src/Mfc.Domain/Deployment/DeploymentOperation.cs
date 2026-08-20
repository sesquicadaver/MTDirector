using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Deployment;

/// <summary>Mutable Node deployment with a closed transition table (Safe Deployment Spec §13). No campaign state.</summary>
public sealed class DeploymentOperation
{
    private static readonly HashSet<(DeploymentOperationState From, DeploymentOperationState To)> Allowed =
    [
        (DeploymentOperationState.Created, DeploymentOperationState.Prechecking),
        (DeploymentOperationState.Created, DeploymentOperationState.Canceled),
        (DeploymentOperationState.Created, DeploymentOperationState.RecoveryRequired),
        (DeploymentOperationState.Prechecking, DeploymentOperationState.Staging),
        (DeploymentOperationState.Prechecking, DeploymentOperationState.Blocked),
        (DeploymentOperationState.Prechecking, DeploymentOperationState.NoChanges),
        (DeploymentOperationState.Prechecking, DeploymentOperationState.Canceled),
        (DeploymentOperationState.Prechecking, DeploymentOperationState.Failed),
        (DeploymentOperationState.Prechecking, DeploymentOperationState.RecoveryRequired),
        (DeploymentOperationState.Staging, DeploymentOperationState.Staged),
        (DeploymentOperationState.Staged, DeploymentOperationState.ArmingWatchdog),
        (DeploymentOperationState.ArmingWatchdog, DeploymentOperationState.WatchdogArmed),
        (DeploymentOperationState.WatchdogArmed, DeploymentOperationState.Activating),
        (DeploymentOperationState.Activating, DeploymentOperationState.Verifying),
        (DeploymentOperationState.Verifying, DeploymentOperationState.DisarmingWatchdog),
        (DeploymentOperationState.DisarmingWatchdog, DeploymentOperationState.Committed),
        (DeploymentOperationState.Staging, DeploymentOperationState.RollbackPending),
        (DeploymentOperationState.ArmingWatchdog, DeploymentOperationState.RollbackPending),
        (DeploymentOperationState.Activating, DeploymentOperationState.RollbackPending),
        (DeploymentOperationState.Verifying, DeploymentOperationState.RollbackPending),
        (DeploymentOperationState.DisarmingWatchdog, DeploymentOperationState.RollbackPending),
        (DeploymentOperationState.Staging, DeploymentOperationState.RecoveryRequired),
        (DeploymentOperationState.ArmingWatchdog, DeploymentOperationState.RecoveryRequired),
        (DeploymentOperationState.Activating, DeploymentOperationState.RecoveryRequired),
        (DeploymentOperationState.Verifying, DeploymentOperationState.RecoveryRequired),
        (DeploymentOperationState.DisarmingWatchdog, DeploymentOperationState.RecoveryRequired),
        (DeploymentOperationState.RollbackPending, DeploymentOperationState.RollingBack),
        (DeploymentOperationState.RollbackPending, DeploymentOperationState.RecoveryRequired),
        (DeploymentOperationState.RollingBack, DeploymentOperationState.RolledBack),
        (DeploymentOperationState.RollingBack, DeploymentOperationState.RecoveryRequired),
        (DeploymentOperationState.RollingBack, DeploymentOperationState.Failed),
    ];

    private DeploymentOperation(
        DeploymentOperationId id,
        NodeId nodeId,
        DeploymentPlanId planId,
        DeploymentOperationState state,
        UserId createdBy,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? completedAtUtc,
        string? errorCode,
        ulong rowVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        NodeId = nodeId;
        PlanId = planId;
        State = state;
        CreatedBy = createdBy;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        ErrorCode = errorCode;
        RowVersion = rowVersion;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public DeploymentOperationId Id { get; }

    public NodeId NodeId { get; }

    public DeploymentPlanId PlanId { get; }

    public DeploymentOperationState State { get; private set; }

    public UserId CreatedBy { get; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? ErrorCode { get; private set; }

    public ulong RowVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsTerminal => IsTerminalState(State);

    public bool IsNonterminal => !IsTerminal;

    public static bool IsTerminalState(DeploymentOperationState state)
        => state is DeploymentOperationState.Committed
            or DeploymentOperationState.RolledBack
            or DeploymentOperationState.Blocked
            or DeploymentOperationState.NoChanges
            or DeploymentOperationState.Canceled
            or DeploymentOperationState.Failed
            or DeploymentOperationState.RecoveryRequired;

    public static bool CanTransition(DeploymentOperationState from, DeploymentOperationState to)
        => Allowed.Contains((from, to));

    public static DeploymentOperation Create(DeploymentPlan plan, Node node, UserId createdBy, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(node);
        if (node.Id != plan.NodeId)
        {
            throw new DomainInvariantException("Operation node_id must match plan.node_id.");
        }

        if (node.Status == NodeStatus.Disabled)
        {
            throw new DomainInvariantException($"{DeploymentCodes.NodeDisabled}: disabled Node cannot be deployed.");
        }

        return Create(plan, createdBy, nowUtc);
    }

    public static DeploymentOperation Create(DeploymentPlan plan, UserId createdBy, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        DateTimeOffset now = nowUtc.ToUniversalTime();
        if (plan.IsExpired(now))
        {
            throw new DomainInvariantException($"{DeploymentCodes.PlanExpired}: plan lifetime elapsed.");
        }

        return new DeploymentOperation(
            DeploymentOperationId.New(),
            plan.NodeId,
            plan.Id,
            DeploymentOperationState.Created,
            createdBy,
            startedAtUtc: null,
            completedAtUtc: null,
            errorCode: null,
            rowVersion: 1,
            now,
            now);
    }

    public static DeploymentOperation Reconstitute(
        DeploymentOperationId id,
        NodeId nodeId,
        DeploymentPlanId planId,
        DeploymentOperationState state,
        UserId createdBy,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? completedAtUtc,
        string? errorCode,
        ulong rowVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        if (rowVersion == 0)
        {
            throw new DomainInvariantException("row_version must be greater than zero.");
        }

        if (IsTerminalState(state) && completedAtUtc is null)
        {
            throw new DomainInvariantException("Terminal operations require completed_at.");
        }

        return new DeploymentOperation(
            id,
            nodeId,
            planId,
            state,
            createdBy,
            startedAtUtc?.ToUniversalTime(),
            completedAtUtc?.ToUniversalTime(),
            errorCode,
            rowVersion,
            createdAtUtc.ToUniversalTime(),
            updatedAtUtc.ToUniversalTime());
    }

    public void EnsureTransition(DeploymentOperationState next, DateTimeOffset nowUtc, string? errorCode = null)
    {
        if (IsTerminal)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.TerminalImmutable}: terminal operations are immutable.");
        }

        if (!CanTransition(State, next))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.InvalidTransition}: '{State}' → '{next}' is not allowed.");
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        if (State == DeploymentOperationState.Created && next == DeploymentOperationState.Prechecking)
        {
            StartedAtUtc = now;
        }

        State = next;
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim();
        if (IsTerminalState(next))
        {
            CompletedAtUtc = now;
        }

        RowVersion++;
        UpdatedAtUtc = now;
    }

    public void EnsureCommitted(IReadOnlyList<DeviceDeployment> devices, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(devices);
        if (devices.Count == 0 || devices.Any(d => d.State != DeviceDeploymentState.Committed))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.DevicesNotCommitted}: Node COMMITTED requires every Device COMMITTED.");
        }

        EnsureTransition(DeploymentOperationState.Committed, nowUtc);
    }
}
