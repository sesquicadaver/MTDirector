using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>Durable commit evidence retained after successful standalone deploy (AC#9).</summary>
public sealed class DeploymentCommitSnapshot
{
    public required DeploymentOperationId OperationId { get; init; }

    public required Hash256 PlanHash { get; init; }

    public required Hash256 NewArtifactHash { get; init; }

    public required Hash256 OldArtifactHash { get; init; }

    public required DateTimeOffset CommittedAtUtc { get; init; }
}

/// <summary>Outcome of <see cref="ExecuteStandaloneDeploymentUseCase"/>.</summary>
public sealed class StandaloneDeploymentResult
{
    public required bool Succeeded { get; init; }

    public required DeploymentOperationState State { get; init; }

    public string? ErrorCode { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }

    public required bool WroteToDevice { get; init; }

    public required bool WatchdogArmedBeforeActivation { get; init; }

    public required bool WatchdogDisarmedBeforeCommit { get; init; }

    public required bool DetachedArtifactPreservedOnFailure { get; init; }

    public DeploymentCommitSnapshot? CommitSnapshot { get; init; }
}

/// <summary>Per-device runtime ports for standalone deployment (M4-08).</summary>
public interface IStandaloneDeploymentDeviceRuntime
{
    DeviceId DeviceId { get; }

    IRouterOsDeploymentSession Session { get; }

    IDeploymentWatchdogPort Watchdog { get; }

    IDeploymentFreshSessionFactory FreshSessions { get; }

    Task<DeploymentSystemNameFacts> ReadSystemNamesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Coordinates LOCK → PRECHECK → STAGE → ARM → ACTIVATE → VERIFY → DISARM → COMMIT
/// for a standalone Router/Switch Node (Safe Deployment Spec §35 / M4-08).
/// </summary>
public static class ExecuteStandaloneDeploymentUseCase
{
    public static async Task<StandaloneDeploymentResult> ExecuteAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperation operation,
        DeviceDeployment deviceState,
        IStandaloneDeploymentDeviceRuntime runtime,
        IReadOnlyList<DeploymentOperation> existingForNode,
        IReadOnlyList<PacketPathPairFact> packetPathPairs,
        IReadOnlyList<AddressListArtifactDraft> addressLists,
        IReadOnlyList<ChainArtifactDraft> chains,
        Hash256 observedResourceHashAfterStaging,
        DateTimeOffset nowUtc,
        DateTimeOffset routerClock,
        TimeSpan? remainingWatchdogTtl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(deviceState);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(existingForNode);
        ArgumentNullException.ThrowIfNull(packetPathPairs);
        ArgumentNullException.ThrowIfNull(addressLists);
        ArgumentNullException.ThrowIfNull(chains);
        ArgumentNullException.ThrowIfNull(observedResourceHashAfterStaging);

        List<string> timeline = [];
        bool wrote = false;
        bool armedBeforeActivation = false;
        bool disarmedBeforeCommit = false;
        bool detachedPreserved = true;
        DeploymentWatchdogBundle? armed = null;

        try
        {
            StandaloneDeploymentPolicy.EnsureEligible(node, plan);
            DeviceDeploymentPlan devicePlan = plan.DevicePlans.Single();
            if (operation.NodeId != node.Id || plan.NodeId != node.Id || deviceState.DeviceId != devicePlan.DeviceId
                || runtime.DeviceId != devicePlan.DeviceId)
            {
                throw new DomainInvariantException("Standalone deployment node/plan/device/runtime mismatch.");
            }

            StandaloneDeploymentPolicy.RecheckPreconditions(
                node,
                plan,
                existingForNode,
                nowUtc,
                packetPathPairs);
            timeline.Add("precheck:revalidated");

            Advance(operation, DeploymentOperationState.Prechecking, nowUtc);
            deviceState.EnsureTransition(DeviceDeploymentState.Prechecked, nowUtc);

            if (StandaloneDeploymentPolicy.IsNoChanges(devicePlan))
            {
                Advance(operation, DeploymentOperationState.NoChanges, nowUtc);
                timeline.Add("no-changes");
                return Ok(
                    operation.State,
                    timeline,
                    wrote: false,
                    armedBeforeActivation: false,
                    disarmedBeforeCommit: false,
                    detachedPreserved: true,
                    commit: null);
            }

            deviceState.EnsureTransition(DeviceDeploymentState.Staging, nowUtc);
            Advance(operation, DeploymentOperationState.Staging, nowUtc);

            foreach (AddressListArtifactDraft list in addressLists)
            {
                AddressListStagingResult staged = await StageAddressListUseCase.ExecuteAsync(
                    list,
                    runtime.Session,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!staged.Succeeded)
                {
                    return await FailAsync(
                        operation,
                        deviceState,
                        staged.Code ?? DeploymentCodes.StagingResourceCollision,
                        nowUtc,
                        timeline,
                        wrote,
                        armedBeforeActivation,
                        disarmedBeforeCommit,
                        detachedPreserved,
                        recovery: false).ConfigureAwait(false);
                }

                if (staged.AddedCount > 0)
                {
                    wrote = true;
                }

                timeline.Add($"stage-al:{list.Name}");
            }

            if (chains.Count > 0)
            {
                DetachedChainsStagingResult stagedChains = await StageDetachedChainsUseCase.ExecuteAsync(
                    chains,
                    runtime.Session,
                    activeRootChainNames: null,
                    cancellationToken).ConfigureAwait(false);
                if (!stagedChains.Succeeded || !stagedChains.ArtifactStaged)
                {
                    return await FailAsync(
                        operation,
                        deviceState,
                        stagedChains.Code ?? DeploymentCodes.StagingResourceCollision,
                        nowUtc,
                        timeline,
                        wrote,
                        armedBeforeActivation,
                        disarmedBeforeCommit,
                        detachedPreserved,
                        recovery: false).ConfigureAwait(false);
                }

                if (stagedChains.TotalAddedCount > 0)
                {
                    wrote = true;
                }

                timeline.Add("stage-chains");
            }

            // Staging must not mutate permanent anchors (AC#3 — no active traffic cut-over yet).
            timeline.Add("stage:detached-only");
            deviceState.EnsureTransition(DeviceDeploymentState.Staged, nowUtc);
            Advance(operation, DeploymentOperationState.Staged, nowUtc);

            Advance(operation, DeploymentOperationState.ArmingWatchdog, nowUtc);
            DeploymentSystemNameFacts names = await runtime.ReadSystemNamesAsync(cancellationToken).ConfigureAwait(false);
            DeploymentWatchdogPlanResult planned = PlanDeploymentWatchdogUseCase.PlanWatchdog(
                operation.Id,
                devicePlan,
                names);
            if (planned.HasBlockers || planned.Watchdog is null)
            {
                string code = planned.Findings.Count > 0
                    ? planned.Findings[0].Code
                    : DeploymentCodes.WatchdogArmFailed;
                return await FailAsync(
                    operation,
                    deviceState,
                    code,
                    nowUtc,
                    timeline,
                    wrote,
                    armedBeforeActivation,
                    disarmedBeforeCommit,
                    detachedPreserved,
                    recovery: false).ConfigureAwait(false);
            }

            DeploymentWatchdogExecutionResult arm = await runtime.Watchdog.ArmWatchdogAsync(
                planned.Watchdog,
                routerClock,
                remainingWatchdogTtl,
                cancellationToken).ConfigureAwait(false);
            if (!arm.Succeeded)
            {
                return await FailAsync(
                    operation,
                    deviceState,
                    arm.Code,
                    nowUtc,
                    timeline,
                    wrote,
                    armedBeforeActivation,
                    disarmedBeforeCommit,
                    detachedPreserved,
                    recovery: false).ConfigureAwait(false);
            }

            armed = planned.Watchdog;
            wrote = true;
            armedBeforeActivation = true;
            timeline.Add("watchdog:armed");
            deviceState.EnsureTransition(DeviceDeploymentState.WatchdogArmed, nowUtc);
            Advance(operation, DeploymentOperationState.WatchdogArmed, nowUtc);

            Advance(operation, DeploymentOperationState.Activating, nowUtc);
            deviceState.EnsureTransition(DeviceDeploymentState.Activating, nowUtc);
            TimeSpan margin = remainingWatchdogTtl ?? devicePlan.RollbackTtl;
            AnchorActivationResult activated = await ActivateAnchorsUseCase.ExecuteAsync(
                devicePlan,
                runtime.Session,
                () => margin,
                cancellationToken).ConfigureAwait(false);
            if (!activated.Succeeded)
            {
                timeline.Add("activate:failed");
                return await RollbackAfterActivationAsync(
                    operation,
                    deviceState,
                    runtime,
                    devicePlan,
                    armed,
                    activated.Code ?? DeploymentCodes.AnchorSetFailed,
                    nowUtc,
                    timeline,
                    wrote: true,
                    armedBeforeActivation,
                    detachedPreserved: true,
                    activated.RecoveryRequired).ConfigureAwait(false);
            }

            timeline.Add("activate:done");
            deviceState.EnsureTransition(DeviceDeploymentState.ActiveUnverified, nowUtc);
            Advance(operation, DeploymentOperationState.Verifying, nowUtc);

            DeploymentVerificationResult verified = await VerifyDeploymentActivationUseCase.ExecuteAsync(
                devicePlan,
                priorSessionIdentity: runtime.Session,
                runtime.FreshSessions,
                observedResourceHashAfterStaging,
                armed,
                margin,
                cancellationToken).ConfigureAwait(false);
            if (!verified.Succeeded)
            {
                timeline.Add("verify:failed");
                return await RollbackAfterActivationAsync(
                    operation,
                    deviceState,
                    runtime,
                    devicePlan,
                    armed,
                    verified.Code ?? DeploymentCodes.DeploymentProbeFailed,
                    nowUtc,
                    timeline,
                    wrote: true,
                    armedBeforeActivation,
                    detachedPreserved: true,
                    recovery: verified.Code == DeploymentCodes.RecoveryRequired)
                    .ConfigureAwait(false);
            }

            timeline.Add("verify:passed");
            deviceState.EnsureTransition(DeviceDeploymentState.Verified, nowUtc);

            Advance(operation, DeploymentOperationState.DisarmingWatchdog, nowUtc);
            DeploymentWatchdogExecutionResult disarmed = await runtime.Watchdog.DisarmWatchdogAsync(
                armed,
                margin,
                cancellationToken).ConfigureAwait(false);
            if (!disarmed.Succeeded)
            {
                timeline.Add("watchdog:disarm-failed");
                return await RollbackAfterActivationAsync(
                    operation,
                    deviceState,
                    runtime,
                    devicePlan,
                    armed,
                    disarmed.Code,
                    nowUtc,
                    timeline,
                    wrote: true,
                    armedBeforeActivation,
                    detachedPreserved: true,
                    recovery: true).ConfigureAwait(false);
            }

            disarmedBeforeCommit = true;
            timeline.Add("watchdog:disarmed");
            deviceState.EnsureTransition(DeviceDeploymentState.WatchdogDisarmed, nowUtc);

            DateTimeOffset committedAt = nowUtc.ToUniversalTime();
            DeploymentCommitSnapshot snapshot = new()
            {
                OperationId = operation.Id,
                PlanHash = plan.PlanHash,
                NewArtifactHash = devicePlan.NewArtifactHash,
                OldArtifactHash = devicePlan.OldArtifactHash,
                CommittedAtUtc = committedAt,
            };
            // Old artifact hash retained on the snapshot for rollback history (AC#7).
            timeline.Add($"commit:{snapshot.NewArtifactHash}");
            deviceState.EnsureTransition(DeviceDeploymentState.Committed, nowUtc);
            Advance(operation, DeploymentOperationState.Committed, nowUtc);

            return Ok(
                operation.State,
                timeline,
                wrote: true,
                armedBeforeActivation,
                disarmedBeforeCommit,
                detachedPreserved: true,
                snapshot);
        }
        catch (Exception ex) when (ex is DomainInvariantException or InvalidOperationException)
        {
            string code = ex is DomainInvariantException dix && dix.Message.Contains(':', StringComparison.Ordinal)
                ? dix.Message.Split(':', 2)[0]
                : DeploymentCodes.InvalidTransition;
            return await FailAsync(
                operation,
                deviceState,
                code,
                nowUtc,
                timeline,
                wrote,
                armedBeforeActivation,
                disarmedBeforeCommit,
                detachedPreserved,
                recovery: false).ConfigureAwait(false);
        }
    }

    private static async Task<StandaloneDeploymentResult> RollbackAfterActivationAsync(
        DeploymentOperation operation,
        DeviceDeployment deviceState,
        IStandaloneDeploymentDeviceRuntime runtime,
        DeviceDeploymentPlan devicePlan,
        DeploymentWatchdogBundle? armed,
        string errorCode,
        DateTimeOffset nowUtc,
        List<string> timeline,
        bool wrote,
        bool armedBeforeActivation,
        bool detachedPreserved,
        bool recovery)
    {
        Advance(operation, DeploymentOperationState.RollbackPending, nowUtc, errorCode);
        if (recovery)
        {
            if (!deviceState.IsTerminal)
            {
                try
                {
                    deviceState.EnsureTransition(DeviceDeploymentState.RecoveryRequired, nowUtc);
                }
                catch (DomainInvariantException)
                {
                    // Device may already be mid-activation; leave as-is and mark operation recovery.
                }
            }

            Advance(operation, DeploymentOperationState.RecoveryRequired, nowUtc, errorCode);
            timeline.Add("recovery-required");
            return FailResult(
                operation.State,
                errorCode,
                timeline,
                wrote,
                armedBeforeActivation,
                disarmedBeforeCommit: false,
                detachedPreserved);
        }

        Advance(operation, DeploymentOperationState.RollingBack, nowUtc, errorCode);
        if (!deviceState.IsTerminal
            && deviceState.State is DeviceDeploymentState.Activating
                or DeviceDeploymentState.ActiveUnverified
                or DeviceDeploymentState.Verified
                or DeviceDeploymentState.WatchdogArmed)
        {
            deviceState.EnsureTransition(DeviceDeploymentState.RollingBack, nowUtc);
        }

        // Restore old jump-targets only — never remove detached staged resources (AC#7 / AC#8).
        foreach (AnchorKey key in devicePlan.AnchorRollbackOrder)
        {
            AnchorTarget old = devicePlan.OldAnchorTargets.Single(t => t.Key.Equals(key));
            DeploymentWriteExecutionResult restored = await runtime.Session.SetAnchorTargetAsync(
                new AnchorTargetWrite(old.Key.Family, old.Key.Chain, old.JumpTarget),
                default).ConfigureAwait(false);
            timeline.Add(restored.Succeeded
                ? $"rollback-anchor:{old.Key.Marker}"
                : $"rollback-anchor-failed:{old.Key.Marker}");
            if (!restored.Succeeded)
            {
                Advance(operation, DeploymentOperationState.RecoveryRequired, nowUtc, DeploymentCodes.RecoveryRequired);
                return FailResult(
                    operation.State,
                    DeploymentCodes.RecoveryRequired,
                    timeline,
                    wrote,
                    armedBeforeActivation,
                    disarmedBeforeCommit: false,
                    detachedPreserved);
            }
        }

        if (armed is not null)
        {
            _ = await runtime.Watchdog.DisarmWatchdogAsync(armed, cancellationToken: default).ConfigureAwait(false);
            timeline.Add("watchdog:disarmed-on-rollback");
        }

        if (!deviceState.IsTerminal)
        {
            deviceState.EnsureTransition(DeviceDeploymentState.RolledBack, nowUtc);
        }

        Advance(operation, DeploymentOperationState.RolledBack, nowUtc, errorCode);
        timeline.Add("rolled-back");
        return FailResult(
            operation.State,
            errorCode,
            timeline,
            wrote,
            armedBeforeActivation,
            disarmedBeforeCommit: false,
            detachedPreserved);
    }

    private static Task<StandaloneDeploymentResult> FailAsync(
        DeploymentOperation operation,
        DeviceDeployment deviceState,
        string code,
        DateTimeOffset nowUtc,
        List<string> timeline,
        bool wrote,
        bool armedBeforeActivation,
        bool disarmedBeforeCommit,
        bool detachedPreserved,
        bool recovery)
    {
        if (!operation.IsTerminal)
        {
            DeploymentOperationState next = recovery
                ? DeploymentOperationState.RecoveryRequired
                : operation.State is DeploymentOperationState.Prechecking
                    ? DeploymentOperationState.Failed
                    : DeploymentOperationState.RollbackPending;
            if (DeploymentOperation.CanTransition(operation.State, next))
            {
                Advance(operation, next, nowUtc, code);
            }
            else if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.Failed))
            {
                Advance(operation, DeploymentOperationState.Failed, nowUtc, code);
            }
            else if (DeploymentOperation.CanTransition(operation.State, DeploymentOperationState.RecoveryRequired))
            {
                Advance(operation, DeploymentOperationState.RecoveryRequired, nowUtc, code);
            }
        }

        timeline.Add($"fail:{code}");
        return Task.FromResult(FailResult(
            operation.State,
            code,
            timeline,
            wrote,
            armedBeforeActivation,
            disarmedBeforeCommit,
            detachedPreserved));
    }

    private static void Advance(
        DeploymentOperation operation,
        DeploymentOperationState next,
        DateTimeOffset nowUtc,
        string? errorCode = null)
        => operation.EnsureTransition(next, nowUtc, errorCode);

    private static StandaloneDeploymentResult Ok(
        DeploymentOperationState state,
        List<string> timeline,
        bool wrote,
        bool armedBeforeActivation,
        bool disarmedBeforeCommit,
        bool detachedPreserved,
        DeploymentCommitSnapshot? commit)
        => new()
        {
            Succeeded = true,
            State = state,
            Timeline = timeline,
            WroteToDevice = wrote,
            WatchdogArmedBeforeActivation = armedBeforeActivation,
            WatchdogDisarmedBeforeCommit = disarmedBeforeCommit,
            DetachedArtifactPreservedOnFailure = detachedPreserved,
            CommitSnapshot = commit,
        };

    private static StandaloneDeploymentResult FailResult(
        DeploymentOperationState state,
        string code,
        List<string> timeline,
        bool wrote,
        bool armedBeforeActivation,
        bool disarmedBeforeCommit,
        bool detachedPreserved)
        => new()
        {
            Succeeded = false,
            State = state,
            ErrorCode = code,
            Timeline = timeline,
            WroteToDevice = wrote,
            WatchdogArmedBeforeActivation = armedBeforeActivation,
            WatchdogDisarmedBeforeCommit = disarmedBeforeCommit,
            DetachedArtifactPreservedOnFailure = detachedPreserved,
            CommitSnapshot = null,
        };
}
