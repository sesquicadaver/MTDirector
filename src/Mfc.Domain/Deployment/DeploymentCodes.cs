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
}
