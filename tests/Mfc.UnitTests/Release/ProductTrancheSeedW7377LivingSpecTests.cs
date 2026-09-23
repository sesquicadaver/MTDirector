using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-377: known-limitations / queue seed locked DESK-PANEL-FAULT-01 (W7-378) after PLAN-58 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7377LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskPanelFault01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan58 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-58-desktop-panel-status-fault-text.md"));
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-377 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-PANEL-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-378", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-379", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-378**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-377 | [#1160](https://github.com/sesquicadaver/MTDirector/issues/1160) | Seed first PLAN-58 atomic row after inventory → DESK-PANEL-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-378 | [#1162](https://github.com/sesquicadaver/MTDirector/issues/1162) | DESK-PANEL-FAULT-01 — Show RPC fault text on panel status lines | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-379 | [#1163](https://github.com/sesquicadaver/MTDirector/issues/1163) | Seed next after DESK-PANEL-FAULT-01 (PLAN-58 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-405 (#1212)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-377", plan, StringComparison.Ordinal);
        Assert.Contains("W7-378", plan, StringComparison.Ordinal);
        Assert.Contains("W7-379", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-PANEL-FAULT-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-405 (#1212)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-377 (#1160) DONE", plan58, StringComparison.Ordinal);
        Assert.Contains("DESK-PANEL-FAULT-01", plan58, StringComparison.Ordinal);
        Assert.Contains("W7-378", plan58, StringComparison.Ordinal);
        Assert.Contains("W7-379", plan58, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-405 (#1212)", plan58, StringComparison.Ordinal);

        Assert.Contains("StatusText = \"Drift load failed.\"", drift, StringComparison.Ordinal);
        Assert.Contains("DeploymentReadinessText = \"GetNodeWorkflow failed.\"", node, StringComparison.Ordinal);
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
