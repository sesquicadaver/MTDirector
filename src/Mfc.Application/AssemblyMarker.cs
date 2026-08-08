using Mfc.Application;

namespace Mfc.Application;

/// <summary>
/// Assembly marker for architecture tests.
/// M1-05: inventory/snapshot use cases and ports; RouterOS implementations land in M1-06+.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>Preserves the Domain project reference for boundary analysis.</summary>
    public static Type DomainDependencyAnchor { get; } = typeof(Domain.AssemblyMarker);

    /// <summary>Keeps secret port types rooted in Application for architecture scans.</summary>
    public static Type SecretPortAnchor { get; } = typeof(Abstractions.Secrets.ISecretProtector);

    /// <summary>Inventory use-case surface for architecture scans.</summary>
    public static Type InventoryUseCaseAnchor { get; } = typeof(Inventory.CreateSiteUseCase);
}
