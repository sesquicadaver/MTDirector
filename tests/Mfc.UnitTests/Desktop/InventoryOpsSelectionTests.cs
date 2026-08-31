using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W4.2 ops pair resolve + W4.4 per-member capture / same-device compare guidance.</summary>
public sealed class InventoryOpsSelectionTests
{
    [Fact]
    public void VrrpNodeRequiresAllMemberIdsNotOnlyFirstChild()
    {
        (InventoryNodeViewModel site, Guid nodeId, Guid deviceA, Guid deviceB) = CreateVrrpSite();
        InventoryNodeViewModel node = site.Children[0];

        IReadOnlyList<Guid> ids = InventoryOpsSelection.RequireDeviceIds(node);

        Assert.Equal(nodeId, InventoryOpsSelection.RequireNode(node, [site]).Id);
        Assert.Equal([deviceA, deviceB], ids);
        Assert.True(InventoryOpsSelection.IsVrrpPair(node, [site]));
        Assert.Equal(InventoryOpsSelection.VrrpPairHint, InventoryOpsSelection.FormatTargetHint(node, [site]));
        Assert.DoesNotContain("Master", InventoryOpsSelection.VrrpPairHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectingVrrpMemberStillResolvesPairNotThatDeviceAlone()
    {
        (InventoryNodeViewModel site, Guid nodeId, Guid deviceA, Guid deviceB) = CreateVrrpSite();
        InventoryNodeViewModel memberB = site.Children[0].Children[1];

        InventoryNodeViewModel node = InventoryOpsSelection.RequireNode(memberB, [site]);
        IReadOnlyList<Guid> ids = InventoryOpsSelection.RequireDeviceIds(node);

        Assert.Equal(nodeId, node.Id);
        Assert.Equal([deviceA, deviceB], ids);
        Assert.Contains(deviceB, ids);
        Assert.True(InventoryOpsSelection.IsVrrpPair(memberB, [site]));
    }

    [Fact]
    public void RouterNodeUsesEveryDeviceMember()
    {
        Guid nodeId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        InventoryNodeViewModel site = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Site,
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            DisplayName = "LAB",
            Children =
            [
                new InventoryTreeItem
                {
                    Kind = InventoryTreeKind.Node,
                    Id = nodeId,
                    DisplayName = "core",
                    NodeKindText = "Router",
                    Children =
                    [
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceId,
                            DisplayName = "chr-seed",
                        },
                    ],
                },
            ],
        });
        InventoryNodeViewModel node = site.Children[0];

        Assert.Equal([deviceId], InventoryOpsSelection.RequireDeviceIds(node));
        Assert.False(InventoryOpsSelection.IsVrrpPair(node, [site]));
        Assert.Contains("every Device member", InventoryOpsSelection.FormatTargetHint(node, [site]), StringComparison.Ordinal);
    }

    [Fact]
    public void NodeWithoutDevicesThrows()
    {
        InventoryNodeViewModel node = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Node,
            Id = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            DisplayName = "empty",
            NodeKindText = "Vrrp",
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => InventoryOpsSelection.RequireDeviceIds(node));
        Assert.Contains("no Device child", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSelectionThrowsForRequireNode()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => InventoryOpsSelection.RequireNode(null, []));
        Assert.Contains("Select a Node", ex.Message, StringComparison.Ordinal);
        Assert.Equal("Select a Node, then create a plan.", InventoryOpsSelection.FormatTargetHint(null, []));
    }

    [Fact]
    public void VrrpPairCaptureGuidanceIsPerMemberNotNode()
    {
        (InventoryNodeViewModel site, _, _, _) = CreateVrrpSite();
        InventoryNodeViewModel node = site.Children[0];
        InventoryNodeViewModel memberA = node.Children[0];

        Assert.Equal(
            InventoryOpsSelection.VrrpPairCaptureNodeHint,
            InventoryOpsSelection.FormatCaptureGuidance(node, [site]));
        Assert.Equal(
            InventoryOpsSelection.VrrpPairCaptureMemberHint,
            InventoryOpsSelection.FormatCaptureGuidance(memberA, [site]));
        Assert.Contains("per member", InventoryOpsSelection.VrrpPairCaptureNodeHint, StringComparison.Ordinal);
        Assert.DoesNotContain("Master", InventoryOpsSelection.VrrpPairCaptureNodeHint, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(InventoryOpsSelection.FormatCaptureGuidance(null, [site]));
    }

    [Fact]
    public void VrrpPairCompareGuidanceExplainsSameDeviceForbid()
    {
        (InventoryNodeViewModel site, _, _, _) = CreateVrrpSite();
        InventoryNodeViewModel node = site.Children[0];
        InventoryNodeViewModel memberB = node.Children[1];

        string nodeHint = InventoryOpsSelection.FormatCompareGuidance(node, [site]);
        string memberHint = InventoryOpsSelection.FormatCompareGuidance(memberB, [site]);

        Assert.Contains("Select a VRRP member Device", nodeHint, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOTS_FROM_DIFFERENT_DEVICES", nodeHint, StringComparison.Ordinal);
        Assert.Equal(InventoryOpsSelection.CrossDeviceCompareForbiddenReason, memberHint);
        Assert.Contains("do not compare a against b", memberHint, StringComparison.Ordinal);
        Assert.DoesNotContain("Master", memberHint, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(InventoryOpsSelection.FormatCompareGuidance(null, [site]));
    }

    [Fact]
    public void ExplainCompareErrorMapsDifferentDevicesCode()
    {
        Assert.Equal(
            InventoryOpsSelection.CrossDeviceCompareForbiddenReason,
            InventoryOpsSelection.ExplainCompareError(
                "Snapshots belong to different devices (SNAPSHOTS_FROM_DIFFERENT_DEVICES)."));
        Assert.Equal(
            InventoryOpsSelection.CrossDeviceCompareForbiddenReason,
            InventoryOpsSelection.ExplainCompareError("snapshots_from_different_devices"));
        Assert.Equal("Load cancelled.", InventoryOpsSelection.ExplainCompareError("Load cancelled."));
        Assert.Null(InventoryOpsSelection.ExplainCompareError(null));
    }

    private static (InventoryNodeViewModel Site, Guid NodeId, Guid DeviceA, Guid DeviceB) CreateVrrpSite()
    {
        Guid nodeId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid deviceA = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceB = Guid.Parse("22222222-3333-4444-5555-666666666666");
        InventoryNodeViewModel site = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Site,
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            DisplayName = "LAB",
            Children =
            [
                new InventoryTreeItem
                {
                    Kind = InventoryTreeKind.Node,
                    Id = nodeId,
                    DisplayName = "edge-pair",
                    NodeKindText = "Vrrp",
                    Children =
                    [
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceA,
                            DisplayName = "r1",
                        },
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceB,
                            DisplayName = "r2",
                        },
                    ],
                },
            ],
        });
        return (site, nodeId, deviceA, deviceB);
    }
}
