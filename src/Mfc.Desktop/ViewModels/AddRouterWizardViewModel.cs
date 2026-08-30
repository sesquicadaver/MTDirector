using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Inventory Add Router wizard: CreateSite → CreateNode → RegisterDevice → UpdateDeviceConnection,
/// plus ValidateDeviceConnection probe for a selected or last-registered device.
/// Contracts-only; password never retained after submit success.
/// </summary>
public sealed partial class AddRouterWizardViewModel : ObservableObject, IDisposable
{
    private const uint DefaultManagementPort = 8729;
    private const uint DefaultConnectTimeoutMs = 5_000;
    private const uint DefaultCommandTimeoutMs = 30_000;
    private const ulong DefaultMaxResponseBytes = 1_048_576;

    private readonly IInventoryTreeClient _client;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private bool _disposed;
    private bool _suppressSiteCascade;
    private Guid? _lastRegisteredDeviceId;

    public AddRouterWizardViewModel(
        IInventoryTreeClient client,
        IControllerConnectionService connection,
        InventoryTreeViewModel inventory)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        TrustModes =
        [
            CertificateTrustMode.InternalCa,
            CertificateTrustMode.SpkiPin,
        ];
        UplinkModes =
        [
            DeclaredUplinkMode.One,
            DeclaredUplinkMode.Failover,
            DeclaredUplinkMode.Balanced,
            DeclaredUplinkMode.Mixed,
            DeclaredUplinkMode.None,
        ];
        SelectedTrustMode = CertificateTrustMode.InternalCa;
        SelectedUplinkMode = DeclaredUplinkMode.One;
        ManagementPortText = DefaultManagementPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        CaProfileRef = "lab-ca";
        _connection.StateChanged += OnConnectionStateChanged;
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
        _inventory.Roots.CollectionChanged += OnInventoryRootsChanged;
        RebuildSiteChoices();
        if (SiteChoices.Count == 0)
        {
            UseExistingSite = false;
            UseExistingNode = false;
        }

        ApplySelectionDefaults();
    }

    public ObservableCollection<InventoryPickerItem> SiteChoices { get; } = [];

    public ObservableCollection<InventoryPickerItem> NodeChoices { get; } = [];

    public IReadOnlyList<CertificateTrustMode> TrustModes { get; }

    public IReadOnlyList<DeclaredUplinkMode> UplinkModes { get; }

    public ObservableCollection<NeighborCandidateItem> NeighborCandidates { get; } = [];

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);

    public bool HasNeighborCandidates => NeighborCandidates.Count > 0;

    public bool HasProbeResult => !string.IsNullOrWhiteSpace(ProbeResultText);

    public bool CanLoadNeighborsVisible => TryGetSeedDeviceId() is not null;

    public bool CanProbeVisible => TryGetProbeDeviceId() is not null;

    public bool ShowExistingSitePicker => UseExistingSite;

    public bool ShowNewSiteFields => !UseExistingSite;

    public bool ShowExistingNodePicker => UseExistingNode;

    public bool ShowNewNodeFields => !UseExistingNode;

    public bool ShowCaProfile => SelectedTrustMode == CertificateTrustMode.InternalCa;

    public bool ShowSpkiPin => SelectedTrustMode == CertificateTrustMode.SpkiPin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusText;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowExistingSitePicker))]
    [NotifyPropertyChangedFor(nameof(ShowNewSiteFields))]
    private bool _useExistingSite = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowExistingNodePicker))]
    [NotifyPropertyChangedFor(nameof(ShowNewNodeFields))]
    private bool _useExistingNode = true;

    [ObservableProperty]
    private InventoryPickerItem? _selectedSite;

    [ObservableProperty]
    private InventoryPickerItem? _selectedNode;

    [ObservableProperty]
    private string _newSiteCode = string.Empty;

    [ObservableProperty]
    private string _newSiteName = string.Empty;

    [ObservableProperty]
    private string _newNodeName = string.Empty;

    [ObservableProperty]
    private DeclaredUplinkMode _selectedUplinkMode;

    [ObservableProperty]
    private string _deviceDisplayName = string.Empty;

    [ObservableProperty]
    private string _managementHost = string.Empty;

    [ObservableProperty]
    private string _managementPortText = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCaProfile))]
    [NotifyPropertyChangedFor(nameof(ShowSpkiPin))]
    private CertificateTrustMode _selectedTrustMode;

    [ObservableProperty]
    private string _caProfileRef = string.Empty;

    [ObservableProperty]
    private string _pinnedSpkiSha256Hex = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProbeResult))]
    private string _probeResultText = string.Empty;

    [ObservableProperty]
    private NeighborCandidateItem? _selectedNeighborCandidate;

    [RelayCommand(CanExecute = nameof(CanLoadNeighbors))]
    private async Task LoadNeighborsAsync()
    {
        await RunBusyAsync(async ct =>
        {
            Guid? seedId = TryGetSeedDeviceId();
            if (seedId is null)
            {
                throw new InvalidOperationException(
                    "Select a registered Device in the inventory tree as the seed.");
            }

            ListNeighborCandidatesResponse response = await Task.Run(
                    async () => await _client.ListNeighborCandidatesAsync(seedId.Value, ct).ConfigureAwait(false),
                    ct)
                .ConfigureAwait(true);

            NeighborCandidates.Clear();
            SelectedNeighborCandidate = null;
            foreach (NeighborCandidate candidate in response.Candidates)
            {
                NeighborCandidates.Add(new NeighborCandidateItem(
                    candidate.Address,
                    candidate.SuggestedPort == 0 ? DefaultManagementPort : candidate.SuggestedPort,
                    string.IsNullOrWhiteSpace(candidate.Identity) ? null : candidate.Identity,
                    string.IsNullOrWhiteSpace(candidate.Platform) ? null : candidate.Platform,
                    string.IsNullOrWhiteSpace(candidate.MacAddress) ? null : candidate.MacAddress,
                    string.IsNullOrWhiteSpace(candidate.Version) ? null : candidate.Version,
                    string.IsNullOrWhiteSpace(candidate.Board) ? null : candidate.Board,
                    string.IsNullOrWhiteSpace(candidate.InterfaceName) ? null : candidate.InterfaceName));
            }

            OnPropertyChanged(nameof(HasNeighborCandidates));
            string seedLabel = string.IsNullOrWhiteSpace(response.SeedIdentity)
                ? seedId.Value.ToString("D")
                : response.SeedIdentity;
            StatusText = NeighborCandidates.Count == 0
                ? $"No MikroTik neighbors from seed '{seedLabel}'."
                : $"Loaded {NeighborCandidates.Count} MikroTik candidate(s) from seed '{seedLabel}'. Pick one to pre-fill host/port.";
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanApplyNeighbor))]
    private void ApplyNeighborCandidate()
    {
        if (SelectedNeighborCandidate is null)
        {
            return;
        }

        ManagementHost = SelectedNeighborCandidate.Address;
        ManagementPortText = SelectedNeighborCandidate.SuggestedPort.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(SelectedNeighborCandidate.Identity))
        {
            DeviceDisplayName = SelectedNeighborCandidate.Identity.Trim();
        }

        StatusText =
            $"Pre-filled from neighbor '{SelectedNeighborCandidate.DisplayText}'. Enter credentials and submit to register.";
    }

    [RelayCommand(CanExecute = nameof(CanProbe))]
    private async Task ProbeAsync()
    {
        await RunBusyAsync(async ct =>
        {
            Guid? deviceId = TryGetProbeDeviceId();
            if (deviceId is null)
            {
                throw new InvalidOperationException(
                    "Select a registered Device in the inventory tree, or register one first.");
            }

            ProbeResultText = string.Empty;
            ValidateDeviceConnectionResponse response = await Task.Run(
                    async () => await _client.ValidateDeviceConnectionAsync(deviceId.Value, ct).ConfigureAwait(false),
                    ct)
                .ConfigureAwait(true);

            string identity = string.IsNullOrWhiteSpace(response.ObservedIdentity)
                ? "—"
                : response.ObservedIdentity.Trim();
            ProbeResultText =
                $"identity: {identity} · support: {response.SupportState} · mutated: {response.RouterosMutated}";
            StatusText = $"Probe completed for {deviceId.Value:D}.";
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        await RunBusyAsync(async ct =>
        {
            string displayName = RequireTrimmed(DeviceDisplayName, "Device display name");
            string host = RequireTrimmed(ManagementHost, "Management host");
            uint port = ParsePort(ManagementPortText);
            string user = RequireTrimmed(Username, "Username");
            if (string.IsNullOrEmpty(Password))
            {
                throw new InvalidOperationException("Password is required.");
            }

            if (UseExistingSite)
            {
                if (SelectedSite is null)
                {
                    throw new InvalidOperationException("Select an existing Site or create a new one.");
                }
            }
            else
            {
                _ = RequireTrimmed(NewSiteCode, "Site code");
                _ = RequireTrimmed(NewSiteName, "Site name");
            }

            if (UseExistingNode)
            {
                if (SelectedNode is null)
                {
                    throw new InvalidOperationException("Select an existing Node or create a new one.");
                }
            }
            else
            {
                _ = RequireTrimmed(NewNodeName, "Node name");
            }

            (CertificateTrustMode trust, string? caRef, Sha256? pin) = BuildTrust();

            Guid siteId = await ResolveSiteIdAsync(ct).ConfigureAwait(true);
            Guid nodeId = await ResolveNodeIdAsync(siteId, ct).ConfigureAwait(true);

            byte[] passwordBytes = Encoding.UTF8.GetBytes(Password);
            try
            {
                Device device = await Task.Run(
                        async () => await _client.RegisterDeviceAsync(
                                nodeId,
                                displayName,
                                host,
                                port,
                                DeviceRole.Router,
                                ct)
                            .ConfigureAwait(false),
                        ct)
                    .ConfigureAwait(true);

                await Task.Run(
                        async () => await _client.UpdateDeviceConnectionAsync(
                                DesktopProtoUuid.ToGuid(device.Id),
                                user,
                                passwordBytes,
                                trust,
                                caRef,
                                pin,
                                DefaultConnectTimeoutMs,
                                DefaultCommandTimeoutMs,
                                DefaultMaxResponseBytes,
                                ct)
                            .ConfigureAwait(false),
                        ct)
                    .ConfigureAwait(true);

                Password = string.Empty;
                _lastRegisteredDeviceId = DesktopProtoUuid.ToGuid(device.Id);
                OnPropertyChanged(nameof(CanProbeVisible));
                ProbeCommand.NotifyCanExecuteChanged();
                StatusText =
                    $"Registered device '{displayName}' under node {nodeId:D}. Refreshing inventory…";
                if (_inventory.RefreshCommand.CanExecute(null))
                {
                    await _inventory.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
                }

                StatusText = $"Device '{displayName}' ready. Next: validate connection / capture snapshot.";
            }
            finally
            {
                CryptographicClear(passwordBytes);
            }
        }).ConfigureAwait(true);
    }

    private bool CanSubmit()
        => !IsBusy && _connection.State == ControllerConnectionState.Connected;

    private bool CanLoadNeighbors()
        => !IsBusy
           && _connection.State == ControllerConnectionState.Connected
           && TryGetSeedDeviceId() is not null;

    private bool CanProbe()
        => !IsBusy
           && _connection.State == ControllerConnectionState.Connected
           && TryGetProbeDeviceId() is not null;

    private bool CanApplyNeighbor()
        => !IsBusy && SelectedNeighborCandidate is not null;

    private Guid? TryGetSeedDeviceId()
    {
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        return selected?.Kind == InventoryTreeKind.Device ? selected.Id : null;
    }

    private Guid? TryGetProbeDeviceId()
        => TryGetSeedDeviceId() ?? _lastRegisteredDeviceId;

    partial void OnSelectedNeighborCandidateChanged(NeighborCandidateItem? value)
        => ApplyNeighborCandidateCommand.NotifyCanExecuteChanged();

    private async Task<Guid> ResolveSiteIdAsync(CancellationToken cancellationToken)
    {
        if (UseExistingSite)
        {
            if (SelectedSite is null)
            {
                throw new InvalidOperationException("Select an existing Site or create a new one.");
            }

            return SelectedSite.Id;
        }

        string code = RequireTrimmed(NewSiteCode, "Site code");
        string name = RequireTrimmed(NewSiteName, "Site name");
        Site site = await Task.Run(
                async () => await _client.CreateSiteAsync(code, name, cancellationToken).ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(true);
        return DesktopProtoUuid.ToGuid(site.Id);
    }

    private async Task<Guid> ResolveNodeIdAsync(Guid siteId, CancellationToken cancellationToken)
    {
        if (UseExistingNode)
        {
            if (SelectedNode is null)
            {
                throw new InvalidOperationException("Select an existing Node or create a new one.");
            }

            return SelectedNode.Id;
        }

        string name = RequireTrimmed(NewNodeName, "Node name");
        Node node = await Task.Run(
                async () => await _client.CreateNodeAsync(
                        siteId,
                        name,
                        NodeKind.Router,
                        SelectedUplinkMode,
                        cancellationToken)
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(true);
        return DesktopProtoUuid.ToGuid(node.Id);
    }

    private (CertificateTrustMode Trust, string? CaRef, Sha256? Pin) BuildTrust()
    {
        if (SelectedTrustMode == CertificateTrustMode.InternalCa)
        {
            string ca = RequireTrimmed(CaProfileRef, "CA profile ref");
            return (CertificateTrustMode.InternalCa, ca, null);
        }

        if (SelectedTrustMode == CertificateTrustMode.SpkiPin)
        {
            string hex = RequireTrimmed(PinnedSpkiSha256Hex, "Pinned SPKI SHA-256 hex");
            if (hex.Length != 64 || !hex.All(char.IsAsciiHexDigit))
            {
                throw new InvalidOperationException("Pinned SPKI must be exactly 64 hexadecimal characters.");
            }

            byte[] digest = Convert.FromHexString(hex);
            Sha256 pin = new() { Value = ByteString.CopyFrom(digest) };
            return (CertificateTrustMode.SpkiPin, null, pin);
        }

        throw new InvalidOperationException("Select INTERNAL_CA or SPKI_PIN trust mode.");
    }

    private async Task RunBusyAsync(Func<CancellationToken, Task> action)
    {
        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller first.";
            return;
        }

        IsBusy = true;
        SubmitCommand.NotifyCanExecuteChanged();
        LoadNeighborsCommand.NotifyCanExecuteChanged();
        ApplyNeighborCandidateCommand.NotifyCanExecuteChanged();
        ProbeCommand.NotifyCanExecuteChanged();
        ErrorText = null;
        StatusText = null;
        try
        {
            await action(CancellationToken.None).ConfigureAwait(true);
        }
        catch (RpcException ex)
        {
            ErrorText = ex.Status.Detail;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            SubmitCommand.NotifyCanExecuteChanged();
            LoadNeighborsCommand.NotifyCanExecuteChanged();
            ApplyNeighborCandidateCommand.NotifyCanExecuteChanged();
            ProbeCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            NotifySeedCommands();
        }
        else
        {
            Dispatcher.UIThread.Post(NotifySeedCommands);
        }
    }

    private void OnInventoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InventoryTreeViewModel.SelectedNode)
            or nameof(InventoryTreeViewModel.IsRefreshing))
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                HandleInventoryChanged();
            }
            else
            {
                Dispatcher.UIThread.Post(HandleInventoryChanged);
            }
        }
    }

    private void OnInventoryRootsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RebuildSiteChoices();
            ApplySelectionDefaults();
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                RebuildSiteChoices();
                ApplySelectionDefaults();
            });
        }
    }

    private void HandleInventoryChanged()
    {
        if (!_inventory.IsRefreshing)
        {
            RebuildSiteChoices();
            ApplySelectionDefaults();
        }

        OnPropertyChanged(nameof(CanLoadNeighborsVisible));
        OnPropertyChanged(nameof(CanProbeVisible));
        NotifySeedCommands();
    }

    private void NotifySeedCommands()
    {
        SubmitCommand.NotifyCanExecuteChanged();
        LoadNeighborsCommand.NotifyCanExecuteChanged();
        ApplyNeighborCandidateCommand.NotifyCanExecuteChanged();
        ProbeCommand.NotifyCanExecuteChanged();
    }

    private void RebuildSiteChoices()
    {
        Guid? keepSite = SelectedSite?.Id;
        Guid? keepNode = SelectedNode?.Id;
        SiteChoices.Clear();
        foreach (InventoryNodeViewModel site in _inventory.Roots.Where(static r => r.Kind == InventoryTreeKind.Site))
        {
            SiteChoices.Add(new InventoryPickerItem(site.Id, site.DisplayName));
        }

        _suppressSiteCascade = true;
        try
        {
            SelectedSite = keepSite is Guid id
                ? SiteChoices.FirstOrDefault(s => s.Id == id)
                : SiteChoices.FirstOrDefault();
            RebuildNodeChoices(keepNode);
        }
        finally
        {
            _suppressSiteCascade = false;
        }
    }

    private void RebuildNodeChoices(Guid? preferredNodeId = null)
    {
        NodeChoices.Clear();
        if (SelectedSite is null)
        {
            SelectedNode = null;
            return;
        }

        InventoryNodeViewModel? siteVm = _inventory.Roots.FirstOrDefault(r => r.Id == SelectedSite.Id);
        if (siteVm is null)
        {
            SelectedNode = null;
            return;
        }

        foreach (InventoryNodeViewModel node in siteVm.Children.Where(static c => c.Kind == InventoryTreeKind.Node))
        {
            NodeChoices.Add(new InventoryPickerItem(node.Id, node.DisplayName));
        }

        SelectedNode = preferredNodeId is Guid id
            ? NodeChoices.FirstOrDefault(n => n.Id == id) ?? NodeChoices.FirstOrDefault()
            : NodeChoices.FirstOrDefault();
    }

    private void ApplySelectionDefaults()
    {
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        if (selected is null)
        {
            return;
        }

        if (selected.Kind == InventoryTreeKind.Site)
        {
            UseExistingSite = true;
            SelectedSite = SiteChoices.FirstOrDefault(s => s.Id == selected.Id);
            UseExistingNode = NodeChoices.Count > 0;
            return;
        }

        if (selected.Kind == InventoryTreeKind.Node && selected.ParentId is Guid siteId)
        {
            UseExistingSite = true;
            SelectedSite = SiteChoices.FirstOrDefault(s => s.Id == siteId);
            UseExistingNode = true;
            SelectedNode = NodeChoices.FirstOrDefault(n => n.Id == selected.Id);
            if (string.IsNullOrWhiteSpace(DeviceDisplayName))
            {
                DeviceDisplayName = selected.DisplayName;
            }

            return;
        }

        if (selected.Kind == InventoryTreeKind.Device && selected.ParentId is Guid nodeId)
        {
            InventoryNodeViewModel? nodeVm = FindNode(nodeId);
            if (nodeVm?.ParentId is Guid parentSite)
            {
                UseExistingSite = true;
                SelectedSite = SiteChoices.FirstOrDefault(s => s.Id == parentSite);
                UseExistingNode = true;
                SelectedNode = NodeChoices.FirstOrDefault(n => n.Id == nodeId);
            }
        }
    }

    private InventoryNodeViewModel? FindNode(Guid nodeId)
    {
        foreach (InventoryNodeViewModel site in _inventory.Roots)
        {
            InventoryNodeViewModel? node = site.Children.FirstOrDefault(c => c.Id == nodeId);
            if (node is not null)
            {
                return node;
            }
        }

        return null;
    }

    partial void OnSelectedSiteChanged(InventoryPickerItem? value)
    {
        if (_suppressSiteCascade)
        {
            return;
        }

        RebuildNodeChoices();
    }

    private static string RequireTrimmed(string? value, string fieldName)
    {
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return trimmed;
    }

    private static uint ParsePort(string? text)
    {
        if (!uint.TryParse(
                (text ?? string.Empty).Trim(),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out uint port)
            || port is 0 or > 65535)
        {
            throw new InvalidOperationException("Management port must be an integer 1–65535.");
        }

        return port;
    }

    private static void CryptographicClear(byte[] buffer)
        => CryptographicOperations.ZeroMemory(buffer);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.StateChanged -= OnConnectionStateChanged;
        _inventory.PropertyChanged -= OnInventoryPropertyChanged;
        _inventory.Roots.CollectionChanged -= OnInventoryRootsChanged;
    }
}
