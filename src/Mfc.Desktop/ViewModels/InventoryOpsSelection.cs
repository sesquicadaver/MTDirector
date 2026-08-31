using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Inventory selection for Operations and Snapshots: Node-centric pair ops;
/// capture/compare stay per-device (never a silent first Device child).
/// </summary>
public static class InventoryOpsSelection
{
    public const string VrrpPairHint =
        "VRRP ops target this Node (pair). Create plan includes all members; the first Device is not used silently.";

    public const string VrrpPairCaptureNodeHint =
        "VRRP capture is per member. Select Device a or b in the tree; Capture does not run against the Node (no silent first child).";

    public const string VrrpPairCaptureMemberHint =
        "Capturing this member of the VRRP pair. Compare only later captures of this same device — not the peer.";

    public const string CrossDeviceCompareForbiddenReason =
        "Compare is same-device only (SNAPSHOTS_FROM_DIFFERENT_DEVICES). VRRP members a and b are different devices; capture each separately and do not compare a against b.";

    public static bool IsVrrpNode(InventoryNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return string.Equals(node.NodeKindText, "Vrrp", StringComparison.Ordinal);
    }

    public static InventoryNodeViewModel? TryResolveNode(
        InventoryNodeViewModel? selected,
        IEnumerable<InventoryNodeViewModel> siteRoots)
    {
        ArgumentNullException.ThrowIfNull(siteRoots);
        if (selected is null)
        {
            return null;
        }

        if (selected.Kind == InventoryTreeKind.Node)
        {
            return selected;
        }

        if (selected.Kind == InventoryTreeKind.Device && selected.ParentId is Guid parentId)
        {
            foreach (InventoryNodeViewModel site in siteRoots)
            {
                foreach (InventoryNodeViewModel candidate in site.Children)
                {
                    if (candidate.Kind == InventoryTreeKind.Node && candidate.Id == parentId)
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    public static InventoryNodeViewModel RequireNode(
        InventoryNodeViewModel? selected,
        IEnumerable<InventoryNodeViewModel> siteRoots)
    {
        return TryResolveNode(selected, siteRoots)
            ?? throw new InvalidOperationException("Select a Node in the inventory tree.");
    }

    /// <summary>All Device children of the resolved Node (VRRP pair or standalone).</summary>
    public static IReadOnlyList<Guid> RequireDeviceIds(InventoryNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        List<Guid> ids = node.Children
            .Where(static c => c.Kind == InventoryTreeKind.Device)
            .Select(static c => c.Id)
            .ToList();
        if (ids.Count == 0)
        {
            throw new InvalidOperationException("Selected Node has no Device child.");
        }

        return ids;
    }

    public static bool IsVrrpPair(InventoryNodeViewModel? selected, IEnumerable<InventoryNodeViewModel> siteRoots)
    {
        InventoryNodeViewModel? node = TryResolveNode(selected, siteRoots);
        return node is not null && IsVrrpNode(node);
    }

    public static string FormatTargetHint(InventoryNodeViewModel? selected, IEnumerable<InventoryNodeViewModel> siteRoots)
    {
        InventoryNodeViewModel? node = TryResolveNode(selected, siteRoots);
        if (node is null)
        {
            return "Select a Node, then create a plan.";
        }

        if (IsVrrpNode(node))
        {
            return VrrpPairHint;
        }

        return $"Node {node.DisplayName}: plan includes every Device member.";
    }

    /// <summary>Per-member capture guidance when the selection is a VRRP pair or a pair member.</summary>
    public static string FormatCaptureGuidance(
        InventoryNodeViewModel? selected,
        IEnumerable<InventoryNodeViewModel> siteRoots)
    {
        if (!IsVrrpPair(selected, siteRoots) || selected is null)
        {
            return string.Empty;
        }

        return selected.Kind == InventoryTreeKind.Node
            ? VrrpPairCaptureNodeHint
            : VrrpPairCaptureMemberHint;
    }

    /// <summary>Why CompareSnapshots forbids a-against-b (M1-24 same-device only).</summary>
    public static string FormatCompareGuidance(
        InventoryNodeViewModel? selected,
        IEnumerable<InventoryNodeViewModel> siteRoots)
    {
        if (!IsVrrpPair(selected, siteRoots))
        {
            return string.Empty;
        }

        if (selected?.Kind == InventoryTreeKind.Node)
        {
            return "Select a VRRP member Device, then compare two captures of that same member. "
                   + CrossDeviceCompareForbiddenReason;
        }

        return CrossDeviceCompareForbiddenReason;
    }

    /// <summary>Maps Controller SNAPSHOTS_FROM_DIFFERENT_DEVICES to the operator why-text.</summary>
    public static string? ExplainCompareError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return error;
        }

        if (error.Contains("SNAPSHOTS_FROM_DIFFERENT_DEVICES", StringComparison.Ordinal)
            || error.Contains("snapshots_from_different_devices", StringComparison.Ordinal))
        {
            return CrossDeviceCompareForbiddenReason;
        }

        return error;
    }
}
