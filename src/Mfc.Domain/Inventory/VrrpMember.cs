using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Inventory;

/// <summary>
/// Per-device membership in a VRRP group. Observed role is per instance, not a global device role.
/// </summary>
public sealed class VrrpMember
{
    public VrrpGroupId GroupId { get; }

    public DeviceId DeviceId { get; }

    public byte ConfiguredPriority { get; private set; }

    public bool ConfiguredOwner { get; private set; }

    public VrrpMemberObservedState ObservedState { get; private set; }

    public DateTimeOffset? ObservedAtUtc { get; private set; }

    private VrrpMember(
        VrrpGroupId groupId,
        DeviceId deviceId,
        byte configuredPriority,
        bool configuredOwner,
        VrrpMemberObservedState observedState,
        DateTimeOffset? observedAtUtc)
    {
        GroupId = groupId;
        DeviceId = deviceId;
        ConfiguredPriority = configuredPriority;
        ConfiguredOwner = configuredOwner;
        ObservedState = observedState;
        ObservedAtUtc = observedAtUtc;
    }

    internal static VrrpMember Create(
        VrrpGroupId groupId,
        DeviceId deviceId,
        byte configuredPriority,
        bool configuredOwner)
    {
        if (configuredPriority == 0)
        {
            throw new DomainInvariantException("configured_priority must be between 1 and 255.");
        }

        return new VrrpMember(
            groupId,
            deviceId,
            configuredPriority,
            configuredOwner,
            VrrpMemberObservedState.Unknown,
            observedAtUtc: null);
    }

    public void Configure(byte priority, bool owner)
    {
        if (priority == 0)
        {
            throw new DomainInvariantException("configured_priority must be between 1 and 255.");
        }

        ConfiguredPriority = priority;
        ConfiguredOwner = owner;
    }

    public void RecordObservation(VrrpMemberObservedState state, DateTimeOffset observedAtUtc)
    {
        if (observedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainInvariantException("observed_at must be UTC.");
        }

        ObservedState = state;
        ObservedAtUtc = observedAtUtc;
    }
}
