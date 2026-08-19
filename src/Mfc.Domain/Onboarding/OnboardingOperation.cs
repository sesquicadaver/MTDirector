using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>Mutable onboarding operation with a closed transition table (Onboarding Spec §5 / §48).</summary>
public sealed class OnboardingOperation
{
    private static readonly HashSet<(OnboardingOperationState From, OnboardingOperationState To)> Allowed =
        new()
        {
            (OnboardingOperationState.Created, OnboardingOperationState.Prechecking),
            (OnboardingOperationState.Created, OnboardingOperationState.RecoveryRequired),
            (OnboardingOperationState.Prechecking, OnboardingOperationState.StagingBootstrapRoots),
            (OnboardingOperationState.Prechecking, OnboardingOperationState.Blocked),
            (OnboardingOperationState.Prechecking, OnboardingOperationState.RecoveryRequired),
            (OnboardingOperationState.StagingBootstrapRoots, OnboardingOperationState.StagingDisabledAnchors),
            (OnboardingOperationState.StagingDisabledAnchors, OnboardingOperationState.ArmingWatchdogs),
            (OnboardingOperationState.ArmingWatchdogs, OnboardingOperationState.EnablingAnchors),
            (OnboardingOperationState.EnablingAnchors, OnboardingOperationState.Verifying),
            (OnboardingOperationState.Verifying, OnboardingOperationState.DisarmingWatchdogs),
            (OnboardingOperationState.DisarmingWatchdogs, OnboardingOperationState.Committed),
            (OnboardingOperationState.StagingBootstrapRoots, OnboardingOperationState.RollbackPending),
            (OnboardingOperationState.StagingDisabledAnchors, OnboardingOperationState.RollbackPending),
            (OnboardingOperationState.ArmingWatchdogs, OnboardingOperationState.RollbackPending),
            (OnboardingOperationState.EnablingAnchors, OnboardingOperationState.RollbackPending),
            (OnboardingOperationState.Verifying, OnboardingOperationState.RollbackPending),
            (OnboardingOperationState.DisarmingWatchdogs, OnboardingOperationState.RollbackPending),
            (OnboardingOperationState.StagingBootstrapRoots, OnboardingOperationState.RecoveryRequired),
            (OnboardingOperationState.StagingDisabledAnchors, OnboardingOperationState.RecoveryRequired),
            (OnboardingOperationState.ArmingWatchdogs, OnboardingOperationState.RecoveryRequired),
            (OnboardingOperationState.EnablingAnchors, OnboardingOperationState.RecoveryRequired),
            (OnboardingOperationState.Verifying, OnboardingOperationState.RecoveryRequired),
            (OnboardingOperationState.DisarmingWatchdogs, OnboardingOperationState.RecoveryRequired),
            (OnboardingOperationState.RollbackPending, OnboardingOperationState.RollingBack),
            (OnboardingOperationState.RollbackPending, OnboardingOperationState.RecoveryRequired),
            (OnboardingOperationState.RollingBack, OnboardingOperationState.RolledBack),
            (OnboardingOperationState.RollingBack, OnboardingOperationState.RecoveryRequired),
        };

    private OnboardingOperation(
        OnboardingOperationId id,
        NodeId nodeId,
        OnboardingPlanId planId,
        OnboardingOperationState state,
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

    public OnboardingOperationId Id { get; }

    public NodeId NodeId { get; }

    public OnboardingPlanId PlanId { get; }

    public OnboardingOperationState State { get; private set; }

    public UserId CreatedBy { get; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? ErrorCode { get; private set; }

    public ulong RowVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsTerminal => IsTerminalState(State);

    public bool IsNonterminal => !IsTerminal;

    public static bool IsTerminalState(OnboardingOperationState state)
        => state is OnboardingOperationState.Committed
            or OnboardingOperationState.RolledBack
            or OnboardingOperationState.Blocked
            or OnboardingOperationState.RecoveryRequired;

    public static bool CanTransition(OnboardingOperationState from, OnboardingOperationState to)
        => Allowed.Contains((from, to));

    /// <summary>Creates CREATED operation. Node must be UNMANAGED and match <paramref name="plan"/>.</summary>
    public static OnboardingOperation Create(
        OnboardingPlan plan,
        Node node,
        UserId createdBy,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(node);
        if (node.Id != plan.NodeId)
        {
            throw new DomainInvariantException("Operation node_id must match plan.node_id.");
        }

        if (node.ManagementState != ManagementState.Unmanaged)
        {
            throw new DomainInvariantException(
                $"{OnboardingCodes.NodeNotUnmanaged}: Node must be UNMANAGED to start onboarding.");
        }

        return Create(plan, createdBy, nowUtc);
    }

    public static OnboardingOperation Create(OnboardingPlan plan, UserId createdBy, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        DateTimeOffset now = nowUtc.ToUniversalTime();
        if (plan.IsExpired(now))
        {
            throw new DomainInvariantException($"{OnboardingCodes.PlanExpired}: plan lifetime elapsed.");
        }

        return new OnboardingOperation(
            OnboardingOperationId.New(),
            plan.NodeId,
            plan.Id,
            OnboardingOperationState.Created,
            createdBy,
            startedAtUtc: null,
            completedAtUtc: null,
            errorCode: null,
            rowVersion: 1,
            now,
            now);
    }

    public static OnboardingOperation Reconstitute(
        OnboardingOperationId id,
        NodeId nodeId,
        OnboardingPlanId planId,
        OnboardingOperationState state,
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

        return new OnboardingOperation(
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

    public void EnsureTransition(OnboardingOperationState next, DateTimeOffset nowUtc, string? errorCode = null)
    {
        OnboardingGateEvaluation evaluation = OnboardingOperationGate.EvaluateTransition(this, next);
        if (!evaluation.Allowed)
        {
            throw new DomainInvariantException($"{evaluation.ErrorCode}: {evaluation.ErrorMessage}");
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        if (State == OnboardingOperationState.Created && next == OnboardingOperationState.Prechecking)
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
}
