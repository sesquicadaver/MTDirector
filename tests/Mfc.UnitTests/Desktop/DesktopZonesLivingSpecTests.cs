using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-ZONE-01 / W7-69: Desktop Zones panel Living Spec vs ZoneGrpcHost (CRUD + resolve).</summary>
public sealed class DesktopZonesLivingSpecTests
{
    private static readonly string[] ExpectedWireMethods =
    [
        "CreateZoneDefinition",
        "DeleteNodeZoneBinding",
        "DeleteZoneDefinition",
        "ListNodeZoneBindings",
        "ListZoneDefinitions",
        "ResolveZonesForDevice",
        "ResolveZonesForNode",
        "UpdateZoneDefinition",
        "UpsertNodeZoneBinding",
    ];

    [Fact]
    public void Ac1WireAndDesktopClientExposeCrudAndResolveRpcs()
    {
        string[] methods = ZoneService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedWireMethods, methods);

        Type client = typeof(IZoneServiceClient);
        Assert.NotNull(client.GetMethod(nameof(IZoneServiceClient.ListZoneDefinitionsAsync)));
        Assert.NotNull(client.GetMethod(nameof(IZoneServiceClient.CreateZoneDefinitionAsync)));
        Assert.NotNull(client.GetMethod(nameof(IZoneServiceClient.UpdateZoneDefinitionAsync)));
        Assert.NotNull(client.GetMethod(nameof(IZoneServiceClient.DeleteZoneDefinitionAsync)));
        Assert.NotNull(client.GetMethod(nameof(IZoneServiceClient.ListNodeZoneBindingsAsync)));
        Assert.NotNull(client.GetMethod(nameof(IZoneServiceClient.UpsertNodeZoneBindingAsync)));
        Assert.NotNull(client.GetMethod(nameof(IZoneServiceClient.DeleteNodeZoneBindingAsync)));
        Assert.NotNull(client.GetMethod(nameof(IZoneServiceClient.ResolveZonesForNodeAsync)));
        Assert.NotNull(client.GetMethod(nameof(IZoneServiceClient.ResolveZonesForDeviceAsync)));

        string source = ReadSource("src/Mfc.Desktop/Services/GrpcZoneServiceClient.cs");
        foreach (string rpc in ExpectedWireMethods)
        {
            Assert.Contains(rpc + "Async", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac2ViewModelExposesZoneCrudBindingAndResolveCommands()
    {
        using ZonesViewModel vm = CreateVm(
            new FakeZonesPanel(),
            ControllerConnectionState.Connected,
            selectNode: false,
            selectDevice: false);

        Assert.NotNull(vm.GetType().GetProperty(nameof(ZonesViewModel.RefreshCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(ZonesViewModel.CreateZoneCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(ZonesViewModel.UpdateZoneCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(ZonesViewModel.DeleteZoneCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(ZonesViewModel.UpsertBindingCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(ZonesViewModel.DeleteBindingCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(ZonesViewModel.ResolveCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(ZonesViewModel.ResolveDeviceCommand)));
        Assert.Contains(NodeZoneBindingKind.SingleInterface, vm.BindingKinds);
    }

    [Fact]
    public async Task Ac3RefreshLoadsZonesAndNodeBindingsWhenNodeSelected()
    {
        Guid nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid zoneId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        FakeZonesPanel panel = new()
        {
            Zones =
            [
                new ZoneDefinitionListItem
                {
                    Id = zoneId,
                    Key = "lan",
                    Name = "LAN",
                    OwnerScopeText = "Company",
                    Description = "corp",
                    RowVersion = 1,
                },
            ],
            Bindings =
            [
                new NodeZoneBindingListItem
                {
                    Id = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
                    ZoneId = zoneId,
                    KindText = nameof(NodeZoneBindingKind.SingleInterface),
                    ValuesText = "ether1",
                    AnalysisStale = false,
                    RowVersion = 2,
                },
            ],
        };

        using ZonesViewModel vm = CreateVm(
            panel,
            ControllerConnectionState.Connected,
            selectNode: true,
            selectDevice: false,
            nodeId: nodeId);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.ListZonesCalls);
        Assert.Equal(1, panel.ListBindingsCalls);
        Assert.Equal(nodeId, panel.LastListBindingsNodeId);
        Assert.Equal(zoneId, Assert.Single(vm.Zones).Id);
        Assert.Equal("ether1", Assert.Single(vm.Bindings).ValuesText);
        Assert.Contains("Node:", vm.SelectedNodeHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac4ResolveRequiresConnectedControllerAndInventorySelection()
    {
        FakeZonesPanel disconnectedPanel = new();
        using ZonesViewModel disconnected = CreateVm(
            disconnectedPanel,
            ControllerConnectionState.Disconnected,
            selectNode: true,
            selectDevice: false);

        await disconnected.ResolveCommand.ExecuteAsync(null);

        Assert.Equal(0, disconnectedPanel.ResolveNodeCalls);
        Assert.Contains("Connect to Controller", disconnected.ErrorText, StringComparison.Ordinal);

        FakeZonesPanel noNodePanel = new();
        using ZonesViewModel noNode = CreateVm(
            noNodePanel,
            ControllerConnectionState.Connected,
            selectNode: false,
            selectDevice: false);

        await noNode.ResolveCommand.ExecuteAsync(null);

        Assert.Equal(0, noNodePanel.ResolveNodeCalls);
        Assert.Contains("Select a Node", noNode.ErrorText, StringComparison.Ordinal);

        FakeZonesPanel noDevicePanel = new();
        using ZonesViewModel noDevice = CreateVm(
            noDevicePanel,
            ControllerConnectionState.Connected,
            selectNode: true,
            selectDevice: false);

        await noDevice.ResolveDeviceCommand.ExecuteAsync(null);

        Assert.Equal(0, noDevicePanel.ResolveDeviceCalls);
        Assert.Contains("Select a Device", noDevice.ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5MainWindowBindsZonesCrudBindingsAndResolve()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Logical zones", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.RefreshCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.CreateZoneCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.UpdateZoneCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.DeleteZoneCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.UpsertBindingCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.DeleteBindingCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.ResolveCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.ResolveDeviceCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.Zones", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.Bindings", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.ResolveResults", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.SelectedNodeHint", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostContractLivingSpecRemainsPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/ZoneGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/ZoneGrpcHostTests.cs"));
        Assert.Contains("ZoneDefinitionBindingResolveAndRowVersionCas", host, StringComparison.Ordinal);
        Assert.Contains("CreateZoneAndUpsertAreIdempotent", host, StringComparison.Ordinal);
    }

    private static ZonesViewModel CreateVm(
        IZonePanelService panel,
        ControllerConnectionState state,
        bool selectNode,
        bool selectDevice,
        Guid? nodeId = null)
    {
        Guid resolvedNodeId = nodeId ?? Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        FakeConnection connection = new(state);
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
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
                    Id = resolvedNodeId,
                    DisplayName = "core",
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
        inventory.Roots.Add(site);
        if (selectDevice)
        {
            inventory.SelectedNode = site.Children[0].Children[0];
        }
        else if (selectNode)
        {
            inventory.SelectedNode = site.Children[0];
        }

        return new ZonesViewModel(panel, connection, inventory);
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

    private sealed class FakeZonesPanel : IZonePanelService
    {
        public IReadOnlyList<ZoneDefinitionListItem> Zones { get; init; } = [];

        public IReadOnlyList<NodeZoneBindingListItem> Bindings { get; init; } = [];

        public int ListZonesCalls { get; private set; }

        public int ListBindingsCalls { get; private set; }

        public int ResolveNodeCalls { get; private set; }

        public int ResolveDeviceCalls { get; private set; }

        public Guid? LastListBindingsNodeId { get; private set; }

        public Task<IReadOnlyList<ZoneDefinitionListItem>> ListZonesAsync(
            CancellationToken cancellationToken = default)
        {
            ListZonesCalls++;
            return Task.FromResult(Zones);
        }

        public Task<ZoneDefinitionListItem> CreateCompanyZoneAsync(
            string key,
            string name,
            string? description,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ZoneDefinitionListItem> UpdateZoneAsync(
            ZoneDefinitionListItem zone,
            string name,
            string? description,
            bool resetDescription,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteZoneAsync(
            ZoneDefinitionListItem zone,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<NodeZoneBindingListItem>> ListBindingsAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
        {
            ListBindingsCalls++;
            LastListBindingsNodeId = nodeId;
            return Task.FromResult(Bindings);
        }

        public Task<NodeZoneBindingListItem> UpsertBindingAsync(
            Guid nodeId,
            Guid zoneId,
            NodeZoneBindingKind kind,
            IReadOnlyList<string> values,
            ulong? expectedRowVersion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteBindingAsync(
            NodeZoneBindingListItem binding,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ZoneResolveResultListItem>> ResolveForNodeAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
        {
            ResolveNodeCalls++;
            return Task.FromResult<IReadOnlyList<ZoneResolveResultListItem>>([]);
        }

        public Task<IReadOnlyList<ZoneResolveResultListItem>> ResolveForDeviceAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            ResolveDeviceCalls++;
            return Task.FromResult<IReadOnlyList<ZoneResolveResultListItem>>([]);
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
