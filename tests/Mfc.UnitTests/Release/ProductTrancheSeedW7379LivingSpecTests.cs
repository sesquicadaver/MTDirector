using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-379: PLAN-58 COMPLETE; known-limitations / queue seed locked PLAN-59 inventory (W7-380)
/// and follow-up seed W7-381 after DESK-PANEL-FAULT-01.
/// </summary>
public sealed class ProductTrancheSeedW7379LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan59AfterPlan58Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan58 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-58-desktop-panel-status-fault-text.md"));
        string plan59 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-59-snapshot-failed-errortext-correlation.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-379 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-58 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-59", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-380", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-381", limitations, StringComparison.Ordinal);
        Assert.Contains("SNAP-ERRTEXT-CORR-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-380**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-379 | [#1163](https://github.com/sesquicadaver/MTDirector/issues/1163) | Seed next after DESK-PANEL-FAULT-01 (PLAN-58 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-380 | [#1167](https://github.com/sesquicadaver/MTDirector/issues/1167) | PLAN-59 — Inventory Snapshot failed-stage ErrorText correlation | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-381 | [#1168](https://github.com/sesquicadaver/MTDirector/issues/1168) | Seed first PLAN-59 atomic row after inventory → SNAP-ERRTEXT-CORR-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-58 COMPLETE", plan58, StringComparison.Ordinal);
        Assert.Contains("W7-379 (#1163) DONE", plan58, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", plan58, StringComparison.Ordinal);
        Assert.Contains("plan-59-snapshot-failed-errortext-correlation.md", plan, StringComparison.Ordinal);
        Assert.Contains("plan-59-snapshot-failed-errortext-correlation.md", docsIndex, StringComparison.Ordinal);

        Assert.Contains("PLAN-59", plan, StringComparison.Ordinal);
        Assert.Contains("W7-380", plan, StringComparison.Ordinal);
        Assert.Contains("W7-379 (#1163) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", plan, StringComparison.Ordinal);
        Assert.Contains("SNAP-ERRTEXT-CORR-01", plan59, StringComparison.Ordinal);
        Assert.Contains("SanitizedDetail", plan59, StringComparison.Ordinal);
        Assert.Contains("6b0b3f95", plan59, StringComparison.Ordinal);
        Assert.Contains("W7-380", plan59, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", plan59, StringComparison.Ordinal);

        Assert.Contains(
            "ErrorText = FormatCaptureProgress(failed)",
            viewer,
            StringComparison.Ordinal);
        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Drift load failed. {fault}\"", drift, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("SnapshotViewerViewModel.FormatCaptureProgress(progress)", node, StringComparison.Ordinal);
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
