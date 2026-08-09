using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>
/// Abstraction over SnapshotService RPCs needed for the read-only snapshot viewer.
/// Unit tests substitute a fake without live gRPC.
/// </summary>
public interface ISnapshotViewerClient
{
    Task<IReadOnlyList<SnapshotSummary>> ListCapturesAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<SnapshotSummary> GetSummaryAsync(Guid captureId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SnapshotRecord>> GetAllSectionRecordsAsync(
        Guid captureId,
        string sectionId,
        DiffDomain domain,
        CancellationToken cancellationToken = default);
}
