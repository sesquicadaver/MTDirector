using Mfc.Contracts.Mfc.V1;
using Mfc.Controller.Grpc;
using Xunit;
using DomainDeploymentState = Mfc.Domain.Deployment.DeploymentOperationState;
using DomainOnboardingState = Mfc.Domain.Onboarding.OnboardingOperationState;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-236 / AUDIT-INT-01 Living Spec: FastTrack topology wire-up, fresh-session disposal,
/// Watch authorization, Desktop OperationId, hub retention, docs/queue advance.
/// </summary>
public sealed class AuditInt01W7236LivingSpecTests
{
    [Fact]
    public void AcFtCompileWiresCaptureTopologyAndNeverHardcodesSafeSingleWan()
    {
        string root = RepoRoot();
        string compile = File.ReadAllText(Path.Combine(
            root,
            "src/Mfc.Application/Policies/CompileNodeFilterArtifactsUseCase.cs"));
        string mapper = File.ReadAllText(Path.Combine(
            root,
            "src/Mfc.Application/Policies/FastTrackContextMapper.cs"));

        Assert.Contains("FastTrackTopology = fastTrackTopology", compile, StringComparison.Ordinal);
        Assert.Contains("FastTrackContextMapper.MapTopology", compile, StringComparison.Ordinal);
        Assert.Contains("LoadCanonicalSectionsAsync", compile, StringComparison.Ordinal);
        Assert.DoesNotContain("SafeSingleWan", compile, StringComparison.Ordinal);
        Assert.Contains("MapTopology", mapper, StringComparison.Ordinal);
        Assert.Contains("Never invents", mapper, StringComparison.Ordinal);
    }

    [Fact]
    public void AcDispFreshSessionOwnsAuthenticatedConnectionOnDispose()
    {
        string root = RepoRoot();
        string deviceSession = File.ReadAllText(Path.Combine(
            root,
            "src/Mfc.RouterOs/Deployment/RouterOsDeploymentDeviceSession.cs"));
        string session = File.ReadAllText(Path.Combine(
            root,
            "src/Mfc.RouterOs/Deployment/RouterOsDeploymentSession.cs"));

        Assert.Contains("ownedConnection: connection", deviceSession, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "return new RouterOsDeploymentSession(new RouterOsDeploymentWriteChannel(connection.Session));",
            deviceSession,
            StringComparison.Ordinal);
        Assert.Contains("AuthenticatedRosConnection? _ownedConnection", session, StringComparison.Ordinal);
        Assert.Contains("await _ownedConnection.DisposeAsync()", session, StringComparison.Ordinal);
    }

    [Fact]
    public void AcWatchAuthResolveActorAndReadPermissionsBeforeHub()
    {
        string root = RepoRoot();
        string snapshot = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/SnapshotGrpcService.cs"));
        string deployment = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/DeploymentGrpcService.cs"));
        string onboarding = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/OnboardingGrpcService.cs"));

        Assert.Contains("EnsureWatchAuthorizedAsync", snapshot, StringComparison.Ordinal);
        Assert.Contains("ApplicationPermissions.SnapshotRead", snapshot, StringComparison.Ordinal);
        Assert.Contains("TryGetOwnerActor", snapshot, StringComparison.Ordinal);
        Assert.Contains("EnsureWatchAuthorizedAsync", deployment, StringComparison.Ordinal);
        Assert.Contains("ApplicationPermissions.DeploymentRead", deployment, StringComparison.Ordinal);
        Assert.Contains("CreatedBy", deployment, StringComparison.Ordinal);
        Assert.Contains("EnsureWatchAuthorizedAsync", onboarding, StringComparison.Ordinal);
        Assert.Contains("ApplicationPermissions.OnboardingRead", onboarding, StringComparison.Ordinal);
        Assert.Contains("CreatedBy", onboarding, StringComparison.Ordinal);
        Assert.Contains("ApplicationError.Forbidden", snapshot, StringComparison.Ordinal);
        Assert.Contains("ApplicationError.Forbidden", deployment, StringComparison.Ordinal);
        Assert.Contains("ApplicationError.Forbidden", onboarding, StringComparison.Ordinal);
    }

    [Fact]
    public void AcOpidDesktopSetsOperationIdImmediatelyAfterStart()
    {
        string root = RepoRoot();
        string deployment = File.ReadAllText(Path.Combine(
            root,
            "src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs"));
        string onboarding = File.ReadAllText(Path.Combine(
            root,
            "src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs"));

        Assert.Contains(
            "retain OperationId immediately after Start, before Watch can fail",
            deployment,
            StringComparison.Ordinal);
        Assert.Contains(
            "retain OperationId immediately after Start, before Watch can fail",
            onboarding,
            StringComparison.Ordinal);
        Assert.Contains("OperationId = DesktopProtoUuid.ToGuid(started.OperationId);", deployment, StringComparison.Ordinal);
        Assert.Contains("OperationId = DesktopProtoUuid.ToGuid(started.OperationId);", onboarding, StringComparison.Ordinal);
        Assert.Contains("ProgressLines.Add(FormatProgress(progress));", deployment, StringComparison.Ordinal);
        Assert.Contains("ProgressLines.Add(FormatProgress(progress));", onboarding, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcHubPrunesAfterTerminalWatchCompletes()
    {
        CaptureProgressHub capture = new();
        Guid captureOp = capture.Begin(Guid.NewGuid(), "owner-a");
        capture.Publish(captureOp, CaptureStage.Completed, captureId: Guid.NewGuid());
        Assert.True(capture.Contains(captureOp));
        await foreach (CaptureProgress _ in capture.WatchAsync(captureOp, CancellationToken.None))
        {
        }

        Assert.False(capture.Contains(captureOp));

        DeploymentProgressHub deployment = new();
        Guid depOp = Guid.NewGuid();
        deployment.Ensure(depOp, "owner-a");
        deployment.Publish(depOp, DomainDeploymentState.Committed);
        Assert.True(deployment.Contains(depOp));
        await foreach (DeploymentProgress _ in deployment.WatchAsync(depOp, CancellationToken.None))
        {
        }

        Assert.False(deployment.Contains(depOp));

        OnboardingProgressHub onboarding = new();
        Guid onbOp = Guid.NewGuid();
        onboarding.Ensure(onbOp, "owner-a");
        onboarding.Publish(onbOp, DomainOnboardingState.Committed);
        Assert.True(onboarding.Contains(onbOp));
        await foreach (OnboardingProgress _ in onboarding.WatchAsync(onbOp, CancellationToken.None))
        {
        }

        Assert.False(onboarding.Contains(onbOp));
    }

    [Fact]
    public void AcDocsQueueAdvancesPastW7237()
    {
        string root = RepoRoot();
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("§3.C NEXT = none (queue exhausted; W7-392 #1191 DONE)", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-26 COMPLETE", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "W7-236 | [#879](https://github.com/sesquicadaver/MTDirector/issues/879) | AUDIT-INT-01 — FastTrack topology / verification session disposal / progress Watch auth hubs | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("W7-236 (#879) DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("PLAN-26 COMPLETE", plan26, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none (queue exhausted; W7-392 #1191 DONE)", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-237 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-236 AUDIT-INT-01", continuous, StringComparison.Ordinal);
        Assert.Contains("**DONE**", continuous, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-236 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AuditInt01W7236LivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("W7-236", changelog, StringComparison.Ordinal);
        Assert.Contains("AUDIT-INT-01", changelog, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none (queue exhausted; W7-392 #1191 DONE)", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none (queue exhausted; W7-392 #1191 DONE)", readme, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            root,
            "tests/Mfc.UnitTests/Release/AuditInt01W7236LivingSpecTests.cs")));
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
