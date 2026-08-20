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
/// Deployment operator panel: semantic diff, artifacts, order, probes/TTL, progress, recovery.
/// No ForceApply and no raw RouterOS command surface.
/// </summary>
public sealed partial class DeploymentViewModel : ObservableObject, IDisposable
{
    private readonly IDeploymentServiceClient _client;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private bool _disposed;

    public DeploymentViewModel(
        IDeploymentServiceClient client,
        IControllerConnectionService connection,
        InventoryTreeViewModel inventory)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _connection.StateChanged += OnConnectionStateChanged;
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
    }

    public ObservableCollection<string> SemanticDiffLines { get; } = [];

    public ObservableCollection<string> ArtifactLines { get; } = [];

    public ObservableCollection<string> OrderLines { get; } = [];

    public ObservableCollection<string> ProbeAndWatchdogLines { get; } = [];

    public ObservableCollection<string> ProgressLines { get; } = [];

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasForceApply
    {
        get
        {
            _ = _client;
            return false;
        }
    }

    public bool HasRawRouterOsCommands
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
    private string _statusText = "Select a Node, then create a deployment plan.";

    [ObservableProperty]
    private string _recoveryFactsText = string.Empty;

    [ObservableProperty]
    private Guid? _planId;

    [ObservableProperty]
    private Sha256? _planHash;

    [ObservableProperty]
    private Guid? _operationId;

    [RelayCommand]
    private async Task CreatePlanAsync()
    {
        await RunAsync(async () =>
        {
            Guid nodeId = RequireNodeId();
            Guid deviceId = RequireDeviceId();
            Sha256 hash = Utf8Sha256("deployment-desktop");
            DeploymentPlanSummary plan = await _client.CreatePlanAsync(
                nodeId,
                hash,
                hash,
                hash,
                [DefaultDevicePlan(deviceId)],
                CancellationToken.None).ConfigureAwait(true);
            PlanId = DesktopProtoUuid.ToGuid(plan.PlanId);
            PlanHash = plan.PlanHash;
            SemanticDiffLines.Clear();
            foreach (string entry in plan.SemanticDiffEntries)
            {
                SemanticDiffLines.Add(entry);
            }

            ArtifactLines.Clear();
            OrderLines.Clear();
            ProbeAndWatchdogLines.Clear();
            foreach (DeploymentDevicePlanView device in plan.Devices)
            {
                ArtifactLines.Add(
                    $"device={DesktopProtoUuid.ToGuid(device.DeviceId):D} old={ToHex(device.OldArtifactHash)} new={ToHex(device.NewArtifactHash)}");
                OrderLines.Add(
                    $"activation=[{string.Join(',', device.ActivationOrderMarkers)}] rollback=[{string.Join(',', device.RollbackOrderMarkers)}]");
                ProbeAndWatchdogLines.Add($"watchdog_ttl_seconds={device.WatchdogTtlSeconds}");
                foreach (DeploymentProbeView probe in device.Probes)
                {
                    ProbeAndWatchdogLines.Add($"probe:{probe.Kind}:{probe.Destination}:{probe.TimeoutMilliseconds}ms");
                }
            }

            foreach (Uuid deviceOrderId in plan.ActivationOrderDeviceIds)
            {
                OrderLines.Add($"plan_activation_device={DesktopProtoUuid.ToGuid(deviceOrderId):D}");
            }

            foreach (Uuid deviceOrderId in plan.RollbackOrderDeviceIds)
            {
                OrderLines.Add($"plan_rollback_device={DesktopProtoUuid.ToGuid(deviceOrderId):D}");
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

            DeploymentOperationSummary started = await _client.StartAsync(
                    planId,
                    PlanHash,
                    [
                        new DeploymentPacketPathPairFact
                        {
                            IngressInterface = "ether1",
                            EgressInterface = "wan1",
                            PathClass = DeploymentPacketPathKind.CpuFirewall,
                        },
                    ],
                    CancellationToken.None)
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

            DeploymentOperationSummary rolled = await _client.RollbackAsync(operationId, CancellationToken.None)
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
            DeploymentRecoveryStatus status = await _client.GetRecoveryStatusAsync(nodeId, OperationId, CancellationToken.None)
                .ConfigureAwait(true);
            RecoveryFactsText =
                $"state={status.OperationState}; action={status.Action}; error={status.ErrorCode}";
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

    private static DeploymentDevicePlanInput DefaultDevicePlan(Guid deviceId)
    {
        // Desktop demo payload: operator normally receives a sealed plan from Controller compile path.
        // Placeholder hashes keep the panel Contracts-only until binder UI lands with M4-13 acceptance.
        Sha256 hash = Utf8Sha256(deviceId.ToString("D"));
        DeploymentDevicePlanInput input = new()
        {
            DeviceId = DesktopProtoUuid.FromGuid(deviceId),
            ExpectedRouterosVersion = "7.16.2",
            ExpectedCapabilityHash = hash,
            ExpectedConfigurationHash = hash,
            ExpectedCompatibilityHash = hash,
            ExpectedGuardContextHash = hash,
            ExpectedAnchorContextHash = hash,
            OldArtifactHash = Utf8Sha256("old-art"),
            NewArtifactHash = Utf8Sha256("new-art"),
            RollbackTtlSeconds = 180,
        };
        string[] markers = ["mfc:anchor:v1:4:f", "mfc:anchor:v1:4:o", "mfc:anchor:v1:4:i"];
        input.AnchorActivationOrderMarkers.AddRange(markers);
        foreach (string marker in markers)
        {
            input.OldAnchorTargets.Add(new DeploymentAnchorTargetInput
            {
                Marker = marker,
                JumpTarget = marker.Contains(":4:i", StringComparison.Ordinal) ? "mfc4.in"
                    : marker.Contains(":4:o", StringComparison.Ordinal) ? "mfc4.out" : "mfc4.fwd",
            });
            input.NewAnchorTargets.Add(new DeploymentAnchorTargetInput
            {
                Marker = marker,
                JumpTarget = marker.Contains(":4:i", StringComparison.Ordinal) ? "mfc4.in.r.0123456789abcdef"
                    : marker.Contains(":4:o", StringComparison.Ordinal) ? "mfc4.out.r.0123456789abcdef"
                    : "mfc4.fwd.r.0123456789abcdef",
            });
            input.TransitionStateHashes.Add(Utf8Sha256($"transition-{marker}"));
        }

        input.TransitionStateHashes.Add(Utf8Sha256("transition-final"));
        input.Probes.Add(new DeploymentProbeInput
        {
            Kind = DeploymentProbeKind.RouterPing,
            Destination = "192.0.2.1",
            TimeoutMilliseconds = 500,
        });
        return input;
    }

    private static string ToHex(Sha256 hash)
        => Convert.ToHexString(hash.Value.Span)[..12] + "…";

    private static Sha256 Utf8Sha256(string value)
        => new() { Value = ByteString.CopyFrom(SHA256.HashData(Encoding.UTF8.GetBytes(value))) };
}
