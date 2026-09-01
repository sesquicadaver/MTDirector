namespace Mfc.Application.Abstractions.Inventory;

/// <summary>
/// Optional process-local Reachability cache from ValidateDeviceConnection / DiscoverDevice (W6-05).
/// W6-08 persists Unreachable/Reachable on Device; this store remains a same-process overlay.
/// </summary>
public interface IDeviceReachabilityObservationStore
{
    void Record(Guid deviceId, string reachability);

    bool TryGet(Guid deviceId, out string reachability);
}
