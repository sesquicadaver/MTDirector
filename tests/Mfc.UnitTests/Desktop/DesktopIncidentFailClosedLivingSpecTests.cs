using System.Reflection;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>
/// DESK-INCIDENT-04 / W7-76: Desktop Incident fail-closed Living Spec —
/// no deploy / overlay / feedback RPCs beyond SEC-06 Ingest+Bind wire.
/// </summary>
public sealed class DesktopIncidentFailClosedLivingSpecTests
{
    private static readonly string[] ForbiddenRpcNameFragments =
    [
        "Deploy",
        "Overlay",
        "Expire",
        "Feedback",
        "Heal",
        "Repair",
    ];

    [Fact]
    public void Ac1WireAndProtoStayIngestAndBindOnly()
    {
        string[] methods = IncidentService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["BindIncidentResponseAssessment", "IngestIncidentSignal"], methods);

        string proto = ReadSource("src/Mfc.Contracts/Protos/mfc/v1/incident.proto");
        Assert.Contains("rpc IngestIncidentSignal", proto, StringComparison.Ordinal);
        Assert.Contains("rpc BindIncidentResponseAssessment", proto, StringComparison.Ordinal);
        Assert.Contains("Deploy/overlay/feedback RPCs stay Application-only", proto, StringComparison.Ordinal);
        foreach (string fragment in ForbiddenRpcNameFragments)
        {
            Assert.DoesNotContain($"rpc {fragment}", proto, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Ac2DesktopClientAndGrpcSourceExposeNoDeployOverlayFeedback()
    {
        Type client = typeof(IIncidentServiceClient);
        MethodInfo[] methods = client.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.Equal(2, methods.Length);
        Assert.Contains(methods, m => m.Name == nameof(IIncidentServiceClient.IngestIncidentSignalAsync));
        Assert.Contains(methods, m => m.Name == nameof(IIncidentServiceClient.BindIncidentResponseAssessmentAsync));

        foreach (MethodInfo method in methods)
        {
            foreach (string fragment in ForbiddenRpcNameFragments)
            {
                Assert.DoesNotContain(fragment, method.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        string grpc = ReadSource("src/Mfc.Desktop/Services/GrpcIncidentServiceClient.cs");
        Assert.Contains("IngestIncidentSignalAsync", grpc, StringComparison.Ordinal);
        Assert.Contains("BindIncidentResponseAssessmentAsync", grpc, StringComparison.Ordinal);
        foreach (string fragment in new[] { "Deploy", "Overlay", "Expire", "Feedback" })
        {
            Assert.DoesNotContain(fragment, grpc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Ac3ViewModelFailClosedFlagsAndCommands()
    {
        using IncidentViewModel vm = new(new NullIncidentClient(), new NullConnection());
        Assert.False(vm.HasDeployOverlayCommands);
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.IngestCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(IncidentViewModel.BindAssessmentCommand)));

        foreach (string name in new[]
                 {
                     "DeployCommand", "ExpireOverlayCommand", "EmitFeedbackCommand",
                     "DeployDenyOverlayCommand", "ExpireIncidentDenyOverlayCommand",
                 })
        {
            Assert.Null(vm.GetType().GetProperty(name));
        }

        foreach (string name in new[]
                 {
                     "DeployAsync", "ExpireOverlayAsync", "EmitFeedbackAsync",
                     "DeployDenyOverlayAsync", "ExpireIncidentDenyOverlayAsync",
                 })
        {
            Assert.Null(vm.GetType().GetMethod(name));
        }

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/IncidentViewModel.cs");
        Assert.Contains("HasDeployOverlayCommands", vmSource, StringComparison.Ordinal);
        Assert.Contains("No deploy/overlay/feedback Desktop RPCs", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DeployDenyOverlay", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ExpireIncidentDenyOverlay", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EmitResponseFeedback", vmSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4MainWindowIncidentSurfaceHasNoDeployOverlayActions()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Incident.IngestCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.BindAssessmentCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("no deploy/overlay", axaml, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("Incident.Deploy", axaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Incident.Expire", axaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Incident.Feedback", axaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DenyOverlay", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("EmitResponseFeedback", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5HostWireLockAndApplicationOnlySurfacesRemainDocumented()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/IncidentGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/IncidentGrpcHostTests.cs"));
        Assert.Contains("IncidentServiceExposesOnlyIngestAndBindOnWire", host, StringComparison.Ordinal);

        // Application use cases for overlay/feedback may exist; Desktop must not import them.
        string desktopCsproj = ReadSource("src/Mfc.Desktop/Mfc.Desktop.csproj");
        Assert.DoesNotContain("Mfc.Application", desktopCsproj, StringComparison.Ordinal);
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

    private sealed class NullIncidentClient : IIncidentServiceClient
    {
        public Task<IncidentSignal> IngestIncidentSignalAsync(
            IngestIncidentSignalRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new IncidentSignal());

        public Task<IncidentResponseAssessmentBinding> BindIncidentResponseAssessmentAsync(
            BindIncidentResponseAssessmentRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new IncidentResponseAssessmentBinding());
    }

    private sealed class NullConnection : IControllerConnectionService
    {
        public GrpcChannel? Channel => null;

        public ControllerConnectionState State => ControllerConnectionState.Disconnected;

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
