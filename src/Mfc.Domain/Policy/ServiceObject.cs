using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Typed service object with canonicalized terms (Policy Model §18).</summary>
public sealed class ServiceObject
{
    public ServiceObjectId Id { get; }

    public PolicyObjectOwnerScope OwnerScope { get; }

    public Guid? OwnerId { get; }

    public PolicyRevisionId? ExceptionRevisionId { get; }

    public NonEmptyName Name { get; }

    public string? Description { get; }

    /// <summary>Canonical ordered unique terms.</summary>
    public IReadOnlyList<ServiceTerm> Terms { get; }

    private ServiceObject(
        ServiceObjectId id,
        PolicyObjectOwnerScope ownerScope,
        Guid? ownerId,
        PolicyRevisionId? exceptionRevisionId,
        NonEmptyName name,
        string? description,
        IReadOnlyList<ServiceTerm> terms)
    {
        Id = id;
        OwnerScope = ownerScope;
        OwnerId = ownerId;
        ExceptionRevisionId = exceptionRevisionId;
        Name = name;
        Description = description;
        Terms = terms;
    }

    public static ServiceObject Create(
        PolicyObjectOwnerScope ownerScope,
        Guid? ownerId,
        PolicyRevisionId? exceptionRevisionId,
        NonEmptyName name,
        IEnumerable<ServiceTerm> terms,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(terms);
        ValidateOwner(ownerScope, ownerId, exceptionRevisionId);

        IReadOnlyList<ServiceTerm> canonical = CanonicalizeTerms(terms);
        if (canonical.Count == 0)
        {
            throw new DomainInvariantException("Empty service object is forbidden.");
        }

        return new ServiceObject(
            ServiceObjectId.New(),
            ownerScope,
            ownerId,
            exceptionRevisionId,
            name,
            description,
            canonical);
    }

    public static ServiceObject Reconstitute(
        ServiceObjectId id,
        PolicyObjectOwnerScope ownerScope,
        Guid? ownerId,
        PolicyRevisionId? exceptionRevisionId,
        NonEmptyName name,
        string? description,
        IEnumerable<ServiceTerm> terms)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(terms);
        ValidateOwner(ownerScope, ownerId, exceptionRevisionId);
        IReadOnlyList<ServiceTerm> canonical = CanonicalizeTerms(terms);
        if (canonical.Count == 0)
        {
            throw new DomainInvariantException("Empty service object is forbidden.");
        }

        return new ServiceObject(id, ownerScope, ownerId, exceptionRevisionId, name, description, canonical);
    }

    /// <summary>
    /// Deduplicates and order-sorts terms; merges overlapping same-protocol port terms when deterministic.
    /// Rejects mixing ICMP and ICMPv6 in one object.
    /// </summary>
    public static IReadOnlyList<ServiceTerm> CanonicalizeTerms(IEnumerable<ServiceTerm> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        List<ServiceTerm> list = terms.ToList();
        if (list.Count == 0)
        {
            return [];
        }

        bool hasIcmp = list.Any(t => t.Protocol.IsIcmpV4);
        bool hasIcmpV6 = list.Any(t => t.Protocol.IsIcmpV6Protocol);
        if (hasIcmp && hasIcmpV6)
        {
            throw new DomainInvariantException("ICMP and ICMPv6 terms must not be mixed in one service object.");
        }

        // Group by protocol identity for deterministic port-term merging.
        List<ServiceTerm> result = [];
        foreach (IGrouping<IpProtocol, ServiceTerm> group in list.GroupBy(t => t.Protocol))
        {
            List<ServiceTerm> groupTerms = group.ToList();
            if (group.Key.HasPortSemantics)
            {
                result.AddRange(MergePortTerms(group.Key, groupTerms));
            }
            else
            {
                result.AddRange(groupTerms.Distinct().OrderBy(static t => t));
            }
        }

        return result.Distinct().OrderBy(static t => t).ToArray();
    }

    private static IEnumerable<ServiceTerm> MergePortTerms(IpProtocol protocol, List<ServiceTerm> terms)
    {
        // Merge only when ICMP is absent and terms differ only by port sets that can union.
        // Keep icmp-less port terms; union source and destination independently across duplicates.
        List<PortInterval> allSrc = [];
        List<PortInterval> allDst = [];
        bool anySrc = false;
        bool anyDst = false;
        bool sawUnconstrained = false;

        foreach (ServiceTerm term in terms)
        {
            if (term.IcmpSelectors is not null)
            {
                yield return term;
                continue;
            }

            if (term.SourcePorts is null && term.DestinationPorts is null)
            {
                sawUnconstrained = true;
                continue;
            }

            if (term.SourcePorts is not null)
            {
                anySrc = true;
                allSrc.AddRange(term.SourcePorts.Intervals);
            }

            if (term.DestinationPorts is not null)
            {
                anyDst = true;
                allDst.AddRange(term.DestinationPorts.Intervals);
            }
        }

        if (sawUnconstrained)
        {
            yield return ServiceTerm.Create(protocol);
            yield break;
        }

        if (!anySrc && !anyDst)
        {
            yield break;
        }

        yield return ServiceTerm.Create(
            protocol,
            anySrc ? PortSet.Create(allSrc) : null,
            anyDst ? PortSet.Create(allDst) : null);
    }

    private static void ValidateOwner(
        PolicyObjectOwnerScope ownerScope,
        Guid? ownerId,
        PolicyRevisionId? exceptionRevisionId)
    {
        switch (ownerScope)
        {
            case PolicyObjectOwnerScope.Company:
                if (ownerId is not null || exceptionRevisionId is not null)
                {
                    throw new DomainInvariantException(
                        "Company service objects must not set owner_id or exception revision.");
                }

                break;

            case PolicyObjectOwnerScope.Site:
            case PolicyObjectOwnerScope.Node:
                if (ownerId is null || exceptionRevisionId is not null)
                {
                    throw new DomainInvariantException(
                        $"{ownerScope} service objects require owner_id and must not set exception revision.");
                }

                break;

            case PolicyObjectOwnerScope.Exception:
                if (ownerId is null || exceptionRevisionId is null)
                {
                    throw new DomainInvariantException(
                        "Exception service objects require owner_id and exception revision id.");
                }

                break;

            default:
                throw new DomainInvariantException($"Unknown object owner scope '{ownerScope}'.");
        }
    }
}
