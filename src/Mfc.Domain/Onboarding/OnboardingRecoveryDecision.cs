using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>Observed permanent-anchor set for Spec §46 recovery.</summary>
public enum OnboardingAnchorSetState : byte
{
    Absent = 0,
    AllDisabledBootstrap = 1,
    AllEnabledBootstrap = 2,
    MixedEnablement = 3,
    UnexpectedTarget = 4,
    CommittedMissing = 5,
    CommittedDisabled = 6,
}

/// <summary>Watchdog schedulers for Spec §46 (deadline/startup).</summary>
public enum OnboardingWatchdogPresence : byte
{
    AbsentOrDisabled = 0,
    Active = 1,
}

/// <summary>Controller action from Spec §46.</summary>
public enum OnboardingRecoveryAction : byte
{
    CleanupRolledBack = 0,
    ControllerRollback = 1,
    RecoveryRequired = 2,
    KeepManaged = 3,
    CriticalDrift = 4,
}

/// <summary>Pure Spec §46 decision (no RouterOS I/O, no automatic adoption).</summary>
public static class OnboardingRecoveryDecision
{
    public static OnboardingAnchorSetState ClassifyAnchors(
        IReadOnlyList<AnchorKey> required,
        IReadOnlyList<ActualFilterRule> live,
        bool committed)
    {
        ArgumentNullException.ThrowIfNull(required);
        ArgumentNullException.ThrowIfNull(live);
        if (required.Count == 0)
        {
            throw new DomainInvariantException("Recovery classification requires a non-empty anchor set.");
        }

        int present = 0;
        int enabled = 0;
        int disabled = 0;
        int missing = 0;
        foreach (AnchorKey key in required)
        {
            ActualFilterRule[] matches = live
                .Where(r => string.Equals(r.Comment, key.Marker, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length > 1)
            {
                return OnboardingAnchorSetState.UnexpectedTarget;
            }

            if (matches.Length == 0)
            {
                missing++;
                continue;
            }

            ActualFilterRule rule = matches[0];
            string expectedChain = BuiltinName(key.Chain);
            string expectedTarget = BootstrapArtifact.RootChainName(key.Family, key.Chain);
            if (!string.Equals(rule.Chain, expectedChain, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(rule.Action, "jump", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(rule.JumpTarget, expectedTarget, StringComparison.Ordinal))
            {
                return OnboardingAnchorSetState.UnexpectedTarget;
            }

            present++;
            if (rule.Disabled)
            {
                disabled++;
            }
            else
            {
                enabled++;
            }
        }

        if (committed)
        {
            if (missing > 0)
            {
                return OnboardingAnchorSetState.CommittedMissing;
            }

            if (disabled > 0)
            {
                return OnboardingAnchorSetState.CommittedDisabled;
            }

            return OnboardingAnchorSetState.AllEnabledBootstrap;
        }

        if (present == 0)
        {
            return OnboardingAnchorSetState.Absent;
        }

        if (missing > 0 || (enabled > 0 && disabled > 0))
        {
            return OnboardingAnchorSetState.MixedEnablement;
        }

        if (disabled == present)
        {
            return OnboardingAnchorSetState.AllDisabledBootstrap;
        }

        return OnboardingAnchorSetState.AllEnabledBootstrap;
    }

    public static OnboardingWatchdogPresence ClassifyWatchdog(OnboardingSystemNameFacts names)
    {
        ArgumentNullException.ThrowIfNull(names);
        foreach (string name in names.SchedulerNames)
        {
            if (!OnboardingWatchdogNames.IsOnboardingWatchdogName(name)
                || name.StartsWith("mfc-ob-s-", StringComparison.Ordinal))
            {
                continue;
            }

            bool disabled = names.SchedulerDisabled.TryGetValue(name, out bool flag) && flag;
            if (!disabled)
            {
                return OnboardingWatchdogPresence.Active;
            }
        }

        return OnboardingWatchdogPresence.AbsentOrDisabled;
    }

    public static OnboardingRecoveryAction Decide(
        OnboardingAnchorSetState anchors,
        OnboardingWatchdogPresence watchdog,
        bool committed)
    {
        if (anchors == OnboardingAnchorSetState.UnexpectedTarget)
        {
            return OnboardingRecoveryAction.RecoveryRequired;
        }

        if (committed)
        {
            if (anchors is OnboardingAnchorSetState.AllEnabledBootstrap)
            {
                return OnboardingRecoveryAction.KeepManaged;
            }

            return OnboardingRecoveryAction.CriticalDrift;
        }

        if (anchors is OnboardingAnchorSetState.AllEnabledBootstrap
            or OnboardingAnchorSetState.MixedEnablement)
        {
            return OnboardingRecoveryAction.ControllerRollback;
        }

        _ = watchdog;
        return OnboardingRecoveryAction.CleanupRolledBack;
    }

    public static string? CodeFor(OnboardingRecoveryAction action)
        => action switch
        {
            OnboardingRecoveryAction.RecoveryRequired => OnboardingCodes.UnexpectedAnchorTarget,
            OnboardingRecoveryAction.CriticalDrift => OnboardingCodes.OnboardingCriticalDrift,
            _ => null,
        };

    private static string BuiltinName(FilterBuiltInContext chain)
        => chain switch
        {
            FilterBuiltInContext.Input => "input",
            FilterBuiltInContext.Forward => "forward",
            FilterBuiltInContext.Output => "output",
            _ => throw new DomainInvariantException($"Unsupported recovery chain '{chain}'."),
        };
}
