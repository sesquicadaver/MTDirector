namespace Mfc.Domain.Drift;

/// <summary>Severity buckets for drift findings (E2E Spec §33).</summary>
public enum DriftSeverity : byte
{
    Critical = 1,
    Warning = 2,
    Observation = 3,
    Ignored = 4,
}
