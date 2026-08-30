using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Inventory selection for Operations: Node-centric pair ops, never a silent first Device child.
/// </summary>
public static class InventoryOpsSelection
{
    public const string VrrpPairHint =
        "VRRP ops target this Node (pair). Create plan includes all members; the first Device is not used silently.";

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
}
