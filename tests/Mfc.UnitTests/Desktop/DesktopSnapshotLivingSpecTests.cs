using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-SNAPSHOT-01 / W7-85: Desktop Snapshot capture/compare Living Spec vs SnapshotGrpcHost.</summary>
public sealed class DesktopSnapshotLivingSpecTests
{
    private static readonly string[] ExpectedWireMethods =
    [
        "CompareSnapshots",
        "GetSnapshotSection",
        "GetSnapshotSummary",
        "ListCaptures",
        "StartCapture",
        "WatchCapture",
    ];

    [Fact]
    public void Ac1WireAndDesktopClientExposeCaptureListSectionAndCompareRpcs()
    {
        string[] methods = SnapshotService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedWireMethods, methods);

        Type client = typeof(ISnapshotViewerClient);
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.StartCaptureAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.WatchCaptureAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.ListCapturesAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.GetSummaryAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.GetAllSectionRecordsAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.CompareSnapshotsAsync)));

        string source = ReadSource("src/Mfc.Desktop/Services/GrpcSnapshotViewerClient.cs");
        Assert.Contains("StartCaptureAsync", source, StringComparison.Ordinal);
        Assert.Contains("WatchCaptureAsync", source, StringComparison.Ordinal);
        Assert.Contains("ListCapturesAsync", source, StringComparison.Ordinal);
        Assert.Contains("GetSnapshotSummaryAsync", source, StringComparison.Ordinal);
        Assert.Contains("GetSnapshotSectionAsync", source, StringComparison.Ordinal);
        Assert.Contains("CompareSnapshotsAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ViewModelExposesReloadCaptureCopyAndDiffCompareCommands()
    {
        using SnapshotViewerViewModel snapshot = CreateViewer(
            new StubViewer(),
            new FakeSnapshotClient(),
            ControllerConnectionState.Connected,
            selectDevice: true);

        Assert.NotNull(snapshot.GetType().GetProperty(nameof(SnapshotViewerViewModel.ReloadCommand)));
        Assert.NotNull(snapshot.GetType().GetProperty(nameof(SnapshotViewerViewModel.CaptureCommand)));
        Assert.NotNull(snapshot.GetType().GetProperty(nameof(SnapshotViewerViewModel.CopySanitizedCommand)));
        Assert.NotNull(snapshot.GetType().GetProperty(nameof(SnapshotViewerViewModel.Captures)));
        Assert.True(snapshot.CaptureCommand.CanExecute(null));

        using SnapshotDiffViewModel diff = CreateDiff(
            new StubDiffService(),
            ControllerConnectionState.Connected,
            selectDevice: true);
        Assert.NotNull(diff.GetType().GetProperty(nameof(SnapshotDiffViewModel.CompareCommand)));
        Assert.NotNull(diff.GetType().GetProperty(nameof(SnapshotDiffViewModel.ReloadCapturesCommand)));
    }

    [Fact]
    public async Task Ac3ReloadLoadsCapturesWhenDeviceSelected()
    {
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid captureId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
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
                        CompletedAtText = "2026-09-07 12:00:00Z",
                        SchemaVersion = 1,
                    },
                ],
            },
        };

        using SnapshotViewerViewModel vm = CreateViewer(
            viewer,
            new FakeSnapshotClient(),
            ControllerConnectionState.Connected,
            selectDevice: true,
            deviceId: deviceId);

        await vm.ReloadCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, viewer.LoadDeviceCalls);
        Assert.Equal(captureId, Assert.Single(vm.Captures).CaptureId);
    }

    [Fact]
    public void Ac4CaptureRequiresDeviceSelectionAndConnectedController()
    {
        FakeSnapshotClient disconnectedClient = new();
        using SnapshotViewerViewModel disconnected = CreateViewer(
            new StubViewer(),
            disconnectedClient,
            ControllerConnectionState.Disconnected,
            selectDevice: true);

        Assert.False(disconnected.CaptureCommand.CanExecute(null));
        Assert.False(disconnected.ReloadCommand.CanExecute(null));

        FakeSnapshotClient noDeviceClient = new();
        using SnapshotViewerViewModel noDevice = CreateViewer(
            new StubViewer(),
            noDeviceClient,
            ControllerConnectionState.Connected,
            selectDevice: false);

        Assert.False(noDevice.CaptureCommand.CanExecute(null));
        Assert.False(noDevice.ReloadCommand.CanExecute(null));
        Assert.Equal(0, noDeviceClient.StartCalls);
    }

    [Fact]
    public void Ac5MainWindowBindsSnapshotCaptureAndDiffCompare()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Snapshot.ReloadCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Snapshot.CaptureCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Snapshot.CopySanitizedCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Snapshot.Captures", axaml, StringComparison.Ordinal);
        Assert.Contains("Snapshot.CaptureProgressText", axaml, StringComparison.Ordinal);
        Assert.Contains("Diff.CompareCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Diff.ReloadCapturesCommand", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostContractLivingSpecRemainsPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/SnapshotGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/SnapshotGrpcHostTests.cs"));
        Assert.Contains("StartWatchListSectionCompareAndCancel", host, StringComparison.Ordinal);
        Assert.Contains("NodeCaptureIsRejectedAndAuthIsEnforced", host, StringComparison.Ordinal);
    }

    private static SnapshotViewerViewModel CreateViewer(
        ISnapshotViewerService viewer,
        ISnapshotViewerClient client,
        ControllerConnectionState state,
        bool selectDevice,
        Guid? deviceId = null)
    {
        Guid resolvedDeviceId = deviceId ?? Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeConnection connection = new(state);
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        if (selectDevice)
        {
            inventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
            {
                Kind = InventoryTreeKind.Device,
                Id = resolvedDeviceId,
                DisplayName = "chr-seed",
            });
        }

        return new SnapshotViewerViewModel(viewer, client, connection, inventory);
    }

    private static SnapshotDiffViewModel CreateDiff(
        ISnapshotDiffService diff,
        ControllerConnectionState state,
        bool selectDevice)
    {
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeConnection connection = new(state);
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        if (selectDevice)
        {
            inventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
            {
                Kind = InventoryTreeKind.Device,
                Id = deviceId,
                DisplayName = "chr-seed",
            });
        }

        return new SnapshotDiffViewModel(diff, connection, inventory);
    }

    private static string ReadSource(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
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

    private sealed class StubDiffService : ISnapshotDiffService
    {
        public SnapshotDiffLoadResult Current { get; private set; } = new() { Succeeded = false };

        public Task<SnapshotDiffLoadResult> LoadCapturesAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SnapshotDiffLoadResult> CompareAsync(
            Guid leftCaptureId,
            Guid rightCaptureId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void Clear() => Current = new SnapshotDiffLoadResult { Succeeded = false };
    }

    private sealed class FakeSnapshotClient : ISnapshotViewerClient
    {
        public int StartCalls { get; private set; }

        public Task<StartCaptureResponse> StartCaptureAsync(
            Guid deviceId,
            Guid idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            StartCalls++;
            return Task.FromResult(new StartCaptureResponse());
        }

        public Task<StartCaptureResponse> StartNodeCaptureAsync(
            Guid nodeId,
            Guid idempotencyKey,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<CaptureProgress> WatchCaptureAsync(
            Guid operationId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
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

    private sealed class FakeConnection(ControllerConnectionState state) : IControllerConnectionService
    {
        public GrpcChannel? Channel => null;

        public ControllerConnectionState State { get; } = state;

        public string? LastError => null;

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
}
