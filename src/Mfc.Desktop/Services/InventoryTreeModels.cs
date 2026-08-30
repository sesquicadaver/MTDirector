namespace Mfc.Desktop.Services;

/// <summary>Presentation kind for inventory tree rows (no Domain types).</summary>
public enum InventoryTreeKind
{
    Site = 0,
    Node = 1,
    Device = 2,
}

/// <summary>Immutable presentation DTO for one tree row built from server inventory data.</summary>
public sealed class InventoryTreeItem
{
    public required InventoryTreeKind Kind { get; init; }

    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    public string StatusText { get; init; } = string.Empty;

    public string NodeKindText { get; init; } = string.Empty;

    public string UplinkModeText { get; init; } = string.Empty;

    public string SupportStateText { get; init; } = string.Empty;

    public string ReachabilityText { get; init; } = string.Empty;

    public string RouterOsVersionText { get; init; } = "—";

    public string ModelText { get; init; } = "—";

    public string VrrpRolesText { get; init; } = "—";

    public string LastSnapshotText { get; init; } = "—";

    /// <summary>Device management host from proto (host or host:port); — when empty.</summary>
    public string ManagementHostText { get; init; } = "—";

    /// <summary>Derived Node workflow status text (M6-01); empty for Site/Device.</summary>
    public string WorkflowStatusText { get; init; } = "—";

    /// <summary>Desired artifact hash short hex (M6-01); — when missing.</summary>
    public string DesiredHashText { get; init; } = "—";

    /// <summary>Last committed artifact hash short hex (M6-01); — when missing.</summary>
    public string CommittedHashText { get; init; } = "—";

    /// <summary>Actual managed resource hash short hex (M6-01); — when missing.</summary>
    public string ActualHashText { get; init; } = "—";

    public IReadOnlyList<InventoryTreeItem> Children { get; init; } = [];
}

/// <summary>Result of an inventory tree refresh attempt.</summary>
public sealed class InventoryTreeLoadResult
{
    public required IReadOnlyList<InventoryTreeItem> Roots { get; init; }

    public required bool Succeeded { get; init; }

    public string? Error { get; init; }

    /// <summary>True when Roots are from a previous successful load after a failed/cancelled refresh.</summary>
    public required bool IsCached { get; init; }

    public required bool IsRefreshing { get; init; }
}
