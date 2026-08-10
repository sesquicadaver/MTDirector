using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Static typed address object (Policy Model §16).</summary>
public sealed class AddressObject
{
    public AddressObjectId Id { get; }

    public PolicyObjectOwnerScope OwnerScope { get; }

    public Guid? OwnerId { get; }

    /// <summary>Required when <see cref="OwnerScope"/> is <see cref="PolicyObjectOwnerScope.Exception"/>.</summary>
    public PolicyRevisionId? ExceptionRevisionId { get; }

    public NonEmptyName Name { get; }

    public IpAddressFamily Family { get; }

    public string? Description { get; }

    /// <summary>Canonical disjoint intervals after normalization.</summary>
    public IReadOnlyList<AddressInterval> Intervals { get; }

    private AddressObject(
        AddressObjectId id,
        PolicyObjectOwnerScope ownerScope,
        Guid? ownerId,
        PolicyRevisionId? exceptionRevisionId,
        NonEmptyName name,
        IpAddressFamily family,
        string? description,
        IReadOnlyList<AddressInterval> intervals)
    {
        Id = id;
        OwnerScope = ownerScope;
        OwnerId = ownerId;
        ExceptionRevisionId = exceptionRevisionId;
        Name = name;
        Family = family;
        Description = description;
        Intervals = intervals;
    }

    public static AddressObject Create(
        PolicyObjectOwnerScope ownerScope,
        Guid? ownerId,
        PolicyRevisionId? exceptionRevisionId,
        NonEmptyName name,
        IpAddressFamily family,
        IEnumerable<AddressEntry> entries,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(entries);
        ValidateOwner(ownerScope, ownerId, exceptionRevisionId);

        List<AddressEntry> entryList = entries.ToList();
        if (entryList.Count == 0)
        {
            throw new DomainInvariantException("Address object entries must be non-empty.");
        }

        if (entryList.Any(e => e.Family != family))
        {
            throw new DomainInvariantException("All address entries must match the object family.");
        }

        IReadOnlyList<AddressInterval> intervals = AddressSetAlgebra.Normalize(entryList.Select(e => e.ToInterval()));
        if (intervals.Count == 0)
        {
            throw new DomainInvariantException("Empty resolved address object is a blocker.");
        }

        return new AddressObject(
            AddressObjectId.New(),
            ownerScope,
            ownerId,
            exceptionRevisionId,
            name,
            family,
            description,
            intervals);
    }

    public static AddressObject Reconstitute(
        AddressObjectId id,
        PolicyObjectOwnerScope ownerScope,
        Guid? ownerId,
        PolicyRevisionId? exceptionRevisionId,
        NonEmptyName name,
        IpAddressFamily family,
        string? description,
        IReadOnlyList<AddressInterval> intervals)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(intervals);
        ValidateOwner(ownerScope, ownerId, exceptionRevisionId);
        IReadOnlyList<AddressInterval> normalized = AddressSetAlgebra.Normalize(intervals);
        if (normalized.Count == 0)
        {
            throw new DomainInvariantException("Empty resolved address object is a blocker.");
        }

        if (normalized.Any(i => i.Family != family))
        {
            throw new DomainInvariantException("Interval family must match address object family.");
        }

        return new AddressObject(
            id,
            ownerScope,
            ownerId,
            exceptionRevisionId,
            name,
            family,
            description,
            normalized);
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
                    throw new DomainInvariantException("Company address objects must not set owner_id or exception revision.");
                }

                break;

            case PolicyObjectOwnerScope.Site:
            case PolicyObjectOwnerScope.Node:
                if (ownerId is null || exceptionRevisionId is not null)
                {
                    throw new DomainInvariantException(
                        $"{ownerScope} address objects require owner_id and must not set exception revision.");
                }

                break;

            case PolicyObjectOwnerScope.Exception:
                if (ownerId is null || exceptionRevisionId is null)
                {
                    throw new DomainInvariantException(
                        "Exception address objects require owner_id (target SITE/NODE) and exception revision id.");
                }

                break;

            default:
                throw new DomainInvariantException($"Unknown object owner scope '{ownerScope}'.");
        }
    }
}
