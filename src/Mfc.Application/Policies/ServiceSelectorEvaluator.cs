using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Policies;

/// <summary>
/// Application-facing service selector resolution with UUID visibility checks (M2-04).
/// </summary>
public static class ServiceSelectorEvaluator
{
    public static ServiceSelectorResolveResult Resolve(
        ServiceSelector selector,
        IpAddressFamily ruleFamily,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> catalog,
        AddressConsumerContext consumer)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(consumer);

        foreach (ServiceObjectId id in selector.Include)
        {
            if (!catalog.TryGetValue(id, out ServiceObject? obj))
            {
                throw new DomainInvariantException($"Service object '{id}' was not found in the catalog.");
            }

            ServiceObjectVisibility.EnsureCanReference(consumer, obj);
        }

        return ServiceSelectorResolver.Resolve(selector, ruleFamily, catalog);
    }
}
