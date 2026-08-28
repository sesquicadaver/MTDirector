using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>
/// Abstraction over InventoryService RPCs needed for the Site→Node→Device tree
/// and the Desktop Add Router write path.
/// Unit tests substitute a fake without live gRPC.
/// </summary>
public interface IInventoryTreeClient
{
    Task<IReadOnlyList<Site>> ListAllSitesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Node>> ListAllNodesAsync(Guid siteId, CancellationToken cancellationToken = default);

    Task<NodeDetails> GetNodeAsync(Guid nodeId, CancellationToken cancellationToken = default);

    Task<Site> CreateSiteAsync(string code, string name, CancellationToken cancellationToken = default);

    Task<Node> CreateNodeAsync(
        Guid siteId,
        string name,
        NodeKind declaredKind,
        DeclaredUplinkMode declaredUplinkMode,
        CancellationToken cancellationToken = default);

    Task<Device> RegisterDeviceAsync(
        Guid nodeId,
        string displayName,
        string managementHost,
        uint managementPort,
        DeviceRole role,
        CancellationToken cancellationToken = default);

    Task<DeviceConnectionSummary> UpdateDeviceConnectionAsync(
        Guid deviceId,
        string username,
        ReadOnlyMemory<byte> passwordUtf8,
        CertificateTrustMode trustMode,
        string? caProfileRef,
        Sha256? pinnedSpkiSha256,
        uint connectTimeoutMs,
        uint commandTimeoutMs,
        ulong maxResponseBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// On-demand MikroTik neighbor suggestions from a registered seed device (#314).
    /// Never registers devices; caller pre-fills Add Router fields only.
    /// </summary>
    Task<ListNeighborCandidatesResponse> ListNeighborCandidatesAsync(
        Guid seedDeviceId,
        CancellationToken cancellationToken = default);
}
