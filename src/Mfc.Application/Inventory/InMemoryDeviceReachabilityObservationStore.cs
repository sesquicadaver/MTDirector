using System.Collections.Concurrent;
using Mfc.Application.Abstractions.Inventory;
using Mfc.Application.Mapping;

namespace Mfc.Application.Inventory;

/// <summary>Thread-safe in-process Reachability observation map (W6-05 overlay; W6-08 persists on Device).</summary>
public sealed class InMemoryDeviceReachabilityObservationStore : IDeviceReachabilityObservationStore
{
    private readonly ConcurrentDictionary<Guid, string> _byDevice = new();

    public void Record(Guid deviceId, string reachability)
    {
        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("deviceId must be non-empty.", nameof(deviceId));
        }

        if (string.IsNullOrWhiteSpace(reachability))
        {
            throw new ArgumentException("reachability must be non-empty.", nameof(reachability));
        }

        string trimmed = reachability.Trim();
        if (trimmed is not (
            DeviceReachabilityProjector.Reachable
            or DeviceReachabilityProjector.Unreachable
            or DeviceReachabilityProjector.Unknown))
        {
            throw new ArgumentException(
                $"reachability must be {DeviceReachabilityProjector.Reachable}, {DeviceReachabilityProjector.Unreachable}, or {DeviceReachabilityProjector.Unknown}.",
                nameof(reachability));
        }

        _byDevice[deviceId] = trimmed;
    }

    public bool TryGet(Guid deviceId, out string reachability)
        => _byDevice.TryGetValue(deviceId, out reachability!);
}
