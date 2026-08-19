using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Onboarding;

/// <summary>Outcome of crash recovery (Spec §45–§46 / M5-08). Never adopts unexpected targets.</summary>
public sealed class OnboardingRecoveryResult
{
    public required OnboardingRecoveryAction Action { get; init; }

    public required OnboardingOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }

    public required bool NodeUnmanaged { get; init; }

    public required bool NodeManaged { get; init; }
}

/// <summary>Applies Spec §46 after process restart. Automatic adoption is absent by construction.</summary>
public static class RecoverOnboardingUseCase
{
    public static async Task<OnboardingRecoveryResult> ExecuteAsync(
        Node node,
        OnboardingPlan plan,
        OnboardingOperation operation,
        IReadOnlyList<IOnboardingDeviceSession> sessions,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(sessions);

        Dictionary<DeviceId, IOnboardingDeviceSession> byDevice = sessions.ToDictionary(static s => s.DeviceId);
        bool committed = operation.State == OnboardingOperationState.Committed;
        OnboardingRecoveryAction worst = OnboardingRecoveryAction.KeepManaged;
        List<string> timeline = [];
        foreach (DeviceOnboardingPlan devicePlan in plan.DevicePlans.OrderBy(static p => p.DeviceId.Value))
        {
            if (!byDevice.TryGetValue(devicePlan.DeviceId, out IOnboardingDeviceSession? session))
            {
                throw new DomainInvariantException("Every device plan must have an onboarding session.");
            }

            IReadOnlyList<ActualFilterRule> live = await session.PrintFilterAsync(cancellationToken).ConfigureAwait(false);
            OnboardingSystemNameFacts names = await session.PrintSystemNamesAsync(cancellationToken).ConfigureAwait(false);
            OnboardingAnchorSetState anchors = OnboardingRecoveryDecision.ClassifyAnchors(
                devicePlan.RequiredAnchorSet,
                live,
                committed);
            OnboardingWatchdogPresence watchdog = OnboardingRecoveryDecision.ClassifyWatchdog(names);
            OnboardingRecoveryAction action = OnboardingRecoveryDecision.Decide(anchors, watchdog, committed);
            timeline.Add($"observe:{devicePlan.DeviceId.Value:D}:{anchors}:{watchdog}:{action}");
            worst = Rank(action) > Rank(worst) ? action : worst;
        }

        if (worst == OnboardingRecoveryAction.RecoveryRequired)
        {
            MarkRecovery(node, operation, OnboardingCodes.UnexpectedAnchorTarget, nowUtc);
            return Outcome(operation, node, worst, timeline);
        }

        if (worst == OnboardingRecoveryAction.CriticalDrift)
        {
            MarkRecovery(node, operation, OnboardingCodes.OnboardingCriticalDrift, nowUtc);
            return Outcome(operation, node, worst, timeline);
        }

        if (worst == OnboardingRecoveryAction.KeepManaged)
        {
            foreach (DeviceOnboardingPlan devicePlan in plan.DevicePlans)
            {
                OnboardingWatchdogExecutionResult cleaned = await byDevice[devicePlan.DeviceId].Watchdog
                    .CleanupWatchdogAsync(operation.Id, devicePlan.DeviceId, cancellationToken)
                    .ConfigureAwait(false);
                if (!cleaned.Succeeded)
                {
                    timeline.Add($"cleanup-failed:{cleaned.Code}");
                    MarkRecovery(node, operation, cleaned.Code, nowUtc);
                    return Outcome(operation, node, OnboardingRecoveryAction.CriticalDrift, timeline);
                }

                timeline.Add($"cleanup-watchdog:{devicePlan.DeviceId.Value:D}");
            }

            return Outcome(operation, node, worst, timeline);
        }

        OnboardingRollbackResult rolled = await RollbackOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            sessions,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        timeline.AddRange(rolled.Timeline);
        return new OnboardingRecoveryResult
        {
            Action = worst,
            State = rolled.State,
            ErrorCode = rolled.ErrorCode,
            Timeline = timeline,
            NodeUnmanaged = rolled.NodeUnmanaged,
            NodeManaged = node.ManagementState == ManagementState.Managed,
        };
    }

    private static int Rank(OnboardingRecoveryAction action)
        => action switch
        {
            OnboardingRecoveryAction.KeepManaged => 0,
            OnboardingRecoveryAction.CleanupRolledBack => 1,
            OnboardingRecoveryAction.ControllerRollback => 2,
            OnboardingRecoveryAction.CriticalDrift => 3,
            OnboardingRecoveryAction.RecoveryRequired => 4,
            _ => throw new DomainInvariantException($"Unknown recovery action '{action}'."),
        };

    private static void MarkRecovery(Node node, OnboardingOperation operation, string code, DateTimeOffset nowUtc)
    {
        foreach (Device device in node.Devices)
        {
            device.SetManagementState(ManagementState.RecoveryRequired);
        }

        node.SetManagementState(ManagementState.RecoveryRequired);
        if (operation.State == OnboardingOperationState.Committed)
        {
            return;
        }

        if (OnboardingOperation.IsTerminalState(operation.State))
        {
            return;
        }

        operation.EnsureTransition(OnboardingOperationState.RecoveryRequired, nowUtc, code);
    }

    private static OnboardingRecoveryResult Outcome(
        OnboardingOperation operation,
        Node node,
        OnboardingRecoveryAction action,
        List<string> timeline)
        => new()
        {
            Action = action,
            State = operation.State,
            ErrorCode = OnboardingRecoveryDecision.CodeFor(action) ?? operation.ErrorCode,
            Timeline = timeline,
            NodeUnmanaged = node.ManagementState == ManagementState.Unmanaged,
            NodeManaged = node.ManagementState == ManagementState.Managed,
        };
}
