using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Inventory;

/// <summary>
/// Site aggregate. <see cref="Code"/> is immutable after the site becomes <see cref="SiteStatus.Active"/>.
/// </summary>
public sealed class Site
{
    public SiteId Id { get; }

    public SiteCode Code { get; private set; }

    public NonEmptyName Name { get; private set; }

    public SiteStatus Status { get; private set; }

    public ulong RowVersion { get; private set; }

    private Site(SiteId id, SiteCode code, NonEmptyName name, SiteStatus status, ulong rowVersion)
    {
        Id = id;
        Code = code;
        Name = name;
        Status = status;
        RowVersion = rowVersion;
    }

    public static Site Create(SiteCode code, NonEmptyName name)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);
        return new Site(SiteId.New(), code, name, SiteStatus.Draft, rowVersion: 1);
    }

    public void Rename(NonEmptyName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Touch();
    }

    /// <summary>Changes site code only while the site is still in Draft.</summary>
    public void ChangeCode(SiteCode code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (Status != SiteStatus.Draft)
        {
            throw new DomainInvariantException("Site.code is immutable after the site leaves Draft.");
        }

        Code = code;
        Touch();
    }

    public void Activate()
    {
        if (Status == SiteStatus.Disabled)
        {
            throw new DomainInvariantException("Disabled site cannot be activated.");
        }

        Status = SiteStatus.Active;
        Touch();
    }

    public void Disable()
    {
        Status = SiteStatus.Disabled;
        Touch();
    }

    private void Touch() => RowVersion++;
}
