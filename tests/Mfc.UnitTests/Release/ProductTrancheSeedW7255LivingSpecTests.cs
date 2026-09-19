using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-255: PLAN-29 COMPLETE; known-limitations / queue seed locked PLAN-30 inventory (W7-256)
/// and follow-up seed W7-257 after DESK-CONN-RECONNECT-01.
/// Historical: inventory DONE; OWN/BP opened; seed W7-257 DONE; NEXT advanced to W7-258 WATCH-OWN-01.
/// </summary>
public sealed class ProductTrancheSeedW7255LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan30AfterPlan29Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan29 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-29-desktop-connection-health-reconnect.md"));
        string plan30 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-30-watch-owner-acl-hub-backpressure.md"));
        string captureHub = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/CaptureProgressHub.cs"));
        string snapshotGrpc = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/SnapshotGrpcService.cs"));

        Assert.Contains("Intentional residual (W7-255 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-29 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-30", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-256", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-257", limitations, StringComparison.Ordinal);
        Assert.Contains("WATCH-OWN-01", limitations, StringComparison.Ordinal);
        Assert.Contains("WATCH-BP-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-256**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-255 | [#916](https://github.com/sesquicadaver/MTDirector/issues/916) | Seed next after DESK-CONN-RECONNECT-01 (PLAN-29 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-256 | [#919](https://github.com/sesquicadaver/MTDirector/issues/919) | PLAN-30 — Inventory Watch operation-owner ACL / hub slow-subscriber backpressure (AUDIT §19 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-257 | [#920](https://github.com/sesquicadaver/MTDirector/issues/920) | Seed first PLAN-30 atomic row after inventory → WATCH-OWN-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-351 (#1107)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-29 COMPLETE", plan29, StringComparison.Ordinal);
        Assert.Contains("W7-255 DONE", plan29, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-351 (#1107)", plan29, StringComparison.Ordinal);

        Assert.Contains("PLAN-30", plan, StringComparison.Ordinal);
        Assert.Contains("W7-256", plan, StringComparison.Ordinal);
        Assert.Contains("W7-255 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-351 (#1107)", plan, StringComparison.Ordinal);

        Assert.Contains("WATCH-OWN-01", plan30, StringComparison.Ordinal);
        Assert.Contains("WATCH-BP-01", plan30, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan30, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-351 (#1107)", plan30, StringComparison.Ordinal);
        Assert.Contains("EnsureWatchAuthorizedAsync", plan30, StringComparison.Ordinal);
        Assert.Contains("CreateUnbounded", plan30, StringComparison.Ordinal);
        Assert.Contains("EnsureWatchAuthorizedAsync", snapshotGrpc, StringComparison.Ordinal);
        Assert.Contains("Channel.CreateBounded", captureHub, StringComparison.Ordinal);
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
