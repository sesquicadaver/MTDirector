using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Policies;

/// <summary>
/// Application-facing address selector resolution with UUID visibility checks (M2-03).
/// </summary>
public static class AddressSelectorEvaluator
{
    public static AddressSelectorResolveResult Resolve(
        AddressSelector selector,
        IpAddressFamily family,
        IReadOnlyDictionary<AddressObjectId, AddressObject> catalog,
        AddressConsumerContext consumer)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(consumer);

        foreach (AddressObjectId id in selector.Include.Concat(selector.Exclude))
        {
            if (!catalog.TryGetValue(id, out AddressObject? obj))
            {
                throw new DomainInvariantException($"Address object '{id}' was not found in the catalog.");
            }

            AddressObjectVisibility.EnsureCanReference(consumer, obj);
        }

        return AddressSelectorResolver.Resolve(selector, family, catalog);
    }
}
