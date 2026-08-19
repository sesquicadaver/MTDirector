using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Onboarding;

/// <summary>Outcome of controller-initiated onboarding rollback (M5-08).</summary>
public sealed class OnboardingRollbackResult
{
    public required bool Succeeded { get; init; }

    public required OnboardingOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }

    public required bool WatchdogsCleaned { get; init; }

    public required bool NodeUnmanaged { get; init; }

    public required bool RemainingEnabledAnchors { get; init; }
}

/// <summary>
/// Deterministic rollback: disable exact bootstrap anchors (reverse enable order),
/// reconnect, remove only operation resources, then roots, then watchdog residue (Spec §44).
/// </summary>
public static class RollbackOnboardingBootstrapUseCase
{
    public static async Task<OnboardingRollbackResult> ExecuteAsync(
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
        List<string> timeline = [];
        try
        {
            if (operation.NodeId != node.Id || plan.NodeId != node.Id)
            {
                throw new DomainInvariantException("Onboarding rollback node/plan/operation mismatch.");
            }

            if (operation.State == OnboardingOperationState.RolledBack)
            {
                return Success(operation, node, timeline, watchdogsCleaned: true, remainingEnabled: false);
            }

            if (operation.State == OnboardingOperationState.RecoveryRequired)
            {
                return Fail(operation, operation.ErrorCode ?? OnboardingCodes.UnexpectedAnchorTarget, timeline, remainingEnabled: true);
            }

            Dictionary<DeviceId, IOnboardingDeviceSession> byDevice = sessions.ToDictionary(static s => s.DeviceId);
            DeviceOnboardingPlan[] devicePlans = plan.DevicePlans
                .OrderByDescending(static p => p.DeviceId.Value)
                .ToArray();
            if (devicePlans.Length == 0 || devicePlans.Any(p => !byDevice.ContainsKey(p.DeviceId)))
            {
                throw new DomainInvariantException("Every device plan must have an onboarding session.");
            }

            foreach (DeviceOnboardingPlan devicePlan in devicePlans)
            {
                IOnboardingDeviceSession session = byDevice[devicePlan.DeviceId];
                IReadOnlyList<ActualFilterRule> live = await session.PrintFilterAsync(cancellationToken).ConfigureAwait(false);
                OnboardingAnchorSetState anchors = OnboardingRecoveryDecision.ClassifyAnchors(
                    devicePlan.RequiredAnchorSet,
                    live,
                    committed: false);
                if (anchors == OnboardingAnchorSetState.UnexpectedTarget)
                {
                    return await RecoverAsync(
                        node,
                        operation,
                        OnboardingCodes.UnexpectedAnchorTarget,
                        nowUtc,
                        timeline).ConfigureAwait(false);
                }
            }

            await AdvanceToRollingBackAsync(operation, nowUtc).ConfigureAwait(false);

            foreach (DeviceOnboardingPlan devicePlan in devicePlans)
            {
                IOnboardingDeviceSession session = byDevice[devicePlan.DeviceId];
                foreach (AnchorKey key in OnboardingEnableOrder.Reverse(devicePlan.RequiredAnchorSet))
                {
                    IReadOnlyList<ActualFilterRule> live = await session.PrintFilterAsync(cancellationToken).ConfigureAwait(false);
                    ActualFilterRule? match = live.FirstOrDefault(r =>
                        string.Equals(r.Comment, key.Marker, StringComparison.Ordinal));
                    if (match is null)
                    {
                        continue;
                    }

                    if (!match.Disabled)
                    {
                        OnboardingBootstrapWriteExecutionResult disabled = await session.Bootstrap.ApplyAsync(
                            OnboardingBootstrapWrite.SetAnchorDisabled(key.Family, key.Chain, disabled: true),
                            live,
                            cancellationToken).ConfigureAwait(false);
                        if (!disabled.Succeeded)
                        {
                            return await RecoverAsync(node, operation, OnboardingCodes.RollbackFailed, nowUtc, timeline)
                                .ConfigureAwait(false);
                        }

                        timeline.Add($"disable:{key.Marker}");
                    }
                }

                if (!await session.ReconnectManagementAsync(cancellationToken).ConfigureAwait(false))
                {
                    return await RecoverAsync(
                        node,
                        operation,
                        OnboardingCodes.OnboardingManagementReconnectFailed,
                        nowUtc,
                        timeline).ConfigureAwait(false);
                }

                timeline.Add($"reconnect:{devicePlan.DeviceId.Value:D}");

                foreach (AnchorKey key in OnboardingEnableOrder.Reverse(devicePlan.RequiredAnchorSet))
                {
                    IReadOnlyList<ActualFilterRule> live = await session.PrintFilterAsync(cancellationToken).ConfigureAwait(false);
                    if (!live.Any(r => string.Equals(r.Comment, key.Marker, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    OnboardingBootstrapWriteExecutionResult removed = await session.Bootstrap.ApplyAsync(
                        OnboardingBootstrapWrite.RemoveDisabledAnchor(key.Family, key.Chain),
                        live,
                        cancellationToken).ConfigureAwait(false);
                    if (!removed.Succeeded)
                    {
                        return await RecoverAsync(node, operation, OnboardingCodes.RollbackFailed, nowUtc, timeline)
                            .ConfigureAwait(false);
                    }

                    timeline.Add($"remove-anchor:{key.Marker}");
                }

                foreach (AnchorKey key in OnboardingEnableOrder.Reverse(devicePlan.RequiredAnchorSet))
                {
                    IReadOnlyList<ActualFilterRule> live = await session.PrintFilterAsync(cancellationToken).ConfigureAwait(false);
                    bool rootPresent = live.Any(r =>
                        string.Equals(r.Comment, BootstrapArtifact.ReturnComment, StringComparison.Ordinal)
                        && string.Equals(
                            r.Chain,
                            BootstrapArtifact.RootChainName(key.Family, key.Chain),
                            StringComparison.OrdinalIgnoreCase));
                    if (!rootPresent)
                    {
                        continue;
                    }

                    OnboardingBootstrapWriteExecutionResult removedRoot = await session.Bootstrap.ApplyAsync(
                        OnboardingBootstrapWrite.RemoveBootstrapReturn(key.Family, key.Chain),
                        live,
                        cancellationToken).ConfigureAwait(false);
                    if (!removedRoot.Succeeded)
                    {
                        return await RecoverAsync(node, operation, OnboardingCodes.RollbackFailed, nowUtc, timeline)
                            .ConfigureAwait(false);
                    }

                    timeline.Add($"remove-root:{key.Marker}");
                }

                OnboardingWatchdogExecutionResult cleaned = await session.Watchdog.CleanupWatchdogAsync(
                    operation.Id,
                    devicePlan.DeviceId,
                    cancellationToken).ConfigureAwait(false);
                if (!cleaned.Succeeded)
                {
                    return await RecoverAsync(node, operation, cleaned.Code, nowUtc, timeline).ConfigureAwait(false);
                }

                timeline.Add($"cleanup-watchdog:{devicePlan.DeviceId.Value:D}");
            }

            bool remainingEnabled = false;
            foreach (DeviceOnboardingPlan devicePlan in devicePlans)
            {
                IReadOnlyList<ActualFilterRule> after = await byDevice[devicePlan.DeviceId].PrintFilterAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (after.Any(r =>
                        r.Comment is not null
                        && r.Comment.StartsWith("mfc:anchor:v1:", StringComparison.Ordinal)
                        && !r.Disabled))
                {
                    remainingEnabled = true;
                }
            }

            if (remainingEnabled)
            {
                return await RecoverAsync(node, operation, OnboardingCodes.RollbackFailed, nowUtc, timeline)
                    .ConfigureAwait(false);
            }

            foreach (Device device in node.Devices)
            {
                if (device.ManagementState != ManagementState.Unmanaged)
                {
                    device.SetManagementState(ManagementState.Unmanaged);
                }
            }

            if (node.ManagementState != ManagementState.Unmanaged)
            {
                node.SetManagementState(ManagementState.Unmanaged);
            }

            if (operation.State != OnboardingOperationState.RolledBack)
            {
                operation.EnsureTransition(OnboardingOperationState.RolledBack, nowUtc);
            }

            return Success(operation, node, timeline, watchdogsCleaned: true, remainingEnabled: false);
        }
        catch (InvalidOperationException ex)
        {
            timeline.Add($"error:{ex.Message}");
            return await RecoverAsync(node, operation, OnboardingCodes.RollbackFailed, nowUtc, timeline).ConfigureAwait(false);
        }
    }

    private static Task AdvanceToRollingBackAsync(OnboardingOperation operation, DateTimeOffset nowUtc)
    {
        if (operation.State == OnboardingOperationState.Created)
        {
            operation.EnsureTransition(OnboardingOperationState.Prechecking, nowUtc);
        }

        if (operation.State == OnboardingOperationState.Prechecking)
        {
            operation.EnsureTransition(OnboardingOperationState.StagingBootstrapRoots, nowUtc);
        }

        if (operation.State != OnboardingOperationState.RollbackPending
            && operation.State != OnboardingOperationState.RollingBack
            && !OnboardingOperation.IsTerminalState(operation.State))
        {
            operation.EnsureTransition(OnboardingOperationState.RollbackPending, nowUtc);
        }

        if (operation.State == OnboardingOperationState.RollbackPending)
        {
            operation.EnsureTransition(OnboardingOperationState.RollingBack, nowUtc);
        }

        return Task.CompletedTask;
    }

    private static Task<OnboardingRollbackResult> RecoverAsync(
        Node node,
        OnboardingOperation operation,
        string code,
        DateTimeOffset nowUtc,
        List<string> timeline)
    {
        foreach (Device device in node.Devices)
        {
            device.SetManagementState(ManagementState.RecoveryRequired);
        }

        node.SetManagementState(ManagementState.RecoveryRequired);
        if (!operation.IsTerminal)
        {
            operation.EnsureTransition(OnboardingOperationState.RecoveryRequired, nowUtc, code);
        }

        timeline.Add($"recovery:{code}");
        return Task.FromResult(Fail(operation, code, timeline, remainingEnabled: true));
    }

    private static OnboardingRollbackResult Success(
        OnboardingOperation operation,
        Node node,
        List<string> timeline,
        bool watchdogsCleaned,
        bool remainingEnabled)
        => new()
        {
            Succeeded = true,
            State = operation.State,
            Timeline = timeline,
            WatchdogsCleaned = watchdogsCleaned,
            NodeUnmanaged = node.ManagementState == ManagementState.Unmanaged,
            RemainingEnabledAnchors = remainingEnabled,
        };

    private static OnboardingRollbackResult Fail(
        OnboardingOperation operation,
        string code,
        List<string> timeline,
        bool remainingEnabled)
        => new()
        {
            Succeeded = false,
            State = operation.State,
            ErrorCode = code,
            Timeline = timeline,
            WatchdogsCleaned = false,
            NodeUnmanaged = false,
            RemainingEnabledAnchors = remainingEnabled,
        };
}
