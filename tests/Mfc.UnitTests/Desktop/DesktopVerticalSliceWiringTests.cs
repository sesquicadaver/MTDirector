using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>M1-30 AC#9: Desktop wires inventory, snapshot viewer, and semantic diff (no Avalonia headless).</summary>
public sealed class DesktopVerticalSliceWiringTests
{
    [Fact]
    public void ShellExposesInventorySnapshotAndDiffViewModels()
    {
        System.Reflection.PropertyInfo[] properties = typeof(ShellViewModel).GetProperties();
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Inventory)
                                         && p.PropertyType == typeof(InventoryTreeViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Snapshot)
                                         && p.PropertyType == typeof(SnapshotViewerViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Diff)
                                         && p.PropertyType == typeof(SnapshotDiffViewModel));
    }

    [Fact]
    public void DesktopClientsCoverInventorySnapshotAndCompareRpcs()
    {
        Type client = typeof(ISnapshotViewerClient);
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.ListCapturesAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.GetSummaryAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.GetAllSectionRecordsAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.CompareSnapshotsAsync)));

        Type inventory = typeof(IInventoryTreeClient);
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.ListAllSitesAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.ListAllNodesAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.GetNodeAsync)));
    }
}
