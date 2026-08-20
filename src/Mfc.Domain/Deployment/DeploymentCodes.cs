namespace Mfc.Domain.Deployment;

/// <summary>Stable deployment codes (Safe Deployment Spec §9–§16 / Issue Set M4-01).</summary>
public static class DeploymentCodes
{
    public const string PlanHashPrefix = "mfc.deployment.plan.v1";

    public const string SchemaVersion = "mfc.deployment.schema.v1";

    public const string CompilerVersionSlot = "mfc.compiler.v1";

    public static readonly TimeSpan DefaultPlanLifetime = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan MinRollbackTtl = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan DefaultRollbackTtl = TimeSpan.FromSeconds(180);

    public static readonly TimeSpan MaxRollbackTtl = TimeSpan.FromSeconds(600);

    public static readonly TimeSpan DefaultLockLease = TimeSpan.FromMinutes(2);

    public const string InvalidTransition = "DEPLOYMENT_INVALID_TRANSITION";

    public const string PlanExpired = "DEPLOYMENT_PLAN_EXPIRED";

    public const string PlanHashMismatch = "DEPLOYMENT_PLAN_HASH_MISMATCH";

    public const string NonterminalExists = "DEPLOYMENT_NONTERMINAL_EXISTS";

    public const string NodeDisabled = "DEPLOYMENT_NODE_DISABLED";

    public const string DevicePlanCardinality = "DEPLOYMENT_DEVICE_PLAN_CARDINALITY";

    public const string TerminalImmutable = "DEPLOYMENT_TERMINAL_IMMUTABLE";

    public const string StepInvalidTransition = "DEPLOYMENT_STEP_INVALID_TRANSITION";

    public const string LockHeld = "DEPLOYMENT_LOCK_HELD";

    public const string LockOwnerMismatch = "DEPLOYMENT_LOCK_OWNER_MISMATCH";

    public const string LockExpired = "DEPLOYMENT_LOCK_EXPIRED";

    public const string DevicesNotCommitted = "DEPLOYMENT_DEVICES_NOT_COMMITTED";

    public const string CampaignForbidden = "DEPLOYMENT_CAMPAIGN_FORBIDDEN";

    public const string RollbackTtlOutOfRange = "DEPLOYMENT_ROLLBACK_TTL_OUT_OF_RANGE";

    public const string ActivationOrderInvalid = "DEPLOYMENT_ACTIVATION_ORDER_INVALID";

    public const string PacketPathBlocked = "DEPLOYMENT_PACKET_PATH_BLOCKED";

    public const string StagingResourceCollision = "STAGING_RESOURCE_COLLISION";

    public const string StagingPrefixDiverged = "STAGING_PREFIX_DIVERGED";

    public const string StagingArtifactHashMismatch = "STAGING_ARTIFACT_HASH_MISMATCH";

    public const string StagingRuleInvalid = "STAGING_RULE_INVALID";

    public const string StagingLimitExceeded = "STAGING_LIMIT_EXCEEDED";

    public static readonly TimeSpan MinCommitMargin = TimeSpan.FromSeconds(30);

    public const string WatchdogScriptCollision = "WATCHDOG_SCRIPT_COLLISION";

    public const string WatchdogScriptInvalid = "WATCHDOG_SCRIPT_INVALID";

    public const string WatchdogSchedulerCollision = "WATCHDOG_SCHEDULER_COLLISION";

    public const string WatchdogArmFailed = "WATCHDOG_ARM_FAILED";

    public const string WatchdogDeadlineTooClose = "WATCHDOG_DEADLINE_TOO_CLOSE";

    public const string WatchdogDisableFailed = "WATCHDOG_DISABLE_FAILED";

    public const string WatchdogCleanupIncomplete = "WATCHDOG_CLEANUP_INCOMPLETE";

    public const string WatchdogNotArmed = "WATCHDOG_NOT_ARMED";

    public const string TransitionStateUnsafe = "TRANSITION_STATE_UNSAFE";

    public const string AnchorPreconditionFailed = "ANCHOR_PRECONDITION_FAILED";

    public const string AnchorSetFailed = "ANCHOR_SET_FAILED";

    public const string AnchorReadbackFailed = "ANCHOR_READBACK_FAILED";

    public const string AnchorInvalid = "ANCHOR_INVALID";

    public const string RecoveryRequired = "RECOVERY_REQUIRED";

    public const string ActiveArtifactHashMismatch = "ACTIVE_ARTIFACT_HASH_MISMATCH";

    public const string ManagementReconnectFailed = "MANAGEMENT_RECONNECT_FAILED";

    public const string DeploymentProbeFailed = "DEPLOYMENT_PROBE_FAILED";

    public const string DeploymentProbeInconclusive = "DEPLOYMENT_PROBE_INCONCLUSIVE";

    public const string ProbeKindUnsupported = "DEPLOYMENT_PROBE_KIND_UNSUPPORTED";

    public const string ProbeHostnameForbidden = "DEPLOYMENT_PROBE_HOSTNAME_FORBIDDEN";

    public const string WatchdogNotReady = "WATCHDOG_NOT_READY";

    public const string StandaloneNodeRequired = "DEPLOYMENT_STANDALONE_NODE_REQUIRED";

    public const string CommitSnapshotMissing = "DEPLOYMENT_COMMIT_SNAPSHOT_MISSING";

    public const string SeverityBlocker = "BLOCKER";
}
