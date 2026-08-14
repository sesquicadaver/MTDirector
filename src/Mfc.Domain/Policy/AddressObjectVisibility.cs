using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Consumer context for address-object visibility checks (Policy Model §11).</summary>
public sealed class AddressConsumerContext
{
    public required PolicyObjectOwnerScope Scope { get; init; }

    public Guid? OwnerId { get; init; }

    /// <summary>For NODE consumers: parent site id used when referencing SITE objects.</summary>
    public Guid? SiteId { get; init; }

    public PolicyRevisionId? ExceptionRevisionId { get; init; }
}

/// <summary>UUID-based scope visibility for address objects (Policy Model §11.1).</summary>
public static class AddressObjectVisibility
{
    public static bool CanReference(AddressConsumerContext consumer, AddressObject referenced)
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
            PolicyObjectOwnerScope.Site => CanSeeSiteObject(consumer, referenced.OwnerId!.Value),
            PolicyObjectOwnerScope.Node => CanSeeNodeObject(consumer, referenced.OwnerId!.Value),
            PolicyObjectOwnerScope.Exception =>
                consumer.Scope == PolicyObjectOwnerScope.Exception
                && consumer.ExceptionRevisionId == referenced.ExceptionRevisionId,
            _ => false,
        };
    }

    public static void EnsureCanReference(AddressConsumerContext consumer, AddressObject referenced)
    {
        if (!CanReference(consumer, referenced))
        {
            throw new DomainInvariantException(
                $"Address object '{referenced.Id}' is not visible to consumer scope {consumer.Scope} " +
                $"(visibility is UUID/scope based; upward hierarchy references are forbidden).");
        }
    }

    private static bool CanSeeSiteObject(AddressConsumerContext consumer, Guid siteId)
        => consumer.Scope switch
        {
            PolicyObjectOwnerScope.Site => consumer.OwnerId == siteId,
            PolicyObjectOwnerScope.Node => consumer.SiteId == siteId,
            PolicyObjectOwnerScope.Exception => consumer.OwnerId == siteId,
            // Company cannot reference Site objects (upward forbidden from company's view of child-owned).
            PolicyObjectOwnerScope.Company => false,
            _ => false,
        };

    private static bool CanSeeNodeObject(AddressConsumerContext consumer, Guid nodeId)
        => consumer.Scope switch
        {
            PolicyObjectOwnerScope.Node => consumer.OwnerId == nodeId,
            PolicyObjectOwnerScope.Exception => consumer.OwnerId == nodeId,
            PolicyObjectOwnerScope.Company => false,
            PolicyObjectOwnerScope.Site => false,
            _ => false,
        };
}

/// <summary>
/// Managed rules must reference address objects by UUID; inline IP literals are forbidden (Policy Model §17).
/// </summary>
public static class ManagedRuleAddressConstraint
{
    public static void EnsureNoInlineAddress(bool hasInlineIpLiteral)
    {
        if (hasInlineIpLiteral)
        {
            throw new DomainInvariantException(
                "Direct inline IP in a managed rule is forbidden; use AddressSelector object UUIDs.");
        }
    }
}
