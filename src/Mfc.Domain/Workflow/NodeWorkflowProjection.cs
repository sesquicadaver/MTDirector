using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Workflow;

/// <summary>Per-device projection retained alongside the aggregated Node status (VRRP-safe).</summary>
public sealed class DeviceWorkflowProjection
{
    public required DeviceId DeviceId { get; init; }

    public required DeviceHashState HashState { get; init; }

    public required DeviceSyncClassification SyncClassification { get; init; }

    /// <summary>
    /// Status this device contributes to Node aggregation, or null when Incomplete (no sync contribution).
    /// </summary>
    public NodeWorkflowStatus? ContributingStatus { get; init; }
}

/// <summary>
/// Derived Node workflow projection. NodeStatus is computed; per-device rows are never dropped.
/// </summary>
public sealed class NodeWorkflowProjection
{
    public required NodeWorkflowStatus NodeStatus { get; init; }

    public required IReadOnlyList<DeviceWorkflowProjection> Devices { get; init; }
}
