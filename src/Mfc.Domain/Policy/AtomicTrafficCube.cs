using Mfc.Domain.Inventory;

namespace Mfc.Domain.Policy;

/// <summary>
/// One atomic traffic cube (Policy Model §37). Zones are UUID sets (M2-09 L2), not interfaces.
/// Null TCP-flag / IPsec constraints mean unconstrained.
/// </summary>
public sealed class AtomicTrafficCube
{
    private AtomicTrafficCube(
        IpAddressFamily family,
        PolicyFilterChain chain,
        IReadOnlyList<AddressInterval> sourceAddresses,
        IReadOnlyList<AddressInterval> destinationAddresses,
        SymbolicSet<Guid> ingressZones,
        SymbolicSet<Guid> egressZones,
        ProtocolBitSet protocols,
        IReadOnlyList<PortInterval> sourcePorts,
        IReadOnlyList<PortInterval> destinationPorts,
        IcmpSelectorSet? icmpSelectors,
        SymbolicSet<ConnectionState> connectionStates,
        SymbolicSet<ConnectionNatState> connectionNatStates,
        SymbolicSet<AddressType> sourceAddressTypes,
        SymbolicSet<AddressType> destinationAddressTypes,
        TcpFlagConstraint? tcpFlags,
        IpsecPolicyPredicate? ipsecPolicy)
    {
        Family = family;
        Chain = chain;
        SourceAddresses = sourceAddresses;
        DestinationAddresses = destinationAddresses;
        IngressZones = ingressZones;
        EgressZones = egressZones;
        Protocols = protocols;
        SourcePorts = sourcePorts;
        DestinationPorts = destinationPorts;
        IcmpSelectors = icmpSelectors;
        ConnectionStates = connectionStates;
        ConnectionNatStates = connectionNatStates;
        SourceAddressTypes = sourceAddressTypes;
        DestinationAddressTypes = destinationAddressTypes;
        TcpFlags = tcpFlags;
        IpsecPolicy = ipsecPolicy;
    }

    public IpAddressFamily Family { get; }

    public PolicyFilterChain Chain { get; }

    public IReadOnlyList<AddressInterval> SourceAddresses { get; }

    public IReadOnlyList<AddressInterval> DestinationAddresses { get; }

    public SymbolicSet<Guid> IngressZones { get; }

    public SymbolicSet<Guid> EgressZones { get; }

    public ProtocolBitSet Protocols { get; }

    public IReadOnlyList<PortInterval> SourcePorts { get; }

    public IReadOnlyList<PortInterval> DestinationPorts { get; }

    /// <summary>Null means any ICMP type/code (when the protocol set includes ICMP).</summary>
    public IcmpSelectorSet? IcmpSelectors { get; }

    public SymbolicSet<ConnectionState> ConnectionStates { get; }

    public SymbolicSet<ConnectionNatState> ConnectionNatStates { get; }

    public SymbolicSet<AddressType> SourceAddressTypes { get; }

    public SymbolicSet<AddressType> DestinationAddressTypes { get; }

    public TcpFlagConstraint? TcpFlags { get; }

    public IpsecPolicyPredicate? IpsecPolicy { get; }

    public bool IsEmpty
        => SourceAddresses.Count == 0
           || DestinationAddresses.Count == 0
           || IngressZones.IsEmpty
           || EgressZones.IsEmpty
           || Protocols.IsEmpty
           || ConnectionStates.IsEmpty
           || ConnectionNatStates.IsEmpty
           || SourceAddressTypes.IsEmpty
           || DestinationAddressTypes.IsEmpty
           || (HasPortCapableProtocol(Protocols) && (SourcePorts.Count == 0 || DestinationPorts.Count == 0))
           || (HasIcmpProtocol(Protocols) && IcmpSelectors is { Items.Count: 0 });

    public static AtomicTrafficCube Create(
        IpAddressFamily family,
        PolicyFilterChain chain,
        IReadOnlyList<AddressInterval> sourceAddresses,
        IReadOnlyList<AddressInterval> destinationAddresses,
        SymbolicSet<Guid>? ingressZones = null,
        SymbolicSet<Guid>? egressZones = null,
        ProtocolBitSet? protocols = null,
        IReadOnlyList<PortInterval>? sourcePorts = null,
        IReadOnlyList<PortInterval>? destinationPorts = null,
        IcmpSelectorSet? icmpSelectors = null,
        SymbolicSet<ConnectionState>? connectionStates = null,
        SymbolicSet<ConnectionNatState>? connectionNatStates = null,
        SymbolicSet<AddressType>? sourceAddressTypes = null,
        SymbolicSet<AddressType>? destinationAddressTypes = null,
        TcpFlagConstraint? tcpFlags = null,
        IpsecPolicyPredicate? ipsecPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(sourceAddresses);
        ArgumentNullException.ThrowIfNull(destinationAddresses);
        return new AtomicTrafficCube(
            family,
            chain,
            AddressSetAlgebra.Normalize(sourceAddresses),
            AddressSetAlgebra.Normalize(destinationAddresses),
            ingressZones ?? SymbolicSets.Universe<Guid>(),
            egressZones ?? SymbolicSets.Universe<Guid>(),
            protocols ?? ProtocolBitSet.Universe,
            PortSet.Normalize(sourcePorts ?? PortSetAlgebra.Universe.Intervals),
            PortSet.Normalize(destinationPorts ?? PortSetAlgebra.Universe.Intervals),
            icmpSelectors,
            connectionStates ?? SymbolicSets.Universe<ConnectionState>(),
            connectionNatStates ?? SymbolicSets.Universe<ConnectionNatState>(),
            sourceAddressTypes ?? SymbolicSets.Universe<AddressType>(),
            destinationAddressTypes ?? SymbolicSets.Universe<AddressType>(),
            tcpFlags,
            ipsecPolicy);
    }

    /// <summary>Packet-space subset: every packet in <paramref name="inner"/> also matches <paramref name="cover"/>.</summary>
    public static bool IsSubset(AtomicTrafficCube inner, AtomicTrafficCube cover)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(cover);
        if (inner.IsEmpty)
        {
            return true;
        }

        if (cover.IsEmpty || inner.Family != cover.Family || inner.Chain != cover.Chain)
        {
            return false;
        }

        if (!AddressSetAlgebra.IsSubset(inner.SourceAddresses, cover.SourceAddresses)
            || !AddressSetAlgebra.IsSubset(inner.DestinationAddresses, cover.DestinationAddresses)
            || !inner.IngressZones.IsSubsetOf(cover.IngressZones)
            || !inner.EgressZones.IsSubsetOf(cover.EgressZones)
            || !ProtocolBitSet.IsSubset(inner.Protocols, cover.Protocols)
            || !inner.ConnectionStates.IsSubsetOf(cover.ConnectionStates)
            || !inner.ConnectionNatStates.IsSubsetOf(cover.ConnectionNatStates)
            || !inner.SourceAddressTypes.IsSubsetOf(cover.SourceAddressTypes)
            || !inner.DestinationAddressTypes.IsSubsetOf(cover.DestinationAddressTypes)
            || !FlagsSubset(inner.TcpFlags, cover.TcpFlags)
            || !IpsecSubset(inner.IpsecPolicy, cover.IpsecPolicy))
        {
            return false;
        }

        if (HasPortCapableProtocol(inner.Protocols)
            && (!PortSetAlgebra.IsSubset(inner.SourcePorts, cover.SourcePorts)
                || !PortSetAlgebra.IsSubset(inner.DestinationPorts, cover.DestinationPorts)))
        {
            return false;
        }

        return !HasIcmpProtocol(inner.Protocols) || IcmpSubset(inner.IcmpSelectors, cover.IcmpSelectors);
    }

    /// <summary>True when the cubes share at least one packet.</summary>
    public static bool Overlaps(AtomicTrafficCube left, AtomicTrafficCube right)
        => Intersect(left, right) is { IsEmpty: false };

    /// <summary>Returns null when the intersection is empty.</summary>
    public static AtomicTrafficCube? Intersect(AtomicTrafficCube left, AtomicTrafficCube right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Family != right.Family || left.Chain != right.Chain)
        {
            return null;
        }

        TcpFlagConstraint? flags = IntersectFlags(left.TcpFlags, right.TcpFlags, out bool flagsEmpty);
        if (flagsEmpty)
        {
            return null;
        }

        IpsecPolicyPredicate? ipsec = IntersectIpsec(left.IpsecPolicy, right.IpsecPolicy, out bool ipsecEmpty);
        if (ipsecEmpty)
        {
            return null;
        }

        AtomicTrafficCube cube = Create(
            left.Family,
            left.Chain,
            AddressSetAlgebra.Intersect(left.SourceAddresses, right.SourceAddresses),
            AddressSetAlgebra.Intersect(left.DestinationAddresses, right.DestinationAddresses),
            left.IngressZones.Intersect(right.IngressZones),
            left.EgressZones.Intersect(right.EgressZones),
            ProtocolBitSet.Intersect(left.Protocols, right.Protocols),
            PortSetAlgebra.Intersect(left.SourcePorts, right.SourcePorts),
            PortSetAlgebra.Intersect(left.DestinationPorts, right.DestinationPorts),
            IntersectIcmp(left.IcmpSelectors, right.IcmpSelectors),
            left.ConnectionStates.Intersect(right.ConnectionStates),
            left.ConnectionNatStates.Intersect(right.ConnectionNatStates),
            left.SourceAddressTypes.Intersect(right.SourceAddressTypes),
            left.DestinationAddressTypes.Intersect(right.DestinationAddressTypes),
            flags,
            ipsec);
        return cube.IsEmpty ? null : cube;
    }

    /// <summary>
    /// Shannon box difference. ICMP/flags/IPsec residuals that cannot be represented as a cube
    /// are omitted (under-approximate complement); <c>A−A</c> is empty via the subset short-circuit.
    /// </summary>
    public static IReadOnlyList<AtomicTrafficCube> Subtract(AtomicTrafficCube include, AtomicTrafficCube exclude)
    {
        ArgumentNullException.ThrowIfNull(include);
        ArgumentNullException.ThrowIfNull(exclude);
        if (include.IsEmpty)
        {
            return [];
        }

        if (!Overlaps(include, exclude))
        {
            return [include];
        }

        if (IsSubset(include, exclude))
        {
            return [];
        }

        List<AtomicTrafficCube> pieces = [];
        AtomicTrafficCube prefix = include;

        IReadOnlyList<AddressInterval> extraSrc = AddressSetAlgebra.Subtract(prefix.SourceAddresses, exclude.SourceAddresses);
        Emit(pieces, WithSource(prefix, extraSrc));
        prefix = WithSource(prefix, AddressSetAlgebra.Intersect(prefix.SourceAddresses, exclude.SourceAddresses));
        if (prefix.IsEmpty)
        {
            return pieces;
        }

        IReadOnlyList<AddressInterval> extraDst = AddressSetAlgebra.Subtract(prefix.DestinationAddresses, exclude.DestinationAddresses);
        Emit(pieces, WithDestination(prefix, extraDst));
        prefix = WithDestination(prefix, AddressSetAlgebra.Intersect(prefix.DestinationAddresses, exclude.DestinationAddresses));
        if (prefix.IsEmpty)
        {
            return pieces;
        }

        SymbolicSet<Guid> extraIng = prefix.IngressZones.Subtract(exclude.IngressZones);
        Emit(pieces, WithIngress(prefix, extraIng));
        prefix = WithIngress(prefix, prefix.IngressZones.Intersect(exclude.IngressZones));
        if (prefix.IsEmpty)
        {
            return pieces;
        }

        SymbolicSet<Guid> extraEgr = prefix.EgressZones.Subtract(exclude.EgressZones);
        Emit(pieces, WithEgress(prefix, extraEgr));
        prefix = WithEgress(prefix, prefix.EgressZones.Intersect(exclude.EgressZones));
        if (prefix.IsEmpty)
        {
            return pieces;
        }

        ProtocolBitSet extraProto = ProtocolBitSet.Subtract(prefix.Protocols, exclude.Protocols);
        Emit(pieces, WithProtocols(prefix, extraProto));
        prefix = WithProtocols(prefix, ProtocolBitSet.Intersect(prefix.Protocols, exclude.Protocols));
        if (prefix.IsEmpty)
        {
            return pieces;
        }

        IReadOnlyList<PortInterval> extraSrcPorts = PortSetAlgebra.Subtract(prefix.SourcePorts, exclude.SourcePorts);
        Emit(pieces, WithSourcePorts(prefix, extraSrcPorts));
        prefix = WithSourcePorts(prefix, PortSetAlgebra.Intersect(prefix.SourcePorts, exclude.SourcePorts));
        if (prefix.IsEmpty)
        {
            return pieces;
        }

        IReadOnlyList<PortInterval> extraDstPorts = PortSetAlgebra.Subtract(prefix.DestinationPorts, exclude.DestinationPorts);
        Emit(pieces, WithDestinationPorts(prefix, extraDstPorts));
        prefix = WithDestinationPorts(prefix, PortSetAlgebra.Intersect(prefix.DestinationPorts, exclude.DestinationPorts));
        if (prefix.IsEmpty)
        {
            return pieces;
        }

        SymbolicSet<ConnectionState> extraStates = prefix.ConnectionStates.Subtract(exclude.ConnectionStates);
        Emit(pieces, WithConnectionStates(prefix, extraStates));
        prefix = WithConnectionStates(prefix, prefix.ConnectionStates.Intersect(exclude.ConnectionStates));
        if (prefix.IsEmpty)
        {
            return pieces;
        }

        SymbolicSet<ConnectionNatState> extraNat = prefix.ConnectionNatStates.Subtract(exclude.ConnectionNatStates);
        Emit(pieces, WithNatStates(prefix, extraNat));
        prefix = WithNatStates(prefix, prefix.ConnectionNatStates.Intersect(exclude.ConnectionNatStates));
        if (prefix.IsEmpty)
        {
            return pieces;
        }

        SymbolicSet<AddressType> extraSrcTypes = prefix.SourceAddressTypes.Subtract(exclude.SourceAddressTypes);
        Emit(pieces, WithSourceTypes(prefix, extraSrcTypes));
        prefix = WithSourceTypes(prefix, prefix.SourceAddressTypes.Intersect(exclude.SourceAddressTypes));
        if (prefix.IsEmpty)
        {
            return pieces;
        }

        SymbolicSet<AddressType> extraDstTypes = prefix.DestinationAddressTypes.Subtract(exclude.DestinationAddressTypes);
        Emit(pieces, WithDestinationTypes(prefix, extraDstTypes));
        return pieces;
    }

    private static void Emit(List<AtomicTrafficCube> pieces, AtomicTrafficCube cube)
    {
        if (!cube.IsEmpty)
        {
            pieces.Add(cube);
        }
    }

    private static AtomicTrafficCube WithSource(AtomicTrafficCube s, IReadOnlyList<AddressInterval> src)
        => Create(s.Family, s.Chain, src, s.DestinationAddresses, s.IngressZones, s.EgressZones, s.Protocols, s.SourcePorts, s.DestinationPorts, s.IcmpSelectors, s.ConnectionStates, s.ConnectionNatStates, s.SourceAddressTypes, s.DestinationAddressTypes, s.TcpFlags, s.IpsecPolicy);

    private static AtomicTrafficCube WithDestination(AtomicTrafficCube s, IReadOnlyList<AddressInterval> dst)
        => Create(s.Family, s.Chain, s.SourceAddresses, dst, s.IngressZones, s.EgressZones, s.Protocols, s.SourcePorts, s.DestinationPorts, s.IcmpSelectors, s.ConnectionStates, s.ConnectionNatStates, s.SourceAddressTypes, s.DestinationAddressTypes, s.TcpFlags, s.IpsecPolicy);

    private static AtomicTrafficCube WithIngress(AtomicTrafficCube s, SymbolicSet<Guid> ingress)
        => Create(s.Family, s.Chain, s.SourceAddresses, s.DestinationAddresses, ingress, s.EgressZones, s.Protocols, s.SourcePorts, s.DestinationPorts, s.IcmpSelectors, s.ConnectionStates, s.ConnectionNatStates, s.SourceAddressTypes, s.DestinationAddressTypes, s.TcpFlags, s.IpsecPolicy);

    private static AtomicTrafficCube WithEgress(AtomicTrafficCube s, SymbolicSet<Guid> egress)
        => Create(s.Family, s.Chain, s.SourceAddresses, s.DestinationAddresses, s.IngressZones, egress, s.Protocols, s.SourcePorts, s.DestinationPorts, s.IcmpSelectors, s.ConnectionStates, s.ConnectionNatStates, s.SourceAddressTypes, s.DestinationAddressTypes, s.TcpFlags, s.IpsecPolicy);

    private static AtomicTrafficCube WithProtocols(AtomicTrafficCube s, ProtocolBitSet protocols)
        => Create(s.Family, s.Chain, s.SourceAddresses, s.DestinationAddresses, s.IngressZones, s.EgressZones, protocols, s.SourcePorts, s.DestinationPorts, s.IcmpSelectors, s.ConnectionStates, s.ConnectionNatStates, s.SourceAddressTypes, s.DestinationAddressTypes, s.TcpFlags, s.IpsecPolicy);

    private static AtomicTrafficCube WithSourcePorts(AtomicTrafficCube s, IReadOnlyList<PortInterval> ports)
        => Create(s.Family, s.Chain, s.SourceAddresses, s.DestinationAddresses, s.IngressZones, s.EgressZones, s.Protocols, ports, s.DestinationPorts, s.IcmpSelectors, s.ConnectionStates, s.ConnectionNatStates, s.SourceAddressTypes, s.DestinationAddressTypes, s.TcpFlags, s.IpsecPolicy);

    private static AtomicTrafficCube WithDestinationPorts(AtomicTrafficCube s, IReadOnlyList<PortInterval> ports)
        => Create(s.Family, s.Chain, s.SourceAddresses, s.DestinationAddresses, s.IngressZones, s.EgressZones, s.Protocols, s.SourcePorts, ports, s.IcmpSelectors, s.ConnectionStates, s.ConnectionNatStates, s.SourceAddressTypes, s.DestinationAddressTypes, s.TcpFlags, s.IpsecPolicy);

    private static AtomicTrafficCube WithConnectionStates(AtomicTrafficCube s, SymbolicSet<ConnectionState> states)
        => Create(s.Family, s.Chain, s.SourceAddresses, s.DestinationAddresses, s.IngressZones, s.EgressZones, s.Protocols, s.SourcePorts, s.DestinationPorts, s.IcmpSelectors, states, s.ConnectionNatStates, s.SourceAddressTypes, s.DestinationAddressTypes, s.TcpFlags, s.IpsecPolicy);

    private static AtomicTrafficCube WithNatStates(AtomicTrafficCube s, SymbolicSet<ConnectionNatState> nat)
        => Create(s.Family, s.Chain, s.SourceAddresses, s.DestinationAddresses, s.IngressZones, s.EgressZones, s.Protocols, s.SourcePorts, s.DestinationPorts, s.IcmpSelectors, s.ConnectionStates, nat, s.SourceAddressTypes, s.DestinationAddressTypes, s.TcpFlags, s.IpsecPolicy);

    private static AtomicTrafficCube WithSourceTypes(AtomicTrafficCube s, SymbolicSet<AddressType> types)
        => Create(s.Family, s.Chain, s.SourceAddresses, s.DestinationAddresses, s.IngressZones, s.EgressZones, s.Protocols, s.SourcePorts, s.DestinationPorts, s.IcmpSelectors, s.ConnectionStates, s.ConnectionNatStates, types, s.DestinationAddressTypes, s.TcpFlags, s.IpsecPolicy);

    private static AtomicTrafficCube WithDestinationTypes(AtomicTrafficCube s, SymbolicSet<AddressType> types)
        => Create(s.Family, s.Chain, s.SourceAddresses, s.DestinationAddresses, s.IngressZones, s.EgressZones, s.Protocols, s.SourcePorts, s.DestinationPorts, s.IcmpSelectors, s.ConnectionStates, s.ConnectionNatStates, s.SourceAddressTypes, types, s.TcpFlags, s.IpsecPolicy);

    private static bool HasPortCapableProtocol(ProtocolBitSet protocols)
        => protocols.Contains(IpProtocol.Tcp)
           || protocols.Contains(IpProtocol.Udp)
           || protocols.Contains(IpProtocol.Sctp)
           || protocols.IsUniverse;

    private static bool HasIcmpProtocol(ProtocolBitSet protocols)
        => protocols.Contains(IpProtocol.Icmp)
           || protocols.Contains(IpProtocol.IcmpV6)
           || protocols.IsUniverse;

    private static bool FlagsSubset(TcpFlagConstraint? inner, TcpFlagConstraint? cover)
    {
        if (cover is null)
        {
            return true;
        }

        if (inner is null)
        {
            return false;
        }

        HashSet<TcpHeaderBit> presentInner = inner.RequiredPresent.ToHashSet();
        HashSet<TcpHeaderBit> absentInner = inner.RequiredAbsent.ToHashSet();
        HashSet<TcpHeaderBit> presentCover = cover.RequiredPresent.ToHashSet();
        HashSet<TcpHeaderBit> absentCover = cover.RequiredAbsent.ToHashSet();
        return presentCover.IsSubsetOf(presentInner)
               && absentCover.IsSubsetOf(absentInner)
               && !presentInner.Overlaps(absentCover)
               && !absentInner.Overlaps(presentCover);
    }

    private static TcpFlagConstraint? IntersectFlags(
        TcpFlagConstraint? left,
        TcpFlagConstraint? right,
        out bool empty)
    {
        empty = false;
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        try
        {
            return TcpFlagConstraint.Create(
                left.RequiredPresent.Concat(right.RequiredPresent),
                left.RequiredAbsent.Concat(right.RequiredAbsent));
        }
        catch (DomainInvariantException)
        {
            empty = true;
            return null;
        }
    }

    private static bool IpsecSubset(IpsecPolicyPredicate? inner, IpsecPolicyPredicate? cover)
    {
        if (cover is null)
        {
            return true;
        }

        if (inner is null)
        {
            return false;
        }

        return inner.Direction == cover.Direction && inner.Policy == cover.Policy;
    }

    private static IpsecPolicyPredicate? IntersectIpsec(
        IpsecPolicyPredicate? left,
        IpsecPolicyPredicate? right,
        out bool empty)
    {
        empty = false;
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        if (left.Direction != right.Direction || left.Policy != right.Policy)
        {
            empty = true;
            return null;
        }

        return left;
    }

    private static bool IcmpSubset(IcmpSelectorSet? inner, IcmpSelectorSet? cover)
    {
        if (cover is null)
        {
            return true;
        }

        if (inner is null)
        {
            return false;
        }

        foreach (IcmpSelector item in inner.Items)
        {
            if (!IcmpCovered(item, cover))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IcmpCovered(IcmpSelector inner, IcmpSelectorSet cover)
    {
        foreach (IcmpSelector item in cover.Items)
        {
            if (item.Type != inner.Type)
            {
                continue;
            }

            if (inner.Code is null)
            {
                return item.Code is null;
            }

            if (item.Code is null || item.Code == inner.Code)
            {
                return true;
            }
        }

        return false;
    }

    private static IcmpSelectorSet? IntersectIcmp(IcmpSelectorSet? left, IcmpSelectorSet? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        List<IcmpSelector> items = [];
        foreach (IcmpSelector a in left.Items)
        {
            foreach (IcmpSelector b in right.Items)
            {
                if (a.Type != b.Type)
                {
                    continue;
                }

                if (a.Code is null)
                {
                    items.Add(b);
                }
                else if (b.Code is null || a.Code == b.Code)
                {
                    items.Add(a);
                }
            }
        }

        IcmpSelectorSet set = IcmpSelectorSet.Create(items);
        return set.Items.Count == 0 ? IcmpSelectorSet.Empty : set;
    }
}
