using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Snapshots;

/// <summary>
/// Observed topology projection facts for a node. Pure value object — not a persisted aggregate,
/// and never stores raw RouterOS API payloads or credentials.
/// </summary>
public sealed class TopologyObservation : IEquatable<TopologyObservation>
{
    private readonly VrrpRoleObservation[] _vrrpRoles;
    private readonly string[] _activeInterfaceKeys;

    public NodeId NodeId { get; }

    public DateTimeOffset ObservedAtUtc { get; }

    public IReadOnlyList<string> ActiveInterfaceKeys => _activeInterfaceKeys;

    public IReadOnlyList<VrrpRoleObservation> VrrpRoles => _vrrpRoles;

    private TopologyObservation(
        NodeId nodeId,
        DateTimeOffset observedAtUtc,
        string[] activeInterfaceKeys,
        VrrpRoleObservation[] vrrpRoles)
    {
        NodeId = nodeId;
        ObservedAtUtc = observedAtUtc;
        _activeInterfaceKeys = activeInterfaceKeys;
        _vrrpRoles = vrrpRoles;
    }

    public static TopologyObservation Create(
        NodeId nodeId,
        DateTimeOffset observedAtUtc,
        IEnumerable<string> activeInterfaceKeys,
        IEnumerable<VrrpRoleObservation> vrrpRoles)
    {
        ArgumentNullException.ThrowIfNull(activeInterfaceKeys);
        ArgumentNullException.ThrowIfNull(vrrpRoles);
        if (observedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainInvariantException("TopologyObservation.ObservedAtUtc must be UTC.");
        }

        string[] interfaces = activeInterfaceKeys
            .Select(key =>
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new DomainInvariantException("Interface keys must be non-empty.");
                }

                return key.Trim();
            })
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();

        VrrpRoleObservation[] roles = vrrpRoles
            .OrderBy(r => r.Vrid)
            .ThenBy(r => r.Family)
            .ThenBy(r => r.DeviceId.Value)
            .ToArray();

        return new TopologyObservation(nodeId, observedAtUtc, interfaces, roles);
    }

    public bool Equals(TopologyObservation? other)
    {
        if (other is null)
        {
            return false;
        }

        return NodeId == other.NodeId
               && ObservedAtUtc == other.ObservedAtUtc
               && _activeInterfaceKeys.SequenceEqual(other._activeInterfaceKeys, StringComparer.Ordinal)
               && _vrrpRoles.SequenceEqual(other._vrrpRoles);
    }

    public override bool Equals(object? obj) => obj is TopologyObservation other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hc = default;
        hc.Add(NodeId);
        hc.Add(ObservedAtUtc);
        foreach (string key in _activeInterfaceKeys)
        {
            hc.Add(key, StringComparer.Ordinal);
        }

        foreach (VrrpRoleObservation role in _vrrpRoles)
        {
            hc.Add(role);
        }

        return hc.ToHashCode();
    }
}

/// <summary>Per-instance VRRP role observation (family + VRID scoped, not a global device role).</summary>
public readonly struct VrrpRoleObservation : IEquatable<VrrpRoleObservation>
{
    public DeviceId DeviceId { get; }

    public IpAddressFamily Family { get; }

    public byte Vrid { get; }

    public VrrpMemberObservedState ObservedState { get; }

    public VrrpRoleObservation(
        DeviceId deviceId,
        IpAddressFamily family,
        byte vrid,
        VrrpMemberObservedState observedState)
    {
        if (vrid == 0)
        {
            throw new DomainInvariantException("VRRP VRID must be between 1 and 255.");
        }

        DeviceId = deviceId;
        Family = family;
        Vrid = vrid;
        ObservedState = observedState;
    }

    public bool Equals(VrrpRoleObservation other)
        => DeviceId == other.DeviceId
           && Family == other.Family
           && Vrid == other.Vrid
           && ObservedState == other.ObservedState;

    public override bool Equals(object? obj) => obj is VrrpRoleObservation other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(DeviceId, Family, Vrid, ObservedState);

    public static bool operator ==(VrrpRoleObservation left, VrrpRoleObservation right) => left.Equals(right);

    public static bool operator !=(VrrpRoleObservation left, VrrpRoleObservation right) => !left.Equals(right);
}

/// <summary>
/// Snapshot metadata envelope. Separates configuration vs observation digests; forbids credentials/raw payload.
/// </summary>
public sealed class SnapshotMetadata : IEquatable<SnapshotMetadata>
{
    public SnapshotId Id { get; }

    public DeviceId DeviceId { get; }

    public SnapshotStatus Status { get; }

    public ConfigurationHash? ConfigurationHash { get; }

    public ObservationHash? ObservationHash { get; }

    public CapabilityHash? CapabilityHash { get; }

    public SnapshotHash? SnapshotHash { get; }

    public DateTimeOffset? CompletedAtUtc { get; }

    private SnapshotMetadata(
        SnapshotId id,
        DeviceId deviceId,
        SnapshotStatus status,
        ConfigurationHash? configurationHash,
        ObservationHash? observationHash,
        CapabilityHash? capabilityHash,
        SnapshotHash? snapshotHash,
        DateTimeOffset? completedAtUtc)
    {
        Id = id;
        DeviceId = deviceId;
        Status = status;
        ConfigurationHash = configurationHash;
        ObservationHash = observationHash;
        CapabilityHash = capabilityHash;
        SnapshotHash = snapshotHash;
        CompletedAtUtc = completedAtUtc;
    }

    public static SnapshotMetadata CreateCompleted(
        DeviceId deviceId,
        ConfigurationHash configurationHash,
        ObservationHash observationHash,
        CapabilityHash capabilityHash,
        SnapshotHash snapshotHash,
        DateTimeOffset completedAtUtc)
    {
        if (completedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainInvariantException("Snapshot completed_at must be UTC.");
        }

        return new SnapshotMetadata(
            SnapshotId.New(),
            deviceId,
            SnapshotStatus.Completed,
            configurationHash,
            observationHash,
            capabilityHash,
            snapshotHash,
            completedAtUtc);
    }

    public static SnapshotMetadata CreateFailed(DeviceId deviceId, DateTimeOffset failedAtUtc)
    {
        if (failedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainInvariantException("Snapshot failed_at must be UTC.");
        }

        return new SnapshotMetadata(
            SnapshotId.New(),
            deviceId,
            SnapshotStatus.Failed,
            configurationHash: null,
            observationHash: null,
            capabilityHash: null,
            snapshotHash: null,
            completedAtUtc: failedAtUtc);
    }

    public bool Equals(SnapshotMetadata? other)
    {
        if (other is null)
        {
            return false;
        }

        return Id == other.Id
               && DeviceId == other.DeviceId
               && Status == other.Status
               && NullableEquals(ConfigurationHash, other.ConfigurationHash)
               && NullableEquals(ObservationHash, other.ObservationHash)
               && NullableEquals(CapabilityHash, other.CapabilityHash)
               && NullableEquals(SnapshotHash, other.SnapshotHash)
               && CompletedAtUtc == other.CompletedAtUtc;
    }

    public override bool Equals(object? obj) => obj is SnapshotMetadata other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Id, DeviceId, Status, ConfigurationHash, ObservationHash, CapabilityHash, SnapshotHash, CompletedAtUtc);

    private static bool NullableEquals<T>(T? left, T? right)
        where T : struct, IEquatable<T>
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Value.Equals(right.Value);
    }
}
