using System.Reflection;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>
/// DESK-INCIDENT-01 / W7-73: Desktop Incident ViewModel + client Living Spec vs IncidentGrpcHost
/// (SEC-06 Ingest + Bind wire). MainWindow panel bindings are DESK-INCIDENT-02.
/// </summary>
public sealed class DesktopIncidentLivingSpecTests
{
    private static readonly string[] ExpectedWireMethods =
    [
        "BindIncidentResponseAssessment",
        "IngestIncidentSignal",
    ];

    [Fact]
    public void Ac1WireAndDesktopClientExposeIngestAndBindOnly()
    {
        string[] methods = IncidentService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedWireMethods, methods);

        Type client = typeof(IIncidentServiceClient);
        Assert.NotNull(client.GetMethod(nameof(IIncidentServiceClient.IngestIncidentSignalAsync)));
        Assert.NotNull(client.GetMethod(nameof(IIncidentServiceClient.BindIncidentResponseAssessmentAsync)));
        Assert.Null(client.GetMethod("DeployIncidentDenyOverlayAsync"));
        Assert.Null(client.GetMethod("ExpireIncidentDenyOverlayAsync"));
        Assert.Null(client.GetMethod("EmitResponseFeedbackAsync"));

        string source = ReadSource("src/Mfc.Desktop/Services/GrpcIncidentServiceClient.cs");
        Assert.Contains("IngestIncidentSignalAsync", source, StringComparison.Ordinal);
        Assert.Contains("BindIncidentResponseAssessmentAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Deploy", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Overlay", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Feedback", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac2ViewModelExposesIngestSurfaceWithoutDeployOverlayCommands()
    {
        using IncidentViewModel vm = CreateVm(new FakeIncidentClient(), ControllerConnectionState.Connected);

        Assert.False(vm.HasDeployOverlayCommands);
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.IngestCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.SourceEventId)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.Category)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.DeduplicationKey)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.Confidence)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.LastSignal)));
        Assert.Null(vm.GetType().GetProperty("DeployCommand"));
        Assert.Null(vm.GetType().GetProperty("ExpireOverlayCommand"));
        Assert.Null(vm.GetType().GetProperty("EmitFeedbackCommand"));
        Assert.Null(vm.GetType().GetMethod("DeployAsync"));
        Assert.Null(vm.GetType().GetMethod("ExpireOverlayAsync"));
    }

    [Fact]
    public async Task Ac3IngestRequiresConnectedController()
    {
        FakeIncidentClient disconnectedClient = new();
        using IncidentViewModel disconnected = CreateVm(disconnectedClient, ControllerConnectionState.Disconnected);
        disconnected.SourceEventId = "siem-1";
        disconnected.Category = "brute_force_login";
        disconnected.DeduplicationKey = "dedup:1";

        await disconnected.IngestCommand.ExecuteAsync(null);

        Assert.Equal(0, disconnectedClient.IngestCalls);
        Assert.Contains("Connect to Controller", disconnected.ErrorText, StringComparison.Ordinal);
        Assert.Null(disconnected.LastSignal);
    }

    [Fact]
    public async Task Ac4IngestMapsLastSignalFromClientPayload()
    {
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
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
        };

        using IncidentViewModel vm = CreateVm(client, ControllerConnectionState.Connected);
        vm.SourceEventId = "siem-evt-42";
        vm.Category = "brute_force_login";
        vm.DeduplicationKey = "dedup:siem:42";
        vm.Confidence = 85;

        await vm.IngestCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, client.IngestCalls);
        Assert.NotNull(client.LastIngestRequest);
        Assert.Equal("siem-evt-42", client.LastIngestRequest!.SourceEventId);
        Assert.Equal("brute_force_login", client.LastIngestRequest.Category);
        Assert.Equal("dedup:siem:42", client.LastIngestRequest.DeduplicationKey);
        Assert.Equal(85, client.LastIngestRequest.Confidence);
        Assert.Equal(eventId, vm.LastSignal!.EventId);
        Assert.Equal("brute_force_login", vm.LastSignal.Category);
        Assert.Contains(eventId.ToString("D"), vm.StatusText, StringComparison.Ordinal);
        Assert.True(vm.HasLastSignal);
    }

    [Fact]
    public void Ac5ShellWiresIncidentWithoutEighthNavigationModule()
    {
        PropertyInfo? incident = typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.Incident));
        Assert.NotNull(incident);
        Assert.Equal(typeof(IncidentViewModel), incident!.PropertyType);

        string[] modules = Enum.GetNames<ShellNavigationModule>();
        Assert.Equal(7, modules.Length);
        Assert.DoesNotContain("Incident", modules, StringComparer.Ordinal);

        string app = ReadSource("src/Mfc.Desktop/App.axaml.cs");
        Assert.Contains("GrpcIncidentServiceClient", app, StringComparison.Ordinal);
        Assert.Contains("IncidentViewModel", app, StringComparison.Ordinal);
        Assert.Contains("incidentVm", app, StringComparison.Ordinal);

        // MainWindow panel bindings land in DESK-INCIDENT-02 (DesktopIncidentPanelLivingSpecTests).
    }

    [Fact]
    public void Ac6HostContractLivingSpecRemainsPresent()
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

        public int IngestCalls { get; private set; }

        public int BindCalls { get; private set; }

        public IngestIncidentSignalRequest? LastIngestRequest { get; private set; }

        public Task<IncidentSignal> IngestIncidentSignalAsync(
            IngestIncidentSignalRequest request,
            CancellationToken cancellationToken = default)
        {
            IngestCalls++;
            LastIngestRequest = request;
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
            return Task.FromResult(new IncidentResponseAssessmentBinding());
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
