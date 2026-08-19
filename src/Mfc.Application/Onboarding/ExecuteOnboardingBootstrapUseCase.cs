using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Onboarding;

/// <summary>Per-device closed session used by onboarding execution (M5-07). No free-form commands.</summary>
public interface IOnboardingDeviceSession
{
    DeviceId DeviceId { get; }

    IOnboardingBootstrapWritePort Bootstrap { get; }

    IOnboardingWatchdogPort Watchdog { get; }

    Task<IReadOnlyList<ActualFilterRule>> PrintFilterAsync(CancellationToken cancellationToken = default);

    Task<OnboardingSystemNameFacts> PrintSystemNamesAsync(CancellationToken cancellationToken = default);

    Task<OnboardingAuxiliarySnapshot> PrintAuxiliaryAsync(CancellationToken cancellationToken = default);

    Task<bool> ReconnectManagementAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActualFilterRule>> CaptureStableAsync(CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="ExecuteOnboardingBootstrapUseCase"/>.</summary>
public sealed class OnboardingExecutionResult
{
    public required bool Succeeded { get; init; }

    public required OnboardingOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }

    public required bool CapturePerformed { get; init; }

    public required bool WatchdogsDisarmed { get; init; }

    public required bool NodeManaged { get; init; }
}

/// <summary>
/// Executes staging, watchdog arming, normative enable, verification, disarm, and commit (M5-07).
/// Indeterminate/failed equivalence records ROLLBACK_PENDING; <see cref="RollbackOnboardingBootstrapUseCase"/> performs resource rollback.
/// </summary>
public static class ExecuteOnboardingBootstrapUseCase
{
    public static async Task<OnboardingExecutionResult> ExecuteAsync(
        Node node,
        OnboardingPlan plan,
        OnboardingOperation operation,
        IReadOnlyList<IOnboardingDeviceSession> sessions,
        DateTimeOffset nowUtc,
        DateTimeOffset routerClock,
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
                throw new DomainInvariantException("Onboarding execution node/plan/operation mismatch.");
            }

            Dictionary<DeviceId, IOnboardingDeviceSession> byDevice = sessions.ToDictionary(static s => s.DeviceId);
            DeviceOnboardingPlan[] devicePlans = plan.DevicePlans
                .OrderBy(static p => p.DeviceId.Value)
                .ToArray();
            if (devicePlans.Length == 0 || devicePlans.Any(p => !byDevice.ContainsKey(p.DeviceId)))
            {
                throw new DomainInvariantException("Every device plan must have an onboarding session.");
            }

            Dictionary<DeviceId, OnboardingAuxiliarySnapshot> auxiliaryBefore = [];
            Dictionary<DeviceId, IReadOnlyList<ActualFilterRule>> filterBefore = [];
            Dictionary<DeviceId, OnboardingWatchdogBundle> watchdogs = [];
            Dictionary<DeviceId, OnboardingBootstrapWritePlan> writePlans = [];
            foreach (DeviceOnboardingPlan devicePlan in devicePlans)
            {
                IOnboardingDeviceSession session = byDevice[devicePlan.DeviceId];
                auxiliaryBefore[devicePlan.DeviceId] = await session.PrintAuxiliaryAsync(cancellationToken).ConfigureAwait(false);
                filterBefore[devicePlan.DeviceId] = await session.PrintFilterAsync(cancellationToken).ConfigureAwait(false);
                OnboardingBootstrapWritePlan writes = PlanOnboardingBootstrapWritesUseCase.Execute(
                    devicePlan,
                    filterBefore[devicePlan.DeviceId]);
                if (writes.HasBlockers)
                {
                    return await BlockAsync(operation, writes.Findings[0].Code, nowUtc, timeline).ConfigureAwait(false);
                }

                writePlans[devicePlan.DeviceId] = writes;
            }

            Advance(operation, OnboardingOperationState.Prechecking, nowUtc);
            Advance(operation, OnboardingOperationState.StagingBootstrapRoots, nowUtc);

            foreach (DeviceOnboardingPlan devicePlan in devicePlans)
            {
                IOnboardingDeviceSession session = byDevice[devicePlan.DeviceId];
                IReadOnlyList<ActualFilterRule> live = await session.PrintFilterAsync(cancellationToken).ConfigureAwait(false);
                foreach (OnboardingBootstrapWrite write in writePlans[devicePlan.DeviceId].Writes
                             .Where(static w => w.Kind == OnboardingBootstrapWriteKind.AddBootstrapReturn))
                {
                    await RequireWriteAsync(session, write, live, cancellationToken).ConfigureAwait(false);
                    live = await session.PrintFilterAsync(cancellationToken).ConfigureAwait(false);
                    timeline.Add($"root:{devicePlan.DeviceId.Value:D}");
                }
            }

            Advance(operation, OnboardingOperationState.StagingDisabledAnchors, nowUtc);
            foreach (DeviceOnboardingPlan devicePlan in devicePlans)
            {
                IOnboardingDeviceSession session = byDevice[devicePlan.DeviceId];
                IReadOnlyList<ActualFilterRule> live = await session.PrintFilterAsync(cancellationToken).ConfigureAwait(false);
                foreach (OnboardingBootstrapWrite write in writePlans[devicePlan.DeviceId].Writes
                             .Where(static w => w.Kind == OnboardingBootstrapWriteKind.AddDisabledAnchor))
                {
                    await RequireWriteAsync(session, write, live, cancellationToken).ConfigureAwait(false);
                    live = await session.PrintFilterAsync(cancellationToken).ConfigureAwait(false);
                    ActualFilterRule? added = live.FirstOrDefault(r =>
                        string.Equals(r.Comment, write.AnchorMarker, StringComparison.Ordinal));
                    if (added is null || !added.Disabled)
                    {
                        throw new InvalidOperationException("Permanent anchor was not staged disabled.");
                    }

                    timeline.Add($"anchor-disabled:{write.AnchorMarker}");
                }
            }

            Advance(operation, OnboardingOperationState.ArmingWatchdogs, nowUtc);
            foreach (DeviceOnboardingPlan devicePlan in devicePlans)
            {
                IOnboardingDeviceSession session = byDevice[devicePlan.DeviceId];
                OnboardingWatchdogPlanResult planned = PlanOnboardingWatchdogUseCase.PlanWatchdog(
                    operation.Id,
                    devicePlan,
                    await session.PrintSystemNamesAsync(cancellationToken).ConfigureAwait(false));
                if (planned.HasBlockers || planned.Watchdog is null)
                {
                    return await RollbackAsync(
                        operation,
                        planned.Findings.Count > 0 ? planned.Findings[0].Code : OnboardingCodes.OnboardingWatchdogCollision,
                        nowUtc,
                        timeline).ConfigureAwait(false);
                }

                OnboardingWatchdogExecutionResult armed = await session.Watchdog.ArmWatchdogAsync(
                    planned.Watchdog,
                    routerClock,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!armed.Succeeded)
                {
                    return await RollbackAsync(operation, armed.Code, nowUtc, timeline).ConfigureAwait(false);
                }

                watchdogs[devicePlan.DeviceId] = planned.Watchdog;
                timeline.Add($"arm:{devicePlan.DeviceId.Value:D}");
            }

            Advance(operation, OnboardingOperationState.EnablingAnchors, nowUtc);
            foreach (DeviceOnboardingPlan devicePlan in devicePlans)
            {
                IOnboardingDeviceSession session = byDevice[devicePlan.DeviceId];
                HashSet<string> enabled = new(StringComparer.Ordinal);
                IpAddressFamily managementFamily = devicePlan.RequiredAnchorSet.Any(static k => k.Family == IpAddressFamily.IPv4)
                    ? IpAddressFamily.IPv4
                    : IpAddressFamily.IPv6;
                bool reconnected = false;
                foreach (AnchorKey key in OnboardingEnableOrder.Sort(devicePlan.RequiredAnchorSet))
                {
                    IReadOnlyList<ActualFilterRule> live = await session.PrintFilterAsync(cancellationToken).ConfigureAwait(false);
                    OnboardingBootstrapWrite enable = OnboardingBootstrapWrite.SetAnchorDisabled(key.Family, key.Chain, disabled: false);
                    OnboardingBootstrapWriteExecutionResult written = await session.Bootstrap.ApplyAsync(enable, live, cancellationToken)
                        .ConfigureAwait(false);
                    if (!written.Succeeded || !IsEnabledFlag(written.ReadBack.GetValueOrDefault("disabled")))
                    {
                        return await RollbackAsync(
                            operation,
                            OnboardingCodes.RollbackFailed,
                            nowUtc,
                            timeline).ConfigureAwait(false);
                    }

                    timeline.Add($"enable:{key.Marker}");
                    enabled.Add(key.Marker);
                    if (!reconnected
                        && enabled.Contains(new AnchorKey(managementFamily, FilterBuiltInContext.Output).Marker)
                        && enabled.Contains(new AnchorKey(managementFamily, FilterBuiltInContext.Input).Marker))
                    {
                        if (!await session.ReconnectManagementAsync(cancellationToken).ConfigureAwait(false))
                        {
                            return await RollbackAsync(
                                operation,
                                OnboardingCodes.OnboardingManagementReconnectFailed,
                                nowUtc,
                                timeline).ConfigureAwait(false);
                        }

                        timeline.Add($"reconnect:{devicePlan.DeviceId.Value:D}");
                        reconnected = true;
                    }
                }
            }

            Advance(operation, OnboardingOperationState.Verifying, nowUtc);
            bool captured = false;
            foreach (DeviceOnboardingPlan devicePlan in devicePlans)
            {
                IOnboardingDeviceSession session = byDevice[devicePlan.DeviceId];
                IReadOnlyList<ActualFilterRule> capturedFilter = await session.CaptureStableAsync(cancellationToken)
                    .ConfigureAwait(false);
                captured = true;
                timeline.Add($"capture:{devicePlan.DeviceId.Value:D}");
                OnboardingAuxiliarySnapshot auxiliaryAfter = await session.PrintAuxiliaryAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (!auxiliaryBefore[devicePlan.DeviceId].EqualsSnapshot(auxiliaryAfter))
                {
                    return await RollbackAsync(
                        operation,
                        OnboardingCodes.OnboardingAuxiliaryMutated,
                        nowUtc,
                        timeline,
                        captured).ConfigureAwait(false);
                }

                OnboardingEquivalenceResult equivalence = OnboardingPassThroughEquivalence.Evaluate(
                    filterBefore[devicePlan.DeviceId],
                    capturedFilter);
                if (equivalence.RequiresRollback)
                {
                    return await RollbackAsync(
                        operation,
                        equivalence.Code ?? OnboardingCodes.BootstrapSemanticEquivalenceNotProven,
                        nowUtc,
                        timeline,
                        captured).ConfigureAwait(false);
                }
            }

            Advance(operation, OnboardingOperationState.DisarmingWatchdogs, nowUtc);
            foreach (DeviceOnboardingPlan devicePlan in devicePlans)
            {
                IOnboardingDeviceSession session = byDevice[devicePlan.DeviceId];
                OnboardingWatchdogExecutionResult disarmed = await session.Watchdog.DisarmWatchdogAsync(
                    watchdogs[devicePlan.DeviceId],
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!disarmed.Succeeded)
                {
                    return await RollbackAsync(operation, disarmed.Code, nowUtc, timeline, captured).ConfigureAwait(false);
                }

                timeline.Add($"disarm:{devicePlan.DeviceId.Value:D}");
            }

            foreach (Device device in node.Devices.Where(static d => d.Enabled).OrderBy(static d => d.Id.Value))
            {
                device.SetManagementState(ManagementState.Managed);
            }

            node.SetManagementState(ManagementState.Managed);
            Advance(operation, OnboardingOperationState.Committed, nowUtc);
            return new OnboardingExecutionResult
            {
                Succeeded = true,
                State = operation.State,
                Timeline = timeline,
                CapturePerformed = captured,
                WatchdogsDisarmed = true,
                NodeManaged = node.ManagementState == ManagementState.Managed,
            };
        }
        catch (InvalidOperationException ex)
        {
            return await RollbackAsync(
                operation,
                OnboardingCodes.RollbackFailed,
                nowUtc,
                timeline,
                capturePerformed: timeline.Any(static t => t.StartsWith("capture:", StringComparison.Ordinal)),
                error: ex.Message).ConfigureAwait(false);
        }
    }

    private static bool IsEnabledFlag(string? raw)
        => raw is "no" or "false" or "0" or null;

    private static void Advance(OnboardingOperation operation, OnboardingOperationState next, DateTimeOffset nowUtc)
    {
        if (operation.State == next)
        {
            return;
        }

        operation.EnsureTransition(next, nowUtc);
    }

    private static async Task RequireWriteAsync(
        IOnboardingDeviceSession session,
        OnboardingBootstrapWrite write,
        IReadOnlyList<ActualFilterRule> live,
        CancellationToken cancellationToken)
    {
        OnboardingBootstrapWriteExecutionResult result = await session.Bootstrap.ApplyAsync(write, live, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Error ?? "Onboarding write failed.");
        }
    }

    private static Task<OnboardingExecutionResult> BlockAsync(
        OnboardingOperation operation,
        string code,
        DateTimeOffset nowUtc,
        List<string> timeline)
    {
        if (operation.State == OnboardingOperationState.Created)
        {
            operation.EnsureTransition(OnboardingOperationState.Prechecking, nowUtc);
        }

        operation.EnsureTransition(OnboardingOperationState.Blocked, nowUtc, code);
        timeline.Add($"blocked:{code}");
        return Task.FromResult(new OnboardingExecutionResult
        {
            Succeeded = false,
            State = operation.State,
            ErrorCode = code,
            Timeline = timeline,
            CapturePerformed = false,
            WatchdogsDisarmed = false,
            NodeManaged = false,
        });
    }

    private static Task<OnboardingExecutionResult> RollbackAsync(
        OnboardingOperation operation,
        string code,
        DateTimeOffset nowUtc,
        List<string> timeline,
        bool capturePerformed = false,
        string? error = null)
    {
        if (operation.State is OnboardingOperationState.Created)
        {
            operation.EnsureTransition(OnboardingOperationState.Prechecking, nowUtc);
        }

        if (operation.State is OnboardingOperationState.Prechecking)
        {
            operation.EnsureTransition(OnboardingOperationState.Blocked, nowUtc, code);
        }
        else if (!OnboardingOperation.IsTerminalState(operation.State)
                 && operation.State != OnboardingOperationState.RollbackPending)
        {
            operation.EnsureTransition(OnboardingOperationState.RollbackPending, nowUtc, code);
        }

        timeline.Add($"rollback:{code}");
        if (error is not null)
        {
            timeline.Add($"error:{error}");
        }

        return Task.FromResult(new OnboardingExecutionResult
        {
            Succeeded = false,
            State = operation.State,
            ErrorCode = code,
            Timeline = timeline,
            CapturePerformed = capturePerformed,
            WatchdogsDisarmed = false,
            NodeManaged = false,
        });
    }
}
