using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>
/// Abstraction over SnapshotService RPCs for the snapshot viewer (list/get/compare)
/// and device capture (StartCapture + WatchCapture). Unit tests substitute a fake without live gRPC.
/// </summary>
public interface ISnapshotViewerClient
{
    Task<StartCaptureResponse> StartCaptureAsync(
        Guid deviceId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<CaptureProgress> WatchCaptureAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SnapshotSummary>> ListCapturesAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<SnapshotSummary> GetSummaryAsync(Guid captureId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SnapshotRecord>> GetAllSectionRecordsAsync(
        Guid captureId,
        string sectionId,
        DiffDomain domain,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches all CompareSnapshots pages (server-side semantic diff; no local recompute).</summary>
    Task<DiffPage> CompareSnapshotsAsync(
        Guid leftCaptureId,
        Guid rightCaptureId,
        CancellationToken cancellationToken = default);
}
