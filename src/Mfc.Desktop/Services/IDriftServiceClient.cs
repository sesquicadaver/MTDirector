using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Contracts-only drift event client (M6-04).</summary>
public interface IDriftServiceClient
{
    Task<IReadOnlyList<DriftEvent>> ListDeviceDriftEventsAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<DriftEvent> GetDriftEventAsync(
        Guid driftEventId,
        CancellationToken cancellationToken = default);
}
