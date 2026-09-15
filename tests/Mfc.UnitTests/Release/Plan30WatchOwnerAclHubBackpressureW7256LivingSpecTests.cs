using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-256: PLAN-30 inventory documents ranked WATCH-OWN/BP rows and seeds WATCH-OWN-01.</summary>
public sealed class Plan30WatchOwnerAclHubBackpressureW7256LivingSpecTests
{
    [Fact]
    public void Ac1Plan30InventoryDocumentsRankedRowsAndSeedsWatchOwn01()
    {
        string root = RepoRoot();
        string plan30 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-30-watch-owner-acl-hub-backpressure.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string snapshotGrpc = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/SnapshotGrpcService.cs"));
        string deploymentGrpc = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/DeploymentGrpcService.cs"));
        string onboardingGrpc = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/OnboardingGrpcService.cs"));
        string captureHub = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/CaptureProgressHub.cs"));
        string deploymentHub = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/DeploymentProgressHub.cs"));
        string onboardingHub = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/OnboardingProgressHub.cs"));

        Assert.Contains("PLAN-30 — Watch operation-owner ACL / hub slow-subscriber backpressure", plan30, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan30, StringComparison.Ordinal);
        Assert.Contains("WATCH-OWN-01", plan30, StringComparison.Ordinal);
        Assert.Contains("WATCH-BP-01", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-258", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-259", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-257", plan30, StringComparison.Ordinal);
        Assert.Contains("EnsureWatchAuthorizedAsync", plan30, StringComparison.Ordinal);
        Assert.Contains("CreateUnbounded", plan30, StringComparison.Ordinal);
        Assert.Contains("CreatedBy", plan30, StringComparison.Ordinal);
        Assert.Contains("892a073", plan30, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-267 (#940)", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-257 (#920) DONE", plan30, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-256 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("WATCH-OWN-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-258", limitations, StringComparison.Ordinal);

        Assert.Contains("WATCH-OWN-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-258", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "W7-256 | [#919](https://github.com/sesquicadaver/MTDirector/issues/919) | PLAN-30 — Inventory Watch operation-owner ACL / hub slow-subscriber backpressure (AUDIT §19 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-257 | [#920](https://github.com/sesquicadaver/MTDirector/issues/920) | Seed first PLAN-30 atomic row after inventory → WATCH-OWN-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-258 | [#922](https://github.com/sesquicadaver/MTDirector/issues/922) | WATCH-OWN-01 — Bind Watch RPCs to operation owner beyond Read permission | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-259 | [#923](https://github.com/sesquicadaver/MTDirector/issues/923) | Seed next PLAN-30 row after WATCH-OWN-01 → WATCH-BP-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-260 | [#927](https://github.com/sesquicadaver/MTDirector/issues/927) | WATCH-BP-01 — Bounded ProgressHub subscriber channels / slow-subscriber backpressure + live `_history` cap | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-267 (#940)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-257", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-258", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-30", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-30-watch-owner-acl-hub-backpressure.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-30-watch-owner-acl-hub-backpressure.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan30WatchOwnerAclHubBackpressureW7256", testing, StringComparison.Ordinal);

        Assert.Contains("EnsureWatchAuthorizedAsync", snapshotGrpc, StringComparison.Ordinal);
        Assert.Contains("SnapshotRead", snapshotGrpc, StringComparison.Ordinal);
        Assert.Contains("EnsureWatchAuthorizedAsync", deploymentGrpc, StringComparison.Ordinal);
        Assert.Contains("DeploymentRead", deploymentGrpc, StringComparison.Ordinal);
        Assert.Contains("EnsureWatchAuthorizedAsync", onboardingGrpc, StringComparison.Ordinal);
        Assert.Contains("OnboardingRead", onboardingGrpc, StringComparison.Ordinal);
        Assert.Contains("Channel.CreateBounded", captureHub, StringComparison.Ordinal);
        Assert.Contains("Channel.CreateBounded", deploymentHub, StringComparison.Ordinal);
        Assert.Contains("Channel.CreateBounded", onboardingHub, StringComparison.Ordinal);
        Assert.Contains("_history", captureHub, StringComparison.Ordinal);
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
