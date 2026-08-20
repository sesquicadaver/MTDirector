namespace Mfc.Domain.Drift;

/// <summary>Stable drift detection / gate codes (Issue Set M6-02 / E2E §32–§34).</summary>
public static class DriftCodes
{
    public const string SchemaVersion = "mfc.drift.schema.v1";

    public const string CriticalDriftBlocksDeploy = "DRIFT_CRITICAL_BLOCKS_DEPLOY";

    public const string BaselineIsLastCommitted = "DRIFT_BASELINE_LAST_COMMITTED";

    public const string DesiredNotBaseline = "DRIFT_DESIRED_NOT_BASELINE";

    public const string EventImmutable = "DRIFT_EVENT_IMMUTABLE";

    public const string Detected = "drift.detect";

    public const string NoAutoRepair = "DRIFT_NO_AUTO_REPAIR";
}
