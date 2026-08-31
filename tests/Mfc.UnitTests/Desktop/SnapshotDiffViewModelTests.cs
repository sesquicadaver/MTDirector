using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W4.4 VRRP compare why + W2.1 Before/After records and warning truncate.</summary>
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

    [Fact]
    public async Task CompareTruncatesWarningsAndSurfacesSelectedBeforeAfter()
    {
        string[] warnings = Enumerable.Range(1, 15).Select(i => "hash_changed:f" + i).ToArray();
        SnapshotDiffEntryItem entry = new()
        {
            SectionId = "firewall.ipv4.filter",
            DomainText = "Configuration",
            ChangesText = "Modified",
            RecordKey = "fwc:rule:1",
            OrdinalText = "order: 0 → 1",
            ConfidenceText = "ControllerId",
            FieldLines =
            [
                new SnapshotDiffFieldLine { FieldName = "action", Summary = "action: accept → drop" },
            ],
            HasBeforeRecord = true,
            HasAfterRecord = true,
            BeforeStableKey = "fwc:rule:1",
            AfterStableKey = "fwc:rule:1",
            BeforeRecordFields =
            [
                new SnapshotDiffFieldLine { FieldName = "action", Summary = "action=accept" },
            ],
            AfterRecordFields =
            [
                new SnapshotDiffFieldLine { FieldName = "action", Summary = "action=drop" },
            ],
        };
        StubDiff diff = new()
        {
            CompareResult = new SnapshotDiffLoadResult
            {
                Succeeded = true,
                Warnings = warnings,
                AllEntries = [entry],
                SectionGroups =
                [
                    new SnapshotDiffSectionGroup
                    {
                        SectionId = entry.SectionId,
                        EntryCount = 1,
                        Entries = [entry],
                    },
                ],
            },
        };
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        using SnapshotDiffViewModel vm = new(diff, connection, inventory)
        {
            BaseCapture = new SnapshotCaptureListItem
            {
                CaptureId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                StatusText = "Completed",
                CompletedAtText = "2026-08-30 12:00:00Z",
                SchemaVersion = 1,
            },
            TargetCapture = new SnapshotCaptureListItem
            {
                CaptureId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                StatusText = "Completed",
                CompletedAtText = "2026-08-30 13:00:00Z",
                SchemaVersion = 1,
            },
        };

        await vm.CompareCommand.ExecuteAsync(null);

        Assert.Equal(15, vm.Warnings.Count);
        Assert.Equal(SnapshotDiffService.MaxVisibleCompareWarnings, vm.VisibleWarnings.Count);
        Assert.True(vm.HasWarningOverflow);
        Assert.Contains("truncated", vm.WarningOverflowText, StringComparison.Ordinal);
        Assert.True(vm.HasSelectedEntryRecord);
        Assert.Equal("action=accept", Assert.Single(vm.SelectedEntry!.BeforeRecordFields).Summary);
        Assert.Equal("action=drop", Assert.Single(vm.SelectedEntry.AfterRecordFields).Summary);
        Assert.DoesNotContain("WriteEnabled", vm.StatusText, StringComparison.Ordinal);
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
