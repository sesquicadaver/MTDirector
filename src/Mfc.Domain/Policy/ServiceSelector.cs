using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Include-only service selector; negation is unsupported (Policy Model §19).</summary>
public sealed class ServiceSelector
{
    public IReadOnlyList<ServiceObjectId> Include { get; }

    private ServiceSelector(IReadOnlyList<ServiceObjectId> include) => Include = include;

    public static ServiceSelector Create(IEnumerable<ServiceObjectId>? include = null)
    {
        ServiceObjectId[] ids = (include ?? []).ToArray();
        HashSet<Guid> seen = [];
        foreach (ServiceObjectId id in ids)
        {
            if (!seen.Add(id.Value))
            {
                throw new DomainInvariantException("Duplicate service object id in selector include.");
            }
        }

        return new ServiceSelector(ids);
    }

    /// <summary>Empty include means any IP protocol.</summary>
    public bool MatchesAnyProtocol => Include.Count == 0;
}

/// <summary>Resolved service selector surface.</summary>
public sealed class ServiceSelectorResolveResult
{
    public required bool IsAnyProtocol { get; init; }

    public required IReadOnlyList<ServiceTerm> Terms { get; init; }
}

/// <summary>Resolves service selectors and enforces ICMP family constraints.</summary>
public static class ServiceSelectorResolver
{
    public static ServiceSelectorResolveResult Resolve(
        ServiceSelector selector,
        IpAddressFamily ruleFamily,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> catalog)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(catalog);

        if (selector.MatchesAnyProtocol)
        {
            return new ServiceSelectorResolveResult
            {
                IsAnyProtocol = true,
                Terms = [],
            };
        }

        List<ServiceTerm> terms = [];
        foreach (ServiceObjectId id in selector.Include)
        {
            if (!catalog.TryGetValue(id, out ServiceObject? obj))
            {
                throw new DomainInvariantException($"Service object '{id}' was not found in the catalog.");
            }

            EnsureFamilyCompatible(obj, ruleFamily);
            terms.AddRange(obj.Terms);
        }

        return new ServiceSelectorResolveResult
        {
            IsAnyProtocol = false,
            Terms = ServiceObject.CanonicalizeTerms(terms),
        };
    }

    public static void EnsureFamilyCompatible(ServiceObject obj, IpAddressFamily ruleFamily)
    {
        ArgumentNullException.ThrowIfNull(obj);
        foreach (ServiceTerm term in obj.Terms)
        {
            if (ruleFamily == IpAddressFamily.IPv4 && term.Protocol.IsIcmpV6Protocol)
            {
                throw new DomainInvariantException("IPv4 rules cannot reference ICMPv6 service terms.");
            }

            if (ruleFamily == IpAddressFamily.IPv6 && term.Protocol.IsIcmpV4)
            {
                throw new DomainInvariantException("IPv6 rules cannot reference ICMP (v4) service terms.");
            }

            if (term.IcmpSelectors is not null)
            {
                if (ruleFamily == IpAddressFamily.IPv4 && !term.Protocol.IsIcmpV4)
                {
                    throw new DomainInvariantException("ICMP selectors on a non-ICMP protocol are invalid for IPv4.");
                }

                if (ruleFamily == IpAddressFamily.IPv6 && !term.Protocol.IsIcmpV6Protocol)
                {
                    throw new DomainInvariantException("ICMP selectors on a non-ICMPv6 protocol are invalid for IPv6.");
                }
            }
        }
    }
}

/// <summary>UUID-based scope visibility for service objects (Policy Model §11.1).</summary>
public static class ServiceObjectVisibility
{
    public static bool CanReference(AddressConsumerContext consumer, ServiceObject referenced)
    {
        ArgumentNullException.ThrowIfNull(referenced);
        return CanReference(
            consumer,
            new PolicyObjectIdentity(
                referenced.Id.Value,
                referenced.OwnerScope,
                referenced.OwnerId,
                referenced.ExceptionRevisionId));
    }

    /// <summary>Compose-time visibility against a lightweight object identity.</summary>
    public static bool CanReference(AddressConsumerContext consumer, PolicyObjectIdentity referenced)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(referenced);

        return referenced.OwnerScope switch
        {
            PolicyObjectOwnerScope.Company => true,
            PolicyObjectOwnerScope.Site => CanSeeSite(consumer, referenced.OwnerId!.Value),
            PolicyObjectOwnerScope.Node => CanSeeNode(consumer, referenced.OwnerId!.Value),
            PolicyObjectOwnerScope.Exception =>
                consumer.Scope == PolicyObjectOwnerScope.Exception
                && consumer.ExceptionRevisionId == referenced.ExceptionRevisionId,
            _ => false,
        };
    }

    public static void EnsureCanReference(AddressConsumerContext consumer, ServiceObject referenced)
    {
        if (!CanReference(consumer, referenced))
        {
            throw new DomainInvariantException(
                $"Service object '{referenced.Id}' is not visible to consumer scope {consumer.Scope}.");
        }
    }

    private static bool CanSeeSite(AddressConsumerContext consumer, Guid siteId)
        => consumer.Scope switch
        {
            PolicyObjectOwnerScope.Site => consumer.OwnerId == siteId,
            PolicyObjectOwnerScope.Node => consumer.SiteId == siteId,
            PolicyObjectOwnerScope.Exception => consumer.OwnerId == siteId,
            PolicyObjectOwnerScope.Company => false,
            _ => false,
        };

    private static bool CanSeeNode(AddressConsumerContext consumer, Guid nodeId)
        => consumer.Scope switch
        {
            PolicyObjectOwnerScope.Node => consumer.OwnerId == nodeId,
            PolicyObjectOwnerScope.Exception => consumer.OwnerId == nodeId,
            _ => false,
        };
}
