using Mfc.Domain.Inventory;

namespace Mfc.Application.Mapping;

/// <summary>
/// Projects operator-facing Reachability for GetNode (W6-05 / W6-08).
/// Does not invent live topology: successful probe → Reachable; otherwise Unknown unless an observation overrides.
/// </summary>
public static class DeviceReachabilityProjector
{
    public const string Unknown = "Unknown";

    public const string Reachable = "Reachable";

    public const string Unreachable = "Unreachable";

    /// <summary>
    /// Durable hint from a successful DiscoverDevice probe that persisted <see cref="Device.LastSupportState"/>.
    /// </summary>
    public static string FromSupportState(SupportState? lastSupportState)
        => lastSupportState is null ? Unknown : Reachable;

    /// <summary>Maps durable <see cref="ObservedReachability"/> to the operator-facing wire string.</summary>
    public static string FromObserved(ObservedReachability? observed)
        => observed switch
        {
            ObservedReachability.Reachable => Reachable,
            ObservedReachability.Unreachable => Unreachable,
            ObservedReachability.Unknown => Unknown,
            null => Unknown,
            _ => Unknown,
        };

    /// <summary>
    /// Process-local or durable observation overrides support-state hint when present.
    /// Prefer explicit observed string; else durable <see cref="Device.LastObservedReachability"/>; else LastSupportState.
    /// </summary>
    public static string Project(
        SupportState? lastSupportState,
        string? observedReachability,
        ObservedReachability? durableObserved = null)
    {
        if (!string.IsNullOrWhiteSpace(observedReachability))
        {
            string trimmed = observedReachability.Trim();
            if (trimmed is Reachable or Unreachable or Unknown)
            {
                return trimmed;
            }
        }

        if (durableObserved is not null)
        {
            return FromObserved(durableObserved);
        }

        return FromSupportState(lastSupportState);
    }
}
