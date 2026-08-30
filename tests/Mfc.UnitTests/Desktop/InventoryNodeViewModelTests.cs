using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W1.6: device fields stay explicit; VRRP is shown only when backend labels exist.</summary>
public sealed class InventoryNodeViewModelTests
{
    [Fact]
    public void DeviceExposesReachabilityModelVersionSnapshotAndOptionalVrrp()
    {
        InventoryNodeViewModel device = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            DisplayName = "chr-seed",
            ReachabilityText = "Reachable",
            ModelText = "CHR",
            RouterOsVersionText = "7.16.2",
            VrrpRolesText = "master",
            LastSnapshotText = "2026-08-30 10:00:00Z",
        });

        Assert.True(device.IsDevice);
        Assert.True(device.HasVrrpRoles);
        Assert.Equal("Reachable", device.ReachabilityText);
        Assert.Equal("CHR", device.ModelText);
        Assert.Equal("7.16.2", device.RouterOsVersionText);
        Assert.Equal("master", device.VrrpRolesText);
        Assert.Equal("2026-08-30 10:00:00Z", device.LastSnapshotText);
        Assert.Contains("Reachability: Reachable", device.DetailSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyVrrpPlaceholderIsNotShownAsRoles()
    {
        InventoryNodeViewModel device = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            DisplayName = "standalone",
            VrrpRolesText = "—",
        });

        Assert.True(device.IsDevice);
        Assert.False(device.HasVrrpRoles);
    }

    [Fact]
    public void NodeIsNotADeviceAndHasNoVrrpSurface()
    {
        InventoryNodeViewModel node = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Node,
            Id = Guid.Parse("99999999-8888-7777-6666-555555555555"),
            DisplayName = "core",
            NodeKindText = "Vrrp",
            VrrpRolesText = "master",
        });

        Assert.False(node.IsDevice);
        Assert.False(node.HasVrrpRoles);
    }
}
