namespace Mfc.Domain.Workflow;

/// <summary>
/// Per-device sync classification from desired / committed / actual hashes (E2E Spec §8).
/// </summary>
public enum DeviceSyncClassification : byte
{
    /// <summary>Hashes present and equal across desired, committed, and actual artifact digests.</summary>
    Synchronized = 0,

    /// <summary>Desired differs from committed while actual still matches committed (not drift).</summary>
    PendingDeployment = 1,

    /// <summary>Actual managed resource diverges from last committed artifact.</summary>
    Drifted = 2,

    /// <summary>Anchor or actual state is unknown / ambiguous.</summary>
    RecoveryRequired = 3,

    /// <summary>Known anchors but insufficient hashes to claim synchronized or pending.</summary>
    Incomplete = 4,
}
