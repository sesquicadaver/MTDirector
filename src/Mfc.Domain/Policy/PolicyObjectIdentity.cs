using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Compose-time identity for an opaque address/service object (Policy Model §11).
/// Avoids constructing a full <see cref="AddressObject"/> / <see cref="ServiceObject"/>.
/// </summary>
public sealed class PolicyObjectIdentity
{
    public Guid Id { get; }

    public PolicyObjectOwnerScope OwnerScope { get; }

    public Guid? OwnerId { get; }

    public PolicyRevisionId? ExceptionRevisionId { get; }

    public PolicyObjectIdentity(
        Guid id,
        PolicyObjectOwnerScope ownerScope,
        Guid? ownerId,
        PolicyRevisionId? exceptionRevisionId = null)
    {
        Id = id;
        OwnerScope = ownerScope;
        OwnerId = ownerId;
        ExceptionRevisionId = exceptionRevisionId;
    }
}
