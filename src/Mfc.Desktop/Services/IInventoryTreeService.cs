namespace Mfc.Desktop.Services;

/// <summary>Loads Site→Node→Device tree from Controller inventory RPCs off the UI thread.</summary>
public interface IInventoryTreeService
{
    InventoryTreeLoadResult Current { get; }

    Task<InventoryTreeLoadResult> RefreshAsync(CancellationToken cancellationToken = default);
}
