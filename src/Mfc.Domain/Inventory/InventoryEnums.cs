namespace Mfc.Domain.Inventory;

public enum SiteStatus : byte
{
    Draft = 0,
    Active = 1,
    Disabled = 2,
}

public enum NodeKind : byte
{
    Router = 0,
    Vrrp = 1,
    Switch = 2,
}

public enum NodeStatus : byte
{
    Draft = 0,
    Active = 1,
    Disabled = 2,
}

/// <summary>
/// Managed-device lifecycle on Node and Device (Onboarding Spec §4). Distinct from <see cref="NodeStatus"/>.
/// </summary>
public enum ManagementState : byte
{
    Unmanaged = 0,
    Managed = 1,
    RecoveryRequired = 2,
}

/// <summary>Operator-declared uplink mode on a Node (Vertical Slice §6.2).</summary>
public enum DeclaredUplinkMode : byte
{
    None = 0,
    /// <summary>Single uplink path (Vertical Slice SINGLE).</summary>
    One = 1,
    Failover = 2,
    Balanced = 3,
    Mixed = 4,
}

public enum DeviceRole : byte
{
    Router = 0,
    L3Switch = 1,
    L2Switch = 2,
    Unknown = 3,
}

public enum SupportState : byte
{
    Supported = 0,
    ReadOnly = 1,
    NeedsRevalidation = 2,
    Unsupported = 3,
}

/// <summary>
/// Durable connectivity observation from DiscoverDevice (W6-08). Distinct from <see cref="SupportState"/>.
/// </summary>
public enum ObservedReachability : byte
{
    Unknown = 0,
    Reachable = 1,
    Unreachable = 2,
}

public enum IpAddressFamily : byte
{
    IPv4 = 0,
    IPv6 = 1,
}

public enum ZoneAddressFamily : byte
{
    IPv4 = 0,
    IPv6 = 1,
    Dual = 2,
}

/// <summary>Uplink traffic role for policy compilation (MVP §6.6).</summary>
public enum UplinkTrafficMode : byte
{
    Primary = 0,
    Backup = 1,
    Balanced = 2,
    Transit = 3,
}

public enum VrrpMemberObservedState : byte
{
    Master = 0,
    Backup = 1,
    Init = 2,
    Unknown = 3,
}

public enum ZoneBindingType : byte
{
    InterfaceList = 0,
    InterfaceSet = 1,
}
