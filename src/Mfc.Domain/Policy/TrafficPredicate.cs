using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Typed managed traffic matchers for a policy rule (Policy Model §24).
/// No raw RouterOS matcher string surface.
/// </summary>
public sealed class TrafficPredicate
{
    private readonly bool _isTcpOnly;

    public AddressSelector? SourceAddresses { get; }

    public AddressSelector? DestinationAddresses { get; }

    public ZoneSelector? IngressZones { get; }

    public ZoneSelector? EgressZones { get; }

    public ServiceSelector? Services { get; }

    public IReadOnlyList<ConnectionState>? ConnectionStates { get; }

    public IReadOnlyList<ConnectionNatState>? ConnectionNatStates { get; }

    public IReadOnlyList<AddressType>? SourceAddressTypes { get; }

    public IReadOnlyList<AddressType>? DestinationAddressTypes { get; }

    public TcpFlagConstraint? TcpFlags { get; }

    public IpsecPolicyPredicate? IpsecPolicy { get; }

    private TrafficPredicate(
        AddressSelector? sourceAddresses,
        AddressSelector? destinationAddresses,
        ZoneSelector? ingressZones,
        ZoneSelector? egressZones,
        ServiceSelector? services,
        IReadOnlyList<ConnectionState>? connectionStates,
        IReadOnlyList<ConnectionNatState>? connectionNatStates,
        IReadOnlyList<AddressType>? sourceAddressTypes,
        IReadOnlyList<AddressType>? destinationAddressTypes,
        TcpFlagConstraint? tcpFlags,
        IpsecPolicyPredicate? ipsecPolicy,
        bool isTcpOnly)
    {
        SourceAddresses = sourceAddresses;
        DestinationAddresses = destinationAddresses;
        IngressZones = ingressZones;
        EgressZones = egressZones;
        Services = services;
        ConnectionStates = connectionStates;
        ConnectionNatStates = connectionNatStates;
        SourceAddressTypes = sourceAddressTypes;
        DestinationAddressTypes = destinationAddressTypes;
        TcpFlags = tcpFlags;
        IpsecPolicy = ipsecPolicy;
        _isTcpOnly = isTcpOnly;
    }

    /// <summary>
    /// Creates a predicate. Optional <paramref name="serviceCatalog"/> is used only to evaluate
    /// <see cref="IsTcpOnly"/> for TCP_RESET validation (empty/any-protocol is never TCP-only).
    /// </summary>
    public static TrafficPredicate Create(
        AddressSelector? sourceAddresses = null,
        AddressSelector? destinationAddresses = null,
        ZoneSelector? ingressZones = null,
        ZoneSelector? egressZones = null,
        ServiceSelector? services = null,
        IEnumerable<ConnectionState>? connectionStates = null,
        IEnumerable<ConnectionNatState>? connectionNatStates = null,
        IEnumerable<AddressType>? sourceAddressTypes = null,
        IEnumerable<AddressType>? destinationAddressTypes = null,
        TcpFlagConstraint? tcpFlags = null,
        IpsecPolicyPredicate? ipsecPolicy = null,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject>? serviceCatalog = null)
    {
        IReadOnlyList<ConnectionState>? states = NormalizeSet(connectionStates, "connection_states");
        IReadOnlyList<ConnectionNatState>? natStates = NormalizeSet(connectionNatStates, "connection_nat_states");
        IReadOnlyList<AddressType>? srcTypes = NormalizeSet(sourceAddressTypes, "source_address_types");
        IReadOnlyList<AddressType>? dstTypes = NormalizeSet(destinationAddressTypes, "destination_address_types");

        bool isTcpOnly = EvaluateIsTcpOnly(services, serviceCatalog);
        return new TrafficPredicate(
            sourceAddresses,
            destinationAddresses,
            ingressZones,
            egressZones,
            services,
            states,
            natStates,
            srcTypes,
            dstTypes,
            tcpFlags,
            ipsecPolicy,
            isTcpOnly);
    }

    /// <summary>
    /// Reader/reconstitute path: preserves structural fields without catalog-based TCP evaluation
    /// (stored <paramref name="isTcpOnly"/> bit is not persisted; defaults to false).
    /// </summary>
    public static TrafficPredicate Reconstitute(
        AddressSelector? sourceAddresses = null,
        AddressSelector? destinationAddresses = null,
        ZoneSelector? ingressZones = null,
        ZoneSelector? egressZones = null,
        ServiceSelector? services = null,
        IEnumerable<ConnectionState>? connectionStates = null,
        IEnumerable<ConnectionNatState>? connectionNatStates = null,
        IEnumerable<AddressType>? sourceAddressTypes = null,
        IEnumerable<AddressType>? destinationAddressTypes = null,
        TcpFlagConstraint? tcpFlags = null,
        IpsecPolicyPredicate? ipsecPolicy = null,
        bool isTcpOnly = false)
    {
        IReadOnlyList<ConnectionState>? states = NormalizeSet(connectionStates, "connection_states");
        IReadOnlyList<ConnectionNatState>? natStates = NormalizeSet(connectionNatStates, "connection_nat_states");
        IReadOnlyList<AddressType>? srcTypes = NormalizeSet(sourceAddressTypes, "source_address_types");
        IReadOnlyList<AddressType>? dstTypes = NormalizeSet(destinationAddressTypes, "destination_address_types");
        return new TrafficPredicate(
            sourceAddresses,
            destinationAddresses,
            ingressZones,
            egressZones,
            services,
            states,
            natStates,
            srcTypes,
            dstTypes,
            tcpFlags,
            ipsecPolicy,
            isTcpOnly);
    }

    /// <summary>
    /// True only when services constrain the rule to TCP-only traffic.
    /// Empty/any-protocol selectors are never TCP-only.
    /// </summary>
    public bool IsTcpOnly() => _isTcpOnly;

    private static bool EvaluateIsTcpOnly(
        ServiceSelector? services,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject>? catalog)
    {
        if (services is null || services.MatchesAnyProtocol)
        {
            return false;
        }

        if (catalog is null)
        {
            return false;
        }

        foreach (ServiceObjectId id in services.Include)
        {
            if (!catalog.TryGetValue(id, out ServiceObject? obj))
            {
                return false;
            }

            if (obj.Terms.Count == 0)
            {
                return false;
            }

            foreach (ServiceTerm term in obj.Terms)
            {
                if (term.Protocol.IsAny || term.Protocol.Number != IpProtocol.Tcp)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static T[]? NormalizeSet<T>(IEnumerable<T>? values, string label)
        where T : struct, Enum
    {
        if (values is null)
        {
            return null;
        }

        T[] array = values.ToArray();
        if (array.Length == 0)
        {
            return null;
        }

        HashSet<T> seen = [];
        foreach (T value in array)
        {
            if (!Enum.IsDefined(value))
            {
                throw new DomainInvariantException($"Unknown value in {label}: '{value}'.");
            }

            if (!seen.Add(value))
            {
                throw new DomainInvariantException($"Duplicate value in {label}: '{value}'.");
            }
        }

        return array.OrderBy(static v => v).ToArray();
    }
}
