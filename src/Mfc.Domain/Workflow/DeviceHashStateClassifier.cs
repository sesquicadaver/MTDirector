using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Workflow;

/// <summary>
/// Pure classification of per-device desired / committed / actual hashes (E2E Spec §8.1–§8.4).
/// </summary>
public static class DeviceHashStateClassifier
{
    /// <summary>Classifies <paramref name="state"/> into a sync bucket. Deterministic and side-effect free.</summary>
    public static DeviceSyncClassification Classify(DeviceHashState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // §8.4 — Controller explicitly cannot determine anchor or actual → recovery.
        if (!state.ActualKnown || !state.AnchorKnown)
        {
            return DeviceSyncClassification.RecoveryRequired;
        }

        Hash256? committed = state.LastCommittedArtifactHash;
        Hash256? actual = state.ActualManagedResourceHash;
        Hash256? desired = state.DesiredArtifactHash;

        // Never managed / no baseline yet — incomplete, not recovery.
        if (committed is null && actual is null)
        {
            return DeviceSyncClassification.Incomplete;
        }

        // One side of the committed/actual pair is missing while flags claim certainty → ambiguous.
        if (committed is null || actual is null)
        {
            return DeviceSyncClassification.RecoveryRequired;
        }

        // §8.3 — actual divergence from last committed is drift (checked before pending).
        if (!actual.Equals(committed))
        {
            return DeviceSyncClassification.Drifted;
        }

        if (desired is null)
        {
            return DeviceSyncClassification.Incomplete;
        }

        // §8.2 — desired changed while actual still matches committed (not drift).
        if (!desired.Equals(committed))
        {
            return DeviceSyncClassification.PendingDeployment;
        }

        // §8.1 — desired == committed == actual.
        return DeviceSyncClassification.Synchronized;
    }
}
