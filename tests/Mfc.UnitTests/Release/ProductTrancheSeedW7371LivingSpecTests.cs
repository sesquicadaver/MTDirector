using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-371: PLAN-56 COMPLETE; known-limitations / queue seed locked PLAN-57 inventory (W7-372)
/// and follow-up seed W7-373 after DESK-VRRP-FAULT-01.
/// </summary>
public sealed class ProductTrancheSeedW7371LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan57AfterPlan56Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan56 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-56-vrrp-pair-status-fault-text.md"));
        string plan57 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-57-vrrp-capture-progress-fault-text.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-371 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-56 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-57", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-372", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-373", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-PROG-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-372**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-371 | [#1147](https://github.com/sesquicadaver/MTDirector/issues/1147) | Seed next after DESK-VRRP-FAULT-01 (PLAN-56 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-372 | [#1151](https://github.com/sesquicadaver/MTDirector/issues/1151) | PLAN-57 — Inventory VRRP capture-progress fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-373 | [#1152](https://github.com/sesquicadaver/MTDirector/issues/1152) | Seed first PLAN-57 atomic row after inventory → DESK-VRRP-PROG-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-56 COMPLETE", plan56, StringComparison.Ordinal);
        Assert.Contains("W7-371 (#1147) DONE", plan56, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", plan56, StringComparison.Ordinal);
        Assert.Contains("plan-57-vrrp-capture-progress-fault-text.md", plan, StringComparison.Ordinal);
        Assert.Contains("plan-57-vrrp-capture-progress-fault-text.md", docsIndex, StringComparison.Ordinal);

        Assert.Contains("PLAN-57", plan, StringComparison.Ordinal);
        Assert.Contains("W7-372", plan, StringComparison.Ordinal);
        Assert.Contains("W7-371 (#1147) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-PROG-01", plan57, StringComparison.Ordinal);
        Assert.Contains("progress.Stage", plan57, StringComparison.Ordinal);
        Assert.Contains("218cdba7", plan57, StringComparison.Ordinal);
        Assert.Contains("W7-372", plan57, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", plan57, StringComparison.Ordinal);

        Assert.Contains("VrrpPairStatusText = $\"{memberName}: {SnapshotViewerViewModel.FormatCaptureProgress(progress)}\"", node, StringComparison.Ordinal);
        Assert.Contains("throw new InvalidOperationException(", node, StringComparison.Ordinal);
        Assert.DoesNotContain("progress.Error", node, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains("gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}", mapper, StringComparison.Ordinal);
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
