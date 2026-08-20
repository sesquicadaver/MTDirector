namespace Mfc.Domain.Deployment;

/// <summary>Node deployment operation states (Safe Deployment Spec §13).</summary>
public enum DeploymentOperationState : byte
{
    Created = 0,
    Prechecking = 1,
    Staging = 2,
    Staged = 3,
    ArmingWatchdog = 4,
    WatchdogArmed = 5,
    Activating = 6,
    Verifying = 7,
    DisarmingWatchdog = 8,
    Committed = 9,
    RollbackPending = 10,
    RollingBack = 11,
    RolledBack = 12,
    Blocked = 13,
    NoChanges = 14,
    Canceled = 15,
    Failed = 16,
    RecoveryRequired = 17,
}

/// <summary>Per-device deployment states (Safe Deployment Spec §14).</summary>
public enum DeviceDeploymentState : byte
{
    Pending = 0,
    Prechecked = 1,
    Staging = 2,
    Staged = 3,
    WatchdogArmed = 4,
    Activating = 5,
    ActiveUnverified = 6,
    Verified = 7,
    WatchdogDisarmed = 8,
    Committed = 9,
    RollingBack = 10,
    RolledBack = 11,
    RecoveryRequired = 12,
}

/// <summary>Write-ahead step states (Safe Deployment Spec §16).</summary>
public enum DeploymentStepState : byte
{
    IntentRecorded = 0,
    EffectSent = 1,
    Verified = 2,
    Failed = 3,
}

/// <summary>Typed journal operations (Safe Deployment Spec §16). No free-form command strings.</summary>
public enum DeploymentStepKind : byte
{
    Precheck = 0,
    StageAddressList = 1,
    StageFilterChain = 2,
    ArmWatchdog = 3,
    ActivateAnchor = 4,
    Verify = 5,
    DisarmWatchdog = 6,
    Commit = 7,
    RollbackAnchor = 8,
    CleanupWatchdog = 9,
    MarkRecovery = 10,
}

/// <summary>Bounded verification probe kinds (Safe Deployment Spec §33). Only API_SSL and ROUTER_PING.</summary>
public enum DeploymentProbeKind : byte
{
    /// <summary>ROUTER_PING — ICMP via RouterOS <c>/ping</c> (Spec §33.2).</summary>
    RouterPing = 0,

    /// <summary>API_SSL — independent management reconnect (Spec §33.1).</summary>
    ApiSsl = 1,
}
