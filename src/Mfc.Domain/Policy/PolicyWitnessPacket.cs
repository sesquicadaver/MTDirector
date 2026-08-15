using Mfc.Domain.Inventory;

namespace Mfc.Domain.Policy;

/// <summary>
/// Synthetic witness packet derived from a normalized cube (Policy Model §43).
/// Not a capture; contains no credentials.
/// </summary>
public sealed class PolicyWitnessPacket
{
    public required IpAddressFamily Family { get; init; }

    public required PolicyFilterChain Chain { get; init; }

    public required string SourceAddress { get; init; }

    public required string DestinationAddress { get; init; }

    public byte? Protocol { get; init; }

    public ushort? SourcePort { get; init; }

    public ushort? DestinationPort { get; init; }

    public Guid? IngressZoneId { get; init; }

    public Guid? EgressZoneId { get; init; }

    public ConnectionState? ConnectionState { get; init; }

    public ConnectionNatState? ConnectionNatState { get; init; }

    public AddressType? SourceAddressType { get; init; }

    public AddressType? DestinationAddressType { get; init; }

    public TcpHeaderBit? TcpFlagPresent { get; init; }

    public byte? IcmpType { get; init; }

    public byte? IcmpCode { get; init; }

    public IpsecDirection? IpsecDirection { get; init; }

    /// <summary>Picks a concrete representative from the first non-empty cube.</summary>
    public static PolicyWitnessPacket? TryFrom(NormalizedPredicate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        AtomicTrafficCube? cube = predicate.Cubes.FirstOrDefault(static c => !c.IsEmpty);
        return cube is null ? null : FromCube(cube);
    }

    public static PolicyWitnessPacket FromCube(AtomicTrafficCube cube)
    {
        ArgumentNullException.ThrowIfNull(cube);
        if (cube.IsEmpty || cube.SourceAddresses.Count == 0 || cube.DestinationAddresses.Count == 0)
        {
            throw new DomainInvariantException("Witness packet requires a non-empty traffic cube.");
        }

        AddressInterval source = cube.SourceAddresses[0];
        AddressInterval destination = cube.DestinationAddresses[0];
        byte? protocol = FirstProtocol(cube.Protocols);
        return new PolicyWitnessPacket
        {
            Family = cube.Family,
            Chain = cube.Chain,
            SourceAddress = AddressInterval.FromNumeric(source.Family, source.Start).ToString(),
            DestinationAddress = AddressInterval.FromNumeric(destination.Family, destination.Start).ToString(),
            Protocol = protocol,
            SourcePort = cube.SourcePorts.Count == 0 ? null : cube.SourcePorts[0].Start,
            DestinationPort = cube.DestinationPorts.Count == 0 ? null : cube.DestinationPorts[0].Start,
            IngressZoneId = FirstFinite(cube.IngressZones),
            EgressZoneId = FirstFinite(cube.EgressZones),
            ConnectionState = FirstFinite(cube.ConnectionStates),
            ConnectionNatState = FirstFinite(cube.ConnectionNatStates),
            SourceAddressType = FirstFinite(cube.SourceAddressTypes),
            DestinationAddressType = FirstFinite(cube.DestinationAddressTypes),
            TcpFlagPresent = cube.TcpFlags is { RequiredPresent.Count: > 0 } flags
                ? flags.RequiredPresent[0]
                : null,
            IcmpType = cube.IcmpSelectors is { Items.Count: > 0 } icmp ? icmp.Items[0].Type : null,
            IcmpCode = cube.IcmpSelectors is { Items.Count: > 0 } icmpCode ? icmpCode.Items[0].Code : null,
            IpsecDirection = cube.IpsecPolicy?.Direction,
        };
    }

    private static byte? FirstProtocol(ProtocolBitSet protocols)
    {
        if (protocols.IsEmpty)
        {
            return null;
        }

        if (protocols.IsUniverse)
        {
            return IpProtocol.Tcp;
        }

        for (int i = 0; i < 256; i++)
        {
            if (protocols.Contains((byte)i))
            {
                return (byte)i;
            }
        }

        return null;
    }

    private static T? FirstFinite<T>(SymbolicSet<T> set)
        where T : struct
        => set.IsUniverse || set.IsEmpty ? null : set.Members.OrderBy(static v => v).First();
}
