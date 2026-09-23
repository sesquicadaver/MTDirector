using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-383: PLAN-59 COMPLETE; known-limitations / queue seed locked PLAN-60 inventory (W7-384)
/// and follow-up seed W7-385 after SNAP-ERRTEXT-CORR-01.
/// </summary>
public sealed class ProductTrancheSeedW7383LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan60AfterPlan59Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan59 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-59-snapshot-failed-errortext-correlation.md"));
        string plan60 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-60-desktop-service-rpc-fault-text.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string viewerService = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotViewerService.cs"));
        string diff = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotDiffService.cs"));
        string inventory = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/InventoryTreeService.cs"));
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-383 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-59 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-60", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-384", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-385", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-SVC-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-384**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-383 | [#1171](https://github.com/sesquicadaver/MTDirector/issues/1171) | Seed next after SNAP-ERRTEXT-CORR-01 (PLAN-59 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-384 | [#1175](https://github.com/sesquicadaver/MTDirector/issues/1175) | PLAN-60 — Inventory Desktop service RPC fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-385 | [#1176](https://github.com/sesquicadaver/MTDirector/issues/1176) | Seed first PLAN-60 atomic row after inventory → DESK-SVC-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-59 COMPLETE", plan59, StringComparison.Ordinal);
        Assert.Contains("W7-383 (#1171) DONE", plan59, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", plan59, StringComparison.Ordinal);
        Assert.Contains("plan-60-desktop-service-rpc-fault-text.md", plan, StringComparison.Ordinal);
        Assert.Contains("plan-60-desktop-service-rpc-fault-text.md", docsIndex, StringComparison.Ordinal);

        Assert.Contains("PLAN-60", plan, StringComparison.Ordinal);
        Assert.Contains("W7-384", plan, StringComparison.Ordinal);
        Assert.Contains("W7-383 (#1171) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-SVC-FAULT-01", plan60, StringComparison.Ordinal);
        Assert.Contains("ex.Message", plan60, StringComparison.Ordinal);
        Assert.Contains("10adc5ba", plan60, StringComparison.Ordinal);
        Assert.Contains("W7-384", plan60, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", plan60, StringComparison.Ordinal);

        Assert.Equal(
            6,
            Count(viewerService, "Error = DesktopRpcFaultText.Format(ex)")
            + Count(diff, "Error = DesktopRpcFaultText.Format(ex)")
            + Count(inventory, "Error = DesktopRpcFaultText.Format(ex)"));
        Assert.Equal(
            0,
            Count(viewerService, "Error = ex.Message")
            + Count(diff, "Error = ex.Message")
            + Count(inventory, "Error = ex.Message"));
        Assert.Contains("ErrorText = FormatCaptureProgress(failed)", viewer, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Drift load failed. {fault}\"", drift, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("SnapshotViewerViewModel.FormatCaptureProgress(progress)", node, StringComparison.Ordinal);
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int index = 0;
        while (true)
        {
            int found = text.IndexOf(value, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + value.Length;
        }
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
