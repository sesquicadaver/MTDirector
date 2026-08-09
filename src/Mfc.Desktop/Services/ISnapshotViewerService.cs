namespace Mfc.Desktop.Services;

/// <summary>Loads read-only snapshot presentation models from SnapshotService (Contracts only).</summary>
public interface ISnapshotViewerService
{
    SnapshotViewerLoadResult Current { get; }

    Task<SnapshotViewerLoadResult> LoadDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<SnapshotViewerLoadResult> LoadCaptureAsync(
        Guid captureId,
        CancellationToken cancellationToken = default);

    Task<SnapshotViewerLoadResult> LoadSectionAsync(
        Guid captureId,
        string sectionId,
        CancellationToken cancellationToken = default);

    void Clear();
}
