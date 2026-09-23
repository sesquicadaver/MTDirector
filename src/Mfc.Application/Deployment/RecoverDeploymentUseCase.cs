using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;

namespace Mfc.Application.Deployment;

/// <summary>Observed jump-targets for one device during rollback/recovery.</summary>
public interface IDeploymentRollbackDeviceRuntime
{
    DeviceId DeviceId { get; }

    Task<IReadOnlyDictionary<string, string>> ReadAnchorJumpsAsync(CancellationToken cancellationToken = default);

    Task<DeploymentWriteExecutionResult> SetAnchorTargetAsync(
        AnchorTargetWrite write,
        CancellationToken cancellationToken = default);

    Task<Hash256> ReadManagedResourceHashAsync(CancellationToken cancellationToken = default);

    Task<IDeploymentFreshSessionFactory> CreateFreshSessionFactoryAsync(
        CancellationToken cancellationToken = default);

    Task<RouterPingResult> ProbeAsync(DeploymentProbe probe, CancellationToken cancellationToken = default);

    Task<DeploymentWatchdogExecutionResult> DisarmAndCleanupWatchdogAsync(
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<string> SchedulerNames, IReadOnlyDictionary<string, bool> SchedulerDisabled)>
        ReadWatchdogSchedulerFactsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Outcome of controller-initiated rollback (Spec §46 / M4-11).</summary>
public sealed class DeploymentRollbackResult
{
    public required bool Succeeded { get; init; }

    public required DeploymentOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }

    public required bool UsedFreshApiSslSession { get; init; }

    public required bool DetachedArtifactPreserved { get; init; }
}

/// <summary>Outcome of crash/watchdog recovery (Spec §47–§49 / M4-11).</summary>
public sealed class DeploymentRecoveryResult
{
    public required bool Succeeded { get; init; }

    public required DeploymentRecoveryAction Action { get; init; }

    public required DeploymentOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }
}

/// <summary>
/// Controller-initiated rollback to old artifact (Safe Deployment Spec §46).
/// Devices in reverse activation order; anchors in plan rollback order; no detached remove.
/// Automatic standalone/VRRP paths must use this coordinator (AUDIT-RB-01 / F04).
/// </summary>
public static class ExecuteDeploymentRollbackUseCase
{
    public static async Task<DeploymentRollbackResult> ExecuteAsync(
        DeploymentPlan plan,
        DeploymentOperation operation,
        IReadOnlyList<IDeploymentRollbackDeviceRuntime> devices,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(devices);

        List<string> timeline = [];
        bool usedFresh = false;
        Dictionary<DeviceId, IDeploymentRollbackDeviceRuntime> byId = devices.ToDictionary(static d => d.DeviceId);
        IReadOnlyList<DeviceId> deviceOrder = DeploymentRecoveryDecision.DeviceRollbackOrder(plan.ActivationOrder);

        try
        {
            if (operation.IsTerminal && operation.State == DeploymentOperationState.Committed)
            {
                throw new DomainInvariantException(
                    $"{DeploymentCodes.TerminalImmutable}: COMMITTED deployment cannot be rolled back by this path.");
            }

            if (!operation.IsTerminal
                && DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RollbackPending))
            {
                operation.EnsureTransition(DeploymentOperationState.RollbackPending, nowUtc);
            }

            if (!operation.IsTerminal
                && DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RollingBack))
            {
                operation.EnsureTransition(DeploymentOperationState.RollingBack, nowUtc);
            }

            timeline.Add("rollback:start");

            foreach (DeviceId deviceId in deviceOrder)
            {
                if (!byId.TryGetValue(deviceId, out IDeploymentRollbackDeviceRuntime? runtime))
                {
                    throw new DomainInvariantException(
                        $"{DeploymentCodes.DevicePlanCardinality}: missing rollback runtime for '{deviceId.Value:D}'.");
                }

                DeviceDeploymentPlan devicePlan = plan.DevicePlans.Single(p => p.DeviceId.Equals(deviceId));
                timeline.Add($"rollback-device:{deviceId.Value:D}");
                bool deviceFresh = await RollbackDeviceAsync(devicePlan, runtime, timeline, cancellationToken)
                    .ConfigureAwait(false);
                usedFresh = usedFresh || deviceFresh;
            }

            if (!operation.IsTerminal)
            {
                operation.EnsureTransition(DeploymentOperationState.RolledBack, nowUtc);
            }

            timeline.Add("rollback:done");
            timeline.Add("detached-preserved");
            return new DeploymentRollbackResult
            {
                Succeeded = true,
                State = operation.State,
                Timeline = timeline,
                UsedFreshApiSslSession = usedFresh,
                DetachedArtifactPreserved = true,
            };
        }
        catch (Exception ex) when (ex is DomainInvariantException or InvalidOperationException)
        {
            string code = ex is DomainInvariantException dix && dix.Message.Contains(':', StringComparison.Ordinal)
                ? dix.Message.Split(':', 2)[0]
                : DeploymentCodes.InvalidTransition;
            timeline.Add($"fail:{code}");
            if (!operation.IsTerminal
                && DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RecoveryRequired))
            {
                MarkRecovery(operation, nowUtc);
            }

            return Fail(operation.State, code, timeline, usedFresh);
        }
    }

    /// <summary>
    /// Strict single-device rollback: third target blocks; old artifact + probes + watchdog cleanup required.
    /// </summary>
    /// <returns><see langword="true"/> when a fresh API-SSL session was opened.</returns>
    public static async Task<bool> RollbackDeviceAsync(
        DeviceDeploymentPlan devicePlan,
        IDeploymentRollbackDeviceRuntime runtime,
        List<string> timeline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(timeline);

        IReadOnlyDictionary<string, string> jumps = await runtime.ReadAnchorJumpsAsync(cancellationToken)
            .ConfigureAwait(false);
        DeploymentAnchorSetState classified = DeploymentRecoveryDecision.ClassifyAnchors(
            devicePlan.OldAnchorTargets,
            devicePlan.NewAnchorTargets,
            jumps);
        if (classified == DeploymentAnchorSetState.ThirdTarget)
        {
            timeline.Add("recovery-required:third-target");
            throw new DomainInvariantException(
                $"{DeploymentCodes.RecoveryRequired}: third/manual anchor target blocks automatic rollback.");
        }

        foreach (AnchorKey key in devicePlan.AnchorRollbackOrder)
        {
            AnchorTarget old = devicePlan.OldAnchorTargets.Single(t => t.Key.Equals(key));
            AnchorTarget neu = devicePlan.NewAnchorTargets.Single(t => t.Key.Equals(key));
            if (!jumps.TryGetValue(key.Marker, out string? current) || string.IsNullOrWhiteSpace(current))
            {
                throw new DomainInvariantException(
                    $"{DeploymentCodes.AnchorInvalid}: missing live jump for '{key.Marker}'.");
            }

            string jump = current.Trim();
            if (!string.Equals(jump, old.JumpTarget, StringComparison.Ordinal)
                && !string.Equals(jump, neu.JumpTarget, StringComparison.Ordinal))
            {
                timeline.Add($"recovery-required:{key.Marker}");
                throw new DomainInvariantException(
                    $"{DeploymentCodes.RecoveryRequired}: unknown jump for '{key.Marker}'.");
            }

            if (string.Equals(jump, old.JumpTarget, StringComparison.Ordinal))
            {
                timeline.Add($"anchor-already-old:{key.Marker}");
                continue;
            }

            DeploymentWriteExecutionResult set = await runtime.SetAnchorTargetAsync(
                new AnchorTargetWrite(old.Key.Family, old.Key.Chain, old.JumpTarget),
                cancellationToken).ConfigureAwait(false);
            if (!set.Succeeded)
            {
                timeline.Add($"rollback-anchor-failed:{key.Marker}");
                throw new DomainInvariantException(
                    $"{DeploymentCodes.RecoveryRequired}: failed to restore old jump for '{key.Marker}'.");
            }

            timeline.Add($"rollback-anchor:{key.Marker}");
        }

        IReadOnlyDictionary<string, string> after = await runtime.ReadAnchorJumpsAsync(cancellationToken)
            .ConfigureAwait(false);
        if (DeploymentRecoveryDecision.ClassifyAnchors(
                devicePlan.OldAnchorTargets,
                devicePlan.NewAnchorTargets,
                after) != DeploymentAnchorSetState.AllOld)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.RecoveryRequired}: post-rollback anchors are not all-old.");
        }

        Hash256 observed = await runtime.ReadManagedResourceHashAsync(cancellationToken).ConfigureAwait(false);
        if (!observed.Equals(devicePlan.OldArtifactHash))
        {
            timeline.Add("old-artifact-hash:mismatch");
            throw new DomainInvariantException(
                $"{DeploymentCodes.OldArtifactHashMismatch}: observed managed hash is not the sealed old artifact.");
        }

        timeline.Add("old-artifact-hash:ok");

        IDeploymentFreshSessionFactory freshFactory = await runtime.CreateFreshSessionFactoryAsync(cancellationToken)
            .ConfigureAwait(false);
        await using IRouterOsDeploymentSession fresh = await freshFactory.OpenFreshAsync(cancellationToken)
            .ConfigureAwait(false);
        timeline.Add("fresh-api-ssl:opened");

        foreach (DeploymentProbe probe in devicePlan.Probes.Where(static p => p.Kind == DeploymentProbeKind.RouterPing))
        {
            RouterPingResult ping = await runtime.ProbeAsync(probe, cancellationToken).ConfigureAwait(false);
            DeploymentVerificationFinding? finding = PostActivationVerification.ClassifyCriticalProbeOutcome(
                probe.Kind,
                probe.Destination,
                ping.Outcome.ToString());
            if (finding is not null)
            {
                timeline.Add($"old-state-probe:fail:{probe.Destination}");
                throw new DomainInvariantException($"{finding.Code}: {finding.Message}");
            }

            timeline.Add($"old-state-probe:ok:{probe.Destination}");
        }

        _ = fresh;

        DeploymentWatchdogExecutionResult cleaned = await runtime
            .DisarmAndCleanupWatchdogAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!cleaned.Succeeded)
        {
            timeline.Add($"watchdog-cleanup-failed:{runtime.DeviceId.Value:D}:{cleaned.Code}");
            throw new DomainInvariantException(
                $"{cleaned.Code}: watchdog disarm/cleanup failed during strict rollback.");
        }

        timeline.Add($"watchdog-cleanup:{runtime.DeviceId.Value:D}");
        return true;
    }

    private static void MarkRecovery(DeploymentOperation operation, DateTimeOffset nowUtc)
    {
        if (operation.IsTerminal)
        {
            return;
        }

        if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RecoveryRequired))
        {
            operation.EnsureTransition(DeploymentOperationState.RecoveryRequired, nowUtc, DeploymentCodes.RecoveryRequired);
        }
    }

    private static DeploymentRollbackResult Fail(
        DeploymentOperationState state,
        string code,
        List<string> timeline,
        bool usedFresh)
        => new()
        {
            Succeeded = false,
            State = state,
            ErrorCode = code,
            Timeline = timeline,
            UsedFreshApiSslSession = usedFresh,
            DetachedArtifactPreserved = true,
        };
}

/// <summary>
/// Crash / watchdog recovery for nonterminal deployments (Safe Deployment Spec §47–§49).
/// </summary>
public static class RecoverDeploymentUseCase
{
    public static async Task<DeploymentRecoveryResult> ExecuteAsync(
        DeploymentPlan plan,
        DeploymentOperation operation,
        IReadOnlyList<IDeploymentRollbackDeviceRuntime> devices,
        bool activationStarted,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(devices);

        List<string> timeline = [];
        bool committed = operation.State == DeploymentOperationState.Committed;
        Dictionary<DeviceId, IDeploymentRollbackDeviceRuntime> byId = devices.ToDictionary(static d => d.DeviceId);

        // Aggregate worst-case classification across members.
        DeploymentAnchorSetState anchors = DeploymentAnchorSetState.AllOld;
        DeploymentWatchdogPresence watchdog = DeploymentWatchdogPresence.AbsentOrDisabled;

        foreach (DeviceDeploymentPlan devicePlan in plan.DevicePlans.OrderBy(static p => p.DeviceId.Value))
        {
            IDeploymentRollbackDeviceRuntime runtime = byId[devicePlan.DeviceId];
            IReadOnlyDictionary<string, string> jumps = await runtime.ReadAnchorJumpsAsync(cancellationToken)
                .ConfigureAwait(false);
            DeploymentAnchorSetState one = DeploymentRecoveryDecision.ClassifyAnchors(
                devicePlan.OldAnchorTargets,
                devicePlan.NewAnchorTargets,
                jumps);
            anchors = MergeAnchorState(anchors, one);

            (IReadOnlyList<string> names, IReadOnlyDictionary<string, bool> disabled) =
                await runtime.ReadWatchdogSchedulerFactsAsync(cancellationToken).ConfigureAwait(false);
            DeploymentWatchdogPresence wd = DeploymentRecoveryDecision.ClassifyWatchdog(names, disabled);
            if (wd == DeploymentWatchdogPresence.Active)
            {
                watchdog = DeploymentWatchdogPresence.Active;
            }
        }

        timeline.Add($"classify:anchors:{anchors}");
        timeline.Add($"classify:watchdog:{watchdog}");

        DeploymentRecoveryAction action = DeploymentRecoveryDecision.Decide(
            anchors,
            watchdog,
            committed,
            activationStarted);
        timeline.Add($"decide:{action}");

        if (action == DeploymentRecoveryAction.KeepCommitted)
        {
            return Ok(action, operation.State, timeline);
        }

        if (action == DeploymentRecoveryAction.RecoveryRequired)
        {
            if (!operation.IsTerminal
                && DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RecoveryRequired))
            {
                operation.EnsureTransition(
                    DeploymentOperationState.RecoveryRequired,
                    nowUtc,
                    DeploymentCodes.RecoveryRequired);
            }

            return new DeploymentRecoveryResult
            {
                Succeeded = false,
                Action = action,
                State = operation.State,
                ErrorCode = DeploymentCodes.RecoveryRequired,
                Timeline = timeline,
            };
        }

        if (action == DeploymentRecoveryAction.MarkFailedOrCanceled)
        {
            foreach (IDeploymentRollbackDeviceRuntime runtime in devices)
            {
                DeploymentWatchdogExecutionResult cleaned = await runtime
                    .DisarmAndCleanupWatchdogAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (!cleaned.Succeeded)
                {
                    timeline.Add($"watchdog-cleanup-failed:{runtime.DeviceId.Value:D}:{cleaned.Code}");
                    if (!operation.IsTerminal
                        && DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RecoveryRequired))
                    {
                        operation.EnsureTransition(
                            DeploymentOperationState.RecoveryRequired,
                            nowUtc,
                            cleaned.Code);
                    }

                    return new DeploymentRecoveryResult
                    {
                        Succeeded = false,
                        Action = DeploymentRecoveryAction.RecoveryRequired,
                        State = operation.State,
                        ErrorCode = cleaned.Code,
                        Timeline = timeline,
                    };
                }

                timeline.Add($"watchdog-cleanup:{runtime.DeviceId.Value:D}");
            }

            if (!operation.IsTerminal)
            {
                if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.Failed))
                {
                    operation.EnsureTransition(DeploymentOperationState.Failed, nowUtc);
                }
                else if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.Canceled))
                {
                    operation.EnsureTransition(DeploymentOperationState.Canceled, nowUtc);
                }
                else
                {
                    ForceRolledBack(operation, nowUtc, DeploymentCodes.InvalidTransition);
                }
            }

            timeline.Add("mark:failed-or-canceled");
            return Ok(action, operation.State, timeline);
        }

        if (action == DeploymentRecoveryAction.RecognizeWatchdogRollback)
        {
            foreach (IDeploymentRollbackDeviceRuntime runtime in devices)
            {
                DeploymentWatchdogExecutionResult cleaned = await runtime
                    .DisarmAndCleanupWatchdogAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (!cleaned.Succeeded)
                {
                    timeline.Add($"watchdog-cleanup-failed:{runtime.DeviceId.Value:D}:{cleaned.Code}");
                    if (!operation.IsTerminal
                        && DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RecoveryRequired))
                    {
                        operation.EnsureTransition(
                            DeploymentOperationState.RecoveryRequired,
                            nowUtc,
                            cleaned.Code);
                    }

                    return new DeploymentRecoveryResult
                    {
                        Succeeded = false,
                        Action = DeploymentRecoveryAction.RecoveryRequired,
                        State = operation.State,
                        ErrorCode = cleaned.Code,
                        Timeline = timeline,
                    };
                }

                timeline.Add($"watchdog-cleanup:{runtime.DeviceId.Value:D}");
            }

            if (!operation.IsTerminal)
            {
                ForceRolledBack(operation, nowUtc, DeploymentCodes.WatchdogRollbackDetected);
            }

            timeline.Add("recognize:watchdog-rollback");
            return Ok(action, operation.State, timeline);
        }

        // ControllerRollback — including crash after disarm before durable commit (AC#10).
        DeploymentRollbackResult rolled = await ExecuteDeploymentRollbackUseCase.ExecuteAsync(
            plan,
            operation,
            devices,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        timeline.AddRange(rolled.Timeline);
        return new DeploymentRecoveryResult
        {
            Succeeded = rolled.Succeeded,
            Action = action,
            State = rolled.State,
            ErrorCode = rolled.ErrorCode,
            Timeline = timeline,
        };
    }

    private static DeploymentAnchorSetState MergeAnchorState(DeploymentAnchorSetState current, DeploymentAnchorSetState next)
    {
        if (current == DeploymentAnchorSetState.ThirdTarget || next == DeploymentAnchorSetState.ThirdTarget)
        {
            return DeploymentAnchorSetState.ThirdTarget;
        }

        if (current == DeploymentAnchorSetState.Incomplete || next == DeploymentAnchorSetState.Incomplete)
        {
            return DeploymentAnchorSetState.Incomplete;
        }

        if (current == next)
        {
            return current;
        }

        return DeploymentAnchorSetState.MixedOldNew;
    }

    private static void ForceRolledBack(DeploymentOperation operation, DateTimeOffset nowUtc, string code)
    {
        if (operation.IsTerminal)
        {
            return;
        }

        if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RollbackPending))
        {
            operation.EnsureTransition(DeploymentOperationState.RollbackPending, nowUtc, code);
        }

        if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RollingBack))
        {
            operation.EnsureTransition(DeploymentOperationState.RollingBack, nowUtc, code);
        }

        if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RolledBack))
        {
            operation.EnsureTransition(DeploymentOperationState.RolledBack, nowUtc, code);
        }
    }

    private static DeploymentRecoveryResult Ok(
        DeploymentRecoveryAction action,
        DeploymentOperationState state,
        List<string> timeline)
        => new()
        {
            Succeeded = true,
            Action = action,
            State = state,
            Timeline = timeline,
        };
}
