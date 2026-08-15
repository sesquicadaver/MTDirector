using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Expands a managed <see cref="TrafficPredicate"/> into cubes (service-term OR, not per-IP).
/// </summary>
public static class PredicateNormalizer
{
    /// <summary>
    /// Normalizes <paramref name="predicate"/> against typed catalogs.
    /// Missing/unparseable objects surface as <see cref="PolicyComposeCodes.SelectorUnresolved"/>.
    /// </summary>
    public static PredicateAlgebraResult Normalize(
        TrafficPredicate predicate,
        IpAddressFamily family,
        PolicyFilterChain chain,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(services);
        try
        {
            IReadOnlyList<AddressInterval> source = ResolveAddresses(predicate.SourceAddresses, family, addresses);
            IReadOnlyList<AddressInterval> destination = ResolveAddresses(predicate.DestinationAddresses, family, addresses);
            SymbolicSet<Guid> ingress = FromZones(predicate.IngressZones);
            SymbolicSet<Guid> egress = FromZones(predicate.EgressZones);
            SymbolicSet<ConnectionState> states = SymbolicSets.FromNullableList(predicate.ConnectionStates);
            SymbolicSet<ConnectionNatState> nat = SymbolicSets.FromNullableList(predicate.ConnectionNatStates);
            SymbolicSet<AddressType> srcTypes = SymbolicSets.FromNullableList(predicate.SourceAddressTypes);
            SymbolicSet<AddressType> dstTypes = SymbolicSets.FromNullableList(predicate.DestinationAddressTypes);

            ServiceSelectorResolveResult resolved = predicate.Services is null
                ? new ServiceSelectorResolveResult { IsAnyProtocol = true, Terms = [] }
                : ServiceSelectorResolver.Resolve(predicate.Services, family, services);

            List<AtomicTrafficCube> cubes = [];
            if (resolved.IsAnyProtocol)
            {
                cubes.Add(AtomicTrafficCube.Create(
                    family,
                    chain,
                    source,
                    destination,
                    ingress,
                    egress,
                    ProtocolBitSet.Universe,
                    PortSetAlgebra.Universe.Intervals,
                    PortSetAlgebra.Universe.Intervals,
                    icmpSelectors: null,
                    states,
                    nat,
                    srcTypes,
                    dstTypes,
                    predicate.TcpFlags,
                    predicate.IpsecPolicy));
            }
            else
            {
                foreach (ServiceTerm term in resolved.Terms)
                {
                    cubes.Add(AtomicTrafficCube.Create(
                        family,
                        chain,
                        source,
                        destination,
                        ingress,
                        egress,
                        ProtocolBitSet.From(term.Protocol),
                        term.SourcePorts?.Intervals ?? PortSetAlgebra.Universe.Intervals,
                        term.DestinationPorts?.Intervals ?? PortSetAlgebra.Universe.Intervals,
                        term.IcmpSelectors,
                        states,
                        nat,
                        srcTypes,
                        dstTypes,
                        predicate.TcpFlags,
                        predicate.IpsecPolicy));
                }
            }

            return NormalizedPredicate.Create(cubes, PredicateAlgebraCodes.MaxCubesPerRule);
        }
        catch (DomainInvariantException ex)
        {
            return PredicateAlgebraResult.Fail(
                PolicyComposeCodes.SelectorUnresolved,
                ex.Message);
        }
    }

    private static IReadOnlyList<AddressInterval> ResolveAddresses(
        AddressSelector? selector,
        IpAddressFamily family,
        IReadOnlyDictionary<AddressObjectId, AddressObject> catalog)
    {
        if (selector is null)
        {
            return [AddressInterval.Universe(family)];
        }

        return AddressSelectorResolver.Resolve(selector, family, catalog).Intervals;
    }

    private static SymbolicSet<Guid> FromZones(ZoneSelector? selector)
    {
        if (selector is null)
        {
            return SymbolicSets.Universe<Guid>();
        }

        if (selector.Include.Count == 0)
        {
            return SymbolicSets.UniverseMinus(selector.Exclude.Select(static z => z.Value));
        }

        HashSet<Guid> include = selector.Include.Select(static z => z.Value).ToHashSet();
        include.ExceptWith(selector.Exclude.Select(static z => z.Value));
        return SymbolicSets.Finite(include);
    }
}
