using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W4.4: Semantic diff shows why VRRP a-against-b compare is forbidden.</summary>
public sealed class SnapshotDiffViewModelTests
{
    [Fact]
    public void VrrpNodeSelectionShowsCrossDeviceCompareForbidWhy()
    {
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        (InventoryNodeViewModel site, InventoryNodeViewModel node, _) = CreateVrrpSite();
        inventory.Roots.Add(site);
        inventory.SelectedNode = node;

        using SnapshotDiffViewModel vm = new(new StubDiff(), connection, inventory);

        Assert.False(vm.ReloadCapturesCommand.CanExecute(null));
        Assert.True(vm.HasVrrpPairGuidance);
        Assert.Contains("SNAPSHOTS_FROM_DIFFERENT_DEVICES", vm.PairGuidanceText, StringComparison.Ordinal);
        Assert.Contains("do not compare a against b", vm.PairGuidanceText, StringComparison.Ordinal);
        Assert.DoesNotContain("Master", vm.PairGuidanceText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VrrpMemberSelectionKeepsSameDeviceCompareGuidance()
    {
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        (InventoryNodeViewModel site, _, InventoryNodeViewModel memberA) = CreateVrrpSite();
        inventory.Roots.Add(site);
        inventory.SelectedNode = memberA;

        using SnapshotDiffViewModel vm = new(new StubDiff(), connection, inventory);

        Assert.True(vm.ReloadCapturesCommand.CanExecute(null));
        Assert.True(vm.HasVrrpPairGuidance);
        Assert.Equal(InventoryOpsSelection.CrossDeviceCompareForbiddenReason, vm.PairGuidanceText);
    }

    [Fact]
    public async Task CompareMapsDifferentDevicesRpcToOperatorWhy()
    {
        Guid left = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid right = Guid.Parse("22222222-2222-2222-2222-222222222222");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        StubDiff diff = new()
        {
            CompareResult = new SnapshotDiffLoadResult
            {
                Succeeded = false,
                Error = "Snapshots belong to different devices (SNAPSHOTS_FROM_DIFFERENT_DEVICES).",
            },
        };
        using SnapshotDiffViewModel vm = new(diff, connection, inventory)
        {
            BaseCapture = new SnapshotCaptureListItem
            {
                CaptureId = left,
                StatusText = "Completed",
                CompletedAtText = "2026-08-30 12:00:00Z",
                SchemaVersion = 1,
            },
            TargetCapture = new SnapshotCaptureListItem
            {
                CaptureId = right,
                StatusText = "Completed",
                CompletedAtText = "2026-08-30 13:00:00Z",
                SchemaVersion = 1,
            },
        };

        await vm.CompareCommand.ExecuteAsync(null);

        Assert.Equal(InventoryOpsSelection.CrossDeviceCompareForbiddenReason, vm.ErrorText);
        Assert.Equal(1, diff.CompareCalls);
    }

    private static (InventoryNodeViewModel Site, InventoryNodeViewModel Node, InventoryNodeViewModel MemberA)
        CreateVrrpSite()
    {
        Guid nodeId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
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
                            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                            DisplayName = "r1",
                        },
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = Guid.Parse("22222222-3333-4444-5555-666666666666"),
                            DisplayName = "r2",
                        },
                    ],
                },
            ],
        });
        return (site, site.Children[0], site.Children[0].Children[0]);
    }

    private sealed class FakeConnection : IControllerConnectionService
    {
        public ControllerConnectionState State { get; set; } = ControllerConnectionState.Disconnected;

        public string? LastError => null;

        public Grpc.Net.Client.GrpcChannel? Channel => null;

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync() => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EmptyTreeService : IInventoryTreeService
    {
        public InventoryTreeLoadResult Current { get; } = new()
        {
            Roots = [],
            Succeeded = true,
            IsCached = false,
            IsRefreshing = false,
        };

        public Task<InventoryTreeLoadResult> RefreshAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Current);
    }

    private sealed class StubDiff : ISnapshotDiffService
    {
        public SnapshotDiffLoadResult CompareResult { get; init; } = new() { Succeeded = false };

        public int CompareCalls { get; private set; }

        public SnapshotDiffLoadResult Current { get; private set; } = new() { Succeeded = false };

        public Task<SnapshotDiffLoadResult> LoadCapturesAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = new SnapshotDiffLoadResult { Succeeded = true, Captures = [] };
            return Task.FromResult(Current);
        }

        public Task<SnapshotDiffLoadResult> CompareAsync(
            Guid leftCaptureId,
            Guid rightCaptureId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompareCalls++;
            Current = CompareResult;
            return Task.FromResult(CompareResult);
        }

        public void Clear() => Current = new SnapshotDiffLoadResult { Succeeded = false };
    }
}
