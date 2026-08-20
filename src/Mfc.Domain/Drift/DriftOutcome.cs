namespace Mfc.Domain.Drift;

/// <summary>Aggregate outcome of a managed drift evaluation (M6-02).</summary>
public enum DriftOutcome : byte
{
    /// <summary>Actual matches committed; no blocking findings.</summary>
    NoDrift = 0,

    /// <summary>Only observation/ignored findings; not configuration drift.</summary>
    ObservationOnly = 1,

    /// <summary>Warning-class findings without Critical or hash divergence.</summary>
    WarningDrift = 2,

    /// <summary>Critical findings and/or managed-resource hash divergence from last committed.</summary>
    CriticalDrift = 3,

    /// <summary>Desired differs while actual still equals committed — pending deploy, not drift.</summary>
    PendingDeploymentNotDrift = 4,
}
