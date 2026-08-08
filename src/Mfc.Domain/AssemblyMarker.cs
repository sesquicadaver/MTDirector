namespace Mfc.Domain;

/// <summary>
/// Assembly marker for architecture tests.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>Keeps inventory types rooted for analyzers and docs.</summary>
    public static Type InventoryAnchor { get; } = typeof(Inventory.Site);
}
