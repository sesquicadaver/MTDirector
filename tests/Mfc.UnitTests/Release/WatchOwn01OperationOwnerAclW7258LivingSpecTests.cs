using Mfc.Application.Common;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller.Grpc;
using Xunit;
using DomainDeploymentState = Mfc.Domain.Deployment.DeploymentOperationState;
using DomainOnboardingState = Mfc.Domain.Onboarding.OnboardingOperationState;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-258 / WATCH-OWN-01: Watch RPCs bind to operation owner beyond Read permission;
/// hubs retain OwnerActor; Deployment/Onboarding durable CreatedBy is equivalent ACL.
/// </summary>
public sealed class WatchOwn01OperationOwnerAclW7258LivingSpecTests
{
    [Fact]
    public void Ac1HubsBindOwnerActorAndFailClosedWithoutMatch()
    {
        CaptureProgressHub capture = new();
        Guid captureOp = capture.Begin(Guid.NewGuid(), "owner-a");
        Assert.True(capture.TryGetOwnerActor(captureOp, out string captureOwner));
        Assert.Equal("owner-a", captureOwner);
        Assert.False(string.Equals(captureOwner, "owner-b", StringComparison.Ordinal));

        DeploymentProgressHub deployment = new();
        Guid depOp = Guid.NewGuid();
        deployment.Ensure(depOp, "owner-a");
        deployment.Publish(depOp, DomainDeploymentState.Created);
        Assert.True(deployment.TryGetOwnerActor(depOp, out string depOwner));
        Assert.Equal("owner-a", depOwner);
        deployment.Ensure(depOp, "owner-b");
        Assert.True(deployment.TryGetOwnerActor(depOp, out string depOwnerAfter));
        Assert.Equal("owner-a", depOwnerAfter);

        OnboardingProgressHub onboarding = new();
        Guid onbOp = Guid.NewGuid();
        onboarding.Ensure(onbOp, "owner-a");
        onboarding.Publish(onbOp, DomainOnboardingState.Created);
        Assert.True(onboarding.TryGetOwnerActor(onbOp, out string onbOwner));
        Assert.Equal("owner-a", onbOwner);
    }

    [Fact]
    public void Ac2GrpcWatchAuthComparesOwnerBeyondReadPermission()
    {
        string root = RepoRoot();
        string snapshot = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/SnapshotGrpcService.cs"));
        string deployment = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/DeploymentGrpcService.cs"));
        string onboarding = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/OnboardingGrpcService.cs"));
        string captureHub = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/CaptureProgressHub.cs"));
        string deploymentHub = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/DeploymentProgressHub.cs"));
        string onboardingHub = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/OnboardingProgressHub.cs"));

        Assert.Contains("Begin(deviceId, ResolveActor(context))", snapshot, StringComparison.Ordinal);
        Assert.Contains("TryGetOwnerActor(operationId, out string ownerActor)", snapshot, StringComparison.Ordinal);
        Assert.Contains("Watch requires the operation owner.", snapshot, StringComparison.Ordinal);
        Assert.Contains("ApplicationPermissions.SnapshotRead", snapshot, StringComparison.Ordinal);

        Assert.Contains("_progress.Ensure(view.OperationId, actor)", deployment, StringComparison.Ordinal);
        Assert.Contains("TryGetOwnerActor(operationId, out string hubOwner)", deployment, StringComparison.Ordinal);
        Assert.Contains("ActorKey.FromActor(actor)", deployment, StringComparison.Ordinal);
        Assert.Contains("CreatedBy.Value", deployment, StringComparison.Ordinal);
        Assert.Contains("Watch requires the operation owner.", deployment, StringComparison.Ordinal);
        Assert.Contains("ApplicationPermissions.DeploymentRead", deployment, StringComparison.Ordinal);

        Assert.Contains("_progress.Ensure(view.OperationId, actor)", onboarding, StringComparison.Ordinal);
        Assert.Contains("TryGetOwnerActor(operationId, out string hubOwner)", onboarding, StringComparison.Ordinal);
        Assert.Contains("ActorKey.FromActor(actor)", onboarding, StringComparison.Ordinal);
        Assert.Contains("CreatedBy.Value", onboarding, StringComparison.Ordinal);
        Assert.Contains("Watch requires the operation owner.", onboarding, StringComparison.Ordinal);
        Assert.Contains("ApplicationPermissions.OnboardingRead", onboarding, StringComparison.Ordinal);

        Assert.Contains("TryGetOwnerActor", captureHub, StringComparison.Ordinal);
        Assert.Contains("OwnerActor", captureHub, StringComparison.Ordinal);
        Assert.Contains("TryGetOwnerActor", deploymentHub, StringComparison.Ordinal);
        Assert.Contains("BindOwnerIfAbsent", deploymentHub, StringComparison.Ordinal);
        Assert.Contains("TryGetOwnerActor", onboardingHub, StringComparison.Ordinal);
        Assert.Contains("BindOwnerIfAbsent", onboardingHub, StringComparison.Ordinal);

        Guid durableKey = ActorKey.FromActor("owner-a");
        Assert.NotEqual(Guid.Empty, durableKey);
        Assert.Equal(durableKey, ActorKey.FromActor("owner-a"));
        Assert.NotEqual(durableKey, ActorKey.FromActor("owner-b"));
    }

    [Fact]
    public void Ac3DocsAdvanceNextToWatchBp01Seed()
    {
        string root = RepoRoot();
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan30 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-30-watch-owner-acl-hub-backpressure.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));

        Assert.Contains(
            "W7-258 | [#922](https://github.com/sesquicadaver/MTDirector/issues/922) | WATCH-OWN-01 — Bind Watch RPCs to operation owner beyond Read permission | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-337 (#1080)", roadmap, StringComparison.Ordinal);
        Assert.Contains("WATCH-OWN-01", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-258", plan30, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-337 (#1080)", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-258", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-337 (#1080)", continuous, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-258 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("WatchOwn01OperationOwnerAclW7258", testing, StringComparison.Ordinal);
        Assert.Contains("W7-258", changelog, StringComparison.Ordinal);
        Assert.Contains("WATCH-OWN-01", changelog, StringComparison.Ordinal);
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
