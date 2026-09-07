using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>
/// DESK-INCIDENT-03 / W7-75: Bind assessment operator path Living Spec (selection → Bind RPC).
/// </summary>
public sealed class DesktopIncidentBindLivingSpecTests
{
    [Fact]
    public void Ac1ViewModelExposesBindAssessmentCommandAndBindingFields()
    {
        using IncidentViewModel vm = CreateVm(new FakeIncidentClient(), ControllerConnectionState.Connected);
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.BindAssessmentCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.EndpointIdText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.PresenceIdText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.EnforcementNodeIdText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.FlowSourceAddress)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.LastBinding)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.HasLastBinding)));
        Assert.NotNull(typeof(IncidentAssessmentBindingListItem).GetProperty(
            nameof(IncidentAssessmentBindingListItem.FeasibilityText)));
        Assert.False(vm.HasDeployOverlayCommands);
        Assert.Null(vm.GetType().GetProperty("DeployCommand"));
    }

    [Fact]
    public async Task Ac2BindRequiresConnectedControllerAndIngestedSignal()
    {
        FakeIncidentClient disconnectedClient = new();
        using IncidentViewModel disconnected = CreateVm(disconnectedClient, ControllerConnectionState.Disconnected);
        disconnected.EndpointIdText = "11111111-1111-1111-1111-111111111111";
        disconnected.PresenceIdText = "22222222-2222-2222-2222-222222222222";
        disconnected.EnforcementNodeIdText = "33333333-3333-3333-3333-333333333333";

        await disconnected.BindAssessmentCommand.ExecuteAsync(null);

        Assert.Equal(0, disconnectedClient.BindCalls);
        Assert.Contains("Connect to Controller", disconnected.ErrorText, StringComparison.Ordinal);

        FakeIncidentClient noSignalClient = new();
        using IncidentViewModel noSignal = CreateVm(noSignalClient, ControllerConnectionState.Connected);
        noSignal.EndpointIdText = "11111111-1111-1111-1111-111111111111";
        noSignal.PresenceIdText = "22222222-2222-2222-2222-222222222222";
        noSignal.EnforcementNodeIdText = "33333333-3333-3333-3333-333333333333";

        await noSignal.BindAssessmentCommand.ExecuteAsync(null);

        Assert.Equal(0, noSignalClient.BindCalls);
        Assert.Contains("Ingest a signal", noSignal.ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac3BindMapsAssessmentBindingFromClientPayload()
    {
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid incidentId = eventId;
        FakeIncidentClient client = new()
        {
            IngestPayload = new IncidentSignal
            {
                EventId = DesktopProtoUuid.FromGuid(eventId),
                SourceEventId = "siem-evt-42",
                Category = "brute_force_login",
                Severity = IncidentSeverity.High,
                SourceType = IncidentSignalSourceType.Siem,
                Confidence = 85,
                DeduplicationKey = "dedup:siem:42",
            },
            BindPayload = new IncidentResponseAssessmentBinding
            {
                IncidentId = DesktopProtoUuid.FromGuid(incidentId),
                CorrelationFlow = new IncidentFlowTuple
                {
                    SourceAddress = "10.0.0.8",
                    DestinationAddress = "198.51.100.10",
                    Protocol = "tcp",
                },
                Assessment = new ResponseAssessment
                {
                    Feasibility = "FullyEnforceable",
                    VisibilityStatus = "Full",
                    Confidence = 80,
                    Status = "Active",
                },
            },
        };

        using IncidentViewModel vm = CreateVm(client, ControllerConnectionState.Connected);
        vm.SourceEventId = "siem-evt-42";
        vm.Category = "brute_force_login";
        vm.DeduplicationKey = "dedup:siem:42";
        vm.Confidence = 85;
        await vm.IngestCommand.ExecuteAsync(null);

        vm.EndpointIdText = "11111111-1111-1111-1111-111111111111";
        vm.PresenceIdText = "22222222-2222-2222-2222-222222222222";
        vm.EnforcementNodeIdText = "33333333-3333-3333-3333-333333333333";
        vm.FlowSourceAddress = "10.0.0.8";
        vm.FlowDestinationAddress = "198.51.100.10";
        vm.FlowProtocol = "tcp";

        await vm.BindAssessmentCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, client.BindCalls);
        Assert.NotNull(client.LastBindRequest);
        Assert.Equal(eventId, DesktopProtoUuid.ToGuid(client.LastBindRequest!.Signal.EventId));
        Assert.Equal("10.0.0.8", client.LastBindRequest.Signal.Flow.SourceAddress);
        Assert.Equal(IncidentSessionVisibilityStatus.Full, client.LastBindRequest.SessionVisibility);
        Assert.Equal(ObservedPacketPathClass.CpuFirewall, client.LastBindRequest.PacketPathClass);
        Assert.Equal(incidentId, vm.LastBinding!.IncidentId);
        Assert.Equal("FullyEnforceable", vm.LastBinding.FeasibilityText);
        Assert.Contains("FullyEnforceable", vm.StatusText, StringComparison.Ordinal);
        Assert.True(vm.HasLastBinding);
    }

    [Fact]
    public void Ac4MainWindowBindsBindAssessmentFormAndResult()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Incident.BindAssessmentCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.EndpointIdText", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.PresenceIdText", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.EnforcementNodeIdText", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.FlowSourceAddress", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.LastBinding.SummaryLine", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.LastBinding.FeasibilityText", axaml, StringComparison.Ordinal);
        Assert.Contains("Bind response assessment", axaml, StringComparison.Ordinal);
        Assert.Contains("BindIncidentResponseAssessment", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Incident.DeployCommand", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5HostContractLivingSpecRemainsPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/IncidentGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/IncidentGrpcHostTests.cs"));
        Assert.Contains("IngestAndBindAssessmentOverHost", host, StringComparison.Ordinal);
        Assert.Contains("IncidentServiceExposesOnlyIngestAndBindOnWire", host, StringComparison.Ordinal);
    }

    private static IncidentViewModel CreateVm(IIncidentServiceClient client, ControllerConnectionState state)
        => new(client, new FakeConnection(state));

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

    private sealed class FakeIncidentClient : IIncidentServiceClient
    {
        public IncidentSignal? IngestPayload { get; init; }

        public IncidentResponseAssessmentBinding? BindPayload { get; init; }

        public int IngestCalls { get; private set; }

        public int BindCalls { get; private set; }

        public BindIncidentResponseAssessmentRequest? LastBindRequest { get; private set; }

        public Task<IncidentSignal> IngestIncidentSignalAsync(
            IngestIncidentSignalRequest request,
            CancellationToken cancellationToken = default)
        {
            IngestCalls++;
            return Task.FromResult(
                IngestPayload
                ?? new IncidentSignal
                {
                    EventId = request.EventId,
                    SourceEventId = request.SourceEventId,
                    Category = request.Category,
                    Severity = request.Severity,
                    SourceType = request.SourceType,
                    Confidence = request.Confidence,
                    DeduplicationKey = request.DeduplicationKey,
                });
        }

        public Task<IncidentResponseAssessmentBinding> BindIncidentResponseAssessmentAsync(
            BindIncidentResponseAssessmentRequest request,
            CancellationToken cancellationToken = default)
        {
            BindCalls++;
            LastBindRequest = request;
            return Task.FromResult(BindPayload ?? new IncidentResponseAssessmentBinding
            {
                IncidentId = request.Signal.EventId,
                Assessment = new ResponseAssessment { Feasibility = "FullyEnforceable" },
            });
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
}
