using Mfc.Domain.Policy;

namespace Mfc.Application.Models;

/// <summary>Application view of a desired zone definition.</summary>
public sealed class ZoneDefinitionView
{
    public required Guid Id { get; init; }

    public required PolicyOwnerScope OwnerScope { get; init; }

    public Guid? OwnerId { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required ulong RowVersion { get; init; }
}

/// <summary>Application view of a desired Node→zone binding.</summary>
public sealed class NodeZoneBindingView
{
    public required Guid Id { get; init; }

    public required Guid NodeId { get; init; }

    public required Guid ZoneId { get; init; }

    public required NodeZoneBindingKind Kind { get; init; }

    public required IReadOnlyList<string> Values { get; init; }

    public required string ExpectedDependencyHashHex { get; init; }

    public string? LastResolvedDependencyHashHex { get; init; }

    public required bool AnalysisStale { get; init; }

    public required ulong RowVersion { get; init; }
}

/// <summary>Typed blocker surfaced from zone resolve.</summary>
public sealed class ZoneResolveBlockerView
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Resolve outcome for one binding on one device.</summary>
public sealed class ZoneBindingResolveView
{
    public required Guid BindingId { get; init; }

    public required Guid ZoneId { get; init; }

    public required Guid DeviceId { get; init; }

    public required IReadOnlyList<string> ResolvedMembers { get; init; }

    public required string FreshDependencyHashHex { get; init; }

    public required bool AnalysisStale { get; init; }

    public required IReadOnlyList<ZoneResolveBlockerView> Blockers { get; init; }

    public required NodeZoneBindingView Binding { get; init; }
}

/// <summary>Batch resolve response for a device or node.</summary>
public sealed class ZoneResolveBatchView
{
    public required IReadOnlyList<ZoneBindingResolveView> Results { get; init; }
}
