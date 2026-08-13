using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Logical zone definition catalog entry (Policy Model §20).
/// Desired SoT is the zone catalog table — not <see cref="PolicyDocument.ZoneDefinitions"/>.
/// </summary>
public sealed class ZoneDefinition
{
    public ZoneId Id { get; }

    public PolicyOwnerScope OwnerScope { get; }

    public Guid? OwnerId { get; }

    public NonEmptyName Key { get; }

    public NonEmptyName Name { get; private set; }

    public string? Description { get; private set; }

    public ulong RowVersion { get; private set; }

    private ZoneDefinition(
        ZoneId id,
        PolicyOwnerScope ownerScope,
        Guid? ownerId,
        NonEmptyName key,
        NonEmptyName name,
        string? description,
        ulong rowVersion)
    {
        Id = id;
        OwnerScope = ownerScope;
        OwnerId = ownerId;
        Key = key;
        Name = name;
        Description = description;
        RowVersion = rowVersion;
    }

    public static ZoneDefinition Create(
        PolicyOwnerScope ownerScope,
        Guid? ownerId,
        NonEmptyName key,
        NonEmptyName name,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(name);
        ValidateOwner(ownerScope, ownerId);
        return new ZoneDefinition(
            ZoneId.New(),
            ownerScope,
            ownerId,
            key,
            name,
            NormalizeDescription(description),
            rowVersion: 1);
    }

    public static ZoneDefinition Reconstitute(
        ZoneId id,
        PolicyOwnerScope ownerScope,
        Guid? ownerId,
        NonEmptyName key,
        NonEmptyName name,
        string? description,
        ulong rowVersion)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(name);
        if (rowVersion == 0)
        {
            throw new DomainInvariantException("row_version must be greater than zero.");
        }

        ValidateOwner(ownerScope, ownerId);
        return new ZoneDefinition(
            id,
            ownerScope,
            ownerId,
            key,
            name,
            NormalizeDescription(description),
            rowVersion);
    }

    public void Rename(NonEmptyName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Touch();
    }

    public void SetDescription(string? description)
    {
        Description = NormalizeDescription(description);
        Touch();
    }

    private void Touch() => RowVersion++;

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static void ValidateOwner(PolicyOwnerScope ownerScope, Guid? ownerId)
    {
        switch (ownerScope)
        {
            case PolicyOwnerScope.Company:
                if (ownerId is not null)
                {
                    throw new DomainInvariantException("Company zones must not set owner_id.");
                }

                break;

            case PolicyOwnerScope.Site:
            case PolicyOwnerScope.Node:
                if (ownerId is null)
                {
                    throw new DomainInvariantException($"{ownerScope} zones require owner_id.");
                }

                break;

            default:
                throw new DomainInvariantException($"Unsupported zone owner scope '{ownerScope}'.");
        }
    }
}
