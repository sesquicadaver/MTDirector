using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-409: AUDIT-RPC-01 — durable Accept + Fast Start + live Watch phases.
/// </summary>
public sealed class AuditRpc01FastStartLiveWatchW7409LivingSpecTests
{
    [Fact]
    public void Ac1FastStartAcceptLivePhasesAndStableDesktopKey()
    {
        string root = RepoRoot();
        string start = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Application/Deployment/DeploymentWorkflowUseCases.cs"));
        string queue = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Application/Abstractions/Deployment/IDeploymentStartWorkChannel.cs"));
        string sink = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Application/Abstractions/Deployment/IDeploymentProgressSink.cs"));
        string phases = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Application/Abstractions/Deployment/IDeploymentPhaseReporter.cs"));
        string hosted = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Controller/Jobs/DeploymentStartHostedService.cs"));
        string channel = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Controller/Grpc/DeploymentStartQueue.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string grpc = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Controller/Grpc/DeploymentGrpcService.cs"));
        string desktopVm = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs"));
        string desktopClient = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Desktop/Services/GrpcDeploymentServiceClient.cs"));
        string standalone = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));

        Assert.Contains("AUDIT-RPC-01", start, StringComparison.Ordinal);
        Assert.Contains("ContinueAcceptedAsync", start, StringComparison.Ordinal);
        Assert.Contains("accepted = true", start, StringComparison.Ordinal);
        Assert.Contains("IDeploymentStartWorkChannel", start, StringComparison.Ordinal);
        Assert.Contains("SinkDeploymentPhaseReporter", start, StringComparison.Ordinal);
        Assert.Contains("RunsSynchronously", queue, StringComparison.Ordinal);
        Assert.Contains("ImmediateDeploymentStartWorkChannel", queue, StringComparison.Ordinal);
        Assert.Contains("IDeploymentProgressSink", sink, StringComparison.Ordinal);
        Assert.Contains("IDeploymentPhaseReporter", phases, StringComparison.Ordinal);

        Assert.Contains("DeploymentStartHostedService", hosted, StringComparison.Ordinal);
        Assert.Contains("ContinueAcceptedAsync", hosted, StringComparison.Ordinal);
        Assert.Contains("ChannelDeploymentStartWorkChannel", channel, StringComparison.Ordinal);
        Assert.Contains("HubDeploymentProgressSink", channel, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<DeploymentStartHostedService>", program, StringComparison.Ordinal);
        Assert.Contains("ChannelDeploymentStartWorkChannel", program, StringComparison.Ordinal);

        Assert.Contains("AUDIT-RPC-01", grpc, StringComparison.Ordinal);
        Assert.Contains("view.Timeline.Count > 0", grpc, StringComparison.Ordinal);

        Assert.Contains("_startIdempotencyKey", desktopVm, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RPC-01", desktopVm, StringComparison.Ordinal);
        Assert.Contains("idempotencyKey", desktopClient, StringComparison.Ordinal);
        Assert.Contains("phases?.Report", standalone, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-409 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RPC-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("AuditRpc01FastStartLiveWatchW7409LivingSpecTests", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-410", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-03", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-409 | [#1218](https://github.com/sesquicadaver/MTDirector/issues/1218) | AUDIT-RPC-01 — Fast Start + live Watch | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-410 | [#1220](https://github.com/sesquicadaver/MTDirector/issues/1220) | Seed next after AUDIT-RPC-01 → AUDIT-CAP-03 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-411 | [#1221](https://github.com/sesquicadaver/MTDirector/issues/1221) | AUDIT-CAP-03 — Full capture projection | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-412 | [#1224](https://github.com/sesquicadaver/MTDirector/issues/1224) | Seed next after AUDIT-CAP-03 → AUDIT-CAP-04 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-413 | [#1226](https://github.com/sesquicadaver/MTDirector/issues/1226) | AUDIT-CAP-04 — Capture attempt identity | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-414 | [#1228](https://github.com/sesquicadaver/MTDirector/issues/1228) | Seed next after AUDIT-CAP-04 → AUDIT-AN-03 | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", roadmap, StringComparison.Ordinal);

        Assert.Contains("AUDIT-RPC-01 W7-409 (#1218) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-410 (#1220) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", plan62, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-03 W7-411 (#1221) DONE", plan62, StringComparison.Ordinal);

        Assert.Contains("AUDIT-RPC-01 W7-409 (#1218) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", continuous, StringComparison.Ordinal);

        Assert.Contains("AuditRpc01FastStartLiveWatchW7409", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7410", testing, StringComparison.Ordinal);
        Assert.Contains("AuditCap03FullCaptureProjectionW7411", testing, StringComparison.Ordinal);
        Assert.Contains("| `W7-409` | #1218 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-410` | #1220 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-411` | #1221 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-412` | #1224 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-413` | #1226 |", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", issues, StringComparison.Ordinal);
    }

    private static string RepoRoot()
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
}
