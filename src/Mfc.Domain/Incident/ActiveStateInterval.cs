using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Incident;

/// <summary>
/// Historical active-state interval for one device (next-2 §ActiveStateInterval / M7.3-02).
/// </summary>
public sealed class ActiveStateInterval : IEquatable<ActiveStateInterval>
{
    public DeviceId DeviceId { get; }

    public DateTimeOffset ValidFrom { get; }

    public DateTimeOffset? ValidUntil { get; }

    public Hash256? PolicyHash { get; }

    public Hash256? ArtifactHash { get; }

    public Hash256? ConfigurationHash { get; }

    public Hash256? TopologyHash { get; }

    public ActiveStateCertainty Certainty { get; }

    public bool IsActive => ValidUntil is null;

    internal ActiveStateInterval(
        DeviceId deviceId,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil,
        Hash256? policyHash,
        Hash256? artifactHash,
        Hash256? configurationHash,
        Hash256? topologyHash,
        ActiveStateCertainty certainty)
    {
        DeviceId = deviceId;
        ValidFrom = validFrom.ToUniversalTime();
        ValidUntil = validUntil?.ToUniversalTime();
        PolicyHash = policyHash;
        ArtifactHash = artifactHash;
        ConfigurationHash = configurationHash;
        TopologyHash = topologyHash;
        Certainty = certainty;
    }

    public bool Contains(DateTimeOffset occurredAt)
    {
        DateTimeOffset instant = occurredAt.ToUniversalTime();
        if (instant < ValidFrom)
        {
            return false;
        }

        return ValidUntil is null || instant < ValidUntil;
    }

    public bool Equals(ActiveStateInterval? other) =>
        other is not null
        && DeviceId.Equals(other.DeviceId)
        && ValidFrom.Equals(other.ValidFrom)
        && Nullable.Equals(ValidUntil, other.ValidUntil)
        && HashEquals(PolicyHash, other.PolicyHash)
        && HashEquals(ArtifactHash, other.ArtifactHash)
        && HashEquals(ConfigurationHash, other.ConfigurationHash)
        && HashEquals(TopologyHash, other.TopologyHash)
        && Certainty == other.Certainty;

    public override bool Equals(object? obj) => obj is ActiveStateInterval other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(DeviceId, ValidFrom, ValidUntil, PolicyHash, ArtifactHash, ConfigurationHash, TopologyHash, Certainty);

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
