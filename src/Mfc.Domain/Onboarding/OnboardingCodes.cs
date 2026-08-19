namespace Mfc.Domain.Onboarding;

/// <summary>Stable onboarding codes (Onboarding Spec §56 / §58 / Issue Sets M5-01–M5-02).</summary>
public static class OnboardingCodes
{
    public const string PlanHashPrefix = "mfc.onboarding.plan.v1";

    public static readonly TimeSpan DefaultPlanLifetime = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan MinWatchdogTtl = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan DefaultWatchdogTtl = TimeSpan.FromSeconds(180);

    public static readonly TimeSpan MaxWatchdogTtl = TimeSpan.FromSeconds(600);

    public const string InvalidTransition = "ONBOARDING_INVALID_TRANSITION";

    public const string PlanExpired = "ONBOARDING_PLAN_EXPIRED";

    public const string PlanHashMismatch = "ONBOARDING_PLAN_HASH_MISMATCH";

    public const string NonterminalExists = "ONBOARDING_NONTERMINAL_EXISTS";

    public const string NodeNotUnmanaged = "ONBOARDING_NODE_NOT_UNMANAGED";

    public const string DevicePlanCardinality = "ONBOARDING_DEVICE_PLAN_CARDINALITY";

    public const string TerminalImmutable = "ONBOARDING_TERMINAL_IMMUTABLE";

    public const string StepInvalidTransition = "ONBOARDING_STEP_INVALID_TRANSITION";

    public const string WatchdogTtlOutOfRange = "ONBOARDING_WATCHDOG_TTL_OUT_OF_RANGE";

    public const string NamespaceCollision = "ONBOARDING_NAMESPACE_COLLISION";

    public const string UnexpectedAnchorTarget = "ONBOARDING_UNEXPECTED_ANCHOR_TARGET";

    public const string RollbackFailed = "ONBOARDING_ROLLBACK_FAILED";

    /// <summary>Spec §58 / M5-02 AC#1.</summary>
    public const string RouterOsUnsupported = "ONBOARDING_ROUTEROS_UNSUPPORTED";

    /// <summary>Spec §58 / M5-02 AC#3.</summary>
    public const string ApiSslInvalid = "ONBOARDING_API_SSL_INVALID";

    /// <summary>Spec §58 / M5-02 AC#2.</summary>
    public const string PlainApiEnabled = "ONBOARDING_PLAIN_API_ENABLED";

    /// <summary>Spec §58 / M5-02 AC#4–#6.</summary>
    public const string ReadAccountInvalid = "ONBOARDING_READ_ACCOUNT_INVALID";

    /// <summary>Spec §58 / M5-02 AC#4–#6.</summary>
    public const string DeployAccountInvalid = "ONBOARDING_DEPLOY_ACCOUNT_INVALID";

    /// <summary>Spec §58 / M5-02 AC#7.</summary>
    public const string AccountSourceInvalid = "ONBOARDING_ACCOUNT_SOURCE_INVALID";

    /// <summary>Spec §58 / M5-02 AC#8.</summary>
    public const string DeviceModeSchedulerDisabled = "DEVICE_MODE_SCHEDULER_DISABLED";

    /// <summary>Spec §58 / M5-02 AC#9.</summary>
    public const string DeviceFlagged = "DEVICE_FLAGGED";

    /// <summary>Spec §58 / M5-03 — no matching enabled input/output guard.</summary>
    public const string ManagementGuardMissing = "MANAGEMENT_GUARD_MISSING";

    /// <summary>Spec §17 / M5-03 AC#4–#5 — guard predicate wider than GuardProfile (incl. default routes).</summary>
    public const string ManagementGuardTooBroad = "MANAGEMENT_GUARD_TOO_BROAD";

    /// <summary>Spec §58 / M5-03 — marker, static/enabled, matcher, or hash invalid.</summary>
    public const string ManagementGuardInvalid = "MANAGEMENT_GUARD_INVALID";

    /// <summary>Spec §58 / M5-03 — unprovable management path (unknown matcher / indeterminate).</summary>
    public const string ManagementPathIndeterminate = "MANAGEMENT_PATH_INDETERMINATE";

    /// <summary>Spec §58 / M5-04 — snapshot order or neighbor fingerprints no longer match the plan.</summary>
    public const string AnchorPlacementStale = "ANCHOR_PLACEMENT_STALE";

    /// <summary>Spec §58 / M5-04 — BEFORE_STATIC_RULE reference not found at the recorded rank.</summary>
    public const string AnchorReferenceMissing = "ANCHOR_REFERENCE_MISSING";

    /// <summary>Spec §58 / M5-04 — placement reference is a dynamic rule.</summary>
    public const string AnchorReferenceDynamic = "ANCHOR_REFERENCE_DYNAMIC";

    /// <summary>Spec §58 / M5-04 — planned ordinal is at or before a management guard.</summary>
    public const string AnchorBeforeGuard = "ANCHOR_BEFORE_GUARD";

    /// <summary>Spec §58 / M5-04 — insertion is after an unconditional terminal rule.</summary>
    public const string AnchorUnreachable = "ANCHOR_UNREACHABLE";

    /// <summary>Spec §58 / M5-04 — unknown matcher or unprovable jump context around the insertion point.</summary>
    public const string AnchorContextIndeterminate = "ANCHOR_CONTEXT_INDETERMINATE";

    public const string SeverityBlocker = "BLOCKER";
}
