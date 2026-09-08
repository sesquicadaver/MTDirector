using Google.Protobuf;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-PROBE-01 / W7-94: Desktop ValidateDeviceConnection probe Living Spec depth.</summary>
public sealed class DesktopProbeLivingSpecTests
{
    [Fact]
    public void Ac1WireAndDesktopClientExposeValidateDeviceConnection()
    {
        Assert.Contains(
            InventoryService.Descriptor.Methods,
            static m => m.Name == "ValidateDeviceConnection");
        Assert.NotNull(typeof(IInventoryTreeClient).GetMethod(nameof(IInventoryTreeClient.ValidateDeviceConnectionAsync)));
        string source = ReadSource("src/Mfc.Desktop/Services/GrpcInventoryTreeClient.cs");
        Assert.Contains("ValidateDeviceConnectionAsync", source, StringComparison.Ordinal);
        Assert.Contains("ValidateDeviceConnectionRequest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2WizardExposesProbeCommandAndResultSurface()
    {
        FakeConnection connection = new(ControllerConnectionState.Connected);
        using InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        using AddRouterWizardViewModel wizard = new(new StubInventoryClient(), connection, inventory);

        Assert.NotNull(wizard.GetType().GetProperty(nameof(AddRouterWizardViewModel.ProbeCommand)));
        Assert.NotNull(wizard.GetType().GetProperty(nameof(AddRouterWizardViewModel.ProbeResultText)));
        Assert.NotNull(wizard.GetType().GetProperty(nameof(AddRouterWizardViewModel.HasProbeResult)));
        Assert.NotNull(wizard.GetType().GetProperty(nameof(AddRouterWizardViewModel.CanProbeVisible)));
    }

    [Fact]
    public async Task Ac3ProbeShowsIdentitySupportAndMutatedWithoutRegister()
    {
        Guid deviceId = Guid.Parse("dddddddd-eeee-ffff-aaaa-bbbbbbbbbbbb");
        StubInventoryClient client = new()
        {
            ValidateResponse = new ValidateDeviceConnectionResponse
            {
                ObservedIdentity = "CHR-LAB",
                SupportState = SupportState.Supported,
                RouterosMutated = false,
            },
        };

        FakeConnection connection = new(ControllerConnectionState.Connected);
        using InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        inventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = deviceId,
            DisplayName = "chr-seed",
        });
        using AddRouterWizardViewModel wizard = new(client, connection, inventory);

        Assert.True(wizard.ProbeCommand.CanExecute(null));
        await wizard.ProbeCommand.ExecuteAsync(null);

        Assert.Null(wizard.ErrorText);
        Assert.Equal(1, client.ValidateCalls);
        Assert.Equal(0, client.RegisterCalls);
        Assert.Contains("CHR-LAB", wizard.ProbeResultText, StringComparison.Ordinal);
        Assert.Contains("Supported", wizard.ProbeResultText, StringComparison.Ordinal);
        Assert.Contains("mutated: False", wizard.ProbeResultText, StringComparison.Ordinal);
        Assert.True(wizard.HasProbeResult);
    }

    [Fact]
    public void Ac4ProbeRequiresDeviceAndConnectedController()
    {
        StubInventoryClient disconnectedClient = new();
        FakeConnection disconnected = new(ControllerConnectionState.Disconnected);
        using InventoryTreeViewModel disconnectedInventory = new(new EmptyTreeService(), disconnected);
        disconnectedInventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = Guid.Parse("dddddddd-eeee-ffff-aaaa-bbbbbbbbbbbb"),
            DisplayName = "chr-seed",
        });
        using AddRouterWizardViewModel disconnectedWizard = new(disconnectedClient, disconnected, disconnectedInventory);
        Assert.False(disconnectedWizard.ProbeCommand.CanExecute(null));

        StubInventoryClient noDeviceClient = new();
        FakeConnection connected = new(ControllerConnectionState.Connected);
        using InventoryTreeViewModel noDeviceInventory = new(new EmptyTreeService(), connected);
        using AddRouterWizardViewModel noDeviceWizard = new(noDeviceClient, connected, noDeviceInventory);
        Assert.False(noDeviceWizard.ProbeCommand.CanExecute(null));
        Assert.Equal(0, noDeviceClient.ValidateCalls);
        Assert.Equal(0, disconnectedClient.ValidateCalls);
    }

    [Fact]
    public void Ac5MainWindowBindsProbeCommandAndResult()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("AddRouter.ProbeCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.ProbeResultText", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.HasProbeResult", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.CanProbeVisible", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostAndProtoContractRemainPresentForValidateDeviceConnection()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/InventoryGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/InventoryGrpcHostTests.cs"));
        Assert.Contains("InventoryLifecycleListGetRegisterValidateAndIdempotency", host, StringComparison.Ordinal);
        string proto = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/Protos/mfc/v1/inventory.proto"));
        Assert.Contains("ValidateDeviceConnection", proto, StringComparison.Ordinal);
        string contract = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Contracts/InventoryProtoContractTests.cs"));
        Assert.Contains("ValidateDeviceConnection", contract, StringComparison.Ordinal);
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
        public ValidateDeviceConnectionResponse ValidateResponse { get; init; } = new();

        public int ValidateCalls { get; private set; }

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
        {
            ValidateCalls++;
            return Task.FromResult(ValidateResponse);
        }

        public Task<ListNeighborCandidatesResponse> ListNeighborCandidatesAsync(
            Guid seedDeviceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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
