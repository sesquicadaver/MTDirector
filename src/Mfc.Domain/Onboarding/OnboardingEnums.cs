namespace Mfc.Domain.Onboarding;

/// <summary>OnboardingOperation.state (Onboarding Spec §5).</summary>
public enum OnboardingOperationState : byte
{
    Created = 0,
    Prechecking = 1,
    StagingBootstrapRoots = 2,
    StagingDisabledAnchors = 3,
    ArmingWatchdogs = 4,
    EnablingAnchors = 5,
    Verifying = 6,
    DisarmingWatchdogs = 7,
    Committed = 8,
    RollbackPending = 9,
    RollingBack = 10,
    RolledBack = 11,
    Blocked = 12,
    RecoveryRequired = 13,
}

/// <summary>OnboardingStep.state (Onboarding Spec §54).</summary>
public enum OnboardingStepState : byte
{
    IntentRecorded = 0,
    EffectSent = 1,
    Verified = 2,
    Failed = 3,
}

/// <summary>AnchorPlacement.mode (Onboarding Spec §20).</summary>
public enum AnchorPlacementMode : byte
{
    BeforeStaticRule = 0,
    Append = 1,
}

/// <summary>Typed journal step operation (Onboarding Spec §48 / §54).</summary>
public enum OnboardingStepKind : byte
{
    Precheck = 0,
    CreateBootstrapRoot = 1,
    CreateDisabledAnchor = 2,
    ArmWatchdog = 3,
    EnableAnchor = 4,
    Verify = 5,
    DisarmWatchdog = 6,
    Commit = 7,
    Rollback = 8,
    RemoveBootstrapRoot = 9,
    RemoveDisabledAnchor = 10,
    DisableAnchor = 11,
    CleanupWatchdog = 12,
    MarkRecovery = 13,
}
