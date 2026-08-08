using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Inventory;

/// <summary>
/// VRRP group within a VRRP node. Identity is family + VRID + interface (not role alone).
/// Not a persisted aggregate in M1 (Vertical Slice §31).
/// </summary>
public sealed class VrrpGroup
{
    private readonly List<AddressPrefix> _virtualAddresses;
    private readonly List<VrrpMember> _members = [];

    public VrrpGroupId Id { get; }

    public NodeId NodeId { get; }

    public IpAddressFamily Family { get; }

    public byte Vrid { get; }

    public NonEmptyName InterfaceKey { get; }

    public IReadOnlyList<AddressPrefix> VirtualAddresses => _virtualAddresses;

    public TimeSpan AdvertisementInterval { get; private set; }

    public bool Preemption { get; private set; }

    public IReadOnlyList<VrrpMember> Members => _members;

    private VrrpGroup(
        VrrpGroupId id,
        NodeId nodeId,
        IpAddressFamily family,
        byte vrid,
        NonEmptyName interfaceKey,
        List<AddressPrefix> virtualAddresses,
        TimeSpan advertisementInterval,
        bool preemption)
    {
        Id = id;
        NodeId = nodeId;
        Family = family;
        Vrid = vrid;
        InterfaceKey = interfaceKey;
        _virtualAddresses = virtualAddresses;
        AdvertisementInterval = advertisementInterval;
        Preemption = preemption;
    }

    public static VrrpGroup Create(
        NodeId nodeId,
        IpAddressFamily family,
        byte vrid,
        NonEmptyName interfaceKey,
        IEnumerable<AddressPrefix> virtualAddresses,
        TimeSpan advertisementInterval,
        bool preemption)
    {
        ArgumentNullException.ThrowIfNull(interfaceKey);
        ArgumentNullException.ThrowIfNull(virtualAddresses);
        if (vrid == 0)
        {
            throw new DomainInvariantException("VRRP VRID must be between 1 and 255.");
        }

        if (advertisementInterval <= TimeSpan.Zero || advertisementInterval > TimeSpan.FromMinutes(1))
        {
            throw new DomainInvariantException("advertisement_interval must be within (0, 1 minute].");
        }

        List<AddressPrefix> addresses = [];
        foreach (AddressPrefix prefix in virtualAddresses)
        {
            ArgumentNullException.ThrowIfNull(prefix);
            if (prefix.Family != family)
            {
                throw new DomainInvariantException("VRRP virtual address family must match the group family.");
            }

            addresses.Add(prefix);
        }

        if (addresses.Count == 0)
        {
            throw new DomainInvariantException("VrrpGroup requires at least one virtual address.");
        }

        return new VrrpGroup(
            VrrpGroupId.New(),
            nodeId,
            family,
            vrid,
            interfaceKey,
            addresses,
            advertisementInterval,
            preemption);
    }

    public VrrpMember AddMember(DeviceId deviceId, byte configuredPriority, bool configuredOwner)
    {
        if (_members.Exists(m => m.DeviceId == deviceId))
        {
            throw new DomainInvariantException("Device is already a member of this VRRP group.");
        }

        VrrpMember member = VrrpMember.Create(Id, deviceId, configuredPriority, configuredOwner);
        _members.Add(member);
        return member;
    }

    public void SetPreemption(bool preemption) => Preemption = preemption;

    public void SetAdvertisementInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero || interval > TimeSpan.FromMinutes(1))
        {
            throw new DomainInvariantException("advertisement_interval must be within (0, 1 minute].");
        }

        AdvertisementInterval = interval;
    }
}
