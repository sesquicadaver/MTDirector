using Mfc.Domain.Onboarding;

namespace Mfc.Domain.Deployment;

/// <summary>Precondition outcome for one permanent-anchor set (Safe Deployment Spec §30–§31).</summary>
public enum AnchorActivationAction : byte
{
    /// <summary>jump-target already equals desired new — step already applied.</summary>
    AlreadyApplied = 0,

    /// <summary>jump-target equals expected old — typed set is allowed.</summary>
    ReadyToSet = 1,

    /// <summary>Third/unknown target — controller must not rewrite; recovery required.</summary>
    RecoveryRequired = 2,

    /// <summary>Missing/invalid live anchor identity.</summary>
    PreconditionFailed = 3,
}

/// <summary>Decision for a single anchor before/after set (Spec §30).</summary>
public sealed class AnchorActivationDecision
{
    public required AnchorActivationAction Action { get; init; }

    public required string Code { get; init; }

    public required string? ObservedJumpTarget { get; init; }

    public required string ExpectedOld { get; init; }

    public required string DesiredNew { get; init; }
}

/// <summary>
/// Pure decisions for permanent-anchor jump-target activation (Safe Deployment Spec §30–§31 / M4-06).
/// Never performs RouterOS I/O.
/// </summary>
public static class AnchorActivationPlanner
{
    /// <summary>
    /// Classify live jump-target against plan old/new. Unknown third target → recovery (AC#6).
    /// </summary>
    public static AnchorActivationDecision Decide(
        string? observedJumpTarget,
        string expectedOld,
        string desiredNew,
        bool anchorIdentityValid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedOld);
        ArgumentException.ThrowIfNullOrWhiteSpace(desiredNew);
        string oldTarget = expectedOld.Trim();
        string newTarget = desiredNew.Trim();
        if (!anchorIdentityValid)
        {
            return new AnchorActivationDecision
            {
                Action = AnchorActivationAction.PreconditionFailed,
                Code = DeploymentCodes.AnchorPreconditionFailed,
                ObservedJumpTarget = observedJumpTarget,
                ExpectedOld = oldTarget,
                DesiredNew = newTarget,
            };
        }

        if (string.IsNullOrWhiteSpace(observedJumpTarget))
        {
            return new AnchorActivationDecision
            {
                Action = AnchorActivationAction.PreconditionFailed,
                Code = DeploymentCodes.AnchorPreconditionFailed,
                ObservedJumpTarget = observedJumpTarget,
                ExpectedOld = oldTarget,
                DesiredNew = newTarget,
            };
        }

        string observed = observedJumpTarget.Trim();
        if (string.Equals(observed, newTarget, StringComparison.Ordinal))
        {
            return new AnchorActivationDecision
            {
                Action = AnchorActivationAction.AlreadyApplied,
                Code = "ANCHOR_ALREADY_APPLIED",
                ObservedJumpTarget = observed,
                ExpectedOld = oldTarget,
                DesiredNew = newTarget,
            };
        }

        if (string.Equals(observed, oldTarget, StringComparison.Ordinal))
        {
            return new AnchorActivationDecision
            {
                Action = AnchorActivationAction.ReadyToSet,
                Code = "ANCHOR_READY",
                ObservedJumpTarget = observed,
                ExpectedOld = oldTarget,
                DesiredNew = newTarget,
            };
        }

        return new AnchorActivationDecision
        {
            Action = AnchorActivationAction.RecoveryRequired,
            Code = DeploymentCodes.RecoveryRequired,
            ObservedJumpTarget = observed,
            ExpectedOld = oldTarget,
            DesiredNew = newTarget,
        };
    }

    /// <summary>
    /// After an unknown set outcome, classify by re-read (Spec §31 / AC#7). Never implies a blind retry.
    /// </summary>
    public static AnchorActivationDecision ClassifyAfterUnknownSet(
        string? observedJumpTarget,
        string expectedOld,
        string desiredNew)
        => Decide(observedJumpTarget, expectedOld, desiredNew, anchorIdentityValid: true);

    /// <summary>
    /// One controlled retry is allowed only when re-read proves the target is still old (Spec §31).
    /// Blind retry when observation is missing/unknown is forbidden (AC#8).
    /// </summary>
    public static bool AllowsControlledRetry(AnchorActivationDecision afterUnknownSet)
    {
        ArgumentNullException.ThrowIfNull(afterUnknownSet);
        return afterUnknownSet.Action == AnchorActivationAction.ReadyToSet;
    }

    /// <summary>Watchdog remaining TTL must stay above the commit margin after each anchor (AC#10).</summary>
    public static bool HasWatchdogMargin(TimeSpan remainingTtl)
        => remainingTtl >= DeploymentCodes.MinCommitMargin;
}
