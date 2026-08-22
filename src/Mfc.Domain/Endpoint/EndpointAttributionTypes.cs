using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Endpoint;

/// <summary>Resolver certainty (next-2 §3; fail-closed on conflict or missing evidence).</summary>
public enum EndpointAttributionCertainty
{
    Proven = 1,
    Partial = 2,
    Unknown = 3,
}

/// <summary>Ordered hop kinds in the attribution chain (IP → … → inventory anchors).</summary>
public enum EndpointAttributionHopKind
{
    Ip = 1,
    Mac = 2,
    Vlan = 3,
    Bridge = 4,
    Port = 5,
    Interface = 6,
    Veth = 7,
    Container = 8,
    VpnPeer = 9,
    Site = 10,
    Node = 11,
    Device = 12,
}

public sealed class ArpFact
{
    public required string IpAddress { get; init; }

    public required string MacAddress { get; init; }

    public string? Interface { get; init; }
}

public sealed class DhcpLeaseFact
{
    public required string IpAddress { get; init; }

    public required string MacAddress { get; init; }

    public string? Interface { get; init; }

    public string? Status { get; init; }
}

public sealed class DhcpSnoopingBindingFact
{
    public required string IpAddress { get; init; }

    public required string MacAddress { get; init; }

    public string? VlanId { get; init; }

    public string? Bridge { get; init; }

    public string? Port { get; init; }
}

public sealed class Ipv6NeighborFact
{
    public required string IpAddress { get; init; }

    public required string MacAddress { get; init; }

    public string? Interface { get; init; }
}

public sealed class BridgeHostFact
{
    public required string MacAddress { get; init; }

    public string? VlanId { get; init; }

    public string? Bridge { get; init; }

    public string? Port { get; init; }

    public string? Interface { get; init; }
}

public sealed class VlanMembershipFact
{
    public required string VlanId { get; init; }

    public required string Interface { get; init; }

    public string? Bridge { get; init; }
}

public sealed class VpnSessionFact
{
    public required string Protocol { get; init; }

    public required string InternalAddress { get; init; }

    public string? PeerName { get; init; }

    public string? RemoteEndpoint { get; init; }

    public string? User { get; init; }
}

public sealed class VethEndpointFact
{
    public required string VethName { get; init; }

    public string? ContainerName { get; init; }

    public string? AppName { get; init; }

    public string? IpAddress { get; init; }

    public string? MacAddress { get; init; }

    public string? Interface { get; init; }
}

/// <summary>Structured discovery facts for endpoint attribution (M7.2-01).</summary>
public sealed class EndpointAttributionSnapshot
{
    public IReadOnlyList<ArpFact> ArpEntries { get; init; } = [];

    public IReadOnlyList<DhcpLeaseFact> DhcpLeases { get; init; } = [];

    public IReadOnlyList<DhcpSnoopingBindingFact> DhcpSnoopingBindings { get; init; } = [];

    public IReadOnlyList<Ipv6NeighborFact> Ipv6Neighbors { get; init; } = [];

    public IReadOnlyList<BridgeHostFact> BridgeHostEntries { get; init; } = [];

    public IReadOnlyList<VlanMembershipFact> VlanMemberships { get; init; } = [];

    public IReadOnlyList<VpnSessionFact> VpnSessions { get; init; } = [];

    public IReadOnlyList<VethEndpointFact> VethMappings { get; init; } = [];

    public SiteId? SiteId { get; init; }

    public NodeId? NodeId { get; init; }

    public DeviceId? DeviceId { get; init; }
}

public sealed class EndpointAttributionQuery
{
    public required string Family { get; init; }

    public required string IpAddress { get; init; }

    public SiteId? SiteId { get; init; }

    public NodeId? NodeId { get; init; }

    public DeviceId? DeviceId { get; init; }
}

public sealed class EndpointAttributionHop
{
    public required EndpointAttributionHopKind Kind { get; init; }

    public required string Value { get; init; }

    public string? Detail { get; init; }
}

public sealed class EndpointAttributionChain
{
    public required IReadOnlyList<EndpointAttributionHop> Hops { get; init; }
}

public sealed class EndpointAttributionFinding
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

public sealed class EndpointAttributionResult
{
    public required EndpointAttributionChain Chain { get; init; }

    public required EndpointAttributionCertainty Certainty { get; init; }

    public IReadOnlyList<EndpointAttributionFinding> Findings { get; init; } = [];
}
