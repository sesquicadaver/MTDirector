using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>Pure presentation node for the inventory TreeView (no Domain/RouterOS/SQL types).</summary>
public sealed partial class InventoryNodeViewModel : ObservableObject
{
    public InventoryNodeViewModel(InventoryTreeItem item, Guid? parentId = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        Kind = item.Kind;
        Id = item.Id;
        ParentId = parentId;
        DisplayName = item.DisplayName;
        StatusText = item.StatusText;
        NodeKindText = item.NodeKindText;
        UplinkModeText = item.UplinkModeText;
        SupportStateText = item.SupportStateText;
        ReachabilityText = item.ReachabilityText;
        RouterOsVersionText = item.RouterOsVersionText;
        ModelText = item.ModelText;
        VrrpRolesText = item.VrrpRolesText;
        LastSnapshotText = item.LastSnapshotText;
        foreach (InventoryTreeItem child in item.Children)
        {
            Children.Add(new InventoryNodeViewModel(child, parentId: item.Id));
        }
    }

    public InventoryTreeKind Kind { get; }

    public Guid Id { get; }

    public Guid? ParentId { get; }

    public string DisplayName { get; }

    public string StatusText { get; }

    public string NodeKindText { get; }

    public string UplinkModeText { get; }

    public string SupportStateText { get; }

    public string ReachabilityText { get; }

    public string RouterOsVersionText { get; }

    public string ModelText { get; }

    public string VrrpRolesText { get; }

    public string LastSnapshotText { get; }

    public ObservableCollection<InventoryNodeViewModel> Children { get; } = [];

    public string KindLabel => Kind switch
    {
        InventoryTreeKind.Site => "Site",
        InventoryTreeKind.Node => "Node",
        InventoryTreeKind.Device => "Device",
        _ => Kind.ToString(),
    };

    public string Subtitle => Kind switch
    {
        InventoryTreeKind.Site => StatusText,
        InventoryTreeKind.Node => $"{NodeKindText} · {UplinkModeText}",
        InventoryTreeKind.Device =>
            $"{SupportStateText} · {ReachabilityText} · {RouterOsVersionText} · {ModelText}",
        _ => string.Empty,
    };

    public string DetailSummary => Kind switch
    {
        InventoryTreeKind.Site => $"Status: {OrDash(StatusText)}",
        InventoryTreeKind.Node =>
            $"Kind: {OrDash(NodeKindText)}; Uplink: {OrDash(UplinkModeText)}; Status: {OrDash(StatusText)}",
        InventoryTreeKind.Device =>
            $"Support: {OrDash(SupportStateText)}; Reachability: {OrDash(ReachabilityText)}; " +
            $"Version: {OrDash(RouterOsVersionText)}; Model: {OrDash(ModelText)}; " +
            $"VRRP: {OrDash(VrrpRolesText)}; Last snapshot: {OrDash(LastSnapshotText)}",
        _ => string.Empty,
    };

    private static string OrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
