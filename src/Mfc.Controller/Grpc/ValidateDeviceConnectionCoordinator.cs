using System.Collections.Concurrent;
using Mfc.Application.Common;
using Mfc.Application.Models;

namespace Mfc.Controller.Grpc;

/// <summary>
/// Process-wide in-flight ValidateDeviceConnection probes keyed by device id (M1-25 AC#5).
/// Second concurrent call awaits the same task instead of starting a duplicate probe.
/// </summary>
public sealed class ValidateDeviceConnectionCoordinator
{
    private readonly ConcurrentDictionary<Guid, Lazy<Task<ApplicationResult<DeviceDiscoveryView>>>> _inflight = new();

    /// <summary>
    /// Runs <paramref name="probeFactory"/> once per device while a probe is already in flight.
    /// Uses <see cref="Lazy{T}"/> so ConcurrentDictionary value-factory races cannot start two probes.
    /// </summary>
    public async Task<ApplicationResult<DeviceDiscoveryView>> RunAsync(
        Guid deviceId,
        Func<CancellationToken, Task<ApplicationResult<DeviceDiscoveryView>>> probeFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(probeFactory);

        Lazy<Task<ApplicationResult<DeviceDiscoveryView>>> lazy = _inflight.GetOrAdd(
            deviceId,
            _ => new Lazy<Task<ApplicationResult<DeviceDiscoveryView>>>(
                () => probeFactory(cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        finally
        {
            _inflight.TryRemove(
                new KeyValuePair<Guid, Lazy<Task<ApplicationResult<DeviceDiscoveryView>>>>(deviceId, lazy));
        }
    }
}
