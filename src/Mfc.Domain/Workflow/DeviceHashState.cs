using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Workflow;

/// <summary>
/// Persisted desired / committed / actual hash projection for one Device (E2E Spec §8).
/// Workflow status is never stored here — only hashes and known-flags.
/// </summary>
public sealed class DeviceHashState : IEquatable<DeviceHashState>
{
    public DeviceId DeviceId { get; }

    public Hash256? DesiredPolicyHash { get; }

    public Hash256? DesiredArtifactHash { get; }

    public Hash256? LastCommittedPolicyHash { get; }

    public Hash256? LastCommittedArtifactHash { get; }

    public Hash256? ActualManagedResourceHash { get; }

    /// <summary>False when Controller cannot uniquely determine the active managed artifact.</summary>
    public bool ActualKnown { get; }

    /// <summary>False when Controller cannot uniquely determine the active anchor set.</summary>
    public bool AnchorKnown { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public ulong RowVersion { get; }

    private DeviceHashState(
        DeviceId deviceId,
        Hash256? desiredPolicyHash,
        Hash256? desiredArtifactHash,
        Hash256? lastCommittedPolicyHash,
        Hash256? lastCommittedArtifactHash,
        Hash256? actualManagedResourceHash,
        bool actualKnown,
        bool anchorKnown,
        DateTimeOffset updatedAtUtc,
        ulong rowVersion)
    {
        DeviceId = deviceId;
        DesiredPolicyHash = desiredPolicyHash;
        DesiredArtifactHash = desiredArtifactHash;
        LastCommittedPolicyHash = lastCommittedPolicyHash;
        LastCommittedArtifactHash = lastCommittedArtifactHash;
        ActualManagedResourceHash = actualManagedResourceHash;
        ActualKnown = actualKnown;
        AnchorKnown = anchorKnown;
        UpdatedAtUtc = updatedAtUtc;
        RowVersion = rowVersion;
    }

    /// <summary>Creates a new hash-state row for <paramref name="deviceId"/>.</summary>
    public static DeviceHashState Create(
        DeviceId deviceId,
        Hash256? desiredPolicyHash,
        Hash256? desiredArtifactHash,
        Hash256? lastCommittedPolicyHash,
        Hash256? lastCommittedArtifactHash,
        Hash256? actualManagedResourceHash,
        bool actualKnown,
        bool anchorKnown,
        DateTimeOffset updatedAtUtc)
        => new(
            deviceId,
            desiredPolicyHash,
            desiredArtifactHash,
            lastCommittedPolicyHash,
            lastCommittedArtifactHash,
            actualManagedResourceHash,
            actualKnown,
            anchorKnown,
            updatedAtUtc.ToUniversalTime(),
            rowVersion: 1);

    /// <summary>Rebuilds a hash-state row from persistence.</summary>
    public static DeviceHashState Reconstitute(
        DeviceId deviceId,
        Hash256? desiredPolicyHash,
        Hash256? desiredArtifactHash,
        Hash256? lastCommittedPolicyHash,
        Hash256? lastCommittedArtifactHash,
        Hash256? actualManagedResourceHash,
        bool actualKnown,
        bool anchorKnown,
        DateTimeOffset updatedAtUtc,
        ulong rowVersion)
    {
        if (rowVersion == 0)
        {
            throw new DomainInvariantException("DeviceHashState row_version must be greater than zero.");
        }

        return new DeviceHashState(
            deviceId,
            desiredPolicyHash,
            desiredArtifactHash,
            lastCommittedPolicyHash,
            lastCommittedArtifactHash,
            actualManagedResourceHash,
            actualKnown,
            anchorKnown,
            updatedAtUtc.ToUniversalTime(),
            rowVersion);
    }

    /// <summary>Returns a copy with updated hashes / known-flags and bumped row version.</summary>
    public DeviceHashState With(
        Hash256? desiredPolicyHash,
        Hash256? desiredArtifactHash,
        Hash256? lastCommittedPolicyHash,
        Hash256? lastCommittedArtifactHash,
        Hash256? actualManagedResourceHash,
        bool actualKnown,
        bool anchorKnown,
        DateTimeOffset updatedAtUtc)
        => new(
            DeviceId,
            desiredPolicyHash,
            desiredArtifactHash,
            lastCommittedPolicyHash,
            lastCommittedArtifactHash,
            actualManagedResourceHash,
            actualKnown,
            anchorKnown,
            updatedAtUtc.ToUniversalTime(),
            RowVersion + 1);

    public bool Equals(DeviceHashState? other)
    {
        if (other is null)
        {
            return false;
        }

        return DeviceId.Equals(other.DeviceId)
               && HashEquals(DesiredPolicyHash, other.DesiredPolicyHash)
               && HashEquals(DesiredArtifactHash, other.DesiredArtifactHash)
               && HashEquals(LastCommittedPolicyHash, other.LastCommittedPolicyHash)
               && HashEquals(LastCommittedArtifactHash, other.LastCommittedArtifactHash)
               && HashEquals(ActualManagedResourceHash, other.ActualManagedResourceHash)
               && ActualKnown == other.ActualKnown
               && AnchorKnown == other.AnchorKnown
               && RowVersion == other.RowVersion;
    }

    public override bool Equals(object? obj) => obj is DeviceHashState other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(
            DeviceId,
            DesiredArtifactHash,
            LastCommittedArtifactHash,
            ActualManagedResourceHash,
            ActualKnown,
            AnchorKnown,
            RowVersion);

    private static bool HashEquals(Hash256? left, Hash256? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Equals(right);
    }
}
