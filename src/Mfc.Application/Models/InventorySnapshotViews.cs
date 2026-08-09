using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;

namespace Mfc.Application.Models;

public sealed class SiteView
{
    public required Guid Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required SiteStatus Status { get; init; }

    public required ulong RowVersion { get; init; }
}

public sealed class SiteListPageView
{
    public required IReadOnlyList<SiteView> Items { get; init; }

    public string? NextCursor { get; init; }
}

public sealed class NodeView
{
    public required Guid Id { get; init; }

    public required Guid SiteId { get; init; }

    public required string Name { get; init; }

    public required NodeKind DeclaredKind { get; init; }

    public required DeclaredUplinkMode DeclaredUplinkMode { get; init; }

    public required NodeStatus Status { get; init; }

    public required ulong RowVersion { get; init; }
}

public sealed class NodeListPageView
{
    public required IReadOnlyList<NodeView> Items { get; init; }

    public string? NextCursor { get; init; }
}

public sealed class DeviceView
{
    public required Guid Id { get; init; }

    public required Guid NodeId { get; init; }

    public required string DisplayName { get; init; }

    public required string ManagementHost { get; init; }

    public required ushort ManagementPort { get; init; }

    public required DeviceRole Role { get; init; }

    public required bool Enabled { get; init; }

    public SupportState? LastSupportState { get; init; }

    public Guid? LastCompletedCaptureId { get; init; }

    public required ulong RowVersion { get; init; }

    /// <summary>Observed RouterOS version when known; null means unset (UI shows —).</summary>
    public string? RouterOsVersion { get; init; }

    /// <summary>Observed hardware model when known; null means unset (UI shows —).</summary>
    public string? Model { get; init; }

    /// <summary>Unknown / Reachable / Unreachable. Defaults to Unknown until probe wiring.</summary>
    public string Reachability { get; init; } = "Unknown";

    /// <summary>VRRP role labels when topology observation is wired; empty until then.</summary>
    public IReadOnlyList<string> VrrpRoleLabels { get; init; } = [];

    /// <summary>CompletedAtUtc of last completed capture when resolvable.</summary>
    public DateTimeOffset? LastSnapshotAtUtc { get; init; }
}

public sealed class NodeDetailsView
{
    public required NodeView Node { get; init; }

    public required IReadOnlyList<DeviceView> Devices { get; init; }
}

public sealed class SnapshotView
{
    public required Guid Id { get; init; }

    public required Guid DeviceId { get; init; }

    public required SnapshotStatus Status { get; init; }

    public string? ConfigurationHashHex { get; init; }

    public string? ObservationHashHex { get; init; }

    public string? CapabilityHashHex { get; init; }

    public string? SnapshotHashHex { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public int SchemaVersion { get; init; }

    /// <summary>Capture operation id when known (persisted or in-flight StartCapture).</summary>
    public Guid? OperationId { get; init; }

    /// <summary>True when StartCapture reused an idempotent or identical completed capture.</summary>
    public bool Deduplicated { get; init; }

    /// <summary>
    /// Per-section capture status for GetSnapshotSummary (empty for ListCaptures items).
    /// </summary>
    public IReadOnlyList<SnapshotSectionSummaryView> Sections { get; init; } = [];
}

/// <summary>Section status row exposed on SnapshotSummary (M1-28).</summary>
public sealed class SnapshotSectionSummaryView
{
    public required string SectionId { get; init; }

    public required short Status { get; init; }

    public required bool Ordered { get; init; }

    public int ConfigurationRecordCount { get; init; }

    public int ObservationRecordCount { get; init; }

    public int CapabilityRecordCount { get; init; }

    public int CompatibilityRecordCount { get; init; }
}

/// <summary>Paged canonical section records for Viewer (never raw unredacted payload).</summary>
public sealed class SnapshotSectionPageView
{
    public required Guid CaptureId { get; init; }

    public required string SectionId { get; init; }

    public required bool Ordered { get; init; }

    public required IReadOnlyList<SnapshotRecordView> Records { get; init; }

    public string? NextCursor { get; init; }
}

/// <summary>One canonical record projected for gRPC SnapshotRecord.</summary>
public sealed class SnapshotRecordView
{
    public required string StableKey { get; init; }

    public int? Ordinal { get; init; }

    /// <summary>Configuration-domain property bag (string values).</summary>
    public IReadOnlyDictionary<string, string> Configuration { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Observation-domain property bag (string values).</summary>
    public IReadOnlyDictionary<string, string> Observations { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed class SnapshotDiffView
{
    public required Guid LeftSnapshotId { get; init; }

    public required Guid RightSnapshotId { get; init; }

    public required bool Identical { get; init; }

    /// <summary>Hash-level summary kept for M1-05 compatibility when sections are absent.</summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }

    /// <summary>Semantic record-level entries (M1-24). Empty when falling back to hash-level compare.</summary>
    public IReadOnlyList<SnapshotDiffEntryView> Entries { get; init; } = [];

    /// <summary>Non-fatal semantic diff warnings (complexity / degraded matching).</summary>
    public IReadOnlyList<SnapshotDiffWarningView> Warnings { get; init; } = [];
}

/// <summary>Application projection of a domain <c>DiffEntry</c>.</summary>
public sealed class SnapshotDiffEntryView
{
    public required string SectionId { get; init; }

    public required string Domain { get; init; }

    public required IReadOnlyList<string> Changes { get; init; }

    public required string Confidence { get; init; }

    public required string RecordKey { get; init; }

    public int? BeforeOrdinal { get; init; }

    public int? AfterOrdinal { get; init; }

    public IReadOnlyDictionary<string, string>? BeforeProps { get; init; }

    public IReadOnlyDictionary<string, string>? AfterProps { get; init; }

    public IReadOnlyList<SnapshotDiffFieldChangeView> FieldChanges { get; init; } = [];
}

/// <summary>Application projection of a domain field change.</summary>
public sealed class SnapshotDiffFieldChangeView
{
    public required string FieldName { get; init; }

    public string? Before { get; init; }

    public string? After { get; init; }

    public IReadOnlyList<string> AddedValues { get; init; } = [];

    public IReadOnlyList<string> RemovedValues { get; init; } = [];
}

/// <summary>Application projection of a domain diff warning.</summary>
public sealed class SnapshotDiffWarningView
{
    public required string Code { get; init; }

    public required string Message { get; init; }
}

public sealed class DeviceDiscoveryView
{
    public required Guid DeviceId { get; init; }

    public required string ObservedIdentity { get; init; }

    public required SupportState SupportState { get; init; }

    public required bool RouterOsMutated { get; init; }
}
