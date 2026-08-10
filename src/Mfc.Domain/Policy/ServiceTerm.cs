namespace Mfc.Domain.Policy;

/// <summary>One protocol term inside a service object (Policy Model §18 / §18.1).</summary>
public sealed class ServiceTerm : IEquatable<ServiceTerm>, IComparable<ServiceTerm>
{
    public IpProtocol Protocol { get; }

    public PortSet? SourcePorts { get; }

    public PortSet? DestinationPorts { get; }

    public IcmpSelectorSet? IcmpSelectors { get; }

    private ServiceTerm(
        IpProtocol protocol,
        PortSet? sourcePorts,
        PortSet? destinationPorts,
        IcmpSelectorSet? icmpSelectors)
    {
        Protocol = protocol;
        SourcePorts = sourcePorts;
        DestinationPorts = destinationPorts;
        IcmpSelectors = icmpSelectors;
    }

    public static ServiceTerm Create(
        IpProtocol protocol,
        PortSet? sourcePorts = null,
        PortSet? destinationPorts = null,
        IcmpSelectorSet? icmpSelectors = null)
    {
        ArgumentNullException.ThrowIfNull(protocol);

        bool hasPorts = (sourcePorts is { Intervals.Count: > 0 })
                        || (destinationPorts is { Intervals.Count: > 0 });
        bool hasIcmp = icmpSelectors is { Items.Count: > 0 };

        if (protocol.IsAny && hasPorts)
        {
            throw new DomainInvariantException("protocol=any with ports is forbidden.");
        }

        if (hasPorts && !protocol.HasPortSemantics)
        {
            throw new DomainInvariantException(
                "Port matchers are allowed only for port-capable protocols (TCP/UDP/SCTP).");
        }

        if (hasIcmp && !(protocol.IsIcmpV4 || protocol.IsIcmpV6Protocol))
        {
            throw new DomainInvariantException("ICMP selectors are allowed only for ICMP or ICMPv6.");
        }

        if (!hasIcmp && (protocol.IsIcmpV4 || protocol.IsIcmpV6Protocol) && icmpSelectors is null)
        {
            // ICMP terms may omit selectors (= any ICMP for that protocol).
        }

        PortSet? src = NormalizeOptionalPorts(sourcePorts);
        PortSet? dst = NormalizeOptionalPorts(destinationPorts);
        IcmpSelectorSet? icmp = icmpSelectors is null || icmpSelectors.Items.Count == 0
            ? null
            : IcmpSelectorSet.Create(icmpSelectors.Items);

        return new ServiceTerm(protocol, src, dst, icmp);
    }

    public int CompareTo(ServiceTerm? other)
    {
        if (other is null)
        {
            return 1;
        }

        int protocol = Protocol.CompareTo(other.Protocol);
        if (protocol != 0)
        {
            return protocol;
        }

        int src = ComparePorts(SourcePorts, other.SourcePorts);
        if (src != 0)
        {
            return src;
        }

        int dst = ComparePorts(DestinationPorts, other.DestinationPorts);
        if (dst != 0)
        {
            return dst;
        }

        return CompareIcmp(IcmpSelectors, other.IcmpSelectors);
    }

    public bool Equals(ServiceTerm? other)
        => other is not null
           && Protocol.Equals(other.Protocol)
           && EqualsPorts(SourcePorts, other.SourcePorts)
           && EqualsPorts(DestinationPorts, other.DestinationPorts)
           && EqualsIcmp(IcmpSelectors, other.IcmpSelectors);

    public override bool Equals(object? obj) => obj is ServiceTerm other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Protocol, SourcePorts, DestinationPorts, IcmpSelectors);

    public static bool operator ==(ServiceTerm? left, ServiceTerm? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(ServiceTerm? left, ServiceTerm? right) => !(left == right);

    public static bool operator <(ServiceTerm? left, ServiceTerm? right)
        => left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator >(ServiceTerm? left, ServiceTerm? right)
        => right is null ? left is not null : left is not null && left.CompareTo(right) > 0;

    public static bool operator <=(ServiceTerm? left, ServiceTerm? right) => !(left > right);

    public static bool operator >=(ServiceTerm? left, ServiceTerm? right) => !(left < right);

    private static PortSet? NormalizeOptionalPorts(PortSet? ports)
    {
        if (ports is null || ports.Intervals.Count == 0)
        {
            return null;
        }

        return PortSet.Create(ports.Intervals);
    }

    private static bool EqualsPorts(PortSet? left, PortSet? right)
        => left is null ? right is null : left.Equals(right);

    private static bool EqualsIcmp(IcmpSelectorSet? left, IcmpSelectorSet? right)
        => left is null ? right is null : left.Equals(right);

    private static int ComparePorts(PortSet? left, PortSet? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        int count = left.Intervals.Count.CompareTo(right.Intervals.Count);
        if (count != 0)
        {
            return count;
        }

        for (int i = 0; i < left.Intervals.Count; i++)
        {
            int cmp = left.Intervals[i].CompareTo(right.Intervals[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    private static int CompareIcmp(IcmpSelectorSet? left, IcmpSelectorSet? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        int count = left.Items.Count.CompareTo(right.Items.Count);
        if (count != 0)
        {
            return count;
        }

        for (int i = 0; i < left.Items.Count; i++)
        {
            int cmp = left.Items[i].CompareTo(right.Items[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }
}
