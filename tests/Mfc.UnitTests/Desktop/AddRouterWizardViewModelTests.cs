using System.Text;
using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>Add Router wizard: CreateSite → CreateNode → RegisterDevice → UpdateDeviceConnection.</summary>
public sealed class AddRouterWizardViewModelTests
{
    [Fact]
    public async Task SubmitCreatesSiteNodeDeviceAndConnectionThenClearsPassword()
    {
        RecordingInventoryClient client = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        AddRouterWizardViewModel wizard = new(client, connection, inventory)
        {
            UseExistingSite = false,
            UseExistingNode = false,
            NewSiteCode = "LAB01",
            NewSiteName = "Lab One",
            NewNodeName = "core",
            SelectedUplinkMode = DeclaredUplinkMode.One,
            DeviceDisplayName = "chr-1",
            ManagementHost = "192.0.2.10",
            ManagementPortText = "8729",
            Username = "admin",
            Password = "secret-password",
            SelectedTrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "lab-ca",
        };

        await wizard.SubmitCommand.ExecuteAsync(null);

        Assert.Null(wizard.ErrorText);
        Assert.Equal(string.Empty, wizard.Password);
        Assert.Equal(1, client.CreateSiteCalls);
        Assert.Equal(1, client.CreateNodeCalls);
        Assert.Equal(1, client.RegisterDeviceCalls);
        Assert.Equal(1, client.UpdateConnectionCalls);
        Assert.Equal("LAB01", client.LastSiteCode);
        Assert.Equal("core", client.LastNodeName);
        Assert.Equal("chr-1", client.LastDeviceName);
        Assert.Equal("admin", client.LastUsername);
        Assert.Equal(CertificateTrustMode.InternalCa, client.LastTrustMode);
        Assert.Equal("lab-ca", client.LastCaProfileRef);
        Assert.Contains("ready", wizard.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitUsesExistingSiteAndNodeWithoutCreateCalls()
    {
        Guid siteId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        RecordingInventoryClient client = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeService tree = new(new SeededTreeClient(siteId, nodeId));
        InventoryTreeViewModel inventory = new(tree, connection);
        await inventory.RefreshCommand.ExecuteAsync(null);

        AddRouterWizardViewModel wizard = new(client, connection, inventory)
        {
            UseExistingSite = true,
            UseExistingNode = true,
            SelectedSite = new InventoryPickerItem(siteId, "Lab"),
            SelectedNode = new InventoryPickerItem(nodeId, "core"),
            DeviceDisplayName = "member-a",
            ManagementHost = "192.0.2.11",
            ManagementPortText = "8729",
            Username = "readonly",
            Password = "pw",
            SelectedTrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "lab-ca",
        };

        await wizard.SubmitCommand.ExecuteAsync(null);

        Assert.Null(wizard.ErrorText);
        Assert.Equal(0, client.CreateSiteCalls);
        Assert.Equal(0, client.CreateNodeCalls);
        Assert.Equal(1, client.RegisterDeviceCalls);
        Assert.Equal(nodeId, client.LastRegisterNodeId);
    }

    [Fact]
    public async Task SubmitRejectsInvalidSpkiHex()
    {
        RecordingInventoryClient client = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        AddRouterWizardViewModel wizard = new(client, connection, new InventoryTreeViewModel(new EmptyTreeService(), connection))
        {
            UseExistingSite = false,
            UseExistingNode = false,
            NewSiteCode = "S1",
            NewSiteName = "Site",
            NewNodeName = "n1",
            DeviceDisplayName = "d1",
            ManagementHost = "192.0.2.1",
            ManagementPortText = "8729",
            Username = "u",
            Password = "p",
            SelectedTrustMode = CertificateTrustMode.SpkiPin,
            PinnedSpkiSha256Hex = "deadbeef",
        };

        await wizard.SubmitCommand.ExecuteAsync(null);

        Assert.NotNull(wizard.ErrorText);
        Assert.Contains("64", wizard.ErrorText, StringComparison.Ordinal);
        Assert.Equal(0, client.CreateSiteCalls);
        Assert.Equal(0, client.RegisterDeviceCalls);
    }

    private sealed class FakeConnection : IControllerConnectionService
    {
        public ControllerConnectionState State { get; set; } = ControllerConnectionState.Disconnected;

        public string? LastError => null;

        public Grpc.Net.Client.GrpcChannel? Channel => null;

        public event EventHandler? StateChanged;

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync() => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Raise() => StateChanged?.Invoke(this, EventArgs.Empty);
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

    private sealed class SeededTreeClient : IInventoryTreeClient
    {
        private readonly Guid _siteId;
        private readonly Guid _nodeId;

        public SeededTreeClient(Guid siteId, Guid nodeId)
        {
            _siteId = siteId;
            _nodeId = nodeId;
        }

        public Task<IReadOnlyList<Site>> ListAllSitesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Site>>(
            [
                new Site
                {
                    Id = ToUuid(_siteId),
                    Code = "LAB",
                    Name = "Lab",
                    Status = SiteStatus.Active,
                },
            ]);

        public Task<IReadOnlyList<Node>> ListAllNodesAsync(Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Node>>(
            [
                new Node
                {
                    Id = ToUuid(_nodeId),
                    SiteId = ToUuid(_siteId),
                    Name = "core",
                    DeclaredKind = NodeKind.Router,
                    DeclaredUplinkMode = DeclaredUplinkMode.One,
                    Status = NodeStatus.Active,
                },
            ]);

        public Task<NodeDetails> GetNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
            => Task.FromResult(new NodeDetails
            {
                Node = new Node
                {
                    Id = ToUuid(nodeId),
                    SiteId = ToUuid(_siteId),
                    Name = "core",
                    DeclaredKind = NodeKind.Router,
                    DeclaredUplinkMode = DeclaredUplinkMode.One,
                    Status = NodeStatus.Active,
                },
            });

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
    }

    private sealed class RecordingInventoryClient : IInventoryTreeClient
    {
        private readonly Guid _siteId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        private readonly Guid _nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private readonly Guid _deviceId = Guid.Parse("99999999-8888-7777-6666-555555555555");

        public int CreateSiteCalls { get; private set; }

        public int CreateNodeCalls { get; private set; }

        public int RegisterDeviceCalls { get; private set; }

        public int UpdateConnectionCalls { get; private set; }

        public string? LastSiteCode { get; private set; }

        public string? LastNodeName { get; private set; }

        public string? LastDeviceName { get; private set; }

        public Guid? LastRegisterNodeId { get; private set; }

        public string? LastUsername { get; private set; }

        public CertificateTrustMode LastTrustMode { get; private set; }

        public string? LastCaProfileRef { get; private set; }

        public Task<IReadOnlyList<Site>> ListAllSitesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Site>>([]);

        public Task<IReadOnlyList<Node>> ListAllNodesAsync(Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Node>>([]);

        public Task<NodeDetails> GetNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
            => Task.FromResult(new NodeDetails());

        public Task<Site> CreateSiteAsync(string code, string name, CancellationToken cancellationToken = default)
        {
            CreateSiteCalls++;
            LastSiteCode = code;
            return Task.FromResult(new Site
            {
                Id = ToUuid(_siteId),
                Code = code,
                Name = name,
                Status = SiteStatus.Draft,
            });
        }

        public Task<Node> CreateNodeAsync(
            Guid siteId,
            string name,
            NodeKind declaredKind,
            DeclaredUplinkMode declaredUplinkMode,
            CancellationToken cancellationToken = default)
        {
            CreateNodeCalls++;
            LastNodeName = name;
            return Task.FromResult(new Node
            {
                Id = ToUuid(_nodeId),
                SiteId = ToUuid(siteId),
                Name = name,
                DeclaredKind = declaredKind,
                DeclaredUplinkMode = declaredUplinkMode,
                Status = NodeStatus.Draft,
            });
        }

        public Task<Device> RegisterDeviceAsync(
            Guid nodeId,
            string displayName,
            string managementHost,
            uint managementPort,
            DeviceRole role,
            CancellationToken cancellationToken = default)
        {
            RegisterDeviceCalls++;
            LastDeviceName = displayName;
            LastRegisterNodeId = nodeId;
            return Task.FromResult(new Device
            {
                Id = ToUuid(_deviceId),
                NodeId = ToUuid(nodeId),
                DisplayName = displayName,
                ManagementHost = managementHost,
                ManagementPort = managementPort,
                Role = role,
            });
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
        {
            UpdateConnectionCalls++;
            LastUsername = username;
            LastTrustMode = trustMode;
            LastCaProfileRef = caProfileRef;
            _ = Encoding.UTF8.GetString(passwordUtf8.Span);
            return Task.FromResult(new DeviceConnectionSummary
            {
                DeviceId = ToUuid(deviceId),
                Username = username,
                TrustMode = trustMode,
            });
        }
    }

    private static Uuid ToUuid(Guid id)
        => new() { Value = ByteString.CopyFrom(id.ToByteArray(bigEndian: true)) };
}
