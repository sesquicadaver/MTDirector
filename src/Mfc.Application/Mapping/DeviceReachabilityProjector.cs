using Mfc.Domain.Inventory;

namespace Mfc.Application.Mapping;

/// <summary>
/// Projects operator-facing Reachability for GetNode (W6-05).
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

    /// <summary>
    /// Observation (process-local failed/success probe) overrides durable support-state hint when present.
    /// </summary>
    public static string Project(SupportState? lastSupportState, string? observedReachability)
    {
        if (!string.IsNullOrWhiteSpace(observedReachability))
        {
            string trimmed = observedReachability.Trim();
            if (trimmed is Reachable or Unreachable or Unknown)
            {
                return trimmed;
            }
        }

        return FromSupportState(lastSupportState);
    }
}
