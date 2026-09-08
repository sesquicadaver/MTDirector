using Google.Protobuf;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-NBR-01 / W7-92: Desktop Neighbor candidates Living Spec depth vs ListNeighborCandidates.</summary>
public sealed class DesktopNeighborLivingSpecTests
{
    [Fact]
    public void Ac1WireAndDesktopClientExposeListNeighborCandidates()
    {
        Assert.Contains(
            InventoryService.Descriptor.Methods,
            static m => m.Name == "ListNeighborCandidates");
        Assert.NotNull(typeof(IInventoryTreeClient).GetMethod(nameof(IInventoryTreeClient.ListNeighborCandidatesAsync)));
        string source = ReadSource("src/Mfc.Desktop/Services/GrpcInventoryTreeClient.cs");
        Assert.Contains("ListNeighborCandidatesAsync", source, StringComparison.Ordinal);
        Assert.Contains("ListNeighborCandidatesRequest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2WizardExposesLoadApplyNeighborCommandsAndCandidates()
    {
        FakeConnection connection = new(ControllerConnectionState.Connected);
        using InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        using AddRouterWizardViewModel wizard = new(new StubInventoryClient(), connection, inventory);

        Assert.NotNull(wizard.GetType().GetProperty(nameof(AddRouterWizardViewModel.LoadNeighborsCommand)));
        Assert.NotNull(wizard.GetType().GetProperty(nameof(AddRouterWizardViewModel.ApplyNeighborCandidateCommand)));
        Assert.NotNull(wizard.GetType().GetProperty(nameof(AddRouterWizardViewModel.NeighborCandidates)));
        Assert.NotNull(wizard.GetType().GetProperty(nameof(AddRouterWizardViewModel.SelectedNeighborCandidate)));
        Assert.NotNull(wizard.GetType().GetProperty(nameof(AddRouterWizardViewModel.HasNeighborCandidates)));
    }

    [Fact]
    public async Task Ac3LoadNeighborsFillsCandidatesFromSeedDeviceWithoutRegister()
    {
        Guid deviceId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        StubInventoryClient client = new()
        {
            NeighborResponse = new ListNeighborCandidatesResponse
            {
                SeedIdentity = "chr-seed",
            },
        };
        client.NeighborResponse.Candidates.Add(new NeighborCandidate
        {
            Address = "203.0.113.5",
            SuggestedPort = 8729,
            Identity = "peer-1",
            Platform = "MikroTik",
        });

        FakeConnection connection = new(ControllerConnectionState.Connected);
        using InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        inventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = deviceId,
            DisplayName = "chr-seed",
        });
        using AddRouterWizardViewModel wizard = new(client, connection, inventory);

        await wizard.LoadNeighborsCommand.ExecuteAsync(null);

        Assert.Null(wizard.ErrorText);
        Assert.Equal(1, client.ListNeighborCalls);
        Assert.Equal(0, client.RegisterCalls);
        NeighborCandidateItem item = Assert.Single(wizard.NeighborCandidates);
        Assert.Equal("203.0.113.5", item.Address);
        Assert.Contains("Loaded", wizard.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac4LoadNeighborsRequiresSeedDeviceAndConnectedController()
    {
        StubInventoryClient disconnectedClient = new();
        FakeConnection disconnected = new(ControllerConnectionState.Disconnected);
        using InventoryTreeViewModel disconnectedInventory = new(new EmptyTreeService(), disconnected);
        disconnectedInventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = Guid.Parse("99999999-8888-7777-6666-555555555555"),
            DisplayName = "chr-seed",
        });
        using AddRouterWizardViewModel disconnectedWizard = new(disconnectedClient, disconnected, disconnectedInventory);
        Assert.False(disconnectedWizard.LoadNeighborsCommand.CanExecute(null));

        StubInventoryClient noSeedClient = new();
        FakeConnection connected = new(ControllerConnectionState.Connected);
        using InventoryTreeViewModel noSeedInventory = new(new EmptyTreeService(), connected);
        using AddRouterWizardViewModel noSeedWizard = new(noSeedClient, connected, noSeedInventory);
        Assert.False(noSeedWizard.LoadNeighborsCommand.CanExecute(null));
        Assert.False(noSeedWizard.ApplyNeighborCandidateCommand.CanExecute(null));
        Assert.Equal(0, noSeedClient.ListNeighborCalls);
        Assert.Equal(0, disconnectedClient.ListNeighborCalls);
    }

    [Fact]
    public void Ac5ApplyNeighborPrefillsHostWithoutRegisterAndMainWindowBinds()
    {
        StubInventoryClient client = new();
        FakeConnection connection = new(ControllerConnectionState.Connected);
        using InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        using AddRouterWizardViewModel wizard = new(client, connection, inventory);

        NeighborCandidateItem candidate = new(
            "198.51.100.20",
            8729,
            "peer-a",
            "MikroTik",
            "AA:BB:CC:DD:EE:01",
            "7.16",
            "CHR",
            "ether1");
        wizard.NeighborCandidates.Add(candidate);
        wizard.SelectedNeighborCandidate = candidate;
        Assert.True(wizard.ApplyNeighborCandidateCommand.CanExecute(null));
        wizard.ApplyNeighborCandidateCommand.Execute(null);

        Assert.Equal("198.51.100.20", wizard.ManagementHost);
        Assert.Equal("8729", wizard.ManagementPortText);
        Assert.Equal("peer-a", wizard.DeviceDisplayName);
        Assert.Equal(0, client.RegisterCalls);
        Assert.Equal(0, client.ListNeighborCalls);

        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("AddRouter.LoadNeighborsCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.ApplyNeighborCandidateCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.NeighborCandidates", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.SelectedNeighborCandidate", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.HasNeighborCandidates", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostAndProtoContractRemainPresentForNeighborCandidates()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/InventoryGrpcHostTests.cs")));
        string proto = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/Protos/mfc/v1/inventory.proto"));
        Assert.Contains("ListNeighborCandidates", proto, StringComparison.Ordinal);
        string contract = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Contracts/InventoryProtoContractTests.cs"));
        Assert.Contains("ListNeighborCandidates", contract, StringComparison.Ordinal);
        string living = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Inventory/NeighborCandidatesLivingSpecTests.cs"));
        Assert.Contains("ListNeighborCandidates", living, StringComparison.Ordinal);
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

    private sealed class StubInventoryClient : IInventoryTreeClient
    {
        public ListNeighborCandidatesResponse NeighborResponse { get; init; } = new();

        public int ListNeighborCalls { get; private set; }

        public int RegisterCalls { get; private set; }

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
        {
            RegisterCalls++;
            return Task.FromResult(new Device());
        }

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
        {
            ListNeighborCalls++;
            return Task.FromResult(NeighborResponse);
        }

        public Task<VrrpPairConsistencyReport> ValidateVrrpPairConsistencyAsync(
            Guid nodeId,
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
