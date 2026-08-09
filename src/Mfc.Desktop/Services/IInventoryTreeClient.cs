using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>
/// Abstraction over InventoryService RPCs needed for the Site→Node→Device tree.
/// Unit tests substitute a fake without live gRPC.
/// </summary>
public interface IInventoryTreeClient
{
    Task<IReadOnlyList<Site>> ListAllSitesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Node>> ListAllNodesAsync(Guid siteId, CancellationToken cancellationToken = default);

    Task<NodeDetails> GetNodeAsync(Guid nodeId, CancellationToken cancellationToken = default);
}
