using System.Collections.ObjectModel;
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
/// Onboarding operator panel: checklist, placement preview, recovery facts.
/// No script source and no free-form RouterOS write controls.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject, IDisposable
{
    private readonly IOnboardingServiceClient _client;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private bool _disposed;

    public OnboardingViewModel(
        IOnboardingServiceClient client,
        IControllerConnectionService connection,
        InventoryTreeViewModel inventory)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _connection.StateChanged += OnConnectionStateChanged;
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
    }

    public ObservableCollection<OnboardingFindingListItem> Findings { get; } = [];

    public ObservableCollection<OnboardingPlacementListItem> Placements { get; } = [];

    public ObservableCollection<string> ProgressLines { get; } = [];

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasScriptSource
    {
        get
        {
            _ = _client;
            return false;
        }
    }

    public bool HasArbitraryWriteControls
    {
        get
        {
            _ = _inventory;
            return false;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Select a Node, then validate prerequisites.";

    [ObservableProperty]
    private string _recoveryFactsText = string.Empty;

    [ObservableProperty]
    private Guid? _planId;

    [ObservableProperty]
    private Sha256? _planHash;

    [ObservableProperty]
    private Guid? _operationId;

    [RelayCommand]
    private async Task ValidateAsync()
    {
        await RunAsync(async () =>
        {
            Guid deviceId = RequireDeviceId();
            OnboardingPrerequisiteReport report = await _client.ValidatePrerequisitesAsync(
                RequireNodeId(),
                [DefaultFacts(deviceId)],
                CancellationToken.None).ConfigureAwait(true);
            Findings.Clear();
            foreach (OnboardingFinding finding in report.Findings)
            {
                Findings.Add(new OnboardingFindingListItem
                {
                    Code = finding.Code,
                    Severity = finding.Severity.ToString(),
                    Message = finding.Message,
                });
            }

            StatusText = report.Passed ? "Prerequisites passed." : "Prerequisites have blockers.";
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CreatePlanAsync()
    {
        await RunAsync(async () =>
        {
            Guid nodeId = RequireNodeId();
            Sha256 hash = Utf8Sha256("onboarding-desktop");
            OnboardingPlanSummary plan = await _client.CreatePlanAsync(
                nodeId,
                hash,
                hash,
                [DefaultDevicePlan(RequireDeviceId())],
                CancellationToken.None).ConfigureAwait(true);
            PlanId = DesktopProtoUuid.ToGuid(plan.PlanId);
            PlanHash = plan.PlanHash;
            Placements.Clear();
            foreach (OnboardingAnchorPlacementView placement in plan.Placements)
            {
                Placements.Add(new OnboardingPlacementListItem
                {
                    Marker = placement.Marker,
                    Mode = placement.Mode.ToString(),
                    BeforeLabel = placement.BeforeLabel,
                    AfterLabel = placement.AfterLabel,
                });
            }

            StatusText = $"Plan {PlanId:D} created.";
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        await RunAsync(async () =>
        {
            if (PlanId is not Guid planId || PlanHash is null)
            {
                throw new InvalidOperationException("Create a plan before start.");
            }

            OnboardingOperationSummary started = await _client.StartAsync(planId, PlanHash, CancellationToken.None)
                .ConfigureAwait(true);
            OperationId = DesktopProtoUuid.ToGuid(started.OperationId);
            ProgressLines.Clear();
            foreach (string line in started.Timeline)
            {
                ProgressLines.Add(line);
            }

            StatusText = $"Operation {started.State}.";
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RollbackAsync()
    {
        await RunAsync(async () =>
        {
            if (OperationId is not Guid operationId)
            {
                throw new InvalidOperationException("Start an operation before rollback.");
            }

            OnboardingOperationSummary rolled = await _client.RollbackAsync(operationId, CancellationToken.None)
                .ConfigureAwait(true);
            StatusText = $"Rollback {rolled.State}.";
            foreach (string line in rolled.Timeline)
            {
                ProgressLines.Add(line);
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RecoveryAsync()
    {
        await RunAsync(async () =>
        {
            Guid nodeId = RequireNodeId();
            OnboardingRecoveryStatus status = await _client.GetRecoveryStatusAsync(nodeId, OperationId, CancellationToken.None)
                .ConfigureAwait(true);
            RecoveryFactsText =
                $"state={status.OperationState}; action={status.Action}; node={status.NodeManagementState}; error={status.ErrorCode}";
            StatusText = "Recovery facts refreshed.";
        }).ConfigureAwait(true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.StateChanged -= OnConnectionStateChanged;
        _inventory.PropertyChanged -= OnInventoryPropertyChanged;
    }

    private Guid RequireNodeId()
    {
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        if (selected is null || selected.KindLabel is not ("Node" or "Device"))
        {
            throw new InvalidOperationException("Select a Node in the inventory tree.");
        }

        return selected.KindLabel == "Node" ? selected.Id : selected.ParentId ?? selected.Id;
    }

    private Guid RequireDeviceId()
    {
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        if (selected is null)
        {
            throw new InvalidOperationException("Select a Node or Device in the inventory tree.");
        }

        if (selected.KindLabel == "Device")
        {
            return selected.Id;
        }

        InventoryNodeViewModel? device = selected.Children.FirstOrDefault(static c => c.KindLabel == "Device");
        if (device is null)
        {
            throw new InvalidOperationException("Selected Node has no Device child.");
        }

        return device.Id;
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ErrorText = null;
        try
        {
            await action().ConfigureAwait(true);
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
        }
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
        => Dispatcher.UIThread.Post(() => StatusText = _connection.State.ToString());

    private void OnInventoryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InventoryTreeViewModel.SelectedNode))
        {
            Dispatcher.UIThread.Post(() => StatusText = "Node selection changed.");
        }
    }

    private static OnboardingDevicePrerequisiteFacts DefaultFacts(Guid nodeId)
        => new()
        {
            DeviceId = DesktopProtoUuid.FromGuid(nodeId),
            ExactSupportedBuild = true,
            VersionMajor = 7,
            VersionMinor = 16,
            VersionPatch = 2,
            VersionChannel = "stable",
            SupportState = 0,
            PlainApi = new OnboardingIpServiceFacts { Found = true, Disabled = true, Port = 8728 },
            ApiSsl = new OnboardingIpServiceFacts
            {
                Found = true,
                Disabled = false,
                Port = 8729,
                Certificate = "mfc-api",
                MaxSessions = 4,
            },
            ReadAccount = new OnboardingServiceAccountFacts
            {
                Name = "mfc-read",
                GroupName = "mfc-read-group",
                Policies = { "api", "read" },
                AddressPrefixes = { "10.0.0.0/24" },
            },
            DeploymentAccount = new OnboardingServiceAccountFacts
            {
                Name = "mfc-deploy",
                GroupName = "mfc-deploy-group",
                Policies = { "api", "read", "write", "test" },
                AddressPrefixes = { "10.0.0.0/24" },
            },
            DeviceMode = new OnboardingDeviceModeFacts { SchedulerEnabled = true, Flagged = false },
            ExpectedApiSslPort = 8729,
        };

    private static OnboardingDevicePlanInput DefaultDevicePlan(Guid deviceOrNodeId)
    {
        Sha256 hash = Utf8Sha256(deviceOrNodeId.ToString("D"));
        return new OnboardingDevicePlanInput
        {
            DeviceId = DesktopProtoUuid.FromGuid(deviceOrNodeId),
            ExpectedRouterosVersion = "7.16.2",
            ExpectedCapabilityHash = hash,
            ExpectedConfigurationHash = hash,
            ExpectedCompatibilityHash = hash,
            ExpectedApiServiceHash = hash,
            ExpectedReadAccountHash = hash,
            ExpectedDeploymentAccountHash = hash,
            ExpectedDeviceModeHash = hash,
            ExpectedGuardHash = hash,
            WatchdogTtlSeconds = 180,
        };
    }

    private static Sha256 Utf8Sha256(string value)
        => new() { Value = ByteString.CopyFrom(SHA256.HashData(Encoding.UTF8.GetBytes(value))) };
}
