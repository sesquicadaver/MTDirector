namespace Mfc.Desktop.Services;

/// <summary>Loads server-side semantic diffs for Desktop (no local SemanticDiffEngine).</summary>
public interface ISnapshotDiffService
{
    SnapshotDiffLoadResult Current { get; }

    Task<SnapshotDiffLoadResult> LoadCapturesAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<SnapshotDiffLoadResult> CompareAsync(
        Guid leftCaptureId,
        Guid rightCaptureId,
        CancellationToken cancellationToken = default);

    void Clear();
}
