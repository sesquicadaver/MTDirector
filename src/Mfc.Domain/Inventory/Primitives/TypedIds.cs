namespace Mfc.Domain.Inventory.Primitives;

public readonly record struct SiteId(Guid Value)
{
    public static SiteId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct NodeId(Guid Value)
{
    public static NodeId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct DeviceId(Guid Value)
{
    public static DeviceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct UplinkId(Guid Value)
{
    public static UplinkId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct VrrpGroupId(Guid Value)
{
    public static VrrpGroupId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ZoneBindingId(Guid Value)
{
    public static ZoneBindingId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
