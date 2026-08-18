namespace Mfc.Domain.Onboarding;

/// <summary>Stable onboarding domain codes and bounds (Onboarding Spec §56 / Issue Set M5-01).</summary>
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

    public const string ApiSslInvalid = "ONBOARDING_API_SSL_INVALID";

    public const string UnexpectedAnchorTarget = "ONBOARDING_UNEXPECTED_ANCHOR_TARGET";

    public const string RollbackFailed = "ONBOARDING_ROLLBACK_FAILED";
}
