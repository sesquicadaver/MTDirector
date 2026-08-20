using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;

namespace Mfc.Domain.Deployment;

/// <summary>Observed permanent-anchor set relative to a sealed deployment plan (Spec §46–§48).</summary>
public enum DeploymentAnchorSetState : byte
{
    AllOld = 0,
    AllNew = 1,
    MixedOldNew = 2,
    ThirdTarget = 3,
    Incomplete = 4,
}

/// <summary>Watchdog resource presence for crash/watchdog recovery (Spec §47).</summary>
public enum DeploymentWatchdogPresence : byte
{
    AbsentOrDisabled = 0,
    Active = 1,
}

/// <summary>Controller action from Spec §46–§49 recovery decision table.</summary>
public enum DeploymentRecoveryAction : byte
{
    /// <summary>Pre-activation, all anchors old — mark FAILED/CANCELED and cleanup watchdog.</summary>
    MarkFailedOrCanceled = 0,

    /// <summary>Controller-initiated rollback to old artifact.</summary>
    ControllerRollback = 1,

    /// <summary>Watchdog already restored old anchors; recognize and mark ROLLED_BACK.</summary>
    RecognizeWatchdogRollback = 2,

    /// <summary>Third/unknown anchor target — do not auto-mutate.</summary>
    RecoveryRequired = 3,

    /// <summary>Durable COMMITTED — keep new artifact state.</summary>
    KeepCommitted = 4,
}

/// <summary>
/// Pure deployment recovery decision table (Safe Deployment Spec §46–§49 / M4-11).
/// No RouterOS I/O; no automatic adoption of third targets.
/// </summary>
public static class DeploymentRecoveryDecision
{
    /// <summary>
    /// Classify live jump-targets against plan old/new sets.
    /// Third target → ThirdTarget (AC#7); mixed plan-valid → MixedOldNew (AC#6).
    /// </summary>
    public static DeploymentAnchorSetState ClassifyAnchors(
        IReadOnlyList<AnchorTarget> oldTargets,
        IReadOnlyList<AnchorTarget> newTargets,
        IReadOnlyDictionary<string, string> observedJumpByMarker)
    {
        ArgumentNullException.ThrowIfNull(oldTargets);
        ArgumentNullException.ThrowIfNull(newTargets);
        ArgumentNullException.ThrowIfNull(observedJumpByMarker);
        if (oldTargets.Count == 0 || oldTargets.Count != newTargets.Count)
        {
            throw new DomainInvariantException("Recovery classification requires matching old/new anchor sets.");
        }

        Dictionary<string, string> oldByMarker = oldTargets.ToDictionary(
            static t => t.Key.Marker,
            static t => t.JumpTarget,
            StringComparer.Ordinal);
        Dictionary<string, string> newByMarker = newTargets.ToDictionary(
            static t => t.Key.Marker,
            static t => t.JumpTarget,
            StringComparer.Ordinal);

        int oldHits = 0;
        int newHits = 0;
        foreach ((string marker, string expectedOld) in oldByMarker)
        {
            if (!newByMarker.TryGetValue(marker, out string? expectedNew))
            {
                throw new DomainInvariantException($"Missing new target for anchor '{marker}'.");
            }

            if (!observedJumpByMarker.TryGetValue(marker, out string? observed)
                || string.IsNullOrWhiteSpace(observed))
            {
                return DeploymentAnchorSetState.Incomplete;
            }

            string jump = observed.Trim();
            bool isOld = string.Equals(jump, expectedOld, StringComparison.Ordinal);
            bool isNew = string.Equals(jump, expectedNew, StringComparison.Ordinal);
            if (!isOld && !isNew)
            {
                return DeploymentAnchorSetState.ThirdTarget;
            }

            if (isOld)
            {
                oldHits++;
            }

            if (isNew)
            {
                newHits++;
            }
        }

        if (oldHits == oldByMarker.Count && newHits == 0)
        {
            return DeploymentAnchorSetState.AllOld;
        }

        if (newHits == oldByMarker.Count && oldHits == 0)
        {
            return DeploymentAnchorSetState.AllNew;
        }

        if (oldHits + newHits == oldByMarker.Count)
        {
            return DeploymentAnchorSetState.MixedOldNew;
        }

        return DeploymentAnchorSetState.Incomplete;
    }

    public static DeploymentWatchdogPresence ClassifyWatchdog(
        IReadOnlyList<string> schedulerNames,
        IReadOnlyDictionary<string, bool>? schedulerDisabled = null)
    {
        ArgumentNullException.ThrowIfNull(schedulerNames);
        foreach (string name in schedulerNames)
        {
            if (!DeploymentWatchdogNames.IsDeploymentWatchdogName(name)
                || name.StartsWith("mfc-rb-s-", StringComparison.Ordinal))
            {
                continue;
            }

            bool disabled = schedulerDisabled is not null
                            && schedulerDisabled.TryGetValue(name, out bool flag)
                            && flag;
            if (!disabled)
            {
                return DeploymentWatchdogPresence.Active;
            }
        }

        return DeploymentWatchdogPresence.AbsentOrDisabled;
    }

    /// <summary>
    /// Spec §47: watchdog rollback = anchors all-old AND deployment not committed.
    /// Spec §49: nonterminal after activation → ControllerRollback even if all-new.
    /// Spec §49.1: pre-activation all-old → MarkFailedOrCanceled.
    /// AC#11: only durable COMMITTED keeps new state.
    /// </summary>
    public static DeploymentRecoveryAction Decide(
        DeploymentAnchorSetState anchors,
        DeploymentWatchdogPresence watchdog,
        bool committed,
        bool activationStarted)
    {
        if (anchors == DeploymentAnchorSetState.ThirdTarget)
        {
            return DeploymentRecoveryAction.RecoveryRequired;
        }

        if (committed)
        {
            return DeploymentRecoveryAction.KeepCommitted;
        }

        if (!activationStarted)
        {
            if (anchors is DeploymentAnchorSetState.AllOld or DeploymentAnchorSetState.Incomplete)
            {
                return DeploymentRecoveryAction.MarkFailedOrCanceled;
            }

            // Unexpected pre-activation new/mixed → fail closed with controller rollback.
            return DeploymentRecoveryAction.ControllerRollback;
        }

        // After activation, nonterminal always rolls back to old (AC#9–#11 / Spec §49.2–§49.3),
        // unless watchdog already restored all-old (AC#8 / Spec §47).
        if (anchors == DeploymentAnchorSetState.AllOld
            && watchdog == DeploymentWatchdogPresence.AbsentOrDisabled)
        {
            return DeploymentRecoveryAction.RecognizeWatchdogRollback;
        }

        if (anchors == DeploymentAnchorSetState.AllOld && watchdog == DeploymentWatchdogPresence.Active)
        {
            // Local rollback may still be in progress; complete controller-side cleanup via rollback path.
            return DeploymentRecoveryAction.RecognizeWatchdogRollback;
        }

        return DeploymentRecoveryAction.ControllerRollback;
    }

    public static string? CodeFor(DeploymentRecoveryAction action)
        => action switch
        {
            DeploymentRecoveryAction.RecoveryRequired => DeploymentCodes.RecoveryRequired,
            DeploymentRecoveryAction.RecognizeWatchdogRollback => DeploymentCodes.WatchdogRollbackDetected,
            DeploymentRecoveryAction.ControllerRollback => null,
            DeploymentRecoveryAction.MarkFailedOrCanceled => null,
            DeploymentRecoveryAction.KeepCommitted => null,
            _ => DeploymentCodes.InvalidTransition,
        };

    /// <summary>Device rollback order is reverse activation order (AC#1 / Spec §46).</summary>
    public static IReadOnlyList<DeviceId> DeviceRollbackOrder(IReadOnlyList<DeviceId> activationOrder)
    {
        ArgumentNullException.ThrowIfNull(activationOrder);
        return activationOrder.Reverse().ToArray();
    }

    /// <summary>True when only durable COMMITTED may retain new artifact identity (AC#11).</summary>
    public static bool MayRetainNewArtifact(bool durableCommitted)
        => durableCommitted;
}
