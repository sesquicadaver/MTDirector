using System.Text;
using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>Add Router wizard: CreateSite → CreateNode → RegisterDevice → UpdateDeviceConnection + Probe + W4.3 VRRP pair + CONT-02 neighbor member b.</summary>
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
        Assert.Equal(NodeKind.Router, client.LastDeclaredKind);
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

    [Fact]
    public void ApplyNeighborCandidatePrefillsHostPortAndDisplayNameWithoutRegister()
    {
        RecordingInventoryClient client = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        AddRouterWizardViewModel wizard = new(client, connection, new InventoryTreeViewModel(new EmptyTreeService(), connection));

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
        wizard.ApplyNeighborCandidateCommand.Execute(null);

        Assert.Equal("198.51.100.20", wizard.ManagementHost);
        Assert.Equal("8729", wizard.ManagementPortText);
        Assert.Equal("peer-a", wizard.DeviceDisplayName);
        Assert.Equal(string.Empty, wizard.PairMemberBManagementHost);
        Assert.Equal(0, client.RegisterDeviceCalls);
        Assert.Equal(0, client.ListNeighborCalls);
    }

    [Fact]
    public void ApplyNeighborWhenVrrpPairFirstFillsMemberAThenMemberBWithoutRegister()
    {
        RecordingInventoryClient client = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        AddRouterWizardViewModel wizard = new(client, connection, new InventoryTreeViewModel(new EmptyTreeService(), connection))
        {
            UseExistingSite = false,
            CreateAsVrrpPair = true,
        };

        NeighborCandidateItem memberA = new(
            "198.51.100.20",
            8729,
            "peer-a",
            "MikroTik",
            "AA:BB:CC:DD:EE:01",
            "7.16",
            "CHR",
            "ether1");
        NeighborCandidateItem memberB = new(
            "198.51.100.21",
            8730,
            "peer-b",
            "MikroTik",
            "AA:BB:CC:DD:EE:02",
            "7.16",
            "CHR",
            "ether1");
        wizard.NeighborCandidates.Add(memberA);
        wizard.NeighborCandidates.Add(memberB);

        Assert.True(wizard.ShowVrrpPairFields);
        wizard.SelectedNeighborCandidate = memberA;
        wizard.ApplyNeighborCandidateCommand.Execute(null);

        Assert.Equal("198.51.100.20", wizard.ManagementHost);
        Assert.Equal("8729", wizard.ManagementPortText);
        Assert.Equal("peer-a", wizard.DeviceDisplayName);
        Assert.Equal(string.Empty, wizard.PairMemberBManagementHost);

        wizard.SelectedNeighborCandidate = memberB;
        wizard.ApplyNeighborCandidateCommand.Execute(null);

        Assert.Equal("198.51.100.20", wizard.ManagementHost);
        Assert.Equal("peer-a", wizard.DeviceDisplayName);
        Assert.Equal("198.51.100.21", wizard.PairMemberBManagementHost);
        Assert.Equal("8730", wizard.PairMemberBManagementPortText);
        Assert.Equal("peer-b", wizard.PairMemberBDisplayName);
        Assert.Contains("member b", wizard.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, client.RegisterDeviceCalls);
        Assert.DoesNotContain("Master", wizard.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadNeighborsUsesSeedDeviceAndDoesNotRegister()
    {
        Guid siteId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceId = Guid.Parse("dddddddd-eeee-ffff-aaaa-bbbbbbbbbbbb");

        RecordingInventoryClient client = new();
        client.NeighborResponse.Candidates.Add(new NeighborCandidate
        {
            Address = "203.0.113.5",
            SuggestedPort = 8729,
            Identity = "edge-b",
            Platform = "MikroTik",
        });

        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeService tree = new(new SeededTreeClientWithDevice(siteId, nodeId, deviceId));
        InventoryTreeViewModel inventory = new(tree, connection);
        await inventory.RefreshCommand.ExecuteAsync(null);
        InventoryNodeViewModel deviceNode = inventory.Roots
            .SelectMany(s => s.Children)
            .SelectMany(n => n.Children)
            .Single(d => d.Kind == InventoryTreeKind.Device);
        inventory.SelectedNode = deviceNode;

        AddRouterWizardViewModel wizard = new(client, connection, inventory);
        await wizard.LoadNeighborsCommand.ExecuteAsync(null);

        Assert.Null(wizard.ErrorText);
        Assert.Equal(1, client.ListNeighborCalls);
        Assert.Equal(deviceId, client.LastNeighborSeedId);
        Assert.Equal(0, client.RegisterDeviceCalls);
        Assert.Single(wizard.NeighborCandidates);
        Assert.Equal("203.0.113.5", wizard.NeighborCandidates[0].Address);
    }

    [Fact]
    public async Task ProbeUsesSelectedDeviceAndShowsIdentitySupportAndMutated()
    {
        Guid siteId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceId = Guid.Parse("dddddddd-eeee-ffff-aaaa-bbbbbbbbbbbb");

        RecordingInventoryClient client = new()
        {
            ValidateResponse = new ValidateDeviceConnectionResponse
            {
                DeviceId = ToUuid(deviceId),
                ObservedIdentity = "CHR-LAB",
                SupportState = SupportState.Supported,
                RouterosMutated = false,
            },
        };
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        CountingTreeService tree = new(new InventoryTreeService(new SeededTreeClientWithDevice(siteId, nodeId, deviceId)));
        InventoryTreeViewModel inventory = new(tree, connection);
        await inventory.RefreshCommand.ExecuteAsync(null);
        inventory.SelectedNode = inventory.Roots
            .SelectMany(s => s.Children)
            .SelectMany(n => n.Children)
            .Single(d => d.Kind == InventoryTreeKind.Device);

        AddRouterWizardViewModel wizard = new(client, connection, inventory);
        Assert.True(wizard.ProbeCommand.CanExecute(null));
        await wizard.ProbeCommand.ExecuteAsync(null);

        Assert.Null(wizard.ErrorText);
        Assert.Equal(1, client.ValidateCalls);
        Assert.Equal(deviceId, client.LastValidateDeviceId);
        Assert.Equal(0, client.RegisterDeviceCalls);
        Assert.Contains("CHR-LAB", wizard.ProbeResultText, StringComparison.Ordinal);
        Assert.Contains("Supported", wizard.ProbeResultText, StringComparison.Ordinal);
        Assert.Contains("mutated: False", wizard.ProbeResultText, StringComparison.Ordinal);
        Assert.True(wizard.HasProbeResult);
        Assert.True(tree.RefreshCount >= 2); // initial load + post-probe refresh (W6-05)
    }

    [Fact]
    public async Task ProbeAfterSubmitUsesLastRegisteredDevice()
    {
        RecordingInventoryClient client = new()
        {
            ValidateResponse = new ValidateDeviceConnectionResponse
            {
                ObservedIdentity = "chr-1",
                SupportState = SupportState.Supported,
                RouterosMutated = false,
            },
        };
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        AddRouterWizardViewModel wizard = new(client, connection, inventory)
        {
            UseExistingSite = false,
            UseExistingNode = false,
            NewSiteCode = "LAB01",
            NewSiteName = "Lab One",
            NewNodeName = "core",
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
        Assert.True(wizard.CanProbeVisible);

        await wizard.ProbeCommand.ExecuteAsync(null);

        Assert.Null(wizard.ErrorText);
        Assert.Equal(1, client.ValidateCalls);
        Assert.Equal(client.RegisteredDeviceId, client.LastValidateDeviceId);
        Assert.Contains("chr-1", wizard.ProbeResultText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitCreatesVrrpNodeAndRegistersTwoDevices()
    {
        RecordingInventoryClient client = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        AddRouterWizardViewModel wizard = new(client, connection, inventory)
        {
            UseExistingSite = false,
            UseExistingNode = false,
            CreateAsVrrpPair = true,
            NewSiteCode = "LAB01",
            NewSiteName = "Lab One",
            NewNodeName = "edge-pair",
            SelectedUplinkMode = DeclaredUplinkMode.Failover,
            DeviceDisplayName = "r1",
            ManagementHost = "192.0.2.1",
            ManagementPortText = "8729",
            PairMemberBDisplayName = "r2",
            PairMemberBManagementHost = "192.0.2.2",
            PairMemberBManagementPortText = "8729",
            Username = "admin",
            Password = "secret-password",
            SelectedTrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "lab-ca",
        };

        Assert.True(wizard.ShowVrrpPairFields);
        Assert.False(wizard.UseExistingNode);
        Assert.Contains("members a and b", wizard.VrrpPairHint, StringComparison.Ordinal);
        Assert.DoesNotContain("Master", wizard.VrrpPairHint, StringComparison.OrdinalIgnoreCase);

        await wizard.SubmitCommand.ExecuteAsync(null);

        Assert.Null(wizard.ErrorText);
        Assert.Equal(string.Empty, wizard.Password);
        Assert.Equal(1, client.CreateNodeCalls);
        Assert.Equal(NodeKind.Vrrp, client.LastDeclaredKind);
        Assert.Equal(DeclaredUplinkMode.Failover, client.LastUplinkMode);
        Assert.Equal(2, client.RegisterDeviceCalls);
        Assert.Equal(2, client.UpdateConnectionCalls);
        Assert.Equal(["r1", "r2"], client.RegisteredDeviceNames);
        Assert.Equal(["192.0.2.1", "192.0.2.2"], client.RegisteredHosts);
        Assert.Contains("VRRP pair", wizard.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("Master", wizard.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitVrrpPairRejectsIdenticalManagementHosts()
    {
        RecordingInventoryClient client = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        AddRouterWizardViewModel wizard = new(client, connection, inventory)
        {
            UseExistingSite = false,
            UseExistingNode = false,
            CreateAsVrrpPair = true,
            NewSiteCode = "LAB01",
            NewSiteName = "Lab One",
            NewNodeName = "edge-pair",
            DeviceDisplayName = "r1",
            ManagementHost = "192.0.2.1",
            ManagementPortText = "8729",
            PairMemberBDisplayName = "r2",
            PairMemberBManagementHost = "192.0.2.1",
            PairMemberBManagementPortText = "8729",
            Username = "admin",
            Password = "pw",
            SelectedTrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "lab-ca",
        };

        await wizard.SubmitCommand.ExecuteAsync(null);

        Assert.NotNull(wizard.ErrorText);
        Assert.Contains("distinct management hosts", wizard.ErrorText, StringComparison.Ordinal);
        Assert.Equal(0, client.CreateNodeCalls);
        Assert.Equal(0, client.RegisterDeviceCalls);
    }

    [Fact]
    public void CreateAsVrrpPairForcesNewNode()
    {
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        AddRouterWizardViewModel wizard = new(
            new RecordingInventoryClient(),
            connection,
            new InventoryTreeViewModel(new EmptyTreeService(), connection))
        {
            UseExistingNode = true,
        };

        wizard.CreateAsVrrpPair = true;

        Assert.False(wizard.UseExistingNode);
        Assert.True(wizard.ShowVrrpPairFields);
    }

    [Fact]
    public void ProbeCommandDisabledWhenDisconnected()
    {
        RecordingInventoryClient client = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Disconnected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        inventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = Guid.Parse("dddddddd-eeee-ffff-aaaa-bbbbbbbbbbbb"),
            DisplayName = "seed",
        });

        AddRouterWizardViewModel wizard = new(client, connection, inventory);
        Assert.False(wizard.ProbeCommand.CanExecute(null));
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

    private sealed class CountingTreeService : IInventoryTreeService
    {
        private readonly IInventoryTreeService _inner;

        public CountingTreeService(IInventoryTreeService inner) => _inner = inner;

        public int RefreshCount { get; private set; }

        public InventoryTreeLoadResult Current => _inner.Current;

        public async Task<InventoryTreeLoadResult> RefreshAsync(CancellationToken cancellationToken = default)
        {
            RefreshCount++;
            return await _inner.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
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

        public Task<NodeWorkflow> GetNodeWorkflowAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
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

        public Task<ListNeighborCandidatesResponse> ListNeighborCandidatesAsync(
            Guid seedDeviceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ValidateDeviceConnectionResponse> ValidateDeviceConnectionAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VrrpPairConsistencyReport> ValidateVrrpPairConsistencyAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class SeededTreeClientWithDevice : IInventoryTreeClient
    {
        private readonly Guid _siteId;
        private readonly Guid _nodeId;
        private readonly Guid _deviceId;

        public SeededTreeClientWithDevice(Guid siteId, Guid nodeId, Guid deviceId)
        {
            _siteId = siteId;
            _nodeId = nodeId;
            _deviceId = deviceId;
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
                Devices =
                {
                    new Device
                    {
                        Id = ToUuid(_deviceId),
                        NodeId = ToUuid(_nodeId),
                        DisplayName = "seed",
                        ManagementHost = "192.0.2.1",
                        ManagementPort = 8729,
                        Role = DeviceRole.Router,
                    },
                },
            });

        public Task<NodeWorkflow> GetNodeWorkflowAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
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

        public Task<ListNeighborCandidatesResponse> ListNeighborCandidatesAsync(
            Guid seedDeviceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ValidateDeviceConnectionResponse> ValidateDeviceConnectionAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VrrpPairConsistencyReport> ValidateVrrpPairConsistencyAsync(
            Guid nodeId,
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

        public int ListNeighborCalls { get; private set; }

        public int ValidateCalls { get; private set; }

        public Guid? LastNeighborSeedId { get; private set; }

        public Guid? LastValidateDeviceId { get; private set; }

        public Guid RegisteredDeviceId => _deviceId;

        public ValidateDeviceConnectionResponse ValidateResponse { get; set; } = new()
        {
            ObservedIdentity = "seed-chr",
            SupportState = SupportState.Supported,
            RouterosMutated = false,
        };

        public ListNeighborCandidatesResponse NeighborResponse { get; set; } = new()
        {
            SeedIdentity = "seed-chr",
            RouterosMutated = false,
        };

        public string? LastSiteCode { get; private set; }

        public string? LastNodeName { get; private set; }

        public string? LastDeviceName { get; private set; }

        public NodeKind LastDeclaredKind { get; private set; }

        public DeclaredUplinkMode LastUplinkMode { get; private set; }

        public List<string> RegisteredDeviceNames { get; } = [];

        public List<string> RegisteredHosts { get; } = [];

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

        public Task<NodeWorkflow> GetNodeWorkflowAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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
            LastDeclaredKind = declaredKind;
            LastUplinkMode = declaredUplinkMode;
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
            RegisteredDeviceNames.Add(displayName);
            RegisteredHosts.Add(managementHost);
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

        public Task<ListNeighborCandidatesResponse> ListNeighborCandidatesAsync(
            Guid seedDeviceId,
            CancellationToken cancellationToken = default)
        {
            ListNeighborCalls++;
            LastNeighborSeedId = seedDeviceId;
            NeighborResponse.SeedDeviceId = ToUuid(seedDeviceId);
            return Task.FromResult(NeighborResponse);
        }

        public Task<ValidateDeviceConnectionResponse> ValidateDeviceConnectionAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            ValidateCalls++;
            LastValidateDeviceId = deviceId;
            ValidateResponse.DeviceId = ToUuid(deviceId);
            return Task.FromResult(ValidateResponse);
        }

        public Task<VrrpPairConsistencyReport> ValidateVrrpPairConsistencyAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static Uuid ToUuid(Guid id)
        => new() { Value = ByteString.CopyFrom(id.ToByteArray(bigEndian: true)) };
}
