using Mfc.Domain.Workflow;

namespace Mfc.Application.Models;

/// <summary>Application view of persisted device hash projection (hashes as lowercase hex).</summary>
public sealed class DeviceHashStateView
{
    public required Guid DeviceId { get; init; }

    public string? DesiredPolicyHashHex { get; init; }

    public string? DesiredArtifactHashHex { get; init; }

    public string? LastCommittedPolicyHashHex { get; init; }

    public string? LastCommittedArtifactHashHex { get; init; }

    public string? ActualManagedResourceHashHex { get; init; }

    public required bool ActualKnown { get; init; }

    public required bool AnchorKnown { get; init; }

    public required DeviceSyncClassification SyncClassification { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public required ulong RowVersion { get; init; }
}

/// <summary>Per-device row inside a projected Node workflow.</summary>
public sealed class DeviceWorkflowProjectionView
{
    public required Guid DeviceId { get; init; }

    public required DeviceHashStateView HashState { get; init; }

    public required DeviceSyncClassification SyncClassification { get; init; }

    public NodeWorkflowStatus? ContributingStatus { get; init; }
}

/// <summary>Derived Node workflow projection for gRPC / Desktop (never stored on Node).</summary>
public sealed class NodeWorkflowProjectionView
{
    public required NodeWorkflowStatus NodeStatus { get; init; }

    public required IReadOnlyList<DeviceWorkflowProjectionView> Devices { get; init; }
}
