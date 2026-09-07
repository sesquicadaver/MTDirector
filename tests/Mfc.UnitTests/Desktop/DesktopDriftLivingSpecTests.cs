using Google.Protobuf;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-DRIFT-01 / W7-68: Desktop Drift panel Living Spec vs DriftGrpcHost (List + Get, no auto-fix).</summary>
public sealed class DesktopDriftLivingSpecTests
{
    [Fact]
    public void Ac1WireAndDesktopClientAreListAndGetOnly()
    {
        string[] methods = DriftService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["GetDriftEvent", "ListDeviceDriftEvents"], methods);

        Assert.NotNull(typeof(IDriftServiceClient).GetMethod(nameof(IDriftServiceClient.ListDeviceDriftEventsAsync)));
        Assert.NotNull(typeof(IDriftServiceClient).GetMethod(nameof(IDriftServiceClient.GetDriftEventAsync)));
        Assert.Null(typeof(IDriftServiceClient).GetMethod("ForceRepairAsync"));
        Assert.Null(typeof(IDriftServiceClient).GetMethod("AutoHealAsync"));
        Assert.Null(typeof(IDriftServiceClient).GetMethod("DetectManagedDriftAsync"));

        string client = ReadSource("src/Mfc.Desktop/Services/GrpcDriftServiceClient.cs");
        Assert.Contains("ListDeviceDriftEventsAsync", client, StringComparison.Ordinal);
        Assert.Contains("GetDriftEventAsync", client, StringComparison.Ordinal);
        Assert.DoesNotContain("ForceRepair", client, StringComparison.Ordinal);
        Assert.DoesNotContain("AutoHeal", client, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ViewModelHasNoAutomaticFixOrRepairCommands()
    {
        using DriftViewModel vm = CreateVm(
            new FakeDriftClient([]),
            ControllerConnectionState.Connected,
            deviceId: Guid.Parse("11111111-2222-3333-4444-555555555555"));

        Assert.False(vm.HasAutomaticFix);
        Assert.False(vm.HasForceRepairCommand);
        Assert.False(vm.HasAutoHealCommand);
        Assert.NotNull(vm.GetType().GetProperty(nameof(DriftViewModel.RefreshCommand)));
        Assert.Null(vm.GetType().GetProperty("ForceRepairCommand"));
        Assert.Null(vm.GetType().GetProperty("AutoHealCommand"));
        Assert.Null(vm.GetType().GetMethod("ForceRepairAsync"));
        Assert.Null(vm.GetType().GetMethod("AutoHealAsync"));
    }

    [Fact]
    public async Task Ac3RefreshLoadsListThenGetDetailForSelectedDevice()
    {
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid nodeId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        FakeDriftClient client = new(
        [
            new DriftEvent
            {
                Id = DesktopProtoUuid.FromGuid(eventId),
                DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                NodeId = DesktopProtoUuid.FromGuid(nodeId),
                Outcome = DriftOutcome.WarningDrift,
                SemanticDiffCanonical = "list-diff",
                Findings =
                {
                    new DriftFinding
                    {
                        Kind = DriftFindingKind.CountersChanged,
                        Severity = DriftSeverity.Ignored,
                    },
                },
            },
        ])
        {
            GetPayload = new DriftEvent
            {
                Id = DesktopProtoUuid.FromGuid(eventId),
                DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                NodeId = DesktopProtoUuid.FromGuid(nodeId),
                Outcome = DriftOutcome.WarningDrift,
                Immutable = true,
                BaselineCommittedHash = HashFill(0xab),
                ActualManagedResourceHash = HashFill(0xcd),
                DesiredArtifactHashIgnoredForBaseline = HashFill(0x11),
                SemanticDiffHash = HashFill(0x22),
                SemanticDiffCanonical = "get-diff-canonical",
                Findings =
                {
                    new DriftFinding
                    {
                        Kind = DriftFindingKind.ManagedRuleChanged,
                        Severity = DriftSeverity.Critical,
                        Detail = "rule 1 changed",
                    },
                },
            },
        };

        using DriftViewModel vm = CreateVm(client, ControllerConnectionState.Connected, deviceId);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, client.ListCalls);
        Assert.Equal(deviceId, client.LastListDeviceId);
        Assert.Equal(1, client.GetCalls);
        Assert.Equal(eventId, client.LastGetId);
        Assert.Equal(eventId, vm.SelectedEvent!.Id);
        Assert.True(vm.HasSelectedEventDetail);
        Assert.Equal(nodeId.ToString("D"), vm.DetailNodeIdText);
        Assert.Equal("get-diff-canonical", vm.SemanticDiffText);
        Assert.Equal("immutable", vm.DetailImmutableText);
        Assert.Equal(nameof(DriftFindingKind.ManagedRuleChanged), Assert.Single(vm.SelectedEventFindings).KindText);
        Assert.Contains(deviceId.ToString("D"), vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac4RefreshRequiresConnectedControllerAndSelectedDevice()
    {
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeDriftClient disconnectedClient = new([]);
        using DriftViewModel disconnected = CreateVm(
            disconnectedClient,
            ControllerConnectionState.Disconnected,
            deviceId);

        await disconnected.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, disconnectedClient.ListCalls);
        Assert.Contains("Connect to Controller", disconnected.ErrorText, StringComparison.Ordinal);
        Assert.Empty(disconnected.Events);

        FakeDriftClient noDeviceClient = new([]);
        using DriftViewModel noDevice = CreateVm(
            noDeviceClient,
            ControllerConnectionState.Connected,
            deviceId: null);

        await noDevice.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, noDeviceClient.ListCalls);
        Assert.Contains("Select a Device", noDevice.ErrorText, StringComparison.Ordinal);
        Assert.Empty(noDevice.Events);
    }

    [Fact]
    public void Ac5MainWindowBindsDriftListDetailAndRefreshWithoutAutoFix()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("<!-- Drift (no automatic fix) -->", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.RefreshCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.Events", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.SelectedEvent", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.SelectedEventFindings", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.SemanticDiffText", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.DetailBaselineHashText", axaml, StringComparison.Ordinal);
        Assert.Contains("GetDriftEvent", axaml, StringComparison.Ordinal);
        Assert.Contains("no automatic fix", axaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ForceRepair", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AutoHeal", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostContractLivingSpecRemainsPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/DriftGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/DriftGrpcHostTests.cs"));
        Assert.Contains("ListAndGetDriftEventsAfterDetect", host, StringComparison.Ordinal);
        Assert.Contains("DriftServiceHasNoMutationRpcsOnWire", host, StringComparison.Ordinal);
    }

    private static DriftViewModel CreateVm(
        IDriftServiceClient client,
        ControllerConnectionState state,
        Guid? deviceId)
    {
        FakeConnection connection = new(state);
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        if (deviceId is Guid id)
        {
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
                        Id = Guid.Parse("99999999-8888-7777-6666-555555555555"),
                        DisplayName = "core",
                        Children =
                        [
                            new InventoryTreeItem
                            {
                                Kind = InventoryTreeKind.Device,
                                Id = id,
                                DisplayName = "chr-seed",
                            },
                        ],
                    },
                ],
            });
            inventory.Roots.Add(site);
            inventory.SelectedNode = site.Children[0].Children[0];
        }

        return new DriftViewModel(client, connection, inventory);
    }

    private static Sha256 HashFill(byte fill)
        => new() { Value = ByteString.CopyFrom(Enumerable.Repeat(fill, 32).ToArray()) };

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

    private sealed class FakeDriftClient(IReadOnlyList<DriftEvent> listEvents) : IDriftServiceClient
    {
        public DriftEvent? GetPayload { get; init; }

        public int ListCalls { get; private set; }

        public int GetCalls { get; private set; }

        public Guid? LastListDeviceId { get; private set; }

        public Guid? LastGetId { get; private set; }

        public Task<IReadOnlyList<DriftEvent>> ListDeviceDriftEventsAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            ListCalls++;
            LastListDeviceId = deviceId;
            return Task.FromResult(listEvents);
        }

        public Task<DriftEvent> GetDriftEventAsync(
            Guid driftEventId,
            CancellationToken cancellationToken = default)
        {
            GetCalls++;
            LastGetId = driftEventId;
            return Task.FromResult(GetPayload ?? new DriftEvent { Id = DesktopProtoUuid.FromGuid(driftEventId) });
        }
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
