namespace Mfc.Application.Abstractions.Inventory;

/// <summary>
/// Process-local Reachability observations from ValidateDeviceConnection / DiscoverDevice (W6-05).
/// Unreachable is not durable across Controller restart; successful probes also persist LastSupportState.
/// </summary>
public interface IDeviceReachabilityObservationStore
{
    void Record(Guid deviceId, string reachability);

    bool TryGet(Guid deviceId, out string reachability);
}
