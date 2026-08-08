using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Inventory;

/// <summary>
/// Node aggregate owning devices. Enforces ROUTER/SWITCH cardinality and VRRP minimum for Active.
/// </summary>
public sealed class Node
{
    private readonly List<Device> _devices = [];

    public NodeId Id { get; }

    public SiteId SiteId { get; }

    public NonEmptyName Name { get; private set; }

    public NodeKind DeclaredKind { get; private set; }

    public DeclaredUplinkMode DeclaredUplinkMode { get; private set; }

    public NodeStatus Status { get; private set; }

    public ulong RowVersion { get; private set; }

    public IReadOnlyList<Device> Devices => _devices;

    private Node(
        NodeId id,
        SiteId siteId,
        NonEmptyName name,
        NodeKind declaredKind,
        DeclaredUplinkMode declaredUplinkMode,
        NodeStatus status,
        ulong rowVersion)
    {
        Id = id;
        SiteId = siteId;
        Name = name;
        DeclaredKind = declaredKind;
        DeclaredUplinkMode = declaredUplinkMode;
        Status = status;
        RowVersion = rowVersion;
    }

    public static Node Create(
        SiteId siteId,
        NonEmptyName name,
        NodeKind declaredKind,
        DeclaredUplinkMode declaredUplinkMode)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new Node(
            NodeId.New(),
            siteId,
            name,
            declaredKind,
            declaredUplinkMode,
            NodeStatus.Draft,
            rowVersion: 1);
    }

    /// <summary>Rebuilds a node from persistence. Devices are attached via <see cref="AttachDevice"/>.</summary>
    public static Node Reconstitute(
        NodeId id,
        SiteId siteId,
        NonEmptyName name,
        NodeKind declaredKind,
        DeclaredUplinkMode declaredUplinkMode,
        NodeStatus status,
        ulong rowVersion)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (rowVersion == 0)
        {
            throw new DomainInvariantException("row_version must be greater than zero.");
        }

        return new Node(id, siteId, name, declaredKind, declaredUplinkMode, status, rowVersion);
    }

    /// <summary>Attaches a reconstituted device during load (no cardinality bump of row version).</summary>
    public void AttachDevice(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (device.NodeId != Id)
        {
            throw new DomainInvariantException("Device node_id does not match this node.");
        }

        _devices.Add(device);
    }

    public void Rename(NonEmptyName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Touch();
    }

    public void SetDeclaredKind(NodeKind kind)
    {
        if (Status == NodeStatus.Active)
        {
            EnsureDeviceCardinality(kind, _devices.Count);
        }

        DeclaredKind = kind;
        Touch();
    }

    public void SetDeclaredUplinkMode(DeclaredUplinkMode mode)
    {
        DeclaredUplinkMode = mode;
        Touch();
    }

    public Device AddDevice(
        NonEmptyName displayName,
        ManagementEndpoint managementEndpoint,
        DeviceRole role)
    {
        int nextCount = _devices.Count + 1;
        EnsureCanAcceptAnotherDevice(DeclaredKind, nextCount);

        Device device = Device.Create(Id, displayName, managementEndpoint, role);
        _devices.Add(device);
        Touch();
        return device;
    }

    public void RemoveDevice(DeviceId deviceId)
    {
        int index = _devices.FindIndex(d => d.Id == deviceId);
        if (index < 0)
        {
            throw new DomainInvariantException($"Device '{deviceId}' is not a member of this node.");
        }

        if (Status == NodeStatus.Active)
        {
            EnsureDeviceCardinality(DeclaredKind, _devices.Count - 1);
        }

        _devices.RemoveAt(index);
        Touch();
    }

    public void Activate()
    {
        if (Status == NodeStatus.Disabled)
        {
            throw new DomainInvariantException("Disabled node cannot be activated.");
        }

        EnsureDeviceCardinality(DeclaredKind, _devices.Count);
        Status = NodeStatus.Active;
        Touch();
    }

    public void Disable()
    {
        Status = NodeStatus.Disabled;
        Touch();
    }

    /// <summary>True when current device count satisfies Active invariants for <see cref="DeclaredKind"/>.</summary>
    public bool SatisfiesActiveDeviceCardinality()
        => TryDescribeCardinalityViolation(DeclaredKind, _devices.Count) is null;

    private static void EnsureCanAcceptAnotherDevice(NodeKind kind, int nextCount)
    {
        if (kind is NodeKind.Router or NodeKind.Switch)
        {
            if (nextCount > 1)
            {
                throw new DomainInvariantException(
                    $"{kind} node cannot contain more than one device.");
            }
        }
    }

    private static void EnsureDeviceCardinality(NodeKind kind, int count)
    {
        string? violation = TryDescribeCardinalityViolation(kind, count);
        if (violation is not null)
        {
            throw new DomainInvariantException(violation);
        }
    }

    private static string? TryDescribeCardinalityViolation(NodeKind kind, int count)
        => kind switch
        {
            NodeKind.Router when count != 1
                => "ROUTER node requires exactly one device when Active.",
            NodeKind.Switch when count != 1
                => "SWITCH node requires exactly one device when Active.",
            NodeKind.Vrrp when count < 2
                => "VRRP node requires at least two devices when Active.",
            _ => null,
        };

    private void Touch() => RowVersion++;
}
