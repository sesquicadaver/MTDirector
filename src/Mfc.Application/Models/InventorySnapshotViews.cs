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

    public required ulong RowVersion { get; init; }
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
}

public sealed class SnapshotDiffView
{
    public required Guid LeftSnapshotId { get; init; }

    public required Guid RightSnapshotId { get; init; }

    public required bool Identical { get; init; }

    public required IReadOnlyList<string> ChangedFields { get; init; }
}

public sealed class DeviceDiscoveryView
{
    public required Guid DeviceId { get; init; }

    public required string ObservedIdentity { get; init; }

    public required SupportState SupportState { get; init; }

    public required bool RouterOsMutated { get; init; }
}
