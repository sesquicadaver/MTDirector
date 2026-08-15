using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Parses only the compose objects referenced by exception proofs (M2-09 L4).</summary>
public static class PredicateCatalogBuilder
{
    /// <summary>
    /// Builds typed catalogs for UUIDs referenced by <paramref name="predicates"/>.
    /// Returns an error message on missing or unparseable JSON; otherwise null.
    /// </summary>
    public static string? TryBuild(
        IEnumerable<TrafficPredicate> predicates,
        IReadOnlyDictionary<Guid, ComposedPolicyObject> addressJson,
        IReadOnlyDictionary<Guid, ComposedPolicyObject> serviceJson,
        out Dictionary<AddressObjectId, AddressObject> addresses,
        out Dictionary<ServiceObjectId, ServiceObject> services)
    {
        ArgumentNullException.ThrowIfNull(predicates);
        ArgumentNullException.ThrowIfNull(addressJson);
        ArgumentNullException.ThrowIfNull(serviceJson);
        addresses = [];
        services = [];
        HashSet<Guid> addressIds = [];
        HashSet<Guid> serviceIds = [];
        foreach (TrafficPredicate predicate in predicates)
        {
            CollectAddresses(predicate.SourceAddresses, addressIds);
            CollectAddresses(predicate.DestinationAddresses, addressIds);
            if (predicate.Services is not null)
            {
                foreach (ServiceObjectId id in predicate.Services.Include)
                {
                    serviceIds.Add(id.Value);
                }
            }
        }

        foreach (Guid id in addressIds)
        {
            if (!addressJson.TryGetValue(id, out ComposedPolicyObject? composed))
            {
                return $"Address selector UUID '{id:D}' is unresolved.";
            }

            if (!PolicyObjectJsonReader.TryReadAddress(
                    composed.Element,
                    composed.Identity,
                    out AddressObject? parsed,
                    out string? error)
                || parsed is null)
            {
                return error ?? $"Address object '{id:D}' is not parseable.";
            }

            addresses[parsed.Id] = parsed;
        }

        foreach (Guid id in serviceIds)
        {
            if (!serviceJson.TryGetValue(id, out ComposedPolicyObject? composed))
            {
                return $"Service selector UUID '{id:D}' is unresolved.";
            }

            if (!PolicyObjectJsonReader.TryReadService(
                    composed.Element,
                    composed.Identity,
                    out ServiceObject? parsed,
                    out string? error)
                || parsed is null)
            {
                return error ?? $"Service object '{id:D}' is not parseable.";
            }

            services[parsed.Id] = parsed;
        }

        return null;
    }

    private static void CollectAddresses(AddressSelector? selector, HashSet<Guid> ids)
    {
        if (selector is null)
        {
            return;
        }

        foreach (AddressObjectId id in selector.Include.Concat(selector.Exclude))
        {
            ids.Add(id.Value);
        }
    }
}
