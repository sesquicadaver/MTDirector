using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Policy container aggregate (Policy Model §7). Revisions are separate aggregates linked by <see cref="Id"/>.
/// </summary>
public sealed class Policy
{
    public PolicyId Id { get; }

    public NonEmptyName Name { get; private set; }

    public PolicyKind Kind { get; }

    public PolicyOwnerScope OwnerScope { get; }

    public Guid? OwnerId { get; }

    public PolicyStatus Status { get; private set; }

    public ulong RowVersion { get; private set; }

    private Policy(
        PolicyId id,
        NonEmptyName name,
        PolicyKind kind,
        PolicyOwnerScope ownerScope,
        Guid? ownerId,
        PolicyStatus status,
        ulong rowVersion)
    {
        Id = id;
        Name = name;
        Kind = kind;
        OwnerScope = ownerScope;
        OwnerId = ownerId;
        Status = status;
        RowVersion = rowVersion;
    }

    public static Policy Create(
        NonEmptyName name,
        PolicyKind kind,
        PolicyOwnerScope ownerScope,
        Guid? ownerId)
    {
        ArgumentNullException.ThrowIfNull(name);
        ValidateOwner(kind, ownerScope, ownerId);
        return new Policy(
            PolicyId.New(),
            name,
            kind,
            ownerScope,
            ownerId,
            PolicyStatus.Active,
            rowVersion: 1);
    }

    /// <summary>Rebuilds a policy from persistence.</summary>
    public static Policy Reconstitute(
        PolicyId id,
        NonEmptyName name,
        PolicyKind kind,
        PolicyOwnerScope ownerScope,
        Guid? ownerId,
        PolicyStatus status,
        ulong rowVersion)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (rowVersion == 0)
        {
            throw new DomainInvariantException("row_version must be greater than zero.");
        }

        ValidateOwner(kind, ownerScope, ownerId);
        return new Policy(id, name, kind, ownerScope, ownerId, status, rowVersion);
    }

    public void Rename(NonEmptyName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        EnsureActive();
        Name = name;
        Touch();
    }

    public void Archive()
    {
        Status = PolicyStatus.Archived;
        Touch();
    }

    private void EnsureActive()
    {
        if (Status == PolicyStatus.Archived)
        {
            throw new DomainInvariantException("Archived policy cannot be renamed.");
        }
    }

    private void Touch() => RowVersion++;

    internal static void ValidateOwner(PolicyKind kind, PolicyOwnerScope ownerScope, Guid? ownerId)
    {
        switch (kind)
        {
            case PolicyKind.CompanyBaseline:
                if (ownerScope != PolicyOwnerScope.Company)
                {
                    throw new DomainInvariantException("COMPANY_BASELINE must have owner_scope COMPANY.");
                }

                if (ownerId is not null)
                {
                    throw new DomainInvariantException("COMPANY_BASELINE must not set owner_id.");
                }

                break;

            case PolicyKind.SiteOverlay:
                if (ownerScope != PolicyOwnerScope.Site || ownerId is null)
                {
                    throw new DomainInvariantException("SITE_OVERLAY must own a concrete SITE (owner_id required).");
                }

                break;

            case PolicyKind.NodeOverlay:
                if (ownerScope != PolicyOwnerScope.Node || ownerId is null)
                {
                    throw new DomainInvariantException("NODE_OVERLAY must own a concrete NODE (owner_id required).");
                }

                break;

            case PolicyKind.Exception:
                if (ownerScope is not (PolicyOwnerScope.Site or PolicyOwnerScope.Node) || ownerId is null)
                {
                    throw new DomainInvariantException(
                        "EXCEPTION must target a SITE or NODE; company-wide temporary exception is forbidden.");
                }

                break;

            case PolicyKind.IncidentDenyOverlay:
                if (ownerScope != PolicyOwnerScope.Node || ownerId is null)
                {
                    throw new DomainInvariantException(
                        "INCIDENT_DENY_OVERLAY must own a concrete NODE (owner_id required).");
                }

                break;

            default:
                throw new DomainInvariantException($"Unknown policy kind '{kind}'.");
        }
    }
}
