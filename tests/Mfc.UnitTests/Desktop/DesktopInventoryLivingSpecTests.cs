using Google.Protobuf;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-INVENTORY-01 / W7-87: Desktop Inventory Living Spec vs InventoryGrpcHost.</summary>
public sealed class DesktopInventoryLivingSpecTests
{
    private static readonly string[] ExpectedWireMethods =
    [
        "CreateNode",
        "CreateSite",
        "GetNode",
        "GetNodeWorkflow",
        "ListNeighborCandidates",
        "ListNodes",
        "ListSites",
        "RegisterDevice",
        "UpdateDevice",
        "UpdateDeviceConnection",
        "ValidateDeviceConnection",
        "ValidateVrrpPairConsistency",
    ];

    [Fact]
    public void Ac1WireAndDesktopClientExposeTreeWizardAndNodeWorkflowRpcs()
    {
        string[] methods = InventoryService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedWireMethods, methods);

        Type client = typeof(IInventoryTreeClient);
        Assert.NotNull(client.GetMethod(nameof(IInventoryTreeClient.ListAllSitesAsync)));
        Assert.NotNull(client.GetMethod(nameof(IInventoryTreeClient.ListAllNodesAsync)));
        Assert.NotNull(client.GetMethod(nameof(IInventoryTreeClient.GetNodeAsync)));
        Assert.NotNull(client.GetMethod(nameof(IInventoryTreeClient.GetNodeWorkflowAsync)));
        Assert.NotNull(client.GetMethod(nameof(IInventoryTreeClient.CreateSiteAsync)));
        Assert.NotNull(client.GetMethod(nameof(IInventoryTreeClient.CreateNodeAsync)));
        Assert.NotNull(client.GetMethod(nameof(IInventoryTreeClient.RegisterDeviceAsync)));
        Assert.NotNull(client.GetMethod(nameof(IInventoryTreeClient.UpdateDeviceConnectionAsync)));
        Assert.NotNull(client.GetMethod(nameof(IInventoryTreeClient.ValidateDeviceConnectionAsync)));
        Assert.NotNull(client.GetMethod(nameof(IInventoryTreeClient.ListNeighborCandidatesAsync)));
        Assert.NotNull(client.GetMethod(nameof(IInventoryTreeClient.ValidateVrrpPairConsistencyAsync)));

        string source = ReadSource("src/Mfc.Desktop/Services/GrpcInventoryTreeClient.cs");
        Assert.Contains("ListSitesAsync", source, StringComparison.Ordinal);
        Assert.Contains("ListNodesAsync", source, StringComparison.Ordinal);
        Assert.Contains("GetNodeAsync", source, StringComparison.Ordinal);
        Assert.Contains("GetNodeWorkflowAsync", source, StringComparison.Ordinal);
        Assert.Contains("CreateSiteAsync", source, StringComparison.Ordinal);
        Assert.Contains("CreateNodeAsync", source, StringComparison.Ordinal);
        Assert.Contains("RegisterDeviceAsync", source, StringComparison.Ordinal);
        Assert.Contains("UpdateDeviceConnectionAsync", source, StringComparison.Ordinal);
        Assert.Contains("ValidateDeviceConnectionAsync", source, StringComparison.Ordinal);
        Assert.Contains("ListNeighborCandidatesAsync", source, StringComparison.Ordinal);
        Assert.Contains("ValidateVrrpPairConsistencyAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateDeviceAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ViewModelsExposeRefreshSubmitProbeNeighborsAndNodeRefresh()
    {
        FakeConnection connection = new(ControllerConnectionState.Connected);
        using InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        using AddRouterWizardViewModel addRouter = new(new StubInventoryClient(), connection, inventory);

        Assert.NotNull(inventory.GetType().GetProperty(nameof(InventoryTreeViewModel.RefreshCommand)));
        Assert.NotNull(inventory.GetType().GetProperty(nameof(InventoryTreeViewModel.Roots)));
        Assert.NotNull(inventory.GetType().GetProperty(nameof(InventoryTreeViewModel.SelectedNode)));
        Assert.NotNull(addRouter.GetType().GetProperty(nameof(AddRouterWizardViewModel.SubmitCommand)));
        Assert.NotNull(addRouter.GetType().GetProperty(nameof(AddRouterWizardViewModel.ProbeCommand)));
        Assert.NotNull(addRouter.GetType().GetProperty(nameof(AddRouterWizardViewModel.LoadNeighborsCommand)));
        Assert.NotNull(typeof(NodeDetailViewModel).GetProperty(nameof(NodeDetailViewModel.RefreshCommand)));
        Assert.NotNull(typeof(NodeDetailViewModel).GetProperty(nameof(NodeDetailViewModel.ValidateVrrpPairCommand)));
        Assert.True(inventory.RefreshCommand.CanExecute(null));
        Assert.True(addRouter.SubmitCommand.CanExecute(null));
    }

    [Fact]
    public async Task Ac3RefreshLoadsSiteNodeDeviceTreeWhenConnected()
    {
        Guid siteId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        StubTreeService tree = new()
        {
            Load = new InventoryTreeLoadResult
            {
                Succeeded = true,
                IsCached = false,
                IsRefreshing = false,
                Roots =
                [
                    new InventoryTreeItem
                    {
                        Kind = InventoryTreeKind.Site,
                        Id = siteId,
                        DisplayName = "LAB",
                        Children =
                        [
                            new InventoryTreeItem
                            {
                                Kind = InventoryTreeKind.Node,
                                Id = nodeId,
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
                    },
                ],
            },
        };

        FakeConnection connection = new(ControllerConnectionState.Connected);
        using InventoryTreeViewModel inventory = new(tree, connection);

        await inventory.RefreshCommand.ExecuteAsync(null);

        Assert.Null(inventory.ErrorText);
        Assert.Equal(1, tree.RefreshCalls);
        InventoryNodeViewModel site = Assert.Single(inventory.Roots);
        Assert.Equal("LAB", site.DisplayName);
        Assert.Equal("core", Assert.Single(site.Children).DisplayName);
        Assert.Equal("chr-seed", Assert.Single(site.Children[0].Children).DisplayName);
    }

    [Fact]
    public void Ac4RefreshAndSubmitRequireConnectedController()
    {
        FakeConnection disconnected = new(ControllerConnectionState.Disconnected);
        StubTreeService tree = new();
        using InventoryTreeViewModel inventory = new(tree, disconnected);
        using AddRouterWizardViewModel addRouter = new(new StubInventoryClient(), disconnected, inventory);

        Assert.False(inventory.RefreshCommand.CanExecute(null));
        Assert.False(addRouter.SubmitCommand.CanExecute(null));
        Assert.Equal(0, tree.RefreshCalls);
    }

    [Fact]
    public void Ac5MainWindowBindsInventoryTreeAddRouterAndNodeRefresh()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Inventory.RefreshCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Inventory.Roots", axaml, StringComparison.Ordinal);
        Assert.Contains("Inventory.SelectedNode", axaml, StringComparison.Ordinal);
        Assert.Contains("Inventory.ErrorText", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.SubmitCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.ProbeCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.LoadNeighborsCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.RefreshCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.ValidateVrrpPairCommand", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostContractLivingSpecRemainsPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/InventoryGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/InventoryGrpcHostTests.cs"));
        Assert.Contains("InventoryLifecycleListGetRegisterValidateAndIdempotency", host, StringComparison.Ordinal);
        Assert.Contains("InventoryMutationsAreForbiddenWithoutPermission", host, StringComparison.Ordinal);
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

    private sealed class StubTreeService : IInventoryTreeService
    {
        public InventoryTreeLoadResult Load { get; init; } = new()
        {
            Roots = [],
            Succeeded = true,
            IsCached = false,
            IsRefreshing = false,
        };

        public int RefreshCalls { get; private set; }

        public InventoryTreeLoadResult Current => Load;

        public Task<InventoryTreeLoadResult> RefreshAsync(CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            return Task.FromResult(Load);
        }
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

    private sealed class StubInventoryClient : IInventoryTreeClient
    {
        public Task<IReadOnlyList<Site>> ListAllSitesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Site>>([]);

        public Task<IReadOnlyList<Node>> ListAllNodesAsync(Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Node>>([]);

        public Task<NodeDetails> GetNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<NodeWorkflow> GetNodeWorkflowAsync(Guid nodeId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Site> CreateSiteAsync(string code, string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Node> CreateNodeAsync(
            Guid siteId,
            string name,
            NodeKind declaredKind,
            DeclaredUplinkMode declaredUplinkMode,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Device> RegisterDeviceAsync(
            Guid nodeId,
            string displayName,
            string managementHost,
            uint managementPort,
            DeviceRole role,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeviceConnectionSummary> UpdateDeviceConnectionAsync(
            Guid deviceId,
            string username,
            ReadOnlyMemory<byte> passwordUtf8,
            CertificateTrustMode trustMode,
            string? caProfileRef,
            Sha256? pinnedSpkiSha256,
            uint connectTimeoutMs,
            uint commandTimeoutMs,
            ulong maxResponseBytes,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ValidateDeviceConnectionResponse> ValidateDeviceConnectionAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ListNeighborCandidatesResponse> ListNeighborCandidatesAsync(
            Guid seedDeviceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VrrpPairConsistencyReport> ValidateVrrpPairConsistencyAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
