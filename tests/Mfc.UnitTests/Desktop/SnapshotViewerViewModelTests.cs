using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W3.1: Snapshots Capture calls StartCapture + WatchCapture, then reloads the device list.</summary>
public sealed class SnapshotViewerViewModelTests
{
    [Fact]
    public async Task CaptureStartThenWatchReloadsDeviceAndShowsTerminalProgress()
    {
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid captureId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid operationId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        inventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = deviceId,
            DisplayName = "chr-seed",
        });

        StubViewer viewer = new()
        {
            DeviceLoad = new SnapshotViewerLoadResult
            {
                Succeeded = true,
                DeviceId = deviceId,
                CaptureId = captureId,
                StatusText = "Completed",
                Captures =
                [
                    new SnapshotCaptureListItem
                    {
                        CaptureId = captureId,
                        StatusText = "Completed",
                        CompletedAtText = "2026-08-30 12:00:00Z",
                        SchemaVersion = 1,
                    },
                ],
            },
        };
        FakeSnapshotClient client = new()
        {
            StartResponse = new StartCaptureResponse
            {
                OperationId = DesktopProtoUuid.FromGuid(operationId),
                CaptureId = DesktopProtoUuid.FromGuid(captureId),
            },
            WatchEvents =
            [
                new CaptureProgress
                {
                    Stage = CaptureStage.Queued,
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                    DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                },
                new CaptureProgress
                {
                    Stage = CaptureStage.ReadingPass1,
                    CurrentSection = "firewall.ipv4.filter",
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                    DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                },
                new CaptureProgress
                {
                    Stage = CaptureStage.Completed,
                    CaptureId = DesktopProtoUuid.FromGuid(captureId),
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                    DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                },
            ],
        };

        using SnapshotViewerViewModel vm = new(viewer, client, connection, inventory);
        Assert.True(vm.CaptureCommand.CanExecute(null));

        await vm.CaptureCommand.ExecuteAsync(null);

        Assert.Equal(1, client.StartCalls);
        Assert.Equal(deviceId, client.LastDeviceId);
        Assert.NotEqual(Guid.Empty, client.LastIdempotencyKey);
        Assert.Equal(1, client.WatchCalls);
        Assert.Equal(operationId, client.WatchedOperationId);
        Assert.Equal(1, viewer.LoadDeviceCalls);
        Assert.Equal("Completed", vm.CaptureProgressText);
        Assert.Equal(captureId, Assert.Single(vm.Captures).CaptureId);
        Assert.Null(vm.ErrorText);
        Assert.False(vm.IsCapturing);
    }

    [Fact]
    public async Task CaptureFailedWatchSetsErrorWithoutReload()
    {
        Guid deviceId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        Guid operationId = Guid.Parse("aaaaaaaa-0000-1111-2222-333333333333");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        inventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = deviceId,
            DisplayName = "chr-seed",
        });

        StubViewer viewer = new();
        FakeSnapshotClient client = new()
        {
            StartResponse = new StartCaptureResponse
            {
                OperationId = DesktopProtoUuid.FromGuid(operationId),
            },
            WatchEvents =
            [
                new CaptureProgress
                {
                    Stage = CaptureStage.Failed,
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                    DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                    Error = new ErrorDetail { SanitizedDetail = "device unreachable" },
                },
            ],
        };

        using SnapshotViewerViewModel vm = new(viewer, client, connection, inventory);
        await vm.CaptureCommand.ExecuteAsync(null);

        Assert.Equal(1, client.StartCalls);
        Assert.Equal(1, client.WatchCalls);
        Assert.Equal(0, viewer.LoadDeviceCalls);
        Assert.Equal("Failed: device unreachable", vm.CaptureProgressText);
        Assert.Equal("device unreachable", vm.ErrorText);
        Assert.Empty(vm.Captures);
    }

    [Fact]
    public void CaptureCommandDisabledWhenDisconnected()
    {
        FakeConnection connection = new() { State = ControllerConnectionState.Disconnected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        inventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = Guid.Parse("33333333-4444-5555-6666-777777777777"),
            DisplayName = "chr-seed",
        });

        using SnapshotViewerViewModel vm = new(new StubViewer(), new FakeSnapshotClient(), connection, inventory);
        Assert.False(vm.CaptureCommand.CanExecute(null));
    }

    [Fact]
    public void VrrpNodeSelectionShowsPerMemberCaptureGuidanceAndDisablesCapture()
    {
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        (InventoryNodeViewModel site, InventoryNodeViewModel node, _) = CreateVrrpSite();
        inventory.Roots.Add(site);
        inventory.SelectedNode = node;

        using SnapshotViewerViewModel vm = new(new StubViewer(), new FakeSnapshotClient(), connection, inventory);

        Assert.False(vm.CaptureCommand.CanExecute(null));
        Assert.True(vm.HasVrrpPairGuidance);
        Assert.Equal(InventoryOpsSelection.VrrpPairCaptureNodeHint, vm.PairGuidanceText);
        Assert.DoesNotContain("Master", vm.PairGuidanceText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VrrpMemberSelectionKeepsDeviceCaptureAndShowsSameDeviceCompareHint()
    {
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        (InventoryNodeViewModel site, _, InventoryNodeViewModel memberA) = CreateVrrpSite();
        inventory.Roots.Add(site);
        inventory.SelectedNode = memberA;

        using SnapshotViewerViewModel vm = new(new StubViewer(), new FakeSnapshotClient(), connection, inventory);

        Assert.True(vm.CaptureCommand.CanExecute(null));
        Assert.True(vm.HasVrrpPairGuidance);
        Assert.Equal(InventoryOpsSelection.VrrpPairCaptureMemberHint, vm.PairGuidanceText);
        Assert.Contains("same device", vm.PairGuidanceText, StringComparison.Ordinal);
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

    private sealed class StubViewer : ISnapshotViewerService
    {
        public SnapshotViewerLoadResult DeviceLoad { get; init; } = new() { Succeeded = false };

        public int LoadDeviceCalls { get; private set; }

        public SnapshotViewerLoadResult Current { get; private set; } = new() { Succeeded = false };

        public Task<SnapshotViewerLoadResult> LoadDeviceAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadDeviceCalls++;
            Current = DeviceLoad;
            return Task.FromResult(DeviceLoad);
        }

        public Task<SnapshotViewerLoadResult> LoadCaptureAsync(
            Guid captureId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SnapshotViewerLoadResult> LoadSectionAsync(
            Guid captureId,
            string sectionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void Clear() => Current = new SnapshotViewerLoadResult { Succeeded = false };
    }

    private sealed class FakeSnapshotClient : ISnapshotViewerClient
    {
        public StartCaptureResponse StartResponse { get; init; } = new();

        public IReadOnlyList<CaptureProgress> WatchEvents { get; init; } = [];

        public int StartCalls { get; private set; }

        public int WatchCalls { get; private set; }

        public Guid LastDeviceId { get; private set; }

        public Guid LastIdempotencyKey { get; private set; }

        public Guid WatchedOperationId { get; private set; }

        public Task<StartCaptureResponse> StartCaptureAsync(
            Guid deviceId,
            Guid idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCalls++;
            LastDeviceId = deviceId;
            LastIdempotencyKey = idempotencyKey;
            return Task.FromResult(StartResponse);
        }

        public async IAsyncEnumerable<CaptureProgress> WatchCaptureAsync(
            Guid operationId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            WatchCalls++;
            WatchedOperationId = operationId;
            foreach (CaptureProgress progress in WatchEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return progress;
                await Task.Yield();
            }
        }

        public Task<IReadOnlyList<SnapshotSummary>> ListCapturesAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SnapshotSummary> GetSummaryAsync(Guid captureId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<SnapshotRecord>> GetAllSectionRecordsAsync(
            Guid captureId,
            string sectionId,
            DiffDomain domain,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiffPage> CompareSnapshotsAsync(
            Guid leftCaptureId,
            Guid rightCaptureId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
