namespace Mfc.Domain.Policy;

/// <summary>Supported connection-tracking states (Policy Model §24).</summary>
public enum ConnectionState : byte
{
    New = 0,
    Established = 1,
    Related = 2,
    Invalid = 3,
    Untracked = 4,
}

/// <summary>Supported connection NAT states (Policy Model §24).</summary>
public enum ConnectionNatState : byte
{
    SrcNat = 0,
    DstNat = 1,
}

/// <summary>Supported address-type matchers (Policy Model §24).</summary>
public enum AddressType : byte
{
    Local = 0,
    Unicast = 1,
    Broadcast = 2,
    Multicast = 3,
    Anycast = 4,
    Blackhole = 5,
    Prohibit = 6,
    Unreachable = 7,
}

/// <summary>TCP header bits for <see cref="TcpFlagConstraint"/> (Policy Model §24).</summary>
public enum TcpHeaderBit : byte
{
    Fin = 0,
    Syn = 1,
    Rst = 2,
    Psh = 3,
    Ack = 4,
    Urg = 5,
    Ece = 6,
    Cwr = 7,
}

/// <summary>IPsec predicate direction (Policy Model §24).</summary>
public enum IpsecDirection : byte
{
    In = 0,
    Out = 1,
}

/// <summary>IPsec predicate policy (Policy Model §24).</summary>
public enum IpsecPolicyKind : byte
{
    Ipsec = 0,
    None = 1,
}

/// <summary>Required-present / required-absent TCP flag constraint (Policy Model §24).</summary>
public sealed class TcpFlagConstraint
{
    public IReadOnlyList<TcpHeaderBit> RequiredPresent { get; }

    public IReadOnlyList<TcpHeaderBit> RequiredAbsent { get; }

    private TcpFlagConstraint(
        IReadOnlyList<TcpHeaderBit> requiredPresent,
        IReadOnlyList<TcpHeaderBit> requiredAbsent)
    {
        RequiredPresent = requiredPresent;
        RequiredAbsent = requiredAbsent;
    }

    public static TcpFlagConstraint Create(
        IEnumerable<TcpHeaderBit>? requiredPresent = null,
        IEnumerable<TcpHeaderBit>? requiredAbsent = null)
    {
        TcpHeaderBit[] present = NormalizeUnique(requiredPresent);
        TcpHeaderBit[] absent = NormalizeUnique(requiredAbsent);
        HashSet<TcpHeaderBit> overlap = present.ToHashSet();
        overlap.IntersectWith(absent);
        if (overlap.Count > 0)
        {
            throw new DomainInvariantException(
                "TCP flag cannot be both required_present and required_absent.");
        }

        return new TcpFlagConstraint(present, absent);
    }

    private static TcpHeaderBit[] NormalizeUnique(IEnumerable<TcpHeaderBit>? flags)
    {
        TcpHeaderBit[] values = (flags ?? []).ToArray();
        HashSet<TcpHeaderBit> seen = [];
        foreach (TcpHeaderBit flag in values)
        {
            if (!Enum.IsDefined(flag))
            {
                throw new DomainInvariantException($"Unknown TCP flag '{flag}'.");
            }

            if (!seen.Add(flag))
            {
                throw new DomainInvariantException($"Duplicate TCP flag '{flag}'.");
            }
        }

        return values.OrderBy(static f => (byte)f).ToArray();
    }
}

/// <summary>IPsec policy predicate (Policy Model §24).</summary>
public sealed class IpsecPolicyPredicate
{
    public IpsecDirection Direction { get; }

    public IpsecPolicyKind Policy { get; }

    private IpsecPolicyPredicate(IpsecDirection direction, IpsecPolicyKind policy)
    {
        Direction = direction;
        Policy = policy;
    }

    public static IpsecPolicyPredicate Create(IpsecDirection direction, IpsecPolicyKind policy)
    {
        if (!Enum.IsDefined(direction))
        {
            throw new DomainInvariantException($"Unknown IPsec direction '{direction}'.");
        }

        if (!Enum.IsDefined(policy))
        {
            throw new DomainInvariantException($"Unknown IPsec policy '{policy}'.");
        }

        return new IpsecPolicyPredicate(direction, policy);
    }
}
