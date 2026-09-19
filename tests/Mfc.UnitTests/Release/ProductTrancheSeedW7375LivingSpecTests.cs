using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-375: PLAN-57 COMPLETE; known-limitations / queue seed locked PLAN-58 inventory (W7-376)
/// and follow-up seed W7-377 after DESK-VRRP-PROG-01.
/// </summary>
public sealed class ProductTrancheSeedW7375LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan58AfterPlan57Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan57 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-57-vrrp-capture-progress-fault-text.md"));
        string plan58 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-58-desktop-panel-status-fault-text.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-375 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-57 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-58", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-376", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-377", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-PANEL-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-376**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-375 | [#1155](https://github.com/sesquicadaver/MTDirector/issues/1155) | Seed next after DESK-VRRP-PROG-01 (PLAN-57 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-376 | [#1159](https://github.com/sesquicadaver/MTDirector/issues/1159) | PLAN-58 — Inventory Desktop panel status fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-377 | [#1160](https://github.com/sesquicadaver/MTDirector/issues/1160) | Seed first PLAN-58 atomic row after inventory → DESK-PANEL-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-388 (#1183)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-57 COMPLETE", plan57, StringComparison.Ordinal);
        Assert.Contains("W7-375 (#1155) DONE", plan57, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-388 (#1183)", plan57, StringComparison.Ordinal);
        Assert.Contains("plan-58-desktop-panel-status-fault-text.md", plan, StringComparison.Ordinal);
        Assert.Contains("plan-58-desktop-panel-status-fault-text.md", docsIndex, StringComparison.Ordinal);

        Assert.Contains("PLAN-58", plan, StringComparison.Ordinal);
        Assert.Contains("W7-376", plan, StringComparison.Ordinal);
        Assert.Contains("W7-375 (#1155) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-388 (#1183)", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-PANEL-FAULT-01", plan58, StringComparison.Ordinal);
        Assert.Contains("Drift load failed", plan58, StringComparison.Ordinal);
        Assert.Contains("55765d52", plan58, StringComparison.Ordinal);
        Assert.Contains("W7-376", plan58, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-388 (#1183)", plan58, StringComparison.Ordinal);

        Assert.Contains("SnapshotViewerViewModel.FormatCaptureProgress(progress)", node, StringComparison.Ordinal);
        Assert.Contains("DeploymentReadinessText = \"GetNodeWorkflow failed.\"", node, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Drift load failed.\"", drift, StringComparison.Ordinal);
        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
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
